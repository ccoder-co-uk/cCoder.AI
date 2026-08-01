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
    ICompletionProviderManager completionProviderService,
    IAgentManager agentOrchestrationService,
    ChatContext chatContext)
    : ControllerBase
{
    [HttpPost("Completions")]
    public async ValueTask<IActionResult> PostCompletionsAsync(
        [FromBody] CompletionRequest completionRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var completionResponse =
                await completionProviderService.CompleteAsync(
                    request: completionRequest,
                    cancellationToken: cancellationToken);

            return Ok(value: completionResponse);
        }
        catch (ArgumentException)
        {
            return BadRequest(error: "The completion request is invalid.");
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }

    [HttpPost("Agents")]
    public async ValueTask<IActionResult> PostAgentsAsync(
        [FromBody] AgentRunRequest agentRunRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var agentRunResponse =
                await agentOrchestrationService.RunAsync(
                    request: agentRunRequest,
                    cancellationToken: cancellationToken);

            return Ok(value: agentRunResponse);
        }
        catch (ArgumentException)
        {
            return BadRequest(error: "The agent request is invalid.");
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }

    [HttpPost("Agents/Stream")]
    public async Task<IActionResult> StreamAgentsAsync(
        [FromBody] AgentRunRequest agentRunRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteStreamAsync(
                tokens: agentOrchestrationService.StreamAsync(
                    request: agentRunRequest,
                    cancellationToken: cancellationToken),
                cancellationToken: cancellationToken);

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

            return BadRequest(error: "The agent stream request is invalid.");
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

    [HttpPost("Chat")]
    public async ValueTask<IActionResult> PostChatAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var chatResponse =
                await chatContext.InferAsync(
                    chatRequest: chatRequest,
                    cancellationToken: cancellationToken);

            return Ok(value: chatResponse);
        }
        catch (ArgumentException)
        {
            return BadRequest(error: "The chat request is invalid.");
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }

    [HttpPost("Chat/Stream")]
    public async Task<IActionResult> StreamChatAsync(
        [FromBody] ChatRequest chatRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteStreamAsync(
                tokens: chatContext.InferAsStreamAsync(
                    chatRequest: chatRequest,
                    cancellationToken: cancellationToken),
                cancellationToken: cancellationToken);

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

            return BadRequest(error: "The chat stream request is invalid.");
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