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

public sealed class ModelControllerTests
{
    private readonly AIWebApplicationFactory factory;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public ModelControllerTests()
    {
        factory = new AIWebApplicationFactory();
        client = factory.CreateClient();
        factory.ModelManagerService.Reset();
    }

    [Fact]
    public async Task GetAvailableModels_ShouldReturnProviderModels()
    {
        // Given
        factory.ModelManagerService.SeedAvailableModels(
provider: "Ollama",
            new ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true,
            });

        // When
        using HttpResponseMessage response = await client.GetAsync(requestUri: "/Api/AI/Model/Providers/Ollama/Available");
        string content = await response.Content.ReadAsStringAsync();

        IReadOnlyList<ModelDescriptorResponse> actualResponse =
            JsonSerializer.Deserialize<List<ModelDescriptorResponse>>(json: content, options: JsonSerializerOptions)
            ?? throw new InvalidOperationException(message: "The acceptance response payload could not be deserialized.");

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        actualResponse.Should().ContainSingle();
        actualResponse[0].Id.Should().Be(expected: "gpt-oss:20b");
        factory.ModelManagerService.RetrievalRequests.Should().ContainSingle(because: "Ollama");
    }

    [Fact]
    public async Task GetLegacyAvailableModels_ShouldRemainCompatible()
    {
        // Given
        factory.ModelManagerService.SeedAvailableModels(
            provider: "Ollama",
            new ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true,
            });

        // When
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: "/Api/Model/Providers/Ollama/Available");

        // Then
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task GetOpenApi_ShouldOnlyDescribeCanonicalModelRoutes()
    {
        // When
        using HttpResponseMessage response = await client.GetAsync(
            requestUri: "/openapi/v1.json");

        string content = await response.Content.ReadAsStringAsync();

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        content.Should().Contain(expected: "/Api/AI/Model/");
        content.Should().NotContain(unexpected: "\"/Api/Model/");
    }

    [Fact]
    public async Task PostImportModel_ShouldReturnImportResponse()
    {
        // Given
        factory.ModelManagerService.EnqueueImportResponse(response: new ModelImportResponse
        {
            Provider = "Ollama",
            ModelId = "llama3.1:8b",
            Succeeded = true,
            Message = "success",
            RawContent = "{}",
        });

        ModelImportRequest inputRequest = new()
        {
            ModelId = "llama3.1:8b",
        };

        // When
        using HttpResponseMessage response = await client.PostAsJsonAsync(
requestUri: "/Api/AI/Model/Providers/Ollama/Import",
value: inputRequest);

        string content = await response.Content.ReadAsStringAsync();
        ModelImportResponse actualResponse =
            JsonSerializer.Deserialize<ModelImportResponse>(json: content, options: JsonSerializerOptions)
            ?? throw new InvalidOperationException(message: "The acceptance response payload could not be deserialized.");

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(because: content);
        actualResponse.Provider.Should().Be(expected: "Ollama");
        actualResponse.ModelId.Should().Be(expected: "llama3.1:8b");
        factory.ModelManagerService.ImportRequests.Should().ContainSingle();
        factory.ModelManagerService.ImportRequests[0].ModelId.Should().Be(expected: "llama3.1:8b");
    }
}
