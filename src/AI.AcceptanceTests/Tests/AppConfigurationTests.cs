// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed partial class AppConfigurationTests
{
    [Fact]
    public void WebHost_ShouldExposeStandardRootConfigurationType()
    {
        // Given
        Type configurationAssemblyMarker = typeof(Program);

        // When
        Type appConfigurationType = configurationAssemblyMarker.Assembly
            .GetType(name: "AI.Web.Models.AppConfiguration");

        // Then
        appConfigurationType.Should().NotBeNull();
    }
}