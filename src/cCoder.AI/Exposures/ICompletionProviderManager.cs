// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Exposures;

public interface ICompletionProviderManager
{
    ValueTask<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature = null,
        bool enableShellTooling = false,
        CancellationToken cancellationToken = default);

    ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature,
        bool enableShellTooling,
        IReadOnlyList<string>? inputFilePaths,
        CancellationToken cancellationToken = default) =>
        CompleteChatAsync(
            provider: provider,
            model: model,
            messages: messages,
            temperature: temperature,
            enableShellTooling: enableShellTooling,
            cancellationToken: cancellationToken);
}