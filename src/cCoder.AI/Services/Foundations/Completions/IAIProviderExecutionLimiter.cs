namespace cCoder.AI.Services.Foundations.Completions;

public interface IAIProviderExecutionLimiter
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string providerKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default);
}
