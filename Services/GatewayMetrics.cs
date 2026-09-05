namespace ClashRoyaleApiGateway.Services
{
    public class GatewayMetrics
    {
        private int _totalRequests;
        private int _cacheHits;
        private int _cacheMisses;
        private int _rateLimitedRequests;

        public void RecordRequest() => Interlocked.Increment(ref _totalRequests);
        public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
        public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);
        public void RecordRateLimited() => Interlocked.Increment(ref _rateLimitedRequests);

        public object GetSnapshot()
        {
            var total = _totalRequests;
            var hits = _cacheHits;
            var hitRate = total > 0 ? Math.Round((double)hits / total * 100, 1) : 0;

            return new
            {
                TotalRequests = total,
                CacheHits = hits,
                CacheMisses = _cacheMisses,
                CacheHitRatePercent = hitRate,
                RateLimitedRequests = _rateLimitedRequests,
            };
        }
    }
}
