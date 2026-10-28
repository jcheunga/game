using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CrownroadServer;

/// <summary>
/// Applies route-aware limits. Authenticated traffic is partitioned by its
/// bearer token instead of public IP, so players sharing mobile/Wi-Fi egress do
/// not throttle one another, but only once the token has passed a session check
/// on this replica: an unknown bearer counts against its IP like an anonymous
/// request, so made-up tokens cannot skip the per-IP limits. Redis makes
/// counters consistent across API replicas.
/// </summary>
public sealed class RateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int KnownSessionCap = 200_000;
    // Hashes of tokens this replica has seen pass SessionAuth, with their expiry.
    private static readonly ConcurrentDictionary<string, long> KnownSessions = new(StringComparer.Ordinal);
    private readonly RequestDelegate _next;
    private readonly IRateLimitStore _store;

    public RateLimiter(RequestDelegate next, IRateLimitStore store)
    {
        _next = next;
        _store = store;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var policy = SelectPolicy(context);
        var lease = await _store.TryConsumeAsync(policy.Key, policy.Limit, Window);

        context.Response.Headers["RateLimit-Limit"] = policy.Limit.ToString();
        context.Response.Headers["RateLimit-Remaining"] = Math.Max(0, policy.Limit - lease.Count).ToString();
        context.Response.Headers["RateLimit-Reset"] = lease.RetryAfterSeconds.ToString();
        if (!lease.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = lease.RetryAfterSeconds.ToString();
            return;
        }

        await _next(context);
    }

    /// <summary>Records a token that passed a session check, so its later requests are counted per session.</summary>
    public static void RememberSession(string token, long expiresAtUnixSeconds)
    {
        if (token.Length is 0 or > 512) return;
        if (KnownSessions.Count >= KnownSessionCap) KnownSessions.Clear();
        KnownSessions[Hash(token)] = expiresAtUnixSeconds;
    }

    private static bool IsKnownSession(string token) =>
        KnownSessions.TryGetValue(Hash(token), out var expires) && expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    internal static RateLimitPolicy SelectPolicy(HttpContext context)
    {
        var path = context.Request.Path;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bearerToken = ReadBearerToken(context.Request);

        // A profile registration/recovery attempt has no established bearer
        // yet, so it remains deliberately limited by source IP.
        if (path.StartsWithSegments("/auth"))
        {
            var poll = path.StartsWithSegments("/auth/google/poll") || path.StartsWithSegments("/auth/apple/poll");
            return new RateLimitPolicy($"crownroad:rate:v1:auth:{(poll ? "poll" : "attempt")}:{Hash(ip)}", poll ? 40 : 10);
        }

        // App Store notifications arrive from Apple's own servers, in bursts
        // after an outage, and are answered only once their signature checks.
        if (path.StartsWithSegments("/store/apple/notifications"))
        {
            return new RateLimitPolicy($"crownroad:rate:v1:store-notifications:{Hash(ip)}", 600);
        }

        // Every game polls /health, and players behind one carrier address share it, so it has its own allowance.
        if (path.StartsWithSegments("/health"))
        {
            return new RateLimitPolicy($"crownroad:rate:v1:health:{Hash(ip)}", 600);
        }

        if (path.StartsWithSegments("/player-profile"))
        {
            return new RateLimitPolicy($"crownroad:rate:v1:registration:{Hash(ip)}", 20);
        }

        if (!string.IsNullOrWhiteSpace(bearerToken) && IsKnownSession(bearerToken))
        {
            var isWebSocket = path.StartsWithSegments("/ws");
            return new RateLimitPolicy(
                $"crownroad:rate:v1:session:{Hash(bearerToken)}",
                isWebSocket ? 30 : 180);
        }

        return new RateLimitPolicy($"crownroad:rate:v1:ip:{Hash(ip)}", 60);
    }

    private static string ReadBearerToken(HttpRequest request)
    {
        var value = request.Headers.Authorization.FirstOrDefault() ?? "";
        const string prefix = "Bearer ";
        var token = value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : "";
        // Headers larger than this are malformed and should not create large
        // keys or trigger costly hashing work.
        return token.Length is > 0 and <= 512 ? token : "";
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];

    internal readonly record struct RateLimitPolicy(string Key, int Limit);
}
