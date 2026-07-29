// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Dependencies;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Completions;

internal sealed class ChatCompletionsBroker(
    ChatCompletionsDependency dependency) :
    IChatCompletionsBroker
{
    public ValueTask<CompletionResponse> PostChatCompletionAsync(
        string providerName,
        AICompletionProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default) =>
        dependency.PostChatCompletionAsync(
            providerName: providerName,
            providerConfiguration: providerConfiguration,
            request: request,
            cancellationToken: cancellationToken);
}