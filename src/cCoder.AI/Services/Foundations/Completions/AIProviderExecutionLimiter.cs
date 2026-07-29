// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Concurrent;
using cCoder.AI.Dependencies;

namespace cCoder.AI.Services.Foundations.Completions;

public sealed class AIProviderExecutionLimiter : IAIProviderExecutionLimiter, IDisposable
{
    readonly ConcurrentDictionary<string, ProviderGateDependency> gates =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string providerKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value: providerKey))
            throw new ArgumentException(message: "A provider key is required.", paramName: nameof(providerKey));

        int configuredLimit = Math.Max(val1: 1, val2: maxConcurrency);
        ProviderGateDependency gate = gates.GetOrAdd(
key: providerKey.Trim(),
valueFactory: _ => new ProviderGateDependency(
    maxConcurrency: configuredLimit));

        if (gate.MaxConcurrency != configuredLimit)
        {
            throw new InvalidOperationException(
message: $"AI provider '{providerKey}' was registered with conflicting concurrency limits.");
        }

        return await gate.AcquireAsync(
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        foreach (ProviderGateDependency gate in gates.Values)
            gate.Dispose();

        gates.Clear();
    }
}