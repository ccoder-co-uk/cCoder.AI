// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Text.Json;
using System.Runtime.CompilerServices;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Exposures;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Internal;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Foundations.Completions;

namespace cCoder.AI.Services.Orchestrations;

internal class AgentOrchestrationService(
    ICompletionProviderManager completionProviderService,
    IShellBroker shellBroker,
    AIConfiguration aiConfiguration)
    : IAgentOrchestrationService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request: request);

        int maxIterations = request.MaxIterations ?? aiConfiguration.Agent.MaxIterations;
        List<ChatCompletionMessage> messages =
        [
            new() { Role = "system", Content = BuildSystemPrompt(additionalSystemPrompt: request.SystemPrompt) },
            new() { Role = "user", Content = request.Instructions },
        ];

        List<AgentIterationResponse> iterationResponses = [];
        CompletionResponse? lastCompletionResponse = null;

        for (int iterationNumber = 1; iterationNumber <= maxIterations; iterationNumber++)
        {
            List<ChatCompletionMessage> requestMessages = CloneMessages(messages: messages);

            lastCompletionResponse = request.InputFilePaths?.Count > 0
                ? await completionProviderService.CompleteChatAsync(
                    provider: request.Provider,
                    model: request.Model,
                    messages: messages,
                    temperature: null,
                    enableShellTooling: true,
                    inputFilePaths: request.InputFilePaths,
                    cancellationToken: cancellationToken)
                : await completionProviderService.CompleteChatAsync(
                    provider: request.Provider,
                    model: request.Model,
                    messages: messages,
                    enableShellTooling: true,
                    cancellationToken: cancellationToken);

            if (!TryParseDirective(content: lastCompletionResponse.Content, directive: out AgentDirective agentDirective, error: out string parseError))
            {
                iterationResponses.Add(item: new AgentIterationResponse
                {
                    CompletionContent = lastCompletionResponse.Content,
                    IterationNumber = iterationNumber,
                    ParseError = parseError,
                    RequestMessages = requestMessages,
                    ResultType = AgentResultType.InvalidDirective,
                });

                messages.Add(item: new ChatCompletionMessage { Role = "assistant", Content = lastCompletionResponse.Content ?? string.Empty });
                messages.Add(item: new ChatCompletionMessage { Role = "user", Content = BuildDirectiveRepairMessage(lastCompletionResponse.Content, parseError) });

                continue;
            }

            if (agentDirective.Type.Equals(value: "final", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                iterationResponses.Add(item: new AgentIterationResponse
                {
                    CompletionContent = lastCompletionResponse.Content,
                    IterationNumber = iterationNumber,
                    RequestMessages = requestMessages,
                    ResultType = AgentResultType.Final,
                });

                return new AgentRunResponse
                {
                    FinalMessage = agentDirective.Message ?? string.Empty,
                    Iterations = iterationNumber,
                    Model = lastCompletionResponse.Model,
                    Provider = lastCompletionResponse.Provider,
                    Succeeded = true,
                    IterationResponses = iterationResponses,
                };
            }

            bool isLegacyToolDirective =
                agentDirective.Type.Equals(value: "tool", comparisonType: StringComparison.OrdinalIgnoreCase) &&
                agentDirective.Tool?.Equals(value: "shell", comparisonType: StringComparison.OrdinalIgnoreCase) == true;

            bool isShellDirective =
                agentDirective.Type.Equals(value: "shell", comparisonType: StringComparison.OrdinalIgnoreCase);

            if (!isLegacyToolDirective && !isShellDirective)
            {
                throw new InvalidOperationException(
message: "Agent loop expected a 'final' result or a 'shell' command request.");
            }

            if (string.IsNullOrWhiteSpace(value: agentDirective.Command))
            {
                throw new InvalidOperationException(message: "Shell command requests must include a command.");
            }

            using CancellationTokenSource linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);

            linkedCancellationTokenSource.CancelAfter(
delay: TimeSpan.FromSeconds(aiConfiguration.Agent.ShellCommandTimeoutSeconds));

            ToolExecutionResponse toolExecutionResponse = await shellBroker.ExecuteAsync(
command: agentDirective.Command,
workingDirectory: request.WorkingDirectory,
environmentVariables: request.EnvironmentVariables,
shellKind: request.ShellKind,
cancellationToken: linkedCancellationTokenSource.Token);

            iterationResponses.Add(item: new AgentIterationResponse
            {
                CompletionContent = lastCompletionResponse.Content,
                IterationNumber = iterationNumber,
                RequestMessages = requestMessages,
                ResultType = AgentResultType.Tool,
                ToolExecution = toolExecutionResponse,
                ToolName = "shell",
            });

            messages.Add(item: new ChatCompletionMessage { Role = "assistant", Content = lastCompletionResponse.Content });
            messages.Add(item: new ChatCompletionMessage { Role = "user", Content = BuildToolResultMessage(toolExecutionResponse) });
        }

        return new AgentRunResponse
        {
            FinalMessage = "Agent stopped because it reached the configured iteration limit.",
            Iterations = maxIterations,
            Model = lastCompletionResponse?.Model ?? string.Empty,
            Provider = lastCompletionResponse?.Provider ?? string.Empty,
            Succeeded = false,
            IterationResponses = iterationResponses,
        };
    }

    public async IAsyncEnumerable<AgentStreamTokenResponse> StreamAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new AgentStreamTokenResponse
        {
            Type = "start",
            Sequence = 0,
        };

        AgentRunResponse? agentRunResponse = null;
        Exception? capturedException = null;

        try
        {
            agentRunResponse = await RunAsync(request: request, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            capturedException = exception;
        }

        if (capturedException is not null)
        {
            yield return new AgentStreamTokenResponse
            {
                Type = "error",
                Sequence = 1,
                Content = capturedException.Message,
            };

            yield break;
        }

        int sequence = 1;

        foreach (string chunk in ChunkText(
content: agentRunResponse!.FinalMessage,
chunkCharacterCount: aiConfiguration.Agent.StreamingChunkCharacterCount))
        {
            yield return new AgentStreamTokenResponse
            {
                Type = "token",
                Sequence = sequence++,
                Content = chunk,
                Model = agentRunResponse.Model,
                Provider = agentRunResponse.Provider,
            };

            if (aiConfiguration.Agent.StreamingChunkDelayMilliseconds > 0)
            {
                await Task.Delay(
millisecondsDelay: aiConfiguration.Agent.StreamingChunkDelayMilliseconds,
cancellationToken: cancellationToken);
            }
        }

        yield return new AgentStreamTokenResponse
        {
            Type = "complete",
            Sequence = sequence,
            Content = agentRunResponse.FinalMessage,
            Model = agentRunResponse.Model,
            Provider = agentRunResponse.Provider,
            Completion = agentRunResponse,
        };
    }

    private string BuildSystemPrompt(string? additionalSystemPrompt)
    {
        string basePrompt = aiConfiguration.Agent.BasePrompt;

        if (string.IsNullOrWhiteSpace(value: additionalSystemPrompt))
        {
            return basePrompt;
        }

        return $"{basePrompt}{Environment.NewLine}{Environment.NewLine}{additionalSystemPrompt}";
    }

    private static string BuildToolResultMessage(ToolExecutionResponse toolExecutionResponse) =>
        $"""
        Shell command completed.
        Command: {toolExecutionResponse.Command}
        ExitCode: {toolExecutionResponse.ExitCode}
        WorkingDirectory: {toolExecutionResponse.WorkingDirectory}
        StandardOutput:
        {toolExecutionResponse.StandardOutput}

        StandardError:
        {toolExecutionResponse.StandardError}

        Return either another shell command request or a final answer as JSON.
        """;

    private static string BuildDirectiveRepairMessage(string? invalidContent, string parseError) =>
        "Your previous reply could not be parsed as the required JSON directive."
        + Environment.NewLine
        + $"Parse error: {parseError}"
        + Environment.NewLine
        + "Previous reply:"
        + Environment.NewLine
        + (invalidContent ?? string.Empty)
        + Environment.NewLine
        + Environment.NewLine
        + "Reply again with exactly one JSON object and no markdown fences."
        + Environment.NewLine
        + "Valid final example:"
        + Environment.NewLine
        + "{\"type\":\"final\",\"message\":\"...\"}"
        + Environment.NewLine
        + Environment.NewLine
        + "Valid shell command example:"
        + Environment.NewLine
        + "{\"type\":\"shell\",\"command\":\"...\",\"reason\":\"...\"}";

    private static bool TryParseDirective(string content, out AgentDirective directive, out string error)
    {
        directive = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value: content))
        {
            error = "Response content was empty.";
            return false;
        }

        string normalizedContent = content.Trim();

        if (normalizedContent.StartsWith(value: "```", comparisonType: StringComparison.Ordinal))
        {
            normalizedContent = normalizedContent
                .Replace(oldValue: "```json", newValue: string.Empty, comparisonType: StringComparison.OrdinalIgnoreCase)
                .Replace(oldValue: "```", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
                .Trim();
        }

        try
        {
            directive = JsonSerializer.Deserialize<AgentDirective>(
json: normalizedContent,
options: JsonSerializerOptions);

            if (directive is null)
            {
                error = "Agent response deserialized to null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value: directive.Type))
            {
                error = "Agent response did not include a directive type.";
                directive = null;
                return false;
            }

            return true;
        }
        catch (JsonException jsonException)
        {
            error = jsonException.Message;
            directive = null;
            return false;
        }
    }

    private static void ValidateRequest(AgentRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(value: request.Instructions))
        {
            throw new ArgumentException(message: "Instructions are required.", paramName: nameof(request));
        }
    }

    private static IEnumerable<string> ChunkText(string content, int chunkCharacterCount)
    {
        if (string.IsNullOrEmpty(value: content))
        {
            yield break;
        }

        int safeChunkCharacterCount = Math.Max(val1: 1, val2: chunkCharacterCount);

        for (int index = 0; index < content.Length; index += safeChunkCharacterCount)
        {
            int length = Math.Min(val1: safeChunkCharacterCount, val2: content.Length - index);
            yield return content.Substring(startIndex: index, length: length);
        }
    }

    private static List<ChatCompletionMessage> CloneMessages(IEnumerable<ChatCompletionMessage> messages) =>
        messages
            .Select(selector: message => new ChatCompletionMessage { Role = message.Role, Content = message.Content })
            .ToList();
}
