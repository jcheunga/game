using System.Collections.Concurrent;
using StackExchange.Redis;

namespace CrownroadServer;

/// <summary>
/// A small abstraction so production API replicas share counters through Redis,
/// while local tests remain self-contained.
/// </summary>
public interface IRateLimitStore
{
    ValueTask<RateLimitLease> TryConsumeAsync(string key, int limit, TimeSpan window);
}

public readonly record struct RateLimitLease(bool Allowed, int Count, int RetryAfterSeconds);

public sealed class InMemoryRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public ValueTask<RateLimitLease> TryConsumeAsync(string key, int limit, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = _entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            while (entry.Timestamps.Count > 0 && entry.Timestamps.Peek() <= now - window)
            {
                entry.Timestamps.Dequeue();
            }

            var retryAfter = entry.Timestamps.Count == 0
                ? 1
                : Math.Max(1, (int)Math.Ceiling((entry.Timestamps.Peek() + window - now).TotalSeconds));
            if (entry.Timestamps.Count >= limit)
            {
                // Do not let rejected requests extend the fallback limiter's
                // window indefinitely during an outage. Once the oldest
                // accepted request ages out, a legitimate player can recover.
                return ValueTask.FromResult(new RateLimitLease(false, entry.Timestamps.Count, retryAfter));
            }

            entry.Timestamps.Enqueue(now);
            return ValueTask.FromResult(new RateLimitLease(true, entry.Timestamps.Count, retryAfter));
        }
    }

    private sealed class Entry
    {
        public Queue<DateTimeOffset> Timestamps { get; } = new();
    }
}

public sealed class RedisRateLimitStore : IRateLimitStore
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly InMemoryRateLimitStore _fallback = new();
    private readonly ILogger<RedisRateLimitStore> _logger;
    private long _lastUnavailableLogAtUnixSeconds;

    public RedisRateLimitStore(IConnectionMultiplexer redis, ILogger<RedisRateLimitStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async ValueTask<RateLimitLease> TryConsumeAsync(string key, int limit, TimeSpan window)
    {
        try
        {
            var database = _redis.GetDatabase();
            var windowSeconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds));
            var result = await database.ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [windowSeconds]);
            var count = (int)(long)result;
            var ttl = await database.KeyTimeToLiveAsync(key);
            var retryAfter = Math.Max(1, (int)Math.Ceiling((ttl ?? window).TotalSeconds));
            return new RateLimitLease(count <= limit, count, retryAfter);
        }
        catch (RedisException ex)
        {
            // Keep the game reachable through a transient Redis outage. The
            // local limiter is intentionally conservative. Log at most once a
            // minute so a Redis incident does not create a second log-volume
            // incident on every incoming request.
            LogRedisUnavailableOncePerMinute(ex);
            return await _fallback.TryConsumeAsync(key, limit, window);
        }
    }

    private void LogRedisUnavailableOncePerMinute(RedisException exception)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var previous = Volatile.Read(ref _lastUnavailableLogAtUnixSeconds);
        if (now - previous < 60 || Interlocked.CompareExchange(ref _lastUnavailableLogAtUnixSeconds, now, previous) != previous)
        {
            return;
        }

        _logger.LogWarning(exception, "Redis rate-limit store unavailable; using the local fallback temporarily.");
    }
}
