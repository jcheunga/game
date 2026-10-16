using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace CrownroadServer.Tests;

public static partial class ServerTests
{
    private static async Task<JsonElement> AccountPost(HttpClient client, string path, object body, string profile = "", string token = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (profile.Length > 0) request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.Clone();
    }
    private static async Task TestAccountEmail(HttpClient client)
    {
        var deliveredCode = "";
        AccountAuth.TestMailSender = (email, code) => { deliveredCode = code; return Task.CompletedTask; };
        try
        {
            var challenge = await AccountPost(client, "/auth/email/start", new { email = "caravan@example.com" }, "TEST-01", SessionTokens["TEST-01"]);
            var body = new { challengeId = challenge.GetProperty("challengeId").GetString(), secret = challenge.GetProperty("secret").GetString(), code = deliveredCode };
            var account = await AccountPost(client, "/auth/email/verify", body);
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-01", "Verified email links the authenticated guest's existing progress");
            Assert(account.GetProperty("isNewAccount").GetBoolean(), "First verified email creates an identity");
            var token = account.GetProperty("sessionToken").GetString(); Assert(token?.Length > 32, "Email sign-in issues a secure session");
            using var replay = await client.PostAsJsonAsync("/auth/email/verify", body);
            Assert(replay.StatusCode == HttpStatusCode.BadRequest, "Sign-in codes are single-use");
            challenge = await AccountPost(client, "/auth/email/start", new { email = "CARAVAN@example.com" });
            account = await AccountPost(client, "/auth/email/verify", new { challengeId = challenge.GetProperty("challengeId").GetString(), secret = challenge.GetProperty("secret").GetString(), code = deliveredCode });
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-01", "Another device recovers the same normalized email identity");
            Assert(!account.GetProperty("isNewAccount").GetBoolean(), "Returning identities cannot be mistaken for new accounts");
            using var wallet = new HttpRequestMessage(HttpMethod.Get, "/wallet?profileId=TEST-01");
            wallet.Headers.Authorization = new("Bearer", account.GetProperty("sessionToken").GetString());
            using var response = await client.SendAsync(wallet); Assert(response.IsSuccessStatusCode, "Email account sessions authorize existing protected APIs");
        }
        finally { AccountAuth.TestMailSender = null; }
    }
    private static async Task TestAccountCodeSecurity(HttpClient client)
    {
        var code = ""; AccountAuth.TestMailSender = (_, value) => { code = value; return Task.CompletedTask; };
        try
        {
            var challenge = await AccountPost(client, "/auth/email/start", new { email = "security@example.com" }, "TEST-01", "forged-session");
            var id = challenge.GetProperty("challengeId").GetString(); var secret = challenge.GetProperty("secret").GetString();
            using var stolen = await client.PostAsJsonAsync("/auth/email/verify", new { challengeId = id, secret = new string('a',64), code });
            Assert(stolen.StatusCode == HttpStatusCode.BadRequest, "A mail code without its device secret is rejected");
            var wrong = code == "111111" ? "222222" : "111111";
            for (var i = 0; i < 5; i++)
            {
                using var rejected = await client.PostAsJsonAsync("/auth/email/verify", new { challengeId = id, secret, code = wrong });
                Assert(rejected.StatusCode == HttpStatusCode.BadRequest, "Incorrect code rejected");
            }
            using var exhausted = await client.PostAsJsonAsync("/auth/email/verify", new { challengeId = id, secret, code });
            Assert(exhausted.StatusCode == HttpStatusCode.BadRequest, "Five failures exhaust a challenge");
            challenge = await AccountPost(client, "/auth/email/start", new { email = "security@example.com" }, "TEST-01", "forged-session");
            var account = await AccountPost(client, "/auth/email/verify", new { challengeId = challenge.GetProperty("challengeId").GetString(), secret = challenge.GetProperty("secret").GetString(), code });
            Assert(account.GetProperty("playerProfileId").GetString() != "TEST-01", "A forged guest header cannot link another player's identity");
            var profileId = account.GetProperty("playerProfileId").GetString()!;
            Assert(profileId == profileId.ToUpperInvariant(), "Account profile IDs match the client's canonical session format");
            challenge = await AccountPost(client, "/auth/email/start", new { email = "expiry@example.com" });
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET expires_at=1 WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", challenge.GetProperty("challengeId").GetString()); cmd.ExecuteNonQuery();
            using var expired = await client.PostAsJsonAsync("/auth/email/verify", new { challengeId = challenge.GetProperty("challengeId").GetString(), secret = challenge.GetProperty("secret").GetString(), code });
            Assert(expired.StatusCode == HttpStatusCode.BadRequest, "Expired codes cannot recover accounts");
        }
        finally { AccountAuth.TestMailSender = null; }
    }
    private static async Task TestAccountProviderAvailability(HttpClient client)
    {
        var original = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", "");
            using var unavailable = await client.PostAsJsonAsync("/auth/google/start", new { });
            Assert(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Unconfigured Google sign-in is explicit");
            using var forged = await client.PostAsJsonAsync("/auth/google/poll", new { challengeId = new string('a',64), secret = new string('b',64) });
            Assert(forged.StatusCode == HttpStatusCode.BadRequest, "Forged Google polls cannot issue sessions");
        }
        finally { Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", original); }
    }

    private static async Task TestAccountGoogleChallenge(HttpClient client)
    {
        var keys = new[] { "GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET", "GOOGLE_REDIRECT_URI" };
        var previous = keys.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Environment.SetEnvironmentVariable(keys[0], "test-client");
            Environment.SetEnvironmentVariable(keys[1], "test-secret");
            Environment.SetEnvironmentVariable(keys[2], "http://localhost/auth/google/callback");
            var challenge = await AccountPost(client, "/auth/google/start", new { }, "TEST-01", SessionTokens["TEST-01"]);
            var id = challenge.GetProperty("challengeId").GetString(); var secret = challenge.GetProperty("secret").GetString();
            var uri = new Uri(challenge.GetProperty("url").GetString()!);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert(uri.Host == "accounts.google.com" && query["code_challenge_method"] == "S256", "Google starts on its HTTPS authorization endpoint with PKCE");
            Assert(query["state"].ToString().Length == 64 && query["state"] != secret, "Browser state and device poll secret are independent");
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT verifier FROM account_challenges WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id);
            var verifier = cmd.ExecuteScalar()!.ToString()!;
            var expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
            Assert(query["code_challenge"] == expected, "The challenge hashes the private server verifier");
            var pending = await AccountPost(client, "/auth/google/poll", new { challengeId = id, secret });
            Assert(pending.GetProperty("status").GetString() == "pending", "A pending Google login cannot issue a session");
            using var forged = await client.GetAsync("/auth/google/callback?state=" + new string('a',64) + "&code=forged");
            Assert(forged.StatusCode == HttpStatusCode.BadRequest, "Unknown callback state is rejected before contacting Google");
            cmd.CommandText = "UPDATE account_challenges SET status='ready', subject='test-verified-google-subject' WHERE id=@id";
            cmd.ExecuteNonQuery(); // Simulate a successful provider verification without using live credentials.
            using var stolen = await client.PostAsJsonAsync("/auth/google/poll", new { challengeId = id, secret = query["state"].ToString() });
            Assert(stolen.StatusCode == HttpStatusCode.BadRequest, "A browser callback state cannot retrieve the device session");
            var account = await AccountPost(client, "/auth/google/poll", new { challengeId = id, secret });
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-01", "Verified Google login preserves authenticated guest progress");
            using var replay = await client.PostAsJsonAsync("/auth/google/poll", new { challengeId = id, secret });
            Assert(replay.StatusCode == HttpStatusCode.BadRequest, "Google sessions are retrieved only once");
            var token = account.GetProperty("sessionToken").GetString();
            await AccountPost(client, "/auth/signout", new { }, "TEST-01", token!);
            using var wallet = new HttpRequestMessage(HttpMethod.Get, "/wallet?profileId=TEST-01"); wallet.Headers.Authorization = new("Bearer", token);
            using var revoked = await client.SendAsync(wallet);
            Assert(revoked.StatusCode == HttpStatusCode.Unauthorized, "Sign-out revokes the current account session");
            foreach (var payload in new[] { "{", "[]", "null" })
            {
                using var malformed = await client.PostAsync("/auth/email/verify", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
                Assert(malformed.StatusCode == HttpStatusCode.BadRequest, "Malformed sign-in input fails cleanly");
            }
            Environment.SetEnvironmentVariable(keys[2], "ftp://localhost/auth/google/callback");
            using var unsupported = await client.PostAsJsonAsync("/auth/google/start", new { });
            Assert(unsupported.StatusCode == HttpStatusCode.ServiceUnavailable, "Only secure or loopback HTTP callbacks are accepted");
        }
        finally { for (var i = 0; i < keys.Length; i++) Environment.SetEnvironmentVariable(keys[i], previous[i]); }
    }

    private static async Task TestAccountPlayGames(HttpClient client)
    {
        var keys = new[] { "PLAY_GAMES_CLIENT_ID", "PLAY_GAMES_CLIENT_SECRET" };
        var previous = keys.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, "");
            using var unavailable = await client.PostAsJsonAsync("/auth/playgames", new { authCode = "good-code" });
            Assert(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Unconfigured Play Games sign-in is explicit");
            Environment.SetEnvironmentVariable(keys[0], "play-server.apps.googleusercontent.com");
            Environment.SetEnvironmentVariable(keys[1], "test-secret");
            AccountAuth.TestPlayGamesExchange = code => code == "good-code" ? Task.FromResult("g-test-player") : throw new HttpRequestException("rejected");
            var providers = await client.GetFromJsonAsync<JsonElement>("/auth/providers");
            Assert(providers.GetProperty("playGames").GetBoolean() && providers.GetProperty("playGamesClientId").GetString() == "play-server.apps.googleusercontent.com",
                "Providers hand Android the server client ID Play Games issues codes for");
            await EnsureSession(client, "TEST-PG");
            var account = await AccountPost(client, "/auth/playgames", new { authCode = "good-code" }, "TEST-PG", SessionTokens["TEST-PG"]);
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-PG" && account.GetProperty("isNewAccount").GetBoolean(),
                "A verified Play Games player links the authenticated guest's progress");
            Assert(account.GetProperty("accountLabel").GetString() == "Google Play Games", "Play Games accounts are labelled by service");
            account = await AccountPost(client, "/auth/playgames", new { authCode = "good-code" });
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-PG" && !account.GetProperty("isNewAccount").GetBoolean(),
                "A reinstall signed in to the same Play Games player recovers the same profile");
            using var rejected = await client.PostAsJsonAsync("/auth/playgames", new { authCode = "forged-code" });
            Assert(rejected.StatusCode == HttpStatusCode.BadGateway, "A code Google refuses issues no session");
            using var empty = await client.PostAsJsonAsync("/auth/playgames", new { authCode = "" });
            Assert(empty.StatusCode == HttpStatusCode.BadRequest, "A missing Play Games code is rejected before contacting Google");
        }
        finally
        {
            AccountAuth.TestPlayGamesExchange = null;
            for (var i = 0; i < keys.Length; i++) Environment.SetEnvironmentVariable(keys[i], previous[i]);
        }
    }

    private static async Task TestAccountGameCenter(HttpClient client)
    {
        var keys = new[] { "GAME_CENTER_BUNDLE_ID", "APPLE_IAP_BUNDLE_ID" };
        var previous = keys.Select(Environment.GetEnvironmentVariable).ToArray();
        static X509Certificate2 Root(RSA key)
        {
            var request = new CertificateRequest("CN=Test Game Center Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
        using var rootKey = RSA.Create(2048); using var root = Root(rootKey);
        using var otherKey = RSA.Create(2048); using var otherRoot = Root(otherKey);
        using var leafKey = RSA.Create(2048);
        using var leaf = new CertificateRequest("CN=Test Game Center", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Create(root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMonths(1), RandomNumberGenerator.GetBytes(8));
        var fetched = new List<Uri>();
        Dictionary<string, object> Signed(string playerId, long timestamp, string bundle = "com.crownroad.game")
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var payload = Encoding.UTF8.GetBytes(playerId).Concat(Encoding.UTF8.GetBytes(bundle)).Concat(new byte[8]).Concat(salt).ToArray();
            BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(payload.Length - salt.Length - 8), (ulong)timestamp);
            return new()
            {
                ["playerId"] = playerId, ["publicKeyUrl"] = "https://static.gc.apple.com/public-key/gc-prod-9.cer", ["timestamp"] = timestamp,
                ["signature"] = Convert.ToBase64String(leafKey.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)),
                ["salt"] = Convert.ToBase64String(salt)
            };
        }
        async Task<HttpStatusCode> Status(object body) { using var response = await client.PostAsJsonAsync("/auth/gamecenter", body); return response.StatusCode; }
        try
        {
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, "");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert(await Status(Signed("G:1001", now)) == HttpStatusCode.ServiceUnavailable, "Unconfigured Game Center sign-in is explicit");
            Environment.SetEnvironmentVariable(keys[0], "com.crownroad.game");
            AccountAuth.TestGameCenterRoot = root;
            AccountAuth.TestGameCenterCertificate = url => { fetched.Add(url); return Task.FromResult(leaf.RawData); };
            var providers = await client.GetFromJsonAsync<JsonElement>("/auth/providers");
            Assert(providers.GetProperty("gameCenter").GetBoolean(), "Game Center is offered once the app's bundle ID is configured");
            await EnsureSession(client, "TEST-GC");
            var body = Signed("G:1001", now);
            var account = await AccountPost(client, "/auth/gamecenter", body, "TEST-GC", SessionTokens["TEST-GC"]);
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-GC" && account.GetProperty("isNewAccount").GetBoolean(),
                "A verified Game Center player links the authenticated guest's progress");
            Assert(account.GetProperty("accountLabel").GetString() == "Game Center", "Game Center accounts are labelled by service");
            Assert(await Status(body) == HttpStatusCode.BadRequest, "Each Game Center signature signs in only once");
            account = await AccountPost(client, "/auth/gamecenter", Signed("G:1001", now));
            Assert(account.GetProperty("playerProfileId").GetString() == "TEST-GC" && !account.GetProperty("isNewAccount").GetBoolean(),
                "Another device signed in to the same Game Center player recovers the same profile");
            var forged = Signed("G:1001", now); forged["playerId"] = "G:2002";
            Assert(await Status(forged) == HttpStatusCode.Unauthorized, "A signature cannot be reused for another player");
            Assert(await Status(Signed("G:1001", now, "com.other.game")) == HttpStatusCode.Unauthorized, "A signature for another app is rejected");
            Assert(await Status(Signed("G:1001", now - 20 * 60_000)) == HttpStatusCode.BadRequest, "Stale Game Center signatures are rejected");
            fetched.Clear();
            foreach (var url in new[] { "https://static.gc.apple.com.evil.example/key.cer", "http://static.gc.apple.com/key.cer" })
            {
                var redirected = Signed("G:1001", now); redirected["publicKeyUrl"] = url;
                Assert(await Status(redirected) == HttpStatusCode.BadRequest, "Only HTTPS apple.com key URLs are fetched");
            }
            Assert(fetched.Count == 0, "Rejected key URLs are never downloaded");
            AccountAuth.TestGameCenterRoot = otherRoot;
            Assert(await Status(Signed("G:1001", now)) == HttpStatusCode.Unauthorized, "A key that does not chain to a trusted root is rejected");
        }
        finally
        {
            AccountAuth.TestGameCenterRoot = null; AccountAuth.TestGameCenterCertificate = null;
            for (var i = 0; i < keys.Length; i++) Environment.SetEnvironmentVariable(keys[i], previous[i]);
        }
    }
}
