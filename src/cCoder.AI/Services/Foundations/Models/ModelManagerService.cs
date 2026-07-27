// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Text.Json.Nodes;
using cCoder.AI.Brokers.ModelProviders;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Services.Foundations.Models;

public class ModelManagerService(
    IModelProviderBroker modelProviderBroker,
    AIConfiguration aiConfiguration)
    : IModelManagerService
{
    public AIProviderCapabilitiesResponse GetProviderCapabilities(string provider)
    {
        AIProviderConfiguration configuration = ResolveProviderConfiguration(provider: provider);
        bool hasModelEndpoint = !string.IsNullOrWhiteSpace(value: configuration.ModelProvider.Endpoint);
        bool supportsListing = hasModelEndpoint && configuration.ModelProvider.Mode is
            AIModelProviderMode.OllamaApi
            or AIModelProviderMode.AzureFoundryDeployments
            or AIModelProviderMode.OpenAICompatible;

        return new AIProviderCapabilitiesResponse
        {
            Provider = configuration.Name,
            DefaultModel = configuration.CompletionProvider.DefaultModel,
            MaxConcurrency = Math.Max(val1: 1, val2: configuration.MaxConcurrency),
            SupportsModelListing = supportsListing,
            SupportsModelImport = hasModelEndpoint
                && configuration.ModelProvider.Mode == AIModelProviderMode.OllamaApi
        };
    }

    public async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAvailableModelsAsync(
        string? provider,
        CancellationToken cancellationToken = default)
    {
        AIProviderConfiguration providerConfiguration = ResolveProviderConfiguration(provider: provider);
        if (!GetProviderCapabilities(provider: providerConfiguration.Name).SupportsModelListing)
        {
            throw new InvalidOperationException(
message: $"Model listing is not supported for provider '{providerConfiguration.Name}'.");
        }

        return providerConfiguration.ModelProvider.Mode switch
        {
            AIModelProviderMode.OllamaApi =>
                await RetrieveOllamaModelsAsync(providerConfiguration: providerConfiguration, cancellationToken: cancellationToken),

            AIModelProviderMode.AzureFoundryDeployments =>
                await RetrieveAzureFoundryDeploymentsAsync(providerConfiguration: providerConfiguration, cancellationToken: cancellationToken),

            AIModelProviderMode.OpenAICompatible =>
                await RetrieveOpenAIModelsAsync(providerConfiguration: providerConfiguration, cancellationToken: cancellationToken),

            _ => throw new InvalidOperationException(
message: $"Model listing is not supported for provider '{providerConfiguration.Name}'."),
        };
    }

    public async ValueTask<ModelImportResponse> ImportModelAsync(
        string provider,
        ModelImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateImportRequest(request: request);

        AIProviderConfiguration providerConfiguration = ResolveProviderConfiguration(provider: provider);

        return providerConfiguration.ModelProvider.Mode switch
        {
            AIModelProviderMode.OllamaApi =>
                await ImportOllamaModelAsync(providerConfiguration: providerConfiguration, request: request, cancellationToken: cancellationToken),

            AIModelProviderMode.AzureFoundryDeployments =>
                throw new InvalidOperationException(
message: $"Model import for provider '{providerConfiguration.Name}' requires a deployment workflow and is not supported by the basic import endpoint."),

            _ => throw new InvalidOperationException(
message: $"Model import is not supported for provider '{providerConfiguration.Name}'."),
        };
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveOllamaModelsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
configuration: providerConfiguration.ModelProvider,
relativePath: "api/tags",
cancellationToken: cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(json: content);

        return jsonNode?["models"]?.AsArray()
            .Select(selector: node => new ModelDescriptorResponse
            {
                Id = node?["name"]?.GetValue<string>() ?? string.Empty,
                Name = node?["name"]?.GetValue<string>() ?? string.Empty,
                Provider = providerConfiguration.Name,
                IsAvailable = true,
                Version = node?["modified_at"]?.GetValue<string>(),
                Description = node?["model"]?.GetValue<string>(),
            })
            .Where(predicate: model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAzureFoundryDeploymentsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
configuration: providerConfiguration.ModelProvider,
relativePath: "deployments",
cancellationToken: cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(json: content);

        return jsonNode?["value"]?.AsArray()
            .Select(selector: node =>
            {
                string id = node?["name"]?.GetValue<string>()
                    ?? node?["id"]?.GetValue<string>()
                    ?? string.Empty;

                string name = node?["name"]?.GetValue<string>()
                    ?? node?["properties"]?["model"]?["name"]?.GetValue<string>()
                    ?? id;

                return new ModelDescriptorResponse
                {
                    Id = id,
                    Name = name,
                    Provider = providerConfiguration.Name,
                    IsAvailable = true,
                    Publisher = node?["properties"]?["model"]?["publisher"]?.GetValue<string>(),
                    Version = node?["properties"]?["model"]?["version"]?.GetValue<string>(),
                    Description = node?["properties"]?["model"]?["format"]?.GetValue<string>(),
                };
            })
            .Where(predicate: model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveOpenAIModelsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
configuration: providerConfiguration.ModelProvider,
relativePath: "models",
cancellationToken: cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(json: content);
        return jsonNode?["data"]?.AsArray()
            .Select(selector: node => new ModelDescriptorResponse
            {
                Id = node?["id"]?.GetValue<string>() ?? string.Empty,
                Name = node?["id"]?.GetValue<string>() ?? string.Empty,
                Provider = providerConfiguration.Name,
                IsAvailable = true,
                Publisher = node?["owned_by"]?.GetValue<string>()
            })
            .Where(predicate: model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<ModelImportResponse> ImportOllamaModelAsync(
        AIProviderConfiguration providerConfiguration,
        ModelImportRequest request,
        CancellationToken cancellationToken)
    {
        string rawContent = await modelProviderBroker.PostAsync(
configuration: providerConfiguration.ModelProvider,
relativePath: "api/pull",
payload: new
{
    model = request.ModelId,
    stream = false,
},
cancellationToken: cancellationToken);

        string status = JsonNode.Parse(json: rawContent)?["status"]?.GetValue<string>() ?? "submitted";

        return new ModelImportResponse
        {
            Provider = providerConfiguration.Name,
            ModelId = request.ModelId,
            Succeeded = true,
            Message = status,
            RawContent = rawContent,
        };
    }

    private AIProviderConfiguration ResolveProviderConfiguration(string? provider)
    {
        string resolvedProviderName = string.IsNullOrWhiteSpace(value: provider)
            ? aiConfiguration.DefaultProvider
            : provider;

        if (aiConfiguration.Providers.TryGetValue(key: resolvedProviderName, value: out AIProviderConfiguration? configuration))
        {
            configuration.Name = string.IsNullOrWhiteSpace(value: configuration.Name)
                ? resolvedProviderName
                : configuration.Name;

            return configuration;
        }

        throw new InvalidOperationException(message: $"Unsupported AI provider '{resolvedProviderName}'.");
    }

    private static void ValidateImportRequest(ModelImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(value: request.ModelId))
        {
            throw new ArgumentException(message: "ModelId is required.", paramName: nameof(request));
        }
    }
}