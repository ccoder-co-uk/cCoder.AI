// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Configurations;

public static class AIConfigurationExtensions
{
    public static AIConfiguration AddOllama(
        this AIConfiguration configuration,
        string key,
        Action<OllamaAIProviderOptions> configure)
    {
        OllamaAIProviderOptions options = new();
        configure?.Invoke(obj: options);
        string baseEndpoint = TrimKnownSuffix(endpoint: options.Endpoint, suffix: "/api/chat");

        return AddProvider(configuration: configuration, key: key, options: options, provider: new AIProviderConfiguration
        {
            CompletionProvider = BuildCompletion(
                options,
                AIProviderMode.OllamaApi,
                AppendPath(baseEndpoint, "api/chat"),
                "Authorization",
                "Bearer"),
            ModelProvider = BuildModel(
                options,
                AIModelProviderMode.OllamaApi,
                Coalesce(options.ModelEndpoint, baseEndpoint),
                "Authorization",
                "Bearer")
        });
    }

    public static AIConfiguration AddOpenAI(
        this AIConfiguration configuration,
        string key,
        Action<OpenAIProviderOptions> configure)
    {
        OpenAIProviderOptions options = new();
        configure?.Invoke(obj: options);
        string baseEndpoint = TrimKnownSuffix(endpoint: options.Endpoint, suffix: "/chat/completions");

        return AddProvider(configuration: configuration, key: key, options: options, provider: new AIProviderConfiguration
        {
            CompletionProvider = BuildCompletion(
                options,
                AIProviderMode.OpenAICompatible,
                AppendPath(baseEndpoint, "chat/completions"),
                "Authorization",
                "Bearer"),
            ModelProvider = BuildModel(
                options,
                AIModelProviderMode.OpenAICompatible,
                Coalesce(options.ModelEndpoint, baseEndpoint),
                "Authorization",
                "Bearer")
        });
    }

    public static AIConfiguration AddPeerLlm(
        this AIConfiguration configuration,
        string key,
        Action<PeerLlmProviderOptions> configure)
    {
        PeerLlmProviderOptions options = new();
        configure?.Invoke(obj: options);
        string baseEndpoint = TrimKnownSuffix(endpoint: options.Endpoint, suffix: "/chat/completions");

        return AddProvider(configuration: configuration, key: key, options: options, provider: new AIProviderConfiguration
        {
            CompletionProvider = BuildCompletion(
                options,
                AIProviderMode.OpenAICompatible,
                AppendPath(baseEndpoint, "chat/completions"),
                "Authorization",
                "Bearer"),
            ModelProvider = BuildModel(
                options,
                AIModelProviderMode.OpenAICompatible,
                Coalesce(options.ModelEndpoint, baseEndpoint),
                "Authorization",
                "Bearer")
        });
    }

    public static AIConfiguration AddFoundry(
        this AIConfiguration configuration,
        string key,
        Action<FoundryAIProviderOptions> configure)
    {
        FoundryAIProviderOptions options = new();
        configure?.Invoke(obj: options);

        return AddProvider(configuration: configuration, key: key, options: options, provider: new AIProviderConfiguration
        {
            CompletionProvider = BuildCompletion(
                options,
                AIProviderMode.AzureFoundry,
                BuildFoundryCompletionEndpoint(options.Endpoint),
                options.ApiKeyHeaderName,
                options.ApiKeyScheme),
            ModelProvider = BuildModel(
                options,
                AIModelProviderMode.AzureFoundryDeployments,
                Coalesce(options.ModelEndpoint, options.Endpoint),
                options.ApiKeyHeaderName,
                options.ApiKeyScheme)
        });
    }

    public static AIConfiguration AddCodex(
        this AIConfiguration configuration,
        string key,
        Action<CodexAIProviderOptions> configure)
    {
        CodexAIProviderOptions options = new();
        configure?.Invoke(obj: options);
        AIProviderConfiguration provider = new()
        {
            CompletionProvider = BuildCompletion(
options: options,
mode: AIProviderMode.CodexCli,
endpoint: options.ExecutablePath,
apiKeyHeaderName: string.Empty,
apiKeyScheme: string.Empty),
            ModelProvider = new AIModelProviderConfiguration(),
            CodexCli = new CodexCliConfiguration
            {
                ExecutablePath = options.ExecutablePath?.Trim() ?? "codex",
                WorkingDirectory = options.WorkingDirectory?.Trim() ?? string.Empty,
                SandboxMode = options.SandboxMode?.Trim() ?? "read-only",
                ReasoningEffort = options.ReasoningEffort?.Trim() ?? "low",
                IgnoreUserConfiguration = options.IgnoreUserConfiguration,
                IgnoreRules = options.IgnoreRules,
                UseOss = options.UseOss,
                LocalProvider = options.LocalProvider?.Trim() ?? string.Empty
            }
        };

        return AddProvider(configuration: configuration, key: key, options: options, provider: provider);
    }

    static AIConfiguration AddProvider(
        AIConfiguration configuration,
        string key,
        AIProviderRegistrationOptions options,
        AIProviderConfiguration provider)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);
        string providerKey = ValidateKey(key: key);

        provider.Name = providerKey;
        provider.MaxConcurrency = Math.Max(val1: 1, val2: options.MaxConcurrency);
        configuration.Providers[providerKey] = provider;
        return configuration;
    }

    static AICompletionProviderConfiguration BuildCompletion(
        AIProviderRegistrationOptions options,
        AIProviderMode mode,
        string endpoint,
        string apiKeyHeaderName,
        string apiKeyScheme) => new()
        {
            Mode = mode,
            Endpoint = endpoint?.Trim() ?? string.Empty,
            DefaultModel = options.Model?.Trim() ?? string.Empty,
            ApiKey = options.ApiKey?.Trim() ?? string.Empty,
            ApiKeyHeaderName = apiKeyHeaderName,
            ApiKeyScheme = apiKeyScheme,
            TimeoutSeconds = Math.Max(val1: 1, val2: options.TimeoutSeconds),
            Temperature = options.Temperature,
            MaxRetryAttempts = Math.Max(val1: 0, val2: options.MaxRetryAttempts),
            RetryBaseDelayMilliseconds = Math.Max(val1: 1, val2: options.RetryBaseDelayMilliseconds)
        };

    static AIModelProviderConfiguration BuildModel(
        AIProviderRegistrationOptions options,
        AIModelProviderMode mode,
        string endpoint,
        string apiKeyHeaderName,
        string apiKeyScheme) => new()
        {
            Mode = mode,
            Endpoint = endpoint?.Trim() ?? string.Empty,
            ApiKey = options.ApiKey?.Trim() ?? string.Empty,
            ApiKeyHeaderName = apiKeyHeaderName,
            ApiKeyScheme = apiKeyScheme,
            TimeoutSeconds = Math.Max(val1: 1, val2: options.TimeoutSeconds)
        };

    static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(value: key))
            throw new ArgumentException(message: "A provider key is required.", paramName: nameof(key));

        return key.Trim();
    }

    static string Coalesce(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(value: preferred) ? fallback : preferred.Trim();

    static string AppendPath(string endpoint, string relativePath) =>
        $"{endpoint?.TrimEnd(trimChar: '/')}/{relativePath.TrimStart(trimChar: '/')}";

    static string TrimKnownSuffix(string endpoint, string suffix)
    {
        string value = endpoint?.Trim().TrimEnd(trimChar: '/') ?? string.Empty;
        return value.EndsWith(value: suffix, comparisonType: StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    static string BuildFoundryCompletionEndpoint(string endpoint)
    {
        string value = endpoint?.Trim().TrimEnd(trimChar: '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value: value)
            || value.Contains(value: "/chat/completions", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.EndsWith(value: "/openai/v1", comparisonType: StringComparison.OrdinalIgnoreCase))
            return AppendPath(endpoint: value, relativePath: "chat/completions");

        if (value.EndsWith(value: "/models", comparisonType: StringComparison.OrdinalIgnoreCase))
            return AppendPath(endpoint: value, relativePath: "chat/completions?api-version=2024-05-01-preview");

        return AppendPath(endpoint: value, relativePath: "models/chat/completions?api-version=2024-05-01-preview");
    }
}