using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Exercises the StoreKit plugin on an iOS simulator through its engine singleton, against a real
/// local server: native/StoreKit/test/simulator_e2e.sh launches the game once per phase with
/// Crownroad.storekit loaded, and with Ask to Buy or a simulated failure where a phase needs it.
/// The driver validates each transaction with the server the way NativeIAPService does and calls
/// finish_transaction only after the server recorded it. It talks to the server with JsonDocument
/// and Utf8JsonWriter rather than the game's providers, so it checks the plugin on its own (the
/// shop phase, StoreKitShopReview, buys through the game). Player, session and expected balances
/// carry over in user://storekit-plugin-review.txt.
/// </summary>
public partial class StoreKitPluginReview : Node
{
    private const string Pouch = "com.crownroad.gold_pouch";
    private const int PouchGold = 400;
    private const string HandoffPath = "user://storekit-plugin-review.txt";
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<Godot.Collections.Dictionary> _completed = new();
    private readonly List<string> _finished = new();
    private readonly List<string> _failed = new();
    private readonly List<(Godot.Collections.Array Products, Godot.Collections.Array Invalid)> _productAnswers = new();
    private GodotObject _store;
    private int _failures;
    private string _server = "";
    private Dictionary<string, string> _handoff = new();

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool ok, string description) { GD.Print($"STOREKIT_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?[(name.Length + 3)..] ?? "";

    private async Task<bool> Until(Func<bool> condition, double seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition()) { if (DateTime.UtcNow > deadline) return false; await Wait(.1); }
        return true;
    }

    private async void Run()
    {
        var phase = Arg("phase");
        try
        {
            if (!OS.HasFeature("ios")) throw new Exception("Run this on an iOS simulator with native/StoreKit/test/simulator_e2e.sh.");
            _server = Arg("server").TrimEnd('/');
            if (_server.Length == 0) throw new Exception("Pass --server.");
            Check(Engine.HasSingleton("StoreKit"), "The StoreKit plugin is in the build");
            _store = Engine.GetSingleton("StoreKit");
            // The game's own handlers would act on the same transactions; this driver plays their part.
            foreach (var signal in new[] { "products_received", "products_failed", "purchase_completed", "purchase_failed", "transaction_finished" })
                foreach (var connection in _store.GetSignalConnectionList(signal))
                    _store.Disconnect(signal, connection["callable"].AsCallable());
            _store.Connect("products_received", Callable.From<Godot.Collections.Array, Godot.Collections.Array>((p, i) => _productAnswers.Add((p, i))));
            _store.Connect("purchase_completed", Callable.From<Godot.Collections.Dictionary>(t =>
            {
                GD.Print($"STOREKIT_EVENT: transaction {t["transaction_id"]} {t["product_id"]}{(t["restored"].AsBool() ? " restored" : "")} {SignedPayload(t["receipt"].AsString())}");
                _completed.Add(t);
            }));
            _store.Connect("transaction_finished", Callable.From<string>(id => { GD.Print($"STOREKIT_EVENT: finished {id}"); _finished.Add(id); }));
            _store.Connect("purchase_failed", Callable.From<string, string, string>((_, reason, message) =>
            {
                GD.Print($"STOREKIT_EVENT: failed {reason}: {message}");
                _failed.Add(reason);
            }));
            if (phase != "buy" && Godot.FileAccess.FileExists(HandoffPath))
                foreach (var line in Godot.FileAccess.GetFileAsString(HandoffPath).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    _handoff[line[..line.IndexOf('=')]] = line[(line.IndexOf('=') + 1)..];
            if (phase != "buy") Check(_store.Call("configure", _handoff["profile"]).AsBool(), "The player's profile configures the plugin");
            await Wait(.5);
            switch (phase)
            {
                case "buy": await Buy(); break;
                case "clean": await Clean(); break;
                case "unverified": await NotCompleted("unverified", "A purchase whose signature fails verification"); break;
                case "ask-to-buy": await AskToBuy(); break;
                case "interrupt": await Interrupt(); break;
                case "recover": await Recover(); break;
                default: throw new Exception($"Unknown --phase={phase}.");
            }
            using var file = Godot.FileAccess.Open(HandoffPath, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(string.Join("\n", _handoff.Select(pair => $"{pair.Key}={pair.Value}")));
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"STOREKIT_RESULT: {phase} {_failures} failures");
    }

    /// <summary>A new player loads the products and buys one: validated, then finished, never before.</summary>
    private async Task Buy()
    {
        var profile = $"CVY-SK{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        _handoff["profile"] = profile;
        _handoff["token"] = await SignIn(profile);
        Check(_handoff["token"].Length >= 32, "The player gets a server session");
        Check(!_store.Call("configure", "  ").AsBool(), "A blank profile ID leaves purchases disabled");
        _store.Call("purchase", Pouch);
        Check(await Until(() => _failed.Contains("not_configured"), 5), "Buying without a profile fails as not_configured");
        Check(_store.Call("configure", profile).AsBool(), "The player's profile configures the plugin");

        var catalog = Catalog();
        // The game's shop asks for its products at launch too, so this request's answer is the one naming the unknown ID.
        _store.Call("request_products", new Godot.Collections.Array(catalog.Keys.Append("com.crownroad.not_a_product").Select(id => Variant.CreateFrom(id))));
        Godot.Collections.Array products = null, invalid = null;
        await Until(() => _productAnswers.Any(answer => answer.Invalid.Count > 0), 10);
        if (_productAnswers.Any(answer => answer.Invalid.Count > 0)) (products, invalid) = _productAnswers.First(answer => answer.Invalid.Count > 0);
        Check(products?.Count == 14, $"The fourteen products load from the StoreKit configuration ({products?.Count})");
        Check(invalid?.Count == 1 && invalid[0].AsString() == "com.crownroad.not_a_product", "An unknown product ID is reported as invalid");
        foreach (var item in products ?? new())
        {
            var product = item.AsGodotDictionary();
            var productId = product["product_id"].AsString();
            var price = catalog.GetValueOrDefault(productId);
            Check(product["localized_price"].AsString() == $"${price:0.00}" && product["price_micros"].AsInt64() == (long)Math.Round(price * 1_000_000)
                && product["currency_code"].AsString() == "USD" && product["type"].AsString() == "Consumable" && product["localized_title"].AsString().Length > 0,
                $"{productId}: {product["localized_price"]}, {product["price_micros"]} micros, {product["currency_code"]}");
        }

        _store.Call("purchase", Pouch);
        Check(await Until(() => _completed.Count > 0, 20), "The purchase returns a transaction");
        var transaction = _completed.Last();
        var id = transaction["transaction_id"].AsString();
        var expected = ServerAppAccountToken(profile);
        Check(!transaction["restored"].AsBool() && transaction["product_id"].AsString() == Pouch && transaction["environment"].AsString() == "Xcode",
            "StoreKit returns the transaction to the purchase that started it");
        Check(transaction["app_account_token"].AsString() == expected, $"The transaction carries the player's appAccountToken ({expected})");
        Check(SignedClaim(transaction["receipt"].AsString(), "appAccountToken") == expected && SignedClaim(transaction["receipt"].AsString(), "transactionId") == id,
            "The signed transaction (JWS) carries the token the server derives from the profile ID");

        _completed.Clear();
        _store.Call("check_unfinished");
        Check(await Until(() => _completed.Any(t => t["transaction_id"].AsString() == id), 5) && _completed.Last()["restored"].AsBool(),
            "Until the server answers, the transaction stays unfinished");
        var (status, gold) = await Validate(transaction);
        Check(status == "ok" && gold == PouchGold, $"The server records the purchase ({status}, {gold} gold)");
        Check(_store.Call("finish_transaction", id).AsBool(), "finish_transaction accepts the transaction it handed over");
        Check(await Until(() => _finished.Contains(id), 5), "The transaction is finished");
        Check(!_store.Call("finish_transaction", "4242424242").AsBool(), "finish_transaction refuses a transaction it did not hand over");
        Check(await WalletGold() == PouchGold, "The server wallet holds the pack's gold");
        _handoff["wallet"] = PouchGold.ToString();
    }

    /// <summary>After a relaunch nothing is left unfinished.</summary>
    private async Task Clean()
    {
        _store.Call("check_unfinished");
        await Wait(3);
        Check(_completed.Count == 0, $"No transaction is left unfinished ({_completed.Count} returned)");
        Check(await WalletGold() == int.Parse(_handoff["wallet"]), "Nothing more was recorded");
    }

    /// <summary>A purchase that does not complete returns no transaction.</summary>
    private async Task NotCompleted(string reason, string what)
    {
        _store.Call("purchase", Pouch);
        Check(await Until(() => _failed.Count > 0, 20) && _failed[0] == reason, $"{what} fails as {reason} ({string.Join(", ", _failed)})");
        await Wait(2);
        Check(_completed.Count == 0, $"{what} returns no transaction to grant");
    }

    /// <summary>
    /// Ask to Buy: the purchase reports pending. simulator_e2e.sh approves it when this prints
    /// STOREKIT_AWAITING_APPROVAL, and the approval arrives through Transaction.updates. (Xcode's test
    /// store signs a declined request like a completed purchase, so declining is checked on a device.)
    /// </summary>
    private async Task AskToBuy()
    {
        _store.Call("purchase", Pouch);
        Check(await Until(() => _failed.Contains("pending"), 20), $"An Ask to Buy purchase reports pending ({string.Join(", ", _failed)})");
        await Wait(1);
        Check(_completed.Count == 0, "A pending purchase returns no transaction to grant");
        GD.Print("STOREKIT_AWAITING_APPROVAL");
        Check(await Until(() => _completed.Count > 0, 60), "The approved purchase arrives");
        var transaction = _completed.Last();
        var id = transaction["transaction_id"].AsString();
        Check(transaction["restored"].AsBool() && transaction["product_id"].AsString() == Pouch, "The approval arrives through Transaction.updates, outside the purchase call");
        var (status, gold) = await Validate(transaction);
        Check(status == "ok" && gold == PouchGold, $"The server records the approved purchase ({status})");
        _store.Call("finish_transaction", id);
        Check(await Until(() => _finished.Contains(id), 5), "The approved transaction is finished");
        _handoff["wallet"] = (int.Parse(_handoff["wallet"]) + PouchGold).ToString();
        Check(await WalletGold() == int.Parse(_handoff["wallet"]), "The server wallet holds the approved pack's gold");
    }

    /// <summary>The server cannot be reached: nothing is finished, and the plugin does not finish it either.</summary>
    private async Task Interrupt()
    {
        _store.Call("purchase", Pouch);
        Check(await Until(() => _completed.Count > 0, 20), "The purchase returns a transaction");
        var transaction = _completed.Last();
        var server = _server;
        _server = "http://127.0.0.1:9";
        var (status, _) = await Validate(transaction);
        _server = server;
        Check(status == "unreachable", $"Validation fails while the server is unreachable ({status})");
        await Wait(3);
        Check(_finished.Count == 0, "The plugin never finishes a transaction on its own");
        _handoff["interrupted"] = transaction["transaction_id"].AsString();
    }

    /// <summary>On the next launch the interrupted transaction is still there, is recorded once and finished.</summary>
    private async Task Recover()
    {
        var id = _handoff["interrupted"];
        _store.Call("check_unfinished");
        Check(await Until(() => _completed.Any(t => t["transaction_id"].AsString() == id), 10), "The interrupted transaction is returned on the next launch");
        var transaction = _completed.First(t => t["transaction_id"].AsString() == id);
        Check(transaction["restored"].AsBool(), "It comes back as a restored transaction");
        var (status, _) = await Validate(transaction);
        Check(status == "ok", $"The server records it ({status})");
        (status, _) = await Validate(transaction);
        Check(status == "already_recorded", $"Validating it again does not grant twice ({status})");
        _store.Call("finish_transaction", id);
        Check(await Until(() => _finished.Contains(id), 5), "The recovered transaction is finished");
        _handoff["wallet"] = (int.Parse(_handoff["wallet"]) + PouchGold).ToString();
        Check(await WalletGold() == int.Parse(_handoff["wallet"]), "The server wallet holds the recovered pack's gold once");
    }

    /// <summary>Apple product ID to USD price, from data/shop_products.json.</summary>
    private static Dictionary<string, double> Catalog()
    {
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/shop_products.json"));
        return json.RootElement.EnumerateArray().ToDictionary(p => p.GetProperty("appleProductId").GetString()!, p => p.GetProperty("priceUsd").GetDouble());
    }

    private async Task<string> SignIn(string profile)
    {
        var body = Json(writer =>
        {
            writer.WriteStartObject("profile");
            writer.WriteString("playerProfileId", profile);
            writer.WriteString("playerCallsign", "StoreKit review");
            writer.WriteEndObject();
        });
        var (_, json) = await Post("/player-profile", body, profile, "");
        return json.TryGetProperty("sessionToken", out var token) ? token.GetString() ?? "" : "";
    }

    /// <summary>The request NativeIAPService sends through GameState.ValidatePurchaseWithServer.</summary>
    private async Task<(string Status, int Gold)> Validate(Godot.Collections.Dictionary transaction)
    {
        var body = Json(writer =>
        {
            writer.WriteString("profileId", _handoff["profile"]);
            writer.WriteString("productId", transaction["product_id"].AsString().Replace("com.crownroad.", ""));
            writer.WriteString("platform", "apple");
            writer.WriteString("receiptToken", transaction["receipt"].AsString());
            writer.WriteString("transactionId", transaction["transaction_id"].AsString());
        });
        try
        {
            var (_, json) = await Post("/purchase/validate", body, _handoff["profile"], _handoff["token"]);
            var status = json.TryGetProperty("status", out var s) ? s.GetString() ?? "" : json.GetProperty("error").GetString() ?? "";
            return (status, json.TryGetProperty("goldCredited", out var gold) ? gold.GetInt32() : 0);
        }
        catch (HttpRequestException) { return ("unreachable", 0); }
    }

    private async Task<int> WalletGold()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_server}/wallet?profileId={_handoff["profile"]}");
        request.Headers.Add("X-Convoy-Profile", _handoff["profile"]);
        request.Headers.Authorization = new("Bearer", _handoff["token"]);
        using var response = await Http.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("goldBalance", out var gold) ? gold.GetInt32() : -1;
    }

    private async Task<(int Status, JsonElement Body)> Post(string path, string body, string profile, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _server + path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await Http.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ((int)response.StatusCode, json.RootElement.Clone());
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>ProfileAppAccountToken in server/StorePurchaseVerification.cs.</summary>
    private static string ServerAppAccountToken(string profileId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(profileId.Trim()))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes).ToString();
    }

    private static string SignedPayload(string jws)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3) return "";
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        return Encoding.UTF8.GetString(Convert.FromBase64String(payload + new string('=', (4 - payload.Length % 4) % 4)));
    }

    private static string SignedClaim(string jws, string claim)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3) return "";
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += new string('=', (4 - payload.Length % 4) % 4);
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (!json.RootElement.TryGetProperty(claim, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
    }
}
