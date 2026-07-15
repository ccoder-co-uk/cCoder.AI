using System.Collections.Concurrent;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Foundations.Models;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestModelManagerService : IModelManagerService
{
    private readonly ConcurrentDictionary<string, List<ModelDescriptorResponse>> availableModels =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentQueue<ModelImportResponse> importResponses = new();

    public List<(string? Provider, string ModelId)> ImportRequests { get; } = [];
    public List<string?> RetrievalRequests { get; } = [];

    public void Reset()
    {
        availableModels.Clear();
        ImportRequests.Clear();
        RetrievalRequests.Clear();

        while (importResponses.TryDequeue(out _))
        {
        }
    }

    public void SeedAvailableModels(string provider, params ModelDescriptorResponse[] models) =>
        availableModels[provider] = models.ToList();

    public void EnqueueImportResponse(ModelImportResponse response) =>
        importResponses.Enqueue(response);

    public AIProviderCapabilitiesResponse GetProviderCapabilities(string provider) => new()
    {
        Provider = provider,
        DefaultModel = availableModels.TryGetValue(provider, out List<ModelDescriptorResponse>? models)
            ? models.FirstOrDefault()?.Id ?? string.Empty
            : string.Empty,
        MaxConcurrency = 1,
        SupportsModelListing = true,
        SupportsModelImport = true
    };

    public ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAvailableModelsAsync(
        string? provider,
        CancellationToken cancellationToken = default)
    {
        RetrievalRequests.Add(provider);

        if (provider is not null && availableModels.TryGetValue(provider, out List<ModelDescriptorResponse>? models))
        {
            return ValueTask.FromResult<IReadOnlyList<ModelDescriptorResponse>>(models);
        }

        return ValueTask.FromResult<IReadOnlyList<ModelDescriptorResponse>>([]);
    }

    public ValueTask<ModelImportResponse> ImportModelAsync(
        string provider,
        ModelImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ImportRequests.Add((provider, request.ModelId));

        if (importResponses.TryDequeue(out ModelImportResponse? response))
        {
            return ValueTask.FromResult(response);
        }

        return ValueTask.FromResult(new ModelImportResponse
        {
            Provider = provider,
            ModelId = request.ModelId,
            Message = "accepted",
            RawContent = "{}",
            Succeeded = true,
        });
    }
}
