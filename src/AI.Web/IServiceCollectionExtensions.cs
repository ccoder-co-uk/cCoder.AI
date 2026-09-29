// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AI.Web.Brokers.Loggings;
using AI.Web.Models;
using AI.Web.Exposures;
using AI.Web.Services.Diagnostics;
using cCoder.AI;

namespace AI.Web;

public static class IServiceCollectionExtensions
{
    public static void AddWeb(
        this IServiceCollection services,
        IConfiguration applicationConfiguration,
        Action<AppConfiguration> configure = null)
    {
        AppConfiguration configuration = new();
        applicationConfiguration.Bind(configuration);
        configure?.Invoke(configuration);

        services.AddTransient<ILoggingBroker, LoggingBroker>();
        services.AddFoundations();
        services.AddExposures();
        cCoder.AI.IServiceCollectionExtensions.AddAIWeb(
            services,
            configuration.AI);
    }

    private static void AddFoundations(
        this IServiceCollection services)
    {
        services.AddSingleton<IAgentRunHistoryService, AgentRunHistoryService>();
        services.AddSingleton<IAgentRunHistoryManager, AgentRunHistoryService>();
    }

    private static void AddExposures(
        this IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
    }
}