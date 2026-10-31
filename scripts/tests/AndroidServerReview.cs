using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The game's server calls on Android's Mono runtime, against a throwaway local server.
/// scripts/tests/android_e2e.sh exports a debug build with this scene as the main scene and runs it
/// twice on an emulator or phone. The first launch starts from cleared app data: the game data and
/// save load, HTTPS works, the server answers NetworkStatus, the guest gets a session, the store's
/// profile sync works, and Settings → Account uploads the save, shows the backup and restores it.
/// Without Google Play the shop says billing is unavailable and charges nothing. The second launch
/// keeps the data: the server answers at boot without help, the session and the backup are still
/// there, and the account is deleted. Everything goes through the game's own code.
/// </summary>
public partial class AndroidServerReview : Node
{
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private int _failures;
    private string _server = "";
    private string _phase = "";

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool ok, string description) { GD.Print($"ANDROID_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?[(name.Length + 3)..] ?? "";
    private static IEnumerable<Node> Walk(Node node)
    { yield return node; foreach (var child in node.GetChildren()) foreach (var descendant in Walk(child)) yield return descendant; }

    private async Task<bool> Until(Func<bool> condition, double seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition()) { if (DateTime.UtcNow > deadline) return false; await Wait(.1); }
        return true;
    }

    private async void Run()
    {
        try
        {
            if (!OS.HasFeature("android")) throw new Exception("Run this on Android with scripts/tests/android_e2e.sh.");
            if (!OS.GetCmdlineUserArgs().Contains("--android-e2e")) throw new Exception("Only the android_e2e.sh build runs this.");
            _server = Arg("server").TrimEnd('/');
            if (_server.Length == 0) throw new Exception("Pass --server.");
            // Cleared app data has no server yet; the second launch finds the first one's.
            _phase = GameState.Instance.PurchaseValidationEndpoint.Length == 0 ? "fresh" : "relaunch";
            GD.Print($"ANDROID_PHASE: {_phase} (user data in {OS.GetUserDataDir()})");
            await Wait();
            if (_phase == "fresh") await Fresh();
            else await Relaunch();
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"ANDROID_RESULT: {_phase} {_failures} failures");
    }

    private async Task Fresh()
    {
        Check(!GameData.LoadFailed && GameData.Stages.Length == 100 && GameData.GetPlayerUnits().Count > 0 && GameData.GetPlayerSpells().Count > 0,
            $"The game data loads ({GameData.Stages.Length} stages, {GameData.GetPlayerUnits().Count} player units, {GameData.GetPlayerSpells().Count} spells)");
        var waveFiles = DirAccess.GetFilesAt("res://data/waves").Count(file => file.EndsWith(".json"));
        Check(WaveVersions.All.Count == waveFiles + 1, $"Every wave version loads ({WaveVersions.All.Count})");
        Check(ShopProductCatalog.GetAll().Count == 14, $"The shop catalog has its fourteen products ({ShopProductCatalog.GetAll().Count})");

        var state = GameState.Instance;
        Check(state.PlayerAuthToken.Length == 0 && state.HighestUnlockedStage == 1, "The first launch starts as a new offline guest");
        state.SetShowHints(false); state.SetAnalyticsConsent(false); state.SetDeveloperMode(true);
        Check(state.TryAddDeveloperResources(1234, 0) && SaveSystem.Instance.LastSaveSucceeded, $"Progress saves ({SaveSystem.Instance.LastSaveError})");
        Check(SaveSystem.Instance.TryLoad(out var saved) && saved.Gold == state.Gold && saved.PlayerProfileId == state.PlayerProfileId,
            "The save loads back");

        await Https();

        state.SetPurchaseValidationEndpoint(_server);
        NetworkStatus.Instance.CheckNow();
        Check(NetworkStatus.Instance.IsServerReachable && NetworkStatus.Instance.GetStatusLabel() == "Online",
            $"NetworkStatus reaches the server ({NetworkStatus.Instance.GetStatusLabel()}{(NetworkStatus.Instance.LastError.Length > 0 ? ": " + NetworkStatus.Instance.LastError : "")})");
        Check(await Until(() => state.PlayerAuthToken.Length > 0), "The guest gets a server session");
        Check(await Status($"/cloud-save/info?profileId={state.PlayerProfileId}", state.PlayerProfileId, state.PlayerAuthToken) == 200,
            "The server accepts the guest's session");

        // CashShopMenu.ExecutePurchase refreshes the profile this way when there is no session.
        var token = state.PlayerAuthToken;
        Check(PlayerProfileSyncService.RefreshProfileForBackendEndpoint(_server, out var synced) && state.PlayerAuthToken.Length > 0,
            $"The store's profile sync works ({synced})");
        Check(state.PlayerAuthToken == token || await Status($"/cloud-save/info?profileId={state.PlayerProfileId}", state.PlayerProfileId, state.PlayerAuthToken) == 200,
            "The session after profile sync is still accepted");

        var settings = await OpenSettings();
        var backedUp = state.Gold;
        Tap(settings, "Cloud save");
        await Wait(.3);
        Check(CloudSaveService.LastStatus.StartsWith("Uploaded") && Shows(settings, "Save uploaded to cloud."),
            $"Settings → Account → Cloud save uploads ({CloudSaveService.LastStatus})");
        Tap(settings, "Cloud status");
        await Wait(.3);
        var status = Walk(settings).OfType<RoyalLabel>().Select(label => label.Text).FirstOrDefault(text => text.StartsWith("Saved ")) ?? "";
        Check(status.Contains("version") && status.EndsWith("KB"), $"Cloud status shows the backup ({status})");
        state.TryAddDeveloperResources(1, 0);
        Tap(settings, "Restore cloud save");
        await Wait(1);
        Check(state.Gold == backedUp && CloudSaveService.LastStatus.Length > 0 && SaveSystem.Instance.TryLoad(out var restored) && restored.Gold == backedUp,
            $"Restore cloud save brings back the backup over later progress ({state.Gold} gold, {CloudSaveService.LastStatus})");
        state.SetDeveloperMode(false);

        await ShopWithoutPlay();
    }

    private async Task Relaunch()
    {
        var state = GameState.Instance;
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // NetworkStatus checks once when it loads, before this scene; nothing here asks it to.
        Check(NetworkStatus.Instance.IsServerReachable, $"The server answers at boot ({NetworkStatus.Instance.GetStatusLabel()} {NetworkStatus.Instance.LastError})");
        Check(state.PlayerAuthToken.Length > 0 && state.AccountProvider.Length == 0, "The guest's session survives a restart");
        var profile = state.PlayerProfileId;
        var token = state.PlayerAuthToken;
        var info = CloudSaveService.GetInfo();
        Check(info.Status == "ok" && info.UploadedAtUnixSeconds > 0 && info.UploadedAtUnixSeconds <= started && info.SizeBytes > 0,
            $"The first launch's backup is still on the server ({info.Status}, {info.SizeBytes} bytes)");
        var gold = state.Gold;
        state.SetDeveloperMode(true);
        state.TryAddDeveloperResources(7, 0);
        Check(CloudSaveService.Download(out var restored) && state.Gold == gold, $"It restores after a restart ({restored}, {state.Gold} gold)");
        state.SetDeveloperMode(false);

        await AccountSignIn.DeleteAccount();
        Check(state.PlayerProfileId != profile && state.PlayerAuthToken.Length == 0, "Deleting the account starts the device again as a new guest");
        // 410 rather than 401, so a device still holding the session can say the account was deleted.
        var ended = await Status($"/cloud-save/info?profileId={profile}", profile, token);
        Check(ended == 410, $"The server no longer accepts the deleted account's session, and says the account is gone ({ended})");
    }

    /// <summary>The production server is HTTPS, which on Android goes through .NET's own TLS on the system's certificates.</summary>
    private async Task Https()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.gstatic.com/generate_204");
            using var response = Http.SendAndWait(request);
            Check((int)response.StatusCode == 204, $"HTTPS works with a blocking call ({(int)response.StatusCode})");
        }
        catch (Exception e) { Check(false, $"HTTPS works with a blocking call ({e.GetType().Name}: {e.Message} {e.InnerException?.Message})"); }
        try
        {
            using var response = await Http.GetAsync("https://www.gstatic.com/generate_204");
            Check((int)response.StatusCode == 204, $"HTTPS works with an async call ({(int)response.StatusCode})");
        }
        catch (Exception e) { Check(false, $"HTTPS works with an async call ({e.GetType().Name}: {e.Message} {e.InnerException?.Message})"); }
        try
        {
            using var response = await Http.GetAsync("https://expired.badssl.com/");
            Check(false, $"An expired certificate is refused (answered {(int)response.StatusCode})");
        }
        catch (HttpRequestException e) when (e.InnerException is AuthenticationException)
        { Check(true, $"An expired certificate is refused ({e.InnerException.Message})"); }
        catch (Exception e) { Check(false, $"An expired certificate is refused ({e.GetType().Name}: {e.Message} {e.InnerException?.Message})"); }
    }

    /// <summary>A sideloaded build on a device without the Play Store has no billing; the shop must say so and charge nothing.</summary>
    private async Task ShopWithoutPlay()
    {
        var iap = NativeIAPService.Instance;
        Check(iap.Platform == IAPPlatform.Google, $"NativeIAPService uses Google Play Billing ({iap.Platform}, available {iap.IsAvailable})");
        var menu = (MapMenu)await LiveUiReview.Open(this, "MainMenu");
        menu.OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(.6);
        var shop = Walk(menu).OfType<CashShopMenu>().Single();
        var button = Walk(shop).OfType<Button>().FirstOrDefault(b => b.HasMeta("store_product_id") && (string)b.GetMeta("store_product_id") == "gold_pouch");
        if (button == null) { Check(false, "The shop lists the gold pouch"); return; }
        var gold = GameState.Instance.Gold;
        // One tap starts the purchase; the store's own payment sheet is the confirmation.
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(2);
        var status = Walk(shop).OfType<Label>().Select(label => label.Text.Trim()).FirstOrDefault(text => new[]
            { "Purchased", "Validation failed", "Purchase not completed", "Purchase cancelled", "Starting purchase", "Unable to establish",
              "The store is unavailable", "Native billing is unavailable" }.Any(text.StartsWith)) ?? "";
        // The shop rebuilds its buttons after the attempt, so only the status is read from here on.
        Check(!iap.IsAvailable ? status.StartsWith("Native billing is unavailable") : status.Length > 0 && !status.StartsWith("Purchased"),
            $"Without Google Play the shop says why and charges nothing ({status})");
        Check(GameState.Instance.Gold == gold, "No gold arrives without a purchase");
    }

    private async Task<SettingsMenu> OpenSettings()
    {
        var menu = (MapMenu)await LiveUiReview.Open(this, "MainMenu");
        menu.OpenHomeDestination(SceneRouter.SettingsScene);
        await Wait(.6);
        var settings = Walk(menu).OfType<SettingsMenu>().Single(page => !page.IsQueuedForDeletion());
        Tap(settings, "Account");
        await Wait(.2);
        return settings;
    }

    private static void Tap(SettingsMenu settings, string name)
    {
        var button = Walk(settings).OfType<Button>().FirstOrDefault(b => !b.IsQueuedForDeletion() && b.IsVisibleInTree() && b.AccessibilityName == name)
            ?? throw new InvalidOperationException($"Settings has no {name} button");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static bool Shows(SettingsMenu settings, string text) =>
        Walk(settings).OfType<RoyalLabel>().Any(label => !label.IsQueuedForDeletion() && label.Text == text);

    private async Task<int> Status(string path, string profile, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _server + path);
        request.Headers.Add("X-Convoy-Profile", profile);
        request.Headers.Authorization = new("Bearer", token);
        using var response = await Http.SendAsync(request);
        return (int)response.StatusCode;
    }
}
