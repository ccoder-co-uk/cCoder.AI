// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Concurrent;

namespace cCoder.AI.Services.Foundations.Completions;

public sealed class AIProviderExecutionLimiter : IAIProviderExecutionLimiter, IDisposable
{
    readonly ConcurrentDictionary<string, ProviderGate> gates =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string providerKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value: providerKey))
            throw new ArgumentException(message: "A provider key is required.", paramName: nameof(providerKey));

        int configuredLimit = Math.Max(val1: 1, val2: maxConcurrency);
        ProviderGate gate = gates.GetOrAdd(
key: providerKey.Trim(),
valueFactory: _ => new ProviderGate(configuredLimit));

        if (gate.MaxConcurrency != configuredLimit)
        {
            throw new InvalidOperationException(
message: $"AI provider '{providerKey}' was registered with conflicting concurrency limits.");
        }

        await gate.Semaphore.WaitAsync(cancellationToken: cancellationToken);
        return new GateLease(semaphore: gate.Semaphore);
    }

    public void Dispose()
    {
        foreach (ProviderGate gate in gates.Values)
            gate.Semaphore.Dispose();

        gates.Clear();
    }

    sealed record ProviderGate(int MaxConcurrency)
    {
        public SemaphoreSlim Semaphore { get; } = new(MaxConcurrency, MaxConcurrency);
    }

    sealed class GateLease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        int released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(location1: ref released, value: 1) == 0)
                semaphore.Release();

            return ValueTask.CompletedTask;
        }
    }
}