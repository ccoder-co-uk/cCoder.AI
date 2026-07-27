// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Completions;

public interface ICodexCliBroker
{
    ValueTask<CompletionResponse> CompleteAsync(
        string providerName,
        AIProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default);
}