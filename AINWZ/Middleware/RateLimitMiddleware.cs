using System.Collections.Concurrent;

namespace SpeakEase.Write.Middleware;

public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    private readonly int _generalPerMinute;
    private readonly int _aiChatPerMinute;

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

        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(limit, TimeSpan.FromMinutes(1)));
        if (!bucket.TryConsume())
        {
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"请求过于频繁，请稍后重试\",\"code\":429}");
            return;
        }

        await _next(context);
    }

    private sealed class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private int _tokens;
        private DateTime _lastRefill;

        public TokenBucket(int maxTokens, TimeSpan refillInterval)
        {
            _maxTokens = maxTokens;
            _refillInterval = refillInterval;
            _tokens = maxTokens;
            _lastRefill = DateTime.Now;
        }

        public bool TryConsume()
        {
            Refill();
            if (_tokens <= 0) return false;
            Interlocked.Decrement(ref _tokens);
            return true;
        }

        private void Refill()
        {
            var now = DateTime.Now;
            var elapsed = now - _lastRefill;
            if (elapsed < _refillInterval) return;

            _tokens = _maxTokens;
            _lastRefill = now;
        }
    }
}
