using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// --boards: every painted board of the More hub, first on a fresh save and then in use (floors climbed, an
/// expedition away, a guild joined, friends added, an event running), with its main actions pressed through the
/// real buttons. Each capture is checked for buttons that overlap and for content under the board's painted
/// corners (the props and the hanging banner). Captures go to artifacts/boards (artifacts/boards-WxH with --window).
/// </summary>
public partial class UiReviewSmoke
{
    private async Task ReviewBoards()
    {
        var window = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--window="))?["--window=".Length..];
        _output = ProjectSettings.GlobalizePath(window == null ? "res://artifacts/boards" : $"res://artifacts/boards-{window}");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        var apply = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        var screens = new[] { SceneRouter.TowerScene, SceneRouter.BountyScene, SceneRouter.BossRushScene, SceneRouter.RaidScene, SceneRouter.EventScene,
            SceneRouter.ExpeditionScene, SceneRouter.LoginCalendarScene, SceneRouter.ProgressionScene,
            SceneRouter.GuildScene, SceneRouter.FriendsScene, SceneRouter.ArenaScene, SceneRouter.LeaderboardScene };

        async Task<RoyalBoard> Show(string path)
        {
            menu.CloseHomeModal();
            menu.OpenHomeDestination(path); await Wait(.45);
            var board = Walk(menu).OfType<RoyalBoard>().SingleOrDefault();
            Check(board != null && menu.HomeModalDestination == path, System.IO.Path.GetFileNameWithoutExtension(path) + " opens as a painted board");
            return board;
        }
        RoyalButton Find(RoyalBoard board, string name) => Walk(board).OfType<RoyalButton>()
            .LastOrDefault(b => b.IsVisibleInTree() && (b.AccessibilityName == name || b.Text == name || b.TooltipText == name));
        async Task Look(RoyalBoard board, string name)
        {
            await Capture("board-" + name);
            AuditText("Boards / " + name);
            var buttons = Walk(board).OfType<Button>().Where(b => b.IsVisibleInTree() && !b.IsQueuedForDeletion()).Select(b => (Button: b, Rect: b.GetGlobalRect().Grow(-1))).ToArray();
            for (var i = 0; i < buttons.Length; i++)
                for (var j = i + 1; j < buttons.Length; j++)
                    if (buttons[i].Rect.Intersects(buttons[j].Rect))
                    { GD.Print($"BOARD_OVERLAP {name}: {buttons[i].Button.AccessibilityName} / {buttons[j].Button.AccessibilityName}"); _failures++; }
            var live = board.GetNodeOrNull<Control>("Live");
            foreach (var control in live == null ? Array.Empty<Control>() : Walk(live).OfType<Control>().Where(c => c != live && c.IsVisibleInTree()).ToArray())
            {
                var visible = control switch { RoyalLabel label => label.Text.Length > 0, TextureRect picture => picture.Texture != null, Button or Panel or Label => true, _ => false };
                if (!visible) continue;
                var rect = control.GetGlobalRect().Grow(-3);
                if (board.PaintedCorners.Any(corner => corner.Size.X > 0 && corner.Intersects(rect)))
                { GD.Print($"BOARD_UNDER_PROPS {name}: {control.GetType().Name} {(control as RoyalLabel)?.Text ?? (control as Button)?.AccessibilityName} {rect}"); _failures++; }
            }
        }

        // A fresh caravan: empty slots, no guild, no friends, nothing claimed.
        foreach (var path in screens) await Look(await Show(path), System.IO.Path.GetFileNameWithoutExtension(path) + "-fresh");

        // A caravan in the middle of the campaign.
        var fixture = state.BuildSaveData();
        fixture.Gold = 6000; fixture.Food = 20; fixture.Essence = 40; fixture.Sigils = 6; fixture.HighestUnlockedStage = 40;
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.Take(9).ToArray();
        fixture.ActiveDeckUnitIds = fixture.OwnedPlayerUnitIds.Take(3).ToArray();
        fixture.UnitLevels = fixture.OwnedPlayerUnitIds.ToDictionary(id => id, _ => 8);
        fixture.TowerHighestFloor = 23;
        fixture.TowerFloorStars = Enumerable.Range(0, ChallengeTowerCatalog.MaxFloor).Select(i => i < 23 ? 1 + i % 3 : 0).ToArray();
        fixture.ArenaRating = 1340; fixture.ArenaWins = 12;
        fixture.BestBossRushWave = 5;
        apply.Invoke(state, new object[] { fixture });

        var tower = await Show(SceneRouter.TowerScene);
        Check(Find(tower, "Climb floor 24") is { Disabled: false }, "Tower opens on the next floor to climb");
        Check(Find(tower, "Floor 25") is { Disabled: true } && Find(tower, "Floor 24") is { Disabled: false }, "Floors above the next one are locked");
        await TapModal(Find(tower, "Next floors")); await Wait(.2);
        Check(Find(tower, "Floor 31") is { Disabled: true } && Find(tower, "Floor 21") == null, "Next floors turns to the following ten");
        await TapModal(Find(tower, "Previous floors")); await Wait(.2);
        await TapModal(Find(tower, "Floor 21")); await Wait(.2);
        Check(Find(tower, "Climb floor 21") is { Disabled: false }, "A cleared floor can be climbed again");
        await Look(tower, "TowerMenu-climbed");
        await TapModal(Find(tower, "Climb floor 21")); await Wait(.4);
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene && state.CurrentBattleMode == BattleRunMode.Tower, "Climb prepares the chosen tower floor");

        var expeditions = await Show(SceneRouter.ExpeditionScene);
        var dispatch = Find(expeditions, "Dispatch");
        Check(dispatch is { Disabled: false }, "An expedition can be dispatched with allies outside the squad");
        await TapModal(dispatch); await Wait(.3);
        Check(state.ActiveExpeditionCount == 1, "Dispatch sends an expedition");
        await Look(expeditions, "ExpeditionMenu-underway");

        var gifts = await Show(SceneRouter.LoginCalendarScene);
        var day = state.LoginCalendarDay;
        await TapModal(Find(gifts, "Claim")); await Wait(.3);
        Check(state.LoginCalendarDay == day + 1, "Claim collects today's gift");
        await Look(gifts, "LoginCalendarMenu-claimed");

        var guild = await Show(SceneRouter.GuildScene);
        await TapModal(Find(guild, "Join guild")); await Wait(.3);
        Check(state.CachedGuildInfo != null, "Join guild joins a guild");
        var gold = state.Gold;
        await TapModal(Find(guild, "Contribute")); await Wait(.3);
        Check(state.Gold == gold - 50, "Contribute spends its gold");
        await Look(guild, "GuildMenu-member");

        var friends = await Show(SceneRouter.FriendsScene);
        foreach (var id in new[] { "CVY-7F3A21", "CVY-90BC44", "CVY-12DE55" })
        {
            var input = Walk(friends).OfType<LineEdit>().Single(); input.Text = id; input.EmitSignal(LineEdit.SignalName.TextChanged, id);
            await TapModal(Find(friends, "Add friend")); await Wait(.2);
            friends = Walk(menu).OfType<RoyalBoard>().Single();
        }
        Check(state.GetFriendIds().Count == 3, "Add friend adds each profile ID");
        await TapModal(Find(friends, "Send gift to CVY-12DE55")); await Wait(.3);
        Check(state.GiftsSentToday == 1, "Send gift sends one of the day's gifts");
        await Look(friends, "FriendsMenu-friends");
        await TapModal(Find(Walk(menu).OfType<RoyalBoard>().Single(), "Remove CVY-90BC44")); await Wait(.3);
        Check(state.GetFriendIds().Count == 2, "Remove takes a friend off the list");

        var arena = await Show(SceneRouter.ArenaScene);
        await Look(arena, "ArenaMenu-gold");
        await TapModal(Walk(arena).OfType<RoyalButton>().First(b => b.IsVisibleInTree() && b.AccessibilityName == "Challenge")); await Wait(.4);
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene && state.CurrentBattleMode == BattleRunMode.Arena, "Challenge prepares the arena battle");

        await Look(await Show(SceneRouter.BossRushScene), "BossRushMenu-five");
        foreach (var page in new[] { "Collection", "Wagon looks" })
        {
            var progression = await Show(SceneRouter.ProgressionScene);
            await PressTab(page);
            await Look(Walk(menu).OfType<RoyalBoard>().Single(), "ProgressionMenu-" + page.Replace(' ', '-'));
        }

        // An event outside its dates, to see its stages and milestones.
        EventMenu.Preview = SeasonalEventCatalog.GetAll()[0];
        try { await Look(await Show(SceneRouter.EventScene), "EventMenu-running"); }
        finally { EventMenu.Preview = null; }

        menu.CloseHomeModal();
        GD.Print($"BOARDS_REVIEW_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
