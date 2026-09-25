using System;
using System.Data.Common;
using System.IO;
#if SQLITE_TEST
using Microsoft.Data.Sqlite;
#endif
using Npgsql;

namespace CrownroadServer;

public static class Database
{
    private static string _connectionString = "";
#if SQLITE_TEST
    private static string _dbFilePath = "crownroad.db";
#endif
    private static NpgsqlDataSource? _postgresDataSource;
    private static Provider _provider = Provider.Postgres;
    private const int CurrentSchemaVersion = 4;

    private enum Provider
    {
        Postgres,
#if SQLITE_TEST
        Sqlite
#endif
    }

    public static bool IsPostgres => _provider == Provider.Postgres;
    public static bool SupportsLocalBackups => !IsPostgres;

    public static void Configure(string connectionString, string? passwordFile = null)
    {
        _postgresDataSource?.Dispose();
        _postgresDataSource = null;

        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            _provider = Provider.Postgres;
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(passwordFile))
            {
                if (!File.Exists(passwordFile))
                    throw new InvalidOperationException("DATABASE_PASSWORD_FILE does not point to a readable secret.");
                builder.Password = File.ReadAllText(passwordFile).Trim();
            }

            if (string.IsNullOrWhiteSpace(builder.Password))
                throw new InvalidOperationException("A PostgreSQL password must be supplied through DATABASE_PASSWORD_FILE.");

            _connectionString = builder.ConnectionString;
            _postgresDataSource = NpgsqlDataSource.Create(_connectionString);
            return;
        }

#if SQLITE_TEST
        _provider = Provider.Sqlite;
        _connectionString = connectionString;
        var dataSource = connectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!string.IsNullOrWhiteSpace(dataSource)) _dbFilePath = dataSource;
#else
        throw new InvalidOperationException("Production builds require a PostgreSQL connection string containing Host=.");
#endif
    }

    public static DbConnection Open()
    {
        if (IsPostgres)
        {
            if (_postgresDataSource == null)
                throw new InvalidOperationException("PostgreSQL database was not configured.");
            return _postgresDataSource.OpenConnection();
        }

#if SQLITE_TEST
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
#else
        throw new InvalidOperationException("SQLite is available only in Debug test builds.");
#endif
    }

    public static void Initialize()
    {
        using var conn = Open();
        EnsureSchemaVersionTable(conn);
        using var cmd = conn.CreateCommand();
        var schemaSql = """
            CREATE TABLE IF NOT EXISTS players (
                profile_id TEXT PRIMARY KEY,
                callsign TEXT NOT NULL DEFAULT '',
                auth_state TEXT NOT NULL DEFAULT 'verified',
                session_token TEXT NOT NULL DEFAULT '',
                can_submit_challenges INTEGER NOT NULL DEFAULT 1,
                can_join_rooms INTEGER NOT NULL DEFAULT 1,
                relay_enabled INTEGER NOT NULL DEFAULT 1,
                synced_at INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS challenge_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                submission_id TEXT UNIQUE,
                profile_id TEXT NOT NULL,
                board_code TEXT NOT NULL,
                score INTEGER NOT NULL DEFAULT 0,
                player_won INTEGER NOT NULL DEFAULT 0,
                stars_earned INTEGER NOT NULL DEFAULT 0,
                elapsed_seconds REAL NOT NULL DEFAULT 0,
                hull_remaining REAL NOT NULL DEFAULT 0,
                enemy_defeats INTEGER NOT NULL DEFAULT 0,
                submitted_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_challenge_results_board ON challenge_results(board_code, score DESC);
            CREATE INDEX IF NOT EXISTS idx_challenge_results_profile ON challenge_results(profile_id, board_code);

            CREATE TABLE IF NOT EXISTS rooms (
                room_id TEXT PRIMARY KEY,
                title TEXT NOT NULL DEFAULT '',
                host_profile_id TEXT NOT NULL DEFAULT '',
                host_callsign TEXT NOT NULL DEFAULT '',
                board_code TEXT NOT NULL DEFAULT '',
                board_title TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'lobby',
                max_players INTEGER NOT NULL DEFAULT 4,
                region TEXT NOT NULL DEFAULT 'global',
                uses_locked_deck INTEGER NOT NULL DEFAULT 0,
                locked_deck_unit_ids TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL DEFAULT 0,
                updated_at INTEGER NOT NULL DEFAULT 0,
                race_started_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_rooms_status ON rooms(status);

            CREATE TABLE IF NOT EXISTS room_seats (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                room_id TEXT NOT NULL,
                profile_id TEXT NOT NULL,
                callsign TEXT NOT NULL DEFAULT '',
                ticket_id TEXT NOT NULL DEFAULT '',
                join_token TEXT NOT NULL DEFAULT '',
                seat_label TEXT NOT NULL DEFAULT 'runner',
                status TEXT NOT NULL DEFAULT 'joined',
                is_ready INTEGER NOT NULL DEFAULT 0,
                score INTEGER NOT NULL DEFAULT 0,
                elapsed_seconds REAL NOT NULL DEFAULT 0,
                hull_remaining REAL NOT NULL DEFAULT 0,
                enemy_defeats INTEGER NOT NULL DEFAULT 0,
                race_status TEXT NOT NULL DEFAULT 'prep',
                joined_at INTEGER NOT NULL DEFAULT 0,
                expires_at INTEGER NOT NULL DEFAULT 0,
                UNIQUE(room_id, profile_id)
            );

            CREATE INDEX IF NOT EXISTS idx_room_seats_room ON room_seats(room_id);
            CREATE INDEX IF NOT EXISTS idx_room_seats_status ON room_seats(room_id, status);

            CREATE TABLE IF NOT EXISTS room_telemetry (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                room_id TEXT NOT NULL,
                profile_id TEXT NOT NULL,
                elapsed_seconds REAL NOT NULL DEFAULT 0,
                hull_ratio REAL NOT NULL DEFAULT 1,
                enemy_defeats INTEGER NOT NULL DEFAULT 0,
                race_status TEXT NOT NULL DEFAULT 'racing',
                reported_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_room_telemetry_room ON room_telemetry(room_id, profile_id);

            CREATE TABLE IF NOT EXISTS room_reports (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                room_id TEXT NOT NULL,
                reporter_profile_id TEXT NOT NULL,
                target_profile_id TEXT NOT NULL DEFAULT '',
                reason TEXT NOT NULL DEFAULT '',
                details TEXT NOT NULL DEFAULT '',
                reported_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS challenge_feed (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                board_code TEXT NOT NULL UNIQUE,
                title TEXT NOT NULL DEFAULT '',
                stage INTEGER NOT NULL DEFAULT 1,
                seed INTEGER NOT NULL DEFAULT 0,
                uses_locked_deck INTEGER NOT NULL DEFAULT 0,
                locked_deck_unit_ids TEXT NOT NULL DEFAULT '',
                featured INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS achievements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL,
                achievement_id TEXT NOT NULL,
                unlocked_at TEXT NOT NULL DEFAULT (datetime('now')),
                UNIQUE(profile_id, achievement_id)
            );

            CREATE TABLE IF NOT EXISTS daily_completions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL,
                daily_date TEXT NOT NULL,
                score INTEGER NOT NULL DEFAULT 0,
                completed_at TEXT NOT NULL DEFAULT (datetime('now')),
                UNIQUE(profile_id, daily_date)
            );

            CREATE TABLE IF NOT EXISTS purchases (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                purchase_id TEXT NOT NULL UNIQUE,
                profile_id TEXT NOT NULL,
                product_id TEXT NOT NULL,
                platform TEXT NOT NULL DEFAULT '',
                receipt_token TEXT NOT NULL DEFAULT '',
                transaction_id TEXT NOT NULL DEFAULT '',
                gold_credited INTEGER NOT NULL DEFAULT 0,
                food_credited INTEGER NOT NULL DEFAULT 0,
                granted_unit_unlock INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'validated',
                purchased_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_purchases_profile ON purchases(profile_id);
            CREATE INDEX IF NOT EXISTS idx_purchases_transaction ON purchases(transaction_id);

            CREATE TABLE IF NOT EXISTS analytics_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL DEFAULT '',
                event_type TEXT NOT NULL,
                event_data TEXT NOT NULL DEFAULT '',
                client_version INTEGER NOT NULL DEFAULT 0,
                platform TEXT NOT NULL DEFAULT '',
                recorded_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_analytics_type ON analytics_events(event_type, recorded_at);

            CREATE TABLE IF NOT EXISTS crash_reports (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL DEFAULT '',
                error_type TEXT NOT NULL DEFAULT '',
                error_message TEXT NOT NULL DEFAULT '',
                stack_trace TEXT NOT NULL DEFAULT '',
                client_version INTEGER NOT NULL DEFAULT 0,
                platform TEXT NOT NULL DEFAULT '',
                scene TEXT NOT NULL DEFAULT '',
                reported_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_crash_reports_type ON crash_reports(error_type, reported_at);

            CREATE TABLE IF NOT EXISTS cloud_saves (
                profile_id TEXT PRIMARY KEY,
                save_data TEXT NOT NULL DEFAULT '',
                save_version INTEGER NOT NULL DEFAULT 0,
                save_hash TEXT NOT NULL DEFAULT '',
                uploaded_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS arena_snapshots (
                profile_id TEXT PRIMARY KEY,
                deck_unit_ids TEXT NOT NULL DEFAULT '',
                deck_spell_ids TEXT NOT NULL DEFAULT '',
                unit_levels TEXT NOT NULL DEFAULT '',
                unit_equipment TEXT NOT NULL DEFAULT '',
                power_rating INTEGER NOT NULL DEFAULT 0,
                arena_rating INTEGER NOT NULL DEFAULT 1000,
                updated_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS arena_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL DEFAULT '',
                opponent_profile_id TEXT NOT NULL DEFAULT '',
                won INTEGER NOT NULL DEFAULT 0,
                rating_before INTEGER NOT NULL DEFAULT 0,
                rating_after INTEGER NOT NULL DEFAULT 0,
                played_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_arena_results_player ON arena_results(profile_id, played_at);

            CREATE TABLE IF NOT EXISTS guilds (
                guild_id TEXT PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                leader_profile_id TEXT NOT NULL DEFAULT '',
                tier INTEGER NOT NULL DEFAULT 1,
                experience INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS guild_members (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                guild_id TEXT NOT NULL DEFAULT '',
                profile_id TEXT NOT NULL DEFAULT '',
                contribution_points INTEGER NOT NULL DEFAULT 0,
                joined_at INTEGER NOT NULL DEFAULT 0,
                UNIQUE(guild_id, profile_id),
                FOREIGN KEY(guild_id) REFERENCES guilds(guild_id)
            );

            CREATE INDEX IF NOT EXISTS idx_guild_members_guild ON guild_members(guild_id);

            CREATE TABLE IF NOT EXISTS raid_bosses (
                week_id TEXT PRIMARY KEY,
                boss_id TEXT NOT NULL DEFAULT '',
                total_health INTEGER NOT NULL DEFAULT 10000000,
                damage_dealt INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS raid_contributions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                week_id TEXT NOT NULL DEFAULT '',
                profile_id TEXT NOT NULL DEFAULT '',
                damage INTEGER NOT NULL DEFAULT 0,
                contributed_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_raid_contributions_week ON raid_contributions(week_id, profile_id);

            CREATE TABLE IF NOT EXISTS friendships (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL DEFAULT '',
                friend_id TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL DEFAULT 0,
                UNIQUE(profile_id, friend_id)
            );

            CREATE INDEX IF NOT EXISTS idx_friendships_profile ON friendships(profile_id);

            CREATE TABLE IF NOT EXISTS friend_gifts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sender_id TEXT NOT NULL DEFAULT '',
                receiver_id TEXT NOT NULL DEFAULT '',
                sent_at INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_friend_gifts_sender ON friend_gifts(sender_id, sent_at);
        """;
        cmd.CommandText = NormalizeSchemaSql(schemaSql);
        cmd.ExecuteNonQuery();
        RunMigrations(conn);
        SeedDefaultFeed(conn);
    }

    private static string NormalizeSchemaSql(string sql) => IsPostgres
        ? sql.Replace("INTEGER PRIMARY KEY AUTOINCREMENT", "BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY", StringComparison.Ordinal)
            .Replace("datetime('now')", "CURRENT_TIMESTAMP::text", StringComparison.Ordinal)
        : sql;

    private static void EnsureSchemaVersionTable(DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = NormalizeSchemaSql("""
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL DEFAULT 1,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            )
        """);
        cmd.ExecuteNonQuery();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM schema_version";
        if (ToInt64(check.ExecuteScalar()) == 0)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO schema_version (version) VALUES (1)";
            insert.ExecuteNonQuery();
        }
    }

    private static int GetSchemaVersion(DbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM schema_version";
        return Math.Max(1, ToInt32(cmd.ExecuteScalar(), 1));
    }

    private static void SetSchemaVersion(DbConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO schema_version (version) VALUES (@ver)";
        cmd.Parameters.AddWithValue("@ver", version);
        cmd.ExecuteNonQuery();
    }

    private static void RunMigrations(DbConnection conn)
    {
        var version = GetSchemaVersion(conn);

        if (version < 2)
        {
            // Migration 2: add price_cents column to purchases for Stripe audit trail
            TryAddColumn(conn, "purchases", "price_cents", "INTEGER NOT NULL DEFAULT 0");
            SetSchemaVersion(conn, 2);
        }

        if (version < 3)
        {
            // A profile ID is public and must never be treated as credentials. Keep only
            // hashes of the random session tokens and make paid balances durable server
            // state, independent of the device save file.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS auth_sessions (
                    session_id TEXT PRIMARY KEY,
                    profile_id TEXT NOT NULL,
                    token_hash TEXT NOT NULL UNIQUE,
                    created_at INTEGER NOT NULL DEFAULT 0,
                    expires_at INTEGER NOT NULL DEFAULT 0,
                    revoked_at INTEGER NOT NULL DEFAULT 0,
                    last_seen_at INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_auth_sessions_profile ON auth_sessions(profile_id, expires_at);

                CREATE TABLE IF NOT EXISTS player_wallets (
                    profile_id TEXT PRIMARY KEY,
                    gold_balance INTEGER NOT NULL DEFAULT 0,
                    food_balance INTEGER NOT NULL DEFAULT 0,
                    unit_unlock_balance INTEGER NOT NULL DEFAULT 0,
                    updated_at INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(profile_id) REFERENCES players(profile_id)
                );

                CREATE TABLE IF NOT EXISTS wallet_ledger (
                    entry_id TEXT PRIMARY KEY,
                    profile_id TEXT NOT NULL,
                    currency_type TEXT NOT NULL,
                    amount INTEGER NOT NULL DEFAULT 0,
                    reason TEXT NOT NULL,
                    purchase_id TEXT NOT NULL DEFAULT '',
                    created_at INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(purchase_id, currency_type),
                    FOREIGN KEY(profile_id) REFERENCES players(profile_id)
                );

                CREATE INDEX IF NOT EXISTS idx_wallet_ledger_profile ON wallet_ledger(profile_id, created_at DESC);
            """;
            cmd.ExecuteNonQuery();
            SetSchemaVersion(conn, 3);
        }

        if (version < 4)
        {
            // Challenge sync acknowledgements are keyed by a client-generated ID.
            // NULL keeps legacy rows and manually-entered results non-conflicting.
            TryAddColumn(conn, "challenge_results", "submission_id", "TEXT");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_challenge_results_submission ON challenge_results(submission_id) WHERE submission_id IS NOT NULL";
            cmd.ExecuteNonQuery();
            SetSchemaVersion(conn, 4);
        }
    }

    private static void TryAddColumn(DbConnection conn, string table, string column, string definition)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            cmd.ExecuteNonQuery();
        }
        catch (DbException)
        {
            // Column already exists — safe to ignore
        }
    }

    public static string Backup()
    {
        if (IsPostgres)
            return "";

#if SQLITE_TEST
        if (!File.Exists(_dbFilePath))
            return "";

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{_dbFilePath}.backup-{timestamp}";

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"VACUUM INTO '{backupPath}'";
        cmd.ExecuteNonQuery();

        return backupPath;
#else
        return "";
#endif
    }

    public static void CleanupOldBackups(int keepCount = 5)
    {
        if (IsPostgres)
            return;

#if SQLITE_TEST
        var dir = Path.GetDirectoryName(Path.GetFullPath(_dbFilePath)) ?? ".";
        var dbName = Path.GetFileName(_dbFilePath);
        var backups = Directory.GetFiles(dir, $"{dbName}.backup-*");
        if (backups.Length <= keepCount) return;

        Array.Sort(backups);
        for (var i = 0; i < backups.Length - keepCount; i++)
        {
            try { File.Delete(backups[i]); } catch { /* ignore */ }
        }
#endif
    }

    private static void SeedDefaultFeed(DbConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM challenge_feed";
        var count = ToInt64(check.ExecuteScalar());
        if (count > 0) return;

        var boards = new[]
        {
            ("CH-01-DAILY-1001", "King's Road Daily", 3, 1001, false, ""),
            ("CH-02-DAILY-2002", "Saltwake Docks Daily", 8, 2002, false, ""),
            ("CH-03-LOCK-3003", "Emberforge Locked Convoy", 13, 3003, true, "player_brawler,player_shooter,player_defender"),
            ("CH-04-DAILY-4004", "Thornwall Sprint", 23, 4004, false, ""),
            ("CH-05-LOCK-5005", "Citadel Locked Siege", 48, 5005, true, "player_breacher,player_marksman,player_grenadier"),
        };

        foreach (var (code, title, stage, seed, locked, deckIds) in boards)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO challenge_feed (board_code, title, stage, seed, uses_locked_deck, locked_deck_unit_ids, featured, created_at)
                VALUES (@code, @title, @stage, @seed, @locked, @deckIds, 1, @now)
                ON CONFLICT(board_code) DO NOTHING
            """;
            ins.Parameters.AddWithValue("@code", code);
            ins.Parameters.AddWithValue("@title", title);
            ins.Parameters.AddWithValue("@stage", stage);
            ins.Parameters.AddWithValue("@seed", seed);
            ins.Parameters.AddWithValue("@locked", locked ? 1 : 0);
            ins.Parameters.AddWithValue("@deckIds", deckIds);
            ins.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            ins.ExecuteNonQuery();
        }
    }

    public static long ToInt64(object? value, long fallback = 0) =>
        value == null || value == DBNull.Value ? fallback : Convert.ToInt64(value);

    public static int ToInt32(object? value, int fallback = 0) =>
        value == null || value == DBNull.Value ? fallback : Convert.ToInt32(value);
}
