using System.Text.Json;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class BaselineControllerTests
{
    [Fact]
    public async Task Get_GivenBaselineEndpoint_ShouldReturnPackagesArray()
    {
        JsonElement baseline = await GetBaselineAsync();

        baseline.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
