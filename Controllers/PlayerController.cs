using Microsoft.AspNetCore.Mvc;
using ClashRoyaleApiGateway.Services;

namespace ClashRoyaleApiGateway.Controllers;

[ApiController]
[Route("[controller]")]
public class PlayerController : ControllerBase
{
    private readonly ClashRoyaleProxyService _proxyService;

    public PlayerController(ClashRoyaleProxyService proxyService)
    {
        _proxyService = proxyService;
    }

    [HttpGet("{tag}")]
    public async Task<IActionResult> GetPlayer(string tag)
    {
        var playerJson = await _proxyService.GetPlayerAsync(tag);
        return Content(playerJson, "application/json");
    }
}