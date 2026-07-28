// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net.Http.Json;
using cCoder.AI.Models.Requests;
using FluentAssertions;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [Fact]
    public async Task StreamConversation_ShouldReturnWorkspaceAgentStream()
    {
        // Given
        factory.CompletionProviderService.EnqueueResponse(completionResponse: new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"Workspace response.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        ChatRequest inputRequest = new()
        {
            Instructions = "Respond from the workspace endpoint.",
            Provider = "Ollama",
        };

        // When
        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
requestUri: "/Home/StreamConversation",
value: inputRequest);
        string content = await response.Content.ReadAsStringAsync();

        IReadOnlyList<AgentStreamTokenResponse> actualTokens = content
            .Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(selector: line => System.Text.Json.JsonSerializer.Deserialize<AgentStreamTokenResponse>(
                line,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
            .Cast<AgentStreamTokenResponse>()
            .ToList();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        actualTokens[0].Type.Should().Be(expected: "start");
        actualTokens[^1].Type.Should().Be(expected: "complete");
        actualTokens[^1].Completion!.FinalMessage.Should().Be(expected: "Workspace response.");
    }
}