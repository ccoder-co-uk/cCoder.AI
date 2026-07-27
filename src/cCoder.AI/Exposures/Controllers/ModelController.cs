// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Models;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.AI.Exposures.Controllers;

[ApiController]
[Route("Api/[controller]")]
public sealed class ModelController(
    IModelManagerService modelManagerService)
    : ControllerBase
{
    [HttpGet("Providers/{provider}/Available")]
    public async ValueTask<IActionResult> GetAvailableModelsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        var modelResponses =
            await modelManagerService.RetrieveAvailableModelsAsync(
                provider: provider,
                cancellationToken: cancellationToken);

        return Ok(value: modelResponses);
    }

    [HttpPost("Providers/{provider}/Import")]
    public async ValueTask<IActionResult> PostImportModelAsync(
        string provider,
        [FromBody] ModelImportRequest modelImportRequest,
        CancellationToken cancellationToken)
    {
        var modelImportResponse =
            await modelManagerService.ImportModelAsync(
                provider: provider,
                request: modelImportRequest,
                cancellationToken: cancellationToken);

        return Ok(value: modelImportResponse);
    }
}