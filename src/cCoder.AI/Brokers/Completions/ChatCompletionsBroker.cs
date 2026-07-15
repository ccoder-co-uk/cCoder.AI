using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Completions;

public class ChatCompletionsBroker(HttpClient httpClient) : IChatCompletionsBroker
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<CompletionResponse> PostChatCompletionAsync(
        string providerName,
        AICompletionProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource requestTimeout = CreateRequestTimeout(
            providerConfiguration.TimeoutSeconds,
            cancellationToken);

        string serializedRequest = SerializeRequest(providerConfiguration, request);
        int maxAttempts = Math.Max(1, providerConfiguration.MaxRetryAttempts + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using HttpRequestMessage httpRequestMessage =
                new(HttpMethod.Post, providerConfiguration.Endpoint);
            ApplyAuthentication(providerConfiguration, httpRequestMessage);
            httpRequestMessage.Content =
                new StringContent(serializedRequest, Encoding.UTF8, "application/json");

            using HttpResponseMessage httpResponseMessage =
                await httpClient.SendAsync(httpRequestMessage, requestTimeout.Token);
            string rawContent =
                await httpResponseMessage.Content.ReadAsStringAsync(requestTimeout.Token);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                try
                {
                    return new CompletionResponse
                    {
                        Provider = providerName,
                        Model = request.Model,
                        Content = ExtractContent(providerConfiguration, rawContent),
                        RawContent = rawContent,
                    };
                }
                catch (InvalidOperationException) when (attempt < maxAttempts)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            Math.Max(1, providerConfiguration.RetryBaseDelayMilliseconds)
                            * Math.Pow(2, Math.Max(0, attempt - 1))),
                        requestTimeout.Token);
                    continue;
                }
            }

            if (attempt < maxAttempts && IsTransient(httpResponseMessage.StatusCode, rawContent))
            {
                TimeSpan retryDelay = ResolveRetryDelay(
                    httpResponseMessage,
                    attempt,
                    providerConfiguration.RetryBaseDelayMilliseconds);
                await Task.Delay(retryDelay, requestTimeout.Token);
                continue;
            }

            EnsureSuccessStatusCode(providerName, httpResponseMessage, rawContent);
        }

        throw new InvalidOperationException("The AI provider request ended without a response.");
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode, string rawContent) =>
        !rawContent.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)
        && (statusCode == System.Net.HttpStatusCode.RequestTimeout
            || statusCode == System.Net.HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500);

    private static TimeSpan ResolveRetryDelay(
        HttpResponseMessage response,
        int attempt,
        int retryBaseDelayMilliseconds)
    {
        if (response.Headers.TryGetValues("retry-after-ms", out IEnumerable<string>? millisecondValues)
            && int.TryParse(millisecondValues.FirstOrDefault(), out int retryAfterMilliseconds)
            && retryAfterMilliseconds > 0)
        {
            return TimeSpan.FromMilliseconds(retryAfterMilliseconds);
        }

        if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;

        if (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate)
        {
            TimeSpan dateDelay = retryDate - DateTimeOffset.UtcNow;
            if (dateDelay > TimeSpan.Zero)
                return dateDelay;
        }

        int baseDelay = Math.Max(1, retryBaseDelayMilliseconds);
        return TimeSpan.FromMilliseconds(baseDelay * Math.Pow(2, Math.Max(0, attempt - 1)));
    }

    private static CancellationTokenSource CreateRequestTimeout(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (timeoutSeconds > 0)
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        return requestTimeout;
    }

    private static void ApplyAuthentication(
        AICompletionProviderConfiguration providerConfiguration,
        HttpRequestMessage httpRequestMessage)
    {
        if (string.IsNullOrWhiteSpace(providerConfiguration.ApiKey))
        {
            return;
        }

        if (providerConfiguration.ApiKeyHeaderName.Equals(
            "Authorization",
            StringComparison.OrdinalIgnoreCase))
        {
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                providerConfiguration.ApiKeyScheme,
                providerConfiguration.ApiKey);

            return;
        }

        string headerValue = string.IsNullOrWhiteSpace(providerConfiguration.ApiKeyScheme)
            ? providerConfiguration.ApiKey
            : $"{providerConfiguration.ApiKeyScheme} {providerConfiguration.ApiKey}";

        httpRequestMessage.Headers.Add(providerConfiguration.ApiKeyHeaderName, headerValue);
    }

    private static string SerializeRequest(
        AICompletionProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request)
    {
        object payload = providerConfiguration.Mode switch
        {
            Models.Enums.AIProviderMode.OllamaApi => new
            {
                model = request.Model,
                messages = request.Messages,
                stream = false,
                think = false,
                tools = request.EnableShellTooling ? BuildOllamaShellTools() : null,
                options = new
                {
                    temperature = request.Temperature,
                }
            },

            _ => new
            {
                model = request.Model,
                messages = request.Messages,
                temperature = request.Temperature,
                stream = false,
            }
        };

        return JsonSerializer.Serialize(payload, JsonSerializerOptions);
    }

    private static object[] BuildOllamaShellTools() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "shell",
                description = "Run a local shell command and inspect the result.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        command = new
                        {
                            type = "string",
                            description = "The shell command to execute."
                        },
                        reason = new
                        {
                            type = "string",
                            description = "Why the shell command is needed."
                        }
                    },
                    required = new[] { "command" }
                }
            }
        }
    ];

    private static void EnsureSuccessStatusCode(
        string providerName,
        HttpResponseMessage httpResponseMessage,
        string rawContent)
    {
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"AI provider '{providerName}' returned {(int)httpResponseMessage.StatusCode} ({httpResponseMessage.ReasonPhrase}). Response: {rawContent}",
            null,
            httpResponseMessage.StatusCode);
    }

    private static string ExtractContent(
        AICompletionProviderConfiguration providerConfiguration,
        string rawContent) =>
        providerConfiguration.Mode switch
        {
            Models.Enums.AIProviderMode.OllamaApi => ExtractOllamaContent(rawContent),
            _ => ExtractOpenAICompatibleContent(rawContent),
        };

    private static string ExtractOpenAICompatibleContent(string rawContent)
    {
        JsonNode? jsonNode = JsonNode.Parse(rawContent);

        JsonNode? contentNode = jsonNode?["choices"]?[0]?["message"]?["content"];

        if (contentNode is JsonValue jsonValue)
        {
            return jsonValue.GetValue<string>();
        }

        if (contentNode is JsonArray jsonArray)
        {
            IEnumerable<string> parts = jsonArray
                .Select(node => node?["text"]?.GetValue<string>())
                .Where(text => string.IsNullOrWhiteSpace(text) is false)!;

            string joinedContent = string.Join(Environment.NewLine, parts);

            if (string.IsNullOrWhiteSpace(joinedContent) is false)
            {
                return joinedContent;
            }
        }

        throw new InvalidOperationException("The AI provider response did not contain a usable assistant message.");
    }

    private static string ExtractOllamaContent(string rawContent)
    {
        JsonNode? jsonNode = JsonNode.Parse(rawContent);
        JsonNode? messageNode = jsonNode?["message"];

        string content = messageNode?["content"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content) is false)
        {
            return content;
        }

        JsonNode? toolCallNode = messageNode?["tool_calls"]?[0];
        string command = ExtractOllamaCommand(toolCallNode?["function"]?["arguments"]);

        if (string.IsNullOrWhiteSpace(command) is false)
        {
            string reason = messageNode?["thinking"]?.GetValue<string>() ?? "Ollama requested a shell tool call.";

            return JsonSerializer.Serialize(
                new
                {
                    type = "tool",
                    tool = "shell",
                    command,
                    reason,
                },
                JsonSerializerOptions);
        }

        throw new InvalidOperationException("The Ollama response did not contain usable assistant content or a tool call.");
    }

    private static string ExtractOllamaCommand(JsonNode argumentsNode)
    {
        if (argumentsNode is null)
        {
            return string.Empty;
        }

        if (argumentsNode["command"] is JsonValue commandValue)
        {
            return commandValue.GetValue<string>();
        }

        if (argumentsNode["cmd"] is JsonValue cmdValue)
        {
            return cmdValue.GetValue<string>();
        }

        if (argumentsNode["cmd"] is not JsonArray cmdArray)
        {
            return string.Empty;
        }

        List<string> segments = cmdArray
            .Select(node => node?.GetValue<string>())
            .Where(segment => string.IsNullOrWhiteSpace(segment) is false)
            .ToList();

        if (segments.Count == 0)
        {
            return string.Empty;
        }

        if (segments.Count >= 3 &&
            (segments[0].Equals("bash", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("sh", StringComparison.OrdinalIgnoreCase)) &&
            segments[1].Equals("-lc", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(" ", segments.Skip(2));
        }

        if (segments.Count >= 2 &&
            (segments[0].Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("pwsh", StringComparison.OrdinalIgnoreCase)) &&
            (segments[1].Equals("-Command", StringComparison.OrdinalIgnoreCase) ||
             segments[1].Equals("/Command", StringComparison.OrdinalIgnoreCase)))
        {
            return string.Join(" ", segments.Skip(2));
        }

        if (segments.Count >= 2 &&
            segments[0].Equals("cmd", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("/c", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(" ", segments.Skip(2));
        }

        return string.Join(" ", segments);
    }
}
