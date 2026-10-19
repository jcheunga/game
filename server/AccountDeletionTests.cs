using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrownroadServer.Tests;

public static partial class ServerTests
{
    /// <summary>Every column that holds a player's profile ID, and so must be cleared when the account is deleted.</summary>
    private static readonly (string Table, string Column)[] ProfileColumns =
    [
        ("players", "profile_id"), ("account_identities", "profile_id"), ("account_challenges", "guest_profile"),
        ("auth_sessions", "profile_id"), ("cloud_saves", "profile_id"), ("challenge_results", "profile_id"),
        ("achievements", "profile_id"), ("daily_completions", "profile_id"), ("arena_snapshots", "profile_id"),
        ("arena_results", "profile_id"), ("arena_results", "opponent_profile_id"), ("raid_contributions", "profile_id"),
        ("analytics_events", "profile_id"), ("crash_reports", "profile_id"), ("rooms", "host_profile_id"),
        ("room_seats", "profile_id"), ("room_telemetry", "profile_id"), ("room_reports", "reporter_profile_id"),
        ("room_reports", "target_profile_id"), ("guilds", "leader_profile_id"), ("guild_members", "profile_id"),
        ("friendships", "profile_id"), ("friendships", "friend_id"), ("friend_gifts", "sender_id"),
        ("friend_gifts", "receiver_id"), ("player_wallets", "profile_id"), ("wallet_ledger", "profile_id"),
        ("purchases", "profile_id"), ("wallet_adjustments", "profile_id"),
    ];

    private static object? DbScalar(string sql, params (string Name, object Value)[] values)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteScalar();
    }

    private static long DbCount(string sql, params (string Name, object Value)[] values) => Database.ToInt64(DbScalar(sql, values));

    private static async Task<(HttpStatusCode Status, JsonElement Body)> DeleteAccount(HttpClient client, string profile, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/delete") { Content = JsonContent.Create(new { }) };
        if (profile.Length > 0) request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        return (response.StatusCode, json.RootElement.Clone());
    }

    private static async Task<HttpStatusCode> SessionStatus(HttpClient client, string profile, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/cloud-save/info?profileId={profile}");
        request.Headers.Add("X-Convoy-Profile", profile);
        request.Headers.Authorization = new("Bearer", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<(HttpStatusCode Status, string Error)> SendAs(HttpClient client, HttpMethod method, string url, string profile, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = JsonContent.Create(body);
        if (profile.Length > 0) request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        var error = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var value) ? value.GetString() ?? "" : "";
        return (response.StatusCode, error);
    }

    /// <summary>
    /// A second device still signed in to a deleted account is told the account is gone (410
    /// "account_deleted") by the calls the game makes routinely, and its profile is not registered again.
    /// Requests without a session, or with a token that never was one, are answered as before.
    /// </summary>
    private static async Task TestAccountDeletionTellsOtherDevices(HttpClient client)
    {
        const string profile = "TEST-DEL-OTHER", email = "other-device@example.com";
        var code = "";
        AccountAuth.TestMailSender = (_, value) => { code = value; return Task.CompletedTask; };
        try
        {
            async Task<string> EmailSignIn(string guest = "", string token = "")
            {
                var challenge = await AccountPost(client, "/auth/email/start", new { email }, guest, token);
                var session = await AccountPost(client, "/auth/email/verify", new { challengeId = challenge.GetProperty("challengeId").GetString(),
                    secret = challenge.GetProperty("secret").GetString(), code });
                Assert(session.GetProperty("playerProfileId").GetString() == profile, "Both devices sign in to the same account");
                return session.GetProperty("sessionToken").GetString()!;
            }
            await EnsureSession(client, profile);
            var device = await EmailSignIn(profile, SessionTokens[profile]);
            var otherDevice = await EmailSignIn();
            var save = new { profileId = profile, saveData = "{\"PlayerCallsign\":\"Survivor\"}", saveVersion = 3 };
            Assert((await SendAs(client, HttpMethod.Post, "/cloud-save/upload", profile, otherDevice, save)).Status == HttpStatusCode.OK,
                "The other device backs up before the deletion");
            Assert((await DeleteAccount(client, profile, device)).Status == HttpStatusCode.OK, "One device deletes the account");

            var profileSync = new { profile = new { playerProfileId = profile, playerCallsign = "Survivor" } };
            var routine = new (string Name, HttpMethod Method, string Url, object? Body)[]
            {
                ("player profile", HttpMethod.Post, "/player-profile", profileSync),
                ("cloud save upload", HttpMethod.Post, "/cloud-save/upload", save),
                ("cloud save info", HttpMethod.Get, $"/cloud-save/info?profileId={profile}", null),
                ("cloud save download", HttpMethod.Get, $"/cloud-save/download?profileId={profile}", null),
                ("wallet", HttpMethod.Get, $"/wallet?profileId={profile}", null),
                ("challenge sync", HttpMethod.Post, "/challenge-sync", new { submissions = new[] { new { submissionId = "SUB-DEL-OTHER", code = "CH-01-DAILY-1001", score = 5 } } }),
                ("achievement sync", HttpMethod.Post, "/achievements/sync", new { profileId = profile, achievementIds = new[] { "first_victory" } }),
                ("purchase validation", HttpMethod.Post, "/purchase/validate", new { profileId = profile, productId = "gold_pouch", platform = "stripe",
                    receiptToken = "receipt-del-other", transactionId = "txn-del-other" }),
                ("sign-out", HttpMethod.Post, "/auth/signout", new { }),
            };
            foreach (var (name, method, url, body) in routine)
            {
                var (status, error) = await SendAs(client, method, url, profile, otherDevice, body);
                Assert(status == HttpStatusCode.Gone && error == "account_deleted", $"The other device's {name} hears the account was deleted (HTTP {(int)status} {error})");
            }
            Assert(DbCount("SELECT COUNT(*) FROM players WHERE profile_id = @pid", ("@pid", profile)) == 0 &&
                DbCount("SELECT COUNT(*) FROM auth_sessions WHERE profile_id = @pid", ("@pid", profile)) == 0 &&
                DbCount("SELECT COUNT(*) FROM cloud_saves WHERE profile_id = @pid", ("@pid", profile)) == 0 &&
                DbCount("SELECT COUNT(*) FROM purchases WHERE transaction_id = 'txn-del-other'") == 0,
                "The other device's calls bring no player, session, save or purchase back");

            // Everything without a session of the deleted account is answered as before.
            Assert((await SendAs(client, HttpMethod.Get, $"/cloud-save/info?profileId={profile}", profile, "")).Status == HttpStatusCode.Unauthorized &&
                (await SendAs(client, HttpMethod.Get, $"/cloud-save/info?profileId={profile}", profile, "never-a-session")) is (HttpStatusCode.Unauthorized, "unauthorized"),
                "A request with no session, or a token that never was one, is still refused as unauthorized");
            Assert((await SendAs(client, HttpMethod.Post, "/player-profile", "TEST-DEL-NEWCOMER", "never-a-session",
                    new { profile = new { playerProfileId = "TEST-DEL-NEWCOMER", playerCallsign = "Newcomer" } })).Status == HttpStatusCode.OK,
                "A new profile still registers whatever stale token it carries");
            Assert(await SessionStatus(client, profile, "never-a-session") == HttpStatusCode.Unauthorized, "A made-up token is not mistaken for a deleted session");

            // Once the deleted sessions are forgotten (after any session would have expired), the old answers return.
            DbScalar("UPDATE deleted_sessions SET deleted_at = 0");
            using (var conn = Database.Open()) AccountDeletion.PurgeDeletedSessions(conn);
            Assert(await SessionStatus(client, profile, otherDevice) == HttpStatusCode.Unauthorized, "A purged deleted session is refused like any expired one");
        }
        finally { AccountAuth.TestMailSender = null; }
    }

    private static async Task TestAccountDeletion(HttpClient client)
    {
        const string profile = "TEST-DEL", friend = "TEST-DEL-FRIEND", member = "TEST-DEL-MEMBER";
        const string email = "deleted-player@example.com";
        var code = "";
        AccountAuth.TestMailSender = (_, value) => { code = value; return Task.CompletedTask; };
        try
        {
            async Task<JsonElement> EmailSignIn(string guest = "", string token = "")
            {
                var challenge = await AccountPost(client, "/auth/email/start", new { email }, guest, token);
                return await AccountPost(client, "/auth/email/verify", new { challengeId = challenge.GetProperty("challengeId").GetString(),
                    secret = challenge.GetProperty("secret").GetString(), code });
            }
            await EnsureSession(client, profile);
            var guestToken = SessionTokens[profile];
            var linked = await EmailSignIn(profile, guestToken);
            Assert(linked.GetProperty("playerProfileId").GetString() == profile, "The guest links an email before deleting");
            var device = linked.GetProperty("sessionToken").GetString()!;
            var otherDevice = (await EmailSignIn()).GetProperty("sessionToken").GetString()!;
            await AccountPost(client, "/auth/email/start", new { email = "pending-link@example.com" }, profile, device);

            // Something in every table that holds the profile.
            await Post(client, "/cloud-save/upload", new { profileId = profile, saveData = "{\"PlayerCallsign\":\"Doomed\"}", saveVersion = 3 }, profile);
            await Post(client, "/challenge-sync", new { submissions = new[] { new { submissionId = "SUB-DEL-1", code = "CH-01-DAILY-1001", score = 777 } } }, profile);
            await Post(client, "/achievements/sync", new { profileId = profile, achievementIds = new[] { "first_victory" } }, profile);
            await Post(client, "/daily/complete", new { profileId = profile, date = "2026-10-18", score = 900 }, profile);
            await Post(client, "/purchase/validate", new { profileId = profile, productId = "gold_pouch", platform = "stripe",
                receiptToken = "receipt-del", transactionId = "txn-del-1" }, profile);
            await Post(client, "/purchase/validate", new { profileId = profile, productId = "gold_pouch", platform = "stripe",
                receiptToken = "receipt-del-2", transactionId = "txn-del-2" }, profile);
            Assert((await AdminRefund(client, new { transactionId = "txn-del-2" })).Body.GetProperty("status").GetString() == "refunded",
                "The test refunds a purchase before deleting");
            await Post(client, "/analytics/ingest", new { profileId = profile, events = new[] { new { type = "stage_won", data = "1" } } }, profile);
            await Post(client, "/crash-report", new { profileId = profile, errorType = "Test", errorMessage = "boom" }, profile);
            using (var hosted = await Post(client, "/challenge-room-create", new { room = new { playerCallsign = "Doomed", boardCode = "CH-01-DAILY-1001" } }, profile))
            {
                var hostedRoom = hosted.RootElement.GetProperty("roomId").GetString()!;
                await Post(client, "/challenge-room-join", new { join = new { roomId = hostedRoom, playerCallsign = "Friend" } }, friend);
                await Post(client, "/challenge-room-report", new { report = new { roomId = hostedRoom, targetProfileId = friend, reason = "test" } }, profile);
            }
            using (var friendsRoom = await Post(client, "/challenge-room-create", new { room = new { playerCallsign = "Friend", boardCode = "CH-01-DAILY-1001" } }, friend))
            {
                var room = friendsRoom.RootElement.GetProperty("roomId").GetString()!;
                await Post(client, "/challenge-room-join", new { join = new { roomId = room, playerCallsign = "Doomed" } }, profile);
                await Post(client, "/challenge-room-telemetry", new { roomId = room, elapsedSeconds = 3.5, hullRatio = .8, enemyDefeats = 2 }, profile);
                await Post(client, "/challenge-room-report", new { report = new { roomId = room, targetProfileId = profile, reason = "test" } }, friend);
            }
            await Post(client, "/arena/snapshot", new { profileId = profile, deckUnitIds = "player_brawler", powerRating = 10 }, profile);
            await Post(client, "/arena/snapshot", new { profileId = friend, deckUnitIds = "player_brawler", powerRating = 10 }, friend);
            await Post(client, "/arena/result", new { profileId = profile, opponentProfileId = friend, won = true, ratingBefore = 1000 }, profile);
            await Post(client, "/arena/result", new { profileId = friend, opponentProfileId = profile, won = false, ratingBefore = 1000 }, friend);
            await Post(client, "/raid/contribute", new { profileId = profile, damage = 50, weekId = "2026-W42" }, profile);
            string sharedGuild, soloGuild;
            using (var guild = await Post(client, "/guild/create", new { profileId = profile, name = "Doomed band" }, profile))
                sharedGuild = guild.RootElement.GetProperty("guildId").GetString()!;
            using (var guild = await Post(client, "/guild/create", new { profileId = profile, name = "Doomed alone" }, profile))
                soloGuild = guild.RootElement.GetProperty("guildId").GetString()!;
            await Post(client, "/guild/join", new { profileId = member, guildId = sharedGuild }, member);
            await Post(client, "/guild/contribute", new { profileId = profile, guildId = sharedGuild, points = 40 }, profile);
            await Post(client, "/friends/add", new { profileId = profile, friendId = friend }, profile);
            await Post(client, "/friends/add", new { profileId = friend, friendId = profile }, friend);
            await Post(client, "/friends/gift", new { profileId = profile, friendId = friend }, profile);
            await Post(client, "/friends/gift", new { profileId = friend, friendId = profile }, friend);
            foreach (var (table, column) in ProfileColumns)
                Assert(DbCount($"SELECT COUNT(*) FROM {table} WHERE {column} = @pid", ("@pid", profile)) > 0,
                    $"The test fills {table}.{column} before deleting");

            var (status, body) = await DeleteAccount(client, profile, device);
            Assert(status == HttpStatusCode.OK && body.GetProperty("status").GetString() == "deleted" &&
                !body.GetProperty("alreadyDeleted").GetBoolean() && !body.GetProperty("signInRevocationPending").GetBoolean(),
                $"An authenticated player deletes their account (HTTP {(int)status})");

            foreach (var (table, column) in ProfileColumns)
                Assert(DbCount($"SELECT COUNT(*) FROM {table} WHERE {column} = @pid", ("@pid", profile)) == 0,
                    $"Deleting the account clears {table}.{column}");
            Assert(DbCount("SELECT COUNT(*) FROM account_identities WHERE subject = @email", ("@email", email)) == 0 &&
                DbCount("SELECT COUNT(*) FROM account_challenges WHERE subject = 'pending-link@example.com'") == 0,
                "The email address is gone from linked sign-ins and pending codes");
            var pseudonym = (string?)DbScalar("SELECT profile_id FROM purchases WHERE transaction_id = 'txn-del-1'") ?? "";
            Assert(pseudonym.StartsWith("DELETED-") && pseudonym.Length == 40, "The purchase record is kept under a random pseudonym");
            Assert(DbCount("SELECT COUNT(*) FROM players WHERE profile_id = @p", ("@p", pseudonym)) == 0 &&
                DbCount("SELECT COUNT(*) FROM player_wallets WHERE profile_id = @p", ("@p", pseudonym)) == 0,
                "No player or wallet exists behind the pseudonym");
            Assert((string?)DbScalar("SELECT a.profile_id FROM wallet_adjustments a JOIN purchases p ON p.purchase_id = a.purchase_id " +
                "WHERE p.transaction_id = 'txn-del-2'") == pseudonym, "A refund's record stays with its purchase under the pseudonym");
            var lateRefund = await AdminRefund(client, new { transactionId = "txn-del-1" });
            Assert(lateRefund.Status == HttpStatusCode.OK && lateRefund.Body.GetProperty("status").GetString() == "refunded" &&
                (string?)DbScalar("SELECT status FROM purchases WHERE transaction_id = 'txn-del-1'") == "refunded",
                $"A refund after deletion marks the kept purchase refunded ({lateRefund.Body})");
            Assert(DbCount("SELECT COUNT(*) FROM wallet_ledger WHERE profile_id = @p", ("@p", pseudonym)) == 0 &&
                DbCount("SELECT COUNT(*) FROM wallet_adjustments a JOIN purchases p ON p.purchase_id = a.purchase_id WHERE p.transaction_id = 'txn-del-1'") == 0,
                "A refund after deletion takes nothing back: no wallet or device is left");
            Assert((string?)DbScalar("SELECT leader_profile_id FROM guilds WHERE guild_id = @g", ("@g", sharedGuild)) == member,
                "A guild the player led passes to its remaining member");
            Assert(DbCount("SELECT COUNT(*) FROM guilds WHERE guild_id = @g", ("@g", soloGuild)) == 0, "A guild left empty is removed");
            Assert(DbCount("SELECT COUNT(*) FROM arena_results WHERE profile_id = @f AND opponent_profile_id = ''", ("@f", friend)) == 1,
                "The friend keeps their arena result without the deleted opponent");
            Assert(DbCount("SELECT COUNT(*) FROM rooms WHERE host_profile_id = @f", ("@f", friend)) == 1 &&
                DbCount("SELECT COUNT(*) FROM room_seats WHERE profile_id = @f", ("@f", friend)) == 1,
                "Another player's room stays; their seat in the deleted player's room goes with it");
            using (var friends = await Get(client, $"/friends/list?profileId={friend}"))
                Assert(friends.RootElement.GetProperty("friends").GetArrayLength() == 0, "The deleted player leaves friends' lists");

            foreach (var (name, token) in new[] { ("guest", guestToken), ("this device's", device), ("other device's", otherDevice) })
                Assert(await SessionStatus(client, profile, token) == HttpStatusCode.Gone, $"Deletion ends the {name} session, which is told the account is gone");

            (status, body) = await DeleteAccount(client, profile, device);
            Assert(status == HttpStatusCode.OK && body.GetProperty("alreadyDeleted").GetBoolean(), "Repeating the deletion answers deleted again");
            (status, body) = await DeleteAccount(client, profile, otherDevice);
            Assert(status == HttpStatusCode.OK && body.GetProperty("alreadyDeleted").GetBoolean(), "Another device's old session gets the same answer");
            Assert(DbCount("SELECT COUNT(*) FROM players WHERE profile_id = @pid", ("@pid", profile)) == 0 &&
                DbCount("SELECT COUNT(*) FROM purchases WHERE profile_id LIKE 'DELETED-%' AND transaction_id = 'txn-del-1'") == 1,
                "A repeated deletion changes nothing");
            Assert((await DeleteAccount(client, profile, "not-a-session")).Status == HttpStatusCode.Unauthorized &&
                (await DeleteAccount(client, profile, "")).Status == HttpStatusCode.Unauthorized &&
                (await DeleteAccount(client, "", device)).Status == HttpStatusCode.BadRequest,
                "Deletion needs a session of the account");
            SessionTokens.Remove(profile);

            await PostExpect(client, "/purchase/validate", new { profileId = "TEST-DEL-AGAIN", productId = "gold_pouch", platform = "stripe",
                receiptToken = "receipt-del", transactionId = "txn-del-1" }, "TEST-DEL-AGAIN", HttpStatusCode.Conflict);
            var again = await EmailSignIn();
            Assert(again.GetProperty("isNewAccount").GetBoolean() && again.GetProperty("playerProfileId").GetString() != profile,
                "The same email afterwards starts a new, empty account");
            await PostExpect(client, "/player-profile", new { profile = new { playerProfileId = pseudonym, playerCallsign = "Thief" } }, "",
                HttpStatusCode.BadRequest);
        }
        finally { AccountAuth.TestMailSender = null; }
    }

    /// <summary>
    /// Guards the deletion against new tables: every column that looks like it holds a profile ID must be
    /// one the deletion clears (ProfileColumns, checked by AccountDeletion's test).
    /// </summary>
    private static Task TestAccountDeletionCoversSchema()
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = Database.IsPostgres
            ? "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = current_schema()"
            : "SELECT m.name, p.name FROM sqlite_master m, pragma_table_info(m.name) p WHERE m.type = 'table'";
        var handled = ProfileColumns.ToHashSet();
        var missed = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var (table, column) = (reader.GetString(0), reader.GetString(1));
            if ((column.Contains("profile") || column is "friend_id" or "sender_id" or "receiver_id") && !handled.Contains((table, column)))
                missed.Add($"{table}.{column}");
        }
        Assert(missed.Count == 0, $"AccountDeletion does not clear {string.Join(", ", missed)}");
        return Task.CompletedTask;
    }

    private static async Task TestAccountDeletionRevokesApple(HttpClient client)
    {
        using var apple = new AppleSignInFixture();
        const string profile = "TEST-DEL-AP", subject = "001234.apple.deleted";
        await EnsureSession(client, profile);
        var first = await apple.SignIn(client, subject, "apple-refresh-first", profile, SessionTokens[profile]);
        var latest = await apple.SignIn(client, subject, "apple-refresh-latest");
        Assert(first.GetProperty("playerProfileId").GetString() == profile && latest.GetProperty("playerProfileId").GetString() == profile,
            "Two devices sign in to one Apple account");
        var stored = (string?)DbScalar("SELECT credential FROM account_identities WHERE provider = 'apple' AND subject = @s", ("@s", subject)) ?? "";
        Assert(stored.StartsWith("v1.") && !stored.Contains("apple-refresh"), "The Apple refresh token is stored encrypted");
        Assert(CredentialProtection.Unprotect(stored, AccountAuth.AppleRefreshTokenPurpose) == "apple-refresh-latest",
            "The newest sign-in's refresh token is kept");
        Assert(DbCount("SELECT COUNT(*) FROM account_challenges WHERE credential <> ''") == 0, "No sign-in challenge keeps a token once collected");

        var (status, body) = await DeleteAccount(client, profile, first.GetProperty("sessionToken").GetString()!);
        Assert(status == HttpStatusCode.OK && !body.GetProperty("signInRevocationPending").GetBoolean(), "Deleting an Apple account revokes at once");
        var revoked = apple.Revocations.Single();
        Assert(revoked["token"] == "apple-refresh-latest" && revoked["token_type_hint"] == "refresh_token" &&
            revoked["client_id"] == AppleSignInFixture.ClientId, "Apple's revoke call carries the stored refresh token");
        var secret = revoked["client_secret"].Split('.');
        Assert(secret.Length == 3 && apple.SignedByClientKey(secret), "Apple's revoke call is authenticated with the Sign in with Apple client secret");
        Assert(DbCount("SELECT COUNT(*) FROM pending_token_revocations") == 0 &&
            DbCount("SELECT COUNT(*) FROM account_identities WHERE subject = @s", ("@s", subject)) == 0, "Nothing about the Apple sign-in is left");
        SessionTokens.Remove(profile);

        // Apple unavailable: the account is still deleted, and the revocation waits for a retry.
        apple.RevokeAnswer = () => (503, "");
        var down = await apple.SignIn(client, "001234.apple.down", "apple-refresh-down");
        var downProfile = down.GetProperty("playerProfileId").GetString()!;
        (status, body) = await DeleteAccount(client, downProfile, down.GetProperty("sessionToken").GetString()!);
        Assert(status == HttpStatusCode.OK && body.GetProperty("signInRevocationPending").GetBoolean() &&
            DbCount("SELECT COUNT(*) FROM players WHERE profile_id = @p", ("@p", downProfile)) == 0,
            "When Apple is down the account is deleted and the revocation is queued");
        var queued = (string?)DbScalar("SELECT credential FROM pending_token_revocations") ?? "";
        Assert(queued.StartsWith("v1.") && !queued.Contains("apple-refresh"), "A queued token stays encrypted");
        Assert(await AccountDeletion.RevokeQueuedAsync() && apple.Revocations.Count == 2, "A queued revocation waits for its retry time");
        DbScalar("UPDATE pending_token_revocations SET next_attempt_at = 0");
        apple.RevokeAnswer = () => (400, "{\"error\":\"invalid_client\"}");
        Assert(!await AccountDeletion.RevokeQueuedAsync() && DbCount("SELECT attempts FROM pending_token_revocations") == 2,
            "A refused client secret is retried later");
        DbScalar("UPDATE pending_token_revocations SET next_attempt_at = 0");
        apple.RevokeAnswer = () => (200, "");
        Assert(await AccountDeletion.RevokeQueuedAsync() && apple.Revocations[^1]["token"] == "apple-refresh-down" &&
            DbCount("SELECT COUNT(*) FROM pending_token_revocations") == 0, "The cleanup retry revokes the queued token");

        // A token Apple has already invalidated has nothing left to revoke.
        apple.RevokeAnswer = () => (400, "{\"error\":\"invalid_grant\"}");
        var gone = await apple.SignIn(client, "001234.apple.gone", "apple-refresh-gone");
        (status, body) = await DeleteAccount(client, gone.GetProperty("playerProfileId").GetString()!, gone.GetProperty("sessionToken").GetString()!);
        Assert(status == HttpStatusCode.OK && !body.GetProperty("signInRevocationPending").GetBoolean() &&
            DbCount("SELECT COUNT(*) FROM pending_token_revocations") == 0, "An already revoked token is not retried");
    }

    private static Task TestCredentialProtection()
    {
        var previous = Environment.GetEnvironmentVariable("ACCOUNT_TOKEN_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", "");
            Assert(!CredentialProtection.Configured, "No key, no credential storage");
            Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", Convert.ToBase64String(new byte[16]));
            Assert(!CredentialProtection.Configured, "A short key is refused");
            Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var sealedText = CredentialProtection.Protect("refresh-secret", "purpose-a");
            Assert(sealedText != CredentialProtection.Protect("refresh-secret", "purpose-a") && !sealedText.Contains("refresh-secret"),
                "Sealing is randomised and hides the token");
            Assert(CredentialProtection.Unprotect(sealedText, "purpose-a") == "refresh-secret", "A sealed token opens with its key and purpose");
            bool Fails(Action open) { try { open(); return false; } catch (CryptographicException) { return true; } }
            Assert(Fails(() => CredentialProtection.Unprotect(sealedText, "purpose-b")), "A token sealed for one purpose does not open for another");
            var bytes = Base64Url.DecodeFromChars(sealedText.AsSpan(3)); bytes[^1] ^= 1;
            Assert(Fails(() => CredentialProtection.Unprotect("v1." + Base64Url.EncodeToString(bytes), "purpose-a")), "A tampered token is refused");
            Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Assert(Fails(() => CredentialProtection.Unprotect(sealedText, "purpose-a")), "Another key cannot open it");
        }
        finally { Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", previous); }
        return Task.CompletedTask;
    }

    /// <summary>A database from before schema 7: no sealed credential columns and no deletion tables.</summary>
    private static Task TestAccountDeletionMigration()
    {
        DbScalar("ALTER TABLE account_identities DROP COLUMN credential");
        DbScalar("ALTER TABLE account_challenges DROP COLUMN credential");
        DbScalar("DROP TABLE deleted_sessions");
        DbScalar("DROP TABLE pending_token_revocations");
        DbScalar("DELETE FROM schema_version WHERE version >= 7");
        Database.Initialize();
        Database.Initialize(); // The upgrade must be safe to repeat.
        Assert(DbCount("SELECT COUNT(*) FROM account_identities WHERE credential <> ''") == 0 &&
            DbCount("SELECT COUNT(*) FROM account_challenges WHERE credential <> ''") == 0 &&
            DbCount("SELECT COUNT(*) FROM deleted_sessions") == 0 && DbCount("SELECT COUNT(*) FROM pending_token_revocations") == 0,
            "Schema 7 adds the credential columns and the deletion tables");
        Assert(DbCount("SELECT MAX(version) FROM schema_version") >= 7, "expected schema version 7");
        return Task.CompletedTask;
    }

    /// <summary>Sign in with Apple configured against stand-ins for Apple's keys, token and revoke endpoints.</summary>
    private sealed class AppleSignInFixture : IDisposable
    {
        public const string ClientId = "com.crownroad.signin";
        private static readonly string[] Keys = ["APPLE_SIGN_IN_CLIENT_ID", "APPLE_SIGN_IN_TEAM_ID", "APPLE_SIGN_IN_KEY_ID",
            "APPLE_SIGN_IN_PRIVATE_KEY", "APPLE_SIGN_IN_PRIVATE_KEY_PATH", "APPLE_SIGN_IN_REDIRECT_URI", "ACCOUNT_TOKEN_KEY", "ACCOUNT_TOKEN_KEY_PATH"];
        private readonly string?[] _previous = Keys.Select(Environment.GetEnvironmentVariable).ToArray();
        private readonly RSA _appleKey = RSA.Create(2048);
        private readonly ECDsa _clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private string _nonce = "", _subject = "", _refresh = "";
        public readonly List<IReadOnlyDictionary<string, string>> Revocations = [];
        public Func<(int, string)> RevokeAnswer = () => (200, "");

        public AppleSignInFixture()
        {
            foreach (var key in Keys) Environment.SetEnvironmentVariable(key, "");
            Environment.SetEnvironmentVariable("APPLE_SIGN_IN_CLIENT_ID", ClientId);
            Environment.SetEnvironmentVariable("APPLE_SIGN_IN_TEAM_ID", "TEAM123456");
            Environment.SetEnvironmentVariable("APPLE_SIGN_IN_KEY_ID", "KEY1234567");
            Environment.SetEnvironmentVariable("APPLE_SIGN_IN_REDIRECT_URI", "http://localhost/auth/apple/callback");
            Environment.SetEnvironmentVariable("APPLE_SIGN_IN_PRIVATE_KEY", _clientKey.ExportPkcs8PrivateKeyPem().Replace("\n", "\\n"));
            Environment.SetEnvironmentVariable("ACCOUNT_TOKEN_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var parameters = _appleKey.ExportParameters(false);
            AccountAuth.TestAppleKeys = () => Task.FromResult(JsonSerializer.Serialize(new { keys = new[] {
                new { kty = "RSA", kid = "test-apple-key", use = "sig", alg = "RS256",
                      n = Base64Url.EncodeToString(parameters.Modulus), e = Base64Url.EncodeToString(parameters.Exponent) } } }));
            AccountAuth.TestAppleToken = _ => Task.FromResult(JsonSerializer.Serialize(new
                { access_token = "a", token_type = "Bearer", refresh_token = _refresh, id_token = IdToken() }));
            AccountAuth.TestAppleRevoke = form => { Revocations.Add(form); return Task.FromResult(RevokeAnswer()); };
        }

        private string IdToken()
        {
            static string Segment(object value) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var input = Segment(new { alg = "RS256", kid = "test-apple-key" }) + "." +
                Segment(new { iss = "https://appleid.apple.com", aud = ClientId, sub = _subject, nonce = _nonce, iat = now, exp = now + 600 });
            return input + "." + Base64Url.EncodeToString(_appleKey.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        /// <summary>Signs in through the browser flow: start, Apple's form post back, then the device's poll.</summary>
        public async Task<JsonElement> SignIn(HttpClient client, string subject, string refreshToken, string guest = "", string token = "")
        {
            var challenge = await AccountPost(client, "/auth/apple/start", new { }, guest, token);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(challenge.GetProperty("url").GetString()!).Query);
            (_nonce, _subject, _refresh) = (query["nonce"].ToString(), subject, refreshToken);
            using var callback = await client.PostAsync("/auth/apple/callback", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["state"] = query["state"].ToString(), ["code"] = "good-code" }));
            Assert(callback.IsSuccessStatusCode, "Apple's callback is accepted");
            return await AccountPost(client, "/auth/apple/poll", new { challengeId = challenge.GetProperty("challengeId").GetString(),
                secret = challenge.GetProperty("secret").GetString() });
        }

        public bool SignedByClientKey(string[] jwt) =>
            _clientKey.VerifyData(Encoding.ASCII.GetBytes(jwt[0] + "." + jwt[1]), Base64Url.DecodeFromChars(jwt[2]), HashAlgorithmName.SHA256);

        public void Dispose()
        {
            AccountAuth.TestAppleKeys = null; AccountAuth.TestAppleToken = null; AccountAuth.TestAppleRevoke = null;
            for (var i = 0; i < Keys.Length; i++) Environment.SetEnvironmentVariable(Keys[i], _previous[i]);
            _appleKey.Dispose(); _clientKey.Dispose();
        }
    }
}
