// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Exposures;
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
    IAgentRunHistoryManager agentRunHistoryService)
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
        try
        {
            IReadOnlyList<AIProviderOptionViewModel> providers = aiConfiguration.Providers
            .Where(predicate: provider => IsProviderAvailable(provider.Value))
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

        string defaultProvider = providers.Any(
            predicate: provider => provider.Key.Equals(
                value: aiConfiguration.DefaultProvider,
                comparisonType: StringComparison.OrdinalIgnoreCase))
            ? aiConfiguration.DefaultProvider
            : providers.FirstOrDefault()?.Key ?? string.Empty;

        AIWorkspaceViewModel viewModel = new()
        {
            DefaultProvider = defaultProvider,
            DefaultWorkingDirectory = Environment.CurrentDirectory,
            DefaultMaxIterations = aiConfiguration.Agent.MaxIterations,
            UseCasePrompt = WorkspaceUseCasePrompt,
            Providers = providers,
        };

            return View(model: viewModel);
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> StreamConversationAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken)
    {
        try
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

            string serializedToken = JsonSerializer.Serialize(
                value: token,
                options: JsonSerializerOptions.Web);

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

            return Response.HasStarted
                ? new EmptyResult()
                : Ok();
        }
        catch (ArgumentException)
        {
            if (Response.HasStarted)
            {
                throw;
            }

            return BadRequest(error: "The conversation request is invalid.");
        }
        catch (Exception)
        {
            if (Response.HasStarted)
            {
                throw;
            }

            return StatusCode(statusCode: 500);
        }
    }

    private static string BuildProviderDescription(
        AIProviderConfiguration providerConfiguration) =>
        (providerConfiguration.Name, providerConfiguration.CompletionProvider.Mode) switch
        {
            ("PeerLLM", _) =>
                "PeerLLM hosted inference through the decentralized LLooMA network.",
            (_, AIProviderMode.OllamaApi) =>
                "Local Ollama endpoint for private, on-device inference.",
            (_, AIProviderMode.OpenAICompatible) =>
                "OpenAI-compatible hosted completion provider.",
            (_, AIProviderMode.AzureFoundry) =>
                "Azure AI Foundry hosted model deployment.",
            (_, AIProviderMode.CodexCli) =>
                "Codex CLI using its configured ChatGPT or API-key session.",
            _ => "Configured AI provider.",
        };

    private static bool IsProviderAvailable(
        AIProviderConfiguration providerConfiguration) =>
        providerConfiguration.CompletionProvider.Mode switch
        {
            AIProviderMode.CodexCli => true,
            AIProviderMode.OllamaApi =>
                string.IsNullOrWhiteSpace(providerConfiguration.CompletionProvider.Endpoint) is false,
            _ =>
                string.IsNullOrWhiteSpace(providerConfiguration.CompletionProvider.Endpoint) is false
                && string.IsNullOrWhiteSpace(providerConfiguration.CompletionProvider.ApiKey) is false,
        };
}