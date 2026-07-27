// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Configurations;

public class AIModelProviderConfiguration
{
    public AIModelProviderMode Mode { get; set; } = AIModelProviderMode.OllamaApi;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "Authorization";
    public string ApiKeyScheme { get; set; } = "Bearer";
    public int TimeoutSeconds { get; set; } = 120;
}