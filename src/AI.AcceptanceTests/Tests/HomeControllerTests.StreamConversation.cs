using System.Net.Http.Json;
using AI.Web.Models;
using FluentAssertions;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [Fact]
    public async Task StreamConversation_ShouldReturnWorkspaceAgentStream()
    {
        // Given
        factory.CompletionProviderService.EnqueueResponse(new CompletionResponse
        {
            Content = "{\"type\":\"final\",\"message\":\"Workspace response.\"}",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        AgentWorkspaceRequest inputRequest = new()
        {
            Instructions = "Respond from the workspace endpoint.",
            Provider = "Ollama",
        };

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync("/Home/StreamConversation", inputRequest);
        string content = await response.Content.ReadAsStringAsync();

        IReadOnlyList<AgentStreamTokenResponse> actualTokens = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => System.Text.Json.JsonSerializer.Deserialize<AgentStreamTokenResponse>(
                line,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
            .Cast<AgentStreamTokenResponse>()
            .ToList();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(content);
        actualTokens[0].Type.Should().Be("start");
        actualTokens[^1].Type.Should().Be("complete");
        actualTokens[^1].Completion!.FinalMessage.Should().Be("Workspace response.");
    }
}
