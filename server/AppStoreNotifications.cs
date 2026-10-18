using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// App Store Server Notifications v2 (POST /store/apple/notifications). Apple
/// pushes these, so unlike StorePurchaseVerification, which only decodes what it
/// fetched from Apple itself, every payload here is checked before it is
/// believed: the x5c chain must lead to Apple Root CA - G3 (pinned below, not
/// taken from the payload), carry Apple's marker extensions, and its leaf must
/// have made the ES256 signature. The transaction inside is a second signed
/// payload, checked the same way.
///
/// REFUND takes a consumable's currency back; REFUND_REVERSED gives it back.
/// CONSUMPTION_REQUEST (a player asked Apple for a refund) is answered by
/// AppStoreConsumption when that is switched on. REVOKE only concerns Family
/// Sharing, which consumables don't use, so it is recorded and ignored like
/// every other type.
/// </summary>
public static class AppStoreNotifications
{
    /// <summary>Tests sign with their own root instead of Apple's.</summary>
    internal static X509Certificate2? TestRootCertificate;

    // Apple Root CA - G3, from https://www.apple.com/certificateauthority/AppleRootCA-G3.cer.
    // SHA-256 63:34:3A:BF:B8:9A:6A:03:EB:B5:7E:9B:3F:5F:A7:BE:7C:4F:5C:75:6F:30:17:B3:A8:C4:88:C3:65:3E:91:79
    private const string AppleRootCaG3 =
        "MIICQzCCAcmgAwIBAgIILcX8iNLFS5UwCgYIKoZIzj0EAwMwZzEbMBkGA1UEAwwSQXBwbGUgUm9vdCBDQSAtIEczMSYwJAYDVQQL" +
        "DB1BcHBsZSBDZXJ0aWZpY2F0aW9uIEF1dGhvcml0eTETMBEGA1UECgwKQXBwbGUgSW5jLjELMAkGA1UEBhMCVVMwHhcNMTQwNDMw" +
        "MTgxOTA2WhcNMzkwNDMwMTgxOTA2WjBnMRswGQYDVQQDDBJBcHBsZSBSb290IENBIC0gRzMxJjAkBgNVBAsMHUFwcGxlIENlcnRp" +
        "ZmljYXRpb24gQXV0aG9yaXR5MRMwEQYDVQQKDApBcHBsZSBJbmMuMQswCQYDVQQGEwJVUzB2MBAGByqGSM49AgEGBSuBBAAiA2IA" +
        "BJjpLz1AcqTtkyJygRMc3RCV8cWjTnHcFBbZDuWmBSp3ZHtfTjjTuxxEtX/1H7YyYl3J6YRbTzBPEVoA/VhYDKX1DyxNB0cTddqX" +
        "l5dvMVztK517IDvYuVTZXpmkOlEKMaNCMEAwHQYDVR0OBBYEFLuw3qFYM4iapIqZ3r6966/ayySrMA8GA1UdEwEB/wQFMAMBAf8w" +
        "DgYDVR0PAQH/BAQDAgEGMAoGCCqGSM49BAMDA2gAMGUCMQCD6cHEFl4aXTQY2e3v9GwOAEZLuN+yRhHFD/3meoyhpmvOwgPUnPWT" +
        "xnS4at+qIxUCMG1mihDK1A3UT82NQz60imOlM27jbdoXt2QfyFMm+YhidDkLF1vLUagM6BgD56KyKA==";

    private static readonly X509Certificate2 AppleRoot = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(AppleRootCaG3));

    // Apple's App Store Server Library requires these on the signing and intermediate certificates.
    internal const string LeafMarkerOid = "1.2.840.113635.100.6.11.1";
    internal const string IntermediateMarkerOid = "1.2.840.113635.100.6.2.1";
    private const int MaxBodyBytes = 64 * 1024;

    public static async Task<IResult> Receive(HttpRequest request)
    {
        var bundleId = Environment.GetEnvironmentVariable("APPLE_IAP_BUNDLE_ID")?.Trim() ?? "";
        if (bundleId.Length == 0)
        {
            // Apple retries a failed delivery for days, so a server that cannot
            // check the bundle yet should not answer that it handled anything.
            return Results.Json(new { error = "app_store_notifications_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        JsonElement notification, transaction = default;
        try
        {
            var signedPayload = await ReadSignedPayload(request);
            notification = VerifySignedData(signedPayload);
            var data = notification.ValueKind == JsonValueKind.Object && notification.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
            if (data.ValueKind == JsonValueKind.Object)
            {
                if (Text(data, "bundleId") != bundleId)
                    return Rejected("The notification is for another app.");
                var appAppleId = Environment.GetEnvironmentVariable("APPLE_IAP_APP_APPLE_ID")?.Trim() ?? "";
                if (appAppleId.Length > 0 && Text(data, "environment") == "Production" &&
                    !(data.TryGetProperty("appAppleId", out var id) && id.ToString() == appAppleId))
                    return Rejected("The notification is for another app.");
                var signedTransaction = Text(data, "signedTransactionInfo");
                if (signedTransaction.Length > 0)
                {
                    transaction = VerifySignedData(signedTransaction);
                    if (Text(transaction, "bundleId") != bundleId)
                        return Rejected("The transaction is for another app.");
                }
            }
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return Rejected("The signed payload is not valid.");
        }

        var type = Text(notification, "notificationType");
        var subtype = Text(notification, "subtype");
        var uuid = Text(notification, "notificationUUID");
        if (uuid.Length == 0) return Rejected("The notification has no UUID.");
        var payloadData = notification.ValueKind == JsonValueKind.Object && notification.TryGetProperty("data", out var dataElement) ? dataElement : default;
        var environment = Text(payloadData, "environment");
        var transactionId = transaction.ValueKind == JsonValueKind.Object ? Text(transaction, "transactionId") : "";

        var action = type switch
        {
            "REFUND" when transactionId.Length > 0 => PurchaseRefunds.Action.Refund,
            "REFUND_REVERSED" when transactionId.Length > 0 => PurchaseRefunds.Action.Reinstate,
            "CONSUMPTION_REQUEST" when transactionId.Length > 0 => PurchaseRefunds.Action.Inspect,
            _ => PurchaseRefunds.Action.Ignore
        };
        var requestReason = Text(payloadData, "consumptionRequestReason");
        var detail = string.Join(' ', new[]
        {
            subtype.Length > 0 ? $"subtype={subtype}" : "",
            requestReason.Length > 0 ? $"consumptionRequestReason={requestReason}" : "",
            transaction.ValueKind == JsonValueKind.Object && transaction.TryGetProperty("revocationReason", out var reason) ? $"revocationReason={reason}" : "",
            transaction.ValueKind == JsonValueKind.Object ? $"type={Text(transaction, "type")}" : ""
        }.Where(part => part.Length > 0));

        var outcome = PurchaseRefunds.Record(new PurchaseRefunds.StoreSignal(
            "apple", $"apple:{uuid}", type,
            Platform: "apple",
            TransactionId: transactionId,
            StoreProductId: transaction.ValueKind == JsonValueKind.Object ? Text(transaction, "productId") : "",
            Environment: environment,
            Detail: detail), action);

        // A repeated delivery was answered (or skipped) the first time.
        if (action == PurchaseRefunds.Action.Inspect && outcome.Status != "duplicate_event")
        {
            var now = DateTimeOffset.UtcNow;
            var signedAt = notification.TryGetProperty("signedDate", out var signedDate) && signedDate.TryGetInt64(out var millis)
                ? DateTimeOffset.FromUnixTimeMilliseconds(millis)
                : now;
            outcome = outcome with { Status = await AppStoreConsumption.RespondAsync(outcome, transactionId, environment, signedAt, now) };
        }

        // Anything that passed the signature check is answered 200, including
        // types the game ignores, transactions it never recorded and consumption
        // answers Apple refused, so Apple stops retrying. Only an unexpected
        // failure (500) asks for a retry.
        return Results.Ok(new { status = outcome.Status });
    }

    /// <summary>
    /// Operators check the App Store Connect setup here (POST /admin/refunds/apple-test, optionally
    /// ?environment=sandbox): Apple sends a TEST notification, which then shows in /admin/refunds.
    /// </summary>
    public static async Task<IResult> AdminRequestTest(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        var sandbox = string.Equals(request.Query["environment"].FirstOrDefault(), "sandbox", StringComparison.OrdinalIgnoreCase);
        try
        {
            var token = await StorePurchaseVerification.RequestAppleTestNotificationAsync(sandbox);
            return Results.Ok(new { status = "ok", environment = sandbox ? "Sandbox" : "Production", testNotificationToken = token });
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException or CryptographicException)
        {
            return Results.Json(new { status = "error", message = e.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<string> ReadSignedPayload(HttpRequest request)
    {
        if (request.ContentLength > MaxBodyBytes) throw new InvalidOperationException("Notification too large.");
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes) throw new InvalidOperationException("Notification too large.");
            buffer.Write(chunk, 0, read);
        }
        using var body = JsonDocument.Parse(buffer.ToArray());
        var signedPayload = Text(body.RootElement, "signedPayload");
        return signedPayload.Length > 0 ? signedPayload : throw new JsonException("Missing signedPayload.");
    }

    /// <summary>
    /// Verifies an App Store JWS (a notification or a transaction) and returns its payload.
    /// Throws CryptographicException when the chain or the signature is not Apple's.
    /// </summary>
    internal static JsonElement VerifySignedData(string jws)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3) throw new CryptographicException("Malformed signed data.");
        using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
        if (Text(header.RootElement, "alg") != "ES256") throw new CryptographicException("Unexpected signing algorithm.");
        if (!header.RootElement.TryGetProperty("x5c", out var x5c) || x5c.ValueKind != JsonValueKind.Array || x5c.GetArrayLength() != 3)
            throw new CryptographicException("Expected a three-certificate chain.");

        var certificates = x5c.EnumerateArray()
            .Select(entry => X509CertificateLoader.LoadCertificate(Convert.FromBase64String(entry.GetString() ?? "")))
            .ToArray();
        try
        {
            var (leaf, intermediate, root) = (certificates[0], certificates[1], certificates[2]);
            var trusted = TestRootCertificate ?? AppleRoot;
            if (!root.RawDataMemory.Span.SequenceEqual(trusted.RawDataMemory.Span))
                throw new CryptographicException("The chain does not end at Apple's root.");

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(trusted);
            chain.ChainPolicy.ExtraStore.Add(intermediate);
            // Apple's library can also ask Apple's OCSP responder; this server
            // relies on the chain, the markers and the signature (docs/DEPLOYMENT.md).
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            // Never fetch certificates a payload points at: the chain is the three it carries.
            chain.ChainPolicy.DisableCertificateDownloads = true;
            if (!chain.Build(leaf))
                throw new CryptographicException("Untrusted certificate chain: " +
                    string.Join(", ", chain.ChainStatus.Select(status => status.Status)));
            if (chain.ChainElements.Count != 3 ||
                !chain.ChainElements[1].Certificate.RawDataMemory.Span.SequenceEqual(intermediate.RawDataMemory.Span) ||
                !chain.ChainElements[2].Certificate.RawDataMemory.Span.SequenceEqual(trusted.RawDataMemory.Span))
                throw new CryptographicException("Unexpected certificate chain.");
            if (leaf.Extensions[LeafMarkerOid] == null || intermediate.Extensions[IntermediateMarkerOid] == null)
                throw new CryptographicException("The certificates are not App Store signing certificates.");

            using var key = leaf.GetECDsaPublicKey() ?? throw new CryptographicException("The signing certificate has no ECDSA key.");
            var signature = Base64UrlDecode(parts[2]);
            if (signature.Length != 64 ||
                !key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new CryptographicException("Signature mismatch.");

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            return payload.RootElement.Clone();
        }
        finally
        {
            foreach (var certificate in certificates) certificate.Dispose();
        }
    }

    private static IResult Rejected(string message) =>
        Results.Json(new { error = "invalid_notification", message }, statusCode: StatusCodes.Status400BadRequest);

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
