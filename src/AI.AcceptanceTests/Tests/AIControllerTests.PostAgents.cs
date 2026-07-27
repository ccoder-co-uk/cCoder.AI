// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

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

        factory.CompletionProviderService.EnqueueResponse(completionResponse: new CompletionResponse
        {
            Content = "{\"type\":\"tool\",\"tool\":\"shell\",\"command\":\"Get-Location\",\"reason\":\"Need the current folder.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        factory.CompletionProviderService.EnqueueResponse(completionResponse: new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"The folder is C:\\\\Temp.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        factory.ShellBroker.EnqueueResponse(toolExecutionResponse: new ToolExecutionResponse
        {
            Command = "Get-Location",
            ExitCode = 0,
            ShellKind = ShellKind.PowerShell,
            StandardOutput = "C:\\Temp",
            StandardError = string.Empty,
            WorkingDirectory = "C:\\Temp",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri: "/Api/AI/Agents", value: inputRequest);
        AgentRunResponse actualResponse = await ReadAsAsync<AgentRunResponse>(httpResponseMessage: response);

        // Then
        actualResponse.Succeeded.Should().BeTrue();
        actualResponse.FinalMessage.Should().Be(expected: "The folder is C:\\Temp.");
        actualResponse.IterationResponses.Should().HaveCount(expected: 2);
        factory.ShellBroker.Executions.Should().ContainSingle();
        factory.ShellBroker.Executions[0].Command.Should().Be(expected: "Get-Location");
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

        factory.CompletionProviderService.EnqueueResponse(completionResponse: new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"The folder is C:\\\\Temp.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri: "/Api/AI/Agents/Stream", value: inputRequest);
        IReadOnlyList<AgentStreamTokenResponse> actualTokens = await ReadNdjsonAsAsync(httpResponseMessage: response);

        // Then
        actualTokens.Should().NotBeEmpty();
        actualTokens[0].Type.Should().Be(expected: "start");
        actualTokens[^1].Type.Should().Be(expected: "complete");
        actualTokens[^1].Completion!.FinalMessage.Should().Be(expected: "The folder is C:\\Temp.");
    }
}