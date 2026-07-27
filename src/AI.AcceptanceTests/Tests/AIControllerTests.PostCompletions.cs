// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

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

        factory.CompletionProviderService.EnqueueResponse(completionResponse: new CompletionResponse
        {
            Content = "Hello.",
            Model = "gpt-oss:20b",
            Provider = "Ollama",
            RawContent = "{}",
        });

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri: "/Api/AI/Completions", value: inputRequest);
        CompletionResponse actualResponse = await ReadAsAsync<CompletionResponse>(httpResponseMessage: response);

        // Then
        actualResponse.Content.Should().Be(expected: "Hello.");
        factory.CompletionProviderService.CompletionRequests.Should().ContainSingle();
        factory.CompletionProviderService.CompletionRequests[0].Prompt.Should().Be(expected: "Say hello.");
    }
}