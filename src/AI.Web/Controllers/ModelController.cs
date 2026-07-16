using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Foundations.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI.Web.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ModelController(IModelManagerService modelManagerService) : ControllerBase
{
    [HttpGet("Providers/{provider}/Available")]
    public async ValueTask<IActionResult> GetAvailableModelsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        var response = await modelManagerService.RetrieveAvailableModelsAsync(provider, cancellationToken);

        return Ok(response);
    }

    [HttpPost("Providers/{provider}/Import")]
    public async ValueTask<IActionResult> PostImportModelAsync(
        string provider,
        [FromBody] ModelImportRequest request,
        CancellationToken cancellationToken)
    {
        var response = await modelManagerService.ImportModelAsync(
            provider,
            request,
            cancellationToken);

        return Ok(response);
    }
}
