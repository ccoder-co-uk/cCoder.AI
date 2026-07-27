// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests(AIWebApplicationFactory factory)
    : IClassFixture<AIWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GetHomePage_ShouldRenderAgentConsoleUi()
    {
        // Given
        factory.ModelManagerService.Reset();
        factory.ModelManagerService.SeedAvailableModels(
provider: "Ollama",
            new cCoder.AI.Models.Responses.ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true
            });

        // When
        using HttpResponseMessage response = await client.GetAsync(requestUri: "/");
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        content.Should().Contain(expected: "Agent Console");
        content.Should().Contain(expected: "Send to Agent");
        content.Should().Contain(expected: "Conversation");
        content.Should().Contain(expected: "Refresh Models");
        content.Should().NotContain(unexpected: "Iteration Trace");
    }

    [Fact]
    public async Task GetAdminPage_ShouldRenderOperationalVisibility()
    {
        // Given
        factory.ModelManagerService.Reset();
        factory.ModelManagerService.SeedAvailableModels(
provider: "Ollama",
            new cCoder.AI.Models.Responses.ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true
            });

        // When
        using HttpResponseMessage response = await client.GetAsync(requestUri: "/Admin");
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        content.Should().Contain(expected: "Operational Visibility");
        content.Should().Contain(expected: "Provider Diagnostics");
        content.Should().Contain(expected: "Recent Activity");
        content.Should().Contain(expected: "gpt-oss:20b");
    }
}