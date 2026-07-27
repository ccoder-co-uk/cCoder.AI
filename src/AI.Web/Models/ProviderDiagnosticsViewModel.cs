// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace AI.Web.Models;

public class ProviderDiagnosticsViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CompletionEndpoint { get; set; } = string.Empty;
    public string ModelEndpoint { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool CompletionApiKeyConfigured { get; set; }
    public bool ModelApiKeyConfigured { get; set; }
    public IReadOnlyList<string> AvailableModels { get; set; } = Array.Empty<string>();
    public string? ModelLookupError { get; set; }
}