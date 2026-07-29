// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Dependencies;
using cCoder.AI.Models.Configurations;

namespace cCoder.AI.Brokers.ModelProviders;

internal sealed class ModelProviderBroker(
    ModelProviderDependency dependency) :
    IModelProviderBroker
{
    public ValueTask<string> GetStringAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        CancellationToken cancellationToken = default) =>
        dependency.GetStringAsync(
            configuration: configuration,
            relativePath: relativePath,
            cancellationToken: cancellationToken);

    public ValueTask<string> PostAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        object payload,
        CancellationToken cancellationToken = default) =>
        dependency.PostAsync(
            configuration: configuration,
            relativePath: relativePath,
            payload: payload,
            cancellationToken: cancellationToken);
}