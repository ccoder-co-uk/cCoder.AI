// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using Xunit;
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
        loggingBroker.InformationCount.Should().Be(expected: 0);
        loggingBroker.WarningCount.Should().Be(expected: 0);
        loggingBroker.ErrorCount.Should().Be(expected: 1);
    }

    private sealed class RecordingLoggingBroker : ILoggingBroker
    {
        public List<Exception> Exceptions { get; } = [];
        public int InformationCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }

        public void LogInformation(string message, params object[] args) =>
            InformationCount++;

        public void LogWarning(Exception exception, string message, params object[] args)
        {
            WarningCount++;
            Exceptions.Add(item: exception);

        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            ErrorCount++;
            Exceptions.Add(item: exception);
        }
    }
}
