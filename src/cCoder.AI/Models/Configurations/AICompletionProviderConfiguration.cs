// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Configurations;

public class AICompletionProviderConfiguration
{
    public AIProviderMode Mode { get; set; } = AIProviderMode.OpenAICompatible;
    public string Endpoint { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "Authorization";
    public string ApiKeyScheme { get; set; } = "Bearer";
    public int TimeoutSeconds { get; set; } = 120;
    public double Temperature { get; set; } = 0.2;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryBaseDelayMilliseconds { get; set; } = 500;
}