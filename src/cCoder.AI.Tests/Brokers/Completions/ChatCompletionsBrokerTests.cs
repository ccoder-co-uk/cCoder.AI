// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Text;
using cCoder.AI.Brokers.Completions;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using FluentAssertions;

namespace cCoder.AI.Tests.Brokers.Completions;

public class ChatCompletionsBrokerTests
{
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
        var broker = new ChatCompletionsBroker(httpClient: new HttpClient(handler));
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
    Messages = [new ChatCompletionMessage("user", "Return structured JSON.")]
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
        var broker = new ChatCompletionsBroker(httpClient: new HttpClient(handler));
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
    Messages = [new ChatCompletionMessage("user", "Return structured JSON.")]
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
        var broker = new ChatCompletionsBroker(httpClient: httpClient);

        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OllamaApi,
            Endpoint = "http://localhost:11434/api/chat",
        };

        ProviderCompletionRequest request = new()
        {
            Model = "gpt-oss:20b",
            Messages = [new ChatCompletionMessage(role: "user", content: "Inspect the helper scripts.")],
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
        var broker = new ChatCompletionsBroker(httpClient: httpClient);

        AICompletionProviderConfiguration providerConfiguration = new()
        {
            Mode = AIProviderMode.OpenAICompatible,
            Endpoint = "http://localhost:11434/v1/chat/completions",
            MaxRetryAttempts = 0,
        };

        ProviderCompletionRequest request = new()
        {
            Model = "gpt-oss:20b",
            Messages = [new ChatCompletionMessage(role: "user", content: "Hello.")],
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
        var broker = new ChatCompletionsBroker(httpClient: new HttpClient(handler));
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
    Messages = [new ChatCompletionMessage("user", "Hello.")]
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
}