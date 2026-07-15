using AI.Web.Models;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AI.Web.Services.Diagnostics;

namespace AI.Web.Controllers;

public class HomeController(
    AIConfiguration aiConfiguration,
    IAgentOrchestrationService agentOrchestrationService,
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
            .OrderBy(provider => provider.Key)
            .Select(provider => new AIProviderOptionViewModel
            {
                Key = provider.Key,
                Name = string.IsNullOrWhiteSpace(provider.Value.Name) ? provider.Key : provider.Value.Name,
                Description = BuildProviderDescription(provider.Key),
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

        return View(viewModel);
    }

    [HttpPost]
    public async Task StreamConversationAsync(
        [FromBody] AgentWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedOn = DateTimeOffset.UtcNow;
        string provider = request.Provider ?? string.Empty;
        string model = request.Model ?? string.Empty;
        int iterations = 0;
        bool succeeded = false;
        string? errorMessage = null;

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";

        AgentRunRequest agentRunRequest = new()
        {
            Instructions = request.Instructions,
            MaxIterations = request.MaxIterations,
            Model = request.Model,
            Provider = request.Provider,
            ShellKind = cCoder.AI.Models.Enums.ShellKind.Auto,
            SystemPrompt = WorkspaceUseCasePrompt,
            WorkingDirectory = request.WorkingDirectory,
        };

        await foreach (var token in agentOrchestrationService.StreamAsync(
            agentRunRequest,
            cancellationToken))
        {
            if (token.Completion is not null)
            {
                provider = token.Completion.Provider;
                model = token.Completion.Model;
                iterations = token.Completion.Iterations;
                succeeded = token.Completion.Succeeded;
            }

            if (token.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = token.Content;
            }

            string serializedToken = JsonSerializer.Serialize(token);
            await Response.WriteAsync(serializedToken, cancellationToken);
            await Response.WriteAsync("\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        agentRunHistoryService.Record(new AgentRunHistoryEntry
        {
            Source = "Workspace",
            Operation = "Manual Chat",
            Provider = provider,
            Model = model,
            Succeeded = succeeded && string.IsNullOrWhiteSpace(errorMessage),
            Iterations = iterations,
            Summary = request.Instructions,
            ErrorMessage = errorMessage,
            RecordedOn = DateTimeOffset.UtcNow,
            Duration = DateTimeOffset.UtcNow - startedOn
        });
    }

    private static string BuildProviderDescription(string providerKey) =>
        providerKey.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "Local OpenAI-compatible endpoint for fast iteration against your machine."
            : providerKey.Equals("AzureFoundry", StringComparison.OrdinalIgnoreCase)
                ? "Remote hosted model using your Azure Foundry-compatible endpoint and key."
                : "Configured AI provider.";
}
