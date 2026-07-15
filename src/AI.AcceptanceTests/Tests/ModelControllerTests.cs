using System.Net.Http.Json;
using System.Text.Json;
using AI.AcceptanceTests.Infrastructure;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using FluentAssertions;

namespace AI.AcceptanceTests.Tests;

public sealed class ModelControllerTests : IClassFixture<AIWebApplicationFactory>
{
    private readonly AIWebApplicationFactory factory;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public ModelControllerTests(AIWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        factory.ModelManagerService.Reset();
    }

    [Fact]
    public async Task GetAvailableModels_ShouldReturnProviderModels()
    {
        // Given
        factory.ModelManagerService.SeedAvailableModels(
            "Ollama",
            new ModelDescriptorResponse
            {
                Id = "gpt-oss:20b",
                Name = "gpt-oss:20b",
                Provider = "Ollama",
                IsAvailable = true,
            });

        // When
        using HttpResponseMessage response = await client.GetAsync("/Api/Model/Providers/Ollama/Available");
        string content = await response.Content.ReadAsStringAsync();

        IReadOnlyList<ModelDescriptorResponse> actualResponse =
            JsonSerializer.Deserialize<List<ModelDescriptorResponse>>(content, JsonSerializerOptions)
            ?? throw new InvalidOperationException("The acceptance response payload could not be deserialized.");

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(content);
        actualResponse.Should().ContainSingle();
        actualResponse[0].Id.Should().Be("gpt-oss:20b");
        factory.ModelManagerService.RetrievalRequests.Should().ContainSingle("Ollama");
    }

    [Fact]
    public async Task PostImportModel_ShouldReturnImportResponse()
    {
        // Given
        factory.ModelManagerService.EnqueueImportResponse(new ModelImportResponse
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
            "/Api/Model/Providers/Ollama/Import",
            inputRequest);

        string content = await response.Content.ReadAsStringAsync();
        ModelImportResponse actualResponse =
            JsonSerializer.Deserialize<ModelImportResponse>(content, JsonSerializerOptions)
            ?? throw new InvalidOperationException("The acceptance response payload could not be deserialized.");

        // Then
        response.IsSuccessStatusCode.Should().BeTrue(content);
        actualResponse.Provider.Should().Be("Ollama");
        actualResponse.ModelId.Should().Be("llama3.1:8b");
        factory.ModelManagerService.ImportRequests.Should().ContainSingle();
        factory.ModelManagerService.ImportRequests[0].ModelId.Should().Be("llama3.1:8b");
    }
}
