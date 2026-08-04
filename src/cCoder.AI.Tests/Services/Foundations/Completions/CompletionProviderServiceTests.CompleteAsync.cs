// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;
using Moq;

namespace cCoder.AI.Tests.Services.Foundations.Completions;

public partial class CompletionProviderServiceTests
{
    [Fact]
    public async Task ShouldRejectEmptyPromptAsync()
    {
        // Given
        CompletionRequest inputRequest = new()
        {
            Prompt = " ",
        };

        // When
        Func<Task> completeAction = async () =>
            await completionProviderService.CompleteAsync(request: inputRequest);

        // Then
        await completeAction.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("prompt");

        chatCompletionsBrokerMock.VerifyNoOtherCalls();
        codexCliBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ShouldRejectUnsupportedProviderAsync()
    {
        // Given
        CompletionRequest inputRequest = new()
        {
            Provider = "Missing",
            Prompt = "Hello",
        };

        // When
        Func<Task> completeAction = async () =>
            await completionProviderService.CompleteAsync(request: inputRequest);

        // Then
        await completeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*Unsupported AI provider 'Missing'.*");
    }

    [Fact]
    public async Task ShouldUseDefaultProviderForPromptCompletionAsync()
    {
        // Given
        CompletionRequest inputRequest = new()
        {
            Prompt = "List the files in the current directory.",
        };

        CompletionResponse expectedResponse = new()
        {
            Content = "{\"type\":\"final\",\"message\":\"done\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        };

        chatCompletionsBrokerMock
            .Setup(expression: broker => broker.PostChatCompletionAsync(
                "Ollama",
                It.Is<AICompletionProviderConfiguration>(configuration => configuration.DefaultModel == "gpt-oss:20b"),
                It.Is<ProviderCompletionRequest>(request =>
                    request.Model == "gpt-oss:20b" &&
                    request.Messages.Count == 1 &&
                    request.Messages[0].Role == "user" &&
                    request.Messages[0].Content == inputRequest.Prompt),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expectedResponse);

        // When
        CompletionResponse actualResponse = await completionProviderService.CompleteAsync(request: inputRequest);

        // Then
        actualResponse.Should().BeSameAs(expected: expectedResponse);

        chatCompletionsBrokerMock.Verify(expression: broker => broker.PostChatCompletionAsync(
            "Ollama",
            It.IsAny<AICompletionProviderConfiguration>(),
            It.IsAny<ProviderCompletionRequest>(),
            It.IsAny<CancellationToken>()),
times: Times.Once);
    }

    [Fact]
    public async Task ShouldUseRequestedProviderForChatCompletionAsync()
    {
        // Given
        CompletionResponse expectedResponse = new()
        {
            Content = "Hello from Azure Foundry.",
            Model = "custom-model",
            Provider = "AzureFoundry",
            RawContent = "{}",
        };

        IReadOnlyList<ChatCompletionMessage> inputMessages =
        [
            new() { Role = "system", Content = "Be concise." },
            new() { Role = "user", Content = "Say hello." },
        ];

        chatCompletionsBrokerMock
            .Setup(expression: broker => broker.PostChatCompletionAsync(
                "AzureFoundry",
                It.Is<AICompletionProviderConfiguration>(configuration => configuration.DefaultModel == "gpt-4.1"),
                It.Is<ProviderCompletionRequest>(request =>
                    request.Model == "custom-model" &&
                    request.Messages.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expectedResponse);

        // When
        CompletionResponse actualResponse = await completionProviderService.CompleteChatAsync(
            provider: "AzureFoundry",
            model: "custom-model",
            messages: inputMessages);

        // Then
        actualResponse.Should().BeSameAs(expected: expectedResponse);
    }

    [Fact]
    public async Task ShouldRouteCodexProviderThroughTheCliBrokerAsync()
    {
        CompletionResponse expectedResponse = new()
        {
            Content = "ready",
            Model = "gpt-5.6-luna",
            Provider = "Codex",
            RawContent = "ready"
        };
        codexCliBrokerMock
            .Setup(expression: broker => broker.CompleteAsync(
                "Codex",
                It.Is<AIProviderConfiguration>(provider => provider.CompletionProvider.Mode == Models.Enums.AIProviderMode.CodexCli),
                It.Is<ProviderCompletionRequest>(request => request.Model == "gpt-5.6-luna"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expectedResponse);

        CompletionResponse actualResponse = await completionProviderService.CompleteAsync(
request: new CompletionRequest
{
    Provider = "Codex",
    Prompt = "Return ready."
});

        actualResponse.Should().BeSameAs(expected: expectedResponse);
        chatCompletionsBrokerMock.VerifyNoOtherCalls();
    }
}
