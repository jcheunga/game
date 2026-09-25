using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CrownroadServer;

/// <summary>
/// Verifies a paid transaction directly with its store. A client receipt is
/// only an opaque lookup token; it is never proof of payment on its own.
/// </summary>
public sealed record StorePurchaseVerificationResult(
    bool Verified,
    bool IsConfigurationError,
    string Message,
    string CanonicalTransactionId,
    string ReceiptFingerprint)
{
    public static StorePurchaseVerificationResult Rejected(string message) =>
        new(false, false, message, "", "");

    public static StorePurchaseVerificationResult Unavailable(string message) =>
        new(false, true, message, "", "");
}

public static class StorePurchaseVerification
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static StorePurchaseVerificationResult Development(string receiptToken, string transactionId) =>
        new(
            true,
            false,
            "Development purchase claim accepted.",
            string.IsNullOrWhiteSpace(transactionId) ? ReceiptFingerprint(receiptToken) : transactionId.Trim(),
            ReceiptFingerprint(receiptToken));

    public static Task<StorePurchaseVerificationResult> VerifyAsync(
        string platform,
        string internalProductId,
        string receiptToken,
        string transactionId,
        string profileId)
    {
        return platform.Trim().ToLowerInvariant() switch
        {
            "google" => VerifyGoogleAsync(internalProductId, receiptToken, transactionId, profileId),
            "apple" => VerifyAppleAsync(internalProductId, receiptToken, transactionId, profileId),
            _ => Task.FromResult(StorePurchaseVerificationResult.Rejected("Unsupported native store platform."))
        };
    }

    private static async Task<StorePurchaseVerificationResult> VerifyGoogleAsync(
        string internalProductId,
        string purchaseToken,
        string submittedTransactionId,
        string profileId)
    {
        if (!ProductStoreIds.TryGetValue(internalProductId, out var product))
            return StorePurchaseVerificationResult.Rejected("Unknown product.");

        var packageName = Environment.GetEnvironmentVariable("GOOGLE_PLAY_PACKAGE_NAME")?.Trim() ?? "";
        var serviceAccountJson = ReadSecret("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON", "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON_PATH");
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(serviceAccountJson))
            return StorePurchaseVerificationResult.Unavailable("Google Play purchase verification is not configured.");

        try
        {
            using var serviceAccount = JsonDocument.Parse(serviceAccountJson);
            var root = serviceAccount.RootElement;
            var clientEmail = GetString(root, "client_email");
            var privateKey = GetString(root, "private_key");
            var tokenUri = GetString(root, "token_uri");
            if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(tokenUri))
                return StorePurchaseVerificationResult.Unavailable("Google Play service-account credentials are incomplete.");

            var accessToken = await GetGoogleAccessTokenAsync(clientEmail, privateKey, tokenUri);
            if (string.IsNullOrWhiteSpace(accessToken))
                return StorePurchaseVerificationResult.Unavailable("Unable to authorize with Google Play.");

            var endpoint = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{Escape(packageName)}/purchases/products/{Escape(product.Google)}/tokens/{Escape(purchaseToken)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await Client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? StorePurchaseVerificationResult.Unavailable("Google Play rejected the server credentials.")
                    : StorePurchaseVerificationResult.Rejected("Google Play could not verify this transaction.");
            }

            using var purchase = JsonDocument.Parse(responseText);
            var purchaseRoot = purchase.RootElement;
            if (GetInt(purchaseRoot, "purchaseState", -1) != 0)
                return StorePurchaseVerificationResult.Rejected("Google Play reports that this purchase is not completed.");
            if (!string.Equals(GetString(purchaseRoot, "productId"), product.Google, StringComparison.Ordinal))
                return StorePurchaseVerificationResult.Rejected("Google Play returned a different product.");

            var canonicalTransactionId = GetString(purchaseRoot, "orderId");
            if (string.IsNullOrWhiteSpace(canonicalTransactionId))
                return StorePurchaseVerificationResult.Rejected("Google Play did not return an order ID.");
            if (!string.IsNullOrWhiteSpace(submittedTransactionId) &&
                !string.Equals(submittedTransactionId.Trim(), canonicalTransactionId, StringComparison.Ordinal))
                return StorePurchaseVerificationResult.Rejected("The submitted Google Play order ID does not match the store record.");

            var expectedAccountId = Sha256(profileId.Trim());
            var accountId = GetString(purchaseRoot, "obfuscatedExternalAccountId");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(accountId),
                    Encoding.UTF8.GetBytes(expectedAccountId)))
                return StorePurchaseVerificationResult.Rejected("This Google Play transaction belongs to a different player.");

            return new StorePurchaseVerificationResult(
                true,
                false,
                "Google Play transaction verified.",
                canonicalTransactionId,
                ReceiptFingerprint(purchaseToken));
        }
        catch (CryptographicException)
        {
            return StorePurchaseVerificationResult.Unavailable("Google Play service-account key is invalid.");
        }
        catch (JsonException)
        {
            return StorePurchaseVerificationResult.Unavailable("Google Play returned an unreadable verification response.");
        }
        catch (HttpRequestException)
        {
            return StorePurchaseVerificationResult.Unavailable("Google Play verification is temporarily unavailable.");
        }
        catch (TaskCanceledException)
        {
            return StorePurchaseVerificationResult.Unavailable("Google Play verification timed out.");
        }
    }

    private static async Task<StorePurchaseVerificationResult> VerifyAppleAsync(
        string internalProductId,
        string receiptToken,
        string submittedTransactionId,
        string profileId)
    {
        if (!ProductStoreIds.TryGetValue(internalProductId, out var product))
            return StorePurchaseVerificationResult.Rejected("Unknown product.");
        if (string.IsNullOrWhiteSpace(submittedTransactionId))
            return StorePurchaseVerificationResult.Rejected("Apple transaction ID is required.");

        var issuerId = Environment.GetEnvironmentVariable("APPLE_IAP_ISSUER_ID")?.Trim() ?? "";
        var keyId = Environment.GetEnvironmentVariable("APPLE_IAP_KEY_ID")?.Trim() ?? "";
        var bundleId = Environment.GetEnvironmentVariable("APPLE_IAP_BUNDLE_ID")?.Trim() ?? "";
        var privateKey = ReadSecret("APPLE_IAP_PRIVATE_KEY", "APPLE_IAP_PRIVATE_KEY_PATH");
        if (string.IsNullOrWhiteSpace(issuerId) || string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(privateKey))
            return StorePurchaseVerificationResult.Unavailable("App Store purchase verification is not configured.");

        try
        {
            var authorization = CreateAppleServerToken(issuerId, keyId, bundleId, privateKey);
            var transaction = await GetAppleTransactionAsync(
                "https://api.storekit.apple.com", authorization, submittedTransactionId.Trim());
            if (transaction == null)
            {
                transaction = await GetAppleTransactionAsync(
                    "https://api.storekit-sandbox.apple.com", authorization, submittedTransactionId.Trim());
            }
            if (transaction == null)
                return StorePurchaseVerificationResult.Rejected("Apple could not find this transaction.");

            var payload = DecodeJwsPayload(transaction);
            if (!string.Equals(GetString(payload, "bundleId"), bundleId, StringComparison.Ordinal) ||
                !string.Equals(GetString(payload, "productId"), product.Apple, StringComparison.Ordinal) ||
                !string.Equals(GetString(payload, "transactionId"), submittedTransactionId.Trim(), StringComparison.Ordinal))
                return StorePurchaseVerificationResult.Rejected("App Store transaction details do not match this purchase.");
            if (payload.TryGetProperty("revocationDate", out var revocationDate) && revocationDate.ValueKind != JsonValueKind.Null)
                return StorePurchaseVerificationResult.Rejected("Apple reports this transaction as revoked.");

            // Apple only returns this UUID when the iOS client provides a
            // deterministic app-account token. Do not enable the Apple flow
            // until its native bridge supplies and the server checks it.
            var expectedAppAccountToken = ProfileAppAccountToken(profileId);
            var appAccountToken = GetString(payload, "appAccountToken");
            if (!string.Equals(appAccountToken, expectedAppAccountToken, StringComparison.OrdinalIgnoreCase))
                return StorePurchaseVerificationResult.Rejected("This App Store transaction belongs to a different player.");

            return new StorePurchaseVerificationResult(
                true,
                false,
                "App Store transaction verified.",
                submittedTransactionId.Trim(),
                ReceiptFingerprint(receiptToken));
        }
        catch (CryptographicException)
        {
            return StorePurchaseVerificationResult.Unavailable("App Store private key is invalid.");
        }
        catch (JsonException)
        {
            return StorePurchaseVerificationResult.Unavailable("App Store returned an unreadable verification response.");
        }
        catch (HttpRequestException)
        {
            return StorePurchaseVerificationResult.Unavailable("App Store verification is temporarily unavailable.");
        }
        catch (TaskCanceledException)
        {
            return StorePurchaseVerificationResult.Unavailable("App Store verification timed out.");
        }
    }

    private static async Task<string> GetGoogleAccessTokenAsync(string clientEmail, string privateKey, string tokenUri)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var assertion = CreateRsaJwt(
            new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" },
            new Dictionary<string, object>
            {
                ["iss"] = clientEmail,
                ["scope"] = "https://www.googleapis.com/auth/androidpublisher",
                ["aud"] = tokenUri,
                ["iat"] = now,
                ["exp"] = now + 3300
            },
            privateKey);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });
        using var response = await Client.PostAsync(tokenUri, content);
        if (!response.IsSuccessStatusCode) return "";
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return GetString(payload.RootElement, "access_token");
    }

    private static string CreateAppleServerToken(string issuerId, string keyId, string bundleId, string privateKey)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = keyId, typ = "JWT" }, JsonOptions));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuerId,
            iat = now,
            exp = now + 1200,
            aud = "appstoreconnect-v1",
            bid = bundleId
        }, JsonOptions));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKey);
        var signature = ecdsa.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static async Task<string?> GetAppleTransactionAsync(string baseUrl, string authorization, string transactionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/inApps/v1/transactions/{Escape(transactionId)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authorization);
        using var response = await Client.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return GetString(body.RootElement, "signedTransactionInfo");
    }

    private static JsonElement DecodeJwsPayload(string jws)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3) throw new JsonException("Malformed App Store signed transaction.");
        using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        return payload.RootElement.Clone();
    }

    private static string CreateRsaJwt(Dictionary<string, object> header, Dictionary<string, object> payload, string privateKey)
    {
        var encodedHeader = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions));
        var encodedPayload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signingInput = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        return $"{encodedHeader}.{encodedPayload}.{Base64Url(rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }

    private static string ReadSecret(string valueVariable, string pathVariable)
    {
        var value = Environment.GetEnvironmentVariable(valueVariable);
        if (!string.IsNullOrWhiteSpace(value)) return value.Replace("\\n", "\n", StringComparison.Ordinal);
        var path = Environment.GetEnvironmentVariable(pathVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static int GetInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string ReceiptFingerprint(string value) => Sha256(value.Trim());
    private static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static string ProfileAppAccountToken(string profileId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(profileId.Trim()));
        var guidBytes = bytes[..16];
        // Set RFC 4122 variant/version bits to make an accepted UUID for StoreKit.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes).ToString();
    }

    private sealed record StoreIds(string Google, string Apple);

    private static readonly Dictionary<string, StoreIds> ProductStoreIds = new(StringComparer.Ordinal)
    {
        ["gold_pouch"] = new("gold_pouch", "com.crownroad.gold_pouch"),
        ["gold_chest"] = new("gold_chest", "com.crownroad.gold_chest"),
        ["gold_warchest"] = new("gold_warchest", "com.crownroad.gold_warchest"),
        ["gold_treasury"] = new("gold_treasury", "com.crownroad.gold_treasury"),
        ["food_rations"] = new("food_rations", "com.crownroad.food_rations"),
        ["food_provisions"] = new("food_provisions", "com.crownroad.food_provisions"),
        ["food_stockpile"] = new("food_stockpile", "com.crownroad.food_stockpile"),
        ["food_granary"] = new("food_granary", "com.crownroad.food_granary"),
        ["starter_kit"] = new("starter_kit", "com.crownroad.starter_kit"),
        ["campaign_resupply"] = new("campaign_resupply", "com.crownroad.campaign_resupply")
    };
}
