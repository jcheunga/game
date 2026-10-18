using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace CrownroadServer.Tests;

/// <summary>
/// Store refunds: App Store Server Notifications (signed by a test chain that
/// stands in for Apple Root CA - G3), Google Play voided purchases (a stand-in
/// API), the device's adjustments and their acknowledgement, and the ledger.
/// </summary>
public static partial class ServerTests
{
    private const string AppleBundle = "com.crownroad.game";

    /// <summary>A root, intermediate and leaf shaped like Apple's App Store signing chain.</summary>
    private sealed class AppleTestChain : IDisposable
    {
        public required X509Certificate2 Root { get; init; }
        public required X509Certificate2 Intermediate { get; init; }
        public required X509Certificate2 Leaf { get; init; }
        public required ECDsa LeafKey { get; init; }
        private bool OwnsRoot { get; init; }

        /// <param name="issuer">Issue under this chain's root instead of a new one.</param>
        public static AppleTestChain Create(bool markers = true, AppleTestChain? issuer = null)
        {
            var now = DateTimeOffset.UtcNow;
            var root = issuer?.Root;
            if (root == null)
            {
                using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
                var rootRequest = new CertificateRequest("CN=Test Apple Root CA - G3", rootKey, HashAlgorithmName.SHA384);
                rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
                rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
                root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));
            }

            using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            var intermediateRequest = new CertificateRequest("CN=Test Apple Worldwide Developer Relations Certification Authority", intermediateKey, HashAlgorithmName.SHA384);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            intermediateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(intermediateRequest.PublicKey, false));
            intermediateRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(root, true, false));
            if (markers) intermediateRequest.CertificateExtensions.Add(new X509Extension(AppStoreNotifications.IntermediateMarkerOid, [0x05, 0x00], false));
            using var intermediatePublic = intermediateRequest.Create(root, now.AddHours(-1), now.AddYears(5), RandomNumberGenerator.GetBytes(8));
            var intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);

            var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var leafRequest = new CertificateRequest("CN=Test Prod ECC Mac App Store and iTunes Store Receipt Signing", leafKey, HashAlgorithmName.SHA256);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            leafRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(intermediate, true, false));
            if (markers) leafRequest.CertificateExtensions.Add(new X509Extension(AppStoreNotifications.LeafMarkerOid, [0x05, 0x00], false));
            var leaf = leafRequest.Create(intermediate, now.AddHours(-1), now.AddYears(1), RandomNumberGenerator.GetBytes(8));
            return new AppleTestChain { Root = root, Intermediate = intermediate, Leaf = leaf, LeafKey = leafKey, OwnsRoot = issuer == null };
        }

        /// <summary>A JWS like the App Store's: ES256, with x5c leaf, intermediate, root.</summary>
        public string Sign(object payload, X509Certificate2[]? x5c = null, ECDsa? key = null, string alg = "ES256")
        {
            var chain = x5c ?? [Leaf, Intermediate, Root];
            var header = Segment(new { alg, x5c = chain.Select(c => Convert.ToBase64String(c.RawData)).ToArray() });
            var body = Segment(payload);
            var signature = (key ?? LeafKey).SignData(Encoding.ASCII.GetBytes($"{header}.{body}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return $"{header}.{body}.{Base64Url.EncodeToString(signature)}";
        }

        public void Dispose() { if (OwnsRoot) Root.Dispose(); Intermediate.Dispose(); Leaf.Dispose(); LeafKey.Dispose(); }
    }

    private static string Segment(object value) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));

    private static object AppleTransaction(string transactionId, string productId = "com.crownroad.gold_pouch", string bundleId = AppleBundle)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new
        {
            transactionId, originalTransactionId = transactionId, bundleId, productId,
            purchaseDate = now - 86_400_000, signedDate = now, quantity = 1, type = "Consumable",
            inAppOwnershipType = "PURCHASED", environment = "Sandbox", revocationDate = now, revocationReason = 0
        };
    }

    private static object AppleNotification(string type, string signedTransactionInfo, string? uuid = null, string bundleId = AppleBundle, string? subtype = null) => new
    {
        notificationType = type, subtype, notificationUUID = uuid ?? Guid.NewGuid().ToString(), version = "2.0",
        signedDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        data = new { appAppleId = 6700000000L, bundleId, bundleVersion = "1", environment = "Sandbox", signedTransactionInfo }
    };

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostAppleNotification(HttpClient client, string signedPayload)
    {
        using var response = await client.PostAsync("/store/apple/notifications",
            new StringContent(JsonSerializer.Serialize(new { signedPayload }), Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        return (response.StatusCode, json.RootElement.Clone());
    }

    /// <summary>Runs a test with Apple's root swapped for the test chain's, and the bundle ID configured.</summary>
    private static async Task WithAppleNotifications(AppleTestChain chain, Func<Task> test)
    {
        var previousBundle = Environment.GetEnvironmentVariable("APPLE_IAP_BUNDLE_ID");
        Environment.SetEnvironmentVariable("APPLE_IAP_BUNDLE_ID", AppleBundle);
        AppStoreNotifications.TestRootCertificate = chain.Root;
        try { await test(); }
        finally
        {
            AppStoreNotifications.TestRootCertificate = null;
            Environment.SetEnvironmentVariable("APPLE_IAP_BUNDLE_ID", previousBundle);
        }
    }

    private static async Task<string> BuyForRefund(HttpClient client, string profileId, string productId, string platform, string transactionId, string receiptToken = "")
    {
        var response = await Post(client, "/purchase/validate", new
        {
            profileId, productId, platform,
            receiptToken = receiptToken.Length > 0 ? receiptToken : $"receipt-{transactionId}",
            transactionId
        }, profileId);
        Assert(response.RootElement.GetProperty("status").GetString() == "ok", $"expected the purchase {transactionId} to be credited");
        return response.RootElement.GetProperty("purchaseId").GetString()!;
    }

    private static object? RefundScalar(string sql, params (string Name, object Value)[] parameters)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteScalar();
    }

    private static string PurchaseStatus(string purchaseId) =>
        RefundScalar("SELECT status FROM purchases WHERE purchase_id = @id", ("@id", purchaseId)) as string ?? "";

    private static long LedgerSum(string profileId, string currency) =>
        Database.ToInt64(RefundScalar("SELECT COALESCE(SUM(amount), 0) FROM wallet_ledger WHERE profile_id = @pid AND currency_type = @currency",
            ("@pid", profileId), ("@currency", currency)));

    private static long CountAdjustments(string purchaseId) =>
        Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM wallet_adjustments WHERE purchase_id = @id", ("@id", purchaseId)));

    private static async Task<JsonElement[]> Adjustments(HttpClient client, string profileId) =>
        (await Get(client, $"/wallet/adjustments?profileId={profileId}")).RootElement.GetProperty("adjustments").EnumerateArray().Select(a => a.Clone()).ToArray();

    private static async Task<int> WalletGold(HttpClient client, string profileId) =>
        (await Get(client, $"/wallet?profileId={profileId}")).RootElement.GetProperty("goldBalance").GetInt32();

    // ── App Store ───────────────────────────────────────────

    private static async Task TestAppleRefundNotification(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        await WithAppleNotifications(chain, async () =>
        {
            const string profile = "TEST-REFUND-APPLE";
            const string transactionId = "2000000900000001";
            var purchaseId = await BuyForRefund(client, profile, "gold_pouch", "apple", transactionId);
            Assert(await WalletGold(client, profile) == 500, "expected the purchase to credit the server wallet");

            var notification = chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId))));
            var (status, body) = await PostAppleNotification(client, notification);
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "refunded", $"expected a signed REFUND to refund the purchase, got {(int)status} {body}");
            Assert(PurchaseStatus(purchaseId) == "refunded", "expected the purchase to be marked refunded");
            Assert(LedgerSum(profile, "gold") == 0, "expected a reversing ledger entry that cancels the credit");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM wallet_ledger WHERE purchase_id = @id AND reason = 'refund' AND amount = -500", ("@id", purchaseId))) == 1,
                "expected one refund ledger entry of -500 gold");
            Assert(await WalletGold(client, profile) == 0, "expected the server wallet to give the gold back");

            var adjustments = await Adjustments(client, profile);
            Assert(adjustments.Length == 1, $"expected one adjustment for the device, got {adjustments.Length}");
            var adjustment = adjustments[0];
            Assert(adjustment.GetProperty("purchaseId").GetString() == purchaseId && adjustment.GetProperty("kind").GetString() == "refund" &&
                adjustment.GetProperty("goldDelta").GetInt32() == -500 && adjustment.GetProperty("foodDelta").GetInt32() == 0 &&
                !adjustment.GetProperty("acknowledged").GetBoolean(), $"expected a -500 gold refund adjustment, got {adjustment}");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM store_events WHERE source = 'apple' AND purchase_id = @id AND outcome = 'refunded' AND environment = 'Sandbox'", ("@id", purchaseId))) == 1,
                "expected the notification in the audit trail");

            // Apple retries the same notification; another one about the same refund also changes nothing.
            var (repeatStatus, repeat) = await PostAppleNotification(client, notification);
            Assert(repeatStatus == HttpStatusCode.OK && repeat.GetProperty("status").GetString() == "duplicate_event", $"expected a repeated notification to be a no-op, got {repeat}");
            var (againStatus, again) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId)))));
            Assert(againStatus == HttpStatusCode.OK && again.GetProperty("status").GetString() == "already_refunded", $"expected a second refund notice to change nothing, got {again}");
            Assert(CountAdjustments(purchaseId) == 1 && LedgerSum(profile, "gold") == 0 && await WalletGold(client, profile) == 0,
                "expected repeats to leave one adjustment and the ledger unchanged");

            // The store hands the refunded transaction back: nothing is granted again.
            var revalidated = await Post(client, "/purchase/validate", new { profileId = profile, productId = "gold_pouch", platform = "apple", receiptToken = $"receipt-{transactionId}", transactionId }, profile);
            Assert(revalidated.RootElement.GetProperty("status").GetString() == "refunded" &&
                revalidated.RootElement.GetProperty("purchaseId").GetString() == purchaseId &&
                !revalidated.RootElement.TryGetProperty("goldCredited", out _), "expected a refunded purchase never to be handed back as a grant");
            Assert(await WalletGold(client, profile) == 0, "expected revalidating a refunded purchase to credit nothing");

            // Apple reverses the refund after a dispute: the currency comes back once.
            var (reversedStatus, reversed) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND_REVERSED", chain.Sign(AppleTransaction(transactionId)))));
            Assert(reversedStatus == HttpStatusCode.OK && reversed.GetProperty("status").GetString() == "reinstated", $"expected REFUND_REVERSED to reinstate, got {reversed}");
            Assert(PurchaseStatus(purchaseId) == "validated" && LedgerSum(profile, "gold") == 500 && await WalletGold(client, profile) == 500,
                "expected the reversal to restore the purchase, ledger and wallet");
            adjustments = await Adjustments(client, profile);
            Assert(adjustments.Length == 2 && adjustments[1].GetProperty("kind").GetString() == "refund_reversed" &&
                adjustments[1].GetProperty("goldDelta").GetInt32() == 500 &&
                adjustments[1].GetProperty("reversesAdjustmentId").GetString() == adjustments[0].GetProperty("adjustmentId").GetString(),
                "expected a reversal adjustment, after the refund, naming it");
        });
    }

    private static async Task TestAppleNotificationForgeries(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        using var impostor = AppleTestChain.Create(); // the same names, other keys
        using var unmarked = AppleTestChain.Create(markers: false, issuer: chain);
        await WithAppleNotifications(chain, async () =>
        {
            const string profile = "TEST-REFUND-FORGED";
            const string transactionId = "2000000900000002";
            var purchaseId = await BuyForRefund(client, profile, "gold_chest", "apple", transactionId);
            var transaction = AppleTransaction(transactionId, "com.crownroad.gold_chest");
            var signedTransaction = chain.Sign(transaction);
            var valid = chain.Sign(AppleNotification("REFUND", signedTransaction));
            var parts = valid.Split('.');
            var tamperedPayload = Segment(AppleNotification("REFUND", chain.Sign(AppleTransaction("2000000900000099", "com.crownroad.gold_chest"))));
            var tamperedTransactionParts = signedTransaction.Split('.');
            var tamperedTransaction = $"{tamperedTransactionParts[0]}.{Segment(AppleTransaction("2000000900000098", "com.crownroad.gold_chest"))}.{tamperedTransactionParts[2]}";
            using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var forgeries = new (string Name, string Payload)[]
            {
                ("a chain from another root", impostor.Sign(AppleNotification("REFUND", impostor.Sign(transaction)))),
                ("a payload changed after signing", $"{parts[0]}.{tamperedPayload}.{parts[2]}"),
                ("a signature from another key", chain.Sign(AppleNotification("REFUND", signedTransaction), key: otherKey)),
                ("certificates without Apple's markers", unmarked.Sign(AppleNotification("REFUND", unmarked.Sign(transaction)))),
                ("the trusted root with an impostor's intermediate", chain.Sign(AppleNotification("REFUND", signedTransaction), [impostor.Leaf, impostor.Intermediate, chain.Root], impostor.LeafKey)),
                ("a two-certificate chain", chain.Sign(AppleNotification("REFUND", signedTransaction), [chain.Leaf, chain.Intermediate])),
                ("another algorithm", chain.Sign(AppleNotification("REFUND", signedTransaction), alg: "HS256")),
                ("an unsigned token", $"{Segment(new { alg = "none" })}.{parts[1]}."),
                ("a forged transaction inside a valid notification", chain.Sign(AppleNotification("REFUND", tamperedTransaction))),
                ("another app's notification", chain.Sign(AppleNotification("REFUND", signedTransaction, bundleId: "com.example.other"))),
                ("another app's transaction", chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId, "com.crownroad.gold_chest", "com.example.other"))))),
                ("not a JWS", "not-a-jws"),
            };
            foreach (var (name, payload) in forgeries)
            {
                var (status, _) = await PostAppleNotification(client, payload);
                Assert(status == HttpStatusCode.BadRequest, $"expected {name} to be rejected, got {(int)status}");
            }
            using (var missing = await client.PostAsync("/store/apple/notifications", new StringContent("{}", Encoding.UTF8, "application/json")))
                Assert(missing.StatusCode == HttpStatusCode.BadRequest, "expected a body without signedPayload to be rejected");
            Assert(PurchaseStatus(purchaseId) == "validated" && CountAdjustments(purchaseId) == 0 && LedgerSum(profile, "gold") == 2200,
                "expected no forgery to touch the purchase");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM store_events WHERE transaction_id IN ('2000000900000099', '2000000900000098')")) == 0,
                "expected forgeries to leave no audit rows");

            // The real pinned root: a chain that claims Apple Root CA - G3 but was not issued by it fails.
            AppStoreNotifications.TestRootCertificate = null;
            using var appleRoot = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(AppleRootCaG3ForTests));
            var (pinnedStatus, _) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", signedTransaction), [chain.Leaf, chain.Intermediate, appleRoot]));
            Assert(pinnedStatus == HttpStatusCode.BadRequest, "expected a chain not issued by Apple's real root to be rejected");
            AppStoreNotifications.TestRootCertificate = chain.Root;

            // The genuine notification still works afterwards.
            var (validStatus, body) = await PostAppleNotification(client, valid);
            Assert(validStatus == HttpStatusCode.OK && body.GetProperty("status").GetString() == "refunded", "expected the genuine notification to refund");
        });

        // Without a bundle ID the server cannot check notifications, and asks Apple to retry later.
        var (unconfigured, _) = await PostAppleNotification(client, "x.y.z");
        Assert(unconfigured == HttpStatusCode.ServiceUnavailable, "expected an unconfigured server to answer 503");
    }

    private static async Task TestAppleNotificationUnknownAndIgnored(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        await WithAppleNotifications(chain, async () =>
        {
            var (status, body) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction("2000000900009999")))));
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "unknown_transaction",
                $"expected a refund of a transaction the server never credited to be acknowledged and recorded, got {body}");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM store_events WHERE transaction_id = '2000000900009999' AND outcome = 'unknown_transaction'")) == 1,
                "expected the unknown refund in the audit trail");

            const string profile = "TEST-REFUND-IGNORED";
            const string transactionId = "2000000900000003";
            var purchaseId = await BuyForRefund(client, profile, "food_rations", "apple", transactionId);
            foreach (var type in new[] { "REVOKE", "CONSUMPTION_REQUEST", "TEST" })
            {
                var (typeStatus, typeBody) = await PostAppleNotification(client, chain.Sign(AppleNotification(type, chain.Sign(AppleTransaction(transactionId, "com.crownroad.food_rations")))));
                Assert(typeStatus == HttpStatusCode.OK && typeBody.GetProperty("status").GetString() == "ignored", $"expected {type} to be acknowledged and ignored, got {typeBody}");
            }
            Assert(PurchaseStatus(purchaseId) == "validated" && CountAdjustments(purchaseId) == 0, "expected ignored types to leave the purchase alone");

            // A refund naming a different product than the recorded purchase is not applied.
            var (mismatchStatus, mismatch) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId, "com.crownroad.gold_treasury")))));
            Assert(mismatchStatus == HttpStatusCode.OK && mismatch.GetProperty("status").GetString() == "product_mismatch" && PurchaseStatus(purchaseId) == "validated",
                $"expected a product mismatch to be recorded without a refund, got {mismatch}");
        });
    }

    private static async Task TestRefundConcurrentSignals(HttpClient client)
    {
        // Apple retrying while a support refund and a Google poll land at once:
        // one refund, one adjustment, one reversal in the ledger. Several rounds,
        // because a lost race is a matter of timing (and only PostgreSQL races).
        using var chain = AppleTestChain.Create();
        await WithAppleNotifications(chain, async () =>
        {
            for (var round = 0; round < 4; round++)
            {
                var profile = $"TEST-REFUND-RACE-{round}";
                var transactionId = $"20000009000100{round:00}";
                var purchaseId = await BuyForRefund(client, profile, "gold_chest", "apple", transactionId);
                var notification = chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId, "com.crownroad.gold_chest"))));
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sends = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
                {
                    await gate.Task;
                    if (i % 2 == 0) return (await PostAppleNotification(client, notification)).Body.GetProperty("status").GetString();
                    return (await AdminRefund(client, new { purchaseId })).Body.GetProperty("status").GetString();
                })).ToArray();
                gate.SetResult();
                var statuses = await Task.WhenAll(sends);
                Assert(statuses.Count(s => s == "refunded") == 1 &&
                    statuses.All(s => s is "refunded" or "already_refunded" or "duplicate_event"),
                    $"expected exactly one refund, got {string.Join(",", statuses)}");
                Assert(CountAdjustments(purchaseId) == 1 && LedgerSum(profile, "gold") == 0 && await WalletGold(client, profile) == 0,
                    "expected one adjustment and one reversal");
            }
        });
    }

    // ── Device adjustments ──────────────────────────────────

    private static async Task TestRefundLargerThanBalance(HttpClient client)
    {
        const string profile = "TEST-REFUND-SPENT";
        var purchaseId = await BuyForRefund(client, profile, "campaign_resupply", "google", "GPA.0000-0000-0000-00001");
        var refund = await AdminRefund(client, new { purchaseId, note = "chargeback" });
        Assert(refund.Status == HttpStatusCode.OK && refund.Body.GetProperty("status").GetString() == "refunded", $"expected the refund, got {refund.Body}");
        var adjustment = (await Adjustments(client, profile)).Single();
        Assert(adjustment.GetProperty("goldDelta").GetInt32() == -3000 && adjustment.GetProperty("foodDelta").GetInt32() == -80,
            "expected the adjustment to owe the purchase's full grant, whatever the device holds");
        var adjustmentId = adjustment.GetProperty("adjustmentId").GetString()!;

        // The player spent most of it: the device could take back only 300 gold and 5 food.
        var ack = await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, goldApplied = -300, foodApplied = -5 }, profile);
        var root = ack.RootElement;
        Assert(root.GetProperty("status").GetString() == "ok" && root.GetProperty("goldUnrecovered").GetInt32() == 2700 && root.GetProperty("foodUnrecovered").GetInt32() == 75,
            $"expected the shortfall to be recorded, got {root}");
        Assert(Database.ToInt64(RefundScalar("SELECT gold_applied FROM wallet_adjustments WHERE adjustment_id = @id", ("@id", adjustmentId))) == -300,
            "expected what the device took back in the audit trail");
        Assert(LedgerSum(profile, "gold") == 0 && LedgerSum(profile, "food") == 0, "expected the ledger to reverse the full grant regardless");

        // The first acknowledgement stands; a later one (another device, a restored save) changes nothing.
        var later = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, goldApplied = -3000, foodApplied = -80 }, profile)).RootElement;
        Assert(later.GetProperty("status").GetString() == "already_acknowledged" && later.GetProperty("goldUnrecovered").GetInt32() == 2700,
            "expected the first acknowledgement to be kept");
        Assert((await Adjustments(client, profile)).Single().GetProperty("acknowledged").GetBoolean(), "expected the adjustment to show as acknowledged");

        // A device cannot claim to have taken back more than owed, or to have added currency.
        var second = await AdminRefund(client, new { purchaseId = await BuyForRefund(client, profile, "gold_pouch", "google", "GPA.0000-0000-0000-00002") });
        var clamped = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = second.Body.GetProperty("adjustmentId").GetString(), goldApplied = -9999, foodApplied = 40 }, profile)).RootElement;
        Assert(clamped.GetProperty("goldApplied").GetInt32() == -500 && clamped.GetProperty("foodApplied").GetInt32() == 0 && clamped.GetProperty("goldUnrecovered").GetInt32() == 0,
            $"expected an out-of-range acknowledgement to be clamped, got {clamped}");

        // Another player cannot see or acknowledge these adjustments.
        await PostExpect(client, "/wallet/adjustments/ack", new { profileId = "TEST-REFUND-OTHER", adjustmentId, goldApplied = 0 }, "TEST-REFUND-OTHER", HttpStatusCode.NotFound);
        Assert((await Adjustments(client, "TEST-REFUND-OTHER")).Length == 0, "expected another player to see no adjustments");
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, $"/wallet/adjustments?profileId={profile}");
        using var unauthorized = await client.SendAsync(anonymous);
        Assert(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "expected adjustments to need the player's session");
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> AdminRefund(HttpClient client, object body, bool authorized = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/refunds")
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        if (authorized) request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
        using var response = await client.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync() is { Length: > 0 } text ? text : "{}");
        return (response.StatusCode, json.RootElement.Clone());
    }

    private static async Task TestAdminRefund(HttpClient client)
    {
        const string profile = "TEST-REFUND-ADMIN";
        var purchaseId = await BuyForRefund(client, profile, "starter_kit", "stripe", "cs_test_refund_admin");
        Assert((await AdminRefund(client, new { purchaseId }, authorized: false)).Status == HttpStatusCode.Unauthorized, "expected refunds to need the admin key");
        Assert((await AdminRefund(client, new { purchaseId = "PUR-NOPE" })).Status == HttpStatusCode.NotFound, "expected an unknown purchase to be reported");
        Assert((await AdminRefund(client, new { purchaseId, action = "explode" })).Status == HttpStatusCode.BadRequest, "expected an unknown action to be refused");

        var refund = await AdminRefund(client, new { transactionId = "cs_test_refund_admin", note = "support ticket 12" });
        Assert(refund.Status == HttpStatusCode.OK && refund.Body.GetProperty("status").GetString() == "refunded", "expected an operator refund by transaction ID");
        var adjustment = (await Adjustments(client, profile)).Single();
        Assert(adjustment.GetProperty("goldDelta").GetInt32() == -800 && adjustment.GetProperty("foodDelta").GetInt32() == -30 &&
            adjustment.GetProperty("unitUnlockKept").GetBoolean(), "expected the kit's currency back and its unit kept");
        var wallet = (await Get(client, $"/wallet?profileId={profile}")).RootElement;
        Assert(wallet.GetProperty("goldBalance").GetInt32() == 0 && wallet.GetProperty("unitUnlockBalance").GetInt32() == 1,
            "expected the server wallet to drop the kit's currency and keep its unit");
        Assert((await AdminRefund(client, new { purchaseId })).Body.GetProperty("status").GetString() == "already_refunded", "expected a second refund to change nothing");

        // The kit stays bought: a refund cannot be used to get a second unit.
        await PostExpect(client, "/purchase/validate", new { profileId = profile, productId = "starter_kit", platform = "stripe", receiptToken = "receipt-kit-2", transactionId = "cs_test_refund_admin_2" }, profile, HttpStatusCode.Conflict);

        Assert((await AdminRefund(client, new { purchaseId, action = "reinstate" })).Body.GetProperty("status").GetString() == "reinstated", "expected an operator to reinstate it");
        Assert((await AdminRefund(client, new { purchaseId, action = "reinstate" })).Body.GetProperty("status").GetString() == "not_refunded", "expected a second reinstatement to change nothing");
        Assert(PurchaseStatus(purchaseId) == "validated" && LedgerSum(profile, "gold") == 800, "expected the reinstatement to restore the ledger");

        // Without App Store credentials the setup check says so instead of calling Apple.
        using (var appleTest = new HttpRequestMessage(HttpMethod.Post, "/admin/refunds/apple-test"))
        {
            appleTest.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(appleTest);
            Assert(response.StatusCode == HttpStatusCode.BadGateway && (await response.Content.ReadAsStringAsync()).Contains("not configured"),
                "expected the App Store test request to report missing credentials");
        }

        var history = (await Get(client, $"/purchase/history?profileId={profile}")).RootElement.GetProperty("purchases");
        Assert(history[0].GetProperty("status").GetString() == "validated", "expected the purchase history to show each purchase's status");
        var audit = await GetAdmin(client, "/admin/refunds");
        Assert(audit.RootElement.GetProperty("events").EnumerateArray().Count(e => e.GetProperty("purchaseId").GetString() == purchaseId) == 4,
            "expected the operator's actions in the admin audit list");
    }

    // ── Google Play ─────────────────────────────────────────

    private static async Task TestGoogleVoidedPurchases(HttpClient client)
    {
        const string profile = "TEST-REFUND-GOOGLE";
        var byOrder = await BuyForRefund(client, profile, "gold_warchest", "google", "GPA.3300-0000-0000-00001", "play-token-order");
        var byToken = await BuyForRefund(client, profile, "food_granary", "google", "GPA.3300-0000-0000-00002", "play-token-only");
        var kept = await BuyForRefund(client, profile, "gold_pouch", "google", "GPA.3300-0000-0000-00003", "play-token-kept");
        var appleSameId = await BuyForRefund(client, "TEST-REFUND-GOOGLE-APPLE", "gold_pouch", "apple", "GPA.3300-0000-0000-00004");
        var now = DateTimeOffset.UtcNow;
        var voidedAt = now.AddHours(-3).ToUnixTimeMilliseconds().ToString();

        var requests = new List<Uri>();
        GooglePlayVoidedPurchases.TestFetch = uri =>
        {
            requests.Add(uri);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            object page = query.ContainsKey("token")
                ? new
                {
                    voidedPurchases = new object[]
                    {
                        // Google may omit the order ID; the purchase token still finds it.
                        new { purchaseToken = "play-token-only", purchaseTimeMillis = voidedAt, voidedTimeMillis = voidedAt, voidedSource = 1, voidedReason = 7 },
                        new { purchaseToken = "someone-elses-token", orderId = "GPA.9999-0000-0000-00000", purchaseTimeMillis = voidedAt, voidedTimeMillis = voidedAt, voidedSource = 0, voidedReason = 1 },
                        // Only Google purchases match a Google void.
                        new { purchaseToken = "x", orderId = "GPA.3300-0000-0000-00004", purchaseTimeMillis = voidedAt, voidedTimeMillis = voidedAt, voidedSource = 0, voidedReason = 1 },
                    }
                }
                : new
                {
                    tokenPagination = new { nextPageToken = "page-2" },
                    voidedPurchases = new object[]
                    {
                        new { purchaseToken = "play-token-order", orderId = "GPA.3300-0000-0000-00001", purchaseTimeMillis = voidedAt, voidedTimeMillis = voidedAt, voidedSource = 0, voidedReason = 1, kind = "androidpublisher#voidedPurchase" },
                    }
                };
            return Task.FromResult(JsonSerializer.Serialize(page));
        };
        try
        {
            var first = await GooglePlayVoidedPurchases.PollOnceAsync(now, force: true);
            Assert(first.Polled && first.Voided == 4 && first.Refunded == 2 && first.Unknown == 2, $"expected 2 refunds and 2 unknown voids across both pages, got {first}");
            Assert(requests.Count == 2 && requests[1].Query.Contains("token=page-2") && requests[0].AbsolutePath.EndsWith("/applications/test.package/purchases/voidedpurchases"),
                "expected the poll to follow the page token");
            var firstStart = long.Parse(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(requests[0].Query)["startTime"]!);
            Assert(Math.Abs(firstStart - now.AddDays(-30).ToUnixTimeMilliseconds()) < 10 * 60_000, "expected the first poll to look back Google's 30 days");
            Assert(PurchaseStatus(byOrder) == "refunded" && PurchaseStatus(byToken) == "refunded" && PurchaseStatus(kept) == "validated" && PurchaseStatus(appleSameId) == "validated",
                "expected exactly the voided Google purchases to be refunded");
            Assert(LedgerSum(profile, "gold") == 500 && LedgerSum(profile, "food") == 0, "expected the ledger to reverse the refunded grants");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM store_events WHERE source = 'google' AND detail LIKE '%voidedReason=chargeback%'")) == 1,
                "expected the void's reason in the audit trail");

            // The next poll in this interval is skipped; a later one overlaps and changes nothing.
            requests.Clear();
            var skipped = await GooglePlayVoidedPurchases.PollOnceAsync(now.AddMinutes(1), force: false);
            Assert(!skipped.Polled && requests.Count == 0, "expected a second replica's poll within the interval to be skipped");
            var later = now + GooglePlayVoidedPurchases.Interval;
            var second = await GooglePlayVoidedPurchases.PollOnceAsync(later, force: false);
            Assert(second.Polled && second.Refunded == 0 && CountAdjustments(byOrder) == 1 && CountAdjustments(byToken) == 1,
                $"expected re-listed voids to change nothing, got {second}");
            var secondStart = long.Parse(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(requests[0].Query)["startTime"]!);
            Assert(secondStart == now.ToUnixTimeMilliseconds() - (long)TimeSpan.FromDays(2).TotalMilliseconds, "expected the next poll to start two days before the last one ended");

            // A failed poll leaves the window where it was.
            GooglePlayVoidedPurchases.TestFetch = _ => throw new HttpRequestException("Google is down");
            var failed = false;
            try { await GooglePlayVoidedPurchases.PollOnceAsync(later + GooglePlayVoidedPurchases.Interval, force: true); }
            catch (HttpRequestException) { failed = true; }
            Assert(failed && Database.ToInt64(RefundScalar("SELECT cursor_millis FROM store_sync_state WHERE name = 'google_voided_purchases'")) == later.ToUnixTimeMilliseconds(),
                "expected a failed poll to keep the last complete window");
        }
        finally
        {
            GooglePlayVoidedPurchases.TestFetch = null;
        }
    }

    // ── Schema ──────────────────────────────────────────────

    private static Task TestRefundLedgerMigration()
    {
        using var conn = Database.Open();
        void Execute(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // A ledger from before schema 8: unique per purchase and currency, with no adjustment column.
        Execute("DROP TABLE wallet_ledger");
        Execute("""
            CREATE TABLE wallet_ledger (
                entry_id TEXT PRIMARY KEY,
                profile_id TEXT NOT NULL,
                currency_type TEXT NOT NULL,
                amount INTEGER NOT NULL DEFAULT 0,
                reason TEXT NOT NULL,
                purchase_id TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL DEFAULT 0,
                UNIQUE(purchase_id, currency_type),
                FOREIGN KEY(profile_id) REFERENCES players(profile_id)
            )
        """);
        Execute("INSERT INTO players (profile_id) VALUES ('LEGACY-LEDGER') ON CONFLICT (profile_id) DO NOTHING");
        Execute("INSERT INTO wallet_ledger (entry_id, profile_id, currency_type, amount, reason, purchase_id, created_at) VALUES ('WLT-LEGACY-1', 'LEGACY-LEDGER', 'gold', 500, 'purchase', 'PUR-LEGACY-LEDGER', 1)");
        Execute("DELETE FROM schema_version WHERE version >= 8");
        Database.Initialize();
        Database.Initialize(); // The upgrade must be safe to repeat.

        Assert(Database.ToInt64(RefundScalar("SELECT amount FROM wallet_ledger WHERE entry_id = 'WLT-LEGACY-1'")) == 500, "expected the upgrade to keep ledger entries");
        Execute("INSERT INTO wallet_ledger (entry_id, profile_id, currency_type, amount, reason, purchase_id, adjustment_id, created_at) VALUES ('WLT-LEGACY-2', 'LEGACY-LEDGER', 'gold', -500, 'refund', 'PUR-LEGACY-LEDGER', 'ADJ-LEGACY', 2)");
        var rejected = false;
        try { Execute("INSERT INTO wallet_ledger (entry_id, profile_id, currency_type, amount, reason, purchase_id, adjustment_id, created_at) VALUES ('WLT-LEGACY-3', 'LEGACY-LEDGER', 'gold', -500, 'refund', 'PUR-LEGACY-LEDGER', 'ADJ-LEGACY', 3)"); }
        catch (System.Data.Common.DbException) { rejected = true; }
        Assert(rejected, "expected one entry per purchase, currency and adjustment");
        rejected = false;
        try { Execute("INSERT INTO wallet_ledger (entry_id, profile_id, currency_type, amount, reason, purchase_id, created_at) VALUES ('WLT-LEGACY-4', 'LEGACY-LEDGER', 'gold', 500, 'purchase', 'PUR-LEGACY-LEDGER', 4)"); }
        catch (System.Data.Common.DbException) { rejected = true; }
        Assert(rejected, "expected a purchase to keep one credit per currency");
        return Task.CompletedTask;
    }

    private const string AppleRootCaG3ForTests =
        "MIICQzCCAcmgAwIBAgIILcX8iNLFS5UwCgYIKoZIzj0EAwMwZzEbMBkGA1UEAwwSQXBwbGUgUm9vdCBDQSAtIEczMSYwJAYDVQQL" +
        "DB1BcHBsZSBDZXJ0aWZpY2F0aW9uIEF1dGhvcml0eTETMBEGA1UECgwKQXBwbGUgSW5jLjELMAkGA1UEBhMCVVMwHhcNMTQwNDMw" +
        "MTgxOTA2WhcNMzkwNDMwMTgxOTA2WjBnMRswGQYDVQQDDBJBcHBsZSBSb290IENBIC0gRzMxJjAkBgNVBAsMHUFwcGxlIENlcnRp" +
        "ZmljYXRpb24gQXV0aG9yaXR5MRMwEQYDVQQKDApBcHBsZSBJbmMuMQswCQYDVQQGEwJVUzB2MBAGByqGSM49AgEGBSuBBAAiA2IA" +
        "BJjpLz1AcqTtkyJygRMc3RCV8cWjTnHcFBbZDuWmBSp3ZHtfTjjTuxxEtX/1H7YyYl3J6YRbTzBPEVoA/VhYDKX1DyxNB0cTddqX" +
        "l5dvMVztK517IDvYuVTZXpmkOlEKMaNCMEAwHQYDVR0OBBYEFLuw3qFYM4iapIqZ3r6966/ayySrMA8GA1UdEwEB/wQFMAMBAf8w" +
        "DgYDVR0PAQH/BAQDAgEGMAoGCCqGSM49BAMDA2gAMGUCMQCD6cHEFl4aXTQY2e3v9GwOAEZLuN+yRhHFD/3meoyhpmvOwgPUnPWT" +
        "xnS4at+qIxUCMG1mihDK1A3UT82NQz60imOlM27jbdoXt2QfyFMm+YhidDkLF1vLUagM6BgD56KyKA==";
}
