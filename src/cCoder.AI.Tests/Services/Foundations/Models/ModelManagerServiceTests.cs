// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.ModelProviders;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Models;
using FluentAssertions;
using Moq;

namespace cCoder.AI.Tests.Services.Foundations.ModelManagement;

public class ModelManagerServiceTests
{
    private readonly Mock<IModelProviderBroker> brokerMock = new();

    [Fact]
    public void GetProviderCapabilitiesShouldResolveDefaultsAndNormalizeConcurrency()
    {
        ModelManagerService service = CreateService();

        var result = service.GetProviderCapabilities(provider: " ");

        result.Provider.Should().Be("Ollama");
        result.DefaultModel.Should().Be("llama");
        result.MaxConcurrency.Should().Be(1);
        result.SupportsModelListing.Should().BeTrue();
        result.SupportsModelImport.Should().BeTrue();
    }

    [Theory]
    [InlineData("Ollama", "api/tags", "{\"models\":[{\"name\":\"llama:3\",\"model\":\"llama\",\"modified_at\":\"today\"},{\"name\":\"\"}]}", "llama:3", "llama:3")]
    [InlineData("Azure", "deployments", "{\"value\":[{\"id\":\"deployment-1\",\"properties\":{\"model\":{\"name\":\"gpt-4.1\",\"publisher\":\"OpenAI\",\"version\":\"1\",\"format\":\"OpenAI\"}}}]}", "deployment-1", "gpt-4.1")]
    [InlineData("OpenAI", "models", "{\"data\":[{\"id\":\"gpt-5\",\"owned_by\":\"OpenAI\"},{\"owned_by\":\"none\"}]}", "gpt-5", "gpt-5")]
    public async Task RetrieveAvailableModelsShouldMapProviderPayload(
        string provider, string path, string payload, string expectedId, string expectedName)
    {
        ModelManagerService service = CreateService();
        brokerMock.Setup(broker => broker.GetStringAsync(
            It.IsAny<AIModelProviderConfiguration>(), path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var result = await service.RetrieveAvailableModelsAsync(provider: provider);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(expectedId);
        result[0].Name.Should().Be(expectedName);
        result[0].Provider.Should().Be(provider);
    }

    [Fact]
    public async Task ImportModelShouldPostOllamaPullAndMapResponse()
    {
        ModelManagerService service = CreateService();
        brokerMock.Setup(broker => broker.PostAsync(
            It.IsAny<AIModelProviderConfiguration>(), "api/pull", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"status\":\"success\"}");

        var result = await service.ImportModelAsync(
            provider: "Ollama",
            request: new ModelImportRequest { ModelId = "llama:3" });

        result.Succeeded.Should().BeTrue();
        result.ModelId.Should().Be("llama:3");
        result.Message.Should().Be("success");
        brokerMock.VerifyAll();
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Disabled")]
    public async Task RetrieveAvailableModelsShouldRejectUnsupportedProvider(string provider)
    {
        ModelManagerService service = CreateService();

        Func<Task> action = async () =>
            await service.RetrieveAvailableModelsAsync(provider: provider);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ImportModelShouldValidateRequestBeforeCallingProvider()
    {
        ModelManagerService service = CreateService();

        Func<Task> action = async () => await service.ImportModelAsync(
            provider: "Ollama", request: new ModelImportRequest());

        await action.Should().ThrowAsync<ArgumentException>();
        brokerMock.VerifyNoOtherCalls();
    }

    private ModelManagerService CreateService() => new(
        modelProviderBroker: brokerMock.Object,
        aiConfiguration: new AIConfiguration
        {
            DefaultProvider = "Ollama",
            Providers = new AIProvidersConfiguration
            {
                ["Ollama"] = Provider("Ollama", AIModelProviderMode.OllamaApi, endpoint: "http://ollama", defaultModel: "llama", maxConcurrency: 0),
                ["Azure"] = Provider("Azure", AIModelProviderMode.AzureFoundryDeployments, endpoint: "http://azure"),
                ["OpenAI"] = Provider("OpenAI", AIModelProviderMode.OpenAICompatible, endpoint: "http://openai"),
                ["Disabled"] = Provider("Disabled", AIModelProviderMode.OllamaApi, endpoint: string.Empty),
            },
        });

    private static AIProviderConfiguration Provider(
        string name,
        AIModelProviderMode mode,
        string endpoint,
        string defaultModel = "model",
        int maxConcurrency = 2) => new()
        {
            Name = name,
            MaxConcurrency = maxConcurrency,
            CompletionProvider = new AICompletionProviderConfiguration { DefaultModel = defaultModel },
            ModelProvider = new AIModelProviderConfiguration { Mode = mode, Endpoint = endpoint },
        };
}
