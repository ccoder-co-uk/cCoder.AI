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
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("A provider key is required.", nameof(providerKey));

        int configuredLimit = Math.Max(1, maxConcurrency);
        ProviderGate gate = gates.GetOrAdd(
            providerKey.Trim(),
            _ => new ProviderGate(configuredLimit));

        if (gate.MaxConcurrency != configuredLimit)
        {
            throw new InvalidOperationException(
                $"AI provider '{providerKey}' was registered with conflicting concurrency limits.");
        }

        await gate.Semaphore.WaitAsync(cancellationToken);
        return new GateLease(gate.Semaphore);
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
            if (Interlocked.Exchange(ref released, 1) == 0)
                semaphore.Release();

            return ValueTask.CompletedTask;
        }
    }
}
