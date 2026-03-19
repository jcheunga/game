using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                ("WebSocketRelay", () => TestWebSocketRelay(host)),
                ("AchievementSync", () => TestAchievementSync(client)),
                ("AchievementList", () => TestAchievementList(client)),
                ("DailyComplete", () => TestDailyComplete(client)),
                ("DailyLeaderboard", () => TestDailyLeaderboard(client)),
                ("PurchaseValidate", () => TestPurchaseValidate(client)),
                ("PurchaseHistory", () => TestPurchaseHistory(client)),
                ("PurchaseProducts", () => TestPurchaseProducts(client)),
                ("PurchaseDuplicate", () => TestPurchaseDuplicate(client)),
                ("PurchaseStarterOneTime", () => TestPurchaseStarterOneTime(client)),
                ("StripeCheckoutNoKey", () => TestStripeCheckoutNoKey(client)),
                ("StripeStatusNoKey", () => TestStripeStatusNoKey(client)),
                ("AnalyticsIngest", () => TestAnalyticsIngest(client)),
                ("AnalyticsSummary", () => TestAnalyticsSummary(client)),
                ("CloudSaveUpload", () => TestCloudSaveUpload(client)),
                ("CloudSaveDownload", () => TestCloudSaveDownload(client)),
                ("CloudSaveInfo", () => TestCloudSaveInfo(client)),

                // ── Validation & edge case tests ──
                ("PlayerProfile_EmptyId", () => TestPlayerProfile_EmptyId(client)),
                ("ChallengeSync_EmptyBatch", () => TestChallengeSync_EmptyBatch(client)),
                ("ChallengeSync_BoundsRejection", () => TestChallengeSync_BoundsRejection(client)),
                ("ChallengeLeaderboard_MissingCode", () => TestChallengeLeaderboard_MissingCode(client)),
                ("ChallengeLeaderboard_EmptyCode", () => TestChallengeLeaderboard_EmptyCode(client)),
                ("ChallengeLeaderboard_NonexistentCode", () => TestChallengeLeaderboard_NonexistentCode(client)),
                ("RoomJoin_MissingRoomId", () => TestRoomJoin_MissingRoomId(client)),
                ("RoomJoin_NonexistentRoom", () => TestRoomJoin_NonexistentRoom(client)),
                ("RoomJoin_Duplicate", () => TestRoomJoin_Duplicate(client)),
                ("RoomSession_MissingRoomId", () => TestRoomSession_MissingRoomId(client)),
                ("RoomAction_UnknownAction", () => TestRoomAction_UnknownAction(client)),
                ("RoomScoreboard_MissingRoomId", () => TestRoomScoreboard_MissingRoomId(client)),
                ("PurchaseValidate_MissingFields", () => TestPurchaseValidate_MissingFields(client)),
                ("PurchaseHistory_EmptyProfile", () => TestPurchaseHistory_EmptyProfile(client)),
                ("CloudSave_MissingFields", () => TestCloudSave_MissingFields(client)),
                ("CloudSave_OversizedData", () => TestCloudSave_OversizedData(client)),
                ("CloudSaveInfo_NonexistentProfile", () => TestCloudSaveInfo_NonexistentProfile(client)),
                ("AnalyticsIngest_EmptyBatch", () => TestAnalyticsIngest_EmptyBatch(client)),
                ("AnalyticsIngest_OversizedBatch", () => TestAnalyticsIngest_OversizedBatch(client)),
                ("AnalyticsIngest_SkipsEmptyType", () => TestAnalyticsIngest_SkipsEmptyType(client)),
                ("DailyComplete_MissingDate", () => TestDailyComplete_MissingDate(client)),
                ("DailyComplete_InvalidScore", () => TestDailyComplete_InvalidScore(client)),
                ("DailyLeaderboard_EmptyDate", () => TestDailyLeaderboard_EmptyDate(client)),
                ("AchievementSync_EmptyIds", () => TestAchievementSync_EmptyIds(client)),
                ("AchievementList_NonexistentProfile", () => TestAchievementList_NonexistentProfile(client)),
                ("HealthEndpoint", () => TestHealthEndpoint(client)),
                ("RoomResult_MissingRoomId", () => TestRoomResult_MissingRoomId(client)),
                ("RoomTelemetry_MissingRoomId", () => TestRoomTelemetry_MissingRoomId(client)),
                ("RoomLeave_MissingRoomId", () => TestRoomLeave_MissingRoomId(client)),
                ("RoomReport_MissingFields", () => TestRoomReport_MissingFields(client)),
                ("RoomMatchmake_MissingFields", () => TestRoomMatchmake_MissingFields(client)),
                ("RoomSeatLease_MissingFields", () => TestRoomSeatLease_MissingFields(client)),
                ("CrashReport", () => TestCrashReport(client)),
                ("CrashReport_MissingDetails", () => TestCrashReport_MissingDetails(client)),
                ("DatabaseBackup", () => TestDatabaseBackup(client)),
                ("SchemaVersion", () => TestSchemaVersion()),
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
                    app.UseWebSockets();
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path.StartsWithSegments("/ws/relay") && context.WebSockets.IsWebSocketRequest)
                        {
                            var roomId = context.Request.Path.Value?.Split('/').LastOrDefault() ?? "";
                            await RelayHub.HandleConnection(context, roomId);
                            return;
                        }
                        await next();
                    });
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
                    endpoints.MapPost("/achievements/sync", Endpoints.AchievementSync);
                    endpoints.MapGet("/achievements/{profileId}", (HttpRequest request, string profileId) => Endpoints.AchievementList(request, profileId));
                    endpoints.MapPost("/daily/complete", Endpoints.DailyComplete);
                    endpoints.MapGet("/daily/leaderboard/{date}", (HttpRequest request, string date) => Endpoints.DailyLeaderboard(request, date));
                    endpoints.MapPost("/purchase/validate", Endpoints.PurchaseValidate);
                    endpoints.MapGet("/purchase/history", Endpoints.PurchaseHistory);
                    endpoints.MapGet("/purchase/products", Endpoints.PurchaseProducts);
                    endpoints.MapPost("/purchase/stripe-checkout", Endpoints.StripeCreateCheckout);
                    endpoints.MapPost("/purchase/stripe-webhook", Endpoints.StripeWebhook);
                    endpoints.MapGet("/purchase/stripe-status", Endpoints.StripeCheckoutStatus);
                    endpoints.MapPost("/analytics/ingest", Endpoints.AnalyticsIngest);
                    endpoints.MapGet("/analytics/summary", Endpoints.AnalyticsSummary);
                    endpoints.MapPost("/crash-report", Endpoints.CrashReport);
                    endpoints.MapPost("/admin/backup", () => Results.Ok(new { status = "ok", backupPath = "test" }));
                    endpoints.MapGet("/health", async context =>
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"status\":\"healthy\"}");
                    });
                    endpoints.MapPost("/cloud-save/upload", Endpoints.CloudSaveUpload);
                    endpoints.MapGet("/cloud-save/download", Endpoints.CloudSaveDownload);
                    endpoints.MapGet("/cloud-save/info", Endpoints.CloudSaveInfo);
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

    private static async Task TestWebSocketRelay(IHost host)
    {
        var server = host.GetTestServer();
        var wsClient1 = server.CreateWebSocketClient();
        var wsClient2 = server.CreateWebSocketClient();

        wsClient1.ConfigureRequest = req => req.Headers["X-Convoy-Profile"] = "WS-PEER-1";
        wsClient2.ConfigureRequest = req => req.Headers["X-Convoy-Profile"] = "WS-PEER-2";

        var ws1 = await wsClient1.ConnectAsync(new Uri(server.BaseAddress, "/ws/relay/TEST-RELAY-ROOM"), CancellationToken.None);
        Assert(ws1.State == System.Net.WebSockets.WebSocketState.Open, "ws1 should be open");

        // Small delay for join broadcast
        await Task.Delay(50);

        var ws2 = await wsClient2.ConnectAsync(new Uri(server.BaseAddress, "/ws/relay/TEST-RELAY-ROOM"), CancellationToken.None);
        Assert(ws2.State == System.Net.WebSockets.WebSocketState.Open, "ws2 should be open");

        // Read peer_joined message on ws1 (broadcast when ws2 joins)
        var joinMsg = await ReceiveWsMessage(ws1, 2000);
        Assert(joinMsg.Contains("peer_joined"), $"expected peer_joined, got: {joinMsg}");

        // ws2 also gets a peer_joined broadcast for itself - drain it
        var ws2JoinMsg = await ReceiveWsMessage(ws2, 2000);
        Assert(ws2JoinMsg.Contains("peer_joined"), $"expected ws2 peer_joined, got: {ws2JoinMsg}");

        // Send from ws1, receive on ws2
        var testPayload = "{\"type\":\"telemetry\",\"elapsed\":10.5,\"hull\":0.9}";
        await SendWsMessage(ws1, testPayload);
        var received = await ReceiveWsMessage(ws2, 2000);
        Assert(received.Contains("telemetry"), $"expected relayed telemetry, got: {received}");
        Assert(received.Contains("10.5"), "expected elapsed value in relay");

        // Verify peer count
        Assert(RelayHub.GetPeerCount("TEST-RELAY-ROOM") == 2, "expected 2 peers in relay room");

        await ws1.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        await Task.Delay(50);
        Assert(RelayHub.GetPeerCount("TEST-RELAY-ROOM") == 1, "expected 1 peer after ws1 close");

        await ws2.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        await Task.Delay(50);
        Assert(RelayHub.GetPeerCount("TEST-RELAY-ROOM") == 0, "expected 0 peers after both close");
    }

    private static async Task TestAchievementSync(HttpClient client)
    {
        var body1 = new { profileId = "TEST-ACH-01", achievementIds = new[] { "ach_first_win", "ach_no_damage", "ach_speed_run" } };
        var resp1 = await Post(client, "/achievements/sync", body1, "TEST-ACH-01");
        Assert(resp1.RootElement.GetProperty("synced").GetInt32() == 3, "expected 3 synced on first call");
        Assert(resp1.RootElement.GetProperty("total").GetInt32() == 3, "expected 3 total on first call");

        var body2 = new { profileId = "TEST-ACH-01", achievementIds = new[] { "ach_first_win", "ach_no_damage", "ach_speed_run", "ach_perfectionist" } };
        var resp2 = await Post(client, "/achievements/sync", body2, "TEST-ACH-01");
        Assert(resp2.RootElement.GetProperty("synced").GetInt32() == 1, "expected 1 synced on second call");
        Assert(resp2.RootElement.GetProperty("total").GetInt32() == 4, "expected 4 total on second call");
    }

    private static async Task TestAchievementList(HttpClient client)
    {
        // Sync achievements first
        var syncBody = new { profileId = "TEST-ACH-02", achievementIds = new[] { "ach_explorer", "ach_collector" } };
        await Post(client, "/achievements/sync", syncBody, "TEST-ACH-02");

        var resp = await Get(client, "/achievements/TEST-ACH-02");
        var achievements = resp.RootElement.GetProperty("achievements");
        Assert(achievements.GetArrayLength() == 2, $"expected 2 achievements, got {achievements.GetArrayLength()}");

        var ids = new List<string>();
        foreach (var entry in achievements.EnumerateArray())
        {
            ids.Add(entry.GetProperty("achievementId").GetString()!);
        }
        Assert(ids.Contains("ach_explorer"), "expected ach_explorer in list");
        Assert(ids.Contains("ach_collector"), "expected ach_collector in list");
    }

    private static async Task TestDailyComplete(HttpClient client)
    {
        var date = "2026-03-16";

        // First submission — should be new best
        var body1 = new { profileId = "TEST-DAILY-01", date, score = 500 };
        var resp1 = await Post(client, "/daily/complete", body1, "TEST-DAILY-01");
        Assert(resp1.RootElement.GetProperty("isNewBest").GetBoolean(), "expected isNewBest=true on first submit");
        Assert(resp1.RootElement.GetProperty("personalBest").GetInt32() == 500, "expected personalBest=500");

        // Lower score — not a new best
        var body2 = new { profileId = "TEST-DAILY-01", date, score = 300 };
        var resp2 = await Post(client, "/daily/complete", body2, "TEST-DAILY-01");
        Assert(!resp2.RootElement.GetProperty("isNewBest").GetBoolean(), "expected isNewBest=false on lower score");
        Assert(resp2.RootElement.GetProperty("personalBest").GetInt32() == 500, "expected personalBest still 500");

        // Higher score — new best
        var body3 = new { profileId = "TEST-DAILY-01", date, score = 900 };
        var resp3 = await Post(client, "/daily/complete", body3, "TEST-DAILY-01");
        Assert(resp3.RootElement.GetProperty("isNewBest").GetBoolean(), "expected isNewBest=true on higher score");
        Assert(resp3.RootElement.GetProperty("personalBest").GetInt32() == 900, "expected personalBest=900");
    }

    private static async Task TestDailyLeaderboard(HttpClient client)
    {
        var date = "2026-03-16-LB";

        // Submit 3 completions from different profiles
        await Post(client, "/daily/complete", new { profileId = "TEST-LB-01", date, score = 700 }, "TEST-LB-01");
        await Post(client, "/daily/complete", new { profileId = "TEST-LB-02", date, score = 1200 }, "TEST-LB-02");
        await Post(client, "/daily/complete", new { profileId = "TEST-LB-03", date, score = 950 }, "TEST-LB-03");

        var resp = await Get(client, $"/daily/leaderboard/{date}");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() == 3, $"expected 3 leaderboard entries, got {entries.GetArrayLength()}");

        // Verify descending score order
        Assert(entries[0].GetProperty("score").GetInt32() == 1200, "expected rank 1 score=1200");
        Assert(entries[1].GetProperty("score").GetInt32() == 950, "expected rank 2 score=950");
        Assert(entries[2].GetProperty("score").GetInt32() == 700, "expected rank 3 score=700");

        // Verify profile IDs match
        Assert(entries[0].GetProperty("profileId").GetString() == "TEST-LB-02", "expected rank 1 profile TEST-LB-02");
        Assert(entries[1].GetProperty("profileId").GetString() == "TEST-LB-03", "expected rank 2 profile TEST-LB-03");
        Assert(entries[2].GetProperty("profileId").GetString() == "TEST-LB-01", "expected rank 3 profile TEST-LB-01");
    }

    private static async Task TestPurchaseValidate(HttpClient client)
    {
        var body = new
        {
            profileId = "TEST-PUR-01",
            productId = "gold_pouch",
            platform = "stripe",
            receiptToken = "receipt-test-001",
            transactionId = "txn-test-001"
        };
        var resp = await Post(client, "/purchase/validate", body, "TEST-PUR-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected status ok");
        Assert(resp.RootElement.GetProperty("goldCredited").GetInt32() == 500, "expected 500 gold");
        Assert(resp.RootElement.GetProperty("foodCredited").GetInt32() == 0, "expected 0 food");
        Assert(!string.IsNullOrEmpty(resp.RootElement.GetProperty("purchaseId").GetString()), "expected purchaseId");
    }

    private static async Task TestPurchaseHistory(HttpClient client)
    {
        // Submit a purchase first
        await Post(client, "/purchase/validate", new
        {
            profileId = "TEST-PUR-HIST",
            productId = "food_rations",
            platform = "apple",
            receiptToken = "receipt-hist-001",
            transactionId = "txn-hist-001"
        }, "TEST-PUR-HIST");

        var resp = await Get(client, "/purchase/history?profileId=TEST-PUR-HIST");
        var purchases = resp.RootElement.GetProperty("purchases");
        Assert(purchases.GetArrayLength() >= 1, $"expected at least 1 purchase, got {purchases.GetArrayLength()}");
        Assert(purchases[0].GetProperty("productId").GetString() == "food_rations", "expected food_rations product");
        Assert(purchases[0].GetProperty("foodCredited").GetInt32() == 20, "expected 20 food credited");
    }

    private static async Task TestPurchaseProducts(HttpClient client)
    {
        var resp = await Get(client, "/purchase/products");
        var products = resp.RootElement.GetProperty("products");
        Assert(products.GetArrayLength() == 10, $"expected 10 products, got {products.GetArrayLength()}");
    }

    private static async Task TestPurchaseDuplicate(HttpClient client)
    {
        // First purchase should succeed
        var body = new
        {
            profileId = "TEST-PUR-DUP",
            productId = "gold_chest",
            platform = "stripe",
            receiptToken = "receipt-dup-001",
            transactionId = "txn-dup-001"
        };
        await Post(client, "/purchase/validate", body, "TEST-PUR-DUP");

        // Duplicate transaction should be rejected with 409
        var json = System.Text.Json.JsonSerializer.Serialize(body, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/purchase/validate");
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", "TEST-PUR-DUP");
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert(resp.StatusCode == HttpStatusCode.Conflict, $"expected 409, got {(int)resp.StatusCode}");
    }

    private static async Task TestPurchaseStarterOneTime(HttpClient client)
    {
        // First starter kit purchase should succeed
        var body = new
        {
            profileId = "TEST-PUR-STARTER",
            productId = "starter_kit",
            platform = "stripe",
            receiptToken = "receipt-starter-001",
            transactionId = "txn-starter-001"
        };
        await Post(client, "/purchase/validate", body, "TEST-PUR-STARTER");

        // Second starter kit purchase should be rejected with 409
        var body2 = new
        {
            profileId = "TEST-PUR-STARTER",
            productId = "starter_kit",
            platform = "stripe",
            receiptToken = "receipt-starter-002",
            transactionId = "txn-starter-002"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(body2, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/purchase/validate");
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", "TEST-PUR-STARTER");
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert(resp.StatusCode == HttpStatusCode.Conflict, $"expected 409 for second starter, got {(int)resp.StatusCode}");
    }

    private static async Task TestAnalyticsIngest(HttpClient client)
    {
        var body = new
        {
            profileId = "TEST-ANALYTICS-01",
            clientVersion = 31,
            platform = "desktop",
            events = new[]
            {
                new { type = "session_start", data = "stage=5,gold=300" },
                new { type = "stage_end", data = "stage=5,won=true,stars=2" },
                new { type = "unit_purchase", data = "unit=player_rogue,cost=340" }
            }
        };
        var resp = await Post(client, "/analytics/ingest", body, "TEST-ANALYTICS-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");
        Assert(resp.RootElement.GetProperty("accepted").GetInt32() == 3, "expected 3 accepted");
    }

    private static async Task TestAnalyticsSummary(HttpClient client)
    {
        // Ingest some events first
        await Post(client, "/analytics/ingest", new
        {
            profileId = "TEST-ANALYTICS-SUM",
            clientVersion = 31,
            platform = "desktop",
            events = new[]
            {
                new { type = "stage_end", data = "stage=1,won=true" },
                new { type = "stage_end", data = "stage=1,won=false" },
                new { type = "stage_end", data = "stage=2,won=true" }
            }
        }, "TEST-ANALYTICS-SUM");

        var resp = await Get(client, "/analytics/summary?type=stage_end");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected ok");
        var rows = resp.RootElement.GetProperty("rows");
        Assert(rows.GetArrayLength() >= 2, $"expected at least 2 rows, got {rows.GetArrayLength()}");
    }

    private static async Task TestCloudSaveUpload(HttpClient client)
    {
        var saveJson = "{\"Version\":31,\"Gold\":500,\"Food\":20}";
        var body = new { profileId = "TEST-CLOUD-01", saveData = saveJson, saveVersion = 31 };
        var resp = await Post(client, "/cloud-save/upload", body, "TEST-CLOUD-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected upload ok");
        Assert(resp.RootElement.GetProperty("saveVersion").GetInt32() == 31, "expected version 31");
        Assert(!string.IsNullOrEmpty(resp.RootElement.GetProperty("saveHash").GetString()), "expected saveHash");
    }

    private static async Task TestCloudSaveDownload(HttpClient client)
    {
        // Upload first
        var saveJson = "{\"Version\":31,\"Gold\":999,\"Food\":50}";
        await Post(client, "/cloud-save/upload", new { profileId = "TEST-CLOUD-DL", saveData = saveJson, saveVersion = 31 }, "TEST-CLOUD-DL");

        // Download
        var resp = await Get(client, "/cloud-save/download?profileId=TEST-CLOUD-DL");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected download ok");
        Assert(resp.RootElement.GetProperty("saveData").GetString()!.Contains("999"), "expected save data with 999 gold");
        Assert(resp.RootElement.GetProperty("saveVersion").GetInt32() == 31, "expected version 31");

        // Download non-existent
        var resp2 = await Get(client, "/cloud-save/download?profileId=TEST-CLOUD-MISSING");
        Assert(resp2.RootElement.GetProperty("status").GetString() == "empty", "expected empty for missing profile");
    }

    private static async Task TestCloudSaveInfo(HttpClient client)
    {
        // Upload first
        var saveJson = "{\"Version\":31,\"Gold\":123}";
        await Post(client, "/cloud-save/upload", new { profileId = "TEST-CLOUD-INFO", saveData = saveJson, saveVersion = 31 }, "TEST-CLOUD-INFO");

        var resp = await Get(client, "/cloud-save/info?profileId=TEST-CLOUD-INFO");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected info ok");
        Assert(resp.RootElement.GetProperty("saveVersion").GetInt32() == 31, "expected version 31");
        Assert(resp.RootElement.GetProperty("sizeBytes").GetInt64() > 0, "expected non-zero size");
    }

    private static async Task TestStripeCheckoutNoKey(HttpClient client)
    {
        var body = new
        {
            profileId = "TEST-STRIPE-01",
            productId = "gold_pouch"
        };
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/purchase/stripe-checkout");
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", "TEST-STRIPE-01");
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert(resp.StatusCode == HttpStatusCode.ServiceUnavailable, $"expected 503 without Stripe key, got {(int)resp.StatusCode}");
    }

    private static async Task TestStripeStatusNoKey(HttpClient client)
    {
        var resp = await client.GetAsync("/purchase/stripe-status?sessionId=cs_test_fake");
        Assert(resp.StatusCode == HttpStatusCode.ServiceUnavailable, $"expected 503 without Stripe key, got {(int)resp.StatusCode}");
    }

    private static async Task SendWsMessage(System.Net.WebSockets.WebSocket ws, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveWsMessage(System.Net.WebSockets.WebSocket ws, int timeoutMs)
    {
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(timeoutMs);
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static async Task TestCrashReport(HttpClient client)
    {
        var body = new
        {
            profileId = "TEST-CRASH-01",
            errorType = "NullReferenceException",
            errorMessage = "Object reference not set",
            stackTrace = "at Unit.TakeDamage() line 42",
            clientVersion = 31,
            platform = "desktop",
            scene = "res://scenes/Battle.tscn"
        };
        var resp = await Post(client, "/crash-report", body, "TEST-CRASH-01");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected crash report ok");
    }

    private static async Task TestCrashReport_MissingDetails(HttpClient client)
    {
        await PostExpect(client, "/crash-report", new { profileId = "CR-EMPTY", errorType = "", errorMessage = "" }, "CR-EMPTY", HttpStatusCode.BadRequest);
    }

    private static async Task TestDatabaseBackup(HttpClient client)
    {
        var resp = await Post(client, "/admin/backup", new { }, "ADMIN");
        Assert(resp.RootElement.GetProperty("status").GetString() == "ok", "expected backup ok");
    }

    private static async Task TestSchemaVersion()
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM schema_version";
        var version = (long)(cmd.ExecuteScalar() ?? 0);
        Assert(version >= 2, $"expected schema version >= 2, got {version}");
        await Task.CompletedTask;
    }

    // ── Validation & Edge Case Tests ────────────────────────

    private static async Task TestPlayerProfile_EmptyId(HttpClient client)
    {
        var body = new { profile = new { playerProfileId = "", playerCallsign = "Ghost" } };
        await PostExpect(client, "/player-profile", body, "", HttpStatusCode.BadRequest);
    }

    private static async Task TestChallengeSync_EmptyBatch(HttpClient client)
    {
        var body = new { batch = new { submissions = Array.Empty<object>() } };
        var resp = await Post(client, "/challenge-sync", body, "TEST-EMPTY-BATCH");
        Assert(resp.RootElement.GetProperty("accepted").GetInt32() == 0, "expected 0 accepted");
    }

    private static async Task TestChallengeSync_BoundsRejection(HttpClient client)
    {
        var body = new
        {
            batch = new
            {
                submissions = new[]
                {
                    new { boardCode = "BOUNDS-01", score = 9999999, playerWon = true, starsEarned = 2, elapsedSeconds = 45.0, hullRemaining = 100.0, enemyDefeats = 10 },
                    new { boardCode = "BOUNDS-02", score = 500, playerWon = true, starsEarned = 1, elapsedSeconds = 99999.0, hullRemaining = 50.0, enemyDefeats = 5 },
                    new { boardCode = "BOUNDS-03", score = 500, playerWon = true, starsEarned = 1, elapsedSeconds = 30.0, hullRemaining = 50.0, enemyDefeats = 99999 },
                    new { boardCode = "", score = 500, playerWon = true, starsEarned = 1, elapsedSeconds = 30.0, hullRemaining = 50.0, enemyDefeats = 5 },
                    new { boardCode = "BOUNDS-OK", score = 100, playerWon = true, starsEarned = 3, elapsedSeconds = 20.0, hullRemaining = 200.0, enemyDefeats = 8 }
                }
            }
        };
        var resp = await Post(client, "/challenge-sync", body, "TEST-BOUNDS");
        var accepted = resp.RootElement.GetProperty("accepted").GetInt32();
        var rejected = resp.RootElement.GetProperty("rejected").GetInt32();
        Assert(rejected >= 3, $"expected at least 3 rejected, got {rejected}");
        Assert(accepted >= 1, $"expected at least 1 accepted, got {accepted}");
    }

    private static async Task TestChallengeLeaderboard_MissingCode(HttpClient client)
    {
        await GetExpect(client, "/challenge-boards", HttpStatusCode.BadRequest);
    }

    private static async Task TestChallengeLeaderboard_EmptyCode(HttpClient client)
    {
        await GetExpect(client, "/challenge-boards?code=", HttpStatusCode.BadRequest);
    }

    private static async Task TestChallengeLeaderboard_NonexistentCode(HttpClient client)
    {
        var resp = await Get(client, "/challenge-boards?code=DOES-NOT-EXIST");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() == 0, "expected 0 entries for nonexistent code");
    }

    private static async Task TestRoomJoin_MissingRoomId(HttpClient client)
    {
        var body = new { join = new { roomId = "", playerProfileId = "TEST-JOIN-EMPTY", playerCallsign = "Ghost" } };
        await PostExpect(client, "/challenge-room-join", body, "TEST-JOIN-EMPTY", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomJoin_NonexistentRoom(HttpClient client)
    {
        var body = new { join = new { roomId = "ROOM-DOES-NOT-EXIST", playerProfileId = "TEST-JOIN-NX", playerCallsign = "Ghost" } };
        await PostExpect(client, "/challenge-room-join", body, "TEST-JOIN-NX", HttpStatusCode.NotFound);
    }

    private static async Task TestRoomJoin_Duplicate(HttpClient client)
    {
        // Create a room
        var createBody = new { room = new { boardCode = "DUP-BOARD", boardTitle = "Dup Test", playerProfileId = "TEST-DUP-HOST", playerCallsign = "DupHost", region = "test", usesLockedDeck = false } };
        var createResp = await Post(client, "/challenge-room-create", createBody, "TEST-DUP-HOST");
        var dupRoomId = createResp.RootElement.GetProperty("roomId").GetString()!;

        // Join as runner
        var joinBody = new { join = new { roomId = dupRoomId, playerProfileId = "TEST-DUP-JOIN", playerCallsign = "DupJoin" } };
        await Post(client, "/challenge-room-join", joinBody, "TEST-DUP-JOIN");

        // Try to join again — should get 409
        await PostExpect(client, "/challenge-room-join", joinBody, "TEST-DUP-JOIN", HttpStatusCode.Conflict);
    }

    private static async Task TestRoomSession_MissingRoomId(HttpClient client)
    {
        await GetExpect(client, "/challenge-room-session", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomAction_UnknownAction(HttpClient client)
    {
        var body = new { action = new { roomId = _testRoomId, actionId = "explode_everything", playerProfileId = "TEST-01" } };
        await PostExpect(client, "/challenge-room-action", body, "TEST-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomScoreboard_MissingRoomId(HttpClient client)
    {
        await GetExpect(client, "/challenge-room-scoreboard", HttpStatusCode.BadRequest);
    }

    private static async Task TestPurchaseValidate_MissingFields(HttpClient client)
    {
        // Missing productId
        await PostExpect(client, "/purchase/validate", new { profileId = "V-01", productId = "", platform = "stripe", receiptToken = "r" }, "V-01", HttpStatusCode.BadRequest);
        // Missing platform
        await PostExpect(client, "/purchase/validate", new { profileId = "V-01", productId = "gold_pouch", platform = "", receiptToken = "r" }, "V-01", HttpStatusCode.BadRequest);
        // Missing receipt
        await PostExpect(client, "/purchase/validate", new { profileId = "V-01", productId = "gold_pouch", platform = "stripe", receiptToken = "" }, "V-01", HttpStatusCode.BadRequest);
        // Unknown product
        await PostExpect(client, "/purchase/validate", new { profileId = "V-01", productId = "fake_product", platform = "stripe", receiptToken = "r" }, "V-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestPurchaseHistory_EmptyProfile(HttpClient client)
    {
        var resp = await Get(client, "/purchase/history?profileId=NEVER-PURCHASED");
        var purchases = resp.RootElement.GetProperty("purchases");
        Assert(purchases.GetArrayLength() == 0, "expected 0 purchases for new profile");
    }

    private static async Task TestCloudSave_MissingFields(HttpClient client)
    {
        // Missing profileId
        await PostExpect(client, "/cloud-save/upload", new { profileId = "", saveData = "{}", saveVersion = 31 }, "", HttpStatusCode.BadRequest);
        // Missing saveData
        await PostExpect(client, "/cloud-save/upload", new { profileId = "CS-EMPTY", saveData = "", saveVersion = 31 }, "CS-EMPTY", HttpStatusCode.BadRequest);
    }

    private static async Task TestCloudSave_OversizedData(HttpClient client)
    {
        var oversized = new string('x', 512 * 1024 + 100);
        var json = JsonSerializer.Serialize(new { profileId = "CS-BIG", saveData = oversized, saveVersion = 31 }, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/cloud-save/upload");
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", "CS-BIG");
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert((int)resp.StatusCode == 413, $"expected 413 for oversized save, got {(int)resp.StatusCode}");
    }

    private static async Task TestCloudSaveInfo_NonexistentProfile(HttpClient client)
    {
        var resp = await Get(client, "/cloud-save/info?profileId=CS-GHOST");
        Assert(resp.RootElement.GetProperty("status").GetString() == "empty", "expected empty for nonexistent profile");
    }

    private static async Task TestAnalyticsIngest_EmptyBatch(HttpClient client)
    {
        var body = new { profileId = "A-EMPTY", clientVersion = 31, platform = "desktop", events = Array.Empty<object>() };
        var resp = await Post(client, "/analytics/ingest", body, "A-EMPTY");
        Assert(resp.RootElement.GetProperty("accepted").GetInt32() == 0, "expected 0 accepted for empty batch");
    }

    private static async Task TestAnalyticsIngest_OversizedBatch(HttpClient client)
    {
        var events = new object[51];
        for (var i = 0; i < 51; i++) events[i] = new { type = "spam", data = "x" };
        var body = new { profileId = "A-BIG", clientVersion = 31, platform = "desktop", events };
        await PostExpect(client, "/analytics/ingest", body, "A-BIG", HttpStatusCode.BadRequest);
    }

    private static async Task TestAnalyticsIngest_SkipsEmptyType(HttpClient client)
    {
        var body = new
        {
            profileId = "A-SKIP",
            clientVersion = 31,
            platform = "desktop",
            events = new[] { new { type = "", data = "should skip" }, new { type = "valid_event", data = "ok" } }
        };
        var resp = await Post(client, "/analytics/ingest", body, "A-SKIP");
        Assert(resp.RootElement.GetProperty("accepted").GetInt32() == 1, "expected 1 accepted, 1 skipped");
    }

    private static async Task TestDailyComplete_MissingDate(HttpClient client)
    {
        await PostExpect(client, "/daily/complete", new { profileId = "DC-01", date = "", score = 100 }, "DC-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestDailyComplete_InvalidScore(HttpClient client)
    {
        await PostExpect(client, "/daily/complete", new { profileId = "DC-02", date = "2026-03-17", score = -1 }, "DC-02", HttpStatusCode.BadRequest);
        await PostExpect(client, "/daily/complete", new { profileId = "DC-03", date = "2026-03-17", score = 9999999 }, "DC-03", HttpStatusCode.BadRequest);
    }

    private static async Task TestDailyLeaderboard_EmptyDate(HttpClient client)
    {
        var resp = await Get(client, "/daily/leaderboard/NEVER-PLAYED");
        var entries = resp.RootElement.GetProperty("entries");
        Assert(entries.GetArrayLength() == 0, "expected 0 entries for nonexistent date");
    }

    private static async Task TestAchievementSync_EmptyIds(HttpClient client)
    {
        var body = new { profileId = "ACH-EMPTY", achievementIds = Array.Empty<string>() };
        var resp = await Post(client, "/achievements/sync", body, "ACH-EMPTY");
        Assert(resp.RootElement.GetProperty("synced").GetInt32() == 0, "expected 0 synced");
    }

    private static async Task TestAchievementList_NonexistentProfile(HttpClient client)
    {
        var resp = await Get(client, "/achievements/GHOST-PROFILE");
        var achievements = resp.RootElement.GetProperty("achievements");
        Assert(achievements.GetArrayLength() == 0, "expected 0 achievements for nonexistent profile");
    }

    private static async Task TestHealthEndpoint(HttpClient client)
    {
        var resp = await Get(client, "/health");
        Assert(resp.RootElement.GetProperty("status").GetString() == "healthy", "expected healthy");
    }

    private static async Task TestRoomResult_MissingRoomId(HttpClient client)
    {
        await PostExpect(client, "/challenge-room-result",
            new { result = new { roomId = "", playerProfileId = "RR-01", score = 100 } },
            "RR-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomTelemetry_MissingRoomId(HttpClient client)
    {
        await PostExpect(client, "/challenge-room-telemetry",
            new { telemetry = new { roomId = "", playerProfileId = "RT-01", elapsedSeconds = 10.0, hullRatio = 0.9, enemyDefeats = 5, raceStatus = "racing" } },
            "RT-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomLeave_MissingRoomId(HttpClient client)
    {
        await PostExpect(client, "/challenge-room-leave",
            new { roomId = "" },
            "RL-01", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomReport_MissingFields(HttpClient client)
    {
        // Missing roomId
        await PostExpect(client, "/challenge-room-report",
            new { report = new { roomId = "", reporterProfileId = "REP-01", targetProfileId = "REP-02", reason = "test" } },
            "REP-01", HttpStatusCode.BadRequest);
        // Missing reason
        await PostExpect(client, "/challenge-room-report",
            new { report = new { roomId = "ROOM-X", reporterProfileId = "REP-01", targetProfileId = "REP-02", reason = "" } },
            "REP-01", HttpStatusCode.BadRequest);
        // Missing reporter (empty header + empty body)
        await PostExpect(client, "/challenge-room-report",
            new { report = new { roomId = "ROOM-X", reporterProfileId = "", targetProfileId = "REP-02", reason = "spam" } },
            "", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomMatchmake_MissingFields(HttpClient client)
    {
        // Missing boardCode
        await PostExpect(client, "/challenge-room-matchmake",
            new { matchmake = new { playerProfileId = "MM-01", playerCallsign = "Tester", boardCode = "" } },
            "MM-01", HttpStatusCode.BadRequest);
        // Missing profileId
        await PostExpect(client, "/challenge-room-matchmake",
            new { matchmake = new { playerProfileId = "", playerCallsign = "Tester", boardCode = "BOARD-X" } },
            "", HttpStatusCode.BadRequest);
    }

    private static async Task TestRoomSeatLease_MissingFields(HttpClient client)
    {
        // Missing roomId
        await PostExpect(client, "/challenge-room-seat-lease",
            new { lease = new { roomId = "", playerProfileId = "SL-01", ticketId = "TK-01" } },
            "SL-01", HttpStatusCode.BadRequest);
        // Missing profileId
        await PostExpect(client, "/challenge-room-seat-lease",
            new { lease = new { roomId = "ROOM-X", playerProfileId = "", ticketId = "TK-01" } },
            "", HttpStatusCode.BadRequest);
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

    private static async Task<HttpStatusCode> PostExpect(HttpClient client, string url, object body, string profileId, HttpStatusCode expected)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(msg);
        Assert(resp.StatusCode == expected, $"expected {(int)expected} at {url}, got {(int)resp.StatusCode}");
        return resp.StatusCode;
    }

    private static async Task<HttpStatusCode> GetExpect(HttpClient client, string url, HttpStatusCode expected)
    {
        var resp = await client.GetAsync(url);
        Assert(resp.StatusCode == expected, $"expected {(int)expected} at {url}, got {(int)resp.StatusCode}");
        return resp.StatusCode;
    }

    private static async Task<JsonDocument> Get(HttpClient client, string url)
    {
        var resp = await client.GetAsync(url);
        Assert(resp.StatusCode == HttpStatusCode.OK, $"HTTP {(int)resp.StatusCode} at {url}");
        var text = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }
}
