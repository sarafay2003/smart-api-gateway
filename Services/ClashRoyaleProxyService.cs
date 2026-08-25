using System.Net.Http.Headers;

namespace ClashRoyaleApiGateway.Services;

/// <summary>
/// Handles making the actual outgoing HTTP calls to the real Clash Royale API.
/// This is the "upstream" call - everything else in the gateway (caching,
/// rate limiting) will wrap around this.
/// </summary>
public class ClashRoyaleProxyService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public ClashRoyaleProxyService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["ThirdPartyApi:ApiKey"]
            ?? throw new InvalidOperationException("ThirdPartyApi:ApiKey is not configured.");
        _baseUrl = configuration["ThirdPartyApi:BaseUrl"]
            ?? throw new InvalidOperationException("ThirdPartyApi:BaseUrl is not configured.");
    }

    /// <summary>
    /// Fetches a player's profile from the real Clash Royale API.
    /// Tags starting with '#' need to be URL-encoded as %23.
    /// </summary>
    public async Task<string> GetPlayerAsync(string playerTag)
    {
        if (!playerTag.StartsWith("#"))
        {
            playerTag = "#" + playerTag;
        }

        var encodedTag = playerTag.Replace("#", "%23");
        var url = $"{_baseUrl}/players/{encodedTag}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}