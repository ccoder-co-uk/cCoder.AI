using System.Net.Http.Json;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class AIControllerTests
{
    [Fact]
    public async Task PostCompletions_ShouldReturnCompletionResponse()
    {
        // Given
        CompletionRequest inputRequest = new()
        {
            Prompt = "Say hello.",
            Provider = "Ollama",
        };

        factory.CompletionProviderService.EnqueueResponse(new CompletionResponse
        {
            Content = "Hello.",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync("/Api/AI/Completions", inputRequest);
        CompletionResponse actualResponse = await ReadAsAsync<CompletionResponse>(response);

        // Then
        actualResponse.Content.Should().Be("Hello.");
        factory.CompletionProviderService.CompletionRequests.Should().ContainSingle();
        factory.CompletionProviderService.CompletionRequests[0].Prompt.Should().Be("Say hello.");
    }
}
