// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Services.Foundations.Completions;

internal interface IAIProviderExecutionLimiter
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string providerKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default);
}