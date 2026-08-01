// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class AIProvidersConfiguration
    : Dictionary<string, AIProviderConfiguration>
{
    public AIProvidersConfiguration()
        : base(comparer: StringComparer.OrdinalIgnoreCase)
    {
    }
}