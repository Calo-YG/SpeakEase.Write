using System.Collections.Concurrent;

namespace SpeakEase.Write.Middleware;

public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private static readonly object CleanupLock = new();
    private static DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;

    private readonly int _generalPerMinute;
    private readonly int _aiChatPerMinute;
    private readonly TimeSpan _bucketIdleTtl = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _generalPerMinute = 300;
        _aiChatPerMinute = 10;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var isAiChat = path.Contains("/ai/agent", StringComparison.OrdinalIgnoreCase);
        var limit = isAiChat ? _aiChatPerMinute : _generalPerMinute;
        var key = $"{context.Connection.RemoteIpAddress}:{isAiChat}";
        var now = DateTimeOffset.UtcNow;

        CleanupExpiredBuckets(now);

        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(limit, TimeSpan.FromMinutes(1)));
        if (!bucket.TryConsume(now))
        {
            _logger.LogWarning("请求被限流: {RemoteIp} {Path}", context.Connection.RemoteIpAddress, context.Request.Path);
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"请求过于频繁，请稍后重试\",\"code\":429}");
            return;
        }

        await _next(context);
    }

    private void CleanupExpiredBuckets(DateTimeOffset now)
    {
        if (now - _lastCleanup < _cleanupInterval)
            return;

        lock (CleanupLock)
        {
            if (now - _lastCleanup < _cleanupInterval)
                return;

            foreach (var (key, bucket) in _buckets)
            {
                if (bucket.IsExpired(now, _bucketIdleTtl))
                    _buckets.TryRemove(key, out _);
            }

            _lastCleanup = now;
        }
    }

    private sealed class TokenBucket
    {
        private readonly object _syncRoot = new();
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private int _tokens;
        private DateTimeOffset _lastRefill;
        private DateTimeOffset _lastSeen;

        public TokenBucket(int maxTokens, TimeSpan refillInterval)
        {
            _maxTokens = maxTokens;
            _refillInterval = refillInterval;
            _tokens = maxTokens;
            _lastRefill = DateTimeOffset.UtcNow;
            _lastSeen = _lastRefill;
        }

        public bool TryConsume(DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                Refill(now);
                _lastSeen = now;

                if (_tokens <= 0) return false;

                _tokens--;
                return true;
            }
        }

        public bool IsExpired(DateTimeOffset now, TimeSpan idleTtl)
        {
            lock (_syncRoot)
            {
                return now - _lastSeen > idleTtl;
            }
        }

        private void Refill(DateTimeOffset now)
        {
            var elapsed = now - _lastRefill;
            if (elapsed < _refillInterval) return;

            _tokens = _maxTokens;
            _lastRefill = now;
        }
    }
}
