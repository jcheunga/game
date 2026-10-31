using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The shop's App Store purchase flow on desktop: NativeIAPService and CashShopMenu against a stand-in
/// for the StoreKit plugin's engine singleton and a real local server (native/StoreKit/test/shop_desktop.sh
/// starts one). It checks what the game does with each StoreKit outcome: a grant only after the
/// server recorded the transaction, finish only after that, nothing for cancelled, pending or failed
/// purchases, Ask to Buy approvals, interrupted validation recovered later, and replays. The plugin
/// itself is checked on a simulator by StoreKitPluginReview.
/// </summary>
public partial class StoreKitPurchaseReview : Node
{
    private const string ProductId = "gold_pouch";
    private const int ProductGold = 500;
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private FakeStoreKit _store = null!;
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
            if (OS.HasFeature("ios")) throw new Exception("This review stands in for StoreKit; on iOS use StoreKitPluginReview.");
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=storekit-"))) throw new Exception("Use an isolated storekit save.");
            _server = Arg("server").TrimEnd('/');
            if (_server.Length == 0) throw new Exception("Pass --server.");
            GetTree().AutoAcceptQuit = false;
            await Wait();
            await Review();
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"STOREKIT_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task Review()
    {
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        state.SetPurchaseValidationEndpoint(_server);
        NetworkStatus.Instance.CheckNow();
        Check(await Until(() => state.PlayerAuthToken.Length > 0), "The player gets a server session");

        // NativeIAPService as it starts on an iPhone, with the stand-in as the StoreKit singleton.
        var iap = NativeIAPService.Instance;
        _store = new FakeStoreKit();
        Engine.RegisterSingleton("StoreKit", _store);
        typeof(NativeIAPService).GetProperty(nameof(NativeIAPService.Platform))!.SetValue(iap, IAPPlatform.Apple);
        typeof(NativeIAPService).GetMethod("TryInitializeApple", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(iap, null);
        Check(iap.IsAvailable && _store.Profile == state.PlayerProfileId, "NativeIAPService passes the player's profile ID to the plugin at launch");
        Check(_store.UnfinishedChecks == 1, "It checks for unfinished transactions at launch");
        Check(await Until(() => ShopProductCatalog.GetAll().All(p => iap.GetCachedProductInfo(p.Id) != null)) && ShopProductCatalog.GetAll().Count == 14,
            "The fourteen products' App Store prices reach the shop");
        var treasury = iap.GetCachedProductInfo("gold_treasury");
        Check(treasury?.FormattedPrice == "$19.99" && treasury.PriceMicros == 19_990_000 && treasury.CurrencyCode == "USD"
            && iap.GetLocalizedPrice("gold_treasury") == "$19.99", "Localized price, micros and currency are kept");
        _store.Emit("products_received", new Godot.Collections.Array { FakeStoreKit.Product("com.crownroad.gold_pouch", "¥160", 160_000_000_000L, "JPY") }, new Godot.Collections.Array());
        await Wait(.3);
        Check(iap.GetCachedProductInfo(ProductId)?.PriceMicros == 160_000_000_000L, "Price micros beyond the range of an int are kept");
        _store.Emit("products_received", new Godot.Collections.Array { FakeStoreKit.Product("com.crownroad.gold_pouch", "$0.99", 990_000L, "USD") }, new Godot.Collections.Array());
        await Wait(.3);

        var shop = await OpenShop();
        Check(_store.UnfinishedChecks == 2, "Opening the shop checks again for transactions the server has not recorded");
        var gold = state.Gold;

        // A purchase: granted once the server recorded it, finished only after that.
        _store.Next = "complete";
        await Buy(shop);
        Check(await Until(() => Status(shop).StartsWith("Purchased")), $"A completed purchase is credited ({Status(shop)})");
        var first = _store.LastTransaction;
        Check(state.Gold == gold + ProductGold && await WalletGold(state) == ProductGold, "The pack's gold arrives once, locally and on the server");
        Check(_store.Finished.Contains(first) && _store.RecordedWhenFinished[first], "The transaction is finished, after the server recorded it");
        Check(_store.Profile == state.PlayerProfileId, "Each purchase re-reads the player's profile ID");
        gold = state.Gold;

        // StoreKit returns a transaction the server already credited (the app closed before finishing).
        _store.Next = "replay";
        await Buy(shop);
        Check(await Until(() => Status(shop).Contains("already added")), $"A transaction the server already recorded is not granted again ({Status(shop)})");
        Check(state.Gold == gold && _store.Finished.Count(id => id == first) == 2, "It is finished without a second grant");

        foreach (var (outcome, expected) in new[] { ("cancelled", "Purchase cancelled."), ("pending", "The purchase is waiting for approval"), ("failed", "Purchase not completed: ") })
        {
            _store.Next = outcome;
            var finished = _store.Finished.Count;
            await Buy(shop);
            Check(await Until(() => Status(shop).StartsWith(expected)), $"A {outcome} purchase says so in the shop ({Status(shop)})");
            Check(state.Gold == gold && _store.Finished.Count == finished, $"A {outcome} purchase grants and finishes nothing");
        }

        // The Ask to Buy purchase above is approved later and arrives through Transaction.updates.
        _store.Approve();
        Check(await Until(() => state.Gold == gold + ProductGold), "An approved Ask to Buy purchase is granted");
        Check(_store.Finished.Contains(_store.LastTransaction) && _store.RecordedWhenFinished[_store.LastTransaction], "It is finished after the server recorded it");
        gold = state.Gold;

        // Validation fails: nothing is granted or finished until a later check succeeds.
        _store.Next = "complete";
        state.SetPurchaseValidationEndpoint("http://127.0.0.1:9");
        await Buy(shop);
        Check(await Until(() => Status(shop).StartsWith("Validation failed")), $"A failed validation is reported ({Status(shop)})");
        var interrupted = _store.LastTransaction;
        Check(state.Gold == gold && !_store.Finished.Contains(interrupted), "Without the server nothing is granted or finished");
        state.SetPurchaseValidationEndpoint(_server);
        iap.RetryUnfinishedPurchases();
        Check(await Until(() => state.Gold == gold + ProductGold), "The next unfinished check grants the interrupted purchase");
        Check(_store.Finished.Contains(interrupted) && _store.RecordedWhenFinished[interrupted], "The interrupted transaction is finished after the server recorded it");
        gold = state.Gold;

        // A transaction that arrives with no purchase waiting (the shop closed meanwhile).
        _store.Deliver(restored: false);
        Check(await Until(() => state.Gold == gold + ProductGold), "A transaction with no purchase waiting is still validated and granted");
        Check(_store.Finished.Contains(_store.LastTransaction), "and finished");
        gold = state.Gold;

        // A product the game does not sell is never granted or finished.
        _store.Deliver(restored: true, productId: "com.crownroad.unknown");
        await Wait(.5);
        Check(state.Gold == gold && !_store.Finished.Contains(_store.LastTransaction), "An unknown product is neither granted nor finished");
        Check(await WalletGold(state) == 4 * ProductGold, "The server credited exactly the four completed purchases");
    }

    private async Task<CashShopMenu> OpenShop()
    {
        var menu = (MapMenu)await LiveUiReview.Open(this, "MainMenu");
        menu.OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(.6);
        return Walk(menu).OfType<CashShopMenu>().Single();
    }

    /// <summary>
    /// Buy at the App Store price. A tap on desktop would pick Stripe, so the review calls the shop's native
    /// purchase step that the tap reaches on an iPhone.
    /// </summary>
    private async Task Buy(CashShopMenu shop)
    {
        var button = Walk(shop).OfType<Button>().Single(b => b.HasMeta("store_product_id") && (string)b.GetMeta("store_product_id") == ProductId);
        Check(button.Text == "Buy — $0.99", $"Buy shows the App Store price ({button.Text})");
        await Wait(.2);
        Status(shop, "");
        typeof(CashShopMenu).GetMethod("ExecuteNativeIAPPurchase", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(shop, new object[] { ShopProductCatalog.GetById(ProductId), "apple" });
    }

    private static Label StatusLabel(CashShopMenu shop) =>
        (Label)typeof(CashShopMenu).GetField("_statusLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shop)!;
    private static string Status(CashShopMenu shop) => StatusLabel(shop).Text.Trim();
    private static void Status(CashShopMenu shop, string text) => StatusLabel(shop).Text = text;

    private async Task<int> WalletGold(GameState state)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_server}/wallet?profileId={state.PlayerProfileId}");
        request.Headers.Add("X-Convoy-Profile", state.PlayerProfileId);
        request.Headers.Authorization = new("Bearer", state.PlayerAuthToken);
        using var response = await Http.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("goldBalance", out var gold) ? gold.GetInt32() : -1;
    }

    /// <summary>
    /// Stands in for the plugin's `StoreKit` singleton (native/StoreKit/src/godot_store_kit.mm): the
    /// same methods and signals, with signals emitted deferred as the plugin does. Methods keep the
    /// plugin's snake_case names because NativeIAPService calls them by name.
    /// </summary>
    private partial class FakeStoreKit : GodotObject
    {
        [Signal] public delegate void products_receivedEventHandler(Godot.Collections.Array products, Godot.Collections.Array invalidProductIds);
        [Signal] public delegate void products_failedEventHandler(string message);
        [Signal] public delegate void purchase_completedEventHandler(Godot.Collections.Dictionary transaction);
        [Signal] public delegate void purchase_failedEventHandler(string productId, string reason, string message);
        [Signal] public delegate void transaction_finishedEventHandler(string transactionId);

        public string Profile = "";
        public string Next = "complete";
        public int UnfinishedChecks;
        public string LastTransaction = "";
        public readonly List<string> Finished = new();
        /// <summary>Whether the server had recorded each transaction when the game finished it.</summary>
        public readonly Dictionary<string, bool> RecordedWhenFinished = new();
        private readonly Dictionary<string, Godot.Collections.Dictionary> _unfinished = new();
        private string _awaitingApproval = "";
        private string _lastCompleted = "";
        private long _nextId = 2000000000;

        public static Godot.Collections.Dictionary Product(string id, string price, long micros, string currency) => new()
        {
            ["product_id"] = id, ["localized_title"] = id, ["localized_description"] = "", ["localized_price"] = price,
            ["price_micros"] = micros, ["currency_code"] = currency, ["type"] = "Consumable",
        };

        public void Emit(string signal, params Variant[] args) => CallDeferred(GodotObject.MethodName.EmitSignal, new Variant[] { signal }.Concat(args).ToArray());

        public bool configure(string profileId) { Profile = profileId.Trim(); return Profile.Length > 0; }

        public void request_products(Godot.Collections.Array productIds)
        {
            var products = new Godot.Collections.Array();
            foreach (var product in ShopProductCatalog.GetAll())
                products.Add(Product(product.AppleProductId, $"${product.PriceUsd:0.00}", (long)Math.Round(product.PriceUsd * 1_000_000), "USD"));
            Emit("products_received", products, new Godot.Collections.Array());
        }

        public void purchase(string productId)
        {
            switch (Next)
            {
                case "complete": Deliver(false, productId); break;
                case "replay": Emit("purchase_completed", _unfinished.GetValueOrDefault(_lastCompleted) ?? Transaction(_lastCompleted, productId, false)); break;
                case "pending":
                    _awaitingApproval = productId;
                    Emit("purchase_failed", productId, "pending", "The purchase is waiting for approval. It will be added once it is approved.");
                    break;
                case "cancelled": Emit("purchase_failed", productId, "cancelled", "The purchase was cancelled."); break;
                default: Emit("purchase_failed", productId, "network", "The network connection was lost."); break;
            }
        }

        public bool finish_transaction(string transactionId)
        {
            Finished.Add(transactionId);
            RecordedWhenFinished[transactionId] = Recorded(transactionId);
            _unfinished.Remove(transactionId);
            Emit("transaction_finished", transactionId);
            return true;
        }

        public void check_unfinished()
        {
            UnfinishedChecks++;
            foreach (var transaction in _unfinished.Values)
            {
                var restored = transaction.Duplicate();
                restored["restored"] = true;
                Emit("purchase_completed", restored);
            }
        }

        /// <summary>A parent approves the Ask to Buy purchase: it arrives through Transaction.updates.</summary>
        public void Approve() => Deliver(true, _awaitingApproval);

        public void Deliver(bool restored, string productId = "com.crownroad.gold_pouch")
        {
            LastTransaction = (_nextId++).ToString();
            _lastCompleted = LastTransaction;
            var transaction = Transaction(LastTransaction, productId, restored);
            _unfinished[LastTransaction] = transaction;
            Emit("purchase_completed", transaction);
        }

        private static Godot.Collections.Dictionary Transaction(string id, string productId, bool restored) => new()
        {
            ["product_id"] = productId, ["transaction_id"] = id, ["original_transaction_id"] = id,
            ["receipt"] = $"stand-in.{id}.jws", ["app_account_token"] = "", ["environment"] = "Stand-in", ["restored"] = restored,
        };

        /// <summary>Whether the server's ledger has the transaction: a replayed validation says already_recorded.</summary>
        private static bool Recorded(string transactionId)
        {
            var state = GameState.Instance;
            // finish_transaction is a synchronous plugin call, so block on the check. Run it on the thread
            // pool: started from the main thread, its continuation would wait for the frame this blocks.
            var result = Task.Run(() => state.ValidatePurchaseWithServerAsync("gold_pouch", "apple", $"stand-in.{transactionId}.jws", transactionId))
                .GetAwaiter().GetResult();
            return result.Status == "already_recorded";
        }
    }
}
