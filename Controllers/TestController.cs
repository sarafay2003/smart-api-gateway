using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ClashRoyaleApiGateway.Services;

namespace ClashRoyaleApiGateway.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ClashRoyaleProxyService _proxyService;

    // A handful of real player tags from your clan, used purely to
    // demonstrate rate limiting - different tags avoid the cache, so
    // each one genuinely has to wait for a rate limiter slot.
    private static readonly string[] TestTags = new[]
    {
        "#82JJU9PYG", "#2VR29G9C", "#Q8R8RJQ0L", "#CCLLYY28C",
        "#2QCGQP0GG", "#U99P9CJJQ", "#QVUC2RJU", "#2QVVLG29P"
    };

    public TestController(ClashRoyaleProxyService proxyService)
    {
        _proxyService = proxyService;
    }

    [HttpGet("ratelimit")]
    public async Task<IActionResult> TestRateLimit()
    {
        var stopwatch = Stopwatch.StartNew();

        // Launch all calls at once (not one-by-one) so we can see the
        // rate limiter actually holding some of them back.
        var tasks = TestTags.Select(async tag =>
        {
            var callStart = stopwatch.ElapsedMilliseconds;
            await _proxyService.GetPlayerAsync(tag);
            var callEnd = stopwatch.ElapsedMilliseconds;

            return new
            {
                tag,
                startedAtMs = callStart,
                finishedAtMs = callEnd,
                tookMs = callEnd - callStart
            };
        });

        var results = await Task.WhenAll(tasks);

        return Ok(results.OrderBy(r => r.finishedAtMs));
    }
}