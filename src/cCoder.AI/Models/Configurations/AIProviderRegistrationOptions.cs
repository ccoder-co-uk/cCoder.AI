// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public abstract class AIProviderRegistrationOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ModelEndpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 120;
    public double Temperature { get; set; } = 0.2;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryBaseDelayMilliseconds { get; set; } = 500;
}