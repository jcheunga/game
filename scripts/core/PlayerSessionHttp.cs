using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Godot;

/// <summary>
/// Applies the anonymous-player bearer session to backend requests. Profile IDs
/// remain useful routing identifiers, but they are never authorization by
/// themselves.
/// </summary>
public static class PlayerSessionHttp
{
    private const int StatusGone = 410;
    private static int _forgetting;

    public static void Apply(HttpRequestMessage message, string profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            message.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId.Trim());
        }

        var token = GameState.Instance?.PlayerAuthToken?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        }
    }

    /// <summary>
    /// Whether the server refused a request because its session belonged to an account deleted since,
    /// on another device or by support (HTTP 410, "account_deleted"). The device then forgets that
    /// account too and starts again at home as a new guest, once however many requests are refused.
    /// </summary>
    public static bool NoticeAccountDeleted(int statusCode, string responseBody, string profileId)
    {
        if (statusCode != StatusGone || !IsAccountDeleted(responseBody)) return false;
        // The caller may be in the middle of changing the save, so the reset waits for the next idle moment.
        if (Interlocked.Exchange(ref _forgetting, 1) == 0) Callable.From(() => ForgetWhenSafe(profileId)).CallDeferred();
        return true;
    }

    private static bool IsAccountDeleted(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String &&
                error.GetString() == "account_deleted";
        }
        catch (JsonException) { return false; }
    }

    private static void ForgetWhenSafe(string profileId)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        // A battle in progress plays out, and a scene change finishes, before the device resets.
        if (tree.CurrentScene is BattleController || SceneRouter.Instance?.IsTransitioning == true)
        {
            tree.CreateTimer(0.5).Timeout += () => ForgetWhenSafe(profileId);
            return;
        }

        Interlocked.Exchange(ref _forgetting, 0);
        var state = GameState.Instance;
        // A deletion, sign-out or sign-in on this device may have moved it to another profile meanwhile.
        if (state == null || state.PlayerProfileId != profileId) return;
        state.ForgetDeletedAccount();
        PlayerProfileSyncService.InvalidateFromState("Account deleted.");
        SceneRouter.Instance?.ReloadHome();
    }
}
