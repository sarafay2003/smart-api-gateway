namespace ClashRoyaleApiGateway.Services;

/// <summary>
/// A simple rate limiter that only allows a fixed number of calls per
/// second. Anything calling WaitAsync() will pause if the limit has
/// already been reached, and resume once a slot frees up. This protects
/// the real upstream API from being hit too fast, regardless of how many
/// requests our own gateway receives.
/// </summary>
public class SimpleRateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxCallsPerSecond;
    private readonly object _lock = new();
    private DateTime _windowStart = DateTime.UtcNow;
    private int _callsInWindow = 0;

    public SimpleRateLimiter(int maxCallsPerSecond)
    {
        _maxCallsPerSecond = maxCallsPerSecond;
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task WaitAsync()
    {
        while (true)
        {
            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                if ((now - _windowStart).TotalSeconds >= 1)
                {
                    // A new one-second window has started - reset the count.
                    _windowStart = now;
                    _callsInWindow = 0;
                }

                if (_callsInWindow < _maxCallsPerSecond)
                {
                    _callsInWindow++;
                    return; // allowed to proceed
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Limit reached for this window - wait a bit before checking again.
            await Task.Delay(100);
        }
    }
}