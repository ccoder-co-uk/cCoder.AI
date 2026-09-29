// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using Xunit;
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

    [Fact]
    public async Task PostCompletions_WhenServiceFails_ReturnsServerErrorAndLogsException()
    {
        // Given
        InvalidOperationException expectedException = new(message: "Completion failed.");
        factory.CompletionProviderService.FailWith(expectedException: expectedException);

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/Api/AI/Completions",
            value: new CompletionRequest());

        // Then
        response.StatusCode.Should().Be(expected: HttpStatusCode.InternalServerError);
        factory.LoggingBroker.Exceptions.Should().ContainSingle()
            .Which.Should().BeSameAs(expectedException);
    }
}
