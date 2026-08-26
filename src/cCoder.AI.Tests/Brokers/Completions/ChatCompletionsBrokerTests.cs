// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Text;
using cCoder.AI.Brokers.Completions;
using cCoder.AI.Dependencies;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace cCoder.AI.Tests.Brokers.Completions;

public class ChatCompletionsBrokerTests
{
    [Fact]
    public async Task ShouldSendImagesAsOpenAIMultimodalMessageContentAsync()
    {
        // Given
        string imagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, [137, 80, 78, 71]);
        string requestBody = string.Empty;
        var handler = new StubHttpMessageHandler(responseFactory: request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"ready\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var broker = CreateBroker(httpClient: new HttpClient(handler));

        // When
        await broker.PostChatCompletionAsync(
            providerName: "OpenAI",
            providerConfiguration: new AICompletionProviderConfiguration
            {
                Mode = AIProviderMode.OpenAICompatible,
                Endpoint = "https://api.openai.test/v1/chat/completions"
            },
            request: new ProviderCompletionRequest
            {
                Model = "vision-model",
                Messages = [new ChatCompletionMessage
                {
                    Role = "user",
                    Content = "Inspect this image."
                }],
                InputFilePaths = [imagePath]
            });

        // Then
        JsonNode payload = JsonNode.Parse(requestBody)!;
        JsonArray content = payload["messages"]![0]!["content"]!.AsArray();
        content[0]!["type"]!.GetValue<string>().Should().Be("text");
        content[1]!["type"]!.GetValue<string>().Should().Be("image_url");
        content[1]!["image_url"]!["url"]!.GetValue<string>()
            .Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task ShouldSendImagesUsingNativeOllamaMessageFormatAsync()
    {
        // Given
        string imagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        string requestBody = string.Empty;
        var handler = new StubHttpMessageHandler(responseFactory: request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"ready\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var broker = CreateBroker(httpClient: new HttpClient(handler));

        // When
        await broker.PostChatCompletionAsync(
            providerName: "Ollama",
            providerConfiguration: new AICompletionProviderConfiguration
            {
                Mode = AIProviderMode.OllamaApi,
                Endpoint = "http://localhost:11434/api/chat"
            },
            request: new ProviderCompletionRequest
            {
                Model = "vision-model",
                Messages = [new ChatCompletionMessage
                {
                    Role = "user",
                    Content = "Inspect this image."
                }],
                InputFilePaths = [imagePath]
            });

        // Then
        JsonNode payload = JsonNode.Parse(requestBody)!;
        payload["messages"]![0]!["images"]![0]!.GetValue<string>()
            .Should().Be(Convert.ToBase64String([1, 2, 3]));
    }

    [Fact]
    public async Task ShouldRejectUnsupportedHostedProviderFileTypesAsync()
    {
        // Given
        string filePath = Path.GetTempFileName() + ".pdf";
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
        var broker = CreateBroker(httpClient: new HttpClient(
            new StubHttpMessageHandler(responseFactory: _ =>
                new HttpResponseMessage(HttpStatusCode.OK))));

        // When
        Func<Task> action = async () => await broker.PostChatCompletionAsync(
            providerName: "OpenAI",
            providerConfiguration: new AICompletionProviderConfiguration
            {
                Mode = AIProviderMode.OpenAICompatible,
                Endpoint = "https://api.openai.test/v1/chat/completions"
            },
            request: new ProviderCompletionRequest
            {
                Model = "vision-model",
                Messages = [new ChatCompletionMessage
                {
                    Role = "user",
                    Content = "Inspect this file."
                }],
                InputFilePaths = [filePath]
            });

        // Then
        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Supported inputs are PNG, JPEG, GIF, and WebP images*");
    }
    [Fact]
    public async Task ShouldRetrySuccessfulOllamaResponseWithoutAssistantContentAsync()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler(responseFactory: _ =>
        {
            requestCount++;
            string response = requestCount == 1
                ? "{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true}"
                : "{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"type\\\":\\\"final\\\",\\\"message\\\":\\\"ready\\\"}\"}}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        });
        var broker = CreateBroker(httpClient: new HttpClient(handler));
        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OllamaApi,
            Endpoint = "http://localhost:11434/api/chat",
            MaxRetryAttempts = 1,
            RetryBaseDelayMilliseconds = 1,
        };

        var response = await broker.PostChatCompletionAsync(
providerName: "Ollama",
providerConfiguration: providerConfiguration,
request: new ProviderCompletionRequest
{
    Model = "qwen3.5:4b",
    Messages = [new ChatCompletionMessage { Role = "user", Content = "Return structured JSON." }]
});

        requestCount.Should().Be(expected: 2);
        response.Content.Should().Contain(expected: "ready");
    }

    [Fact]
    public async Task ShouldUseCompatibleOllamaRequestOptionsAsync()
    {
        // Given
        string requestBody = string.Empty;
        var handler = new StubHttpMessageHandler(responseFactory: request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"type\\\":\\\"final\\\",\\\"message\\\":\\\"ready\\\"}\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var broker = CreateBroker(httpClient: new HttpClient(handler));
        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OllamaApi,
            Endpoint = "http://localhost:11434/api/chat",
        };

        // When
        await broker.PostChatCompletionAsync(
providerName: "Ollama",
providerConfiguration: providerConfiguration,
request: new ProviderCompletionRequest
{
    Model = "qwen3.5:4b",
    Messages = [new ChatCompletionMessage { Role = "user", Content = "Return structured JSON." }]
});

        // Then
        requestBody.Should().Contain(expected: "\"think\":false");
        requestBody.Should().NotContain(unexpected: "\"format\":\"json\"");
    }

    [Fact]
    public async Task ShouldMapOllamaToolCallToShellDirectiveAsync()
    {
        // Given
        string rawResponse =
            """
            {
              "message": {
                "role": "assistant",
                "content": "",
                "thinking": "Need to inspect helper scripts first.",
                "tool_calls": [
                  {
                    "function": {
                      "name": "tool",
                      "arguments": {
                        "cmd": ["bash", "-lc", "ls -R ../Shared/helper-scripts"]
                      }
                    }
                  }
                ]
              }
            }
            """;

        var handler = new StubHttpMessageHandler(responseFactory: _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawResponse, Encoding.UTF8, "application/json")
            });

        HttpClient httpClient = new(handler);
        var broker = CreateBroker(httpClient: httpClient);

        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OllamaApi,
            Endpoint = "http://localhost:11434/api/chat",
        };

        ProviderCompletionRequest request = new()
        {
            Model = "gpt-oss:20b",
            Messages = [new ChatCompletionMessage { Role = "user", Content = "Inspect the helper scripts." }],
            Temperature = 0.2,
        };

        // When
        var response = await broker.PostChatCompletionAsync(
providerName: "Ollama",
providerConfiguration: providerConfiguration,
request: request);

        // Then
        response.Content.Should().Contain(expected: "\"type\":\"tool\"");
        response.Content.Should().Contain(expected: "\"tool\":\"shell\"");
        response.Content.Should().Contain(expected: "ls -R ../Shared/helper-scripts");
    }

    [Fact]
    public async Task ShouldIncludeResponseBodyWhenProviderReturnsFailureAsync()
    {
        // Given
        const string rawResponse = "{\"error\":{\"message\":\"simulated provider failure\"}}";

        var handler = new StubHttpMessageHandler(responseFactory: _ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(rawResponse, Encoding.UTF8, "application/json")
            });

        HttpClient httpClient = new(handler);
        var broker = CreateBroker(httpClient: httpClient);

        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OpenAICompatible,
            Endpoint = "http://localhost:11434/v1/chat/completions",
            MaxRetryAttempts = 0,
        };

        ProviderCompletionRequest request = new()
        {
            Model = "gpt-oss:20b",
            Messages = [new ChatCompletionMessage { Role = "user", Content = "Hello." }],
            Temperature = 0.2,
        };

        // When
        Func<Task> action = async () => await broker.PostChatCompletionAsync(
providerName: "Ollama",
providerConfiguration: providerConfiguration,
request: request);

        // Then
        await action.Should().ThrowAsync<HttpRequestException>()
            .Where(exceptionExpression: exception => exception.Message.Contains(rawResponse));
    }

    [Fact]
    public async Task ShouldRetryTransientProviderFailuresAsync()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler(responseFactory: _ =>
        {
            requestCount++;
            return requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":\"busy\"}")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"choices\":[{\"message\":{\"content\":\"ready\"}}]}",
                        Encoding.UTF8,
                        "application/json")
                };
        });
        var broker = CreateBroker(httpClient: new HttpClient(handler));
        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OpenAICompatible,
            Endpoint = "https://api.openai.test/v1/chat/completions",
            MaxRetryAttempts = 1,
            RetryBaseDelayMilliseconds = 1,
        };

        var response = await broker.PostChatCompletionAsync(
providerName: "open-ai",
providerConfiguration: providerConfiguration,
request: new ProviderCompletionRequest
{
    Model = "test-model",
    Messages = [new ChatCompletionMessage { Role = "user", Content = "Hello." }]
});

        response.Content.Should().Be(expected: "ready");
        requestCount.Should().Be(expected: 2);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result: responseFactory(request));
    }

    private static ChatCompletionsBroker CreateBroker(
        HttpClient httpClient) =>
        new(
            dependency: new ChatCompletionsDependency(
                httpClientFactory: new StubHttpClientFactory(
                    httpClient: httpClient)));

    private sealed class StubHttpClientFactory(HttpClient httpClient) :
        IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            httpClient;
    }
}
