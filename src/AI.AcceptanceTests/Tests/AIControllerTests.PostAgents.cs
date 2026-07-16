using System.Net.Http.Json;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class AIControllerTests
{
    [Fact]
    public async Task PostAgents_ShouldExecuteShellToolAndReturnFinalMessage()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Find the current folder.",
            Provider = "Ollama",
            WorkingDirectory = "C:\\Temp",
            ShellKind = ShellKind.PowerShell,
        };

        factory.CompletionProviderService.EnqueueResponse(new CompletionResponse
        {
            Content = "{\"type\":\"tool\",\"tool\":\"shell\",\"command\":\"Get-Location\",\"reason\":\"Need the current folder.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        factory.CompletionProviderService.EnqueueResponse(new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"The folder is C:\\\\Temp.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        factory.ShellBroker.EnqueueResponse(new ToolExecutionResponse
        {
            Command = "Get-Location",
            ExitCode = 0,
            ShellKind = ShellKind.PowerShell,
            StandardOutput = "C:\\Temp",
            StandardError = string.Empty,
            WorkingDirectory = "C:\\Temp",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync("/Api/AI/Agents", inputRequest);
        AgentRunResponse actualResponse = await ReadAsAsync<AgentRunResponse>(response);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be("The folder is C:\\Temp.");
        actualResponse.IterationResponses.Should().HaveCount(2);
        factory.ShellBroker.Executions.Should().ContainSingle();
        factory.ShellBroker.Executions[0].Command.Should().Be("Get-Location");
    }

    [Fact]
    public async Task StreamAgents_ShouldReturnNdjsonTokenStream()
    {
        // Given
        AgentRunRequest inputRequest = new()
        {
            Instructions = "Find the current folder with streaming.",
            Provider = "Ollama",
        };

        factory.CompletionProviderService.EnqueueResponse(new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"The folder is C:\\\\Temp.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync("/Api/AI/Agents/Stream", inputRequest);
        IReadOnlyList<AgentStreamTokenResponse> actualTokens = await ReadNdjsonAsAsync(response);

        // Then
        actualTokens.Should().NotBeEmpty();
        actualTokens[0].Type.Should().Be("start");
        actualTokens[^1].Type.Should().Be("complete");
        actualTokens[^1].Completion!.FinalMessage.Should().Be("The folder is C:\\Temp.");
    }
}
