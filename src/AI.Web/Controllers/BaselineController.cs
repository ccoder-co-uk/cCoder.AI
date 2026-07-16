using AI.Web.Exposures.Setup;
using Microsoft.AspNetCore.Mvc;

namespace AI.Web.Controllers;

[ApiController]
[Route("Api/AI/Baseline")]
public sealed class BaselineController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() =>
        Ok(AIBaselinePackages.All);
}
