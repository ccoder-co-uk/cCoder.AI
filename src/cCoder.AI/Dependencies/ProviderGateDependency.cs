// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Dependencies;

internal sealed class ProviderGateDependency(int maxConcurrency) :
    SemaphoreSlim(
        initialCount: maxConcurrency,
        maxCount: maxConcurrency)
{
    internal int MaxConcurrency { get; } = maxConcurrency;

    internal async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken)
    {
        await WaitAsync(cancellationToken: cancellationToken);
        return new GateLease(providerGate: this);
    }

    private sealed class GateLease(
        ProviderGateDependency providerGate) :
        IAsyncDisposable
    {
        private int released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(
                location1: ref released,
                value: 1) == 0)
            {
                providerGate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
