using System.Net;
using System.Text.Json;
using AI.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class BaselineControllerTests(AIWebApplicationFactory factory)
    : IClassFixture<AIWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    private async Task<JsonElement> GetBaselineAsync()
    {
        using HttpResponseMessage response = await client.GetAsync("/Api/AI/Baseline");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        return JsonDocument.Parse(content).RootElement.Clone();
    }
}
