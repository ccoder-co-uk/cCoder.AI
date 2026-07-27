// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Text.Json;
using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Orchestrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.AI.Exposures.Controllers;

[ApiController]
[Route("Api/[controller]")]
public sealed class AIController(
    ICompletionProviderService completionProviderService,
    IAgentOrchestrationService agentOrchestrationService,
    ChatContext chatContext)
    : ControllerBase
{
    [HttpPost("Completions")]
    public async ValueTask<IActionResult> PostCompletionsAsync(
        [FromBody] CompletionRequest completionRequest,
        CancellationToken cancellationToken)
    {
        var completionResponse =
            await completionProviderService.CompleteAsync(
                request: completionRequest,
                cancellationToken: cancellationToken);

        return Ok(value: completionResponse);
    }

    [HttpPost("Agents")]
    public async ValueTask<IActionResult> PostAgentsAsync(
        [FromBody] AgentRunRequest agentRunRequest,
        CancellationToken cancellationToken)
    {
        var agentRunResponse =
            await agentOrchestrationService.RunAsync(
                request: agentRunRequest,
                cancellationToken: cancellationToken);

        return Ok(value: agentRunResponse);
    }

    [HttpPost("Agents/Stream")]
    public Task StreamAgentsAsync(
        [FromBody] AgentRunRequest agentRunRequest,
        CancellationToken cancellationToken) =>
        WriteStreamAsync(
            tokens: agentOrchestrationService.StreamAsync(
                request: agentRunRequest,
                cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);

    [HttpPost("Chat")]
    public async ValueTask<IActionResult> PostChatAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken)
    {
        var chatResponse =
            await chatContext.InferAsync(
                chatRequest: chatRequest,
                cancellationToken: cancellationToken);

        return Ok(value: chatResponse);
    }

    [HttpPost("Chat/Stream")]
    public Task StreamChatAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken) =>
        WriteStreamAsync(
            tokens: chatContext.InferAsStreamAsync(
                chatRequest: chatRequest,
                cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);

    private async Task WriteStreamAsync(
        IAsyncEnumerable<Models.Responses.AgentStreamTokenResponse> tokens,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";

        await foreach (var token in tokens.WithCancellation(cancellationToken: cancellationToken))
        {
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
    }
}
