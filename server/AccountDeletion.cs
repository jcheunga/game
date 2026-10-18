using System.Data.Common;
using System.Security.Cryptography;

namespace CrownroadServer;

/// <summary>
/// Deletes a player's account: everything stored under the profile ID, its linked sign-ins and its
/// sessions. Purchase records stay for tax, refunds and fraud checks, and so a store transaction is
/// never credited twice, but they move to a random pseudonym with no player behind it. Sign in with
/// Apple is revoked with the refresh token sealed at sign-in. A revocation Apple cannot take at once
/// waits in pending_token_revocations, and StaleDataCleanup retries it.
/// </summary>
public static class AccountDeletion
{
    public const string DeletedProfilePrefix = "DELETED-";
    private const int MaxRevocationAttempts = 30;
    private const long RevocationLeaseSeconds = 300;
    // Longer than any session lives, so every session of a deleted account is remembered until it would have expired.
    private const long DeletedSessionRetentionSeconds = 31L * 24 * 3600;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Purchases of deleted accounts carry this pseudonym. No player, wallet or session exists for it.</summary>
    public static bool IsDeletedProfile(string profileId) =>
        profileId.StartsWith(DeletedProfilePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The answer to a session of a deleted account: the game then resets itself to a new guest.</summary>
    public static IResult Deleted() => Results.Json(new
    {
        error = "account_deleted",
        message = "This account was deleted."
    }, statusCode: StatusCodes.Status410Gone);

    /// <summary>Whether the request carries a session of an account that has since been deleted.</summary>
    public static bool WasDeletedSession(HttpRequest request)
    {
        var hash = SessionAuth.RequestTokenHash(request);
        if (hash.Length == 0) return false;
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM deleted_sessions WHERE token_hash = @hash";
        cmd.Parameters.AddWithValue("@hash", hash);
        return cmd.ExecuteScalar() != null;
    }

    /// <summary>Erases the profile in one transaction. Returns the IDs of the sign-in revocations it queued.</summary>
    public static List<string> Erase(string profileId)
    {
        var now = Now;
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        DbCommand Command(string sql, params (string Name, object Value)[] values)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@pid", profileId);
            cmd.Parameters.AddWithValue("@now", now);
            foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
            return cmd;
        }
        void Run(string sql, params (string Name, object Value)[] values) { using var cmd = Command(sql, values); cmd.ExecuteNonQuery(); }
        List<string> Column(string sql)
        {
            using var cmd = Command(sql);
            using var reader = cmd.ExecuteReader();
            var values = new List<string>();
            while (reader.Read()) values.Add(reader.GetString(0));
            return values;
        }

        // Linked sign-ins first. DELETE … RETURNING locks the rows, so a concurrent deletion of the
        // same account waits here and then finds nothing left to revoke.
        var credentials = new List<(string Provider, string Credential)>();
        using (var cmd = Command("DELETE FROM account_identities WHERE profile_id = @pid RETURNING provider, credential"))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) credentials.Add((reader.GetString(0), reader.GetString(1)));
        var revocations = new List<string>();
        foreach (var (provider, credential) in credentials.Where(c => c.Provider == "apple" && c.Credential.Length > 0))
        {
            var id = $"REV-{Guid.NewGuid():N}";
            Run("""
                INSERT INTO pending_token_revocations (id, provider, credential, attempts, next_attempt_at, created_at)
                VALUES (@id, @provider, @credential, 0, @now, @now)
            """, ("@id", id), ("@provider", provider), ("@credential", credential));
            revocations.Add(id);
        }
        Run("DELETE FROM account_challenges WHERE guest_profile = @pid");

        // Only the hashes of the account's live sessions are remembered, so a retried deletion succeeds again.
        Run("""
            INSERT INTO deleted_sessions (token_hash, deleted_at)
            SELECT token_hash, @now FROM auth_sessions WHERE profile_id = @pid AND revoked_at = 0 AND expires_at > @now
            ON CONFLICT(token_hash) DO NOTHING
        """);
        Run("DELETE FROM auth_sessions WHERE profile_id = @pid");

        foreach (var table in new[] { "cloud_saves", "challenge_results", "achievements", "daily_completions", "arena_snapshots",
            "arena_results", "raid_contributions", "analytics_events", "crash_reports", "room_telemetry", "room_seats",
            "wallet_ledger", "player_wallets" })
            Run($"DELETE FROM {table} WHERE profile_id = @pid");
        Run("UPDATE arena_results SET opponent_profile_id = '' WHERE opponent_profile_id = @pid");
        Run("DELETE FROM friendships WHERE profile_id = @pid OR friend_id = @pid");
        Run("DELETE FROM friend_gifts WHERE sender_id = @pid OR receiver_id = @pid");
        Run("DELETE FROM room_reports WHERE reporter_profile_id = @pid OR target_profile_id = @pid");

        // Rooms are titled with their host's callsign: the ones this player hosted go, with every seat in them.
        Run("DELETE FROM room_telemetry WHERE room_id IN (SELECT room_id FROM rooms WHERE host_profile_id = @pid)");
        Run("DELETE FROM room_seats WHERE room_id IN (SELECT room_id FROM rooms WHERE host_profile_id = @pid)");
        Run("DELETE FROM rooms WHERE host_profile_id = @pid");

        // A guild outlives its leader: it passes to the top contributor, and an empty guild is removed.
        var led = Column("SELECT guild_id FROM guilds WHERE leader_profile_id = @pid");
        Run("DELETE FROM guild_members WHERE profile_id = @pid");
        foreach (var guildId in led)
        {
            using var successor = Command("""
                SELECT profile_id FROM guild_members WHERE guild_id = @gid
                ORDER BY contribution_points DESC, joined_at ASC, id ASC LIMIT 1
            """, ("@gid", guildId));
            if (successor.ExecuteScalar() is string next)
                Run("UPDATE guilds SET leader_profile_id = @next WHERE guild_id = @gid", ("@next", next), ("@gid", guildId));
            else
                Run("DELETE FROM guilds WHERE guild_id = @gid", ("@gid", guildId));
        }

        Run("UPDATE purchases SET profile_id = @pseudonym WHERE profile_id = @pid",
            ("@pseudonym", DeletedProfilePrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)).ToUpperInvariant()));
        Run("DELETE FROM players WHERE profile_id = @pid");
        tx.Commit();
        return revocations;
    }

    /// <summary>
    /// Sends queued revocations to Apple: the given IDs, or every one that is due. Returns true when none
    /// of them is left waiting.
    /// </summary>
    public static async Task<bool> RevokeQueuedAsync(IReadOnlyCollection<string>? ids = null)
    {
        var now = Now;
        var targets = ids?.ToList() ?? [];
        if (ids == null)
        {
            using var conn = await Database.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM pending_token_revocations WHERE next_attempt_at <= @now ORDER BY next_attempt_at LIMIT 100";
            cmd.Parameters.AddWithValue("@now", now);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) targets.Add(reader.GetString(0));
        }

        var settled = true;
        foreach (var id in targets)
        {
            // Claim the row for a few minutes, so another replica's retry does not send it at the same time.
            string credential; int attempts;
            using (var conn = await Database.OpenAsync())
            using (var claim = conn.CreateCommand())
            {
                claim.CommandText = """
                    UPDATE pending_token_revocations SET next_attempt_at = @lease
                    WHERE id = @id AND next_attempt_at <= @now
                    RETURNING credential, attempts
                """;
                claim.Parameters.AddWithValue("@lease", now + RevocationLeaseSeconds);
                claim.Parameters.AddWithValue("@id", id);
                claim.Parameters.AddWithValue("@now", now);
                using var reader = await claim.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) { settled = false; continue; }
                credential = reader.GetString(0); attempts = Database.ToInt32(reader.GetValue(1));
            }

            AccountAuth.RevokeOutcome outcome;
            try { outcome = await AccountAuth.RevokeAppleToken(CredentialProtection.Unprotect(credential, AccountAuth.AppleRefreshTokenPurpose)); }
            catch (Exception e) when (e is CryptographicException or InvalidOperationException or FormatException)
            {
                // A missing or changed ACCOUNT_TOKEN_KEY. Keep the token until the key is restored.
                Console.Error.WriteLine($"A queued Apple token revocation could not be unsealed ({e.GetType().Name}); it will be retried.");
                outcome = AccountAuth.RevokeOutcome.RetryLater;
            }

            using (var conn = await Database.OpenAsync())
            using (var update = conn.CreateCommand())
            {
                update.Parameters.AddWithValue("@id", id);
                if (outcome != AccountAuth.RevokeOutcome.RetryLater || attempts + 1 >= MaxRevocationAttempts)
                {
                    if (outcome == AccountAuth.RevokeOutcome.RetryLater)
                        Console.Error.WriteLine($"Gave up revoking a deleted account's Apple sign-in after {MaxRevocationAttempts} attempts.");
                    update.CommandText = "DELETE FROM pending_token_revocations WHERE id = @id";
                }
                else
                {
                    // 15 minutes, doubling up to a day: about three weeks of attempts in all.
                    update.CommandText = "UPDATE pending_token_revocations SET attempts = @attempts, next_attempt_at = @next WHERE id = @id";
                    update.Parameters.AddWithValue("@attempts", attempts + 1);
                    update.Parameters.AddWithValue("@next", now + Math.Min(24L * 3600, 900L << Math.Min(attempts, 10)));
                }
                await update.ExecuteNonQueryAsync();
            }
            if (outcome == AccountAuth.RevokeOutcome.RetryLater) settled = false;
        }
        return settled;
    }

    /// <summary>Forgets deleted accounts' session hashes once those sessions would have expired anyway.</summary>
    public static int PurgeDeletedSessions(DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM deleted_sessions WHERE deleted_at < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", Now - DeletedSessionRetentionSeconds);
        return cmd.ExecuteNonQuery();
    }
}
