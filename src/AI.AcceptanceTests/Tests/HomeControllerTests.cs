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
            "Ollama",
            new cCoder.AI.Models.Responses.ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true
            });

        // When
        using HttpResponseMessage response = await client.GetAsync("/");
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(content);
        content.Should().Contain("Agent Console");
        content.Should().Contain("Send to Agent");
        content.Should().Contain("Conversation");
        content.Should().Contain("Refresh Models");
        content.Should().NotContain("Iteration Trace");
    }

    [Fact]
    public async Task GetAdminPage_ShouldRenderOperationalVisibility()
    {
        // Given
        factory.ModelManagerService.Reset();
        factory.ModelManagerService.SeedAvailableModels(
            "Ollama",
            new cCoder.AI.Models.Responses.ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true
            });

        // When
        using HttpResponseMessage response = await client.GetAsync("/Admin");
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(content);
        content.Should().Contain("Operational Visibility");
        content.Should().Contain("Provider Diagnostics");
        content.Should().Contain("Recent Activity");
        content.Should().Contain("gpt-oss:20b");
    }
}
