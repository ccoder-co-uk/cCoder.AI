using cCoder.AI.Brokers.Completions;
using cCoder.AI.Brokers.ModelProviders;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Models;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace cCoder.AI;

public static partial class IServiceCollectionExtensions
{
    public static IServiceCollection AddAI(
        this IServiceCollection services,
        Action<AIConfiguration> configure) =>
        services.AddAI((_, configuration) => configure?.Invoke(configuration));

    public static IServiceCollection AddAI(
        this IServiceCollection services,
        Action<IServiceCollection, AIConfiguration> configure)
    {
        AIConfiguration configuration = CreateConfiguration(services, configure);
        RegisterAI(services, configuration);

        return services;
    }

    public static IServiceCollection AddAI(
        this IServiceCollection services,
        AIConfiguration aiConfiguration)
    {
        AIConfiguration configuration = aiConfiguration ?? new AIConfiguration();
        RegisterAI(services, configuration);

        return services;
    }

    private static AIConfiguration CreateConfiguration(
        IServiceCollection services,
        Action<IServiceCollection, AIConfiguration> configure)
    {
        AIConfiguration configuration = new();
        configure?.Invoke(services, configuration);

        return configuration;
    }

    private static void RegisterAI(
        IServiceCollection services,
        AIConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<IOptions<AIConfiguration>>(_ => Options.Create(configuration));
        services.AddHttpClient<IChatCompletionsBroker, ChatCompletionsBroker>()
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<ICodexCliBroker, CodexCliBroker>();
        services.AddHttpClient<IModelProviderBroker, ModelProviderBroker>()
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IShellBroker, ShellBroker>();
        services.AddSingleton<IAIProviderExecutionLimiter, AIProviderExecutionLimiter>();
        services.AddTransient<ICompletionProviderService, CompletionProviderService>();
        services.AddTransient<IModelManagerService, ModelManagerService>();
        services.AddTransient<IAgentOrchestrationService, AgentOrchestrationService>();
    }
}
