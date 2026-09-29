// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace cCoder.AI.Services.Foundations.Completions;

internal interface IAIProviderExecutionLimiter
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string providerKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default);
}