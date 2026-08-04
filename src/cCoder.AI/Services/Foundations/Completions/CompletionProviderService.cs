// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.Completions;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Services.Foundations.Completions;

internal class CompletionProviderService(
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
        ValidatePrompt(prompt: request.Prompt);

        List<ChatCompletionMessage> messages = [];

        if (string.IsNullOrWhiteSpace(value: request.SystemPrompt) is false)
        {
            messages.Add(item: new ChatCompletionMessage { Role = "system", Content = request.SystemPrompt });
        }

        messages.Add(item: new ChatCompletionMessage { Role = "user", Content = request.Prompt });

        return CompleteChatAsync(
provider: request.Provider,
model: request.Model,
messages: messages,
temperature: request.Temperature,
enableShellTooling: false,
cancellationToken: cancellationToken);
    }

    public async ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature = null,
        bool enableShellTooling = false,
        CancellationToken cancellationToken = default) =>
        await CompleteChatAsync(
            provider: provider,
            model: model,
            messages: messages,
            temperature: temperature,
            enableShellTooling: enableShellTooling,
            inputFilePaths: null,
            cancellationToken: cancellationToken);

    public async ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature,
        bool enableShellTooling,
        IReadOnlyList<string>? inputFilePaths,
        CancellationToken cancellationToken = default)
    {
        (string providerKey, AIProviderConfiguration providerConfiguration) = ResolveProviderConfiguration(provider: provider);
        string providerName = ResolveProviderName(providerConfiguration: providerConfiguration, providerName: providerKey);
        AICompletionProviderConfiguration completionProvider = providerConfiguration.CompletionProvider;
        string resolvedModel = string.IsNullOrWhiteSpace(value: model)
            ? completionProvider.DefaultModel
            : model;

        if (string.IsNullOrWhiteSpace(value: resolvedModel))
        {
            throw new InvalidOperationException(
message: $"No model was provided for AI provider '{providerName}'.");
        }

        if (completionProvider.Mode != cCoder.AI.Models.Enums.AIProviderMode.CodexCli
            && string.IsNullOrWhiteSpace(value: completionProvider.Endpoint))
        {
            throw new InvalidOperationException(
message: $"No endpoint was configured for AI provider '{providerName}'.");
        }

        await using IAsyncDisposable lease = await providerExecutionLimiter.AcquireAsync(
providerKey: providerKey,
maxConcurrency: providerConfiguration.MaxConcurrency,
cancellationToken: cancellationToken);

        ProviderCompletionRequest providerRequest = new()
        {
            EnableShellTooling = enableShellTooling,
            InputFilePaths = inputFilePaths ?? Array.Empty<string>(),
            Messages = messages,
            Model = resolvedModel,
            Temperature = temperature ?? completionProvider.Temperature,
        };

        return completionProvider.Mode == cCoder.AI.Models.Enums.AIProviderMode.CodexCli
            ? await codexCliBroker.CompleteAsync(
providerName: providerName,
providerConfiguration: providerConfiguration,
request: providerRequest,
cancellationToken: cancellationToken)
            : await chatCompletionsBroker.PostChatCompletionAsync(
providerName: providerName,
providerConfiguration: completionProvider,
request: providerRequest,
cancellationToken: cancellationToken);
    }

    private (string ProviderKey, AIProviderConfiguration Configuration) ResolveProviderConfiguration(string? provider)
    {
        string resolvedProviderName = string.IsNullOrWhiteSpace(value: provider)
            ? aiConfiguration.DefaultProvider
            : provider;

        if (aiConfiguration.Providers.TryGetValue(key: resolvedProviderName, value: out AIProviderConfiguration? configuration))
        {
            configuration.Name = ResolveProviderName(providerConfiguration: configuration, providerName: resolvedProviderName);
            return (resolvedProviderName, configuration);
        }

        throw new InvalidOperationException(message: $"Unsupported AI provider '{resolvedProviderName}'.");
    }

    private static string ResolveProviderName(
        AIProviderConfiguration providerConfiguration,
        string? providerName) =>
        string.IsNullOrWhiteSpace(value: providerConfiguration.Name)
            ? providerName?.Trim() ?? string.Empty
            : providerConfiguration.Name;

    private static void ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(value: prompt))
        {
            throw new ArgumentException(message: "Prompt is required.", paramName: nameof(prompt));
        }
    }
}
