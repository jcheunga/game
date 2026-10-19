using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The game itself on an iOS simulator, as the NativeAOT build runs it: its data, waves, shop
/// catalog and save load, the player gets a server session and a cloud backup, and a pack is
/// bought from the real shop through NativeIAPService and the StoreKit plugin, validated by a
/// real local server, granted once and finished after that. native/StoreKit/test/simulator_e2e.sh
/// runs it as its shop phase, with Crownroad.storekit loaded and no earlier transactions. Unlike
/// StoreKitPluginReview it leaves the game's own StoreKit handlers in place and uses no
/// reflection, so everything it touches goes through the game's own code.
/// </summary>
public partial class StoreKitShopReview : Node
{
    private const string ProductId = "gold_pouch";
    private const int ProductGold = 500;
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<string> _completed = new();
    private readonly List<string> _finished = new();
    private int _failures;
    private string _server = "";

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool ok, string description) { GD.Print($"STOREKIT_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
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
            if (!OS.HasFeature("ios")) throw new Exception("Run this on an iOS simulator with native/StoreKit/test/simulator_e2e.sh.");
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=storekit-"))) throw new Exception("Use an isolated storekit save.");
            _server = Arg("server").TrimEnd('/');
            if (_server.Length == 0) throw new Exception("Pass --server.");
            await Wait();
            GameLoads();
            await Review();
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"STOREKIT_RESULT: shop {_failures} failures");
    }

    private void GameLoads()
    {
        Check(!GameData.LoadFailed && GameData.Stages.Length == 100 && GameData.GetPlayerUnits().Count > 0 && GameData.GetPlayerSpells().Count > 0,
            $"The game data loads ({GameData.Stages.Length} stages, {GameData.GetPlayerUnits().Count} player units, {GameData.GetPlayerSpells().Count} spells)");
        var waveFiles = DirAccess.GetFilesAt("res://data/waves").Count(file => file.EndsWith(".json"));
        Check(WaveVersions.All.Count == waveFiles + 1, $"Every wave version loads ({WaveVersions.All.Count})");
        Check(ShopProductCatalog.GetAll().Count == 10, $"The shop catalog has its ten products ({ShopProductCatalog.GetAll().Count})");

        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        state.SetDeveloperMode(true);
        Check(state.TryAddDeveloperResources(1234, 0) && SaveSystem.Instance.LastSaveSucceeded, $"Progress saves ({SaveSystem.Instance.LastSaveError})");
        Check(SaveSystem.Instance.TryLoad(out var saved) && saved.Gold == state.Gold && saved.PlayerProfileId == state.PlayerProfileId,
            "The save loads back");
    }

    private async Task Review()
    {
        var state = GameState.Instance;
        state.SetPurchaseValidationEndpoint(_server);
        NetworkStatus.Instance.CheckNow();
        Check(await Until(() => state.PlayerAuthToken.Length > 0), "The player gets a server session");
        var backedUp = state.Gold;
        Check(CloudSaveService.Upload(out var uploaded), $"The save backs up to the cloud ({uploaded})");
        var info = CloudSaveService.GetInfo();
        Check(info.Status == "ok" && info.SizeBytes > 0, $"The cloud backup is listed ({info.Status}, {info.SizeBytes} bytes)");
        state.TryAddDeveloperResources(1, 0);
        Check(CloudSaveService.Download(out var restored) && state.Gold == backedUp, $"and restores over later progress ({restored}, {state.Gold} gold)");
        state.SetDeveloperMode(false);

        var iap = NativeIAPService.Instance;
        Check(iap.IsAvailable && iap.Platform == IAPPlatform.Apple, "NativeIAPService uses the StoreKit plugin");
        var store = Engine.GetSingleton("StoreKit");
        store.Connect("purchase_completed", Callable.From<Godot.Collections.Dictionary>(t => _completed.Add(t["transaction_id"].AsString())));
        store.Connect("transaction_finished", Callable.From<string>(id => _finished.Add(id)));
        Check(await Until(() => ShopProductCatalog.GetAll().All(p => iap.GetCachedProductInfo(p.Id) != null)),
            "The ten products' App Store prices reach the game");
        Check(iap.GetLocalizedPrice(ProductId) == "$0.99" && iap.GetCachedProductInfo(ProductId)?.PriceMicros == 990_000,
            $"The pouch costs $0.99 ({iap.GetLocalizedPrice(ProductId)})");

        var menu = (MapMenu)await LiveUiReview.Open(this, "MainMenu");
        menu.OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(.6);
        var shop = Walk(menu).OfType<CashShopMenu>().Single();
        var button = Walk(shop).OfType<Button>().Single(b => b.HasMeta("store_product_id") && (string)b.GetMeta("store_product_id") == ProductId);
        Check(button.Text == "Buy — $0.99", $"The shop shows the App Store price ({button.Text})");
        var gold = state.Gold;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.2);
        Check(button.Text == "Confirm — $0.99", $"Buy asks for confirmation ({button.Text})");
        button.EmitSignal(BaseButton.SignalName.Pressed);

        Check(await Until(() => Status(shop).StartsWith("Purchased"), 30), $"The purchase is credited ({Status(shop)})");
        Check(_completed.Count == 1, $"StoreKit returned one transaction ({_completed.Count})");
        var id = _completed.FirstOrDefault() ?? "";
        Check(state.Gold == gold + ProductGold, $"The pouch's {ProductGold} gold arrives ({state.Gold - gold})");
        Check(await WalletGold(state) == ProductGold, "The server recorded the purchase in the player's wallet");
        Check(await Until(() => _finished.Contains(id), 5), $"The transaction is finished ({id})");
        Check(SaveSystem.Instance.TryLoad(out var saved) && saved.Gold == state.Gold && saved.AppliedPurchaseIds.Length == 1,
            "The grant is saved with its purchase ID");

        _completed.Clear();
        iap.RetryUnfinishedPurchases();
        await Wait(3);
        Check(_completed.Count == 0 && state.Gold == gold + ProductGold, "Nothing is left unfinished or granted twice");
    }

    /// <summary>The shop's purchase status line (CashShopMenu's status label).</summary>
    private static string Status(CashShopMenu shop) =>
        Walk(shop).OfType<Label>().Select(label => label.Text.Trim()).FirstOrDefault(text => new[]
        {
            "Purchased", "Validation failed", "Purchase not completed", "Purchase cancelled", "Starting purchase", "Validating receipt",
            "Unable to establish", "The store is unavailable", "Native billing is unavailable",
        }.Any(text.StartsWith)) ?? "";

    private async Task<int> WalletGold(GameState state)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_server}/wallet?profileId={state.PlayerProfileId}");
        request.Headers.Add("X-Convoy-Profile", state.PlayerProfileId);
        request.Headers.Authorization = new("Bearer", state.PlayerAuthToken);
        using var response = await Http.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("goldBalance", out var gold) ? gold.GetInt32() : -1;
    }
}
