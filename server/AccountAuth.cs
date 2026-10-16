using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace CrownroadServer;

public static class AccountAuth
{
    private static readonly HttpClient Google = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly HttpClient Apple = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ConcurrentDictionary<string, X509Certificate2> GameCenterCertificates = new();
    internal static Func<string, string, Task>? TestMailSender;
    /// <summary>Test stand-in for Google's code exchange: Play Games auth code → player ID.</summary>
    internal static Func<string, Task<string>>? TestPlayGamesExchange;
    /// <summary>Test stand-ins for Apple's certificate download and the root it chains to.</summary>
    internal static Func<Uri, Task<byte[]>>? TestGameCenterCertificate;
    internal static X509Certificate2? TestGameCenterRoot;
    /// <summary>Test stand-ins for Apple's token endpoint (form fields → token JSON) and its ID-token keys (JWKS JSON).</summary>
    internal static Func<IReadOnlyDictionary<string, string>, Task<string>>? TestAppleToken;
    internal static Func<Task<string>>? TestAppleKeys;
    private sealed record AppleKeySet(DateTime Expires, JsonElement Keys);
    private static AppleKeySet? _appleKeys;
    private const string AppleIssuer = "https://appleid.apple.com";
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
        auth.MapGet("/providers", () => Results.Ok(new
        {
            email = MailConfigured, google = GoogleConfigured, playGames = PlayGamesConfigured,
            // Public OAuth client ID the Android client asks Play Games to issue a server code for.
            playGamesClientId = PlayGamesConfigured ? Env("PLAY_GAMES_CLIENT_ID") : "", gameCenter = GameCenterConfigured,
            apple = AppleConfigured
        }));
        auth.MapPost("/email/start", EmailStart);
        auth.MapPost("/email/verify", EmailVerify);
        auth.MapPost("/google/start", GoogleStart);
        auth.MapGet("/google/callback", GoogleCallback);
        auth.MapPost("/google/poll", (HttpRequest request) => Poll(request, "google"));
        auth.MapPost("/apple/start", AppleStart);
        auth.MapPost("/apple/callback", AppleCallback);
        auth.MapPost("/apple/poll", (HttpRequest request) => Poll(request, "apple"));
        auth.MapPost("/playgames", PlayGames);
        auth.MapPost("/gamecenter", GameCenter);
        auth.MapPost("/signout", (HttpRequest request) =>
        {
            if (!SessionAuth.TryAuthorize(request, request.Headers["X-Convoy-Profile"].ToString(), out var session)) return SessionAuth.Unauthorized();
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE auth_sessions SET revoked_at=@now WHERE session_id=@id";
            cmd.Parameters.AddWithValue("@now", Now); cmd.Parameters.AddWithValue("@id", session!.SessionId); cmd.ExecuteNonQuery();
            return Results.Ok(new { status = "ok" });
        });
    }
    /// <summary>Local servers only: print sign-in codes to the console instead of mailing them. Production refuses to start with it.</summary>
    internal static bool DevSignInCodes => Env("CROWNROAD_DEV_SIGN_IN_CODES") == "1";
    private static bool MailConfigured => TestMailSender != null || DevSignInCodes || (Env("SMTP_HOST").Length > 0 && Env("SMTP_FROM").Length > 0);
    private static bool PlayGamesConfigured => TestPlayGamesExchange != null ||
        (Env("PLAY_GAMES_CLIENT_ID").Length > 0 && Env("PLAY_GAMES_CLIENT_SECRET").Length > 0);
    private static string GameCenterBundleId => Env("GAME_CENTER_BUNDLE_ID") is { Length: > 0 } id ? id : Env("APPLE_IAP_BUNDLE_ID");
    private static bool GameCenterConfigured => GameCenterBundleId.Length > 0;
    private static bool GoogleConfigured => Env("GOOGLE_CLIENT_ID").Length > 0 && Env("GOOGLE_CLIENT_SECRET").Length > 0 && SecureCallback(Env("GOOGLE_REDIRECT_URI"));
    private static bool AppleConfigured => Env("APPLE_SIGN_IN_CLIENT_ID").Length > 0 && Env("APPLE_SIGN_IN_TEAM_ID").Length > 0 &&
        Env("APPLE_SIGN_IN_KEY_ID").Length > 0 && (Env("APPLE_SIGN_IN_PRIVATE_KEY").Length > 0 || File.Exists(Env("APPLE_SIGN_IN_PRIVATE_KEY_PATH"))) &&
        SecureCallback(Env("APPLE_SIGN_IN_REDIRECT_URI"));
    private static bool SecureCallback(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));
    private static string Query(Dictionary<string, string> values) =>
        string.Join("&", values.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
    private static IResult SignedInPage() => Results.Content("<!doctype html><meta name='viewport' content='width=device-width'><title>Crownroad</title><body style='background:#142127;color:#e9d6ae;font:20px system-ui;text-align:center;padding:15vh 24px'><h1>Signed in</h1><p>Return to Crownroad to continue.</p></body>", "text/html");

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
            else if (DevSignInCodes) Console.WriteLine($"[dev] Sign-in code for {email}: {code}");
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
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + Query(new() {
            ["client_id"] = Env("GOOGLE_CLIENT_ID"), ["redirect_uri"] = Env("GOOGLE_REDIRECT_URI"), ["response_type"] = "code",
            ["scope"] = "openid email", ["state"] = challenge.State, ["code_challenge"] = pkce, ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account"
        });
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
            return SignedInPage();
        }
        catch
        {
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET status='failed', verifier='' WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
            return Error("Google sign-in failed. Return to the game and try again.", 502);
        }
    }

    /// <summary>The device collects a browser sign-in with its own secret, which never passes through the browser.</summary>
    private static async Task<IResult> Poll(HttpRequest request, string provider)
    {
        using var document = await JsonDocument.ParseAsync(request.Body); var body = document.RootElement;
        var id = Text(body, "challengeId"); var secret = Text(body, "secret");
        if (id.Length != 64 || secret.Length != 64) return Error("Sign-in expired.");
        using var conn = Database.Open(); using var tx = conn.BeginTransaction(); using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE account_challenges SET status='consumed'
            WHERE id=@id AND secret_hash=@secret AND provider=@provider AND status='ready' AND expires_at>@now
            RETURNING subject, guest_profile
            """;
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@secret", Hash(secret)); cmd.Parameters.AddWithValue("@now", Now);
        cmd.Parameters.AddWithValue("@provider", provider);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var subject = reader.GetString(0); var guest = reader.GetString(1); reader.Close();
            Consume(conn, tx, id); var result = Complete(conn, tx, provider, subject, guest); tx.Commit(); return result;
        }
        reader.Close(); cmd.CommandText = "SELECT status FROM account_challenges WHERE id=@id AND secret_hash=@secret AND provider=@provider AND expires_at>@now";
        var status = cmd.ExecuteScalar()?.ToString();
        return status is "pending" or "verifying" ? Results.Ok(new { status = "pending" }) : Error("Sign-in failed or expired. Try again.");
    }

    /// <summary>
    /// Sign in with Apple in the browser. Apple has no PKCE, so the nonce it signs into the ID token
    /// (a hash of the private server verifier) ties that token to this challenge instead.
    /// No scopes are requested: the game needs only Apple's stable user ID, not a name or email.
    /// </summary>
    private static Task<IResult> AppleStart(HttpRequest request)
    {
        if (!AppleConfigured) return Task.FromResult(Error("Sign in with Apple is not configured on this server.", 503));
        var challenge = CreateChallenge(request, "apple");
        var url = "https://appleid.apple.com/auth/authorize?" + Query(new() {
            ["client_id"] = Env("APPLE_SIGN_IN_CLIENT_ID"), ["redirect_uri"] = Env("APPLE_SIGN_IN_REDIRECT_URI"), ["response_type"] = "code",
            ["response_mode"] = "form_post", ["state"] = challenge.State, ["nonce"] = Hash(challenge.Verifier)
        });
        return Task.FromResult(Results.Ok(new { challengeId = challenge.Id, secret = challenge.Secret, url }));
    }

    /// <summary>Apple posts the authorization (or the player's cancellation) here from the browser.</summary>
    private static async Task<IResult> AppleCallback(HttpRequest request)
    {
        if (!AppleConfigured) return Error("Sign in with Apple unavailable.", 503);
        IFormCollection form;
        try { form = request.HasFormContentType ? await request.ReadFormAsync() : FormCollection.Empty; }
        catch (Exception e) when (e is InvalidDataException or IOException) { form = FormCollection.Empty; }
        var state = form["state"].ToString(); var code = form["code"].ToString();
        if (state.Length != 64) return Error("Sign-in was cancelled or expired.");
        string id, verifier;
        using (var conn = Database.Open())
        using (var cmd = conn.CreateCommand())
        {
            // A cancelled sign-in fails the challenge at once, so the game stops waiting for it.
            cmd.CommandText = code.Length is > 0 and <= 4096
                ? "UPDATE account_challenges SET status='verifying' WHERE state=@state AND provider='apple' AND status='pending' AND expires_at>@now RETURNING id, verifier"
                : "UPDATE account_challenges SET status='failed', verifier='' WHERE state=@state AND provider='apple' AND status='pending' AND expires_at>@now RETURNING id, verifier";
            cmd.Parameters.AddWithValue("@state", state); cmd.Parameters.AddWithValue("@now", Now);
            using var reader = cmd.ExecuteReader(); if (!reader.Read()) return Error("Sign-in expired.");
            id = reader.GetString(0); verifier = reader.GetString(1);
        }
        if (code.Length is 0 or > 4096) return Error("Sign-in was cancelled. Return to the game to choose another way.");
        try
        {
            var fields = new Dictionary<string, string> {
                ["client_id"] = Env("APPLE_SIGN_IN_CLIENT_ID"), ["client_secret"] = AppleClientSecret(), ["code"] = code,
                ["grant_type"] = "authorization_code", ["redirect_uri"] = Env("APPLE_SIGN_IN_REDIRECT_URI")
            };
            string tokens;
            if (TestAppleToken != null) tokens = await TestAppleToken(fields);
            else
            {
                using var response = await Apple.PostAsync("https://appleid.apple.com/auth/token", new FormUrlEncodedContent(fields));
                response.EnsureSuccessStatusCode(); tokens = await response.Content.ReadAsStringAsync();
            }
            using var json = JsonDocument.Parse(tokens);
            var subject = await VerifyAppleIdToken(Text(json.RootElement, "id_token"), Hash(verifier));
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET status='ready', subject=@subject, verifier='' WHERE id=@id AND expires_at>@now";
            cmd.Parameters.AddWithValue("@subject", subject); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@now", Now); cmd.ExecuteNonQuery();
            return SignedInPage();
        }
        catch
        {
            using var conn = Database.Open(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE account_challenges SET status='failed', verifier='' WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
            return Error("Sign in with Apple failed. Return to the game and try again.", 502);
        }
    }

    /// <summary>Apple's client secret: a short-lived ES256 JWT signed with the team's Sign in with Apple key (.p8).</summary>
    private static string AppleClientSecret()
    {
        var key = Env("APPLE_SIGN_IN_PRIVATE_KEY") is { Length: > 0 } inline ? inline.Replace("\\n", "\n") : File.ReadAllText(Env("APPLE_SIGN_IN_PRIVATE_KEY_PATH"));
        var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = Env("APPLE_SIGN_IN_KEY_ID") }));
        var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = Env("APPLE_SIGN_IN_TEAM_ID"), iat = Now, exp = Now + 300, aud = AppleIssuer, sub = Env("APPLE_SIGN_IN_CLIENT_ID")
        }));
        using var ecdsa = ECDsa.Create(); ecdsa.ImportFromPem(key);
        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return header + "." + payload + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>Checks Apple's RS256 signature against its published keys, then the issuer, audience, expiry and nonce.</summary>
    private static async Task<string> VerifyAppleIdToken(string token, string nonce)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw new CryptographicException("Malformed Apple ID token.");
        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        if (Text(header.RootElement, "alg") != "RS256") throw new CryptographicException("Unexpected Apple ID token algorithm.");
        var kid = Text(header.RootElement, "kid");
        var jwk = await AppleKey(kid, refresh: false) ?? await AppleKey(kid, refresh: true) ?? throw new CryptographicException("Unknown Apple signing key.");
        using var rsa = RSA.Create(new RSAParameters {
            Modulus = Base64Url.DecodeFromChars(Text(jwk, "n")), Exponent = Base64Url.DecodeFromChars(Text(jwk, "e"))
        });
        if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException("Apple ID token signature mismatch.");
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1])); var body = claims.RootElement;
        var expires = body.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds) ? seconds : 0;
        var subject = Text(body, "sub");
        if (Text(body, "iss") != AppleIssuer || Text(body, "aud") != Env("APPLE_SIGN_IN_CLIENT_ID") || expires < Now - 60 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Text(body, "nonce")), Encoding.UTF8.GetBytes(nonce)) ||
            subject.Length is 0 or > 256)
            throw new CryptographicException("Apple ID token does not match this sign-in.");
        return subject;
    }

    private static async Task<JsonElement?> AppleKey(string kid, bool refresh)
    {
        var cached = _appleKeys;
        if (refresh || cached == null || cached.Expires < DateTime.UtcNow || TestAppleKeys != null)
        {
            using var json = JsonDocument.Parse(TestAppleKeys != null ? await TestAppleKeys() : await Apple.GetStringAsync("https://appleid.apple.com/auth/keys"));
            cached = new AppleKeySet(DateTime.UtcNow.AddHours(6), json.RootElement.Clone());
            if (TestAppleKeys == null) _appleKeys = cached;
        }
        if (cached.Keys.ValueKind != JsonValueKind.Object || !cached.Keys.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array) return null;
        foreach (var key in keys.EnumerateArray())
            if (Text(key, "kid") == kid && Text(key, "kty") == "RSA") return key.Clone();
        return null;
    }

    private static async Task<IResult> PlayGames(HttpRequest request)
    {
        if (!PlayGamesConfigured) return Error("Google Play Games sign-in is not configured on this server.", 503);
        using var document = await JsonDocument.ParseAsync(request.Body);
        var code = Text(document.RootElement, "authCode");
        if (code.Length is 0 or > 4096) return Error("Google Play Games sign-in failed. Try again.");
        string player;
        try { player = await (TestPlayGamesExchange ?? ExchangePlayGamesCode)(code); }
        catch { return Error("Google Play Games could not confirm this player. Try again.", 502); }
        if (player.Length is 0 or > 256) return Error("Google Play Games could not confirm this player. Try again.", 502);
        var guest = Guest(request);
        using var conn = Database.Open(); using var tx = conn.BeginTransaction();
        var result = Complete(conn, tx, "playgames", player, guest); tx.Commit(); return result;
    }

    private static async Task<string> ExchangePlayGamesCode(string code)
    {
        // Play Games server auth codes are exchanged without a redirect URI.
        using var response = await Google.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["client_id"] = Env("PLAY_GAMES_CLIENT_ID"), ["client_secret"] = Env("PLAY_GAMES_CLIENT_SECRET"), ["code"] = code,
            ["redirect_uri"] = "", ["grant_type"] = "authorization_code"
        }));
        response.EnsureSuccessStatusCode(); using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var playerRequest = new HttpRequestMessage(HttpMethod.Get, "https://games.googleapis.com/games/v1/players/me");
        playerRequest.Headers.Authorization = new("Bearer", Text(tokens.RootElement, "access_token"));
        using var playerResponse = await Google.SendAsync(playerRequest); playerResponse.EnsureSuccessStatusCode();
        using var player = JsonDocument.Parse(await playerResponse.Content.ReadAsStringAsync());
        return Text(player.RootElement, "playerId");
    }

    /// <summary>
    /// Verifies a GameKit identity signature: Apple signs player ID + bundle ID + big-endian
    /// millisecond timestamp + salt with the certificate published at an apple.com URL.
    /// </summary>
    private static async Task<IResult> GameCenter(HttpRequest request)
    {
        if (!GameCenterConfigured) return Error("Game Center sign-in is not configured on this server.", 503);
        using var document = await JsonDocument.ParseAsync(request.Body); var body = document.RootElement;
        var playerId = Text(body, "playerId"); var keyUrl = Text(body, "publicKeyUrl");
        byte[] signature, salt;
        try { signature = Convert.FromBase64String(Text(body, "signature")); salt = Convert.FromBase64String(Text(body, "salt")); }
        catch (FormatException) { return Error("Game Center sign-in failed. Try again."); }
        var timestamp = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("timestamp", out var value) && value.TryGetInt64(out var ms) ? ms : 0;
        if (playerId.Length is 0 or > 256 || signature.Length is 0 or > 1024 || salt.Length is 0 or > 256 ||
            !Uri.TryCreate(keyUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
            !url.Host.EndsWith(".apple.com", StringComparison.OrdinalIgnoreCase))
            return Error("Game Center sign-in failed. Try again.");
        var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
        if (age is > 15 * 60_000 or < -5 * 60_000) return Error("Game Center sign-in expired. Try again.");
        try
        {
            var certificate = await GameCenterCertificate(url);
            var player = Encoding.UTF8.GetBytes(playerId); var bundle = Encoding.UTF8.GetBytes(GameCenterBundleId);
            var payload = new byte[player.Length + bundle.Length + 8 + salt.Length];
            player.CopyTo(payload, 0); bundle.CopyTo(payload, player.Length);
            BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(player.Length + bundle.Length), (ulong)timestamp);
            salt.CopyTo(payload, player.Length + bundle.Length + 8);
            using var key = certificate.GetRSAPublicKey() ?? throw new CryptographicException("Game Center key is not RSA.");
            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new CryptographicException("Game Center signature mismatch.");
        }
        catch { return Error("Game Center could not confirm this player. Try again.", 401); }
        var guest = Guest(request);
        using var conn = Database.Open(); using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            // Each signature signs in once; its row lives as long as the timestamp window that admits it.
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM account_challenges WHERE expires_at <= @now"; cmd.Parameters.AddWithValue("@now", Now); cmd.ExecuteNonQuery();
            cmd.CommandText = """
                INSERT INTO account_challenges(id, secret_hash, provider, state, expires_at, status)
                VALUES(@id,'','gamecenter',@state,@expires,'used') ON CONFLICT(state) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("@id", RandomToken()); cmd.Parameters.AddWithValue("@state", Convert.ToHexStringLower(SHA256.HashData(signature)));
            cmd.Parameters.AddWithValue("@expires", Now + 20 * 60);
            if (cmd.ExecuteNonQuery() == 0) return Error("Game Center sign-in expired. Try again.");
        }
        var result = Complete(conn, tx, "gamecenter", playerId, guest); tx.Commit(); return result;
    }

    private static async Task<X509Certificate2> GameCenterCertificate(Uri url)
    {
        if (GameCenterCertificates.TryGetValue(url.AbsoluteUri, out var cached) && cached.NotAfter > DateTime.Now) return cached;
        var bytes = TestGameCenterCertificate != null ? await TestGameCenterCertificate(url) : await Apple.GetByteArrayAsync(url);
        var certificate = X509CertificateLoader.LoadCertificate(bytes);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (TestGameCenterRoot != null)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(TestGameCenterRoot);
        }
        if (!chain.Build(certificate)) throw new CryptographicException("Untrusted Game Center certificate.");
        if (TestGameCenterRoot == null) GameCenterCertificates[url.AbsoluteUri] = certificate;
        return certificate;
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
            provider, accountLabel = provider switch { "email" => subject, "playgames" => "Google Play Games", "gamecenter" => "Game Center", "apple" => "Apple account", _ => "Google account" },
            isNewAccount, syncedAtUnixSeconds = Now });
    }
}
