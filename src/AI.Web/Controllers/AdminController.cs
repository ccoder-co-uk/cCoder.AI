// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Models;
using AI.Web.Services.Diagnostics;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI.Web.Controllers;

public class AdminController(
    AIConfiguration aiConfiguration,
    IModelManagerService modelManagerService,
    IAgentRunHistoryService agentRunHistoryService)
    : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        List<ProviderDiagnosticsViewModel> providers = [];

        foreach ((string key, AIProviderConfiguration providerConfiguration) in aiConfiguration.Providers.OrderBy(keySelector: item => item.Key))
        {
            ProviderDiagnosticsViewModel provider = new()
            {
                Key = key,
                Name = string.IsNullOrWhiteSpace(value: providerConfiguration.Name) ? key : providerConfiguration.Name,
                CompletionEndpoint = providerConfiguration.CompletionProvider.Endpoint,
                ModelEndpoint = providerConfiguration.ModelProvider.Endpoint,
                DefaultModel = providerConfiguration.CompletionProvider.DefaultModel,
                CompletionApiKeyConfigured = string.IsNullOrWhiteSpace(value: providerConfiguration.CompletionProvider.ApiKey) is false,
                ModelApiKeyConfigured = string.IsNullOrWhiteSpace(value: providerConfiguration.ModelProvider.ApiKey) is false
            };

            try
            {
                provider.AvailableModels = (await modelManagerService
                    .RetrieveAvailableModelsAsync(provider: key, cancellationToken: cancellationToken))
                    .Select(selector: model => model.Name)
                    .ToList();
            }
            catch (Exception exception)
            {
                provider.ModelLookupError = exception.Message;
            }

            providers.Add(item: provider);
        }

        IReadOnlyList<AgentRunHistoryEntry> recentRuns = agentRunHistoryService.RetrieveRecent();

        AdminDashboardViewModel viewModel = new()
        {
            DefaultProvider = aiConfiguration.DefaultProvider,
            ProviderCount = aiConfiguration.Providers.Count,
            RecentRunCount = recentRuns.Count,
            AgentSettings = new AgentSettingsViewModel
            {
                MaxIterations = aiConfiguration.Agent.MaxIterations,
                ShellCommandTimeoutSeconds = aiConfiguration.Agent.ShellCommandTimeoutSeconds,
                StreamingChunkCharacterCount = aiConfiguration.Agent.StreamingChunkCharacterCount,
                StreamingChunkDelayMilliseconds = aiConfiguration.Agent.StreamingChunkDelayMilliseconds
            },
            Providers = providers,
            RecentRuns = recentRuns.Select(selector: run => new RunHistoryItemViewModel
            {
                Source = run.Source,
                Operation = run.Operation,
                Provider = run.Provider,
                Model = run.Model,
                Succeeded = run.Succeeded,
                Iterations = run.Iterations,
                Summary = run.Summary,
                ErrorMessage = run.ErrorMessage,
                RecordedOn = run.RecordedOn,
                DurationMilliseconds = run.Duration.TotalMilliseconds
            }).ToList()
        };

        return View(model: viewModel);
    }
}