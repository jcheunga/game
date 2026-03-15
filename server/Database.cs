using System;
using Microsoft.Data.Sqlite;

namespace CrownroadServer;

public static class Database
{
    private static string _connectionString = "Data Source=crownroad.db";

    public static void Configure(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public static void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
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
        """;
        cmd.ExecuteNonQuery();
        SeedDefaultFeed(conn);
    }

    private static void SeedDefaultFeed(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM challenge_feed";
        var count = (long)(check.ExecuteScalar() ?? 0);
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
                INSERT OR IGNORE INTO challenge_feed (board_code, title, stage, seed, uses_locked_deck, locked_deck_unit_ids, featured, created_at)
                VALUES ($code, $title, $stage, $seed, $locked, $deckIds, 1, $now)
            """;
            ins.Parameters.AddWithValue("$code", code);
            ins.Parameters.AddWithValue("$title", title);
            ins.Parameters.AddWithValue("$stage", stage);
            ins.Parameters.AddWithValue("$seed", seed);
            ins.Parameters.AddWithValue("$locked", locked ? 1 : 0);
            ins.Parameters.AddWithValue("$deckIds", deckIds);
            ins.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            ins.ExecuteNonQuery();
        }
    }
}
