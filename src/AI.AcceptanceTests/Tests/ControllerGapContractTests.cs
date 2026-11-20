// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AI.AcceptanceTests.Infrastructure;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;
using Xunit;

namespace AI.AcceptanceTests.Tests;

public sealed class ControllerGapContractTests
{
    public static IEnumerable<object[]> AiClientErrorEndpoints =>
    [
        ["/Api/AI/Completions", new CompletionRequest()],
        ["/Api/AI/Agents", new AgentRunRequest()],
        ["/Api/AI/Agents/Stream", new AgentRunRequest()],
        ["/Api/AI/Chat", new ChatRequest()],
        ["/Api/AI/Chat/Stream", new ChatRequest()],
    ];

    public static IEnumerable<object[]> AiMissingServerErrorEndpoints =>
    [
        ["/Api/AI/Agents", new AgentRunRequest()],
        ["/Api/AI/Agents/Stream", new AgentRunRequest()],
        ["/Api/AI/Chat", new ChatRequest()],
        ["/Api/AI/Chat/Stream", new ChatRequest()],
    ];

    [Fact]
    public async Task PostChat_ShouldReturnResponseAndLogSuccess()
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/Api/AI/Chat",
            value: new ChatRequest { Instructions = "Reply." });

        // Then
        response.IsSuccessStatusCode.Should().BeTrue();
        factory.LoggingBroker.InformationCount.Should().Be(expected: 1);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 0);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 0);
    }

    [Fact]
    public async Task StreamChat_ShouldReturnNdjsonAndLogSuccess()
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/Api/AI/Chat/Stream",
            value: new ChatRequest { Instructions = "Reply." });

        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        content.Should().Contain(expected: "complete");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 1);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 0);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 0);
    }

    [Theory]
    [MemberData(nameof(AiClientErrorEndpoints))]
    public async Task AiAction_WhenDependencyRejectsRequest_ShouldReturnSafe400AndWarn(
        string requestUri,
        object request)
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        ArgumentException expectedException = new(message: "Sensitive invalid detail.");
        factory.AgentManager.FailWith(expectedException: expectedException);
        factory.CompletionProviderService.FailWith(expectedException: expectedException);

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri: requestUri, value: request);
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 1);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 0);
    }

    [Theory]
    [MemberData(nameof(AiMissingServerErrorEndpoints))]
    public async Task AiAction_WhenDependencyFailsBeforeStreaming_ShouldReturnDetailFree500AndLogError(
        string requestUri,
        object request)
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        factory.AgentManager.FailWith(
            expectedException: new InvalidOperationException(message: "Sensitive failure detail."));

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri: requestUri, value: request);
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: HttpStatusCode.InternalServerError);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 0);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 1);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), HttpStatusCode.BadRequest, 1, 0)]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.InternalServerError, 0, 1)]
    public async Task PostImportModel_WhenDependencyFails_ShouldMapAndLog(
        Type exceptionType,
        HttpStatusCode expectedStatus,
        int warningCount,
        int errorCount)
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        factory.ModelManagerService.FailWith(
            expectedException: (Exception)Activator.CreateInstance(exceptionType, "Sensitive model detail.")!);

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/Api/AI/Model/Providers/Ollama/Import",
            value: new ModelImportRequest { ModelId = "test" });

        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: expectedStatus);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: warningCount);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: errorCount);
    }

    [Fact]
    public async Task GetAvailableModels_WhenDependencyRejectsProvider_ShouldReturnSafe400AndWarn()
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        factory.ModelManagerService.FailWith(
            expectedException: new ArgumentException(message: "Sensitive provider detail."));

        // When
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: "/Api/AI/Model/Providers/Invalid/Available");

        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 1);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 0);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), HttpStatusCode.BadRequest, 1, 0)]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.InternalServerError, 0, 1)]
    public async Task StreamConversation_WhenDependencyFailsBeforeStreaming_ShouldMapAndLog(
        Type exceptionType,
        HttpStatusCode expectedStatus,
        int warningCount,
        int errorCount)
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        factory.AgentManager.FailWith(
            expectedException: (Exception)Activator.CreateInstance(exceptionType, "Sensitive home detail.")!);

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/Home/StreamConversation",
            value: new ChatRequest { Instructions = "Reply." });

        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: expectedStatus);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: warningCount);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: errorCount);
    }

    [Fact]
    public async Task AdminIndex_WhenDependencyFails_ShouldReturnDetailFree500AndLogError()
    {
        // Given
        using AIWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        factory.AgentRunHistoryManager.FailWith(
            expectedException: new InvalidOperationException(message: "Sensitive admin detail."));

        // When
        using HttpResponseMessage response = await client.GetAsync(requestUri: "/Admin");
        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.StatusCode.Should().Be(expected: HttpStatusCode.InternalServerError);
        content.Should().NotContain(unexpected: "Sensitive");
        factory.LoggingBroker.InformationCount.Should().Be(expected: 0);
        factory.LoggingBroker.WarningCount.Should().Be(expected: 0);
        factory.LoggingBroker.ErrorCount.Should().Be(expected: 1);
    }

    private static AIWebApplicationFactory CreateFactory()
    {
        AIWebApplicationFactory factory = new();
        factory.UseTestAgentManager = true;
        factory.CompletionProviderService.Reset();
        factory.ModelManagerService.Reset();
        factory.AgentRunHistoryManager.Reset();
        factory.LoggingBroker.Reset();

        return factory;
    }
}