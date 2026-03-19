using System.Collections.Concurrent;

namespace CrownroadServer;

public class RateLimiter
{
    private readonly RequestDelegate _next;
    private const int MaxRequests = 60;
    private const int WindowSeconds = 60;
    private static readonly ConcurrentDictionary<string, ClientEntry> _clients = new();
    private static readonly Timer _cleanupTimer = new(_ => Cleanup(), null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

    public RateLimiter(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var entry = _clients.GetOrAdd(ip, _ => new ClientEntry());

        lock (entry)
        {
            // Slide the window: remove timestamps older than the window
            while (entry.Timestamps.Count > 0 && entry.Timestamps.Peek() <= now - WindowSeconds)
            {
                entry.Timestamps.Dequeue();
            }

            if (entry.Timestamps.Count >= MaxRequests)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = WindowSeconds.ToString();
                return;
            }

            entry.Timestamps.Enqueue(now);
        }

        await _next(context);
    }

    private static void Cleanup()
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - WindowSeconds;

        foreach (var kvp in _clients)
        {
            lock (kvp.Value)
            {
                while (kvp.Value.Timestamps.Count > 0 && kvp.Value.Timestamps.Peek() <= cutoff)
                {
                    kvp.Value.Timestamps.Dequeue();
                }

                if (kvp.Value.Timestamps.Count == 0)
                {
                    _clients.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private class ClientEntry
    {
        public Queue<long> Timestamps { get; } = new();
    }
}
