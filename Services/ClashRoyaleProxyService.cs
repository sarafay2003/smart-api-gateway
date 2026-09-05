using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;

namespace ClashRoyaleApiGateway.Services;

public class ClashRoyaleProxyService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly SimpleRateLimiter _rateLimiter;
    private readonly GatewayMetrics _metrics;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public ClashRoyaleProxyService(
        HttpClient httpClient,
        IMemoryCache cache,
        SimpleRateLimiter rateLimiter,
        GatewayMetrics metrics,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _rateLimiter = rateLimiter;
        _metrics = metrics;
        _apiKey = configuration["ThirdPartyApi:ApiKey"]
            ?? throw new InvalidOperationException("ThirdPartyApi:ApiKey is not configured.");
        _baseUrl = configuration["ThirdPartyApi:BaseUrl"]
            ?? throw new InvalidOperationException("ThirdPartyApi:BaseUrl is not configured.");
    }

    public async Task<string> GetPlayerAsync(string playerTag)
    {
        _metrics.RecordRequest();

        if (!playerTag.StartsWith("#"))
        {
            playerTag = "#" + playerTag;
        }

        var cacheKey = $"player:{playerTag}";

        if (_cache.TryGetValue(cacheKey, out string? cachedResponse))
        {
            _metrics.RecordCacheHit();
            return cachedResponse!;
        }

        _metrics.RecordCacheMiss();

        // Wait for permission before making the real outgoing call -
        // this is what actually enforces the rate limit.
        await _rateLimiter.WaitAsync();

        var encodedTag = playerTag.Replace("#", "%23");
        var url = $"{_baseUrl}/players/{encodedTag}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();

        _cache.Set(cacheKey, responseBody, CacheDuration);

        return responseBody;
    }
}