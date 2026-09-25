using System.Security.Cryptography;
using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CrownroadServer;

/// <summary>
/// Server-owned authentication for anonymous device accounts. A profile ID is an
/// identifier only; every state-changing request must also carry one of these
/// random bearer sessions. Account-provider login can be layered on top later.
/// </summary>
public static class SessionAuth
{
    private const int SessionLifetimeDays = 30;
    private const int TokenByteCount = 32;

    public sealed record Session(string ProfileId, string SessionId, long ExpiresAtUnixSeconds);

    public static string Issue(DbConnection conn, DbTransaction tx, string profileId, long now)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenByteCount));
        var sessionId = $"SES-{Guid.NewGuid():N}";
        var expiresAt = now + (SessionLifetimeDays * 24L * 60L * 60L);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO auth_sessions (session_id, profile_id, token_hash, created_at, expires_at, revoked_at, last_seen_at)
            VALUES (@sid, @pid, @hash, @now, @expires, 0, @now)
        """;
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.Parameters.AddWithValue("@hash", Hash(token));
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@expires", expiresAt);
        cmd.ExecuteNonQuery();

        return token;
    }

    public static bool TryAuthorize(HttpRequest request, string claimedProfileId, out Session? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(claimedProfileId)) return false;

        var token = ReadBearerToken(request);
        if (string.IsNullOrWhiteSpace(token)) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, profile_id, expires_at
            FROM auth_sessions
            WHERE token_hash = @hash AND revoked_at = 0 AND expires_at > @now
            LIMIT 1
        """;
        cmd.Parameters.AddWithValue("@hash", Hash(token));
        cmd.Parameters.AddWithValue("@now", now);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;

        var profileId = reader.GetString(1);
        if (!string.Equals(profileId, claimedProfileId, StringComparison.Ordinal)) return false;

        var sessionId = reader.GetString(0);
        var expiresAt = reader.GetInt64(2);
        reader.Close();

        using var touch = conn.CreateCommand();
        touch.CommandText = "UPDATE auth_sessions SET last_seen_at = @now WHERE session_id = @sid";
        touch.Parameters.AddWithValue("@now", now);
        touch.Parameters.AddWithValue("@sid", sessionId);
        touch.ExecuteNonQuery();

        session = new Session(profileId, sessionId, expiresAt);
        return true;
    }

    public static bool PlayerExists(DbConnection conn, string profileId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM players WHERE profile_id = @pid LIMIT 1";
        cmd.Parameters.AddWithValue("@pid", profileId);
        return cmd.ExecuteScalar() != null;
    }

    public static IResult Unauthorized() => Results.Json(new
    {
        error = "unauthorized",
        message = "A valid player session is required for this request."
    }, statusCode: StatusCodes.Status401Unauthorized);

    private static string ReadBearerToken(HttpRequest request)
    {
        var value = request.Headers.Authorization.FirstOrDefault() ?? "";
        const string prefix = "Bearer ";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : "";
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
