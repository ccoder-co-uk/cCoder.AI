using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;
using Moq;

namespace cCoder.AI.Tests.Services.Orchestrations;

public partial class AgentOrchestrationServiceTests
{
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
            .Setup(service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .Callback<string?, string?, IReadOnlyList<ChatCompletionMessage>, double?, bool, CancellationToken>(
                (_, _, messages, _, _, _) => actualMessages = messages)
            .ReturnsAsync(new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Hello.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be("Hello.");
        actualResponse.IterationResponses.Should().HaveCount(1);
        actualResponse.IterationResponses[0].ResultType.Should().Be(AgentResultType.Final);
        actualMessages.Should().NotBeNull();
        actualMessages![0].Role.Should().Be("system");
        actualMessages[0].Content.Should().Contain("BASE PROMPT");
        actualMessages[0].Content.Should().Contain("USE CASE PROMPT");

        shellBrokerMock.Verify(broker => broker.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<ShellKind>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
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
            .SetupSequence(service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletionResponse
            {
                Content = "{\"type\":\"tool\",\"tool\":\"shell\",\"command\":\"Get-Location\",\"reason\":\"Need the working directory.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            })
            .ReturnsAsync(new CompletionResponse
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
            .Setup(broker => broker.ExecuteAsync(
                "Get-Location",
                inputRequest.WorkingDirectory,
                inputRequest.EnvironmentVariables,
                inputRequest.ShellKind,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolExecutionResponse);

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be("The working directory is C:\\Temp.");
        actualResponse.IterationResponses.Should().HaveCount(2);
        actualResponse.IterationResponses[0].ToolExecution.Should().BeEquivalentTo(toolExecutionResponse);
        actualResponse.IterationResponses[1].ResultType.Should().Be(AgentResultType.Final);
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
            .Setup(service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Hello stream.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        List<AgentStreamTokenResponse> actualTokens = [];

        // When
        await foreach (AgentStreamTokenResponse token in agentOrchestrationService.StreamAsync(inputRequest))
        {
            actualTokens.Add(token);
        }

        // Then
        actualTokens.Should().HaveCountGreaterThanOrEqualTo(3);
        actualTokens[0].Type.Should().Be("start");
        actualTokens[^1].Type.Should().Be("complete");
        actualTokens.Where(token => token.Type == "token")
            .Select(token => token.Content)
            .Should()
            .NotBeEmpty();

        string streamedMessage = string.Concat(
            actualTokens
                .Where(token => token.Type == "token")
                .Select(token => token.Content));

        streamedMessage.Should().Be("Hello stream.");
        actualTokens[^1].Completion!.FinalMessage.Should().Be("Hello stream.");
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
            .SetupSequence(service => service.CompleteChatAsync(
                inputRequest.Provider,
                inputRequest.Model,
                It.IsAny<IReadOnlyList<ChatCompletionMessage>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletionResponse
            {
                Content = string.Empty,
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            })
            .ReturnsAsync(new CompletionResponse
            {
                Content = "{\"type\":\"final\",\"message\":\"Recovered.\"}",
                Model = "gpt-oss:20b",
                Provider = "Ollama",
                RawContent = "{}",
            });

        // When
        AgentRunResponse actualResponse = await agentOrchestrationService.RunAsync(inputRequest);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be("Recovered.");
        actualResponse.Iterations.Should().Be(2);
        shellBrokerMock.Verify(broker => broker.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<ShellKind>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
