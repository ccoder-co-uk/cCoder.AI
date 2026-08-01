// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net.Http.Json;
using System.Text.Json;
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

        string[] serializedTokens = content.Split(
            separator: '\n',
            options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IReadOnlyList<AgentStreamTokenResponse> actualTokens = serializedTokens
            .Select(selector: line => JsonSerializer.Deserialize<AgentStreamTokenResponse>(
                line,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
            .Cast<AgentStreamTokenResponse>()
            .ToList();

        using JsonDocument startToken = JsonDocument.Parse(json: serializedTokens[0]);
        using JsonDocument completeToken = JsonDocument.Parse(json: serializedTokens[^1]);

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        startToken.RootElement.TryGetProperty(propertyName: "type", value: out _).Should().BeTrue();
        startToken.RootElement.TryGetProperty(propertyName: "Type", value: out _).Should().BeFalse();
        completeToken.RootElement
            .GetProperty(propertyName: "completion")
            .GetProperty(propertyName: "finalMessage")
            .GetString()
            .Should().Be(expected: "Workspace response.");
        actualTokens[0].Type.Should().Be(expected: "start");
        actualTokens[^1].Type.Should().Be(expected: "complete");
        actualTokens[^1].Completion!.FinalMessage.Should().Be(expected: "Workspace response.");
    }
}
