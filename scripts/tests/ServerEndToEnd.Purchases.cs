using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public partial class ServerEndToEnd
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>
    /// A store purchase whose grant the server commits but whose response never reaches the device
    /// (timeout, dropped connection, app killed) is granted exactly once: not by the shop that lost
    /// the response, once on the relaunch Play re-delivers it to, and never again on later
    /// re-deliveries or after a reload. Relies on the server's test purchase claims
    /// (CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1, which only server_e2e.sh sets).
    /// </summary>
    private async Task PurchaseRecovery(GameState state)
    {
        var profile = state.PlayerProfileId; var token = state.PlayerAuthToken;
        var iap = NativeIAPService.Instance;
        var pouch = ShopProductCatalog.GetById("gold_pouch");
        var walletBefore = await WalletGold(profile, token);
        var goldBefore = state.Gold;

        // The shop's request reaches the server, which commits the grant...
        var lost = StorePurchase(pouch);
        var commit = await Post("/purchase/validate", new { profileId = profile, productId = pouch.Id, platform = "google", receiptToken = lost.ReceiptToken, transactionId = lost.TransactionId }, profile, token);
        var purchaseId = commit.Status == 200 ? commit.Body.GetProperty("purchaseId").GetString() ?? "" : "";
        var credited = commit.Status == 200 ? commit.Body.GetProperty("goldCredited").GetInt32() : 0;
        Check(purchaseId.Length > 0 && credited > 0, $"The server commits a store purchase ({commit.Status})");

        // ...but the shop never hears back: its connection stalls, then drops.
        var home = (MapMenu)await LiveUiReview.Open(this, "MainMenu");
        home.OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(.4);
        var shop = Walk(home).OfType<CashShopMenu>().Single();
        var status = (Label)typeof(CashShopMenu).GetField("_statusLabel", Private)!.GetValue(shop)!;
        var stall = new TcpListener(IPAddress.Loopback, 0);
        stall.Start();
        try
        {
            state.SetPurchaseValidationEndpoint($"http://127.0.0.1:{((IPEndPoint)stall.LocalEndpoint).Port}");
            var clock = Stopwatch.StartNew();
            ReturnToShop(shop, pouch, lost);
            var returned = clock.Elapsed.TotalMilliseconds;
            var frames = Engine.GetProcessFrames();
            await Wait(1);
            var ticked = Engine.GetProcessFrames() - frames;
            Check(returned < 100 && ticked > 5 && status.Text.StartsWith("Validating"),
                $"While the server is slow, the shop keeps running and shows it is validating ({returned:0} ms to hand back, {ticked} frames in 1 s)");
        }
        finally { stall.Stop(); }
        Check(await Until(() => !status.Text.StartsWith("Validating")) && state.Gold == goldBefore && !state.HasAppliedPurchase(purchaseId),
            $"A lost response grants nothing and leaves the store purchase open ({Clip(status.Text.Replace('\n', ' '))})");
        state.SetPurchaseValidationEndpoint(_server);

        // Relaunch: Play re-delivers the unconsumed purchase to the billing bridge.
        Redeliver(iap, pouch, lost);
        Check(await Until(() => state.HasAppliedPurchase(purchaseId)) && state.Gold == goldBefore + credited && state.IsPurchaseGrantSaved(purchaseId),
            "On relaunch the re-delivered purchase is credited once, from the server's already_recorded answer");

        // The shop's own purchase is credited once, and a re-delivery (the consume was lost) adds nothing.
        var bought = StorePurchase(pouch);
        var gold = state.Gold;
        ReturnToShop(shop, pouch, bought);
        Check(await Until(() => status.Text.StartsWith("Purchased")) && state.Gold == gold + credited, "A shop purchase is credited once and the shop says so");
        var replay = await iap.FulfillPurchaseAsync(bought, "google");
        Check(replay.Result.Status == "already_recorded" && !replay.Granted && replay.Finished && state.Gold == gold + credited,
            "Re-delivering the shop's purchase finishes it without a second grant");

        // Two deliveries of one purchase at once verify and grant once.
        var twice = StorePurchase(pouch);
        gold = state.Gold;
        var both = await Task.WhenAll(iap.FulfillPurchaseAsync(twice, "google"), iap.FulfillPurchaseAsync(twice, "google"));
        Check(both.Count(f => f.Granted) == 1 && both.Any(f => f.Result.Status == "in_progress") && state.Gold == gold + credited,
            "Two simultaneous deliveries of one purchase grant once");

        // Killed after saving but before consuming: the purchases come back after a reload, and add nothing.
        await LiveUiReview.Open(this, "MainMenu");
        gold = state.Gold;
        state.ReloadFromDisk();
        var afterReload = await Task.WhenAll(new[] { lost, bought, twice }.Select(p => iap.FulfillPurchaseAsync(p, "google")));
        Check(state.Gold == gold && afterReload.All(f => f.Result.Status == "already_recorded" && !f.Granted && f.Finished),
            "After a reload, re-delivered purchases are finished without a second grant");
        var walletAfter = await WalletGold(profile, token);
        Check(walletAfter - walletBefore == state.Gold - goldBefore && state.Gold - goldBefore == 3 * credited,
            $"The device gained exactly what the server credited ({state.Gold - goldBefore} gold for 3 purchases)");
    }

    private static IAPPurchaseResult StorePurchase(ShopProduct product) => new()
        { Success = true, ProductId = product.Id, TransactionId = $"GPA.e2e-{Guid.NewGuid():N}", ReceiptToken = $"play-token-{Guid.NewGuid():N}" };

    /// <summary>The shop's handler for the store sheet returning a paid purchase.</summary>
    private static void ReturnToShop(CashShopMenu shop, ShopProduct product, IAPPurchaseResult purchase) =>
        typeof(CashShopMenu).GetMethod("OnNativePurchaseReturned", Private)!.Invoke(shop, [product, "google", purchase]);

    /// <summary>The billing bridge's purchase_received signal for an unfinished purchase found at launch.</summary>
    private static void Redeliver(NativeIAPService iap, ShopProduct product, IAPPurchaseResult purchase) =>
        typeof(NativeIAPService).GetMethod("OnGooglePurchaseReceived", Private)!.Invoke(iap, [product.GoogleProductId, purchase.TransactionId, purchase.ReceiptToken, true]);

    private async Task<int> WalletGold(string profile, string token)
    {
        var wallet = await Get($"/wallet?profileId={profile}", profile, token);
        return wallet.Status == 200 ? wallet.Body.GetProperty("goldBalance").GetInt32() : -1;
    }

    private async Task<(int Status, JsonElement Body)> Post(string path, object body, string profile, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _server + path)
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        try { using var json = JsonDocument.Parse(text); return ((int)response.StatusCode, json.RootElement.Clone()); }
        catch (JsonException) { return ((int)response.StatusCode, default); }
    }
}
