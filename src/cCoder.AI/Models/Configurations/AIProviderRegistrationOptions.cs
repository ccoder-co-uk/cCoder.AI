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

public sealed class OllamaAIProviderOptions : AIProviderRegistrationOptions
{
    public OllamaAIProviderOptions()
    {
        Endpoint = "http://localhost:11434";
    }
}

public sealed class OpenAIProviderOptions : AIProviderRegistrationOptions
{
    public OpenAIProviderOptions()
    {
        Endpoint = "https://api.openai.com/v1";
    }
}

public sealed class FoundryAIProviderOptions : AIProviderRegistrationOptions
{
    public string ApiKeyHeaderName { get; set; } = "api-key";
    public string ApiKeyScheme { get; set; } = string.Empty;
}

public sealed class CodexAIProviderOptions : AIProviderRegistrationOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkingDirectory { get; set; } = string.Empty;
    public string SandboxMode { get; set; } = "read-only";
    public string ReasoningEffort { get; set; } = "low";
    public bool IgnoreUserConfiguration { get; set; } = true;
    public bool IgnoreRules { get; set; } = true;
    public bool UseOss { get; set; }
    public string LocalProvider { get; set; } = string.Empty;
}

public sealed class PeerLlmProviderOptions : AIProviderRegistrationOptions
{
    public PeerLlmProviderOptions()
    {
        Endpoint = "https://api.peerllm.com/v1";
        Model = "LLooMA2.0";
    }
}