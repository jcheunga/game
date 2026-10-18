using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Brings store refunds to this save. The server records a refund when Google or Apple reports
/// one (server/PurchaseRefunds.cs); this fetches the player's adjustments, applies each once by
/// ID (GameState.TryApplyWalletAdjustment), and reports what the save could take back once that
/// is on disk. It runs when the server answers (at most every ten minutes, or at once after the
/// account changes) and whenever the shop opens. The requests run off the main thread; the save is
/// only touched back on it. JSON is read and written without reflection, which the iOS build lacks.
/// </summary>
public static class WalletAdjustmentSync
{
    private static readonly System.Net.Http.HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(10);
    private static DateTime _lastSyncUtc = DateTime.MinValue;
    private static string _lastProfileId = "";
    private static bool _syncing;

    public static string LastStatus { get; private set; } = "Refunds not checked yet.";

    /// <summary>Returns how many adjustments this call applied. Never throws.</summary>
    public static async Task<int> SyncAsync(bool force = false)
    {
        var state = GameState.Instance;
        var origin = AccountSignIn.Origin;
        if (_syncing || state == null || origin.Length == 0 || state.PlayerAuthToken.Length == 0)
        {
            return 0;
        }

        var profileId = state.PlayerProfileId;
        if (!force && profileId == _lastProfileId && DateTime.UtcNow - _lastSyncUtc < MinimumInterval)
        {
            return 0;
        }

        _syncing = true;
        try
        {
            var token = state.PlayerAuthToken;
            var adjustments = await Task.Run(() => FetchAsync(origin, profileId, token));
            // Back on the main thread. A sign-in that finished meanwhile owns the save now.
            if (state.PlayerProfileId != profileId)
            {
                return 0;
            }

            _lastSyncUtc = DateTime.UtcNow;
            _lastProfileId = profileId;
            var applied = 0;
            foreach (var adjustment in adjustments)
            {
                if (state.TryApplyWalletAdjustment(adjustment) != null)
                {
                    applied++;
                }
            }

            foreach (var adjustment in adjustments)
            {
                if (!adjustment.Acknowledged && state.IsWalletAdjustmentSaved(adjustment.AdjustmentId) &&
                    state.GetAppliedWalletAdjustment(adjustment.AdjustmentId) is { } record)
                {
                    await Task.Run(() => AcknowledgeAsync(origin, profileId, token, record));
                }
            }

            LastStatus = applied > 0 ? $"{applied} refund change(s) applied." : "No new refunds.";
            return applied;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or IOException)
        {
            // Offline or the server is busy: the next sync tries again, and the server keeps the adjustments.
            LastStatus = $"Refunds not checked: {e.Message}";
            Godot.GD.Print($"WalletAdjustmentSync: {LastStatus}");
            return 0;
        }
        catch (Exception e)
        {
            LastStatus = $"Refunds not checked: {e.Message}";
            Godot.GD.PrintErr($"WalletAdjustmentSync: {e}");
            return 0;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static async Task<List<WalletAdjustment>> FetchAsync(string origin, string profileId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{origin}/wallet/adjustments?profileId={Uri.EscapeDataString(profileId)}");
        Authorize(request, profileId, token);
        using var response = await Client.SendAsync(request).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        }

        using var json = JsonDocument.Parse(body);
        var adjustments = new List<WalletAdjustment>();
        if (json.RootElement.TryGetProperty("adjustments", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                adjustments.Add(new WalletAdjustment
                {
                    AdjustmentId = Text(item, "adjustmentId"),
                    PurchaseId = Text(item, "purchaseId"),
                    ProductId = Text(item, "productId"),
                    Kind = Text(item, "kind"),
                    GoldDelta = Int(item, "goldDelta"),
                    FoodDelta = Int(item, "foodDelta"),
                    ReversesAdjustmentId = Text(item, "reversesAdjustmentId"),
                    Acknowledged = item.TryGetProperty("acknowledged", out var acknowledged) && acknowledged.ValueKind == JsonValueKind.True
                });
            }
        }

        return adjustments;
    }

    private static async Task AcknowledgeAsync(string origin, string profileId, string token, AppliedWalletAdjustment record)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("profileId", profileId);
            writer.WriteString("adjustmentId", record.AdjustmentId);
            writer.WriteNumber("goldApplied", record.GoldApplied);
            writer.WriteNumber("foodApplied", record.FoodApplied);
            writer.WriteEndObject();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{origin}/wallet/adjustments/ack")
        {
            Content = new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json")
        };
        Authorize(request, profileId, token);
        using var response = await Client.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} acknowledging a refund");
        }
    }

    private static void Authorize(HttpRequestMessage request, string profileId, string token)
    {
        request.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
