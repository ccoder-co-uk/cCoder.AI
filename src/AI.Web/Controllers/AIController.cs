using cCoder.AI.Models.Requests;
using System.Text.Json;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;
using AI.Web.Services.Diagnostics;

namespace AI.Web.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class AIController(
    ICompletionProviderService completionProviderService,
    IAgentOrchestrationService agentOrchestrationService,
    IAgentRunHistoryService agentRunHistoryService)
    : ControllerBase
{
    [HttpPost("Completions")]
    public async ValueTask<IActionResult> PostCompletionsAsync(
        [FromBody] CompletionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedOn = DateTimeOffset.UtcNow;
        var response = await completionProviderService.CompleteAsync(request, cancellationToken);

        agentRunHistoryService.Record(new AgentRunHistoryEntry
        {
            Source = "AI API",
            Operation = "Completion",
            Provider = response.Provider,
            Model = response.Model,
            Succeeded = true,
            Iterations = 1,
            Summary = request.Prompt,
            RecordedOn = DateTimeOffset.UtcNow,
            Duration = DateTimeOffset.UtcNow - startedOn
        });

        return Ok(response);
    }

    [HttpPost("Agents")]
    public async ValueTask<IActionResult> PostAgentsAsync(
        [FromBody] AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedOn = DateTimeOffset.UtcNow;
        var response = await agentOrchestrationService.RunAsync(request, cancellationToken);

        agentRunHistoryService.Record(new AgentRunHistoryEntry
        {
            Source = "AI API",
            Operation = "Agent Run",
            Provider = response.Provider,
            Model = response.Model,
            Succeeded = response.Succeeded,
            Iterations = response.Iterations,
            Summary = request.Instructions,
            RecordedOn = DateTimeOffset.UtcNow,
            Duration = DateTimeOffset.UtcNow - startedOn
        });

        return Ok(response);
    }

    [HttpPost("Agents/Stream")]
    public async Task StreamAgentsAsync(
        [FromBody] AgentRunRequest request,
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

        await foreach (var token in agentOrchestrationService.StreamAsync(
            request,
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
            Source = "AI API",
            Operation = "Agent Stream",
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
}
