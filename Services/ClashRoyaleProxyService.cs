using System.Net.Http.Headers;
using StackExchange.Redis;

namespace ClashRoyaleApiGateway.Services;

public class ClashRoyaleProxyService
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly SimpleRateLimiter _rateLimiter;
    private readonly GatewayMetrics _metrics;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public ClashRoyaleProxyService(
        HttpClient httpClient,
        IConnectionMultiplexer redis,
        SimpleRateLimiter rateLimiter,
        GatewayMetrics metrics,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _redis = redis;
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
        var db = _redis.GetDatabase();

        var cachedResponse = await db.StringGetAsync(cacheKey);
        if (cachedResponse.HasValue)
        {
            _metrics.RecordCacheHit();
            return cachedResponse!;
        }

        _metrics.RecordCacheMiss();

        await _rateLimiter.WaitAsync();

        var encodedTag = playerTag.Replace("#", "%23");
        var url = $"{_baseUrl}/players/{encodedTag}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();

        await db.StringSetAsync(cacheKey, responseBody, CacheDuration);

        return responseBody;
    }
}