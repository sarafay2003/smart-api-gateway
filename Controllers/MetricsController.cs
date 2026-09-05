using Microsoft.AspNetCore.Mvc;
using ClashRoyaleApiGateway.Services;

namespace ClashRoyaleApiGateway.Controllers;

[ApiController]
[Route("[controller]")]
public class MetricsController : ControllerBase
{
    private readonly GatewayMetrics _metrics;

    public MetricsController(GatewayMetrics metrics)
    {
        _metrics = metrics;
    }

    [HttpGet]
    public IActionResult GetMetrics()
    {
        return Ok(_metrics.GetSnapshot());
    }
}