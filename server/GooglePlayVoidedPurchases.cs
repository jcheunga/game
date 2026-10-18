using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CrownroadServer;

/// <summary>
/// Polls Google Play's Voided Purchases API (purchases.voidedpurchases.list)
/// with the purchase-verification service account and takes back what each
/// refunded, charged-back or revoked purchase granted. Polling needs no Pub/Sub
/// topic or public push endpoint, and the API reports voids from every source
/// (the player, the developer, Google, chargebacks). A void reaches the player
/// within one interval (GOOGLE_PLAY_VOIDED_POLL_MINUTES, default 60).
///
/// Replicas share one claim row, so one polls per interval. Each poll looks
/// back two days past the last one, because Google can list a void late; a void
/// seen twice is recorded once (PurchaseRefunds).
/// </summary>
public sealed class GooglePlayVoidedPurchases : BackgroundService
{
    /// <summary>Tests answer Google's API (and skip its sign-in) here.</summary>
    internal static Func<Uri, Task<string>>? TestFetch;

    private const string StateName = "google_voided_purchases";
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(2);
    // Google keeps 30 days of voided purchases and rejects an older start time.
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30) - TimeSpan.FromMinutes(5);
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ILogger<GooglePlayVoidedPurchases> _logger;

    public GooglePlayVoidedPurchases(ILogger<GooglePlayVoidedPurchases> logger) => _logger = logger;

    internal static TimeSpan Interval =>
        TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable("GOOGLE_PLAY_VOIDED_POLL_MINUTES"), out var minutes)
            ? Math.Clamp(minutes, 5, 24 * 60)
            : 60);

    public sealed record PollResult(bool Polled, int Voided, int Refunded, int Unknown, string Message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (StorePurchaseVerification.GooglePlayConfiguration() == null)
        {
            _logger.LogInformation("Google Play voided purchases: not configured, so Play refunds are not polled.");
            return;
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await PollOnceAsync(DateTimeOffset.UtcNow, force: false, stoppingToken);
                if (result.Polled)
                    _logger.LogInformation("Google Play voided purchases: {Voided} listed, {Refunded} refunded, {Unknown} not ours", result.Voided, result.Refunded, result.Unknown);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Google Play voided purchases: poll failed; the next poll covers the same window.");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>One poll. Without force it only runs when no replica has polled within the interval.</summary>
    internal static async Task<PollResult> PollOnceAsync(DateTimeOffset now, bool force, CancellationToken cancellationToken = default)
    {
        var configuration = StorePurchaseVerification.GooglePlayConfiguration();
        if (configuration == null && TestFetch == null)
            return new PollResult(false, 0, 0, 0, "Google Play is not configured.");
        var packageName = configuration?.PackageName ?? "test.package";

        var nowSeconds = now.ToUnixTimeSeconds();
        long cursor;
        using (var conn = Database.Open())
        {
            using (var ensure = conn.CreateCommand())
            {
                ensure.CommandText = "INSERT INTO store_sync_state (name, cursor_millis, claimed_until, updated_at) VALUES (@name, 0, 0, @now) ON CONFLICT (name) DO NOTHING";
                ensure.Parameters.AddWithValue("@name", StateName);
                ensure.Parameters.AddWithValue("@now", nowSeconds);
                ensure.ExecuteNonQuery();
            }
            using (var claim = conn.CreateCommand())
            {
                claim.CommandText = force
                    ? "UPDATE store_sync_state SET claimed_until = @until WHERE name = @name"
                    : "UPDATE store_sync_state SET claimed_until = @until WHERE name = @name AND claimed_until <= @now";
                claim.Parameters.AddWithValue("@until", nowSeconds + (long)Interval.TotalSeconds - 30);
                claim.Parameters.AddWithValue("@name", StateName);
                claim.Parameters.AddWithValue("@now", nowSeconds);
                if (claim.ExecuteNonQuery() == 0)
                    return new PollResult(false, 0, 0, 0, "Another server polled within the interval.");
            }
            using var read = conn.CreateCommand();
            read.CommandText = "SELECT cursor_millis FROM store_sync_state WHERE name = @name";
            read.Parameters.AddWithValue("@name", StateName);
            cursor = Database.ToInt64(read.ExecuteScalar());
        }

        var oldest = (now - Retention).ToUnixTimeMilliseconds();
        var start = cursor > 0 ? Math.Max(oldest, cursor - (long)Overlap.TotalMilliseconds) : oldest;
        var end = now.ToUnixTimeMilliseconds();
        var accessToken = configuration != null && TestFetch == null
            ? await StorePurchaseVerification.GoogleAccessTokenAsync(configuration.Value.ServiceAccountJson)
            : "";
        if (configuration != null && TestFetch == null && accessToken.Length == 0)
            throw new InvalidOperationException("Google refused the service account's sign-in.");

        int voided = 0, refunded = 0, unknown = 0;
        var pageToken = "";
        do
        {
            var url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/purchases/voidedpurchases" +
                $"?startTime={start}&endTime={end}&maxResults=1000&type=0" +
                (pageToken.Length > 0 ? $"&token={Uri.EscapeDataString(pageToken)}" : "");
            using var page = JsonDocument.Parse(await Fetch(new Uri(url), accessToken, cancellationToken));
            if (page.RootElement.TryGetProperty("voidedPurchases", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    voided++;
                    var outcome = Record(item);
                    if (outcome.Status == "refunded") refunded++;
                    else if (outcome.Status == "unknown_transaction") unknown++;
                }
            }
            pageToken = page.RootElement.TryGetProperty("tokenPagination", out var pagination)
                ? Text(pagination, "nextPageToken")
                : "";
        }
        while (pageToken.Length > 0);

        // Only a complete poll moves the window forward.
        using (var conn = Database.Open())
        using (var save = conn.CreateCommand())
        {
            save.CommandText = "UPDATE store_sync_state SET cursor_millis = @end, updated_at = @now WHERE name = @name AND cursor_millis < @end";
            save.Parameters.AddWithValue("@end", end);
            save.Parameters.AddWithValue("@now", nowSeconds);
            save.Parameters.AddWithValue("@name", StateName);
            save.ExecuteNonQuery();
        }
        return new PollResult(true, voided, refunded, unknown, $"{voided} voided purchases listed.");
    }

    private static PurchaseRefunds.Outcome Record(JsonElement item)
    {
        var orderId = Text(item, "orderId");
        var purchaseToken = Text(item, "purchaseToken");
        var voidedAt = Text(item, "voidedTimeMillis");
        var tokenFingerprint = purchaseToken.Length > 0 ? StorePurchaseVerification.ReceiptFingerprint(purchaseToken) : "";
        var source = item.TryGetProperty("voidedSource", out var s) && s.TryGetInt32(out var sourceCode) ? sourceCode : -1;
        var reason = item.TryGetProperty("voidedReason", out var r) && r.TryGetInt32(out var reasonCode) ? reasonCode : -1;
        return PurchaseRefunds.Record(new PurchaseRefunds.StoreSignal(
            "google",
            $"google:{(orderId.Length > 0 ? orderId : tokenFingerprint)}:{voidedAt}",
            "voided",
            Platform: "google",
            TransactionId: orderId,
            ReceiptFingerprint: tokenFingerprint,
            Detail: $"voidedSource={SourceName(source)} voidedReason={ReasonName(reason)} voidedTimeMillis={voidedAt}"),
            orderId.Length > 0 || tokenFingerprint.Length > 0 ? PurchaseRefunds.Action.Refund : PurchaseRefunds.Action.Ignore);
    }

    private static async Task<string> Fetch(Uri url, string accessToken, CancellationToken cancellationToken)
    {
        if (TestFetch != null) return await TestFetch(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google Play answered {(int)response.StatusCode} to the voided purchases list.");
        return body;
    }

    private static string SourceName(int code) => code switch { 0 => "user", 1 => "developer", 2 => "google", _ => code.ToString() };

    private static string ReasonName(int code) => code switch
    {
        0 => "other", 1 => "remorse", 2 => "not_received", 3 => "defective", 4 => "accidental_purchase",
        5 => "fraud", 6 => "friendly_fraud", 7 => "chargeback", 8 => "unacknowledged_purchase", _ => code.ToString()
    };

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : ""
            : "";

    /// <summary>Operators can poll at once (POST /admin/refunds/poll-google), for example right after setup.</summary>
    public static async Task<IResult> AdminPoll(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        try
        {
            var result = await PollOnceAsync(DateTimeOffset.UtcNow, force: true, request.HttpContext.RequestAborted);
            return Results.Ok(new { status = result.Polled ? "ok" : "skipped", result.Voided, result.Refunded, result.Unknown, result.Message });
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
        {
            return Results.Json(new { status = "error", message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
