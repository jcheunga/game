using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// Switches an operator flips on the running server. They live in the database, so every API replica sees a change
/// within <see cref="CacheFor"/>, with no redeploy.
/// <list type="bullet">
/// <item>Pausing new purchases, for an incident on launch day. A pause stops the game starting a purchase (the shop
/// reads it from /purchase/products) and refuses new Stripe checkouts. A purchase the store has already charged is
/// still validated and delivered, so nobody pays and goes without.</item>
/// <item>Rewarded ads, off until an operator turns them on. The game reads it from /live-settings; while it is off a
/// release build never starts the ad SDK or asks for consent, and offers no ad. Builds without AdMob IDs have no ads
/// whatever it says.</item>
/// </list>
/// </summary>
public static class LiveSettings
{
    private const string PausedKey = "purchases.paused";
    private const string MessageKey = "purchases.message";
    private const string AdsKey = "ads.enabled";
    private const string DefaultMessage = "Purchases are paused for a short while. Please try again later.";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);
    private static readonly object Gate = new();
    private static (Dictionary<string, string> Values, DateTime ReadAt) _cache = (new(), DateTime.MinValue);

    public static void Initialize(System.Data.Common.DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS live_settings (
                name TEXT PRIMARY KEY,
                value TEXT NOT NULL DEFAULT '',
                updated_at INTEGER NOT NULL DEFAULT 0
            );
        """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Every switch as stored, read at most every 10 s per replica.</summary>
    private static Dictionary<string, string> Values()
    {
        lock (Gate)
        {
            if (DateTime.UtcNow - _cache.ReadAt < CacheFor) return _cache.Values;
        }
        var values = new Dictionary<string, string>();
        using (var conn = Database.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name, value FROM live_settings";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) values[reader.GetString(0)] = reader.GetString(1);
        }
        lock (Gate) _cache = (values, DateTime.UtcNow);
        return values;
    }

    private static void Write(params (string Name, string Value)[] settings)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var conn = Database.Open();
        foreach (var (name, value) in settings)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO live_settings (name, value, updated_at) VALUES (@name, @value, @now)
                ON CONFLICT (name) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
            """;
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();
        }
        // This replica sees its own change at once; the others within CacheFor.
        lock (Gate) _cache = (_cache.Values, DateTime.MinValue);
    }

    /// <summary>Whether new purchases are paused, and what to tell players.</summary>
    public static (bool Paused, string Message) Purchases
    {
        get
        {
            var values = Values();
            var paused = values.GetValueOrDefault(PausedKey) == "1";
            var message = values.GetValueOrDefault(MessageKey) ?? "";
            return (paused, paused ? (message.Length > 0 ? message : DefaultMessage) : "");
        }
    }

    public static void SetPurchases(bool paused, string message) =>
        Write((PausedKey, paused ? "1" : "0"), (MessageKey, message));

    /// <summary>Whether rewarded ads are switched on. Off until an operator turns them on.</summary>
    public static bool AdsEnabled => Values().GetValueOrDefault(AdsKey) == "1";

    public static void SetAds(bool enabled) => Write((AdsKey, enabled ? "1" : "0"));

    /// <summary>GET /live-settings: the switches the game reads, for any player.</summary>
    public static IResult PublicGet() => Results.Ok(new { status = "ok", adsEnabled = AdsEnabled });

    /// <summary>GET /admin/purchases: whether new purchases are paused.</summary>
    public static IResult AdminPurchasesGet(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        var (paused, message) = Purchases;
        return Results.Ok(new { status = "ok", paused, message });
    }

    /// <summary>POST /admin/purchases with {"paused": true, "message": "…"} pauses new purchases; {"paused": false} resumes.</summary>
    public static async Task<IResult> AdminPurchasesSet(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        bool paused;
        string message;
        try
        {
            using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            if (!body.RootElement.TryGetProperty("paused", out var pausedElement) || pausedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return Results.BadRequest(new { error = "paused_must_be_true_or_false" });
            paused = pausedElement.GetBoolean();
            message = body.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()!.Trim() : "";
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }
        if (message.Length > 200) return Results.BadRequest(new { error = "message_too_long" });
        SetPurchases(paused, message);
        var state = Purchases;
        return Results.Ok(new { status = "ok", paused = state.Paused, message = state.Message });
    }

    /// <summary>GET /admin/ads: whether rewarded ads are switched on.</summary>
    public static IResult AdminAdsGet(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        return Results.Ok(new { status = "ok", enabled = AdsEnabled });
    }

    /// <summary>POST /admin/ads with {"enabled": true} switches rewarded ads on; {"enabled": false} off.</summary>
    public static async Task<IResult> AdminAdsSet(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        bool enabled;
        try
        {
            using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            if (!body.RootElement.TryGetProperty("enabled", out var element) || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return Results.BadRequest(new { error = "enabled_must_be_true_or_false" });
            enabled = element.GetBoolean();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }
        SetAds(enabled);
        return Results.Ok(new { status = "ok", enabled = AdsEnabled });
    }
}
