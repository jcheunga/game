using System.Net;
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
}
