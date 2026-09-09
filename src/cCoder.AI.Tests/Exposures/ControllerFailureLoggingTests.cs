// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.Loggings;
using cCoder.AI.Exposures;
using cCoder.AI.Exposures.Controllers;
using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Foundations.Models;
using cCoder.AI.Services.Orchestrations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace cCoder.AI.Tests.Exposures;

public sealed class ControllerFailureLoggingTests
{
    [Fact]
    public async Task PostCompletions_WhenServiceFails_ReturnsServerErrorAndLogsException()
    {
        // Given
        InvalidOperationException expectedException = new(message: "Completion failed.");
        Mock<ICompletionProviderManager> completionProviderService = new();

        completionProviderService
            .Setup(expression: service => service.CompleteAsync(
                It.IsAny<CompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: expectedException);

        Mock<IAgentManager> agentManager = new();
        RecordingLoggingBroker loggingBroker = new();

        AIController controller = new(
            completionProviderService: completionProviderService.Object,
            agentOrchestrationService: agentManager.Object,
            chatContext: new ChatContext(agentOrchestrationService: agentManager.Object),
            loggingBroker: loggingBroker)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // When
        IActionResult result = await controller.PostCompletionsAsync(
            completionRequest: new CompletionRequest(),
            cancellationToken: default);

        // Then
        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(expected: 500);
        loggingBroker.Exceptions.Should().ContainSingle().Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task GetAvailableModels_WhenServiceFails_ReturnsServerErrorAndLogsException()
    {
        // Given
        InvalidOperationException expectedException = new(message: "Model lookup failed.");
        Mock<IModelManager> modelManagerService = new();

        modelManagerService
            .Setup(expression: service => service.RetrieveAvailableModelsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: expectedException);

        RecordingLoggingBroker loggingBroker = new();
        ModelController controller = new(
            modelManagerService: modelManagerService.Object,
            loggingBroker: loggingBroker);

        // When
        IActionResult result = await controller.GetAvailableModelsAsync(
            provider: "Ollama",
            cancellationToken: default);

        // Then
        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(expected: 500);
        loggingBroker.Exceptions.Should().ContainSingle().Which.Should().BeSameAs(expectedException);
    }

    private sealed class RecordingLoggingBroker : ILoggingBroker
    {
        public List<Exception> Exceptions { get; } = [];

        public void LogError(Exception exception, string message, params object[] args) =>
            Exceptions.Add(item: exception);
    }
}