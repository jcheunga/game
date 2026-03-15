using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using CrownroadServer;

namespace CrownroadServer.Tests;

public static class ServerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> RunAll()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"crownroad_test_{Guid.NewGuid():N}.db");
        Database.Configure($"Data Source={dbPath}");
        Database.Initialize();

        var passed = 0;
        var failed = 0;

        try
        {
            using var host = await CreateTestHost();
            var client = host.GetTestClient();

            var tests = new (string Name, Func<Task> Test)[]
            {
                ("PlayerProfile", () => TestPlayerProfile(client)),
                ("ChallengeSync", () => TestChallengeSync(client)),
                ("ChallengeLeaderboard", () => TestChallengeLeaderboard(client)),
                ("ChallengeFeed", () => TestChallengeFeed(client)),
                ("RoomCreate", () => TestRoomCreate(client)),
                ("RoomJoin", () => TestRoomJoin(client)),
                ("RoomSession", () => TestRoomSession(client)),
                ("RoomAction_SetReady", () => TestRoomActionSetReady(client)),
                ("RoomAction_Launch", () => TestRoomActionLaunch(client)),
                ("RoomResult", () => TestRoomResult(client)),
                ("RoomScoreboard", () => TestRoomScoreboard(client)),
                ("RoomTelemetry", () => TestRoomTelemetry(client)),
                ("RoomAction_Leave", () => TestRoomActionLeave(client)),
                ("RoomReport", () => TestRoomReport(client)),
                ("RoomMatchmake", () => TestRoomMatchmake(client)),
                ("RoomSeatLease", () => TestRoomSeatLease(client)),
                ("RoomDirectory", () => TestRoomDirectory(client)),
                ("RoomAction_Reset", () => TestRoomActionReset(client)),
            };

            foreach (var (name, test) in tests)
            {
                try
                {
                    await test();
                    Console.WriteLine($"  PASS  {name}");
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  FAIL  {name}: {ex.Message}");
                    failed++;
                }
            }
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }

        Console.WriteLine($"\n{passed} passed, {failed} failed, {passed + failed} total");
        return failed > 0 ? 1 : 0;
    }

    private static async Task<IHost> CreateTestHost()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddRouting());
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/player-profile", Endpoints.PlayerProfile);
                        endpoints.MapPost("/challenge-sync", Endpoints.ChallengeSync);
                        endpoints.MapGet("/challenge-boards", Endpoints.ChallengeLeaderboard);
                        endpoints.MapGet("/challenge-feed", Endpoints.ChallengeFeed);
                        endpoints.MapGet("/challenge-rooms", Endpoints.RoomDirectory);
                        endpoints.MapPost("/challenge-room-create", Endpoints.RoomCreate);
                        endpoints.MapPost("/challenge-room-join", Endpoints.RoomJoin);
                        endpoints.MapGet("/challenge-room-session", Endpoints.RoomSession);
                        endpoints.MapPost("/challenge-room-action", Endpoints.RoomAction);
                        endpoints.MapPost("/challenge-room-result", Endpoints.RoomResult);
                        endpoints.MapGet("/challenge-room-scoreboard", Endpoints.RoomScoreboard);
                        endpoints.MapPost("/challenge-room-telemetry", Endpoints.RoomTelemetry);
                        endpoints.MapPost("/challenge-room-leave", Endpoints.RoomLeave);
                        endpoints.MapPost("/challenge-room-report", Endpoints.RoomReport);
                        endpoints.MapPost("/challenge-room-matchmake", Endpoints.RoomMatchmake);
                        endpoints.MapPost("/challenge-room-seat-lease", Endpoints.RoomSeatLease);
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // ── Shared state for chained tests ───────────────────────

    private static string _testRoomId = "";
    private static string _testTicketId = "";

    // ── Tests ────────────────────────────────────────────────

    private static async Task TestPlayerProfile(HttpClient client)
    {
        var body = new { profile = new { playerProfileId = "TEST-01", playerCallsign = "TestRunner" } };
        var resp = await Post(client, "/player-profile", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("authState").GetString() == "verified", "expected verified auth");
        Assert(resp.RootElement.GetProperty("playerProfileId").GetString() == "TEST-01", "wrong profile id");
    }

    private static async Task TestChallengeSync(HttpClient client)
    {
        var body = new
        {
            batch = new
            {
                submissions = new[]
                {
                    new { boardCode = "TEST-BOARD-01", score = 1200, playerWon = true, starsEarned = 2, elapsedSeconds = 45.5, hullRemaining = 120.0, enemyDefeats = 18 },
                    new { boardCode = "TEST-BOARD-01", score = 950, playerWon = false, starsEarned = 1, elapsedSeconds = 38.2, hullRemaining = 0.0, enemyDefeats = 12 }
                }
            }
        };
        var resp = await Post(client, "/challenge-sync", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("accepted").GetInt32() == 2, "expected 2 accepted");
    }

    private static async Task TestChallengeLeaderboard(HttpClient client)
    {
        var resp = await Get(client, "/challenge-boards?code=TEST-BOARD-01");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() >= 1, "expected at least 1 leaderboard entry");
        Assert(entries[0].GetProperty("score").GetInt32() == 1200, "expected best score 1200");
    }

    private static async Task TestChallengeFeed(HttpClient client)
    {
        var resp = await Get(client, "/challenge-feed");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() >= 1, "expected seeded feed entries");
    }

    private static async Task TestRoomCreate(HttpClient client)
    {
        var body = new
        {
            room = new
            {
                boardCode = "TEST-BOARD-01",
                boardTitle = "Test Board",
                playerProfileId = "TEST-01",
                playerCallsign = "TestHost",
                region = "test",
                usesLockedDeck = false
            }
        };
        var resp = await Post(client, "/challenge-room-create", body, "TEST-01");
        _testRoomId = resp.RootElement.GetProperty("roomId").GetString()!;
        Assert(!string.IsNullOrWhiteSpace(_testRoomId), "missing room id");
        Assert(resp.RootElement.GetProperty("hostTicket").GetProperty("status").GetString() == "hosted", "expected hosted ticket");
    }

    private static async Task TestRoomJoin(HttpClient client)
    {
        var body = new { join = new { roomId = _testRoomId, playerProfileId = "TEST-02", playerCallsign = "TestJoiner" } };
        var resp = await Post(client, "/challenge-room-join", body, "TEST-02");
        _testTicketId = resp.RootElement.GetProperty("ticketId").GetString()!;
        Assert(resp.RootElement.GetProperty("status").GetString() == "joined", "expected joined");
        Assert(resp.RootElement.GetProperty("seatLabel").GetString() == "runner", "expected runner seat");
    }

    private static async Task TestRoomSession(HttpClient client)
    {
        var resp = await Get(client, $"/challenge-room-session?roomId={_testRoomId}");
        Assert(resp.RootElement.GetProperty("hasRoom").GetBoolean(), "expected hasRoom true");
        var peers = resp.RootElement.GetProperty("peers");
        Assert(peers.GetArrayLength() == 2, $"expected 2 peers, got {peers.GetArrayLength()}");
    }

    private static async Task TestRoomActionSetReady(HttpClient client)
    {
        var body = new { action = new { roomId = _testRoomId, actionId = "set_ready", playerProfileId = "TEST-02" } };
        var resp = await Post(client, "/challenge-room-action", body, "TEST-02");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");
    }

    private static async Task TestRoomActionLaunch(HttpClient client)
    {
        var body = new { action = new { roomId = _testRoomId, actionId = "launch_round", playerProfileId = "TEST-01" } };
        var resp = await Post(client, "/challenge-room-action", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");

        var session = await Get(client, $"/challenge-room-session?roomId={_testRoomId}");
        Assert(session.RootElement.GetProperty("status").GetString() == "racing", "expected racing status");
    }

    private static async Task TestRoomResult(HttpClient client)
    {
        var body = new { result = new { roomId = _testRoomId, playerProfileId = "TEST-02", score = 850, elapsedSeconds = 42.1, hullRemaining = 85.0, enemyDefeats = 14 } };
        var resp = await Post(client, "/challenge-room-result", body, "TEST-02");
        Assert(resp.RootElement.GetProperty("status").GetString() == "accepted", "expected accepted");
        Assert(resp.RootElement.GetProperty("provisionalRank").GetInt32() >= 1, "expected valid rank");
    }

    private static async Task TestRoomScoreboard(HttpClient client)
    {
        var resp = await Get(client, $"/challenge-room-scoreboard?roomId={_testRoomId}");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() >= 1, "expected scoreboard entries");
    }

    private static async Task TestRoomTelemetry(HttpClient client)
    {
        var body = new { telemetry = new { roomId = _testRoomId, playerProfileId = "TEST-01", elapsedSeconds = 20.5, hullRatio = 0.85, enemyDefeats = 8, raceStatus = "racing" } };
        var resp = await Post(client, "/challenge-room-telemetry", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");
    }

    private static async Task TestRoomActionLeave(HttpClient client)
    {
        var body = new { action = new { roomId = _testRoomId, actionId = "leave_room", playerProfileId = "TEST-02" } };
        var resp = await Post(client, "/challenge-room-action", body, "TEST-02");
        Assert(resp.RootElement.GetProperty("status").GetString() == "accepted", "expected accepted");
    }

    private static async Task TestRoomReport(HttpClient client)
    {
        var body = new { report = new { roomId = _testRoomId, reporterProfileId = "TEST-01", targetProfileId = "TEST-02", reason = "suspicious_score", details = "test report" } };
        var resp = await Post(client, "/challenge-room-report", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");
    }

    private static async Task TestRoomMatchmake(HttpClient client)
    {
        var body = new { matchmake = new { playerProfileId = "TEST-03", playerCallsign = "Matcher", boardCode = "MATCH-BOARD-01" } };
        var resp = await Post(client, "/challenge-room-matchmake", body, "TEST-03");
        Assert(resp.RootElement.GetProperty("status").GetString() == "matched", "expected matched");
        Assert(!string.IsNullOrWhiteSpace(resp.RootElement.GetProperty("roomId").GetString()), "expected room id");
    }

    private static async Task TestRoomSeatLease(HttpClient client)
    {
        var body = new { lease = new { roomId = _testRoomId, playerProfileId = "TEST-01", ticketId = "HOST-TEST" } };
        var resp = await Post(client, "/challenge-room-seat-lease", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "renewed", "expected renewed");
    }

    private static async Task TestRoomDirectory(HttpClient client)
    {
        var resp = await Get(client, "/challenge-rooms");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() >= 1, "expected at least 1 room in directory");
    }

    private static async Task TestRoomActionReset(HttpClient client)
    {
        var body = new { action = new { roomId = _testRoomId, actionId = "reset_round", playerProfileId = "TEST-01" } };
        var resp = await Post(client, "/challenge-room-action", body, "TEST-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");

        var session = await Get(client, $"/challenge-room-session?roomId={_testRoomId}");
        Assert(session.RootElement.GetProperty("status").GetString() == "lobby", "expected lobby after reset");
    }

    // ── HTTP Helpers ─────────────────────────────────────────

    private static async Task<JsonDocument> Post(HttpClient client, string url, object body, string profileId)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert(resp.StatusCode == HttpStatusCode.OK, $"HTTP {(int)resp.StatusCode} at {url}");
        var text = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    private static async Task<JsonDocument> Get(HttpClient client, string url)
    {
        var resp = await client.GetAsync(url);
        Assert(resp.StatusCode == HttpStatusCode.OK, $"HTTP {(int)resp.StatusCode} at {url}");
        var text = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }
}
