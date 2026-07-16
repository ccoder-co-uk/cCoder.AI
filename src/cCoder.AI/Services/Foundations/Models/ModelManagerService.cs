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
        AIProviderConfiguration configuration = ResolveProviderConfiguration(provider);
        bool hasModelEndpoint = !string.IsNullOrWhiteSpace(configuration.ModelProvider.Endpoint);
        bool supportsListing = hasModelEndpoint && configuration.ModelProvider.Mode is
            AIModelProviderMode.OllamaApi
            or AIModelProviderMode.AzureFoundryDeployments
            or AIModelProviderMode.OpenAICompatible;

        return new AIProviderCapabilitiesResponse
        {
            Provider = configuration.Name,
            DefaultModel = configuration.CompletionProvider.DefaultModel,
            MaxConcurrency = Math.Max(1, configuration.MaxConcurrency),
            SupportsModelListing = supportsListing,
            SupportsModelImport = hasModelEndpoint
                && configuration.ModelProvider.Mode == AIModelProviderMode.OllamaApi
        };
    }

    public async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAvailableModelsAsync(
        string? provider,
        CancellationToken cancellationToken = default)
    {
        AIProviderConfiguration providerConfiguration = ResolveProviderConfiguration(provider);
        if (!GetProviderCapabilities(providerConfiguration.Name).SupportsModelListing)
        {
            throw new InvalidOperationException(
                $"Model listing is not supported for provider '{providerConfiguration.Name}'.");
        }

        return providerConfiguration.ModelProvider.Mode switch
        {
            AIModelProviderMode.OllamaApi =>
                await RetrieveOllamaModelsAsync(providerConfiguration, cancellationToken),

            AIModelProviderMode.AzureFoundryDeployments =>
                await RetrieveAzureFoundryDeploymentsAsync(providerConfiguration, cancellationToken),

            AIModelProviderMode.OpenAICompatible =>
                await RetrieveOpenAIModelsAsync(providerConfiguration, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Model listing is not supported for provider '{providerConfiguration.Name}'."),
        };
    }

    public async ValueTask<ModelImportResponse> ImportModelAsync(
        string provider,
        ModelImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateImportRequest(request);

        AIProviderConfiguration providerConfiguration = ResolveProviderConfiguration(provider);

        return providerConfiguration.ModelProvider.Mode switch
        {
            AIModelProviderMode.OllamaApi =>
                await ImportOllamaModelAsync(providerConfiguration, request, cancellationToken),

            AIModelProviderMode.AzureFoundryDeployments =>
                throw new InvalidOperationException(
                    $"Model import for provider '{providerConfiguration.Name}' requires a deployment workflow and is not supported by the basic import endpoint."),

            _ => throw new InvalidOperationException(
                $"Model import is not supported for provider '{providerConfiguration.Name}'."),
        };
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveOllamaModelsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
            providerConfiguration.ModelProvider,
            "api/tags",
            cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(content);

        return jsonNode?["models"]?.AsArray()
            .Select(node => new ModelDescriptorResponse
            {
                Id = node?["name"]?.GetValue<string>() ?? string.Empty,
                Name = node?["name"]?.GetValue<string>() ?? string.Empty,
                Provider = providerConfiguration.Name,
                IsAvailable = true,
                Version = node?["modified_at"]?.GetValue<string>(),
                Description = node?["model"]?.GetValue<string>(),
            })
            .Where(model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAzureFoundryDeploymentsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
            providerConfiguration.ModelProvider,
            "deployments",
            cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(content);

        return jsonNode?["value"]?.AsArray()
            .Select(node =>
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
            .Where(model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveOpenAIModelsAsync(
        AIProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken)
    {
        string content = await modelProviderBroker.GetStringAsync(
            providerConfiguration.ModelProvider,
            "models",
            cancellationToken);

        JsonNode? jsonNode = JsonNode.Parse(content);
        return jsonNode?["data"]?.AsArray()
            .Select(node => new ModelDescriptorResponse
            {
                Id = node?["id"]?.GetValue<string>() ?? string.Empty,
                Name = node?["id"]?.GetValue<string>() ?? string.Empty,
                Provider = providerConfiguration.Name,
                IsAvailable = true,
                Publisher = node?["owned_by"]?.GetValue<string>()
            })
            .Where(model => string.IsNullOrWhiteSpace(model.Id) is false)
            .ToList()
            ?? [];
    }

    private async ValueTask<ModelImportResponse> ImportOllamaModelAsync(
        AIProviderConfiguration providerConfiguration,
        ModelImportRequest request,
        CancellationToken cancellationToken)
    {
        string rawContent = await modelProviderBroker.PostAsync(
            providerConfiguration.ModelProvider,
            "api/pull",
            new
            {
                model = request.ModelId,
                stream = false,
            },
            cancellationToken);

        string status = JsonNode.Parse(rawContent)?["status"]?.GetValue<string>() ?? "submitted";

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
        string resolvedProviderName = string.IsNullOrWhiteSpace(provider)
            ? aiConfiguration.DefaultProvider
            : provider;

        if (aiConfiguration.Providers.TryGetValue(resolvedProviderName, out AIProviderConfiguration? configuration))
        {
            configuration.Name = string.IsNullOrWhiteSpace(configuration.Name)
                ? resolvedProviderName
                : configuration.Name;

            return configuration;
        }

        throw new InvalidOperationException($"Unsupported AI provider '{resolvedProviderName}'.");
    }

    private static void ValidateImportRequest(ModelImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException("ModelId is required.", nameof(request));
        }
    }
}
