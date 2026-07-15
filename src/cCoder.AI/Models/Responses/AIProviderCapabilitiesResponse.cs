namespace cCoder.AI.Models.Responses;

public sealed class AIProviderCapabilitiesResponse
{
    public string Provider { get; init; } = string.Empty;
    public string DefaultModel { get; init; } = string.Empty;
    public int MaxConcurrency { get; init; } = 1;
    public bool SupportsModelListing { get; init; }
    public bool SupportsModelImport { get; init; }
}
