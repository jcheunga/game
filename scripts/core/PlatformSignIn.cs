using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The phone's own game account: Google Play Games on Android (the PlayGamesSignIn plugin built
/// from native/PlayGamesSignIn) and Game Center on iOS (Godot's official GameCenter plugin, built
/// from native/GameCenter into ios/plugins). Each hands the server a proof of the player that it
/// verifies itself.
/// </summary>
public static class PlatformSignIn
{
    public const string PlayGames = "playgames", GameCenter = "gamecenter";
    private const string PlayGamesSingleton = "PlayGamesSignIn", GameCenterSingleton = "GameCenter";
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private static bool _autoTried;

    /// <summary>The game account this build can use, or "" when it has neither plugin.</summary>
    public static string Provider =>
        OS.GetName() == "Android" && Engine.HasSingleton(PlayGamesSingleton) && Engine.GetSingleton(PlayGamesSingleton).Call("isConfigured").AsBool() ? PlayGames
        : OS.GetName() == "iOS" && Engine.HasSingleton(GameCenterSingleton) ? GameCenter : "";

    public static string ProviderName(string provider) => provider switch
    {
        PlayGames => "Google Play Games", GameCenter => "Game Center", "apple" => "Apple", "google" => "Google", "email" => "Email", _ => provider
    };

    /// <summary>Whether the server can verify this device's game account.</summary>
    public static bool Offered(JsonElement providers) => Provider switch
    {
        PlayGames => Flag(providers, "playGames") && AccountSignIn.Text(providers, "playGamesClientId").Length > 0,
        GameCenter => Flag(providers, "gameCenter"),
        _ => false
    };

    /// <summary>Proves the player to the server; interactive sign-in may show the platform's own sheet.</summary>
    public static async Task<JsonElement> SignIn(JsonElement providers, bool interactive) => Provider == PlayGames
        ? await AccountSignIn.Request("/auth/playgames", new AccountSignIn.PlayGamesRequest
            { AuthCode = await PlayGamesCode(AccountSignIn.Text(providers, "playGamesClientId"), interactive) }, ServerJson.Default.PlayGamesRequest)
        : await AccountSignIn.Request("/auth/gamecenter", await GameCenterProof(interactive), ServerJson.Default.GameCenterRequest);

    /// <summary>
    /// Play Games signs players in by itself when the game starts. A guest is linked to it then,
    /// but only when that cannot cost progress: the account is this guest, is brand new and takes
    /// this progress along, or replaces a campaign that has not started. Anything else waits for
    /// the player to choose in Account.
    /// </summary>
    public static async Task TryAutoSignIn()
    {
        var state = GameState.Instance;
        if (_autoTried || state == null || Provider != PlayGames || state.AccountProvider.Length > 0 || state.SkipAutoSignIn || state.PlayerAuthToken.Length == 0) return;
        _autoTried = true;
        try
        {
            var providers = await AccountSignIn.Request("/auth/providers");
            if (!Offered(providers)) return;
            var profile = state.PlayerProfileId;
            var session = await SignIn(providers, interactive: false);
            if (state.PlayerProfileId != profile || state.AccountProvider.Length > 0) return;
            var replaces = AccountSignIn.Text(session, "playerProfileId") != profile;
            var isNew = session.TryGetProperty("isNewAccount", out var created) && created.ValueKind == JsonValueKind.True;
            var tree = (SceneTree)Engine.GetMainLoop();
            if (replaces && (!isNew && state.HighestUnlockedStage > 1 || tree.CurrentScene is not (MainMenu or MapMenu))) return;
            await AccountSignIn.Apply(session);
            if (replaces) SceneRouter.Instance?.ReloadHome();
        }
        catch (Exception e) { GD.Print($"Automatic sign-in skipped: {e.Message}"); }
    }

    private static bool Flag(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static async Task<string> PlayGamesCode(string serverClientId, bool interactive)
    {
        var plugin = Engine.GetSingleton(PlayGamesSingleton);
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var granted = Callable.From<string>(code => result.TrySetResult(code));
        var failed = Callable.From<string>(reason => result.TrySetException(new InvalidOperationException(reason)));
        plugin.Connect("server_access_granted", granted); plugin.Connect("sign_in_failed", failed);
        try
        {
            plugin.Call("requestServerAccess", serverClientId, interactive);
            if (await Task.WhenAny(result.Task, Task.Delay(Patience)) != result.Task)
                throw new InvalidOperationException("Google Play Games did not answer. Try again.");
            return await result.Task;
        }
        finally { plugin.Disconnect("server_access_granted", granted); plugin.Disconnect("sign_in_failed", failed); }
    }

    private static async Task<AccountSignIn.GameCenterRequest> GameCenterProof(bool interactive)
    {
        var center = Engine.GetSingleton(GameCenterSingleton);
        if (!center.Call("is_authenticated").AsBool())
        {
            if (!interactive) throw new InvalidOperationException("Not signed in to Game Center.");
            // iOS shows its sign-in sheet once per launch; after a cancel, players sign in from Settings.
            if (center.Call("authenticate").AsInt32() != (int)Error.Ok ||
                Value(await NextEvent(center, "authentication"), "result") != "ok")
                throw new InvalidOperationException("Sign in to Game Center in the Settings app, then try again.");
        }
        // A request the plugin refuses at once (for example a player signed out meanwhile) queues no event.
        if (center.Call("request_identity_verification_signature").AsInt32() != (int)Error.Ok)
            throw new InvalidOperationException("Game Center could not confirm this player.");
        var proof = await NextEvent(center, "identity_verification_signature");
        if (Value(proof, "result") != "ok") throw new InvalidOperationException("Game Center could not confirm this player.");
        return new AccountSignIn.GameCenterRequest
        {
            PlayerId = Value(proof, "player_id"), PublicKeyUrl = Value(proof, "public_key_url"),
            Signature = Base64(proof, "signature"), Salt = Base64(proof, "salt"), Timestamp = proof["timestamp"].AsInt64()
        };
    }

    /// <summary>
    /// The GameCenter plugin reports results as queued events rather than signals: dictionaries with
    /// "type" and "result", plus "player_id" (the teamPlayerID Apple signs), "public_key_url",
    /// base64 "signature" and "salt", and an integer "timestamp" for identity signatures.
    /// </summary>
    private static async Task<Godot.Collections.Dictionary> NextEvent(GodotObject center, string type)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            while (center.Call("get_pending_event_count").AsInt32() > 0)
            {
                var next = center.Call("pop_pending_event").AsGodotDictionary();
                if (Value(next, "type").Replace("_", "") == type.Replace("_", "")) return next;
            }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException("Game Center did not answer. Try again.");
    }

    private static string Value(Godot.Collections.Dictionary data, string key) => data.TryGetValue(key, out var value) ? value.AsString() : "";

    // The bundled plugin sends base64 text; raw bytes are accepted too in case another build sends them.
    private static string Base64(Godot.Collections.Dictionary data, string key) =>
        !data.TryGetValue(key, out var value) ? "" : value.VariantType == Variant.Type.PackedByteArray ? Convert.ToBase64String(value.AsByteArray()) : value.AsString();
}
