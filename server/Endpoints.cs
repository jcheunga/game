using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace CrownroadServer;

public static class Endpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string NewId(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    // ── Player Profile ───────────────────────────────────────

    public static async Task<IResult> PlayerProfile(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profile.playerProfileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var callsign = GetString(body, "profile.playerCallsign", "Anonymous");
        var now = Now();

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing playerProfileId" });

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        UpsertPlayer(conn, tx, profileId, callsign, now);
        tx.Commit();

        return Results.Ok(new
        {
            status = "ok",
            message = "Profile synced.",
            playerProfileId = profileId,
            playerCallsign = callsign,
            authState = "verified",
            sessionToken = $"session-{profileId}-{now}",
            canSubmitChallenges = true,
            canJoinRooms = true,
            relayEnabled = true,
            syncedAtUnixSeconds = now
        });
    }

    // ── Challenge Sync ───────────────────────────────────────

    public static async Task<IResult> ChallengeSync(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profile ID" });
        var entries = GetArray(body, "batch.submissions");
        if (entries.Length == 0) entries = GetArray(body, "submissions");
        var accepted = 0;
        var rejected = 0;

        using var conn = Database.Open();
        foreach (var entry in entries)
        {
            var code = GetNestedString(entry, "boardCode", "");
            var score = GetNestedInt(entry, "score", 0);
            if (string.IsNullOrWhiteSpace(code)) { rejected++; continue; }
            // Bounds validation: reject implausible values
            var elapsed = GetNestedDouble(entry, "elapsedSeconds", 0);
            var defeats = GetNestedInt(entry, "enemyDefeats", 0);
            if (score < 0 || score > 999999 || elapsed < 0 || elapsed > 7200 || defeats < 0 || defeats > 9999)
            {
                rejected++;
                continue;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO challenge_results (profile_id, board_code, score, player_won, stars_earned, elapsed_seconds, hull_remaining, enemy_defeats, submitted_at)
                VALUES ($pid, $code, $score, $won, $stars, $elapsed, $hull, $defeats, $now)
            """;
            cmd.Parameters.AddWithValue("$pid", profileId);
            cmd.Parameters.AddWithValue("$code", code);
            cmd.Parameters.AddWithValue("$score", score);
            cmd.Parameters.AddWithValue("$won", GetNestedBool(entry, "playerWon", false) ? 1 : 0);
            cmd.Parameters.AddWithValue("$stars", GetNestedInt(entry, "starsEarned", 0));
            cmd.Parameters.AddWithValue("$elapsed", GetNestedDouble(entry, "elapsedSeconds", 0));
            cmd.Parameters.AddWithValue("$hull", GetNestedDouble(entry, "hullRemaining", 0));
            cmd.Parameters.AddWithValue("$defeats", GetNestedInt(entry, "enemyDefeats", 0));
            cmd.Parameters.AddWithValue("$now", Now());
            cmd.ExecuteNonQuery();
            accepted++;
        }

        return Results.Ok(new
        {
            status = "ok",
            message = $"Processed {accepted + rejected} submissions.",
            accepted,
            rejected
        });
    }

    // ── Challenge Leaderboard ────────────────────────────────

    public static IResult ChallengeLeaderboard(HttpRequest request)
    {
        var code = request.Query["code"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest(new { error = "missing code param" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT profile_id, MAX(score) as best_score, COUNT(*) as attempts
            FROM challenge_results
            WHERE board_code = $code
            GROUP BY profile_id
            ORDER BY best_score DESC
            LIMIT 50
        """;
        cmd.Parameters.AddWithValue("$code", code);

        var entries = new List<object>();
        var rank = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rank++;
            entries.Add(new
            {
                rank,
                playerProfileId = reader.GetString(0),
                score = reader.GetInt32(1),
                attempts = reader.GetInt32(2)
            });
        }

        return Results.Ok(new
        {
            boardCode = code,
            entries,
            fetchedAt = Now()
        });
    }

    // ── Challenge Feed ───────────────────────────────────────

    public static IResult ChallengeFeed(HttpRequest request)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT board_code, title, stage, seed, uses_locked_deck, locked_deck_unit_ids, featured FROM challenge_feed ORDER BY featured DESC, created_at DESC LIMIT 20";

        var entries = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var deckIds = reader.GetString(5);
            entries.Add(new
            {
                boardCode = reader.GetString(0),
                title = reader.GetString(1),
                stage = reader.GetInt32(2),
                seed = reader.GetInt32(3),
                usesLockedDeck = reader.GetInt32(4) == 1,
                lockedDeckUnitIds = string.IsNullOrWhiteSpace(deckIds) ? Array.Empty<string>() : deckIds.Split(','),
                featured = reader.GetInt32(6) == 1
            });
        }

        return Results.Ok(new { entries, fetchedAt = Now() });
    }

    // ── Room Directory ───────────────────────────────────────

    public static IResult RoomDirectory(HttpRequest request)
    {
        using var conn = Database.Open();
        CleanupStaleRooms(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.room_id, r.title, r.host_callsign, r.board_code, r.board_title, r.status,
                   r.max_players, r.region, r.uses_locked_deck, r.locked_deck_unit_ids,
                   (SELECT COUNT(*) FROM room_seats s WHERE s.room_id = r.room_id AND s.status != 'left') as player_count,
                   (SELECT COUNT(*) FROM room_seats s WHERE s.room_id = r.room_id AND s.seat_label = 'spectator') as spectator_count
            FROM rooms r
            WHERE r.status IN ('lobby', 'racing')
            ORDER BY r.updated_at DESC
            LIMIT 20
        """;

        var entries = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var deckIds = reader.GetString(9);
            entries.Add(new
            {
                roomId = reader.GetString(0),
                title = reader.GetString(1),
                hostCallsign = reader.GetString(2),
                boardCode = reader.GetString(3),
                boardTitle = reader.GetString(4),
                status = reader.GetString(5),
                maxPlayers = reader.GetInt32(6),
                region = reader.GetString(7),
                usesLockedDeck = reader.GetInt32(8) == 1,
                lockedDeckUnitIds = string.IsNullOrWhiteSpace(deckIds) ? Array.Empty<string>() : deckIds.Split(','),
                currentPlayers = reader.GetInt32(10),
                spectatorCount = reader.GetInt32(11)
            });
        }

        return Results.Ok(new { entries, fetchedAt = Now() });
    }

    // ── Room Create ──────────────────────────────────────────

    public static async Task<IResult> RoomCreate(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "room.playerProfileId", "");
        var callsign = GetString(body, "room.playerCallsign", "Host");
        var boardCode = GetString(body, "room.boardCode", "");
        var boardTitle = GetString(body, "room.boardTitle", "");
        var region = GetString(body, "room.region", "global");
        var usesLocked = GetBool(body, "room.usesLockedDeck", false);
        var deckIds = GetStringArray(body, "room.lockedDeckUnitIds");
        var now = Now();
        var roomId = NewId("ROOM");
        var ticketId = NewId("HOST");
        var joinToken = $"host-{Guid.NewGuid():N}";

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO rooms (room_id, title, host_profile_id, host_callsign, board_code, board_title, status, region, uses_locked_deck, locked_deck_unit_ids, created_at, updated_at)
                VALUES ($rid, $title, $pid, $cs, $code, $bt, 'lobby', $region, $locked, $deckIds, $now, $now)
            """;
            ins.Parameters.AddWithValue("$rid", roomId);
            ins.Parameters.AddWithValue("$title", $"{callsign} Relay");
            ins.Parameters.AddWithValue("$pid", profileId);
            ins.Parameters.AddWithValue("$cs", callsign);
            ins.Parameters.AddWithValue("$code", boardCode);
            ins.Parameters.AddWithValue("$bt", boardTitle);
            ins.Parameters.AddWithValue("$region", region);
            ins.Parameters.AddWithValue("$locked", usesLocked ? 1 : 0);
            ins.Parameters.AddWithValue("$deckIds", string.Join(",", deckIds));
            ins.Parameters.AddWithValue("$now", now);
            ins.ExecuteNonQuery();

            InsertSeat(conn, tx, roomId, profileId, callsign, ticketId, joinToken, "host seat", now);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return Results.Ok(new
        {
            status = "lobby",
            message = $"Room {roomId} hosted.",
            roomId,
            title = $"{callsign} Relay",
            hostCallsign = callsign,
            boardCode,
            boardTitle,
            currentPlayers = 1,
            maxPlayers = 4,
            spectatorCount = 0,
            region,
            transportHint = "relay_room",
            relayEndpoint = $"ws://localhost:5000/ws/relay/{roomId}",
            usesLockedDeck = usesLocked,
            lockedDeckUnitIds = deckIds,
            hostTicket = new
            {
                status = "hosted",
                message = "Host seat reserved.",
                ticketId,
                joinToken,
                seatLabel = "host seat",
                transportHint = "relay_room",
                relayEndpoint = $"ws://localhost:5000/ws/relay/{roomId}",
                requestedAtUnixSeconds = now,
                expiresAtUnixSeconds = now + 3600,
                usesLockedDeck = usesLocked,
                lockedDeckUnitIds = deckIds
            }
        });
    }

    // ── Room Join ────────────────────────────────────────────

    public static async Task<IResult> RoomJoin(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "join.playerProfileId", GetString(body, "playerProfileId", ""));
        var callsign = GetString(body, "join.playerCallsign", GetString(body, "playerCallsign", "Runner"));
        var roomId = GetString(body, "join.roomId", GetString(body, "roomId", ""));
        var now = Now();

        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });

        using var conn = Database.Open();
        var room = GetRoom(conn, roomId);
        if (room == null)
            return Results.NotFound(new { error = "room not found" });

        // Atomic seat count check + insert inside a transaction to prevent race conditions
        using var tx = conn.BeginTransaction();
        string seatLabel;
        var ticketId = NewId("JOIN");
        var joinToken = $"join-{Guid.NewGuid():N}";
        try
        {
            var seatCount = CountActiveSeats(conn, roomId, tx);
            seatLabel = seatCount >= 4 ? "spectator" : "runner";
            InsertSeat(conn, tx, roomId, profileId, callsign, ticketId, joinToken, seatLabel, now);
            TouchRoom(conn, roomId, tx);
            tx.Commit();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            tx.Rollback();
            return Results.Conflict(new { error = "already joined this room" });
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return Results.Ok(new
        {
            status = "joined",
            message = $"Joined room {roomId}.",
            ticketId,
            joinToken,
            roomId,
            roomTitle = room.Title,
            boardCode = room.BoardCode,
            seatLabel,
            transportHint = "relay_room",
            relayEndpoint = $"ws://localhost:5000/ws/relay/{roomId}",
            requestedAtUnixSeconds = now,
            expiresAtUnixSeconds = now + 3600,
            usesLockedDeck = room.UsesLockedDeck,
            lockedDeckUnitIds = room.LockedDeckUnitIds
        });
    }

    // ── Room Session ─────────────────────────────────────────

    public static IResult RoomSession(HttpRequest request)
    {
        var roomId = request.Query["roomId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });

        using var conn = Database.Open();
        var room = GetRoom(conn, roomId);
        if (room == null)
            return Results.NotFound(new { error = "room not found" });

        var peers = GetPeerSnapshots(conn, roomId);

        return Results.Ok(new
        {
            hasRoom = true,
            roomId,
            title = room.Title,
            boardCode = room.BoardCode,
            status = room.Status,
            raceCountdownActive = room.Status == "countdown",
            raceCountdownRemainingSeconds = room.Status == "countdown" ? 3.0 : 0.0,
            peers,
            fetchedAt = Now()
        });
    }

    // ── Room Action ──────────────────────────────────────────

    public static async Task<IResult> RoomAction(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "action.roomId", GetString(body, "roomId", ""));
        var action = GetString(body, "action.actionId", GetString(body, "action", ""));
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "action.playerProfileId", "");

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(action))
            return Results.BadRequest(new { error = "missing roomId or action" });

        using var conn = Database.Open();

        switch (action)
        {
            case "set_ready":
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE room_seats SET is_ready = 1 WHERE room_id = $rid AND profile_id = $pid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$pid", profileId);
                cmd.ExecuteNonQuery();
                TouchRoom(conn, roomId);
                return Results.Ok(new { status = "ok", message = "Ready state set." });
            }
            case "launch_round":
            {
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE rooms SET status = 'racing', race_started_at = $now, updated_at = $now WHERE room_id = $rid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$now", Now());
                cmd.ExecuteNonQuery();
                using var seats = conn.CreateCommand();
                seats.Transaction = tx;
                seats.CommandText = "UPDATE room_seats SET race_status = 'racing' WHERE room_id = $rid AND status != 'left' AND seat_label != 'spectator'";
                seats.Parameters.AddWithValue("$rid", roomId);
                seats.ExecuteNonQuery();
                tx.Commit();
                return Results.Ok(new { status = "ok", message = "Round launched." });
            }
            case "reset_round":
            {
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE rooms SET status = 'lobby', updated_at = $now WHERE room_id = $rid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$now", Now());
                cmd.ExecuteNonQuery();
                using var seats = conn.CreateCommand();
                seats.Transaction = tx;
                seats.CommandText = "UPDATE room_seats SET is_ready = 0, race_status = 'prep', score = 0 WHERE room_id = $rid AND status != 'left'";
                seats.Parameters.AddWithValue("$rid", roomId);
                seats.ExecuteNonQuery();
                tx.Commit();
                return Results.Ok(new { status = "ok", message = "Round reset." });
            }
            case "leave_room":
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE room_seats SET status = 'left' WHERE room_id = $rid AND profile_id = $pid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$pid", profileId);
                cmd.ExecuteNonQuery();
                TouchRoom(conn, roomId);
                return Results.Ok(new { status = "accepted", actionId = "leave_room", message = "Left room." });
            }
            default:
                return Results.BadRequest(new { error = $"unknown action: {action}" });
        }
    }

    // ── Room Result ──────────────────────────────────────────

    public static async Task<IResult> RoomResult(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "result.roomId", GetString(body, "roomId", ""));
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "result.playerProfileId", "");
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profile ID" });
        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });
        var score = GetInt(body, "result.score", GetInt(body, "score", 0));
        var elapsed = GetDouble(body, "result.elapsedSeconds", GetDouble(body, "elapsedSeconds", 0));
        var hull = GetDouble(body, "result.hullRemaining", GetDouble(body, "hullRemaining", 0));
        var defeats = GetInt(body, "result.enemyDefeats", GetInt(body, "enemyDefeats", 0));

        // Bounds validation
        score = Math.Clamp(score, 0, 999999);
        elapsed = Math.Clamp(elapsed, 0, 7200);
        hull = Math.Clamp(hull, 0, 10000);
        defeats = Math.Clamp(defeats, 0, 9999);

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE room_seats SET score = $score, elapsed_seconds = $elapsed, hull_remaining = $hull,
                   enemy_defeats = $defeats, race_status = 'submitted'
            WHERE room_id = $rid AND profile_id = $pid
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$score", score);
        cmd.Parameters.AddWithValue("$elapsed", elapsed);
        cmd.Parameters.AddWithValue("$hull", hull);
        cmd.Parameters.AddWithValue("$defeats", defeats);
        cmd.ExecuteNonQuery();
        TouchRoom(conn, roomId);

        var rank = GetProvisionalRank(conn, roomId, profileId);

        return Results.Ok(new
        {
            status = "accepted",
            message = $"Result submitted. Provisional rank: {rank}.",
            provisionalRank = rank
        });
    }

    // ── Room Scoreboard ──────────────────────────────────────

    public static IResult RoomScoreboard(HttpRequest request)
    {
        var roomId = request.Query["roomId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT profile_id, callsign, score, elapsed_seconds, hull_remaining, enemy_defeats, race_status
            FROM room_seats WHERE room_id = $rid AND status != 'left' AND seat_label != 'spectator'
            ORDER BY score DESC, elapsed_seconds ASC
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);

        var entries = new List<object>();
        var rank = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rank++;
            entries.Add(new
            {
                rank,
                playerProfileId = reader.GetString(0),
                callsign = reader.GetString(1),
                score = reader.GetInt32(2),
                elapsedSeconds = reader.GetDouble(3),
                hullRemaining = reader.GetDouble(4),
                enemyDefeats = reader.GetInt32(5),
                raceStatus = reader.GetString(6)
            });
        }

        return Results.Ok(new { roomId, entries, fetchedAt = Now() });
    }

    // ── Room Telemetry ───────────────────────────────────────

    public static async Task<IResult> RoomTelemetry(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "telemetry.roomId", GetString(body, "roomId", ""));
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "telemetry.playerProfileId", "");
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profile ID" });
        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });
        var elapsed = GetDouble(body, "telemetry.elapsedSeconds", GetDouble(body, "elapsedSeconds", 0));
        var hull = GetDouble(body, "telemetry.hullRatio", GetDouble(body, "hullRatio", 1));
        var defeats = GetInt(body, "telemetry.enemyDefeats", GetInt(body, "enemyDefeats", 0));
        var status = GetString(body, "telemetry.raceStatus", GetString(body, "raceStatus", "racing"));

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO room_telemetry (room_id, profile_id, elapsed_seconds, hull_ratio, enemy_defeats, race_status, reported_at)
            VALUES ($rid, $pid, $elapsed, $hull, $defeats, $status, $now)
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$elapsed", elapsed);
        cmd.Parameters.AddWithValue("$hull", hull);
        cmd.Parameters.AddWithValue("$defeats", defeats);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$now", Now());
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "ok", message = "Telemetry recorded." });
    }

    // ── Room Leave ───────────────────────────────────────────

    public static async Task<IResult> RoomLeave(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "roomId", "");
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profile ID" });
        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE room_seats SET status = 'left' WHERE room_id = $rid AND profile_id = $pid";
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.ExecuteNonQuery();
        TouchRoom(conn, roomId);

        return Results.Ok(new { status = "ok", message = "Left room." });
    }

    // ── Room Report ──────────────────────────────────────────

    public static async Task<IResult> RoomReport(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "report.roomId", GetString(body, "roomId", ""));
        var reporterId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "report.reporterProfileId", "");
        var targetId = GetString(body, "report.targetProfileId", GetString(body, "targetProfileId", ""));
        var reason = GetString(body, "report.reason", GetString(body, "reason", ""));
        var details = GetString(body, "report.details", GetString(body, "details", ""));

        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });
        if (string.IsNullOrWhiteSpace(reporterId))
            return Results.BadRequest(new { error = "missing reporterProfileId" });
        if (string.IsNullOrWhiteSpace(reason))
            return Results.BadRequest(new { error = "missing reason" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO room_reports (room_id, reporter_profile_id, target_profile_id, reason, details, reported_at)
            VALUES ($rid, $reporter, $target, $reason, $details, $now)
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$reporter", reporterId);
        cmd.Parameters.AddWithValue("$target", targetId);
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$details", details);
        cmd.Parameters.AddWithValue("$now", Now());
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "ok", message = "Report submitted." });
    }

    // ── Room Matchmake ───────────────────────────────────────

    public static async Task<IResult> RoomMatchmake(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "matchmake.playerProfileId", "");
        var callsign = GetString(body, "matchmake.playerCallsign", GetString(body, "playerCallsign", "Runner"));
        var boardCode = GetString(body, "matchmake.boardCode", GetString(body, "boardCode", ""));
        var now = Now();

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });
        if (string.IsNullOrWhiteSpace(boardCode))
            return Results.BadRequest(new { error = "missing boardCode" });

        using var conn = Database.Open();
        CleanupStaleRooms(conn);

        // Atomic find-or-create + seat insert to prevent race conditions
        using var tx = conn.BeginTransaction();
        string roomId, roomTitle;
        var ticketId = NewId("MATCH");
        var joinToken = $"match-{Guid.NewGuid():N}";
        try
        {
            using var find = conn.CreateCommand();
            find.Transaction = tx;
            find.CommandText = """
                SELECT room_id, title, board_code FROM rooms
                WHERE status = 'lobby' AND board_code = $code
                ORDER BY updated_at DESC LIMIT 1
            """;
            find.Parameters.AddWithValue("$code", boardCode);
            using var reader = find.ExecuteReader();

            if (reader.Read())
            {
                roomId = reader.GetString(0);
                roomTitle = reader.GetString(1);
                reader.Close();
            }
            else
            {
                reader.Close();
                roomId = NewId("ROOM");
                roomTitle = $"Quick Match {boardCode}";
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = """
                    INSERT INTO rooms (room_id, title, host_profile_id, host_callsign, board_code, board_title, status, region, created_at, updated_at)
                    VALUES ($rid, $title, $pid, $cs, $code, '', 'lobby', 'global', $now, $now)
                """;
                ins.Parameters.AddWithValue("$rid", roomId);
                ins.Parameters.AddWithValue("$title", roomTitle);
                ins.Parameters.AddWithValue("$pid", profileId);
                ins.Parameters.AddWithValue("$cs", callsign);
                ins.Parameters.AddWithValue("$code", boardCode);
                ins.Parameters.AddWithValue("$now", now);
                ins.ExecuteNonQuery();
            }

            InsertSeat(conn, tx, roomId, profileId, callsign, ticketId, joinToken, "runner", now);
            TouchRoom(conn, roomId, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return Results.Ok(new
        {
            status = "matched",
            message = $"Matched into room {roomId}.",
            roomId,
            roomTitle,
            boardCode,
            ticketId,
            joinToken,
            seatLabel = "runner",
            transportHint = "relay_room",
            relayEndpoint = $"ws://localhost:5000/ws/relay/{roomId}",
            requestedAtUnixSeconds = now,
            expiresAtUnixSeconds = now + 3600
        });
    }

    // ── Room Seat Lease ──────────────────────────────────────

    public static async Task<IResult> RoomSeatLease(HttpRequest request)
    {
        var body = await ReadBody(request);
        var roomId = GetString(body, "lease.roomId", GetString(body, "roomId", ""));
        var profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? GetString(body, "lease.playerProfileId", "");
        var ticketId = GetString(body, "lease.ticketId", GetString(body, "ticketId", ""));
        var now = Now();

        if (string.IsNullOrWhiteSpace(roomId))
            return Results.BadRequest(new { error = "missing roomId" });
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE room_seats SET expires_at = $exp WHERE room_id = $rid AND profile_id = $pid";
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$exp", now + 3600);
        cmd.ExecuteNonQuery();

        return Results.Ok(new
        {
            status = "renewed",
            message = "Seat lease renewed.",
            ticketId,
            expiresAtUnixSeconds = now + 3600
        });
    }

    // ── Achievement Sync ────────────────────────────────────

    public static async Task<IResult> AchievementSync(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        var achievementIds = GetStringArray(body, "achievementIds");
        if (achievementIds.Length == 0)
            return Results.Ok(new { synced = 0, total = 0 });

        var synced = 0;
        using var conn = Database.Open();
        foreach (var achievementId in achievementIds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO achievements (profile_id, achievement_id)
                VALUES ($pid, $aid)
                ON CONFLICT(profile_id, achievement_id) DO NOTHING
            """;
            cmd.Parameters.AddWithValue("$pid", profileId);
            cmd.Parameters.AddWithValue("$aid", achievementId);
            synced += cmd.ExecuteNonQuery();
        }

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM achievements WHERE profile_id = $pid";
        countCmd.Parameters.AddWithValue("$pid", profileId);
        var total = (int)(long)(countCmd.ExecuteScalar() ?? 0);

        return Results.Ok(new { synced, total });
    }

    // ── Achievement List ──────────────────────────────────

    public static IResult AchievementList(HttpRequest request, string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT achievement_id, unlocked_at FROM achievements WHERE profile_id = $pid ORDER BY unlocked_at ASC";
        cmd.Parameters.AddWithValue("$pid", profileId);

        var achievements = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            achievements.Add(new
            {
                achievementId = reader.GetString(0),
                unlockedAt = reader.GetString(1)
            });
        }

        return Results.Ok(new { achievements });
    }

    // ── Daily Complete ────────────────────────────────────

    public static async Task<IResult> DailyComplete(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        var date = GetString(body, "date", "");
        if (string.IsNullOrWhiteSpace(date))
            return Results.BadRequest(new { error = "missing date" });

        var score = GetInt(body, "score", 0);
        if (score < 0 || score > 999999)
            return Results.BadRequest(new { error = "invalid score" });

        using var conn = Database.Open();

        // Check existing personal best
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT score FROM daily_completions WHERE profile_id = $pid AND daily_date = $date";
        checkCmd.Parameters.AddWithValue("$pid", profileId);
        checkCmd.Parameters.AddWithValue("$date", date);
        var existing = checkCmd.ExecuteScalar();
        var previousBest = existing != null ? (int)(long)existing : -1;

        var isNewBest = previousBest < 0 || score > previousBest;

        if (previousBest < 0)
        {
            // Insert new
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO daily_completions (profile_id, daily_date, score)
                VALUES ($pid, $date, $score)
            """;
            ins.Parameters.AddWithValue("$pid", profileId);
            ins.Parameters.AddWithValue("$date", date);
            ins.Parameters.AddWithValue("$score", score);
            ins.ExecuteNonQuery();
        }
        else if (score > previousBest)
        {
            // Update if higher score
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE daily_completions SET score = $score, completed_at = datetime('now')
                WHERE profile_id = $pid AND daily_date = $date
            """;
            upd.Parameters.AddWithValue("$pid", profileId);
            upd.Parameters.AddWithValue("$date", date);
            upd.Parameters.AddWithValue("$score", score);
            upd.ExecuteNonQuery();
        }

        var personalBest = Math.Max(score, previousBest);

        return Results.Ok(new { personalBest, isNewBest });
    }

    // ── Daily Leaderboard ─────────────────────────────────

    public static IResult DailyLeaderboard(HttpRequest request, string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return Results.BadRequest(new { error = "missing date" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT profile_id, score, completed_at
            FROM daily_completions
            WHERE daily_date = $date
            ORDER BY score DESC
            LIMIT 20
        """;
        cmd.Parameters.AddWithValue("$date", date);

        var entries = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new
            {
                profileId = reader.GetString(0),
                score = reader.GetInt32(1),
                completedAt = reader.GetString(2)
            });
        }

        return Results.Ok(new { date, entries });
    }

    // ── Helpers ──────────────────────────────────────────────

    private record RoomInfo(string RoomId, string Title, string BoardCode, string Status, bool UsesLockedDeck, string[] LockedDeckUnitIds);

    private static RoomInfo? GetRoom(SqliteConnection conn, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT room_id, title, board_code, status, uses_locked_deck, locked_deck_unit_ids FROM rooms WHERE room_id = $rid";
        cmd.Parameters.AddWithValue("$rid", roomId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var deckStr = reader.GetString(5);
        return new RoomInfo(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetInt32(4) == 1,
            string.IsNullOrWhiteSpace(deckStr) ? [] : deckStr.Split(','));
    }

    private static void InsertSeat(SqliteConnection conn, SqliteTransaction tx, string roomId, string profileId, string callsign, string ticketId, string joinToken, string seatLabel, long now)
    {
        // Check for existing seat
        using var checkCmd = conn.CreateCommand();
        checkCmd.Transaction = tx;
        checkCmd.CommandText = "SELECT status FROM room_seats WHERE room_id = $rid AND profile_id = $pid";
        checkCmd.Parameters.AddWithValue("$rid", roomId);
        checkCmd.Parameters.AddWithValue("$pid", profileId);
        var existingStatus = checkCmd.ExecuteScalar() as string;

        if (existingStatus != null && existingStatus != "left" && existingStatus != "expired")
        {
            throw new SqliteException("Seat already exists for this profile in this room.", 19);
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = existingStatus != null
            ? """
                UPDATE room_seats SET callsign = $cs, ticket_id = $tid, join_token = $tok,
                    seat_label = $seat, status = 'joined', joined_at = $now, expires_at = $exp,
                    is_ready = 0, score = 0, elapsed_seconds = 0, hull_remaining = 0, enemy_defeats = 0, race_status = 'prep'
                WHERE room_id = $rid AND profile_id = $pid
              """
            : """
                INSERT INTO room_seats (room_id, profile_id, callsign, ticket_id, join_token, seat_label, status, joined_at, expires_at)
                VALUES ($rid, $pid, $cs, $tid, $tok, $seat, 'joined', $now, $exp)
              """;
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$cs", callsign);
        cmd.Parameters.AddWithValue("$tid", ticketId);
        cmd.Parameters.AddWithValue("$tok", joinToken);
        cmd.Parameters.AddWithValue("$seat", seatLabel);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$exp", now + 3600);
        cmd.ExecuteNonQuery();
    }

    private static int CountActiveSeats(SqliteConnection conn, string roomId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM room_seats WHERE room_id = $rid AND status != 'left'";
        cmd.Parameters.AddWithValue("$rid", roomId);
        return (int)(long)(cmd.ExecuteScalar() ?? 0);
    }

    private static void TouchRoom(SqliteConnection conn, string roomId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE rooms SET updated_at = $now WHERE room_id = $rid";
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$now", Now());
        cmd.ExecuteNonQuery();
    }

    private static int GetProvisionalRank(SqliteConnection conn, string roomId, string profileId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) + 1 FROM room_seats
            WHERE room_id = $rid AND status != 'left' AND seat_label != 'spectator'
                  AND score > (SELECT COALESCE(MAX(score), 0) FROM room_seats WHERE room_id = $rid AND profile_id = $pid)
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);
        cmd.Parameters.AddWithValue("$pid", profileId);
        return (int)(long)(cmd.ExecuteScalar() ?? 1);
    }

    private static void CleanupStaleRooms(SqliteConnection conn)
    {
        var cutoff = Now() - 7200;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE rooms SET status = 'expired' WHERE status IN ('lobby', 'racing') AND updated_at < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        cmd.ExecuteNonQuery();
    }

    private static List<object> GetPeerSnapshots(SqliteConnection conn, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT profile_id, callsign, seat_label, is_ready, race_status, score, elapsed_seconds, hull_remaining, enemy_defeats
            FROM room_seats WHERE room_id = $rid AND status != 'left'
        """;
        cmd.Parameters.AddWithValue("$rid", roomId);

        var peers = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            peers.Add(new
            {
                playerProfileId = reader.GetString(0),
                callsign = reader.GetString(1),
                seatLabel = reader.GetString(2),
                isReady = reader.GetInt32(3) == 1,
                raceStatus = reader.GetString(4),
                score = reader.GetInt32(5),
                elapsedSeconds = reader.GetDouble(6),
                hullRemaining = reader.GetDouble(7),
                enemyDefeats = reader.GetInt32(8)
            });
        }

        return peers;
    }

    private static void UpsertPlayer(SqliteConnection conn, SqliteTransaction tx, string profileId, string callsign, long now)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO players (profile_id, callsign, synced_at, created_at)
            VALUES ($pid, $cs, $now, $now)
            ON CONFLICT(profile_id) DO UPDATE SET callsign = $cs, synced_at = $now
        """;
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$cs", callsign);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    // ── Purchase Validation ─────────────────────────────────

    private static readonly Dictionary<string, (int Gold, int Food, bool UnitUnlock)> ProductRewards = new()
    {
        ["gold_pouch"] = (500, 0, false),
        ["gold_chest"] = (2200, 0, false),
        ["gold_warchest"] = (6900, 0, false),
        ["gold_treasury"] = (18000, 0, false),
        ["food_rations"] = (0, 20, false),
        ["food_provisions"] = (0, 65, false),
        ["food_stockpile"] = (0, 170, false),
        ["food_granary"] = (0, 460, false),
        ["starter_kit"] = (800, 30, true),
        ["campaign_resupply"] = (3000, 80, false),
    };

    public static async Task<IResult> PurchaseValidate(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var productId = GetString(body, "productId", "");
        var platform = GetString(body, "platform", "");
        var receiptToken = GetString(body, "receiptToken", "");
        var transactionId = GetString(body, "transactionId", "");
        var now = Now();

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });
        if (string.IsNullOrWhiteSpace(productId))
            return Results.BadRequest(new { error = "missing productId" });
        if (string.IsNullOrWhiteSpace(platform))
            return Results.BadRequest(new { error = "missing platform" });
        if (string.IsNullOrWhiteSpace(receiptToken))
            return Results.BadRequest(new { error = "missing receiptToken" });

        if (!ProductRewards.TryGetValue(productId, out var reward))
            return Results.BadRequest(new { error = "unknown productId" });

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        // Duplicate receipt check
        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            using var dupCheck = conn.CreateCommand();
            dupCheck.Transaction = tx;
            dupCheck.CommandText = "SELECT COUNT(*) FROM purchases WHERE transaction_id = $tid AND transaction_id != ''";
            dupCheck.Parameters.AddWithValue("$tid", transactionId);
            var dupCount = (long)(dupCheck.ExecuteScalar() ?? 0);
            if (dupCount > 0)
            {
                tx.Rollback();
                return Results.Json(new { status = "duplicate", message = "This transaction has already been processed." }, statusCode: 409);
            }
        }

        // Velocity check: max 10 purchases per profile per hour
        using var velocityCheck = conn.CreateCommand();
        velocityCheck.Transaction = tx;
        velocityCheck.CommandText = "SELECT COUNT(*) FROM purchases WHERE profile_id = $pid AND purchased_at > $cutoff";
        velocityCheck.Parameters.AddWithValue("$pid", profileId);
        velocityCheck.Parameters.AddWithValue("$cutoff", now - 3600);
        var recentCount = (long)(velocityCheck.ExecuteScalar() ?? 0);
        if (recentCount >= 10)
        {
            tx.Rollback();
            return Results.Json(new { status = "rate_limited", message = "Too many purchases. Please wait before trying again." }, statusCode: 429);
        }

        // One-time purchase check for starter_kit
        if (productId == "starter_kit")
        {
            using var oneTimeCheck = conn.CreateCommand();
            oneTimeCheck.Transaction = tx;
            oneTimeCheck.CommandText = "SELECT COUNT(*) FROM purchases WHERE profile_id = $pid AND product_id = 'starter_kit'";
            oneTimeCheck.Parameters.AddWithValue("$pid", profileId);
            var starterCount = (long)(oneTimeCheck.ExecuteScalar() ?? 0);
            if (starterCount > 0)
            {
                tx.Rollback();
                return Results.Json(new { status = "already_purchased", message = "Adventurer's Kit can only be purchased once." }, statusCode: 409);
            }
        }

        var purchaseId = NewId("PUR");

        // Record the purchase
        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO purchases (purchase_id, profile_id, product_id, platform, receipt_token, transaction_id,
                gold_credited, food_credited, granted_unit_unlock, status, purchased_at)
            VALUES ($purchaseId, $pid, $productId, $platform, $receipt, $tid,
                $gold, $food, $unitUnlock, 'validated', $now)
        """;
        ins.Parameters.AddWithValue("$purchaseId", purchaseId);
        ins.Parameters.AddWithValue("$pid", profileId);
        ins.Parameters.AddWithValue("$productId", productId);
        ins.Parameters.AddWithValue("$platform", platform);
        ins.Parameters.AddWithValue("$receipt", receiptToken);
        ins.Parameters.AddWithValue("$tid", transactionId);
        ins.Parameters.AddWithValue("$gold", reward.Gold);
        ins.Parameters.AddWithValue("$food", reward.Food);
        ins.Parameters.AddWithValue("$unitUnlock", reward.UnitUnlock ? 1 : 0);
        ins.Parameters.AddWithValue("$now", now);
        ins.ExecuteNonQuery();

        UpsertPlayer(conn, tx, profileId, "", now);
        tx.Commit();

        return Results.Ok(new
        {
            status = "ok",
            message = "Purchase validated and credited.",
            purchaseId,
            productId,
            goldCredited = reward.Gold,
            foodCredited = reward.Food,
            grantedUnitUnlock = reward.UnitUnlock,
            validatedAtUnixSeconds = now
        });
    }

    public static async Task<IResult> PurchaseHistory(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault()
            ?? request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT purchase_id, product_id, platform, gold_credited, food_credited, purchased_at
            FROM purchases WHERE profile_id = $pid ORDER BY purchased_at DESC LIMIT 50
        """;
        cmd.Parameters.AddWithValue("$pid", profileId);

        var purchases = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            purchases.Add(new
            {
                purchaseId = reader.GetString(0),
                productId = reader.GetString(1),
                platform = reader.GetString(2),
                goldCredited = reader.GetInt32(3),
                foodCredited = reader.GetInt32(4),
                purchasedAtUnixSeconds = reader.GetInt64(5)
            });
        }

        return Results.Ok(new { status = "ok", purchases });
    }

    public static async Task<IResult> PurchaseProducts(HttpRequest request)
    {
        var products = new List<object>();
        foreach (var (id, reward) in ProductRewards)
        {
            products.Add(new
            {
                productId = id,
                goldAmount = reward.Gold,
                foodAmount = reward.Food,
                grantsUnitUnlock = reward.UnitUnlock
            });
        }

        return Results.Ok(new { status = "ok", products });
    }

    // ── Analytics ──────────────────────────────────────────

    private const int MaxBatchSize = 50;

    public static async Task<IResult> AnalyticsIngest(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var clientVersion = GetInt(body, "clientVersion", 0);
        var platform = GetString(body, "platform", "");
        var events = GetArray(body, "events");
        var now = Now();

        if (events.Length == 0)
            return Results.Ok(new { status = "ok", accepted = 0 });
        if (events.Length > MaxBatchSize)
            return Results.BadRequest(new { error = $"batch too large, max {MaxBatchSize}" });

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        var accepted = 0;
        foreach (var evt in events)
        {
            var eventType = GetNestedString(evt, "type", "");
            if (string.IsNullOrWhiteSpace(eventType)) continue;
            if (eventType.Length > 64) continue;

            var eventData = GetNestedString(evt, "data", "");
            if (eventData.Length > 2048) eventData = eventData[..2048];

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO analytics_events (profile_id, event_type, event_data, client_version, platform, recorded_at)
                VALUES ($pid, $type, $data, $ver, $plat, $now)
            """;
            cmd.Parameters.AddWithValue("$pid", profileId);
            cmd.Parameters.AddWithValue("$type", eventType);
            cmd.Parameters.AddWithValue("$data", eventData);
            cmd.Parameters.AddWithValue("$ver", clientVersion);
            cmd.Parameters.AddWithValue("$plat", platform);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
            accepted++;
        }

        tx.Commit();
        return Results.Ok(new { status = "ok", accepted });
    }

    public static async Task<IResult> AnalyticsSummary(HttpRequest request)
    {
        var eventType = request.Query["type"].FirstOrDefault() ?? "";
        var hours = Math.Clamp(GetInt(await ReadBody(request), "hours", 24), 1, 720);
        var cutoff = Now() - (hours * 3600);

        using var conn = Database.Open();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT event_data, COUNT(*) as cnt FROM analytics_events
                WHERE event_type = $type AND recorded_at > $cutoff
                GROUP BY event_data ORDER BY cnt DESC LIMIT 50
            """;
            cmd.Parameters.AddWithValue("$type", eventType);
            cmd.Parameters.AddWithValue("$cutoff", cutoff);

            var rows = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new { data = reader.GetString(0), count = reader.GetInt64(1) });
            }

            return Results.Ok(new { status = "ok", eventType, hours, rows });
        }

        // No type filter — return top event types
        using var topCmd = conn.CreateCommand();
        topCmd.CommandText = """
            SELECT event_type, COUNT(*) as cnt FROM analytics_events
            WHERE recorded_at > $cutoff
            GROUP BY event_type ORDER BY cnt DESC LIMIT 30
        """;
        topCmd.Parameters.AddWithValue("$cutoff", cutoff);

        var types = new List<object>();
        using var topReader = topCmd.ExecuteReader();
        while (topReader.Read())
        {
            types.Add(new { eventType = topReader.GetString(0), count = topReader.GetInt64(1) });
        }

        return Results.Ok(new { status = "ok", hours, types });
    }

    // ── Crash Reports ─────────────────────────────────────

    public static async Task<IResult> CrashReport(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var errorType = GetString(body, "errorType", "unknown");
        var errorMessage = GetString(body, "errorMessage", "");
        var stackTrace = GetString(body, "stackTrace", "");
        var clientVersion = GetInt(body, "clientVersion", 0);
        var platform = GetString(body, "platform", "");
        var scene = GetString(body, "scene", "");
        var now = Now();

        if (string.IsNullOrWhiteSpace(errorMessage) && string.IsNullOrWhiteSpace(errorType))
            return Results.BadRequest(new { error = "missing error details" });

        // Truncate large stack traces
        if (stackTrace.Length > 4096)
            stackTrace = stackTrace[..4096];

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crash_reports (profile_id, error_type, error_message, stack_trace, client_version, platform, scene, reported_at)
            VALUES ($pid, $type, $msg, $trace, $ver, $plat, $scene, $now)
        """;
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$type", errorType);
        cmd.Parameters.AddWithValue("$msg", errorMessage.Length > 512 ? errorMessage[..512] : errorMessage);
        cmd.Parameters.AddWithValue("$trace", stackTrace);
        cmd.Parameters.AddWithValue("$ver", clientVersion);
        cmd.Parameters.AddWithValue("$plat", platform);
        cmd.Parameters.AddWithValue("$scene", scene);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "ok", message = "Crash report recorded." });
    }

    // ── Cloud Save ─────────────────────────────────────────

    private const int MaxSaveDataBytes = 512 * 1024; // 512 KB

    public static async Task<IResult> CloudSaveUpload(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var saveData = GetString(body, "saveData", "");
        var saveVersion = GetInt(body, "saveVersion", 0);
        var now = Now();

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });
        if (string.IsNullOrWhiteSpace(saveData))
            return Results.BadRequest(new { error = "missing saveData" });
        if (saveData.Length > MaxSaveDataBytes)
            return Results.Json(new { error = "save data too large" }, statusCode: 413);

        var saveHash = ComputeHash(saveData);

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        UpsertPlayer(conn, tx, profileId, "", now);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO cloud_saves (profile_id, save_data, save_version, save_hash, uploaded_at)
            VALUES ($pid, $data, $ver, $hash, $now)
            ON CONFLICT(profile_id) DO UPDATE SET
                save_data = $data, save_version = $ver, save_hash = $hash, uploaded_at = $now
        """;
        cmd.Parameters.AddWithValue("$pid", profileId);
        cmd.Parameters.AddWithValue("$data", saveData);
        cmd.Parameters.AddWithValue("$ver", saveVersion);
        cmd.Parameters.AddWithValue("$hash", saveHash);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();

        tx.Commit();

        return Results.Ok(new
        {
            status = "ok",
            message = "Save uploaded.",
            saveVersion,
            saveHash,
            uploadedAtUnixSeconds = now
        });
    }

    public static async Task<IResult> CloudSaveDownload(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault()
            ?? request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT save_data, save_version, save_hash, uploaded_at FROM cloud_saves WHERE profile_id = $pid";
        cmd.Parameters.AddWithValue("$pid", profileId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return Results.Ok(new { status = "empty", message = "No cloud save found." });
        }

        return Results.Ok(new
        {
            status = "ok",
            saveData = reader.GetString(0),
            saveVersion = reader.GetInt32(1),
            saveHash = reader.GetString(2),
            uploadedAtUnixSeconds = reader.GetInt64(3)
        });
    }

    public static async Task<IResult> CloudSaveInfo(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault()
            ?? request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT save_version, save_hash, uploaded_at, length(save_data) FROM cloud_saves WHERE profile_id = $pid";
        cmd.Parameters.AddWithValue("$pid", profileId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return Results.Ok(new { status = "empty", message = "No cloud save found." });
        }

        return Results.Ok(new
        {
            status = "ok",
            saveVersion = reader.GetInt32(0),
            saveHash = reader.GetString(1),
            uploadedAtUnixSeconds = reader.GetInt64(2),
            sizeBytes = reader.GetInt64(3)
        });
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes)[..16];
    }

    // ── Stripe Checkout ─────────────────────────────────────

    private static readonly Dictionary<string, (string ProductId, int PriceCents, string Label)> StripePriceMap = new()
    {
        ["price_gold_pouch"] = ("gold_pouch", 99, "Pouch of Gold"),
        ["price_gold_chest"] = ("gold_chest", 399, "Chest of Gold"),
        ["price_gold_warchest"] = ("gold_warchest", 999, "War Chest"),
        ["price_gold_treasury"] = ("gold_treasury", 1999, "King's Treasury"),
        ["price_food_rations"] = ("food_rations", 99, "Field Rations"),
        ["price_food_provisions"] = ("food_provisions", 299, "Caravan Provisions"),
        ["price_food_stockpile"] = ("food_stockpile", 699, "Siege Stockpile"),
        ["price_food_granary"] = ("food_granary", 1499, "Royal Granary"),
        ["price_starter_kit"] = ("starter_kit", 499, "Adventurer's Kit"),
        ["price_campaign_resupply"] = ("campaign_resupply", 799, "Campaign Resupply"),
    };

    private static readonly Dictionary<string, string> ProductToStripePriceId = new()
    {
        ["gold_pouch"] = "price_gold_pouch",
        ["gold_chest"] = "price_gold_chest",
        ["gold_warchest"] = "price_gold_warchest",
        ["gold_treasury"] = "price_gold_treasury",
        ["food_rations"] = "price_food_rations",
        ["food_provisions"] = "price_food_provisions",
        ["food_stockpile"] = "price_food_stockpile",
        ["food_granary"] = "price_food_granary",
        ["starter_kit"] = "price_starter_kit",
        ["campaign_resupply"] = "price_campaign_resupply",
    };

    public static async Task<IResult> StripeCreateCheckout(HttpRequest request)
    {
        var stripeKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(stripeKey))
            return Results.Json(new { error = "Stripe is not configured on this server." }, statusCode: 503);

        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "");
        var productId = GetString(body, "productId", "");
        var successUrl = GetString(body, "successUrl", "");
        var cancelUrl = GetString(body, "cancelUrl", "");

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });
        if (string.IsNullOrWhiteSpace(productId))
            return Results.BadRequest(new { error = "missing productId" });
        if (!ProductToStripePriceId.TryGetValue(productId, out var stripePriceId))
            return Results.BadRequest(new { error = "unknown productId" });
        if (!StripePriceMap.TryGetValue(stripePriceId, out var priceInfo))
            return Results.BadRequest(new { error = "invalid price mapping" });

        if (string.IsNullOrWhiteSpace(successUrl))
            successUrl = "https://crownroad.game/purchase/success";
        if (string.IsNullOrWhiteSpace(cancelUrl))
            cancelUrl = "https://crownroad.game/purchase/cancel";

        Stripe.StripeConfiguration.ApiKey = stripeKey;

        var sessionService = new Stripe.Checkout.SessionService();
        var sessionOptions = new Stripe.Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            ClientReferenceId = profileId,
            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = priceInfo.PriceCents,
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = priceInfo.Label,
                            Description = $"Crownroad — {priceInfo.Label}"
                        }
                    },
                    Quantity = 1
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["profileId"] = profileId,
                ["productId"] = productId,
                ["stripePriceId"] = stripePriceId
            }
        };

        try
        {
            var session = await sessionService.CreateAsync(sessionOptions);
            return Results.Ok(new
            {
                status = "ok",
                checkoutUrl = session.Url,
                sessionId = session.Id,
                productId,
                priceCents = priceInfo.PriceCents
            });
        }
        catch (Stripe.StripeException ex)
        {
            return Results.Json(new { error = $"Stripe error: {ex.Message}" }, statusCode: 502);
        }
    }

    public static async Task<IResult> StripeWebhook(HttpRequest request)
    {
        var stripeKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
        if (string.IsNullOrWhiteSpace(stripeKey))
            return Results.Json(new { error = "Stripe is not configured." }, statusCode: 503);

        Stripe.StripeConfiguration.ApiKey = stripeKey;

        string payload;
        using (var reader = new StreamReader(request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        Stripe.Event stripeEvent;
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            var signature = request.Headers["Stripe-Signature"].FirstOrDefault() ?? "";
            try
            {
                stripeEvent = Stripe.EventUtility.ConstructEvent(payload, signature, webhookSecret);
            }
            catch (Stripe.StripeException)
            {
                return Results.Json(new { error = "Invalid webhook signature." }, statusCode: 400);
            }
        }
        else
        {
            try
            {
                stripeEvent = Stripe.EventUtility.ParseEvent(payload);
            }
            catch
            {
                return Results.Json(new { error = "Invalid event payload." }, statusCode: 400);
            }
        }

        if (stripeEvent.Type != Stripe.EventTypes.CheckoutSessionCompleted)
        {
            return Results.Ok(new { status = "ignored", eventType = stripeEvent.Type });
        }

        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session == null)
            return Results.Ok(new { status = "ignored", reason = "no session object" });

        var profileId = session.Metadata?.GetValueOrDefault("profileId") ?? session.ClientReferenceId ?? "";
        var productId = session.Metadata?.GetValueOrDefault("productId") ?? "";

        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(productId))
            return Results.Ok(new { status = "ignored", reason = "missing metadata" });

        if (!ProductRewards.TryGetValue(productId, out var reward))
            return Results.Ok(new { status = "ignored", reason = "unknown product" });

        var transactionId = $"stripe-{session.Id}";
        var now = Now();

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        // Duplicate check
        using var dupCheck = conn.CreateCommand();
        dupCheck.Transaction = tx;
        dupCheck.CommandText = "SELECT COUNT(*) FROM purchases WHERE transaction_id = $tid";
        dupCheck.Parameters.AddWithValue("$tid", transactionId);
        var dupCount = (long)(dupCheck.ExecuteScalar() ?? 0);
        if (dupCount > 0)
        {
            tx.Rollback();
            return Results.Ok(new { status = "duplicate", message = "Already processed." });
        }

        // One-time check
        if (productId == "starter_kit")
        {
            using var oneTimeCheck = conn.CreateCommand();
            oneTimeCheck.Transaction = tx;
            oneTimeCheck.CommandText = "SELECT COUNT(*) FROM purchases WHERE profile_id = $pid AND product_id = 'starter_kit'";
            oneTimeCheck.Parameters.AddWithValue("$pid", profileId);
            var starterCount = (long)(oneTimeCheck.ExecuteScalar() ?? 0);
            if (starterCount > 0)
            {
                tx.Rollback();
                return Results.Ok(new { status = "skipped", reason = "starter_kit already purchased" });
            }
        }

        var purchaseId = NewId("PUR");

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO purchases (purchase_id, profile_id, product_id, platform, receipt_token, transaction_id,
                gold_credited, food_credited, granted_unit_unlock, status, purchased_at)
            VALUES ($purchaseId, $pid, $productId, 'stripe', $receipt, $tid,
                $gold, $food, $unitUnlock, 'validated', $now)
        """;
        ins.Parameters.AddWithValue("$purchaseId", purchaseId);
        ins.Parameters.AddWithValue("$pid", profileId);
        ins.Parameters.AddWithValue("$productId", productId);
        ins.Parameters.AddWithValue("$receipt", $"stripe-checkout-{session.PaymentIntentId}");
        ins.Parameters.AddWithValue("$tid", transactionId);
        ins.Parameters.AddWithValue("$gold", reward.Gold);
        ins.Parameters.AddWithValue("$food", reward.Food);
        ins.Parameters.AddWithValue("$unitUnlock", reward.UnitUnlock ? 1 : 0);
        ins.Parameters.AddWithValue("$now", now);
        ins.ExecuteNonQuery();

        UpsertPlayer(conn, tx, profileId, "", now);
        tx.Commit();

        return Results.Ok(new
        {
            status = "ok",
            message = "Stripe payment fulfilled.",
            purchaseId,
            productId,
            goldCredited = reward.Gold,
            foodCredited = reward.Food
        });
    }

    public static async Task<IResult> StripeCheckoutStatus(HttpRequest request)
    {
        var stripeKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(stripeKey))
            return Results.Json(new { error = "Stripe is not configured." }, statusCode: 503);

        var sessionId = request.Query["sessionId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(sessionId))
            return Results.BadRequest(new { error = "missing sessionId" });

        Stripe.StripeConfiguration.ApiKey = stripeKey;

        try
        {
            var sessionService = new Stripe.Checkout.SessionService();
            var session = await sessionService.GetAsync(sessionId);

            return Results.Ok(new
            {
                status = "ok",
                paymentStatus = session.PaymentStatus,
                productId = session.Metadata?.GetValueOrDefault("productId") ?? "",
                profileId = session.Metadata?.GetValueOrDefault("profileId") ?? session.ClientReferenceId ?? "",
                completed = session.PaymentStatus == "paid"
            });
        }
        catch (Stripe.StripeException ex)
        {
            return Results.Json(new { error = $"Stripe error: {ex.Message}" }, statusCode: 502);
        }
    }

    // ── JSON Parsing Helpers ─────────────────────────────────

    private static async Task<JsonElement> ReadBody(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        var text = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text)) return default;
        try { return JsonDocument.Parse(text).RootElement; }
        catch { return default; }
    }

    private static string GetString(JsonElement root, string path, string fallback)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return fallback;
            if (!current.TryGetProperty(segment, out var next))
            {
                var found = false;
                foreach (var prop in current.EnumerateObject())
                {
                    if (string.Equals(prop.Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        current = prop.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return fallback;
            }
            else
            {
                current = next;
            }
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? fallback : fallback;
    }

    private static int GetInt(JsonElement root, string path, int fallback)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return fallback;
            if (!current.TryGetProperty(segment, out current)) return fallback;
        }
        return current.TryGetInt32(out var val) ? val : fallback;
    }

    private static double GetDouble(JsonElement root, string path, double fallback)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return fallback;
            if (!current.TryGetProperty(segment, out current)) return fallback;
        }
        return current.TryGetDouble(out var val) ? val : fallback;
    }

    private static bool GetBool(JsonElement root, string path, bool fallback)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return fallback;
            if (!current.TryGetProperty(segment, out current)) return fallback;
        }
        return current.ValueKind is JsonValueKind.True or JsonValueKind.False ? current.GetBoolean() : fallback;
    }

    private static string[] GetStringArray(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return [];
            if (!current.TryGetProperty(segment, out current)) return [];
        }
        if (current.ValueKind != JsonValueKind.Array) return [];
        var result = new List<string>();
        foreach (var item in current.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) result.Add(s);
            }
        }
        return result.ToArray();
    }

    private static JsonElement[] GetArray(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return [];
            if (!current.TryGetProperty(segment, out current)) return [];
        }
        if (current.ValueKind != JsonValueKind.Array) return [];
        var result = new List<JsonElement>();
        foreach (var item in current.EnumerateArray()) result.Add(item);
        return result.ToArray();
    }

    private static string GetNestedString(JsonElement el, string prop, string fallback) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static int GetNestedInt(JsonElement el, string prop, int fallback) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : fallback;

    private static double GetNestedDouble(JsonElement el, string prop, double fallback) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.TryGetDouble(out var n) ? n : fallback;

    private static bool GetNestedBool(JsonElement el, string prop, bool fallback) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean() : fallback;

    // ── Arena ────────────────────────────────────────────────

    public static async Task<IResult> ArenaUploadSnapshot(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        var deckUnitIds = GetString(body, "deckUnitIds", "");
        var deckSpellIds = GetString(body, "deckSpellIds", "");
        var unitLevels = GetString(body, "unitLevels", "");
        var unitEquipment = GetString(body, "unitEquipment", "");
        var powerRating = GetInt(body, "powerRating", 0);
        var arenaRating = GetInt(body, "arenaRating", 1000);
        var now = Now();

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO arena_snapshots (profile_id, deck_unit_ids, deck_spell_ids, unit_levels, unit_equipment, power_rating, arena_rating, updated_at)
            VALUES (@pid, @deck, @spells, @levels, @equip, @power, @rating, @now)
            ON CONFLICT(profile_id) DO UPDATE SET deck_unit_ids=@deck, deck_spell_ids=@spells, unit_levels=@levels, unit_equipment=@equip, power_rating=@power, arena_rating=@rating, updated_at=@now";
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.Parameters.AddWithValue("@deck", deckUnitIds);
        cmd.Parameters.AddWithValue("@spells", deckSpellIds);
        cmd.Parameters.AddWithValue("@levels", unitLevels);
        cmd.Parameters.AddWithValue("@equip", unitEquipment);
        cmd.Parameters.AddWithValue("@power", powerRating);
        cmd.Parameters.AddWithValue("@rating", arenaRating);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "ok" });
    }

    public static async Task<IResult> ArenaFindOpponents(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault() ?? "";
        var rating = int.TryParse(request.Query["rating"].FirstOrDefault(), out var r) ? r : 1000;

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT profile_id, deck_unit_ids, deck_spell_ids, unit_levels, unit_equipment, power_rating, arena_rating
            FROM arena_snapshots WHERE profile_id != @pid AND arena_rating BETWEEN @lo AND @hi ORDER BY ABS(arena_rating - @rating) LIMIT 3";
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.Parameters.AddWithValue("@lo", rating - 200);
        cmd.Parameters.AddWithValue("@hi", rating + 200);
        cmd.Parameters.AddWithValue("@rating", rating);

        var opponents = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            opponents.Add(new
            {
                profileId = reader.GetString(0),
                deckUnitIds = reader.GetString(1),
                deckSpellIds = reader.GetString(2),
                unitLevels = reader.GetString(3),
                unitEquipment = reader.GetString(4),
                powerRating = reader.GetInt32(5),
                arenaRating = reader.GetInt32(6)
            });
        }

        return Results.Ok(new { opponents });
    }

    public static async Task<IResult> ArenaReportResult(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var opponentProfileId = GetString(body, "opponentProfileId", "");
        var won = GetBool(body, "won", false);
        var ratingBefore = GetInt(body, "ratingBefore", 1000);

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        // Simple Elo: K=32
        var opponentRating = ratingBefore;
        using var conn = Database.Open();
        if (!string.IsNullOrWhiteSpace(opponentProfileId))
        {
            using var lookupCmd = conn.CreateCommand();
            lookupCmd.CommandText = "SELECT arena_rating FROM arena_snapshots WHERE profile_id = @pid";
            lookupCmd.Parameters.AddWithValue("@pid", opponentProfileId);
            var oppRatingObj = lookupCmd.ExecuteScalar();
            if (oppRatingObj is long lr) opponentRating = (int)lr;
        }

        var expected = 1.0 / (1.0 + Math.Pow(10, (opponentRating - ratingBefore) / 400.0));
        var actual = won ? 1.0 : 0.0;
        var newRating = Math.Max(0, ratingBefore + (int)(32 * (actual - expected)));

        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"INSERT INTO arena_results (profile_id, opponent_profile_id, won, rating_before, rating_after, played_at)
            VALUES (@pid, @oid, @won, @before, @after, @now)";
        insertCmd.Parameters.AddWithValue("@pid", profileId);
        insertCmd.Parameters.AddWithValue("@oid", opponentProfileId ?? "");
        insertCmd.Parameters.AddWithValue("@won", won ? 1 : 0);
        insertCmd.Parameters.AddWithValue("@before", ratingBefore);
        insertCmd.Parameters.AddWithValue("@after", newRating);
        insertCmd.Parameters.AddWithValue("@now", Now());
        insertCmd.ExecuteNonQuery();

        // Update player's arena rating in snapshot
        using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE arena_snapshots SET arena_rating = @rating, updated_at = @now WHERE profile_id = @pid";
        updateCmd.Parameters.AddWithValue("@rating", newRating);
        updateCmd.Parameters.AddWithValue("@now", Now());
        updateCmd.Parameters.AddWithValue("@pid", profileId);
        updateCmd.ExecuteNonQuery();

        return Results.Ok(new { newRating, ratingDelta = newRating - ratingBefore });
    }

    // ── Guild ────────────────────────────────────────────────

    public static async Task<IResult> GuildCreate(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var name = GetString(body, "name", "Unnamed Warband");

        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        var guildId = NewId("GLD");
        var now = Now();

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        using var createCmd = conn.CreateCommand();
        createCmd.Transaction = tx;
        createCmd.CommandText = @"INSERT INTO guilds (guild_id, name, leader_profile_id, tier, experience, created_at)
            VALUES (@gid, @name, @pid, 1, 0, @now)";
        createCmd.Parameters.AddWithValue("@gid", guildId);
        createCmd.Parameters.AddWithValue("@name", name);
        createCmd.Parameters.AddWithValue("@pid", profileId);
        createCmd.Parameters.AddWithValue("@now", now);
        createCmd.ExecuteNonQuery();

        using var joinCmd = conn.CreateCommand();
        joinCmd.Transaction = tx;
        joinCmd.CommandText = @"INSERT INTO guild_members (guild_id, profile_id, contribution_points, joined_at)
            VALUES (@gid, @pid, 0, @now)";
        joinCmd.Parameters.AddWithValue("@gid", guildId);
        joinCmd.Parameters.AddWithValue("@pid", profileId);
        joinCmd.Parameters.AddWithValue("@now", now);
        joinCmd.ExecuteNonQuery();

        tx.Commit();
        return Results.Ok(new { guildId, name });
    }

    public static async Task<IResult> GuildJoin(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var guildId = GetString(body, "guildId", "");

        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(guildId))
            return Results.BadRequest(new { error = "missing profileId or guildId" });

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        // Check member limit
        using var countCmd = conn.CreateCommand();
        countCmd.Transaction = tx;
        countCmd.CommandText = "SELECT COUNT(*) FROM guild_members WHERE guild_id = @gid";
        countCmd.Parameters.AddWithValue("@gid", guildId);
        var count = (long)(countCmd.ExecuteScalar() ?? 0);

        using var tierCmd = conn.CreateCommand();
        tierCmd.Transaction = tx;
        tierCmd.CommandText = "SELECT tier FROM guilds WHERE guild_id = @gid";
        tierCmd.Parameters.AddWithValue("@gid", guildId);
        var tier = (int)((long)(tierCmd.ExecuteScalar() ?? 1));
        var maxMembers = tier switch { 2 => 10, 3 => 20, 4 => 30, 5 => 50, _ => 5 };

        if (count >= maxMembers)
        {
            tx.Rollback();
            return Results.Json(new { error = "Guild is full" }, statusCode: 409);
        }

        using var joinCmd = conn.CreateCommand();
        joinCmd.Transaction = tx;
        joinCmd.CommandText = @"INSERT OR IGNORE INTO guild_members (guild_id, profile_id, contribution_points, joined_at)
            VALUES (@gid, @pid, 0, @now)";
        joinCmd.Parameters.AddWithValue("@gid", guildId);
        joinCmd.Parameters.AddWithValue("@pid", profileId);
        joinCmd.Parameters.AddWithValue("@now", Now());
        joinCmd.ExecuteNonQuery();

        tx.Commit();
        return Results.Ok(new { guildId, status = "joined" });
    }

    public static async Task<IResult> GuildLeave(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var guildId = GetString(body, "guildId", "");

        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(guildId))
            return Results.BadRequest(new { error = "missing profileId or guildId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM guild_members WHERE guild_id = @gid AND profile_id = @pid";
        cmd.Parameters.AddWithValue("@gid", guildId);
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "left" });
    }

    public static async Task<IResult> GuildInfo(HttpRequest request)
    {
        var guildId = request.Query["guildId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(guildId))
            return Results.BadRequest(new { error = "missing guildId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT guild_id, name, leader_profile_id, tier, experience FROM guilds WHERE guild_id = @gid";
        cmd.Parameters.AddWithValue("@gid", guildId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Results.NotFound(new { error = "guild not found" });

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM guild_members WHERE guild_id = @gid";
        countCmd.Parameters.AddWithValue("@gid", guildId);
        var memberCount = (int)((long)(countCmd.ExecuteScalar() ?? 0));

        var tierVal = reader.GetInt32(3);
        var perkIds = new List<string>();
        if (tierVal >= 1) perkIds.Add("guild_vitality");
        if (tierVal >= 2) perkIds.Add("guild_prosperity");
        if (tierVal >= 3) perkIds.Add("guild_provisions");
        if (tierVal >= 4) perkIds.Add("guild_haste");
        if (tierVal >= 5) perkIds.Add("guild_fortune");

        return Results.Ok(new
        {
            guildId = reader.GetString(0),
            name = reader.GetString(1),
            leaderProfileId = reader.GetString(2),
            tier = tierVal,
            experience = reader.GetInt32(4),
            memberCount,
            activePerkIds = perkIds.ToArray()
        });
    }

    public static async Task<IResult> GuildMembers(HttpRequest request)
    {
        var guildId = request.Query["guildId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(guildId))
            return Results.BadRequest(new { error = "missing guildId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT gm.profile_id, COALESCE(pp.callsign, 'Unknown'), gm.contribution_points
            FROM guild_members gm LEFT JOIN player_profiles pp ON gm.profile_id = pp.profile_id
            WHERE gm.guild_id = @gid ORDER BY gm.contribution_points DESC";
        cmd.Parameters.AddWithValue("@gid", guildId);

        var members = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            members.Add(new
            {
                profileId = reader.GetString(0),
                callsign = reader.GetString(1),
                contributionPoints = reader.GetInt32(2)
            });
        }

        return Results.Ok(new { guildId, members });
    }

    public static async Task<IResult> GuildContribute(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var guildId = GetString(body, "guildId", "");
        var points = GetInt(body, "points", 10);

        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(guildId))
            return Results.BadRequest(new { error = "missing profileId or guildId" });

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        using var memberCmd = conn.CreateCommand();
        memberCmd.Transaction = tx;
        memberCmd.CommandText = "UPDATE guild_members SET contribution_points = contribution_points + @pts WHERE guild_id = @gid AND profile_id = @pid";
        memberCmd.Parameters.AddWithValue("@pts", points);
        memberCmd.Parameters.AddWithValue("@gid", guildId);
        memberCmd.Parameters.AddWithValue("@pid", profileId);
        memberCmd.ExecuteNonQuery();

        using var guildCmd = conn.CreateCommand();
        guildCmd.Transaction = tx;
        guildCmd.CommandText = "UPDATE guilds SET experience = experience + @pts WHERE guild_id = @gid";
        guildCmd.Parameters.AddWithValue("@pts", points);
        guildCmd.Parameters.AddWithValue("@gid", guildId);
        guildCmd.ExecuteNonQuery();

        // Auto-upgrade tier based on experience
        using var tierCmd = conn.CreateCommand();
        tierCmd.Transaction = tx;
        tierCmd.CommandText = "SELECT experience FROM guilds WHERE guild_id = @gid";
        tierCmd.Parameters.AddWithValue("@gid", guildId);
        var xp = (int)((long)(tierCmd.ExecuteScalar() ?? 0));
        var newTier = xp >= 10000 ? 5 : xp >= 5000 ? 4 : xp >= 2000 ? 3 : xp >= 500 ? 2 : 1;

        using var updateTier = conn.CreateCommand();
        updateTier.Transaction = tx;
        updateTier.CommandText = "UPDATE guilds SET tier = @tier WHERE guild_id = @gid";
        updateTier.Parameters.AddWithValue("@tier", newTier);
        updateTier.Parameters.AddWithValue("@gid", guildId);
        updateTier.ExecuteNonQuery();

        tx.Commit();
        return Results.Ok(new { status = "contributed", points, newTier });
    }

    // ── Live Config ────────────────────────────────────────────

    public static async Task<IResult> LiveConfigGet(HttpRequest request)
    {
        return Results.Ok(new
        {
            announcement = "",
            motd = "Welcome to the Lantern Caravan! New features and events are added regularly.",
            goldMultiplier = 1.0,
            xpMultiplier = 1.0,
            disabledFeatureIds = Array.Empty<string>()
        });
    }

    // ── Leaderboards ─────────────────────────────────────────

    public static async Task<IResult> LeaderboardArena(HttpRequest request)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT s.profile_id, COALESCE(pp.callsign, 'Unknown'), s.arena_rating
            FROM arena_snapshots s LEFT JOIN player_profiles pp ON s.profile_id = pp.profile_id
            ORDER BY s.arena_rating DESC LIMIT 10";

        var entries = new List<object>();
        var rank = 1;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new { rank = rank++, profileId = reader.GetString(0), callsign = reader.GetString(1), rating = reader.GetInt32(2) });
        }

        return Results.Ok(new { entries });
    }

    public static async Task<IResult> LeaderboardTower(HttpRequest request)
    {
        // Tower progress is client-side only; return empty for now
        // In a full implementation, clients would submit their tower progress
        return Results.Ok(new { entries = Array.Empty<object>(), note = "Tower leaderboard requires client submission" });
    }

    // ── Friends ──────────────────────────────────────────────

    public static async Task<IResult> FriendAdd(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var friendId = GetString(body, "friendId", "");
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(friendId))
            return Results.BadRequest(new { error = "missing profileId or friendId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO friendships (profile_id, friend_id, created_at) VALUES (@pid, @fid, @now)";
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.Parameters.AddWithValue("@fid", friendId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "added", friendId });
    }

    public static async Task<IResult> FriendRemove(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var friendId = GetString(body, "friendId", "");
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(friendId))
            return Results.BadRequest(new { error = "missing profileId or friendId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM friendships WHERE profile_id = @pid AND friend_id = @fid";
        cmd.Parameters.AddWithValue("@pid", profileId);
        cmd.Parameters.AddWithValue("@fid", friendId);
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "removed" });
    }

    public static async Task<IResult> FriendList(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT f.friend_id, COALESCE(pp.callsign, 'Unknown')
            FROM friendships f LEFT JOIN player_profiles pp ON f.friend_id = pp.profile_id
            WHERE f.profile_id = @pid ORDER BY f.created_at";
        cmd.Parameters.AddWithValue("@pid", profileId);

        var friends = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            friends.Add(new { profileId = reader.GetString(0), callsign = reader.GetString(1) });
        }

        return Results.Ok(new { friends });
    }

    public static async Task<IResult> FriendGift(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var friendId = GetString(body, "friendId", "");
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(friendId))
            return Results.BadRequest(new { error = "missing profileId or friendId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO friend_gifts (sender_id, receiver_id, sent_at) VALUES (@sid, @rid, @now)";
        cmd.Parameters.AddWithValue("@sid", profileId);
        cmd.Parameters.AddWithValue("@rid", friendId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "gift_sent" });
    }

    // ── Raid ─────────────────────────────────────────────────

    public static async Task<IResult> RaidStatus(HttpRequest request)
    {
        var weekId = request.Query["weekId"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(weekId))
        {
            // Auto-detect current week
            var now = DateTime.UtcNow;
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            var week = cal.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            weekId = $"{now.Year}-W{week:D2}";
        }

        using var conn = Database.Open();

        // Auto-create raid boss for current week if missing
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT damage_dealt FROM raid_bosses WHERE week_id = @wid";
        checkCmd.Parameters.AddWithValue("@wid", weekId);
        var existing = checkCmd.ExecuteScalar();

        long damageDone = 0;
        if (existing == null)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO raid_bosses (week_id, boss_id, total_health, damage_dealt, created_at) VALUES (@wid, @bid, @hp, 0, @now)";
            insertCmd.Parameters.AddWithValue("@wid", weekId);
            insertCmd.Parameters.AddWithValue("@bid", $"raid_boss_{weekId}");
            insertCmd.Parameters.AddWithValue("@hp", 10_000_000);
            insertCmd.Parameters.AddWithValue("@now", Now());
            insertCmd.ExecuteNonQuery();
        }
        else
        {
            damageDone = (long)existing;
        }

        return Results.Ok(new { weekId, damageDone, totalHealth = 10_000_000 });
    }

    public static async Task<IResult> RaidContribute(HttpRequest request)
    {
        var body = await ReadBody(request);
        var profileId = GetString(body, "profileId", "");
        var damage = GetInt(body, "damage", 0);
        var weekId = GetString(body, "weekId", "");

        if (string.IsNullOrWhiteSpace(profileId) || damage <= 0)
            return Results.BadRequest(new { error = "missing profileId or damage" });

        if (string.IsNullOrWhiteSpace(weekId))
        {
            var now = DateTime.UtcNow;
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            var week = cal.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            weekId = $"{now.Year}-W{week:D2}";
        }

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        // Insert contribution
        using var contribCmd = conn.CreateCommand();
        contribCmd.Transaction = tx;
        contribCmd.CommandText = "INSERT INTO raid_contributions (week_id, profile_id, damage, contributed_at) VALUES (@wid, @pid, @dmg, @now)";
        contribCmd.Parameters.AddWithValue("@wid", weekId);
        contribCmd.Parameters.AddWithValue("@pid", profileId);
        contribCmd.Parameters.AddWithValue("@dmg", damage);
        contribCmd.Parameters.AddWithValue("@now", Now());
        contribCmd.ExecuteNonQuery();

        // Update boss damage
        using var updateCmd = conn.CreateCommand();
        updateCmd.Transaction = tx;
        updateCmd.CommandText = "UPDATE raid_bosses SET damage_dealt = damage_dealt + @dmg WHERE week_id = @wid";
        updateCmd.Parameters.AddWithValue("@dmg", damage);
        updateCmd.Parameters.AddWithValue("@wid", weekId);
        updateCmd.ExecuteNonQuery();

        tx.Commit();

        // Get updated total
        using var totalCmd = conn.CreateCommand();
        totalCmd.CommandText = "SELECT damage_dealt FROM raid_bosses WHERE week_id = @wid";
        totalCmd.Parameters.AddWithValue("@wid", weekId);
        var total = (long)(totalCmd.ExecuteScalar() ?? 0);

        return Results.Ok(new { weekId, damageDone = total, contributed = damage });
    }

    public static async Task<IResult> RaidRewards(HttpRequest request)
    {
        var weekId = request.Query["weekId"].FirstOrDefault() ?? "";
        var profileId = request.Query["profileId"].FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(weekId) || string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing weekId or profileId" });

        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT damage_dealt FROM raid_bosses WHERE week_id = @wid";
        cmd.Parameters.AddWithValue("@wid", weekId);
        var damageDone = (long)(cmd.ExecuteScalar() ?? 0);

        return Results.Ok(new { weekId, damageDone, totalHealth = 10_000_000 });
    }
}
