// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Models;
using AI.Web.Services.Diagnostics;
using cCoder.AI.Exposures;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AI.Web.Controllers;

public class HomeController(
    AIConfiguration aiConfiguration,
    ChatContext chatContext,
    IAgentRunHistoryService agentRunHistoryService)
    : Controller
{
    private const string WorkspaceUseCasePrompt =
        """
        You are operating inside the cCoder.AI manual workspace console.
        Help the user in a Codex-like way: be practical, tool-aware, concise, and action oriented.
        Prefer direct progress over abstract discussion, but stay safe and explain decisions clearly when they matter.
        Treat the local shell as your primary tool and assume the user is testing agent behaviour interactively.
        """;

    public IActionResult Index()
    {
        IReadOnlyList<AIProviderOptionViewModel> providers = aiConfiguration.Providers
            .OrderBy(keySelector: provider => provider.Key)
            .Select(selector: provider => new AIProviderOptionViewModel
            {
                Key = provider.Key,
                Name = string.IsNullOrWhiteSpace(provider.Value.Name) ? provider.Key : provider.Value.Name,
                Description = BuildProviderDescription(provider.Value),
                DefaultModel = provider.Value.CompletionProvider.DefaultModel,
                SupportsModelListing = string.IsNullOrWhiteSpace(provider.Value.ModelProvider.Endpoint) is false,
            })
            .ToList();

        AIWorkspaceViewModel viewModel = new()
        {
            DefaultProvider = aiConfiguration.DefaultProvider,
            DefaultWorkingDirectory = Environment.CurrentDirectory,
            DefaultMaxIterations = aiConfiguration.Agent.MaxIterations,
            UseCasePrompt = WorkspaceUseCasePrompt,
            Providers = providers,
        };

        return View(model: viewModel);
    }

    [HttpPost]
    public async Task StreamConversationAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedOn = DateTimeOffset.UtcNow;
        string provider = chatRequest.Provider ?? string.Empty;
        string model = chatRequest.Model ?? string.Empty;
        int iterations = 0;
        bool succeeded = false;
        string? errorMessage = null;

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";
        chatRequest.SystemPrompt ??= WorkspaceUseCasePrompt;

        await foreach (var token in chatContext.InferAsStreamAsync(
            chatRequest: chatRequest,
            cancellationToken: cancellationToken))
        {
            if (token.Completion is not null)
            {
                provider = token.Completion.Provider;
                model = token.Completion.Model;
                iterations = token.Completion.Iterations;
                succeeded = token.Completion.Succeeded;
            }

            if (token.Type.Equals(
                value: "error",
                comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = token.Content;
            }

            string serializedToken = JsonSerializer.Serialize(value: token);

            await Response.WriteAsync(
                text: serializedToken,
                cancellationToken: cancellationToken);

            await Response.WriteAsync(
                text: "\n",
                cancellationToken: cancellationToken);

            await Response.Body.FlushAsync(
                cancellationToken: cancellationToken);
        }

        agentRunHistoryService.Record(entry: new AgentRunHistoryEntry
        {
            Source = "Workspace",
            Operation = "Manual Chat",
            Provider = provider,
            Model = model,
            Succeeded = succeeded && string.IsNullOrWhiteSpace(errorMessage),
            Iterations = iterations,
            Summary = chatRequest.Instructions,
            ErrorMessage = errorMessage,
            RecordedOn = DateTimeOffset.UtcNow,
            Duration = DateTimeOffset.UtcNow - startedOn,
        });
    }

    private static string BuildProviderDescription(
        AIProviderConfiguration providerConfiguration) =>
        providerConfiguration.CompletionProvider.Mode switch
        {
            AIProviderMode.OllamaApi =>
                "Local Ollama endpoint for private, on-device inference.",
            AIProviderMode.OpenAICompatible =>
                "OpenAI-compatible hosted completion provider.",
            AIProviderMode.AzureFoundry =>
                "Azure AI Foundry hosted model deployment.",
            AIProviderMode.CodexCli =>
                "Codex CLI using its configured ChatGPT or API-key session.",
            _ => "Configured AI provider.",
        };
}
