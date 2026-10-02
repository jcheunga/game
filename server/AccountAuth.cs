using System.Data.Common;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace CrownroadServer;

public static class AccountAuth
{
    private static readonly HttpClient Google = new() { Timeout = TimeSpan.FromSeconds(15) };
    internal static Func<string, string, Task>? TestMailSender;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static string Env(string name) => Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
    private static string RandomToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static IResult Error(string message, int status = 400) => Results.Json(new { message }, statusCode: status);
    private static string Text(JsonElement body, string name) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    public static void Map(IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/auth").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (JsonException) { return Error("Invalid sign-in request."); }
        });
        auth.MapGet("/providers", () => Results.Ok(new { email = MailConfigured, google = GoogleConfigured }));
        auth.MapPost("/email/start", EmailStart);
        auth.MapPost("/email/verify", EmailVerify);
        auth.MapPost("/google/start", GoogleStart);
        auth.MapGet("/google/callback", GoogleCallback);
        auth.MapPost("/google/poll", GooglePoll);
        auth.MapPost("/signout", (HttpRequest request) =>
        {
            if (!SessionAuth.TryAuthorize(request, request.Headers["X-Convoy-Profile"].ToString(), out var session)) return SessionAuth.Unauthorized();
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE auth_sessions SET revoked_at=@now WHERE session_id=@id";
            cmd.Parameters.AddWithValue("@now", Now); cmd.Parameters.AddWithValue("@id", session!.SessionId); cmd.ExecuteNonQuery();
            return Results.Ok(new { status = "ok" });
        });
    }
    private static bool MailConfigured => TestMailSender != null || (Env("SMTP_HOST").Length > 0 && Env("SMTP_FROM").Length > 0);
    private static bool GoogleConfigured => Env("GOOGLE_CLIENT_ID").Length > 0 && Env("GOOGLE_CLIENT_SECRET").Length > 0 &&
        Uri.TryCreate(Env("GOOGLE_REDIRECT_URI"), UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));

    public static void Initialize(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS account_identities (
                provider TEXT NOT NULL, subject TEXT NOT NULL, profile_id TEXT NOT NULL,
                PRIMARY KEY(provider, subject), FOREIGN KEY(profile_id) REFERENCES players(profile_id)
            );
            CREATE TABLE IF NOT EXISTS account_challenges (
                id TEXT PRIMARY KEY, secret_hash TEXT NOT NULL, provider TEXT NOT NULL, subject TEXT NOT NULL DEFAULT '',
                guest_profile TEXT NOT NULL DEFAULT '', code_hash TEXT NOT NULL DEFAULT '', state TEXT NOT NULL UNIQUE,
                verifier TEXT NOT NULL DEFAULT '', expires_at BIGINT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'pending'
            );
            CREATE INDEX IF NOT EXISTS idx_account_challenges_expiry ON account_challenges(expires_at);
            """;
        cmd.ExecuteNonQuery();
    }

    private static string Guest(HttpRequest request)
    {
        var profile = request.Headers["X-Convoy-Profile"].ToString();
        return SessionAuth.TryAuthorize(request, profile, out _) ? profile : "";
    }

    private static (string Id, string Secret, string State, string Verifier) CreateChallenge(HttpRequest request, string provider, string subject = "", string code = "")
    {
        var id = RandomToken(); var secret = RandomToken(); var state = RandomToken(); var verifier = RandomToken();
        var guest = Guest(request);
        using var conn = Database.Open(); using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM account_challenges WHERE expires_at <= @now;
            INSERT INTO account_challenges(id, secret_hash, provider, subject, guest_profile, code_hash, state, verifier, expires_at)
            VALUES(@id,@secret,@provider,@subject,@guest,@code,@state,@verifier,@expires);
            """;
        cmd.Parameters.AddWithValue("@now", Now); cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@secret", Hash(secret)); cmd.Parameters.AddWithValue("@provider", provider);
        cmd.Parameters.AddWithValue("@subject", subject); cmd.Parameters.AddWithValue("@guest", guest);
        cmd.Parameters.AddWithValue("@code", Hash(secret + code)); cmd.Parameters.AddWithValue("@state", state);
        cmd.Parameters.AddWithValue("@verifier", verifier); cmd.Parameters.AddWithValue("@expires", Now + 600);
        cmd.ExecuteNonQuery(); return (id, secret, state, verifier);
    }

    private static async Task<IResult> EmailStart(HttpRequest request)
    {
        if (!MailConfigured) return Error("Email sign-in is not configured on this server.", 503);
        using var document = await JsonDocument.ParseAsync(request.Body);
        var email = Text(document.RootElement, "email").Trim().ToLowerInvariant();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            return Error("Enter a valid email address.");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = CreateChallenge(request, "email", email, code);
        try
        {
            if (TestMailSender != null) await TestMailSender(email, code);
            else
            {
                using var smtp = new SmtpClient(Env("SMTP_HOST"), int.TryParse(Env("SMTP_PORT"), out var port) ? port : 587)
                { EnableSsl = true, Credentials = Env("SMTP_USER").Length > 0 ? new NetworkCredential(Env("SMTP_USER"), Env("SMTP_PASSWORD")) : null };
                using var mail = new MailMessage(Env("SMTP_FROM"), email, "Your Crownroad sign-in code",
                    $"Your code is {code}. It expires in 10 minutes. If you did not request this code, ignore this email.");
                await smtp.SendMailAsync(mail);
            }
        }
        catch
        {
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM account_challenges WHERE id=@id"; cmd.Parameters.AddWithValue("@id", challenge.Id); cmd.ExecuteNonQuery();
            return Error("Could not send a code. Try again later.", 503);
        }
        return Results.Ok(new { challengeId = challenge.Id, secret = challenge.Secret, message = "Check your email." });
    }

    private static async Task<IResult> EmailVerify(HttpRequest request)
    {
        using var document = await JsonDocument.ParseAsync(request.Body); var body = document.RootElement;
        var id = Text(body, "challengeId"); var secret = Text(body, "secret"); var code = Text(body, "code");
        if (id.Length != 64 || secret.Length != 64 || code.Length != 6) return Error("Invalid code.");
        using var conn = Database.Open(); using var tx = conn.BeginTransaction(); using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
        // Claim one attempt atomically across replicas; secrets never appear in URLs or logs.
        cmd.CommandText = """
            UPDATE account_challenges SET attempts=attempts+1
            WHERE id=@id AND secret_hash=@secret AND provider='email' AND status='pending' AND expires_at>@now AND attempts<5
            RETURNING subject, guest_profile, code_hash
            """;
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@secret", Hash(secret)); cmd.Parameters.AddWithValue("@now", Now);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Error("Code expired. Request a new one.");
        var email = reader.GetString(0); var guest = reader.GetString(1); var hash = reader.GetString(2); reader.Close();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(secret + code))))
        { tx.Commit(); return Error("Incorrect code."); }
        Consume(conn, tx, id);
        var result = Complete(conn, tx, "email", email, guest); tx.Commit(); return result;
    }

    private static Task<IResult> GoogleStart(HttpRequest request)
    {
        if (!GoogleConfigured) return Task.FromResult(Error("Google sign-in is not configured on this server.", 503));
        var challenge = CreateChallenge(request, "google");
        var pkce = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(challenge.Verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string,string> {
            ["client_id"] = Env("GOOGLE_CLIENT_ID"), ["redirect_uri"] = Env("GOOGLE_REDIRECT_URI"), ["response_type"] = "code",
            ["scope"] = "openid email", ["state"] = challenge.State, ["code_challenge"] = pkce, ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account"
        }.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        return Task.FromResult(Results.Ok(new { challengeId = challenge.Id, secret = challenge.Secret, url }));
    }

    private static async Task<IResult> GoogleCallback(HttpRequest request)
    {
        if (!GoogleConfigured) return Error("Google sign-in unavailable.", 503);
        var state = request.Query["state"].ToString(); var code = request.Query["code"].ToString();
        if (state.Length != 64 || code.Length == 0 || code.Length > 4096) return Error("Sign-in was cancelled or expired.");
        string id, verifier;
        using (var conn = Database.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE account_challenges SET status='verifying' WHERE state=@state AND provider='google' AND status='pending' AND expires_at>@now RETURNING id, verifier";
            cmd.Parameters.AddWithValue("@state", state); cmd.Parameters.AddWithValue("@now", Now);
            using var reader = cmd.ExecuteReader(); if (!reader.Read()) return Error("Sign-in expired.");
            id = reader.GetString(0); verifier = reader.GetString(1);
        }
        try
        {
            using var response = await Google.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["client_id"] = Env("GOOGLE_CLIENT_ID"), ["client_secret"] = Env("GOOGLE_CLIENT_SECRET"), ["code"] = code,
                ["code_verifier"] = verifier, ["redirect_uri"] = Env("GOOGLE_REDIRECT_URI"), ["grant_type"] = "authorization_code"
            }));
            response.EnsureSuccessStatusCode(); using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            userRequest.Headers.Authorization = new("Bearer", Text(tokens.RootElement, "access_token"));
            using var userResponse = await Google.SendAsync(userRequest); userResponse.EnsureSuccessStatusCode();
            using var user = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var subject = Text(user.RootElement, "sub");
            if (subject.Length == 0 || !user.RootElement.TryGetProperty("email_verified", out var verified) || verified.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("Unverified Google account.");
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET status='ready', subject=@subject, verifier='' WHERE id=@id AND expires_at>@now";
            cmd.Parameters.AddWithValue("@subject", subject); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@now", Now); cmd.ExecuteNonQuery();
            request.HttpContext.Response.Headers.CacheControl = "no-store";
            return Results.Content("<!doctype html><meta name='viewport' content='width=device-width'><title>Crownroad</title><body style='background:#142127;color:#e9d6ae;font:20px system-ui;text-align:center;padding:15vh 24px'><h1>Signed in</h1><p>Return to Crownroad to continue.</p></body>", "text/html");
        }
        catch
        {
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET status='failed', verifier='' WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
            return Error("Google sign-in failed. Return to the game and try again.", 502);
        }
    }

    private static async Task<IResult> GooglePoll(HttpRequest request)
    {
        using var document = await JsonDocument.ParseAsync(request.Body); var body = document.RootElement;
        var id = Text(body, "challengeId"); var secret = Text(body, "secret");
        if (id.Length != 64 || secret.Length != 64) return Error("Sign-in expired.");
        using var conn = Database.Open(); using var tx = conn.BeginTransaction(); using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE account_challenges SET status='consumed'
            WHERE id=@id AND secret_hash=@secret AND provider='google' AND status='ready' AND expires_at>@now
            RETURNING subject, guest_profile
            """;
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@secret", Hash(secret)); cmd.Parameters.AddWithValue("@now", Now);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var subject = reader.GetString(0); var guest = reader.GetString(1); reader.Close();
            Consume(conn, tx, id); var result = Complete(conn, tx, "google", subject, guest); tx.Commit(); return result;
        }
        reader.Close(); cmd.CommandText = "SELECT status FROM account_challenges WHERE id=@id AND secret_hash=@secret AND expires_at>@now";
        var status = cmd.ExecuteScalar()?.ToString();
        return status is "pending" or "verifying" ? Results.Ok(new { status = "pending" }) : Error("Sign-in failed or expired. Try again.");
    }

    private static void Consume(DbConnection conn, DbTransaction tx, string id)
    {
        using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM account_challenges WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
    }

    private static IResult Complete(DbConnection conn, DbTransaction tx, string provider, string subject, string guest)
    {
        using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT profile_id FROM account_identities WHERE provider=@provider AND subject=@subject";
        cmd.Parameters.AddWithValue("@provider", provider); cmd.Parameters.AddWithValue("@subject", subject);
        var profile = cmd.ExecuteScalar()?.ToString();
        var isNewAccount = false;
        if (profile == null)
        {
            profile = guest.Length > 0 ? guest : $"ACC-{Guid.NewGuid():N}".ToUpperInvariant();
            cmd.CommandText = "INSERT INTO players(profile_id, callsign, created_at) VALUES(@profile,'Caravan',@now) ON CONFLICT(profile_id) DO NOTHING";
            cmd.Parameters.AddWithValue("@profile", profile); cmd.Parameters.AddWithValue("@now", Now); cmd.ExecuteNonQuery();
            // The identity key is unique. Concurrent first logins choose one winner atomically.
            cmd.CommandText = "INSERT INTO account_identities(provider,subject,profile_id) VALUES(@provider,@subject,@profile) ON CONFLICT(provider,subject) DO NOTHING";
            isNewAccount = cmd.ExecuteNonQuery() > 0;
            cmd.CommandText = "SELECT profile_id FROM account_identities WHERE provider=@provider AND subject=@subject"; profile = cmd.ExecuteScalar()!.ToString()!;
        }
        cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("@profile", profile);
        cmd.CommandText = "SELECT callsign FROM players WHERE profile_id=@profile"; var callsign = cmd.ExecuteScalar()?.ToString() ?? "Caravan";
        var token = SessionAuth.Issue(conn, tx, profile, Now);
        return Results.Ok(new { status = "ok", playerProfileId = profile, playerCallsign = callsign, sessionToken = token,
            provider, accountLabel = provider == "email" ? subject : "Google account", isNewAccount, syncedAtUnixSeconds = Now });
    }
}
