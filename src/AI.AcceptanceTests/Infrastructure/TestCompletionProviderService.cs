// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.AI.Exposures;
using System.Collections.Concurrent;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestCompletionProviderService : ICompletionProviderManager
{
    private readonly ConcurrentQueue<CompletionResponse> completionResponses = new();
    private Exception? exception;

    public List<CompletionRequest> CompletionRequests { get; } = [];
    public List<(string? Provider, string? Model, IReadOnlyList<ChatCompletionMessage> Messages)> ChatRequests { get; } = [];

    public void Reset()
    {
        CompletionRequests.Clear();
        ChatRequests.Clear();
        exception = null;

        while (completionResponses.TryDequeue(result: out _))
        {
        }
    }

    public void EnqueueResponse(CompletionResponse completionResponse) =>
        completionResponses.Enqueue(item: completionResponse);

    public void FailWith(Exception expectedException) =>
        exception = expectedException;

    public ValueTask<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CompletionRequests.Add(item: request);

        if (exception is not null)
        {
            throw exception;
        }

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
