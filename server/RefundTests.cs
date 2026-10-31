using System.Buffers.Text;
using System.Collections.Concurrent;
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

    private static async Task<int> WalletShards(HttpClient client, string profileId) =>
        (await Get(client, $"/wallet?profileId={profileId}")).RootElement.GetProperty("shardsBalance").GetInt32();

    // ── App Store ───────────────────────────────────────────

    private static async Task TestAppleRefundNotification(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        await WithAppleNotifications(chain, async () =>
        {
            const string profile = "TEST-REFUND-APPLE";
            const string transactionId = "2000000900000001";
            var purchaseId = await BuyForRefund(client, profile, "gold_pouch", "apple", transactionId);
            Assert(await WalletGold(client, profile) == 400, "expected the purchase to credit the server wallet");

            var notification = chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId))));
            var (status, body) = await PostAppleNotification(client, notification);
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "refunded", $"expected a signed REFUND to refund the purchase, got {(int)status} {body}");
            Assert(PurchaseStatus(purchaseId) == "refunded", "expected the purchase to be marked refunded");
            Assert(LedgerSum(profile, "gold") == 0, "expected a reversing ledger entry that cancels the credit");
            Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM wallet_ledger WHERE purchase_id = @id AND reason = 'refund' AND amount = -400", ("@id", purchaseId))) == 1,
                "expected one refund ledger entry of -400 gold");
            Assert(await WalletGold(client, profile) == 0, "expected the server wallet to give the gold back");

            var adjustments = await Adjustments(client, profile);
            Assert(adjustments.Length == 1, $"expected one adjustment for the device, got {adjustments.Length}");
            var adjustment = adjustments[0];
            Assert(adjustment.GetProperty("purchaseId").GetString() == purchaseId && adjustment.GetProperty("kind").GetString() == "refund" &&
                adjustment.GetProperty("goldDelta").GetInt32() == -400 && adjustment.GetProperty("foodDelta").GetInt32() == 0 &&
                !adjustment.GetProperty("acknowledged").GetBoolean(), $"expected a -400 gold refund adjustment, got {adjustment}");
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
            Assert(PurchaseStatus(purchaseId) == "validated" && LedgerSum(profile, "gold") == 400 && await WalletGold(client, profile) == 400,
                "expected the reversal to restore the purchase, ledger and wallet");
            adjustments = await Adjustments(client, profile);
            Assert(adjustments.Length == 2 && adjustments[1].GetProperty("kind").GetString() == "refund_reversed" &&
                adjustments[1].GetProperty("goldDelta").GetInt32() == 400 &&
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
            Assert(PurchaseStatus(purchaseId) == "validated" && CountAdjustments(purchaseId) == 0 && LedgerSum(profile, "gold") == 1800,
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
            foreach (var type in new[] { "REVOKE", "TEST" })
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

    // ── App Store consumption requests ──────────────────────

    private sealed record AppleApiCall(HttpMethod Method, Uri Uri, string Body, string Authorization);

    /// <summary>An unrefunded consumable, as Apple describes it in a CONSUMPTION_REQUEST.</summary>
    private static object AppleConsumable(string transactionId, string productId = "com.crownroad.gold_chest", string environment = "Sandbox")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new
        {
            transactionId, originalTransactionId = transactionId, bundleId = AppleBundle, productId,
            purchaseDate = now - 3_600_000, signedDate = now, quantity = 1, type = "Consumable",
            inAppOwnershipType = "PURCHASED", environment
        };
    }

    private static object AppleConsumptionRequest(string signedTransactionInfo, string environment = "Sandbox", string? uuid = null, DateTimeOffset? signedAt = null) => new
    {
        notificationType = "CONSUMPTION_REQUEST", notificationUUID = uuid ?? Guid.NewGuid().ToString(), version = "2.0",
        signedDate = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
        data = new
        {
            appAppleId = 6700000000L, bundleId = AppleBundle, bundleVersion = "1", environment, signedTransactionInfo,
            consumptionRequestReason = "UNINTENDED_PURCHASE"
        }
    };

    private static (string Outcome, string Detail, string PurchaseId) AppleEvent(string notificationUuid)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT outcome, detail, purchase_id FROM store_events WHERE event_key = @key";
        cmd.Parameters.AddWithValue("@key", $"apple:{notificationUuid}");
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1), reader.GetString(2)) : ("", "", "");
    }

    /// <summary>
    /// Runs a test with App Store API credentials (a throwaway key) and Apple's API answered
    /// by <paramref name="apple"/>; every call is logged in the queue the test gets.
    /// </summary>
    private static async Task WithAppleApi(
        ECDsa apiKey, Func<HttpRequestMessage, Task<HttpResponseMessage>> apple, Func<ConcurrentQueue<AppleApiCall>, Task> test)
    {
        var names = new[] { "APPLE_IAP_ISSUER_ID", "APPLE_IAP_KEY_ID", "APPLE_IAP_PRIVATE_KEY", AppStoreConsumption.SwitchVariable };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        Environment.SetEnvironmentVariable("APPLE_IAP_ISSUER_ID", "test-issuer");
        Environment.SetEnvironmentVariable("APPLE_IAP_KEY_ID", "TESTKEY123");
        Environment.SetEnvironmentVariable("APPLE_IAP_PRIVATE_KEY", apiKey.ExportPkcs8PrivateKeyPem());
        Environment.SetEnvironmentVariable(AppStoreConsumption.SwitchVariable, null);
        var calls = new ConcurrentQueue<AppleApiCall>();
        AppStoreConsumption.TestSend = async request =>
        {
            calls.Enqueue(new AppleApiCall(request.Method, request.RequestUri!, await request.Content!.ReadAsStringAsync(),
                request.Headers.Authorization?.ToString() ?? ""));
            return await apple(request);
        };
        try { await test(calls); }
        finally
        {
            AppStoreConsumption.TestSend = null;
            foreach (var (name, value) in previous) Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static Task<HttpResponseMessage> AppleAnswers(HttpStatusCode status, string body = "") =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static async Task TestAppleConsumptionRequest(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        using var apiKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await WithAppleNotifications(chain, () => WithAppleApi(apiKey, _ => AppleAnswers(HttpStatusCode.Accepted), async calls =>
        {
            const string profile = "TEST-CONSUMPTION";
            const string transactionId = "2000000900020001";
            var purchaseId = await BuyForRefund(client, profile, "gold_chest", "apple", transactionId);
            var signedTransaction = chain.Sign(AppleConsumable(transactionId));

            // Off by default: recorded, nothing sent.
            var offUuid = Guid.NewGuid().ToString();
            var (offStatus, off) = await PostAppleNotification(client, chain.Sign(AppleConsumptionRequest(signedTransaction, uuid: offUuid)));
            Assert(offStatus == HttpStatusCode.OK && off.GetProperty("status").GetString() == "consumption_skipped", $"expected the request to be skipped while switched off, got {(int)offStatus} {off}");
            Assert(calls.IsEmpty, "expected nothing sent to Apple without the switch");
            var offEvent = AppleEvent(offUuid);
            Assert(offEvent.Outcome == "consumption_skipped" && offEvent.PurchaseId == purchaseId &&
                offEvent.Detail.Contains("consumptionRequestReason=UNINTENDED_PURCHASE") && offEvent.Detail.Contains("consumption=skipped reason=switched_off"),
                $"expected the skip and its reason in the audit trail, got {offEvent}");

            // Switched on: Apple gets the purchase's delivery, consent and nothing else.
            Environment.SetEnvironmentVariable(AppStoreConsumption.SwitchVariable, "1");
            var uuid = Guid.NewGuid().ToString();
            var notification = chain.Sign(AppleConsumptionRequest(signedTransaction, uuid: uuid));
            var (status, body) = await PostAppleNotification(client, notification);
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "consumption_sent", $"expected the consumption information to be sent, got {(int)status} {body}");
            Assert(calls.Count == 1, $"expected one call to Apple, got {calls.Count}");
            var call = calls.Single();
            Assert(call.Method == HttpMethod.Put && call.Uri.ToString() == $"https://api.storekit-sandbox.apple.com/inApps/v2/transactions/consumption/{transactionId}",
                $"expected PUT to the sandbox consumption endpoint, got {call.Method} {call.Uri}");
            using (var sent = JsonDocument.Parse(call.Body))
            {
                var fields = sent.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                Assert(fields.SequenceEqual(["customerConsented", "deliveryStatus", "sampleContentProvided"]),
                    $"expected only the three fields the server knows, got {call.Body}");
                Assert(sent.RootElement.GetProperty("customerConsented").GetBoolean() &&
                    sent.RootElement.GetProperty("deliveryStatus").GetString() == "DELIVERED" &&
                    !sent.RootElement.GetProperty("sampleContentProvided").GetBoolean(),
                    $"expected consent, DELIVERED and no sample, got {call.Body}");
            }

            // The App Store Server API token: ES256 over the configured key, for this app.
            Assert(call.Authorization.StartsWith("Bearer ", StringComparison.Ordinal), "expected a bearer token");
            var token = call.Authorization["Bearer ".Length..].Split('.');
            using (var header = JsonDocument.Parse(Base64Url.DecodeFromChars(token[0])))
            using (var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(token[1])))
            {
                Assert(header.RootElement.GetProperty("alg").GetString() == "ES256" && header.RootElement.GetProperty("kid").GetString() == "TESTKEY123",
                    $"expected an ES256 token naming the key, got {header.RootElement}");
                Assert(claims.RootElement.GetProperty("iss").GetString() == "test-issuer" && claims.RootElement.GetProperty("aud").GetString() == "appstoreconnect-v1" &&
                    claims.RootElement.GetProperty("bid").GetString() == AppleBundle, $"expected the issuer, audience and bundle, got {claims.RootElement}");
            }
            Assert(apiKey.VerifyData(Encoding.ASCII.GetBytes($"{token[0]}.{token[1]}"), Base64Url.DecodeFromChars(token[2]), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation), "expected the token to be signed with the configured key");

            var sentEvent = AppleEvent(uuid);
            Assert(sentEvent.Outcome == "consumption_sent" && sentEvent.Detail.Contains("consumption=sent apple=202"), $"expected the answer in the audit trail, got {sentEvent}");
            Assert(PurchaseStatus(purchaseId) == "validated" && CountAdjustments(purchaseId) == 0 && LedgerSum(profile, "gold") == 1800,
                "expected a refund request to leave the purchase alone until Apple decides");

            // Apple delivers the same notification again: answered once.
            var (repeatStatus, repeat) = await PostAppleNotification(client, notification);
            Assert(repeatStatus == HttpStatusCode.OK && repeat.GetProperty("status").GetString() == "duplicate_event" && calls.Count == 1,
                $"expected a repeated notification not to answer Apple twice, got {repeat}");

            // A production request goes to the production API.
            var (productionStatus, production) = await PostAppleNotification(client,
                chain.Sign(AppleConsumptionRequest(chain.Sign(AppleConsumable(transactionId, environment: "Production")), environment: "Production")));
            Assert(productionStatus == HttpStatusCode.OK && production.GetProperty("status").GetString() == "consumption_sent" && calls.Count == 2 &&
                calls.Last().Uri.ToString() == $"https://api.storekit.apple.com/inApps/v2/transactions/consumption/{transactionId}",
                $"expected the production endpoint for a production request, got {production} {calls.Last().Uri}");

            // Simultaneous deliveries of one notification (several replicas): one answer.
            for (var round = 0; round < 3; round++)
            {
                calls.Clear();
                var racing = chain.Sign(AppleConsumptionRequest(signedTransaction));
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sends = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
                {
                    await gate.Task;
                    return (await PostAppleNotification(client, racing)).Body.GetProperty("status").GetString();
                })).ToArray();
                gate.SetResult();
                var statuses = await Task.WhenAll(sends);
                Assert(statuses.Count(s => s == "consumption_sent") == 1 && statuses.All(s => s is "consumption_sent" or "duplicate_event") && calls.Count == 1,
                    $"expected one answer to Apple, got {string.Join(",", statuses)} and {calls.Count} calls");
            }
        }));
    }

    private static async Task TestAppleConsumptionRequestSkipsAndFailures(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        using var apiKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Func<HttpRequestMessage, Task<HttpResponseMessage>> apple = _ => AppleAnswers(HttpStatusCode.Accepted);
        await WithAppleNotifications(chain, () => WithAppleApi(apiKey, request => apple(request), async calls =>
        {
            Environment.SetEnvironmentVariable(AppStoreConsumption.SwitchVariable, "1");
            const string profile = "TEST-CONSUMPTION-SKIPS";
            const string transactionId = "2000000900020002";
            var purchaseId = await BuyForRefund(client, profile, "gold_chest", "apple", transactionId);

            async Task Expect(string name, string signedTransactionInfo, string expectedStatus, string expectedDetail, DateTimeOffset? signedAt = null)
            {
                var uuid = Guid.NewGuid().ToString();
                var (status, body) = await PostAppleNotification(client, chain.Sign(AppleConsumptionRequest(signedTransactionInfo, uuid: uuid, signedAt: signedAt)));
                Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == expectedStatus,
                    $"expected {name} to be answered 200 {expectedStatus}, got {(int)status} {body}");
                var recorded = AppleEvent(uuid);
                Assert(recorded.Outcome == expectedStatus && recorded.Detail.Contains(expectedDetail),
                    $"expected {name} in the audit trail with '{expectedDetail}', got {recorded}");
            }

            // Nothing is sent about a transaction the server never recorded, or for another product.
            await Expect("an unknown transaction", chain.Sign(AppleConsumable("2000000900029999")),
                "unknown_transaction", "consumption=skipped reason=unknown_transaction");
            await Expect("another product", chain.Sign(AppleConsumable(transactionId, "com.crownroad.gold_treasury")),
                "product_mismatch", "consumption=skipped reason=product_mismatch");
            // Apple's 12 hours are over (the notification was delivered late).
            var signedTransaction = chain.Sign(AppleConsumable(transactionId));
            await Expect("a request older than 12 hours", signedTransaction,
                "consumption_skipped", "consumption=skipped reason=window_closed", DateTimeOffset.UtcNow.AddHours(-13));
            Assert(calls.IsEmpty, $"expected no calls to Apple for skipped requests, got {calls.Count}");

            // Apple refuses or cannot be reached: recorded, and the notification still answered 200.
            apple = _ => AppleAnswers(HttpStatusCode.InternalServerError);
            await Expect("Apple answering 500", signedTransaction, "consumption_failed", "consumption=failed apple=500");
            apple = _ => AppleAnswers(HttpStatusCode.BadRequest, """{"errorCode":4000023,"errorMessage":"Invalid request."}""");
            await Expect("Apple refusing the body", signedTransaction, "consumption_failed", "consumption=failed apple=400 errorCode=4000023");
            apple = _ => throw new HttpRequestException("Apple is down");
            await Expect("Apple unreachable", signedTransaction, "consumption_failed", "consumption=failed error=unreachable");
            apple = _ => throw new TaskCanceledException("timed out");
            await Expect("Apple timing out", signedTransaction, "consumption_failed", "consumption=failed error=timeout");
            Assert(calls.Count == 4, $"expected one call per failed attempt, got {calls.Count}");
            apple = _ => AppleAnswers(HttpStatusCode.Accepted);

            // Without App Store credentials nothing is attempted; a broken key is a failure.
            var key = Environment.GetEnvironmentVariable("APPLE_IAP_PRIVATE_KEY");
            Environment.SetEnvironmentVariable("APPLE_IAP_PRIVATE_KEY", null);
            await Expect("missing credentials", signedTransaction, "consumption_skipped", "consumption=skipped reason=not_configured");
            Environment.SetEnvironmentVariable("APPLE_IAP_PRIVATE_KEY", "not a key");
            await Expect("an unreadable key", signedTransaction, "consumption_failed", "consumption=failed error=invalid_key");
            Environment.SetEnvironmentVariable("APPLE_IAP_PRIVATE_KEY", key);
            Assert(calls.Count == 4, "expected no call without usable credentials");

            // A deleted account withdrew its consent: its kept purchase is not described.
            const string deletedProfile = "TEST-CONSUMPTION-DELETED";
            const string deletedTransactionId = "2000000900020003";
            var deletedPurchaseId = await BuyForRefund(client, deletedProfile, "gold_chest", "apple", deletedTransactionId);
            var (deleteStatus, _) = await DeleteAccount(client, deletedProfile, SessionTokens[deletedProfile]);
            Assert(deleteStatus == HttpStatusCode.OK && PurchaseStatus(deletedPurchaseId) == "validated", "expected the account deleted and its purchase kept");
            await Expect("a deleted account's purchase", chain.Sign(AppleConsumable(deletedTransactionId)), "consumption_skipped", "consumption=skipped reason=account_deleted");
            Assert(calls.Count == 4, "expected nothing sent about a deleted account's purchase");

            // A purchase already refunded (by support, say) is not described as delivered.
            Assert((await AdminRefund(client, new { purchaseId })).Body.GetProperty("status").GetString() == "refunded", "expected the operator refund");
            await Expect("a refunded purchase", signedTransaction, "consumption_skipped", "consumption=skipped reason=purchase_refunded");
            Assert(calls.Count == 4, "expected nothing sent about a refunded purchase");

            // Failures never touched the purchase beyond the operator's refund.
            Assert(CountAdjustments(purchaseId) == 1 && PurchaseStatus(purchaseId) == "refunded", "expected only the operator refund to change the purchase");
        }));
    }

    // ── Device adjustments ──────────────────────────────────

    private static async Task TestRefundLargerThanBalance(HttpClient client)
    {
        const string profile = "TEST-REFUND-SPENT";
        var purchaseId = await BuyForRefund(client, profile, "campaign_resupply", "google", "GPA.0000-0000-0000-00001");
        var refund = await AdminRefund(client, new { purchaseId, note = "chargeback" });
        Assert(refund.Status == HttpStatusCode.OK && refund.Body.GetProperty("status").GetString() == "refunded", $"expected the refund, got {refund.Body}");
        var adjustment = (await Adjustments(client, profile)).Single();
        Assert(adjustment.GetProperty("goldDelta").GetInt32() == -2500 && adjustment.GetProperty("foodDelta").GetInt32() == -80,
            "expected the adjustment to owe the purchase's full grant, whatever the device holds");
        var adjustmentId = adjustment.GetProperty("adjustmentId").GetString()!;

        // The player spent most of it: the device could take back only 300 gold and 5 food.
        var ack = await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, goldApplied = -300, foodApplied = -5 }, profile);
        var root = ack.RootElement;
        Assert(root.GetProperty("status").GetString() == "ok" && root.GetProperty("goldUnrecovered").GetInt32() == 2200 && root.GetProperty("foodUnrecovered").GetInt32() == 75,
            $"expected the shortfall to be recorded, got {root}");
        Assert(Database.ToInt64(RefundScalar("SELECT gold_applied FROM wallet_adjustments WHERE adjustment_id = @id", ("@id", adjustmentId))) == -300,
            "expected what the device took back in the audit trail");
        Assert(LedgerSum(profile, "gold") == 0 && LedgerSum(profile, "food") == 0, "expected the ledger to reverse the full grant regardless");

        // The first acknowledgement stands; a later one (another device, a restored save) changes nothing.
        var later = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, goldApplied = -2500, foodApplied = -80 }, profile)).RootElement;
        Assert(later.GetProperty("status").GetString() == "already_acknowledged" && later.GetProperty("goldUnrecovered").GetInt32() == 2200,
            "expected the first acknowledgement to be kept");
        Assert((await Adjustments(client, profile)).Single().GetProperty("acknowledged").GetBoolean(), "expected the adjustment to show as acknowledged");

        // A device cannot claim to have taken back more than owed, or to have added currency.
        var second = await AdminRefund(client, new { purchaseId = await BuyForRefund(client, profile, "gold_pouch", "google", "GPA.0000-0000-0000-00002") });
        var clamped = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = second.Body.GetProperty("adjustmentId").GetString(), goldApplied = -9999, foodApplied = 40 }, profile)).RootElement;
        Assert(clamped.GetProperty("goldApplied").GetInt32() == -400 && clamped.GetProperty("foodApplied").GetInt32() == 0 && clamped.GetProperty("goldUnrecovered").GetInt32() == 0,
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

    private static async Task TestSupportLookup(HttpClient client)
    {
        const string profile = "TEST-SUPPORT-LOOKUP";
        var purchaseId = await BuyForRefund(client, profile, "gold_pouch", "stripe", "cs_test_support_lookup", "receipt-secret-support-lookup");

        async Task<(HttpStatusCode Status, string Text)> Lookup(string query, bool authorized = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/support/player?" + query);
            if (authorized) request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        Assert((await Lookup($"profileId={profile}", authorized: false)).Status == HttpStatusCode.Unauthorized, "expected the lookup to need the admin key");
        Assert((await Lookup("")).Status == HttpStatusCode.BadRequest, "expected a lookup with no player or transaction to be refused");
        Assert((await Lookup("transactionId=GPA.NOPE")).Status == HttpStatusCode.NotFound, "expected an unknown transaction to be reported");

        var byProfile = await Lookup($"profileId={profile}");
        var body = JsonDocument.Parse(byProfile.Text).RootElement;
        var purchase = body.GetProperty("purchases").EnumerateArray().Single();
        Assert(byProfile.Status == HttpStatusCode.OK && purchase.GetProperty("purchaseId").GetString() == purchaseId &&
            purchase.GetProperty("transactionId").GetString() == "cs_test_support_lookup" && purchase.GetProperty("status").GetString() == "validated",
            "expected the lookup to list the player's purchase");
        Assert(body.GetProperty("wallet").GetProperty("gold").GetInt64() == LedgerSum(profile, "gold") &&
            body.GetProperty("ledger").EnumerateArray().Any(entry => entry.GetProperty("purchaseId").GetString() == purchaseId),
            "expected the lookup to show the wallet and the ledger entry behind it");
        Assert(body.GetProperty("activeSessions").GetInt64() >= 1, "expected the lookup to count the player's live sessions");
        Assert(!byProfile.Text.Contains("receipt-secret-support-lookup") && !byProfile.Text.Contains("token_hash") && !byProfile.Text.Contains("credential"),
            "expected the lookup to leave out receipts, session hashes and sign-in credentials");

        var byTransaction = await Lookup("transactionId=cs_test_support_lookup");
        Assert(byTransaction.Status == HttpStatusCode.OK &&
            JsonDocument.Parse(byTransaction.Text).RootElement.GetProperty("profileId").GetString() == profile,
            "expected a store transaction ID to find the player");

        // An emailed deletion request: the same erase as the in-app button, with the ID typed twice.
        async Task<(HttpStatusCode Status, string Text)> Delete(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/support/delete-account")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        Assert((await Delete(new { profileId = profile, confirmProfileId = profile + "X" })).Status == HttpStatusCode.BadRequest,
            "expected a mismatched confirmation to delete nothing");
        Assert((await Delete(new { profileId = "CVY-NOBODY", confirmProfileId = "CVY-NOBODY" })).Status == HttpStatusCode.NotFound,
            "expected an unknown player to be reported");
        var deleted = await Delete(new { profileId = profile, confirmProfileId = profile });
        Assert(deleted.Status == HttpStatusCode.OK && deleted.Text.Contains("\"deleted\""), "expected the operator to delete the account");
        var afterDelete = JsonDocument.Parse((await Lookup("transactionId=cs_test_support_lookup")).Text).RootElement;
        Assert(afterDelete.GetProperty("profileId").GetString()!.StartsWith("DELETED-") && afterDelete.GetProperty("player").ValueKind == JsonValueKind.Null,
            "expected the purchase to stay under a pseudonym with no player behind it");
    }

    private static async Task TestSupportGrant(HttpClient client)
    {
        const string profile = "TEST-SUPPORT-GRANT";
        await BuyForRefund(client, profile, "gold_pouch", "stripe", "cs_test_support_grant");
        var gold = await WalletGold(client, profile);

        async Task<(HttpStatusCode Status, string Text)> Grant(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/support/grant")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        Assert((await Grant(new { profileId = profile, gold = 300 })).Status == HttpStatusCode.BadRequest, "expected a grant without a note to be refused");
        Assert((await Grant(new { profileId = profile, gold = 0, food = 0, note = "nothing" })).Status == HttpStatusCode.BadRequest, "expected an empty grant to be refused");
        Assert((await Grant(new { profileId = profile, gold = -5, note = "negative" })).Status == HttpStatusCode.BadRequest, "expected a negative grant to be refused");
        Assert((await Grant(new { profileId = "CVY-NOBODY", gold = 300, note = "ticket 7" })).Status == HttpStatusCode.NotFound, "expected an unknown player to be reported");

        var granted = await Grant(new { profileId = profile, gold = 300, food = 5, note = "ticket 7: pouch never arrived" });
        Assert(granted.Status == HttpStatusCode.OK && granted.Text.Contains("\"granted\""), "expected the operator to make good a lost pack");
        Assert(await WalletGold(client, profile) == gold + 300, "expected the server wallet to hold the grant");
        var adjustment = (await Adjustments(client, profile)).Single(a => a.GetProperty("kind").GetString() == "grant");
        Assert(adjustment.GetProperty("goldDelta").GetInt32() == 300 && adjustment.GetProperty("foodDelta").GetInt32() == 5 &&
            !adjustment.GetProperty("acknowledged").GetBoolean(), "expected the device to be offered the grant, once, to apply");
        Assert(LedgerSum(profile, "gold") == gold + 300, "expected the ledger to record the grant");
    }

    private static async Task TestPurchasePause(HttpClient client)
    {
        async Task<(HttpStatusCode Status, string Text)> Admin(HttpMethod method, object? body = null, bool authorized = true)
        {
            using var request = new HttpRequestMessage(method, "/admin/purchases");
            if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            if (authorized) request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        try
        {
            Assert((await Admin(HttpMethod.Get, authorized: false)).Status == HttpStatusCode.Unauthorized, "expected the pause switch to need the admin key");
            Assert((await Admin(HttpMethod.Post, new { message = "no flag" })).Status == HttpStatusCode.BadRequest, "expected a body without paused to change nothing");
            var paused = await Admin(HttpMethod.Post, new { paused = true, message = "Back by 10:00 UTC." });
            Assert(paused.Status == HttpStatusCode.OK && paused.Text.Contains("\"paused\":true"), "expected the operator to pause purchases");

            var products = (await Get(client, "/purchase/products")).RootElement;
            Assert(products.GetProperty("purchasesPaused").GetBoolean() && products.GetProperty("purchasesMessage").GetString() == "Back by 10:00 UTC.",
                "expected the game to be told purchases are paused, and why");
            // A purchase the store has already charged is still delivered.
            await BuyForRefund(client, "TEST-PAUSE-PAID", "gold_pouch", "stripe", "cs_test_paid_during_pause");
        }
        finally
        {
            Assert((await Admin(HttpMethod.Post, new { paused = false })).Status == HttpStatusCode.OK, "expected the operator to resume purchases");
        }
        var resumed = (await Get(client, "/purchase/products")).RootElement;
        Assert(!resumed.GetProperty("purchasesPaused").GetBoolean() && resumed.GetProperty("purchasesMessage").GetString() == "", "expected purchases to resume");
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
            Assert(LedgerSum(profile, "gold") == 400 && LedgerSum(profile, "food") == 0, "expected the ledger to reverse the refunded grants");
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

    // ── Relic shards ────────────────────────────────────────

    private static async Task TestShardRefund(HttpClient client)
    {
        const string profile = "TEST-REFUND-SHARDS";
        var purchaseId = await BuyForRefund(client, profile, "shard_pouch", "google", "GPA.0000-0000-0000-50001");
        Assert(await WalletShards(client, profile) == 10, "expected the purchase to credit 10 shards to the server wallet");

        var refund = await AdminRefund(client, new { purchaseId, note = "chargeback" });
        Assert(refund.Status == HttpStatusCode.OK && refund.Body.GetProperty("status").GetString() == "refunded", $"expected the refund, got {refund.Body}");
        var adjustmentId = refund.Body.GetProperty("adjustmentId").GetString()!;
        var adjustment = (await Adjustments(client, profile)).Single();
        Assert(adjustment.GetProperty("adjustmentId").GetString() == adjustmentId && adjustment.GetProperty("kind").GetString() == "refund" &&
            adjustment.GetProperty("shardsDelta").GetInt32() == -10 && adjustment.GetProperty("goldDelta").GetInt32() == 0 &&
            adjustment.GetProperty("foodDelta").GetInt32() == 0 && !adjustment.GetProperty("acknowledged").GetBoolean(),
            $"expected a -10 shards refund adjustment, got {adjustment}");
        Assert(PurchaseStatus(purchaseId) == "refunded" && LedgerSum(profile, "shards") == 0 && LedgerSum(profile, "gold") == 0,
            "expected a reversing shards ledger entry that cancels the credit");
        Assert(Database.ToInt64(RefundScalar("SELECT COUNT(*) FROM wallet_ledger WHERE purchase_id = @id AND currency_type = 'shards' AND reason = 'refund' AND amount = -10 AND adjustment_id = @adj",
            ("@id", purchaseId), ("@adj", adjustmentId))) == 1, "expected one refund ledger entry of -10 shards under the adjustment");
        Assert(await WalletShards(client, profile) == 0, "expected the server wallet to give the shards back");

        // The player spent 6 of them: the device could take back only 4.
        var ack = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, shardsApplied = -4 }, profile)).RootElement;
        Assert(ack.GetProperty("status").GetString() == "ok" && ack.GetProperty("shardsApplied").GetInt32() == -4 &&
            ack.GetProperty("shardsUnrecovered").GetInt32() == 6 && ack.GetProperty("goldUnrecovered").GetInt32() == 0 &&
            ack.GetProperty("foodUnrecovered").GetInt32() == 0, $"expected the 6-shard shortfall to be recorded, got {ack}");
        Assert(Database.ToInt64(RefundScalar("SELECT shards_applied FROM wallet_adjustments WHERE adjustment_id = @id", ("@id", adjustmentId))) == -4,
            "expected what the device took back in the audit trail");
        var later = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId, shardsApplied = -10 }, profile)).RootElement;
        Assert(later.GetProperty("status").GetString() == "already_acknowledged" && later.GetProperty("shardsApplied").GetInt32() == -4 &&
            later.GetProperty("shardsUnrecovered").GetInt32() == 6, $"expected the first acknowledgement to be kept, got {later}");

        var audit = (await GetAdmin(client, "/admin/refunds")).RootElement.GetProperty("events").EnumerateArray()
            .Single(e => e.GetProperty("adjustmentId").GetString() == adjustmentId);
        Assert(audit.GetProperty("shardsDelta").GetInt32() == -10 && audit.GetProperty("shardsUnrecovered").GetInt32() == 6 && audit.GetProperty("acknowledged").GetBoolean(),
            $"expected the admin list to show the shards taken back and the shortfall, got {audit}");

        var lookup = (await GetAdmin(client, $"/admin/support/player?profileId={profile}")).RootElement;
        var bought = lookup.GetProperty("purchases").EnumerateArray().Single(p => p.GetProperty("purchaseId").GetString() == purchaseId);
        var adjusted = lookup.GetProperty("adjustments").EnumerateArray().Single(a => a.GetProperty("adjustmentId").GetString() == adjustmentId);
        Assert(bought.GetProperty("shardsCredited").GetInt64() == 10 && adjusted.GetProperty("shardsDelta").GetInt64() == -10 &&
            adjusted.GetProperty("shardsApplied").GetInt64() == -4 && lookup.GetProperty("wallet").GetProperty("shards").GetInt64() == 0 &&
            lookup.GetProperty("ledger").EnumerateArray().Count(e => e.GetProperty("currency").GetString() == "shards") == 2,
            $"expected the support lookup to show the shards bought, refunded and taken back, got {lookup}");

        // The refund is reversed: the shards come back once, and a device cannot claim more than that.
        var reinstated = await AdminRefund(client, new { purchaseId, action = "reinstate" });
        Assert(reinstated.Body.GetProperty("status").GetString() == "reinstated", $"expected the reinstatement, got {reinstated.Body}");
        var reversal = (await Adjustments(client, profile)).Last();
        Assert(reversal.GetProperty("kind").GetString() == "refund_reversed" && reversal.GetProperty("shardsDelta").GetInt32() == 10 &&
            reversal.GetProperty("reversesAdjustmentId").GetString() == adjustmentId, $"expected a +10 shards reversal naming the refund, got {reversal}");
        Assert(LedgerSum(profile, "shards") == 10 && await WalletShards(client, profile) == 10, "expected the reversal to restore the ledger and wallet");
        var reversalAck = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = reversal.GetProperty("adjustmentId").GetString(), shardsApplied = 40 }, profile)).RootElement;
        Assert(reversalAck.GetProperty("shardsApplied").GetInt32() == 10 && reversalAck.GetProperty("shardsUnrecovered").GetInt32() == 0,
            $"expected a reversal acknowledgement to be clamped to what it gives back, got {reversalAck}");

        // A device cannot claim to have taken back more than owed, or to have added shards.
        var second = await AdminRefund(client, new { purchaseId = await BuyForRefund(client, profile, "shard_casket", "google", "GPA.0000-0000-0000-50002") });
        var secondId = second.Body.GetProperty("adjustmentId").GetString();
        var clamped = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = secondId, shardsApplied = -9999 }, profile)).RootElement;
        Assert(clamped.GetProperty("shardsApplied").GetInt32() == -45 && clamped.GetProperty("shardsUnrecovered").GetInt32() == 0,
            $"expected an out-of-range acknowledgement to be clamped, got {clamped}");
        var third = await AdminRefund(client, new { purchaseId = await BuyForRefund(client, profile, "shard_coffer", "google", "GPA.0000-0000-0000-50003") });
        var added = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = third.Body.GetProperty("adjustmentId").GetString(), shardsApplied = 40 }, profile)).RootElement;
        Assert(added.GetProperty("shardsApplied").GetInt32() == 0 && added.GetProperty("shardsUnrecovered").GetInt32() == 120,
            $"expected a refund acknowledgement that adds shards to apply nothing, got {added}");
    }

    private static async Task TestShardRefundAppleNotification(HttpClient client)
    {
        using var chain = AppleTestChain.Create();
        await WithAppleNotifications(chain, async () =>
        {
            const string profile = "TEST-REFUND-SHARDS-APPLE";
            const string transactionId = "2000000900050001";
            var purchaseId = await BuyForRefund(client, profile, "shard_casket", "apple", transactionId);

            // A refund naming another shard pack is not applied to this one.
            var (mismatchStatus, mismatch) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId, "com.crownroad.shard_pouch")))));
            Assert(mismatchStatus == HttpStatusCode.OK && mismatch.GetProperty("status").GetString() == "product_mismatch" && PurchaseStatus(purchaseId) == "validated",
                $"expected a product mismatch to be recorded without a refund, got {mismatch}");

            var (status, body) = await PostAppleNotification(client, chain.Sign(AppleNotification("REFUND", chain.Sign(AppleTransaction(transactionId, "com.crownroad.shard_casket")))));
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "refunded", $"expected a signed REFUND of the casket to refund it, got {(int)status} {body}");
            var adjustment = (await Adjustments(client, profile)).Single();
            Assert(adjustment.GetProperty("shardsDelta").GetInt32() == -45 && adjustment.GetProperty("goldDelta").GetInt32() == 0,
                $"expected a -45 shards adjustment, got {adjustment}");
            Assert(LedgerSum(profile, "shards") == 0 && await WalletShards(client, profile) == 0, "expected the ledger and wallet to reverse the casket");

            // The store hands the refunded transaction back: nothing is granted again.
            var revalidated = (await Post(client, "/purchase/validate", new { profileId = profile, productId = "shard_casket", platform = "apple", receiptToken = $"receipt-{transactionId}", transactionId }, profile)).RootElement;
            Assert(revalidated.GetProperty("status").GetString() == "refunded" && !revalidated.TryGetProperty("shardsCredited", out _) && await WalletShards(client, profile) == 0,
                $"expected a refunded shard pack never to be handed back as a grant, got {revalidated}");
        });
    }

    private static async Task TestSupportGrantShards(HttpClient client)
    {
        const string profile = "TEST-SUPPORT-GRANT-SHARDS";
        await EnsureSession(client, profile);

        async Task<(HttpStatusCode Status, JsonElement Body)> Grant(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/support/grant")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Admin-Api-Key", TestAdminApiKey);
            using var response = await client.SendAsync(request);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync() is { Length: > 0 } text ? text : "{}");
            return (response.StatusCode, json.RootElement.Clone());
        }

        Assert((await Grant(new { profileId = profile, shards = -1, note = "negative" })).Status == HttpStatusCode.BadRequest, "expected a negative shard grant to be refused");
        Assert((await Grant(new { profileId = profile, shards = 1001, note = "typo" })).Status == HttpStatusCode.BadRequest, "expected a shard grant over the cap to be refused");

        var granted = await Grant(new { profileId = profile, shards = 45, note = "ticket 9: casket never arrived" });
        Assert(granted.Status == HttpStatusCode.OK && granted.Body.GetProperty("status").GetString() == "granted" && granted.Body.GetProperty("shards").GetInt64() == 45,
            $"expected the operator to make good a lost shard pack, got {granted.Body}");
        var adjustment = (await Adjustments(client, profile)).Single(a => a.GetProperty("kind").GetString() == "grant");
        Assert(adjustment.GetProperty("shardsDelta").GetInt32() == 45 && adjustment.GetProperty("goldDelta").GetInt32() == 0 && !adjustment.GetProperty("acknowledged").GetBoolean(),
            $"expected the device to be offered the shards, once, to apply, got {adjustment}");
        Assert(LedgerSum(profile, "shards") == 45 && await WalletShards(client, profile) == 45, "expected the ledger and server wallet to hold the grant");
        var ack = (await Post(client, "/wallet/adjustments/ack", new { profileId = profile, adjustmentId = adjustment.GetProperty("adjustmentId").GetString(), shardsApplied = 45 }, profile)).RootElement;
        Assert(ack.GetProperty("status").GetString() == "ok" && ack.GetProperty("shardsApplied").GetInt32() == 45 && ack.GetProperty("shardsUnrecovered").GetInt32() == 0,
            $"expected the device to report the whole grant applied, got {ack}");
    }

    // ── Schema ──────────────────────────────────────────────

    private static Task TestShardsMigration()
    {
        using var conn = Database.Open();
        void Execute(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // Tables from before schema 10, when nothing held shards, with a purchase, wallet and refund in them.
        foreach (var (table, column) in new[] { ("purchases", "shards_credited"), ("player_wallets", "shards_balance"),
                     ("wallet_adjustments", "shards_delta"), ("wallet_adjustments", "shards_applied") })
            Execute($"ALTER TABLE {table} DROP COLUMN {column}");
        Execute("INSERT INTO players (profile_id) VALUES ('LEGACY-SHARDS') ON CONFLICT (profile_id) DO NOTHING");
        Execute("INSERT INTO purchases (purchase_id, profile_id, product_id, transaction_id, gold_credited) VALUES ('PUR-LEGACY-SHARDS', 'LEGACY-SHARDS', 'gold_pouch', 'txn-legacy-shards', 500)");
        Execute("INSERT INTO player_wallets (profile_id, gold_balance, updated_at) VALUES ('LEGACY-SHARDS', 500, 1)");
        Execute("INSERT INTO wallet_adjustments (adjustment_id, profile_id, purchase_id, kind, gold_delta, gold_applied, acknowledged_at) VALUES ('ADJ-LEGACY-SHARDS', 'LEGACY-SHARDS', 'PUR-LEGACY-SHARDS', 'refund', -500, -200, 1)");
        Execute("DELETE FROM schema_version WHERE version >= 10");
        Database.Initialize();

        long Scalar(string sql) => Database.ToInt64(RefundScalar(sql), -1);
        Assert(Scalar("SELECT MAX(version) FROM schema_version") == 10, "expected schema version 10");
        Assert(Scalar("SELECT shards_credited FROM purchases WHERE purchase_id = 'PUR-LEGACY-SHARDS'") == 0 &&
            Scalar("SELECT gold_credited FROM purchases WHERE purchase_id = 'PUR-LEGACY-SHARDS'") == 500, "expected an earlier purchase to keep its gold and credit no shards");
        Assert(Scalar("SELECT shards_balance FROM player_wallets WHERE profile_id = 'LEGACY-SHARDS'") == 0 &&
            Scalar("SELECT gold_balance FROM player_wallets WHERE profile_id = 'LEGACY-SHARDS'") == 500, "expected an earlier wallet to keep its gold and hold no shards");
        Assert(Scalar("SELECT shards_delta FROM wallet_adjustments WHERE adjustment_id = 'ADJ-LEGACY-SHARDS'") == 0 &&
            Scalar("SELECT shards_applied FROM wallet_adjustments WHERE adjustment_id = 'ADJ-LEGACY-SHARDS'") == 0 &&
            Scalar("SELECT gold_applied FROM wallet_adjustments WHERE adjustment_id = 'ADJ-LEGACY-SHARDS'") == -200, "expected an earlier adjustment to keep its acknowledgement and owe no shards");

        // A step stopped after adding its columns but before recording its version runs again cleanly.
        Execute("DELETE FROM schema_version WHERE version >= 10");
        Database.Initialize();
        Database.Initialize();
        Assert(Scalar("SELECT COUNT(*) FROM schema_version WHERE version = 10") == 1, "expected schema 10 recorded once");
        Execute("UPDATE player_wallets SET shards_balance = shards_balance + 12 WHERE profile_id = 'LEGACY-SHARDS'");
        Assert(Scalar("SELECT shards_balance FROM player_wallets WHERE profile_id = 'LEGACY-SHARDS'") == 12, "expected the new column to hold shards");
        return Task.CompletedTask;
    }


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
