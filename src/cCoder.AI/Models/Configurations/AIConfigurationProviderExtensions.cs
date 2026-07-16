using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Configurations;

public static class AIConfigurationProviderExtensions
{
    public static AIConfiguration AddOllama(
        this AIConfiguration configuration,
        string key,
        Action<OllamaAIProviderOptions> configure)
    {
        OllamaAIProviderOptions options = new();
        configure?.Invoke(options);
        string baseEndpoint = TrimKnownSuffix(options.Endpoint, "/api/chat");

        return AddProvider(configuration, key, options, new AIProviderConfiguration
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
        configure?.Invoke(options);
        string baseEndpoint = TrimKnownSuffix(options.Endpoint, "/chat/completions");

        return AddProvider(configuration, key, options, new AIProviderConfiguration
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
        configure?.Invoke(options);

        return AddProvider(configuration, key, options, new AIProviderConfiguration
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
        configure?.Invoke(options);
        AIProviderConfiguration provider = new()
        {
            CompletionProvider = BuildCompletion(
                options,
                AIProviderMode.CodexCli,
                options.ExecutablePath,
                string.Empty,
                string.Empty),
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

        return AddProvider(configuration, key, options, provider);
    }

    static AIConfiguration AddProvider(
        AIConfiguration configuration,
        string key,
        AIProviderRegistrationOptions options,
        AIProviderConfiguration provider)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string providerKey = ValidateKey(key);

        provider.Name = providerKey;
        provider.MaxConcurrency = Math.Max(1, options.MaxConcurrency);
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
            TimeoutSeconds = Math.Max(1, options.TimeoutSeconds),
            Temperature = options.Temperature,
            MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts),
            RetryBaseDelayMilliseconds = Math.Max(1, options.RetryBaseDelayMilliseconds)
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
            TimeoutSeconds = Math.Max(1, options.TimeoutSeconds)
        };

    static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A provider key is required.", nameof(key));

        return key.Trim();
    }

    static string Coalesce(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    static string AppendPath(string endpoint, string relativePath) =>
        $"{endpoint?.TrimEnd('/')}/{relativePath.TrimStart('/')}";

    static string TrimKnownSuffix(string endpoint, string suffix)
    {
        string value = endpoint?.Trim().TrimEnd('/') ?? string.Empty;
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    static string BuildFoundryCompletionEndpoint(string endpoint)
    {
        string value = endpoint?.Trim().TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            return AppendPath(value, "chat/completions");

        if (value.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            return AppendPath(value, "chat/completions?api-version=2024-05-01-preview");

        return AppendPath(value, "models/chat/completions?api-version=2024-05-01-preview");
    }
}
