using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// Switches an operator flips on the running server. They live in the database, so every API replica sees a change
/// within <see cref="CacheFor"/>, with no redeploy. One so far: pausing new purchases, for an incident on launch day.
/// A pause stops the game starting a purchase (the shop reads it from /purchase/products) and refuses new Stripe
/// checkouts. A purchase the store has already charged is still validated and delivered, so nobody pays and goes
/// without.
/// </summary>
public static class LiveSettings
{
    private const string PausedKey = "purchases.paused";
    private const string MessageKey = "purchases.message";
    private const string DefaultMessage = "Purchases are paused for a short while. Please try again later.";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);
    private static readonly object Gate = new();
    private static (bool Paused, string Message, DateTime ReadAt) _purchases = (false, "", DateTime.MinValue);

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

    /// <summary>Whether new purchases are paused, and what to tell players. Read at most every 10 s per replica.</summary>
    public static (bool Paused, string Message) Purchases
    {
        get
        {
            lock (Gate)
            {
                if (DateTime.UtcNow - _purchases.ReadAt < CacheFor) return (_purchases.Paused, _purchases.Message);
            }
            var paused = false;
            var message = "";
            using (var conn = Database.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name, value FROM live_settings WHERE name IN (@paused, @message)";
                cmd.Parameters.AddWithValue("@paused", PausedKey);
                cmd.Parameters.AddWithValue("@message", MessageKey);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.GetString(0) == PausedKey) paused = reader.GetString(1) == "1";
                    else message = reader.GetString(1);
                }
            }
            message = paused && message.Length == 0 ? DefaultMessage : paused ? message : "";
            lock (Gate) _purchases = (paused, message, DateTime.UtcNow);
            return (paused, message);
        }
    }

    public static void SetPurchases(bool paused, string message)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var conn = Database.Open();
        foreach (var (name, value) in new[] { (PausedKey, paused ? "1" : "0"), (MessageKey, message) })
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
        lock (Gate) _purchases = (paused, paused ? (message.Length > 0 ? message : DefaultMessage) : "", DateTime.UtcNow);
    }

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
}
