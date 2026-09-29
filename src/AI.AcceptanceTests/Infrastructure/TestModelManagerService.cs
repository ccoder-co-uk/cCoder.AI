// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using cCoder.AI.Exposures;
using System.Collections.Concurrent;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestModelManagerService : IModelManager
{
    private Exception? exception;
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
        exception = null;

        while (importResponses.TryDequeue(result: out _))
        {
        }
    }

    public void SeedAvailableModels(string provider, params ModelDescriptorResponse[] models) =>
        availableModels[provider] = models.ToList();

    public void EnqueueImportResponse(ModelImportResponse response) =>
        importResponses.Enqueue(item: response);

    public void FailWith(Exception expectedException) =>
        exception = expectedException;

    public AIProviderCapabilitiesResponse GetProviderCapabilities(string provider) => new()
    {
        Provider = provider,
        DefaultModel = availableModels.TryGetValue(key: provider, value: out List<ModelDescriptorResponse>? models)
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
        RetrievalRequests.Add(item: provider);

        if (exception is not null)
        {
            throw exception;
        }

        if (provider is not null && availableModels.TryGetValue(key: provider, value: out List<ModelDescriptorResponse>? models))
        {
            return ValueTask.FromResult<IReadOnlyList<ModelDescriptorResponse>>(result: models);
        }

        return ValueTask.FromResult<IReadOnlyList<ModelDescriptorResponse>>(result: []);
    }

    public ValueTask<ModelImportResponse> ImportModelAsync(
        string provider,
        ModelImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ImportRequests.Add(item: (provider, request.ModelId));

        if (importResponses.TryDequeue(result: out ModelImportResponse? response))
        {
            return ValueTask.FromResult(result: response);
        }

        return ValueTask.FromResult(result: new ModelImportResponse
        {
            Provider = provider,
            ModelId = request.ModelId,
            Message = "accepted",
            RawContent = "{}",
            Succeeded = true,
        });
    }
}
