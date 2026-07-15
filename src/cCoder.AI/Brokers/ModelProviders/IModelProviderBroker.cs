using cCoder.AI.Models.Configurations;

namespace cCoder.AI.Brokers.ModelProviders;

public interface IModelProviderBroker
{
    ValueTask<string> GetStringAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        CancellationToken cancellationToken = default);

    ValueTask<string> PostAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        object payload,
        CancellationToken cancellationToken = default);
}
