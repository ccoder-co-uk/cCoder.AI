// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.Completions;
using cCoder.AI.Brokers.ModelProviders;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Exposures;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Models;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace cCoder.AI;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAIWeb(
        this IServiceCollection services,
        Action<AIConfiguration> configure)
    {
        AIConfiguration configuration = new();
        configure?.Invoke(configuration);

        return services.AddAIWeb(configuration);
    }

    public static IServiceCollection AddAIWeb(
        this IServiceCollection services,
        AIConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);

        services.AddSingleton(implementationInstance: configuration);
        services.AddSingleton<IOptions<AIConfiguration>>(
            implementationFactory: _ =>
                Options.Create(configuration));
        services.AddBrokers();
        services.AddFoundations();
        services.AddOrchestrations();
        services.AddExposures();

        return services;
    }

    private static void AddBrokers(
        this IServiceCollection services)
    {
        IHttpClientBuilder completions = services.AddHttpClient<
            IChatCompletionsBroker,
            ChatCompletionsBroker>();

        completions.ConfigureHttpClient(
            configureClient: client =>
                client.Timeout = Timeout.InfiniteTimeSpan);

        services.AddSingleton<ICodexCliBroker, CodexCliBroker>();

        IHttpClientBuilder models = services.AddHttpClient<
            IModelProviderBroker,
            ModelProviderBroker>();

        models.ConfigureHttpClient(
            configureClient: client =>
                client.Timeout = Timeout.InfiniteTimeSpan);

        services.AddTransient<IShellBroker, ShellBroker>();
    }

    private static void AddFoundations(
        this IServiceCollection services)
    {
        services.AddSingleton<IAIProviderExecutionLimiter, AIProviderExecutionLimiter>();
        services.AddTransient<ICompletionProviderService, CompletionProviderService>();
        services.AddTransient<IModelManagerService, ModelManagerService>();
    }

    private static void AddOrchestrations(
        this IServiceCollection services) =>
        services.AddTransient<IAgentOrchestrationService, AgentOrchestrationService>();

    private static void AddExposures(
        this IServiceCollection services)
    {
        services.AddTransient<ChatContext>();

        IMvcBuilder mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(
            assembly: typeof(ChatContext).Assembly);
    }
}