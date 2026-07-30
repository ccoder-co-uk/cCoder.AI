// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Exposures;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Orchestrations;
using FluentAssertions;
using Moq;

namespace cCoder.AI.Tests.Exposures;

public sealed class ChatContextTests
{
    [Fact]
    public async Task InferAsync_ShouldMapChatRequestToAgentRunRequest()
    {
        // Given
        Mock<IAgentManager> orchestrationServiceMock = new();

        AgentRunResponse expectedResponse = new()
        {
            FinalMessage = "Inference complete.",
            Succeeded = true,
        };

        AgentRunRequest? capturedRequest = null;

        orchestrationServiceMock
            .Setup(expression: service => service.RunAsync(
                It.IsAny<AgentRunRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<AgentRunRequest, CancellationToken>(
                (request, _) => capturedRequest = request)
            .ReturnsAsync(value: expectedResponse);

        ChatContext chatContext = new(
            agentOrchestrationService: orchestrationServiceMock.Object);

        ChatRequest chatRequest = new()
        {
            Instructions = "Inspect this request.",
            Provider = "Codex",
            Model = "gpt-5.6-luna",
            SystemPrompt = "Be concise.",
            InputFilePaths = ["C:\\Data\\sample.png"],
            WorkingDirectory = "C:\\Data",
            ShellKind = ShellKind.PowerShell,
            MaxIterations = 4,
        };

        // When
        AgentRunResponse actualResponse = await chatContext.InferAsync(
            chatRequest: chatRequest);

        // Then
        actualResponse.Should().BeSameAs(expectedResponse);
        capturedRequest.Should().BeEquivalentTo(chatRequest);
    }
}