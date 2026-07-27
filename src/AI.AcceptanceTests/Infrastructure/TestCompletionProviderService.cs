// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Concurrent;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Foundations.Completions;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestCompletionProviderService : ICompletionProviderService
{
    private readonly ConcurrentQueue<CompletionResponse> completionResponses = new();

    public List<CompletionRequest> CompletionRequests { get; } = [];
    public List<(string? Provider, string? Model, IReadOnlyList<ChatCompletionMessage> Messages)> ChatRequests { get; } = [];

    public void Reset()
    {
        CompletionRequests.Clear();
        ChatRequests.Clear();

        while (completionResponses.TryDequeue(result: out _))
        {
        }
    }

    public void EnqueueResponse(CompletionResponse completionResponse) =>
        completionResponses.Enqueue(item: completionResponse);

    public ValueTask<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CompletionRequests.Add(item: request);
        return ValueTask.FromResult(result: DequeueResponse());
    }

    public ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature = null,
        bool enableShellTooling = false,
        CancellationToken cancellationToken = default)
    {
        ChatRequests.Add(item: (provider, model, messages));
        return ValueTask.FromResult(result: DequeueResponse());
    }

    private CompletionResponse DequeueResponse()
    {
        if (completionResponses.TryDequeue(result: out CompletionResponse? completionResponse))
        {
            return completionResponse;
        }

        throw new InvalidOperationException(message: "No completion response was queued for the acceptance test.");
    }
}