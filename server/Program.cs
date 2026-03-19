using CrownroadServer;
using CrownroadServer.Tests;

if (args.Length > 0 && args[0] == "--test")
{
    return await ServerTests.RunAll();
}

if (args.Length > 0 && args[0] == "--test-data")
{
    var dataDir = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "..", "data");
    return DataIntegrityValidator.RunAll(dataDir);
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration["AllowedOrigins"] ?? "*";
        if (allowedOrigins == "*")
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
builder.Services.AddHostedService<CrownroadServer.StaleDataCleanup>();
var app = builder.Build();

var dbPath = app.Configuration["DbPath"] ?? "crownroad.db";
Database.Configure($"Data Source={dbPath}");
Database.Initialize();

app.UseCors();
app.UseWebSockets();
app.UseMiddleware<CrownroadServer.RateLimiter>();
app.UseMiddleware<CrownroadServer.RequestLogger>();
app.Map("/ws/relay/{roomId}", async (HttpContext context, string roomId) =>
{
    await RelayHub.HandleConnection(context, roomId);
});

app.MapPost("/player-profile", Endpoints.PlayerProfile);
app.MapPost("/challenge-sync", Endpoints.ChallengeSync);
app.MapGet("/challenge-boards", Endpoints.ChallengeLeaderboard);
app.MapGet("/challenge-feed", Endpoints.ChallengeFeed);
app.MapGet("/challenge-rooms", Endpoints.RoomDirectory);
app.MapPost("/challenge-room-create", Endpoints.RoomCreate);
app.MapPost("/challenge-room-join", Endpoints.RoomJoin);
app.MapGet("/challenge-room-session", Endpoints.RoomSession);
app.MapPost("/challenge-room-action", Endpoints.RoomAction);
app.MapPost("/challenge-room-result", Endpoints.RoomResult);
app.MapGet("/challenge-room-scoreboard", Endpoints.RoomScoreboard);
app.MapPost("/challenge-room-telemetry", Endpoints.RoomTelemetry);
app.MapPost("/challenge-room-leave", Endpoints.RoomLeave);
app.MapPost("/challenge-room-report", Endpoints.RoomReport);
app.MapPost("/challenge-room-matchmake", Endpoints.RoomMatchmake);
app.MapPost("/challenge-room-seat-lease", Endpoints.RoomSeatLease);

app.MapPost("/achievements/sync", Endpoints.AchievementSync);
app.MapGet("/achievements/{profileId}", (HttpRequest request, string profileId) => Endpoints.AchievementList(request, profileId));
app.MapPost("/daily/complete", Endpoints.DailyComplete);
app.MapGet("/daily/leaderboard/{date}", (HttpRequest request, string date) => Endpoints.DailyLeaderboard(request, date));

app.MapPost("/purchase/validate", Endpoints.PurchaseValidate);
app.MapGet("/purchase/history", Endpoints.PurchaseHistory);
app.MapGet("/purchase/products", Endpoints.PurchaseProducts);
app.MapPost("/analytics/ingest", Endpoints.AnalyticsIngest);
app.MapPost("/crash-report", Endpoints.CrashReport);
app.MapGet("/analytics/summary", Endpoints.AnalyticsSummary);

app.MapPost("/cloud-save/upload", Endpoints.CloudSaveUpload);
app.MapGet("/cloud-save/download", Endpoints.CloudSaveDownload);
app.MapGet("/cloud-save/info", Endpoints.CloudSaveInfo);

app.MapPost("/purchase/stripe-checkout", Endpoints.StripeCreateCheckout);
app.MapPost("/purchase/stripe-webhook", Endpoints.StripeWebhook);
app.MapGet("/purchase/stripe-status", Endpoints.StripeCheckoutStatus);

app.MapGet("/health", () =>
{
    try
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();

        return Results.Ok(new { status = "healthy", relayRooms = CrownroadServer.RelayHub.GetRoomCount() });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

app.MapGet("/stats", () =>
{
    try
    {
        using var conn = Database.Open();

        long Count(string table) { using var c = conn.CreateCommand(); c.CommandText = $"SELECT COUNT(*) FROM {table}"; return (long)(c.ExecuteScalar() ?? 0); }
        long CountWhere(string table, string where) { using var c = conn.CreateCommand(); c.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where}"; return (long)(c.ExecuteScalar() ?? 0); }

        return Results.Ok(new
        {
            status = "ok",
            players = Count("players"),
            challengeResults = Count("challenge_results"),
            challengeBoards = Count("challenge_feed"),
            rooms = Count("rooms"),
            activeRooms = CountWhere("rooms", "status IN ('lobby','racing')"),
            expiredRooms = CountWhere("rooms", "status = 'expired'"),
            roomSeats = Count("room_seats"),
            roomTelemetry = Count("room_telemetry"),
            roomReports = Count("room_reports"),
            achievements = Count("achievements"),
            dailyCompletions = Count("daily_completions"),
            purchases = Count("purchases"),
            cloudSaves = Count("cloud_saves"),
            analyticsEvents = Count("analytics_events"),
            crashReports = Count("crash_reports"),
            relayRooms = CrownroadServer.RelayHub.GetRoomCount(),
            stripeConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")),
            uptimeSeconds = (long)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = ex.Message }, statusCode: 503);
    }
});

app.MapGet("/admin/balance", () =>
{
    var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "data");
    if (!Directory.Exists(dataDir))
        dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
    try
    {
        var html = BalanceDashboard.Render(dataDir);
        return Results.Content(html, "text/html");
    }
    catch (Exception ex)
    {
        return Results.Content($"<h1>Error</h1><pre>{ex.Message}</pre>", "text/html", statusCode: 500);
    }
});

app.MapPost("/admin/backup", () =>
{
    try
    {
        var path = Database.Backup();
        Database.CleanupOldBackups();
        return Results.Ok(new { status = "ok", backupPath = path });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/admin", () =>
{
    try
    {
        using var conn = Database.Open();
        long Count(string table) { using var c = conn.CreateCommand(); c.CommandText = $"SELECT COUNT(*) FROM {table}"; return (long)(c.ExecuteScalar() ?? 0); }
        long CountWhere(string table, string where) { using var c = conn.CreateCommand(); c.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where}"; return (long)(c.ExecuteScalar() ?? 0); }

        var players = Count("players");
        var challengeResults = Count("challenge_results");
        var rooms = Count("rooms");
        var activeRooms = CountWhere("rooms", "status IN ('lobby','racing')");
        var purchases = Count("purchases");
        var cloudSaves = Count("cloud_saves");
        var analytics = Count("analytics_events");
        var achievements = Count("achievements");
        var dailies = Count("daily_completions");
        var reports = Count("room_reports");
        var crashes = Count("crash_reports");
        var relay = CrownroadServer.RelayHub.GetRoomCount();
        var stripe = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"));
        var uptime = (long)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        var uptimeStr = $"{uptime / 3600}h {(uptime % 3600) / 60}m";

        // Recent purchases
        var recentPurchases = new System.Text.StringBuilder();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT purchase_id, profile_id, product_id, gold_credited, food_credited, purchased_at FROM purchases ORDER BY purchased_at DESC LIMIT 10";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var when = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)).ToString("MM-dd HH:mm");
                recentPurchases.Append($"<tr><td>{reader.GetString(0)[..10]}</td><td>{reader.GetString(1)[..Math.Min(12, reader.GetString(1).Length)]}</td><td>{reader.GetString(2)}</td><td>{reader.GetInt32(3)}g+{reader.GetInt32(4)}f</td><td>{when}</td></tr>");
            }
        }

        // Recent analytics
        var recentAnalytics = new System.Text.StringBuilder();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT event_type, COUNT(*) FROM analytics_events WHERE recorded_at > $cutoff GROUP BY event_type ORDER BY COUNT(*) DESC LIMIT 10";
            cmd.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                recentAnalytics.Append($"<tr><td>{reader.GetString(0)}</td><td>{reader.GetInt64(1)}</td></tr>");
            }
        }

        var purchaseRows = recentPurchases.Length > 0 ? recentPurchases.ToString() : "<tr><td colspan='5'>No purchases yet.</td></tr>";
        var analyticsRows = recentAnalytics.Length > 0 ? recentAnalytics.ToString() : "<tr><td colspan='2'>No events yet.</td></tr>";
        var stripeLabel = stripe ? "configured" : "not set";
        var stripeBadge = stripe ? "badge-on" : "badge-off";

        var html = "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Crownroad Admin</title>" +
            "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
            "<style>" +
            "body{font-family:-apple-system,sans-serif;background:#0d1117;color:#c9d1d9;max-width:900px;margin:0 auto;padding:20px}" +
            "h1{color:#e2b714}h2{color:#8b949e;border-bottom:1px solid #30363d;padding-bottom:8px}" +
            ".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px;margin:16px 0}" +
            ".card{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:16px;text-align:center}" +
            ".card .value{font-size:28px;font-weight:bold;color:#e2b714}" +
            ".card .label{font-size:12px;color:#8b949e;margin-top:4px}" +
            "table{width:100%;border-collapse:collapse;margin:12px 0}" +
            "th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #21262d;font-size:13px}" +
            "th{color:#8b949e;font-weight:600}" +
            ".badge{display:inline-block;padding:2px 8px;border-radius:4px;font-size:11px}" +
            ".badge-on{background:#238636;color:#fff}.badge-off{background:#6e7681;color:#fff}" +
            "</style></head><body>" +
            "<h1>Crownroad Server</h1>" +
            $"<p>Uptime: {uptimeStr} &nbsp; Stripe: <span class='badge {stripeBadge}'>{stripeLabel}</span></p>" +
            "<h2>Overview</h2><div class='grid'>" +
            $"<div class='card'><div class='value'>{players}</div><div class='label'>Players</div></div>" +
            $"<div class='card'><div class='value'>{rooms}</div><div class='label'>Rooms ({activeRooms} active)</div></div>" +
            $"<div class='card'><div class='value'>{relay}</div><div class='label'>Relay Rooms</div></div>" +
            $"<div class='card'><div class='value'>{challengeResults}</div><div class='label'>Challenge Results</div></div>" +
            $"<div class='card'><div class='value'>{purchases}</div><div class='label'>Purchases</div></div>" +
            $"<div class='card'><div class='value'>{cloudSaves}</div><div class='label'>Cloud Saves</div></div>" +
            $"<div class='card'><div class='value'>{achievements}</div><div class='label'>Achievements</div></div>" +
            $"<div class='card'><div class='value'>{dailies}</div><div class='label'>Daily Completions</div></div>" +
            $"<div class='card'><div class='value'>{analytics}</div><div class='label'>Analytics Events</div></div>" +
            $"<div class='card'><div class='value'>{reports}</div><div class='label'>Reports</div></div>" +
            $"<div class='card'><div class='value'>{crashes}</div><div class='label'>Crashes</div></div>" +
            "</div>" +
            "<h2>Recent Purchases</h2>" +
            $"<table><tr><th>ID</th><th>Profile</th><th>Product</th><th>Credited</th><th>When</th></tr>{purchaseRows}</table>" +
            "<h2>Analytics (Last 24h)</h2>" +
            $"<table><tr><th>Event Type</th><th>Count</th></tr>{analyticsRows}</table>" +
            "<p style='color:#484f58;font-size:12px;margin-top:32px'>API: <a href='/health' style='color:#58a6ff'>/health</a> &middot; <a href='/stats' style='color:#58a6ff'>/stats</a> &middot; <a href='/analytics/summary' style='color:#58a6ff'>/analytics/summary</a> &middot; <a href='/admin/balance' style='color:#58a6ff'>/admin/balance</a></p>" +
            "</body></html>";

        return Results.Content(html, "text/html");
    }
    catch (Exception ex)
    {
        return Results.Content($"<h1>Error</h1><pre>{ex.Message}</pre>", "text/html", statusCode: 503);
    }
});

app.Run();
return 0;
