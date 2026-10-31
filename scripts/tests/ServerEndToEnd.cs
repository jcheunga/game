using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Plays the game against a real local server (scripts/tests/server_e2e.sh starts one): guest
/// session, cloud backup, email sign-in through the Account dialog, every account-backed feature,
/// sign-out, a second install restoring the account, then deleting it from Settings, and the first
/// install opening again to find its account gone. The server prints sign-in codes to its
/// log instead of mailing them, so the account is verified without email delivery.
/// </summary>
public partial class ServerEndToEnd : Node
{
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private int _failures;
    private string _server = "", _log = "", _handoff = "";

    public override void _Ready()
    {
        // A device that finds its account deleted reloads home as soon as the game opens; the driver outlives that.
        LiveUiReview.PreserveDriver(this);
        Callable.From(Run).CallDeferred();
    }

    private void Check(bool ok, string description) { GD.Print($"E2E_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?[(name.Length + 3)..] ?? "";
    private static IEnumerable<Node> Walk(Node node)
    { yield return node; foreach (var child in node.GetChildren()) foreach (var descendant in Walk(child)) yield return descendant; }

    private async Task<bool> Until(Func<bool> condition, double seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition()) { if (DateTime.UtcNow > deadline) return false; await Wait(.1); }
        return true;
    }

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=server-e2e-"))) throw new Exception("Use an isolated server-e2e save.");
            _server = Arg("server").TrimEnd('/'); _log = Arg("server-log"); _handoff = Arg("handoff");
            if (_server.Length == 0 || _log.Length == 0 || _handoff.Length == 0) throw new Exception("Pass --server, --server-log and --handoff.");
            GetTree().AutoAcceptQuit = false;
            await Wait();
            switch (Arg("role"))
            {
                case "second": await SecondInstall(); break;
                case "return": await ReturningInstall(); break;
                default: await FirstInstall(); break;
            }
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"E2E_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>A fresh install with the release backend: both endpoints on the server, HTTP sync on.</summary>
    private async Task<GameState> FreshInstall()
    {
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        state.SetPurchaseValidationEndpoint(_server);
        state.SetChallengeSyncProvider(ChallengeSyncProviderCatalog.HttpApiId);
        state.SetChallengeSyncEndpoint(_server + "/challenge-sync");
        state.SetChallengeSyncAutoFlush(true);
        await LiveUiReview.Open(this, "MainMenu");
        return state;
    }

    private async Task FirstInstall()
    {
        var state = await FreshInstall();
        var email = $"e2e-{Guid.NewGuid():N}"[..16] + "@example.com";

        // Guest
        NetworkStatus.Instance.CheckNow();
        Check(NetworkStatus.Instance.IsServerReachable && NetworkStatus.Instance.GetStatusLabel() == "Online", "The game reaches the server");
        Check(await Until(() => state.PlayerAuthToken.Length > 0), "A guest gets a server session without signing in");
        var guest = state.PlayerProfileId;
        Check(state.AccountProvider.Length == 0 && (await Get($"/cloud-save/info?profileId={guest}", guest, state.PlayerAuthToken)).Status == 200,
            "The guest session is accepted by protected endpoints");
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WinStage(state, 1);
        var info = CloudSaveService.GetInfo();
        Check(info.Status == "ok" && info.UploadedAtUnixSeconds >= started && info.SizeBytes > 0, "Winning a battle backs a guest's progress up to the cloud");

        // Verified account through the Account dialog
        Check(await SignIn(email, wrongCodeFirst: true), "Email sign-in finishes through the Account dialog");
        Check(state.AccountProvider == "email" && state.AccountLabel == email && state.PlayerProfileId == guest,
            "Signing in links the guest's own profile to the verified email");
        Check(state.HighestUnlockedStage == 2, "Linking keeps the guest's progress");
        var account = state.PlayerProfileId; var accountToken = state.PlayerAuthToken;

        await AccountFeatures(state);
        await PurchaseRecovery(state);
        await RefundRecovery(state);
        await PurchasePause(state);
        await SupportGrant(state);

        // A late-game save makes the full trip: upload, sign-out, sign back in, download.
        SaveRoundTripReview.FillLateGame(state);
        Check(CloudSaveService.Upload(out var lateUpload), $"A late-game save uploads ({lateUpload})");
        var uploaded = CloudJson(state);

        // Sign out
        await LiveUiReview.Open(this, "MainMenu");
        AccountDialog.Show((Control)GetTree().CurrentScene);
        var dialog = await OpenedDialog();
        Press(dialog, "Sign out");
        Check(await Until(() => state.AccountProvider.Length == 0 && state.PlayerProfileId != account), "Sign-out returns the device to a new guest");
        Check(state.SkipAutoSignIn && state.HighestUnlockedStage == 1, "After sign-out the device starts fresh and stays a guest");
        Check((await Get($"/cloud-save/info?profileId={account}", account, accountToken)).Status == 401, "Sign-out revokes the account session on the server");

        // Sign back in on the same device
        NetworkStatus.Instance.CheckNow();
        Check(await Until(() => state.PlayerAuthToken.Length > 0), "The new guest gets its own session");
        Check(await SignIn(email), "Signing back in finishes");
        Check(state.PlayerProfileId == account && state.HighestUnlockedStage > 2 && !state.SkipAutoSignIn,
            "Signing back in restores the account's progress from the cloud");
        var restored = CloudJson(state);
        var differences = uploaded.Keys.Where(k => k is not "FoodRechargedAtUnixSeconds" && !(restored.TryGetValue(k, out var v) && Holds(v, uploaded[k]))).ToList();
        foreach (var key in differences) GD.Print($"E2E_DIFF: {key}: uploaded {Clip(uploaded[key])} restored {Clip(restored.GetValueOrDefault(key, "<missing>"))}");
        foreach (var key in uploaded.Keys.Where(k => k is not "FoodRechargedAtUnixSeconds" && restored.TryGetValue(k, out var v) && v != uploaded[k] && !differences.Contains(k)))
            GD.Print($"E2E_ADDED: {key} gained entries at load (defaults and implied map tiles)");
        Check(differences.Count == 0, $"Every saved value comes back from the cloud unchanged ({uploaded.Count} values; lost or changed: {string.Join(", ", differences)})");
        File.WriteAllText(_handoff, JsonSerializer.Serialize(new Dictionary<string, string>
            { ["email"] = email, ["account"] = account, ["token"] = state.PlayerAuthToken, ["stage"] = state.HighestUnlockedStage.ToString() }));
    }

    private async Task SecondInstall()
    {
        var handoff = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_handoff));
        var state = await FreshInstall();
        NetworkStatus.Instance.CheckNow();
        Check(await Until(() => state.PlayerAuthToken.Length > 0) && state.HighestUnlockedStage == 1, "A second install starts as an empty guest");
        Check(await DeveloperSignIn(state, handoff["email"]), "In developer mode the second install signs in to the same email without a code");
        var stage = int.Parse(handoff["stage"]);
        Check(state.PlayerProfileId == handoff["account"] && state.AccountProvider == "email" && state.HighestUnlockedStage == stage,
            $"The second install restores the account's progress (stage {stage})");

        var before = CloudSaveService.GetInfo().UploadedAtUnixSeconds;
        await Wait(1.1);
        WinStage(state, stage);
        var after = CloudSaveService.GetInfo();
        Check(after.Status == "ok" && after.UploadedAtUnixSeconds > before, "Progress on the second install backs up to the account");
        var account = handoff["account"];
        Check((await Get($"/cloud-save/download?profileId={account}", account, "")).Status == 401, "A profile ID alone cannot download an account's save");
        Check(CloudSaveService.Download(out var restored) && state.PlayerProfileId == account && state.HighestUnlockedStage == stage + 1,
            $"Settings' cloud restore downloads the latest account save ({restored})");

        // The server going away mid-play
        state.SetPurchaseValidationEndpoint("http://127.0.0.1:9");
        NetworkStatus.Instance.CheckNow();
        Check(!NetworkStatus.Instance.IsServerReachable, "A missing server is reported as unreachable");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        WinStage(state, stage + 1);
        Check(clock.Elapsed.TotalSeconds < 3 && state.HighestUnlockedStage == stage + 2 && SaveSystem.Instance.LastSaveSucceeded,
            $"Without the server a win still saves on the device, in {clock.Elapsed.TotalMilliseconds:0} ms");
        state.SetPurchaseValidationEndpoint(_server);
        NetworkStatus.Instance.CheckNow();
        var uploaded = CloudSaveService.Upload(out var uploadMessage);
        Check(NetworkStatus.Instance.IsServerReachable && uploaded, $"When the server returns, the save uploads again ({uploadMessage})");

        await DeleteAccountInSettings(state, handoff["email"]);
    }

    /// <summary>Settings → Account → Delete account, through its confirmation; then the server and the device have forgotten the account.</summary>
    private async Task DeleteAccountInSettings(GameState state, string email)
    {
        var account = state.PlayerProfileId; var token = state.PlayerAuthToken;
        Check(LocalBackups(account) > 0, "The device holds a local backup of the account from its earlier sign-out");
        await LiveUiReview.Open(this, "SettingsMenu");
        var settings = Walk(GetTree().Root).OfType<SettingsMenu>().Single(s => !s.IsQueuedForDeletion());
        Named(settings, "Account").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.3);
        Named(settings, "Delete account").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.3);
        Check(Walk(settings).OfType<Label>().Any(l => l.Text == "Delete your account?"), "Delete account asks for confirmation first");
        Press(settings, "Keep account");
        await Wait(.5);
        Check(state.PlayerProfileId == account && state.AccountProvider == "email" && (await Get($"/cloud-save/info?profileId={account}", account, token)).Status == 200,
            "Keeping the account changes nothing");

        Named(settings, "Delete account").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.3);
        Press(settings, "Delete account");
        Check(await Until(() => state.PlayerProfileId != account && state.AccountProvider.Length == 0, 15), "Confirming deletes the account from Settings");
        Check(state.HighestUnlockedStage == 1 && state.SkipAutoSignIn && state.PlayerAuthToken.Length == 0,
            "The device starts again as a new guest that does not sign in by itself");
        Check(LocalBackups(account) == 0, "The device removes its local backups of the deleted account");
        Check(await Until(() => GetTree().CurrentScene is MainMenu { HasHomeModal: false }) && await Quiet(() => DeletedNotice() == null) &&
            !state.AccountDeletedNoticePending, "Deleting in Settings returns home without the deleted-elsewhere notice");
        var ended = await Get($"/cloud-save/info?profileId={account}", account, token);
        Check(ended.Status == 410 && Error(ended.Body) == "account_deleted", "Deletion ends the account's session, which is told the account is gone");
        Check((await Get($"/cloud-save/info?profileId={account}", account, "")).Status == 401, "A request without a session is refused as before");
        var again = await Post("/account/delete", account, token);
        Check(again.Status == 200 && again.Body.GetProperty("alreadyDeleted").GetBoolean(), "Deleting again with the old session is harmless");

        NetworkStatus.Instance.CheckNow();
        Check(await DeveloperSignIn(state, email) && state.PlayerProfileId != account && state.HighestUnlockedStage == 1,
            "Signing in with the same email afterwards finds a new, empty account");
    }

    /// <summary>
    /// The first install opens again after the second deleted the account it is still signed in to. It
    /// notices on its own, starts again at home as a new guest, and the server never takes the old session
    /// or profile back. Routine calls that are refused together reset the device only once.
    /// </summary>
    private async Task ReturningInstall()
    {
        var handoff = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_handoff));
        var state = GameState.Instance;
        var account = handoff["account"]; var token = handoff["token"];
        Check(await Until(() => state.PlayerProfileId != account, 15) && state.LastResultMessage.StartsWith("Account deleted."),
            "Opening the first install again, it notices its account was deleted on the other device");
        Check(state.AccountProvider.Length == 0 && state.SkipAutoSignIn && state.HighestUnlockedStage == 1,
            "It starts again as a new guest that does not sign in by itself");
        Check(await Until(() => GetTree().CurrentScene is MainMenu), "It returns home");
        await DeletedNoticeOnce();

        // The calls the game makes routinely notice too, all refused at once, and the device resets once.
        await LiveUiReview.Open(this, "SettingsMenu");
        state.ApplyPlayerProfileSession(account, state.PlayerCallsign, token, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        state.SetSignedInAccount("email", handoff["email"]);
        Check(!PlayerProfileSyncService.RefreshProfile(out var refresh) && state.PlayerProfileId == account,
            $"A profile refresh with the deleted account's session is refused, not taken as a new guest ({refresh})");
        Check(!CloudSaveService.Upload(out var upload) && CloudSaveService.GetInfo().Status == "error", $"Its cloud backup is refused ({upload})");
        Check(await Until(() => state.PlayerProfileId != account && GetTree().CurrentScene is MainMenu),
            "The refused calls reset the device and return it home");
        var guest = state.PlayerProfileId;
        await Wait(1.5);
        Check(state.PlayerProfileId == guest && state.AccountProvider.Length == 0 && state.SkipAutoSignIn,
            "However many calls were refused, the device resets once");
        Check(await Until(() => DeletedNotice() != null) && await AcknowledgeDeletedNotice(), "Home explains that reset too, and OK closes it");

        // A battle in progress plays out first: the device resets once the player leaves it.
        state.ApplyPlayerProfileSession(account, state.PlayerCallsign, token, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        state.SetSignedInAccount("email", handoff["email"]);
        state.SetSelectedStage(1); state.PrepareCampaignBattle();
        await LiveUiReview.Open(this, "Battle");
        Check(!CloudSaveService.Upload(out _), "A cloud backup during a battle is refused");
        await Wait(1.5);
        Check(state.PlayerProfileId == account && GetTree().CurrentScene is BattleController, "The battle in progress is not interrupted");
        await LiveUiReview.Open(this, "MainMenu");
        Check(await Until(() => state.PlayerProfileId != account && GetTree().CurrentScene is MainMenu), "Leaving the battle resets the device");
        Check(await Until(() => DeletedNotice() != null) && await AcknowledgeDeletedNotice(), "Home explains the reset after the battle, and OK closes it");
        guest = state.PlayerProfileId;

        var profile = await Post("/player-profile", account, token);
        Check(profile.Status == 410 && Error(profile.Body) == "account_deleted", "The server still refuses the old session instead of registering the profile again");
        NetworkStatus.Instance.CheckNow();
        Check(await Until(() => state.PlayerAuthToken.Length > 0) && state.PlayerProfileId == guest, "The new guest gets its own session");
    }

    /// <summary>
    /// After resetting itself, the device says why on the home map: one notice with one button, saved so it would
    /// still come after a restart. OK closes it for good.
    /// </summary>
    private async Task DeletedNoticeOnce()
    {
        var state = GameState.Instance;
        Check(await Until(() => DeletedNotice() != null), "Home explains that the account was deleted elsewhere");
        var notice = DeletedNotice();
        if (notice == null) return;
        var labels = Walk(notice).OfType<Label>().Select(l => l.Text).ToList();
        Check(labels.Contains("Account deleted") && labels.Any(t => t.Contains("new guest")) &&
            Walk(notice).OfType<Button>().Select(b => b.Text).SequenceEqual(["OK"]),
            $"The notice reads \"Account deleted\" with a single OK button ({string.Join(" / ", labels)})");
        Check(state.AccountDeletedNoticePending && SaveSystem.Instance.TryLoad(out var pending) && pending.AccountDeletedNoticePending &&
            !CloudSavePrivacy.Sanitize(JsonSerializer.Serialize(state.BuildSaveData())).Contains(nameof(GameSaveData.AccountDeletedNoticePending)),
            "Until it is read, the notice is saved on the device, so it would still show after a restart, and never in the cloud save");
        Check(await AcknowledgeDeletedNotice() && SaveSystem.Instance.TryLoad(out var read) && !read.AccountDeletedNoticePending,
            "OK closes the notice and the device remembers it was read");
        await LiveUiReview.Open(this, "MainMenu");
        Check(await Quiet(() => DeletedNotice() == null), "Opening home again does not show it again");
    }

    private async Task<bool> AcknowledgeDeletedNotice()
    {
        var notice = DeletedNotice();
        if (notice == null) return false;
        Press(notice, "OK");
        return await Until(() => DeletedNotice() == null) && !GameState.Instance.AccountDeletedNoticePending;
    }

    private Control DeletedNotice() => GetTree().CurrentScene is MainMenu home
        ? Walk(home).OfType<Control>().FirstOrDefault(c => c.Name == "AccountDeletedNotice" && !c.IsQueuedForDeletion()) : null;

    /// <summary>Whether a condition holds throughout the next moment, for things that must not appear.</summary>
    private async Task<bool> Quiet(Func<bool> condition, double seconds = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline) { if (!condition()) return false; await Wait(.1); }
        return condition();
    }

    private static string Error(JsonElement body) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var error) ? error.GetString() ?? "" : "";

    private static int LocalBackups(string profile) =>
        DirAccess.GetFilesAt("user://account-backups").Count(f => f.StartsWith(profile + "-", StringComparison.Ordinal));

    private static BaseButton Named(Node root, string name) =>
        Walk(root).OfType<BaseButton>().Single(b => !b.IsQueuedForDeletion() && b.IsVisibleInTree() && b.AccessibilityName == name);

    /// <summary>Everything else the game sends to the server, as the signed-in account.</summary>
    private async Task AccountFeatures(GameState state)
    {
        var profile = state.PlayerProfileId; var token = state.PlayerAuthToken;
        Check(CloudSaveService.Upload(out var upload) && CloudSaveService.GetInfo().Status == "ok", $"Manual cloud upload works for the account ({upload})");

        foreach (var achievement in AchievementCatalog.GetAll().Take(2)) state.TryUnlockAchievement(achievement.Id);
        state.SyncAchievementsToServer();
        var achievements = await Get($"/achievements/{profile}", profile, token);
        Check(achievements.Status == 200 && achievements.Body.GetProperty("achievements").GetArrayLength() >= state.AchievementUnlockedCount,
            "Unlocked achievements sync to the account");

        var daily = new HttpApiDailyLeaderboardProvider(_server);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var completion = daily.SubmitCompletion(profile, date, 4321);
        Check(completion.PersonalBest == 4321 && daily.FetchLeaderboard(date).Entries.Any(e => e.Player == LeaderboardTag.You && e.Score == 4321),
            "A daily score submits and appears on the daily leaderboard");

        var code = AsyncChallengeCatalog.Create(1, AsyncChallengeCatalog.PressureSpikeId, 5151).Code;
        Check(state.TrySetSelectedAsyncChallengeCode(code, out _), "A challenge code arms");
        var deck = state.GetActiveDeckUnits().Select(unit => unit.Id).ToArray();
        state.ApplyAsyncChallengeResult(code, 1320, 27.5f, 14, 2, true, false, deck,
            [new ChallengeDeploymentRecord { UnitId = deck[0], TimeSeconds = 1.5f, LanePercent = 40 }], 1, .78f, false,
            new AsyncChallengeScoreBreakdown(400, 240, 180, 120, 60, 20, 980, 1.35f, 1320));
        Check(state.PendingChallengeSubmissionCount == 0 || ChallengeSyncService.Instance.FlushPendingSubmissions(out _), "Challenge results flush to the server");
        Check(state.PendingChallengeSubmissionCount == 0, "No challenge results are left waiting");
        Check(ChallengeLeaderboardService.Instance.RefreshBoard(code, 5, out var board)
            && ChallengeLeaderboardService.Instance.GetCachedSnapshot(code).Entries.Any(e => e.PlayerCallsign == LeaderboardTag.You && e.Score == 1320),
            $"The challenge leaderboard shows the account's flushed result ({board})");
        Check(ChallengeBoardFeedService.Instance.RefreshFeed(state.HighestUnlockedStage, state.MaxStage, 3, out var feed), $"The challenge board feed loads ({feed})");

        Check(OnlineRoomCreateService.HostSelectedChallenge(out var hosted) && OnlineRoomCreateService.GetHostedRoom() is { RoomId.Length: > 0 }, $"An online room opens ({hosted})");
        var room = OnlineRoomCreateService.GetHostedRoom()?.RoomId ?? "";
        Check(OnlineRoomDirectoryService.RefreshRooms(state.HighestUnlockedStage, state.MaxStage, 10, out var rooms)
            && OnlineRoomDirectoryService.GetCachedRooms().Any(entry => entry.RoomId == room), $"The room list loads and shows the hosted room ({rooms})");
        Check(OnlineRoomSessionService.RefreshJoinedRoom(out var session) && OnlineRoomSessionService.GetCachedSnapshot() != null, $"The joined room's session loads ({session})");
        Check(OnlineRoomActionService.ToggleReady(out var ready), $"The host can ready up ({ready})");
        Check(OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scores), $"The room scoreboard loads ({scores})");
        Check(OnlineRoomActionService.LeaveRoom(out var left), $"The host can leave room {room} ({left})");

        var purchase = await state.ValidatePurchaseWithServerAsync("gold_pouch", "stripe", $"receipt-{Guid.NewGuid():N}", $"txn-{Guid.NewGuid():N}");
        var wallet = await Get($"/wallet?profileId={profile}", profile, token);
        Check(purchase.Status == "ok" && purchase.GoldCredited > 0 && wallet.Status == 200 && wallet.Body.GetProperty("goldBalance").GetInt32() >= purchase.GoldCredited,
            $"A test purchase validates and credits the account's wallet ({purchase.Status}: {purchase.Message})");

        // A save that never saw the kit (an older one, or another device's) learns from the server that it was bought,
        // so the shop never charges for it twice.
        var kit = await state.ValidatePurchaseWithServerAsync("starter_kit", "stripe", $"receipt-{Guid.NewGuid():N}", $"txn-{Guid.NewGuid():N}");
        Check(kit.Status == "ok" && !state.HasPurchasedProduct("starter_kit"), $"The kit is bought on the server only ({kit.Status}: {kit.Message})");
        Check(await state.SyncOneTimePurchasesFromServerAsync() && state.HasPurchasedProduct("starter_kit"),
            "The server's purchase history marks the kit bought on this device");
    }

    /// <summary>Signs in with email through the real Account dialog, reading the code from the server log.</summary>
    private async Task<bool> SignIn(string email, bool wrongCodeFirst = false)
    {
        var state = GameState.Instance;
        var profileBefore = state.PlayerProfileId; var providerBefore = state.AccountProvider;
        await LiveUiReview.Open(this, "MainMenu");
        AccountDialog.Show((Control)GetTree().CurrentScene);
        var dialog = await OpenedDialog();
        var send = Find(dialog, "Send code");
        if (!await Until(() => !send.Disabled)) { Check(false, $"The server offers email sign-in: {Status(dialog)}"); return false; }
        Check(!Find(dialog, "Continue with Google").Visible && !Find(dialog, "Continue with Apple").Visible,
            "Providers the server has no credentials for are left out");
        Check(Find(dialog, "Developer sign-in (no code)").Visible == state.DeveloperToolsActive, "Developer sign-in shows only in developer mode");
        Walk(dialog).OfType<LineEdit>().Single(x => x.PlaceholderText == "Email address").Text = email;
        var codesBefore = Codes(email).Count;
        send.EmitSignal(BaseButton.SignalName.Pressed);
        if (!await Until(() => Codes(email).Count > codesBefore)) { Check(false, $"The server issued a code: {Status(dialog)}"); return false; }
        var code = Codes(email).Last();
        var box = Walk(dialog).OfType<LineEdit>().Single(x => x.PlaceholderText == "6-digit code");
        await Until(() => box.Visible);
        if (wrongCodeFirst)
        {
            box.Text = code == "000000" ? "111111" : "000000";
            Press(dialog, "Sign in");
            Check(await Until(() => Status(dialog) == "Incorrect code."), "A wrong code is refused in the dialog");
        }
        box.Text = code;
        Press(dialog, "Sign in");
        var done = await Until(() => !IsInstanceValid(dialog) || dialog.IsQueuedForDeletion(), 15);
        if (!done) GD.Print($"E2E_NOTE: dialog still open: {Status(dialog)}");
        await Until(() => state.AccountProvider == "email" && (state.PlayerProfileId != profileBefore || providerBefore.Length == 0));
        await LiveUiReview.Open(this, "MainMenu");
        return done && state.AccountProvider == "email";
    }

    /// <summary>Signs in through the dialog's developer button, which a development server accepts without a code.</summary>
    private async Task<bool> DeveloperSignIn(GameState state, string email)
    {
        state.SetDeveloperMode(true);
        await LiveUiReview.Open(this, "MainMenu");
        AccountDialog.Show((Control)GetTree().CurrentScene);
        var dialog = await OpenedDialog();
        var button = Find(dialog, "Developer sign-in (no code)");
        if (!await Until(() => button.Visible && !button.Disabled)) { Check(false, $"The development server offers developer sign-in: {Status(dialog)}"); return false; }
        Walk(dialog).OfType<LineEdit>().Single(x => x.PlaceholderText == "Email address").Text = email;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        var done = await Until(() => !IsInstanceValid(dialog) || dialog.IsQueuedForDeletion(), 15);
        await Until(() => state.AccountProvider == "email");
        await LiveUiReview.Open(this, "MainMenu");
        return done && state.AccountProvider == "email" && state.AccountLabel == email;
    }

    private async Task<AccountDialog> OpenedDialog()
    {
        await Wait(.3);
        return Walk(GetTree().Root).OfType<AccountDialog>().Single(d => !d.IsQueuedForDeletion());
    }

    private static Button Find(Node dialog, string text) => Walk(dialog).OfType<Button>().Single(b => b.Text == text);
    private static void Press(Node dialog, string text) => Find(dialog, text).EmitSignal(BaseButton.SignalName.Pressed);
    private static string Status(Node dialog) => IsInstanceValid(dialog) ? Walk(dialog).OfType<Label>().FirstOrDefault()?.Text ?? "" : "";

    private List<string> Codes(string email)
    {
        var prefix = $"[dev] Sign-in code for {email}: ";
        try
        {
            using var stream = new FileStream(_log, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            return new StreamReader(stream).ReadToEnd().Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith(prefix)).Select(l => l[^6..]).ToList();
        }
        catch (IOException) { return []; }
    }

    /// <summary>The save as the cloud holds it (device-only fields removed), by top-level value.</summary>
    private static Dictionary<string, string> CloudJson(GameState state)
    {
        using var json = JsonDocument.Parse(CloudSavePrivacy.Sanitize(JsonSerializer.Serialize(state.BuildSaveData())));
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
    }

    /// <summary>Whether the restored JSON holds everything uploaded: same values, with object keys and array items only ever added.</summary>
    private static bool Holds(string restored, string uploaded)
    {
        using var a = JsonDocument.Parse(restored); using var b = JsonDocument.Parse(uploaded);
        return Holds(a.RootElement, b.RootElement);
    }

    private static bool Holds(JsonElement restored, JsonElement uploaded) => (restored.ValueKind, uploaded.ValueKind) switch
    {
        (JsonValueKind.Object, JsonValueKind.Object) => uploaded.EnumerateObject().All(p => restored.TryGetProperty(p.Name, out var v) && Holds(v, p.Value)),
        (JsonValueKind.Array, JsonValueKind.Array) => uploaded.EnumerateArray().Select(e => e.GetRawText()).ToHashSet()
            .IsSubsetOf(restored.EnumerateArray().Select(e => e.GetRawText())),
        _ => restored.GetRawText() == uploaded.GetRawText()
    };

    private static string Clip(string text) => text.Length > 200 ? text[..200] + "…" : text;

    private static void WinStage(GameState state, int stage)
    {
        state.SetSelectedStage(stage);
        state.PrepareCampaignBattle();
        state.ApplyVictory(stage, 40, 2, 3);
    }

    private Task<(int Status, JsonElement Body)> Get(string path, string profile, string token) => Send(HttpMethod.Get, path, profile, token);
    private Task<(int Status, JsonElement Body)> Post(string path, string profile, string token) => Send(HttpMethod.Post, path, profile, token);

    private async Task<(int Status, JsonElement Body)> Send(HttpMethod method, string path, string profile, string token)
    {
        using var request = new HttpRequestMessage(method, _server + path);
        if (method == HttpMethod.Post) request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Convoy-Profile", profile);
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        try { using var json = JsonDocument.Parse(text); return ((int)response.StatusCode, json.RootElement.Clone()); }
        catch (JsonException) { return ((int)response.StatusCode, default); }
    }
}
