using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// Answers App Store CONSUMPTION_REQUEST notifications. Apple sends one when a
/// player asks Apple to refund a purchase, and for 12 hours accepts what the
/// app knows about that purchase (Send Consumption Information,
/// PUT /inApps/v2/transactions/consumption/{transactionId}), which it weighs
/// when it decides.
///
/// Apple requires the player's consent before anything is sent, so nothing is
/// sent unless APPLE_IAP_SEND_CONSUMPTION_INFO=1 (docs/DEPLOYMENT.md, "Refunds").
/// Players agree through the terms and privacy policy; deleting the account
/// withdraws it, so a deleted account's kept purchase is never described.
/// The answer holds only what the server knows. A recorded purchase was
/// delivered: the server credited it and the device applies it once. The shop
/// gives no free samples or trials. How much of a pack was spent is in the
/// player's save, not on the server, so consumptionPercentage is left out, and
/// so is refundPreference: the game has no rule for which refunds to oppose.
/// </summary>
public static class AppStoreConsumption
{
    public const string SwitchVariable = "APPLE_IAP_SEND_CONSUMPTION_INFO";

    /// <summary>Tests answer for Apple's API here.</summary>
    internal static Func<HttpRequestMessage, Task<HttpResponseMessage>>? TestSend;

    internal static readonly TimeSpan Window = TimeSpan.FromHours(12);

    // Apple waits for the notification's answer while this call runs.
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static bool Enabled => Environment.GetEnvironmentVariable(SwitchVariable)?.Trim() == "1";

    /// <summary>The ConsumptionRequest body for a purchase the server recorded.</summary>
    internal static string RequestBody() => JsonSerializer.Serialize(new
    {
        // Only sent when the switch says players agreed (see the class summary).
        customerConsented = true,
        deliveryStatus = "DELIVERED",
        sampleContentProvided = false
    });

    /// <summary>
    /// Answers a recorded CONSUMPTION_REQUEST and notes the result on its store_events
    /// row: consumption_sent, consumption_failed or consumption_skipped, or the lookup's
    /// own outcome (unknown_transaction, product_mismatch), with the reason in the detail.
    /// Apple's failures are recorded, not thrown: the notification is still answered 200,
    /// since Apple decides the refund without this answer anyway.
    /// </summary>
    /// <param name="requestedAt">The notification's signedDate, which starts Apple's 12 hours.</param>
    public static async Task<string> RespondAsync(
        PurchaseRefunds.Outcome recorded, string transactionId, string environment, DateTimeOffset requestedAt, DateTimeOffset now)
    {
        string Note(string outcome, string detail)
        {
            PurchaseRefunds.UpdateEvent(recorded.EventId, outcome, detail);
            return outcome;
        }
        string Skip(string reason) => Note("consumption_skipped", $"consumption=skipped reason={reason}");
        string Fail(string reason) => Note("consumption_failed", $"consumption=failed {reason}");

        if (recorded.Status != "inspected") return Note(recorded.Status, $"consumption=skipped reason={recorded.Status}");
        if (!Enabled) return Skip("switched_off");
        if (AccountDeletion.IsDeletedProfile(recorded.ProfileId)) return Skip("account_deleted");
        var purchaseStatus = PurchaseRefunds.PurchaseStatus(recorded.PurchaseId);
        if (purchaseStatus != "validated") return Skip($"purchase_{purchaseStatus}");
        if (now - requestedAt > Window) return Skip("window_closed");
        bool sandbox;
        switch (environment)
        {
            case "Production": sandbox = false; break;
            case "Sandbox": sandbox = true; break;
            default: return Skip("unknown_environment");
        }

        string? authorization;
        try { authorization = StorePurchaseVerification.AppleServerToken(); }
        catch (Exception e) when (e is CryptographicException or ArgumentException) { return Fail("error=invalid_key"); }
        if (authorization == null) return Skip("not_configured");

        var url = $"{StorePurchaseVerification.AppleApiBaseUrl(sandbox)}/inApps/v2/transactions/consumption/{Uri.EscapeDataString(transactionId)}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(RequestBody(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorization);
        try
        {
            using var response = TestSend != null ? await TestSend(request) : await Client.SendAsync(request);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode) return Note("consumption_sent", $"consumption=sent apple={status} deliveryStatus=DELIVERED");
            var errorCode = await ErrorCode(response);
            return Fail(errorCode.Length > 0 ? $"apple={status} errorCode={errorCode}" : $"apple={status}");
        }
        catch (HttpRequestException) { return Fail("error=unreachable"); }
        catch (TaskCanceledException) { return Fail("error=timeout"); }
    }

    /// <summary>Apple's numeric errorCode from a failed answer, or "".</summary>
    private static async Task<string> ErrorCode(HttpResponseMessage response)
    {
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("errorCode", out var code) &&
                code.ValueKind == JsonValueKind.Number
                ? code.ToString()
                : "";
        }
        catch (JsonException) { return ""; }
    }
}
