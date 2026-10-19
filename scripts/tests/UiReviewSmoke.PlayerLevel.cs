using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    /// <summary>The nickname prompt, the home player card, the level track, the caps in the armory and the result board's level card.</summary>
    private async Task ReviewPlayerLevel()
    {
        _output = ProjectSettings.GlobalizePath(GetWindow().Size.Y < 500 ? "res://artifacts/player-level/mobile" : "res://artifacts/player-level/desktop");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance; state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        Check(!state.HasChosenCallsign && !state.ShouldPromptForNickname, "Isolated review saves skip the first-run nickname prompt");

        // The home map shows this dialog to a new caravan; the driver opens it the same way.
        NicknameDialog.Show(menu, true); await Wait(.4);
        var field = Walk(menu).OfType<LineEdit>().Single(x => x.Name == "NicknameField" && x.IsVisibleInTree());
        Check(GameState.IsValidCallsign(field.Text, out _), $"The first-run prompt suggests a valid name ({field.Text})");
        Check(!Walk(menu).OfType<Button>().Any(x => x.IsVisibleInTree() && x.Text == "Cancel"), "A new caravan cannot skip naming itself");
        AuditText("Nickname / first run"); await Capture("01-nickname-first-run");
        Button Confirm() => Walk(menu).OfType<Button>().Single(x => x.Name == "ConfirmNickname" && x.IsVisibleInTree());
        field.Text = "ab"; Confirm().EmitSignal(BaseButton.SignalName.Pressed); await Wait(.2);
        var error = Walk(menu).OfType<Label>().Single(x => x.Name == "NicknameError");
        Check(error.IsVisibleInTree() && !state.HasChosenCallsign, "A too-short nickname is explained and not saved");
        await Capture("02-nickname-error");
        field.Text = "Ser Rowan"; Confirm().EmitSignal(BaseButton.SignalName.Pressed); await Wait(.3);
        Check(state.PlayerCallsign == "Ser Rowan" && state.HasChosenCallsign && !Walk(menu).OfType<LineEdit>().Any(x => x.Name == "NicknameField" && x.IsVisibleInTree()),
            "Confirming saves the nickname and closes the prompt");

        state.AddPlayerXP(PlayerLevelCatalog.TotalXPForLevel(4) + 70);
        await Open("MainMenu");
        menu = (MapMenu)GetTree().CurrentScene;
        var medallion = Walk(menu).OfType<LevelMedallion>().Single(x => x.IsVisibleInTree());
        Check(medallion.Level == 4 && medallion.Badge == 4 && Walk(menu).OfType<RoyalLabel>().Any(x => x.IsVisibleInTree() && x.Text == "Ser Rowan"),
            "The home card shows the nickname, the level and the rewards waiting");
        var resources = Walk(menu).OfType<RoyalResourceBar>().Single();
        var card = Walk(menu).OfType<RoyalButton>().Single(x => x.Name == "PlayerCard");
        Check(card.GetGlobalRect().Position.X < GetViewport().GetVisibleRect().Size.X * .1f && resources.Position.X + resources.BarRect.Position.X >= 850
              && !Walk(menu).OfType<RoyalLabel>().Any(x => x.IsVisibleInTree() && x.Text.StartsWith("LEVEL ")),
            "The player card sits top left with just the nickname, and the resources move to the top right");
        var amounts = Walk(resources).OfType<RoyalLabel>().Where(x => x.IsVisibleInTree()).Select(x => x.FontSize).ToArray();
        Check(amounts.Length == 3 && amounts.All(size => size >= 18), $"Resource amounts stay readable beside the zone plaque ({string.Join(", ", amounts)} px)");
        AuditText("Home / player card"); await Capture("03-home-card");

        Walk(menu).OfType<RoyalButton>().Single(x => x.Name == "PlayerCard").EmitSignal(BaseButton.SignalName.Pressed); await Wait(.5);
        var track = Walk(menu).OfType<PlayerLevelMenu>().SingleOrDefault(x => x.IsVisibleInTree());
        Check(track != null && menu.HomeModalDestination == "levels", "The player card opens the level track");
        AuditText("Level track"); await Capture("04-level-track");
        // Buttons have no hover highlight. A held press sinks the button and everything drawn on it, and a release
        // that acts springs it up past rest before it settles; its layout never moves.
        var back = Walk(menu).OfType<RoyalButton>().First(x => x.IsVisibleInTree() && x.Caption != null && x.Glyph != null);
        var captionY = back.Caption.Position.Y; var backRect = back.GetRect();
        ButtonFeel.Hold(back, true);
        // Frames, not a timer: the capture before stalls a frame long enough to fire a short timer at once.
        for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var held = ButtonFeel.Amount(back);
        var heldLayout = back.GetRect() == backRect && back.Caption.Position.Y == captionY;
        ButtonFeel.Hold(back, false, acted: true);
        var lowest = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            lowest = Mathf.Min(lowest, ButtonFeel.Amount(back));
        }
        Check(held > .99f && heldLayout && lowest < -.2f && ButtonFeel.Amount(back) == 0 && back.GetThemeStylebox("hover") == back.GetThemeStylebox("normal"),
            $"A held button presses in without moving its layout, springs up on release and settles, and hovering changes nothing (held {held:0.00}, spring {lowest:0.00})");
        // Held presses for the review capture: a kit button (claim all) and a button painted on the plate (back).
        var holdButton = Walk(menu).OfType<RoyalButton>().Single(x => x.Name == "ClaimAllLevelRewards");
        var painted = Walk(menu).OfType<RoyalButton>().FirstOrDefault(x => x.IsVisibleInTree() && x.HasMeta(ButtonFeel.Painted) && x.Caption != null);
        ButtonFeel.Hold(holdButton, true);
        if (painted != null) ButtonFeel.Hold(painted, true);
        for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Capture("04b-held-press");
        ButtonFeel.Hold(holdButton, false);
        if (painted != null) ButtonFeel.Hold(painted, false);
        // A real press held down and released off the button, so nothing is claimed.
        var holdAt = holdButton.GetGlobalRect().GetCenter();
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = holdAt, GlobalPosition = holdAt }); await Wait(.15);
        Send(new InputEventMouseMotion { Position = new Vector2(4, 4), GlobalPosition = new Vector2(4, 4) });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = new Vector2(4, 4), GlobalPosition = new Vector2(4, 4) }); await Wait(.15);
        Check(state.ClaimableLevelRewardCount > 1, "Releasing a held press off the button cancels it");
        RoyalButton Claim() => Walk(menu).OfType<RoyalButton>().FirstOrDefault(x => x.IsVisibleInTree() && !x.Disabled
            && ((x.AccessibilityName ?? "").StartsWith("Claim ") || x.TooltipText.StartsWith("Claim ")) && x.Name != "ClaimAllLevelRewards");
        var gold = state.Gold;
        Claim()?.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.3);
        Check(state.HasClaimedLevelReward(1) && state.Gold == gold + PlayerLevelCatalog.Reward(1).Amount, "A level reward card claims its reward");
        await Capture("05-level-track-claimed");
        Walk(menu).OfType<RoyalButton>().Single(x => x.Name == "ClaimAllLevelRewards" && x.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed); await Wait(.3);
        Check(state.ClaimableLevelRewardCount == 0 && !Walk(menu).OfType<RoyalButton>().Any(x => x.Name == "ClaimAllLevelRewards" && x.IsVisibleInTree()),
            "Claim all collects the rest and then steps aside");
        var grid = Walk(menu).OfType<ScrollContainer>().Single(x => x.Name == "LevelGrid");
        Check(Walk(grid).OfType<Panel>().Count(x => x.Name.ToString().StartsWith("Level")) == PlayerLevelCatalog.MaxLevel, "The grid holds all hundred levels");
        await PressHint("Later levels");
        Check(grid.ScrollVertical > 0, "The rail's chevron scrolls the grid to later levels");
        AuditText("Level track / later"); await Capture("06-level-track-later");
        await PressHint("Levels 81–100"); await Wait(.2);
        bool Shown(string name) => grid.GetGlobalRect().Grow(2).Encloses(Walk(grid).OfType<Panel>().Single(x => x.Name == name).GetGlobalRect());
        Check(Shown("Level81"), "The last rail badge jumps to level 81");
        grid.ScrollVertical = (int)grid.GetVScrollBar().MaxValue; await Wait(.2);
        Check(Shown("Level100"), "The grid scrolls down to level 100");
        await Capture("06b-level-track-100");
        Walk(menu).OfType<RoyalButton>().Single(x => x.Name == "ChangeNickname" && x.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed); await Wait(.4);
        Check(Walk(menu).OfType<Button>().Any(x => x.IsVisibleInTree() && x.Text == "Cancel"), "Renaming from the level track can be cancelled");
        await Capture("07-rename");
        await Press("Cancel");
        menu.CloseHomeModal();

        // The armory names the level a capped unit or wagon upgrade is waiting for.
        var fixture = state.BuildSaveData(); fixture.Gold = 100000;
        var unit = GameData.GetPlayerUnits().First();
        fixture.UnitLevels[unit.Id] = state.UnitLevelCap;
        fixture.BaseUpgradeLevels[BaseUpgradeCatalog.BallistaId] = state.WagonRankCap;
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        SceneRouter.Instance.GoToShop(); await Wait(.4);
        await PressHint(unit.DisplayName);
        var needed = PlayerLevelCatalog.LevelForUnitCap(state.UnitLevelCap + 1);
        Check(Walk(menu).OfType<RoyalButton>().Any(x => x.IsVisibleInTree() && x.Disabled && x.Text == $"Player level {needed}"),
            $"A unit at the cap shows the player level that lifts it ({needed})");
        AuditText("Armory / unit cap"); await Capture("08-armory-unit-cap");
        await Press("War wagon");
        await PressHint(BaseUpgradeCatalog.Get(BaseUpgradeCatalog.BallistaId).Title);
        Check(Walk(menu).OfType<RoyalButton>().Any(x => x.IsVisibleInTree() && x.Disabled && x.Text.StartsWith("PLAYER LV")),
            "A wagon upgrade at the rank cap shows the player level that lifts it");
        AuditText("Armory / wagon cap"); await Capture("09-wagon-cap");
        menu.CloseHomeModal();

        // A victory with mastery and a level gained, then a defeat with XP only.
        state.AddPlayerXP(PlayerLevelCatalog.TotalXPForLevel(state.PlayerLevel + 1) - state.PlayerXP - 5);
        var before = state.PlayerLevel;
        state.PrepareCampaignBattle();
        await Open("Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        battle.SetPhysicsProcess(false);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var (squad, index) in state.GetActiveDeckUnits().Select((squad, index) => (squad, index)))
            typeof(BattleController).GetMethod("SpawnUnit", hidden)!.Invoke(battle, new object[] { Team.Player, state.BuildPlayerUnitStats(squad), new Vector2(330 + index * 40, 360) });
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { true });
        await Wait(1.6);
        var board = Walk(battle).OfType<RoyalResult>().Single(x => x.IsVisibleInTree() && !x.IsQueuedForDeletion());
        Check(state.PlayerLevel == before + 1 && board.Rewards.Any(x => x.Kind == "player_xp") && Walk(board).OfType<Panel>().Any(x => x.Name == "LevelCard")
              && Walk(board).OfType<RoyalLabel>().Any(x => x.Text.StartsWith("LEVEL UP")), "A victory that gains a level says so on the result board");
        AuditText("Victory / level up"); await Capture("10-victory-level-up");

        state.PrepareCampaignBattle();
        await Open("Battle");
        battle = (BattleController)GetTree().CurrentScene;
        battle.SetPhysicsProcess(false);
        typeof(BattleController).GetMethod("DamageBusByRatio", hidden)!.Invoke(battle, new object[] { 1f, Colors.White });
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { false });
        await Wait(.8);
        board = Walk(battle).OfType<RoyalResult>().Single(x => x.IsVisibleInTree() && !x.IsQueuedForDeletion());
        Check(Walk(board).OfType<Panel>().Any(x => x.Name == "LevelCard") && !Walk(board).OfType<RoyalLabel>().Any(x => x.Text == "No rewards this time."),
            "A defeat still shows the XP it paid");
        AuditText("Defeat / level"); await Capture("11-defeat-level");

        await Open("MainMenu");
        menu = (MapMenu)GetTree().CurrentScene;
        AccountDialog.Show(menu); await Wait(.5);
        var account = Walk(GetTree().Root).OfType<LineEdit>().SingleOrDefault(x => x.Name == "AccountNickname" && x.IsVisibleInTree());
        Check(account != null && account.Text == state.PlayerCallsign, "Creating an account starts from the current nickname");
        await Capture("12-account-nickname");
        Walk(GetTree().Root).OfType<AccountDialog>().Single().QueueFree();
        GD.Print($"PLAYER_LEVEL_REVIEW_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
