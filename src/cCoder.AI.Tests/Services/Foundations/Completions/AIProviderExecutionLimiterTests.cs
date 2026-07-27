// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using FluentAssertions;
using cCoder.AI.Services.Foundations.Completions;

namespace cCoder.AI.Tests.Services.Foundations.Completions;

public sealed class AIProviderExecutionLimiterTests
{
    [Fact]
    public async Task ShouldLimitConcurrencyIndependentlyByProviderKey()
    {
        using AIProviderExecutionLimiter limiter = new();
        IAsyncDisposable firstLease = await limiter.AcquireAsync(providerKey: "open-ai", maxConcurrency: 1);

        Task<IAsyncDisposable> queued = limiter.AcquireAsync(providerKey: "open-ai", maxConcurrency: 1).AsTask();
        Task<IAsyncDisposable> otherProvider = limiter.AcquireAsync(providerKey: "ollama", maxConcurrency: 1).AsTask();

        queued.IsCompleted.Should().BeFalse();
        otherProvider.IsCompletedSuccessfully.Should().BeTrue();

        await firstLease.DisposeAsync();
        IAsyncDisposable secondLease = await queued.WaitAsync(timeout: TimeSpan.FromSeconds(1));
        await secondLease.DisposeAsync();
        await (await otherProvider).DisposeAsync();
    }
}