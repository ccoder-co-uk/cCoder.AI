// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Models;
using AI.Web.Services.Diagnostics;
using cCoder.AI;

namespace AI.Web;

public static class IServiceCollectionExtensions
{
    public static void AddAIWeb(
        this IServiceCollection services,
        IConfiguration applicationConfiguration,
        Action<AIWebConfiguration> configure = null)
    {
        AIWebConfiguration configuration = new();
        applicationConfiguration.Bind(configuration);
        configure?.Invoke(configuration);

        services.AddFoundations();
        services.AddExposures();
        cCoder.AI.IServiceCollectionExtensions.AddAIWeb(
            services,
            configuration.AI);
    }

    private static void AddFoundations(
        this IServiceCollection services) =>
        services.AddSingleton<IAgentRunHistoryService, AgentRunHistoryService>();

    private static void AddExposures(
        this IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
    }
}