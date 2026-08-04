// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;
using Moq;

namespace cCoder.AI.Tests.Services.Orchestrations;

public partial class AgentOrchestrationServiceTests
{
    [Fact]
    public async Task ShouldRejectEmptyInstructionsAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = string.Empty,
        };

        // When
        Func<Task> runAction = async () =>
            await agentOrchestrationService.RunAsync(request: inputRequest);

        // Then
        await runAction.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("request");

        completionProviderServiceMock.VerifyNoOtherCalls();
        shellBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ShouldStreamErrorWhenAgentRunFailsAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Run an invalid directive.",
        };

        completionProviderServiceMock
            .Setup(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"unsupported\"}",
            });

        List<AgentStreamTokenResponse> actualTokens = [];

        // When
        await foreach (AgentStreamTokenResponse token in agentOrchestrationService.StreamAsync(request: inputRequest))
        {
            actualTokens.Add(item: token);
        }

        // Then
        actualTokens.Should().HaveCount(expected: 2);
        actualTokens[0].Type.Should().Be(expected: "start");
        actualTokens[1].Type.Should().Be(expected: "error");
        actualTokens[1].Content.Should().Contain(expected: "expected a 'final' result or a 'shell' command request");
    }

    [Fact]
    public async Task ShouldStopAtConfiguredIterationLimitAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Never produce a valid directive.",
            MaxIterations = 2,
        };

        completionProviderServiceMock
            .Setup(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "not json",
                Model = "model",
                Provider = "provider",
            });

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(request: inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeFalse();
        actualResponse.Iterations.Should().Be(expected: 2);
        actualResponse.IterationResponses.Should().HaveCount(expected: 2);
        actualResponse.IterationResponses.Should().OnlyContain(
            predicate: response => response.ResultType == AgentResultType.InvalidDirective);
    }

    [Fact]
    public async Task ShouldReturnFinalMessageWithoutUsingToolAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Say hello.",
            SystemPrompt = "USE CASE PROMPT",
        };

        IReadOnlyList<ChatCompletionMessage>? actualMessages = null;

        completionProviderServiceMock
            .Setup(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .Callback<string?, string?, IReadOnlyList<ChatCompletionMessage>, double?, bool, CancellationToken>(
action: (_, _, messages, _, _, _) => actualMessages = messages)
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Hello.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(request: inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be(expected: "Hello.");
        actualResponse.IterationResponses.Should().HaveCount(expected: 1);
        actualResponse.IterationResponses[0].ResultType.Should().Be(expected: AgentResultType.Final);
        actualMessages.Should().NotBeNull();
        actualMessages![0].Role.Should().Be(expected: "system");
        actualMessages[0].Content.Should().Contain(expected: "BASE PROMPT");
        actualMessages[0].Content.Should().Contain(expected: "USE CASE PROMPT");

        shellBrokerMock.Verify(expression: broker => broker.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<ShellKind>(),
            It.IsAny<CancellationToken>()),
times: Times.Never);
    }

    [Fact]
    public async Task ShouldExecuteShellToolAndThenReturnFinalMessageAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Find the current directory.",
            ShellKind = ShellKind.PowerShell,
            WorkingDirectory = "C:\\Temp",
        };

        completionProviderServiceMock
            .SetupSequence(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"tool\",\"tool\":\"shell\",\"command\":\"Get-Location\",\"reason\":\"Need the working directory.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            })
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"The working directory is C:\\\\Temp.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        ToolExecutionResponse toolExecutionResponse = new()
        {
            Command = "Get-Location",
            ExitCode = 0,
            ShellKind = ShellKind.PowerShell,
            StandardOutput = "C:\\Temp",
            StandardError = string.Empty,
            WorkingDirectory = "C:\\Temp",
        };

        shellBrokerMock
            .Setup(expression: broker => broker.ExecuteAsync(
                "Get-Location",
                inputRequest.WorkingDirectory,
                inputRequest.EnvironmentVariables,
                inputRequest.ShellKind,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: toolExecutionResponse);

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(request: inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be(expected: "The working directory is C:\\Temp.");
        actualResponse.IterationResponses.Should().HaveCount(expected: 2);
        actualResponse.IterationResponses[0].ToolExecution.Should().BeEquivalentTo(expectation: toolExecutionResponse);
        actualResponse.IterationResponses[1].ResultType.Should().Be(expected: AgentResultType.Final);
    }

    [Fact]
    public async Task ShouldStreamFinalMessageAfterInternalLoopCompletesAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Say hello with streaming.",
        };

        completionProviderServiceMock
            .Setup(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Hello stream.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        List<AgentStreamTokenResponse> actualTokens = [];

        // When
        await foreach (AgentStreamTokenResponse token in agentOrchestrationService.StreamAsync(request: inputRequest))
        {
            actualTokens.Add(item: token);
        }

        // Then
        actualTokens.Should().HaveCountGreaterThanOrEqualTo(expected: 3);
        actualTokens[0].Type.Should().Be(expected: "start");
        actualTokens[^1].Type.Should().Be(expected: "complete");
        actualTokens.Where(predicate: token => token.Type == "token")
            .Select(selector: token => token.Content)
            .Should()
            .NotBeEmpty();

        string streamedMessage = string.Concat(
values: actualTokens
                .Where(token => token.Type == "token")
                .Select(token => token.Content));

        streamedMessage.Should().Be(expected: "Hello stream.");
        actualTokens[^1].Completion!.FinalMessage.Should().Be(expected: "Hello stream.");
    }

    [Fact]
    public async Task ShouldRecoverFromInvalidDirectiveAndReturnFinalMessageAsync()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Say hello after recovering from a malformed reply.",
        };

        completionProviderServiceMock
            .SetupSequence(expression: service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = string.Empty,
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            })
            .ReturnsAsync(value: new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Recovered.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(request: inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be(expected: "Recovered.");
        actualResponse.Iterations.Should().Be(expected: 2);
        shellBrokerMock.Verify(expression: broker => broker.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<ShellKind>(),
            It.IsAny<CancellationToken>()),
times: Times.Never);
    }
}
