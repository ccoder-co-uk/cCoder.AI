// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Models.Internal;

namespace cCoder.AI.Dependencies;

internal sealed class ChatCompletionsDependency(
    IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient httpClient =
        httpClientFactory.CreateClient(name: "AI.Completions");

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<CompletionResponse> PostChatCompletionAsync(
        string providerName,
        AICompletionProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource requestTimeout = CreateRequestTimeout(
timeoutSeconds: providerConfiguration.TimeoutSeconds,
cancellationToken: cancellationToken);

        string serializedRequest = SerializeRequest(providerConfiguration: providerConfiguration, request: request);
        int maxAttempts = Math.Max(val1: 1, val2: providerConfiguration.MaxRetryAttempts + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using HttpRequestMessage httpRequestMessage =
                new(HttpMethod.Post, providerConfiguration.Endpoint);
            ApplyAuthentication(providerConfiguration: providerConfiguration, httpRequestMessage: httpRequestMessage);
            httpRequestMessage.Content =
                new StringContent(content: serializedRequest, encoding: Encoding.UTF8, mediaType: "application/json");

            using HttpResponseMessage httpResponseMessage =
                await httpClient.SendAsync(request: httpRequestMessage, cancellationToken: requestTimeout.Token);
            string rawContent =
                await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken: requestTimeout.Token);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                try
                {
                    return new CompletionResponse
                    {
                        Provider = providerName,
                        Model = request.Model,
                        Content = ExtractContent(providerConfiguration: providerConfiguration, rawContent: rawContent),
                        RawContent = rawContent,
                    };
                }
                catch (InvalidOperationException) when (attempt < maxAttempts)
                {
                    await Task.Delay(
delay: TimeSpan.FromMilliseconds(
                            Math.Max(1, providerConfiguration.RetryBaseDelayMilliseconds)
                            * Math.Pow(2, Math.Max(0, attempt - 1))),
cancellationToken: requestTimeout.Token);
                    continue;
                }
            }

            if (attempt < maxAttempts && IsTransient(statusCode: httpResponseMessage.StatusCode, rawContent: rawContent))
            {
                TimeSpan retryDelay = ResolveRetryDelay(
response: httpResponseMessage,
attempt: attempt,
retryBaseDelayMilliseconds: providerConfiguration.RetryBaseDelayMilliseconds);
                await Task.Delay(delay: retryDelay, cancellationToken: requestTimeout.Token);
                continue;
            }

            EnsureSuccessStatusCode(providerName: providerName, httpResponseMessage: httpResponseMessage, rawContent: rawContent);
        }

        throw new InvalidOperationException(message: "The AI provider request ended without a response.");
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode, string rawContent) =>
        !rawContent.Contains(value: "insufficient_quota", comparisonType: StringComparison.OrdinalIgnoreCase)
        && (statusCode == System.Net.HttpStatusCode.RequestTimeout
            || statusCode == System.Net.HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500);

    private static TimeSpan ResolveRetryDelay(
        HttpResponseMessage response,
        int attempt,
        int retryBaseDelayMilliseconds)
    {
        if (response.Headers.TryGetValues(name: "retry-after-ms", values: out IEnumerable<string>? millisecondValues)
            && int.TryParse(s: millisecondValues.FirstOrDefault(), result: out int retryAfterMilliseconds)
            && retryAfterMilliseconds > 0)
        {
            return TimeSpan.FromMilliseconds(milliseconds: retryAfterMilliseconds);
        }

        if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;

        if (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate)
        {
            TimeSpan dateDelay = retryDate - DateTimeOffset.UtcNow;
            if (dateDelay > TimeSpan.Zero)
                return dateDelay;
        }

        int baseDelay = Math.Max(val1: 1, val2: retryBaseDelayMilliseconds);
        return TimeSpan.FromMilliseconds(value: baseDelay * Math.Pow(2, Math.Max(0, attempt - 1)));
    }

    private static CancellationTokenSource CreateRequestTimeout(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);

        if (timeoutSeconds > 0)
            requestTimeout.CancelAfter(delay: TimeSpan.FromSeconds(timeoutSeconds));

        return requestTimeout;
    }

    private static void ApplyAuthentication(
        AICompletionProviderConfiguration providerConfiguration,
        HttpRequestMessage httpRequestMessage)
    {
        if (string.IsNullOrWhiteSpace(value: providerConfiguration.ApiKey))
        {
            return;
        }

        if (providerConfiguration.ApiKeyHeaderName.Equals(
value: "Authorization",
comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
scheme: providerConfiguration.ApiKeyScheme,
parameter: providerConfiguration.ApiKey);

            return;
        }

        string headerValue = string.IsNullOrWhiteSpace(value: providerConfiguration.ApiKeyScheme)
            ? providerConfiguration.ApiKey
            : $"{providerConfiguration.ApiKeyScheme} {providerConfiguration.ApiKey}";

        httpRequestMessage.Headers.Add(name: providerConfiguration.ApiKeyHeaderName, value: headerValue);
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
                messages = BuildOllamaMessages(request: request),
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
                messages = BuildOpenAICompatibleMessages(request: request),
                temperature = request.Temperature,
                stream = false,
            }
        };

        return JsonSerializer.Serialize(value: payload, options: JsonSerializerOptions);
    }

    private static IReadOnlyList<object> BuildOpenAICompatibleMessages(
        ProviderCompletionRequest request)
    {
        IReadOnlyList<InputFileContent> images = ReadImages(
            inputFilePaths: request.InputFilePaths,
            includeDataUrlPrefix: true);
        int targetIndex = LastUserMessageIndex(messages: request.Messages);

        return request.Messages.Select((message, index) => new
        {
            role = message.Role,
            content = index == targetIndex && images.Count > 0
                ? (object) BuildOpenAIContent(
                    text: message.Content,
                    images: images)
                : message.Content
        }).Cast<object>().ToList();
    }

    private static IReadOnlyList<object> BuildOllamaMessages(
        ProviderCompletionRequest request)
    {
        IReadOnlyList<InputFileContent> images = ReadImages(
            inputFilePaths: request.InputFilePaths,
            includeDataUrlPrefix: false);
        int targetIndex = LastUserMessageIndex(messages: request.Messages);

        return request.Messages.Select((message, index) => new
        {
            role = message.Role,
            content = message.Content,
            images = index == targetIndex && images.Count > 0
                ? images.Select(selector: image => image.Content).ToArray()
                : null
        }).Cast<object>().ToList();
    }

    private static IReadOnlyList<object> BuildOpenAIContent(
        string text,
        IReadOnlyList<InputFileContent> images)
    {
        List<object> content = [new { type = "text", text }];
        content.AddRange(images.Select(selector: image => (object) new
        {
            type = "image_url",
            image_url = new { url = image.Content }
        }));

        return content;
    }

    private static IReadOnlyList<InputFileContent> ReadImages(
        IReadOnlyList<string> inputFilePaths,
        bool includeDataUrlPrefix)
    {
        List<InputFileContent> images = [];

        foreach (string inputFilePath in inputFilePaths ?? [])
        {
            string fullPath = Path.GetFullPath(path: inputFilePath);
            string mediaType = ResolveImageMediaType(path: fullPath);

            if (!File.Exists(path: fullPath))
            {
                throw new FileNotFoundException(
                    message: "An AI input file could not be found.",
                    fileName: fullPath);
            }

            string base64 = Convert.ToBase64String(
                inArray: File.ReadAllBytes(path: fullPath));
            images.Add(new InputFileContent
            {
                Content = includeDataUrlPrefix
                    ? $"data:{mediaType};base64,{base64}"
                    : base64
            });
        }

        return images;
    }

    private static string ResolveImageMediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => throw new NotSupportedException(
                message: $"The configured chat-completion provider cannot receive '{Path.GetExtension(path)}' files. Supported inputs are PNG, JPEG, GIF, and WebP images.")
        };

    private static int LastUserMessageIndex(
        IReadOnlyList<ChatCompletionMessage> messages)
    {
        for (int index = messages.Count - 1; index >= 0; index--)
        {
            if (messages[index].Role.Equals(
                value: "user",
                comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
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
message: $"AI provider '{providerName}' returned {(int)httpResponseMessage.StatusCode} ({httpResponseMessage.ReasonPhrase}). Response: {rawContent}",
inner: null,
statusCode: httpResponseMessage.StatusCode);
    }

    private static string ExtractContent(
        AICompletionProviderConfiguration providerConfiguration,
        string rawContent) =>
        providerConfiguration.Mode switch
        {
            Models.Enums.AIProviderMode.OllamaApi => ExtractOllamaContent(rawContent: rawContent),
            _ => ExtractOpenAICompatibleContent(rawContent: rawContent),
        };

    private static string ExtractOpenAICompatibleContent(string rawContent)
    {
        JsonNode? jsonNode = JsonNode.Parse(json: rawContent);

        JsonNode? contentNode = jsonNode?["choices"]?[0]?["message"]?["content"];

        if (contentNode is JsonValue jsonValue)
        {
            return jsonValue.GetValue<string>();
        }

        if (contentNode is JsonArray jsonArray)
        {
            IEnumerable<string> parts = jsonArray
                .Select(selector: node => node?["text"]?.GetValue<string>())
                .Where(predicate: text => string.IsNullOrWhiteSpace(text) is false)!;

            string joinedContent = string.Join(separator: Environment.NewLine, values: parts);

            if (string.IsNullOrWhiteSpace(value: joinedContent) is false)
            {
                return joinedContent;
            }
        }

        throw new InvalidOperationException(message: "The AI provider response did not contain a usable assistant message.");
    }

    private static string ExtractOllamaContent(string rawContent)
    {
        JsonNode? jsonNode = JsonNode.Parse(json: rawContent);
        JsonNode? messageNode = jsonNode?["message"];

        string content = messageNode?["content"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value: content) is false)
        {
            return content;
        }

        JsonNode? toolCallNode = messageNode?["tool_calls"]?[0];
        string command = ExtractOllamaCommand(argumentsNode: toolCallNode?["function"]?["arguments"]);

        if (string.IsNullOrWhiteSpace(value: command) is false)
        {
            string reason = messageNode?["thinking"]?.GetValue<string>() ?? "Ollama requested a shell tool call.";

            return JsonSerializer.Serialize(
value: new
{
    type = "tool",
    tool = "shell",
    command,
    reason,
},
options: JsonSerializerOptions);
        }

        throw new InvalidOperationException(message: "The Ollama response did not contain usable assistant content or a tool call.");
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
            .Select(selector: node => node?.GetValue<string>())
            .Where(predicate: segment => string.IsNullOrWhiteSpace(segment) is false)
            .ToList();

        if (segments.Count == 0)
        {
            return string.Empty;
        }

        if (segments.Count >= 3 &&
            (segments[0].Equals(value: "bash", comparisonType: StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals(value: "sh", comparisonType: StringComparison.OrdinalIgnoreCase)) &&
            segments[1].Equals(value: "-lc", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(separator: " ", values: segments.Skip(2));
        }

        if (segments.Count >= 2 &&
            (segments[0].Equals(value: "powershell", comparisonType: StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals(value: "pwsh", comparisonType: StringComparison.OrdinalIgnoreCase)) &&
            (segments[1].Equals(value: "-Command", comparisonType: StringComparison.OrdinalIgnoreCase) ||
             segments[1].Equals(value: "/Command", comparisonType: StringComparison.OrdinalIgnoreCase)))
        {
            return string.Join(separator: " ", values: segments.Skip(2));
        }

        if (segments.Count >= 2 &&
            segments[0].Equals(value: "cmd", comparisonType: StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals(value: "/c", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(separator: " ", values: segments.Skip(2));
        }

        return string.Join(separator: " ", values: segments);
    }
}
