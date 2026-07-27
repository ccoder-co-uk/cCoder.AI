// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Net.Http.Json;
using System.Text.Json;
using AI.AcceptanceTests.Infrastructure;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class AIControllerTests : IClassFixture<AIWebApplicationFactory>
{
    private readonly AIWebApplicationFactory factory;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public AIControllerTests(AIWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        factory.CompletionProviderService.Reset();
        factory.ShellBroker.Reset();
    }

    private async Task<T> ReadAsAsync<T>(HttpResponseMessage httpResponseMessage)
    {
        string content = await httpResponseMessage.Content.ReadAsStringAsync();
        httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(because: content);

        return JsonSerializer.Deserialize<T>(json: content, options: JsonSerializerOptions)
            ?? throw new InvalidOperationException(message: "The acceptance response payload could not be deserialized.");
    }

    private async Task<IReadOnlyList<AgentStreamTokenResponse>> ReadNdjsonAsAsync(HttpResponseMessage httpResponseMessage)
    {
        string content = await httpResponseMessage.Content.ReadAsStringAsync();
        httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(because: content);

        return content
            .Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(selector: line => JsonSerializer.Deserialize<AgentStreamTokenResponse>(line, JsonSerializerOptions))
            .Cast<AgentStreamTokenResponse>()
            .ToList();
    }
}