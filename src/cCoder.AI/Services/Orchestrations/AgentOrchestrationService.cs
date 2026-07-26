using System.Text.Json;
using System.Runtime.CompilerServices;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Internal;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Foundations.Completions;

namespace cCoder.AI.Services.Orchestrations;

public class AgentOrchestrationService(
    ICompletionProviderService completionProviderService,
    IShellBroker shellBroker,
    AIConfiguration aiConfiguration)
    : IAgentOrchestrationService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        int maxIterations = request.MaxIterations ?? aiConfiguration.Agent.MaxIterations;
        List<ChatCompletionMessage> messages =
        [
            new("system", BuildSystemPrompt(request.SystemPrompt)),
            new("user", request.Instructions),
        ];

        List<AgentIterationResponse> iterationResponses = [];
        CompletionResponse? lastCompletionResponse = null;

        for (int iterationNumber = 1; iterationNumber <= maxIterations; iterationNumber++)
        {
            List<ChatCompletionMessage> requestMessages = CloneMessages(messages);

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

            if (!TryParseDirective(lastCompletionResponse.Content, out AgentDirective agentDirective, out string parseError))
            {
                iterationResponses.Add(new AgentIterationResponse
                {
                    CompletionContent = lastCompletionResponse.Content,
                    IterationNumber = iterationNumber,
                    ParseError = parseError,
                    RequestMessages = requestMessages,
                    ResultType = AgentResultType.InvalidDirective,
                });

                messages.Add(new ChatCompletionMessage("assistant", lastCompletionResponse.Content ?? string.Empty));
                messages.Add(new ChatCompletionMessage("user", BuildDirectiveRepairMessage(lastCompletionResponse.Content, parseError)));

                continue;
            }

            if (agentDirective.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                iterationResponses.Add(new AgentIterationResponse
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
                agentDirective.Type.Equals("tool", StringComparison.OrdinalIgnoreCase) &&
                agentDirective.Tool?.Equals("shell", StringComparison.OrdinalIgnoreCase) == true;

            bool isShellDirective =
                agentDirective.Type.Equals("shell", StringComparison.OrdinalIgnoreCase);

            if (!isLegacyToolDirective && !isShellDirective)
            {
                throw new InvalidOperationException(
                    "Agent loop expected a 'final' result or a 'shell' command request.");
            }

            if (string.IsNullOrWhiteSpace(agentDirective.Command))
            {
                throw new InvalidOperationException("Shell command requests must include a command.");
            }

            using CancellationTokenSource linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            linkedCancellationTokenSource.CancelAfter(
                TimeSpan.FromSeconds(aiConfiguration.Agent.ShellCommandTimeoutSeconds));

            ToolExecutionResponse toolExecutionResponse = await shellBroker.ExecuteAsync(
                agentDirective.Command,
                request.WorkingDirectory,
                request.EnvironmentVariables,
                request.ShellKind,
                linkedCancellationTokenSource.Token);

            iterationResponses.Add(new AgentIterationResponse
            {
                CompletionContent = lastCompletionResponse.Content,
                IterationNumber = iterationNumber,
                RequestMessages = requestMessages,
                ResultType = AgentResultType.Tool,
                ToolExecution = toolExecutionResponse,
                ToolName = "shell",
            });

            messages.Add(new ChatCompletionMessage("assistant", lastCompletionResponse.Content));
            messages.Add(new ChatCompletionMessage("user", BuildToolResultMessage(toolExecutionResponse)));
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
            agentRunResponse = await RunAsync(request, cancellationToken);
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
            agentRunResponse!.FinalMessage,
            aiConfiguration.Agent.StreamingChunkCharacterCount))
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
                    aiConfiguration.Agent.StreamingChunkDelayMilliseconds,
                    cancellationToken);
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

        if (string.IsNullOrWhiteSpace(additionalSystemPrompt))
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

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Response content was empty.";
            return false;
        }

        string normalizedContent = content.Trim();

        if (normalizedContent.StartsWith("```", StringComparison.Ordinal))
        {
            normalizedContent = normalizedContent
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        try
        {
            directive = JsonSerializer.Deserialize<AgentDirective>(
                normalizedContent,
                JsonSerializerOptions);

            if (directive is null)
            {
                error = "Agent response deserialized to null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(directive.Type))
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
        if (string.IsNullOrWhiteSpace(request.Instructions))
        {
            throw new ArgumentException("Instructions are required.", nameof(request));
        }
    }

    private static IEnumerable<string> ChunkText(string content, int chunkCharacterCount)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        int safeChunkCharacterCount = Math.Max(1, chunkCharacterCount);

        for (int index = 0; index < content.Length; index += safeChunkCharacterCount)
        {
            int length = Math.Min(safeChunkCharacterCount, content.Length - index);
            yield return content.Substring(index, length);
        }
    }

    private static List<ChatCompletionMessage> CloneMessages(IEnumerable<ChatCompletionMessage> messages) =>
        messages
            .Select(message => new ChatCompletionMessage(message.Role, message.Content))
            .ToList();
}
