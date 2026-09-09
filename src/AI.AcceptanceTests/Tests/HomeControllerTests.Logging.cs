// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Brokers.Loggings;
using AI.Web.Controllers;
using cCoder.AI.Models.Configurations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace AI.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [Fact]
    public void Index_WhenConfigurationFails_ReturnsServerErrorAndLogsException()
    {
        // Given
        AIConfiguration configuration = new()
        {
            Providers = null!
        };

        RecordingLoggingBroker loggingBroker = new();

        HomeController controller = new(
            aiConfiguration: configuration,
            chatContext: null!,
            agentRunHistoryService: null!,
            loggingBroker: loggingBroker);

        // When
        IActionResult result = controller.Index();

        // Then
        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(expected: 500);
        loggingBroker.Exceptions.Should().ContainSingle()
            .Which.Should().BeOfType<ArgumentNullException>();
    }

    private sealed class RecordingLoggingBroker : ILoggingBroker
    {
        public List<Exception> Exceptions { get; } = [];

        public void LogError(Exception exception, string message, params object[] args) =>
            Exceptions.Add(item: exception);
    }
}