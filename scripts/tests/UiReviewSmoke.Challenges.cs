using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// --challenges: every page of Multiplayer Challenges (Records, Daily, Featured, Saved, Squad) on a fresh save and
/// on a save with runs, pinned codes and a locked featured squad; the Rooms page with internet rooms switched on,
/// before and after joining a room; then the LAN race board, empty and hosted. Each page is captured, run through
/// the text audit and checked to stay inside the plate's board without scrolling. Captures go to
/// artifacts/challenges (artifacts/challenges-WxH with --window).
/// </summary>
public partial class UiReviewSmoke
{
    private static readonly Rect2 ChallengeBoard = new(50, 289, 1182, 318);

    private async Task ReviewChallenges()
    {
        var window = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--window="))?["--window=".Length..];
        _output = ProjectSettings.GlobalizePath(window == null ? "res://artifacts/challenges" : $"res://artifacts/challenges-{window}");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        MultiplayerMenu Menu() => Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<MultiplayerMenu>().First();
        RoyalButton Named(string name) => Walk(Menu()).OfType<RoyalButton>()
            .FirstOrDefault(b => b.IsVisibleInTree() && (b.AccessibilityName == name || b.TooltipText == name));
        void Tab(string name) => Walk(Menu()).OfType<RoyalButton>().First(b => b.Name == "BoardTab" + name).EmitSignal(BaseButton.SignalName.Pressed);

        async Task Look(string name)
        {
            await Wait(.35);
            AuditText("Challenges / " + name);
            await Capture(name);
        }

        async Task Pages(string prefix)
        {
            await Open("MultiplayerMenu");
            foreach (var tab in Walk(Menu()).OfType<RoyalButton>().Where(b => b.Name.ToString().StartsWith("BoardTab")).ToList())
            {
                var name = tab.Name.ToString()["BoardTab".Length..];
                tab.EmitSignal(BaseButton.SignalName.Pressed);
                await Wait(.35);
                CheckPageFits($"{prefix} / {name}");
                await Look($"{prefix}-{name.ToLowerInvariant()}");
            }
        }

        SeedConceptSave();
        await Pages("fresh");

        // Saved with nothing pinned offers to pin the loaded code.
        Tab("Saved");
        await Wait(.2);
        var loaded = state.GetSelectedAsyncChallenge().Code;
        Named("Pin loaded code")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.2);
        Check(state.IsChallengeCodePinned(loaded), "Saved: Pin loaded code pins the code on the briefing");
        await Look("fresh-saved-pinned");
        state.TogglePinnedChallengeCode(loaded, out _, out _);

        SeedChallengeHistory();
        await Pages("played");

        // Loading a featured board from its card makes it the briefing's board.
        Tab("Featured");
        await Wait(.2);
        var featured = FeaturedChallengeCatalog.GetDailyRotation(state.HighestUnlockedStage, state.MaxStage)
            .FirstOrDefault(f => f.Challenge.Stage <= state.HighestUnlockedStage && !state.GetSelectedAsyncChallenge().Code.Equals(f.Challenge.Code));
        if (featured != null)
        {
            var load = Named("Load " + featured.Title);
            Check(load is { Disabled: false }, $"Featured: {featured.Title} has a Load button");
            load?.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(.2);
            Check(state.GetSelectedAsyncChallenge().Code == featured.Challenge.Code, $"Featured: Load loads {featured.Challenge.Code}");
            await Look("played-featured-loaded");
        }

        // Internet rooms (held back at launch): the local directory lists rooms, and joining one shows its bar.
        LaunchFeatures.InternetRooms = true;
        try
        {
            await Open("MultiplayerMenu");
            Named("Refresh")?.EmitSignal(BaseButton.SignalName.Pressed);
            await Look("rooms");
            var join = Walk(Menu()).OfType<RoyalButton>().FirstOrDefault(b => b.IsVisibleInTree() && b.AccessibilityName == "Join room" && !b.Disabled);
            Check(join != null, "Rooms: an open room offers Join");
            join?.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(.3);
            Check(OnlineRoomJoinService.GetCachedTicket() != null && Walk(Menu()).Any(n => n.Name == "JoinedRoom"), "Rooms: joining shows the joined room bar");
            await Look("rooms-joined");
            Named("Leave room")?.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(.2);
            Check(OnlineRoomJoinService.GetCachedTicket() == null, "Rooms: Leave gives up the seat");
        }
        finally { LaunchFeatures.InternetRooms = false; }

        await Open("LanRaceMenu");
        await Look("lan-race");
        var lan = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<LanRaceMenu>().First();
        Walk(lan).OfType<RoyalButton>().First(b => b.AccessibilityName == "Host board").EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.5);
        Check(LanChallengeService.Instance.IsHosting, "LAN: Host board opens a room");
        await Look("lan-race-hosted");
        LanChallengeService.Instance.CloseRoom();

        GD.Print($"CHALLENGES_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }

    /// <summary>A page's content stays inside the plate's board, and nothing on it scrolls.</summary>
    private void CheckPageFits(string page)
    {
        var menu = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<MultiplayerMenu>().First();
        var layer = menu.FindChild("BoardPage", true, false) as Control;
        if (layer == null) { Check(false, $"Challenges / {page}: has a board page"); return; }
        var canvas = layer.GetGlobalTransformWithCanvas().AffineInverse();
        var outside = Walk(layer).OfType<Control>().Where(c => c != layer && c.IsVisibleInTree())
            .Select(c => (c, rect: canvas * c.GetGlobalRect())).Where(x => !ChallengeBoard.Encloses(x.rect)).ToList();
        foreach (var (control, rect) in outside.Take(3)) GD.Print($"CHALLENGE_PAGE_OUTSIDE {page}: {control.GetType().Name} {rect}");
        Check(outside.Count == 0, $"Challenges / {page}: content stays inside the board ({outside.Count} outside)");
        Check(!Walk(menu).OfType<ScrollContainer>().Any(s => s.IsVisibleInTree()) || page.EndsWith("Rooms"), $"Challenges / {page}: nothing scrolls");
    }

    /// <summary>Runs on three codes, two pinned, a best on each, and the first featured board loaded with its locked squad.</summary>
    private static void SeedChallengeHistory()
    {
        var state = GameState.Instance;
        var fixture = state.BuildSaveData();
        var featured = FeaturedChallengeCatalog.GetDailyRotation(state.HighestUnlockedStage, state.MaxStage).ToList();
        var codes = featured.Select(f => f.Challenge.Code).Take(2).Append(AsyncChallengeCatalog.Create(3, AsyncChallengeCatalog.PressureSpikeId, 4821).Code).ToArray();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var history = new List<ChallengeRunRecord>();
        for (var i = 0; i < 6; i++)
        {
            AsyncChallengeCatalog.TryParse(codes[i % codes.Length], out var challenge, out _);
            history.Add(new ChallengeRunRecord
            {
                Code = challenge.Code, Stage = challenge.Stage, MutatorId = challenge.MutatorId, Score = 1800 + i * 430, RawScore = 1500 + i * 360,
                ScoreMultiplier = 1.2f, Won = i % 3 != 2, Retreated = i == 5, ElapsedSeconds = 96 + i * 11, EnemyDefeats = 30 + i * 4,
                StarsEarned = i % 3 == 2 ? 0 : 2 + i % 2, PlayerDeployments = 14 + i, BusHullRatio = .55f + i * .06f, PlayedAtUnixSeconds = now - i * 30000
            });
        }
        fixture.ChallengeHistory = history;
        fixture.ChallengeRuns = history.Count;
        fixture.ChallengeBestScores = codes.ToDictionary(c => c, c => history.Where(h => h.Code == c).Max(h => h.Score));
        fixture.PinnedChallengeCodes = new[] { codes[0], codes[2] };
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { fixture });
        if (featured.Count > 0) state.SetSelectedFeaturedChallenge(featured[0]);
    }
}
