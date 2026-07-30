// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Exposures;

public interface IModelManager
{
    AIProviderCapabilitiesResponse GetProviderCapabilities(string provider);

    ValueTask<IReadOnlyList<ModelDescriptorResponse>> RetrieveAvailableModelsAsync(
        string? provider,
        CancellationToken cancellationToken = default);

    ValueTask<ModelImportResponse> ImportModelAsync(
        string provider,
        ModelImportRequest request,
        CancellationToken cancellationToken = default);
}