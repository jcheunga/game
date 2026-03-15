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
        UpsertPlayer(conn, profileId, callsign, now);

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
        using var ins = conn.CreateCommand();
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

        InsertSeat(conn, roomId, profileId, callsign, ticketId, joinToken, "host seat", now);

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
            relayEndpoint = $"wss://relay.crownroad.invalid/{roomId}",
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
                relayEndpoint = $"wss://relay.crownroad.invalid/{roomId}",
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

        var seatCount = CountActiveSeats(conn, roomId);
        var ticketId = NewId("JOIN");
        var joinToken = $"join-{Guid.NewGuid():N}";
        var seatLabel = seatCount >= 4 ? "spectator" : "runner";

        InsertSeat(conn, roomId, profileId, callsign, ticketId, joinToken, seatLabel, now);
        TouchRoom(conn, roomId);

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
            relayEndpoint = $"wss://relay.crownroad.invalid/{roomId}",
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
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE rooms SET status = 'racing', race_started_at = $now, updated_at = $now WHERE room_id = $rid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$now", Now());
                cmd.ExecuteNonQuery();
                using var seats = conn.CreateCommand();
                seats.CommandText = "UPDATE room_seats SET race_status = 'racing' WHERE room_id = $rid AND status != 'left' AND seat_label != 'spectator'";
                seats.Parameters.AddWithValue("$rid", roomId);
                seats.ExecuteNonQuery();
                return Results.Ok(new { status = "ok", message = "Round launched." });
            }
            case "reset_round":
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE rooms SET status = 'lobby', updated_at = $now WHERE room_id = $rid";
                cmd.Parameters.AddWithValue("$rid", roomId);
                cmd.Parameters.AddWithValue("$now", Now());
                cmd.ExecuteNonQuery();
                using var seats = conn.CreateCommand();
                seats.CommandText = "UPDATE room_seats SET is_ready = 0, race_status = 'prep', score = 0 WHERE room_id = $rid AND status != 'left'";
                seats.Parameters.AddWithValue("$rid", roomId);
                seats.ExecuteNonQuery();
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
        var score = GetInt(body, "result.score", GetInt(body, "score", 0));
        var elapsed = GetDouble(body, "result.elapsedSeconds", GetDouble(body, "elapsedSeconds", 0));
        var hull = GetDouble(body, "result.hullRemaining", GetDouble(body, "hullRemaining", 0));
        var defeats = GetInt(body, "result.enemyDefeats", GetInt(body, "enemyDefeats", 0));

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

        using var conn = Database.Open();
        CleanupStaleRooms(conn);

        // Try to find an open room for this board
        using var find = conn.CreateCommand();
        find.CommandText = """
            SELECT room_id, title, board_code FROM rooms
            WHERE status = 'lobby' AND board_code = $code
            ORDER BY updated_at DESC LIMIT 1
        """;
        find.Parameters.AddWithValue("$code", boardCode);
        using var reader = find.ExecuteReader();

        string roomId, roomTitle;
        if (reader.Read())
        {
            roomId = reader.GetString(0);
            roomTitle = reader.GetString(1);
            reader.Close();
        }
        else
        {
            reader.Close();
            // Create a new room
            roomId = NewId("ROOM");
            roomTitle = $"Quick Match {boardCode}";
            using var ins = conn.CreateCommand();
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

        var ticketId = NewId("MATCH");
        var joinToken = $"match-{Guid.NewGuid():N}";
        InsertSeat(conn, roomId, profileId, callsign, ticketId, joinToken, "runner", now);
        TouchRoom(conn, roomId);

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
            relayEndpoint = $"wss://relay.crownroad.invalid/{roomId}",
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

    private static void InsertSeat(SqliteConnection conn, string roomId, string profileId, string callsign, string ticketId, string joinToken, string seatLabel, long now)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO room_seats (room_id, profile_id, callsign, ticket_id, join_token, seat_label, status, joined_at, expires_at)
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

    private static int CountActiveSeats(SqliteConnection conn, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM room_seats WHERE room_id = $rid AND status != 'left'";
        cmd.Parameters.AddWithValue("$rid", roomId);
        return (int)(long)(cmd.ExecuteScalar() ?? 0);
    }

    private static void TouchRoom(SqliteConnection conn, string roomId)
    {
        using var cmd = conn.CreateCommand();
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

    private static void UpsertPlayer(SqliteConnection conn, string profileId, string callsign, long now)
    {
        using var cmd = conn.CreateCommand();
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
}
