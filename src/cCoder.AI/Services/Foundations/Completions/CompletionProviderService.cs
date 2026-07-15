using cCoder.AI.Brokers.Completions;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Services.Foundations.Completions;

public class CompletionProviderService(
    IChatCompletionsBroker chatCompletionsBroker,
    ICodexCliBroker codexCliBroker,
    IAIProviderExecutionLimiter providerExecutionLimiter,
    AIConfiguration aiConfiguration)
    : ICompletionProviderService
{
    public ValueTask<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePrompt(request.Prompt);

        List<ChatCompletionMessage> messages = [];

        if (string.IsNullOrWhiteSpace(request.SystemPrompt) is false)
        {
            messages.Add(new ChatCompletionMessage("system", request.SystemPrompt));
        }

        messages.Add(new ChatCompletionMessage("user", request.Prompt));

        return CompleteChatAsync(
            request.Provider,
            request.Model,
            messages,
            request.Temperature,
            false,
            cancellationToken);
    }

    public async ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature = null,
        bool enableShellTooling = false,
        CancellationToken cancellationToken = default)
    {
        (string providerKey, AIProviderConfiguration providerConfiguration) = ResolveProviderConfiguration(provider);
        string providerName = ResolveProviderName(providerConfiguration, providerKey);
        AICompletionProviderConfiguration completionProvider = providerConfiguration.CompletionProvider;
        string resolvedModel = string.IsNullOrWhiteSpace(model)
            ? completionProvider.DefaultModel
            : model;

        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            throw new InvalidOperationException(
                $"No model was provided for AI provider '{providerName}'.");
        }

        if (completionProvider.Mode != cCoder.AI.Models.Enums.AIProviderMode.CodexCli
            && string.IsNullOrWhiteSpace(completionProvider.Endpoint))
        {
            throw new InvalidOperationException(
                $"No endpoint was configured for AI provider '{providerName}'.");
        }

        await using IAsyncDisposable lease = await providerExecutionLimiter.AcquireAsync(
            providerKey,
            providerConfiguration.MaxConcurrency,
            cancellationToken);

        ProviderCompletionRequest providerRequest = new()
        {
            EnableShellTooling = enableShellTooling,
            Messages = messages,
            Model = resolvedModel,
            Temperature = temperature ?? completionProvider.Temperature,
        };

        return completionProvider.Mode == cCoder.AI.Models.Enums.AIProviderMode.CodexCli
            ? await codexCliBroker.CompleteAsync(
                providerName,
                providerConfiguration,
                providerRequest,
                cancellationToken)
            : await chatCompletionsBroker.PostChatCompletionAsync(
                providerName,
                completionProvider,
                providerRequest,
                cancellationToken);
    }

    private (string ProviderKey, AIProviderConfiguration Configuration) ResolveProviderConfiguration(string? provider)
    {
        string resolvedProviderName = string.IsNullOrWhiteSpace(provider)
            ? aiConfiguration.DefaultProvider
            : provider;

        if (aiConfiguration.Providers.TryGetValue(resolvedProviderName, out AIProviderConfiguration? configuration))
        {
            configuration.Name = ResolveProviderName(configuration, resolvedProviderName);
            return (resolvedProviderName, configuration);
        }

        throw new InvalidOperationException($"Unsupported AI provider '{resolvedProviderName}'.");
    }

    private static string ResolveProviderName(
        AIProviderConfiguration providerConfiguration,
        string? providerName) =>
        string.IsNullOrWhiteSpace(providerConfiguration.Name)
            ? providerName?.Trim() ?? string.Empty
            : providerConfiguration.Name;

    private static void ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }
    }
}
