// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Models;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.AI.Exposures.Controllers;

[ApiController]
[Route("Api/AI/Model")]
[Route("Api/Model")]
public sealed class ModelController(
    IModelManager modelManagerService)
    : ControllerBase
{
    [HttpGet("Providers/{provider}/Available")]
    public async ValueTask<IActionResult> GetAvailableModelsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        try
        {
            var modelResponses =
                await modelManagerService.RetrieveAvailableModelsAsync(
                    provider: provider,
                    cancellationToken: cancellationToken);

            return Ok(value: modelResponses);
        }
        catch (ArgumentException)
        {
            return BadRequest(error: "The model provider is invalid.");
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }

    [HttpPost("Providers/{provider}/Import")]
    public async ValueTask<IActionResult> PostImportModelAsync(
        string provider,
        [FromBody] ModelImportRequest modelImportRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var modelImportResponse =
                await modelManagerService.ImportModelAsync(
                    provider: provider,
                    request: modelImportRequest,
                    cancellationToken: cancellationToken);

            return Ok(value: modelImportResponse);
        }
        catch (ArgumentException)
        {
            return BadRequest(error: "The model import request is invalid.");
        }
        catch (Exception)
        {
            return StatusCode(statusCode: 500);
        }
    }
}
