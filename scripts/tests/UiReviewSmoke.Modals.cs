using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewModalActions(MapMenu menu, MapPathCanvas canvas)
    {
        var state = GameState.Instance;
        var baseline = state.BuildSaveData(); var fixture = state.BuildSaveData(); fixture.Gold = 10000; fixture.HighestUnlockedStage = 20;
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        var camera = canvas.MapOffset; var beforeFood = state.Food; var beforeKnowledge = state.AdventureKnowledgeRevision;
        SceneRouter.Instance.GoToShop(); await Wait(.4);
        var button = Walk(menu).OfType<Button>().First(control => control.Visible && control.IsVisibleInTree() && control.Text.StartsWith("Upgrade ") && !control.Disabled);
        Check(button.GetGlobalRect().End.Y < menu.GetGlobalRect().End.Y - 80, "Warband upgrade action is visible without scrolling");
        var unit = GameData.GetPlayerUnits().First(); var level = state.GetUnitLevel(unit.Id); var gold = state.Gold; var cost = state.GetUnitUpgradeCost(unit.Id);
        await TapModal(button);
        Check(state.GetUnitLevel(unit.Id) == level + 1 && state.Gold == gold - cost, "A native upgrade click trains once and spends the correct gold");
        Check(state.Food == beforeFood && state.AdventureKnowledgeRevision == beforeKnowledge, "Clicks inside a modal never travel or reveal the map");
        var recruit = GameData.GetPlayerUnits().First(entry => !state.IsUnitOwned(entry.Id) && state.IsUnitAvailableForPurchase(entry.Id));
        await PressHint(recruit.DisplayName); await Press("Buy ");
        Check(state.IsUnitOwned(recruit.Id), "Recruiting an ally remains connected to progression");
        await Press("Equip"); Check(state.IsUnitInActiveDeck(recruit.Id), "The recruited ally can join the equipped warband");
        AuditText("Modal / trained warband"); await Capture("13-trained-warband");
        menu.CloseHomeModal(); SceneRouter.Instance.GoToShop(1); await Wait(.3);
        var spell = GameData.GetPlayerSpells().First(); await Press("Scribe "); await Press("Equip Spell");
        Check(state.IsSpellOwned(spell.Id) && state.IsSpellInActiveDeck(spell.Id), "Scribing and equipping a spell updates the spell loadout");
        var spellLevel = state.GetSpellLevel(spell.Id); await Press("Upgrade Lv");
        Check(state.GetSpellLevel(spell.Id) == spellLevel + 1, "Spell training works inside the modal");
        AuditText("Modal / trained spells"); await Capture("14-trained-spells"); menu.CloseHomeModal();
        state.TryUnlockAchievement("first_blood"); menu.OpenHomeDestination("achievements"); await Wait(.2);
        gold = state.Gold; await Press("Claim +100");
        Check(state.HasClaimedAchievementReward("first_blood") && state.Gold == gold + 100, "Achievement reward claims once from its card");
        Check(!state.TryClaimAchievementReward("first_blood", out _), "A claimed achievement cannot pay a second reward");
        AuditText("Modal / claimed achievements"); await Capture("15-achievement-claimed"); menu.CloseHomeModal();
        state.DiscoverCodexEntry("player_brawler"); state.DiscoverCodexEntry("enemy_walker"); state.DiscoverCodexEntry("enemy_runner");
        SceneRouter.Instance.GoToCodex(); await Wait(.3); await Press("Units"); await PressHint("Swordsman");
        Check(Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("steadfast blade")), "Selecting a discovered codex portrait shows its lore");
        Check(Walk(menu).OfType<Button>().Any(control => control.IsVisibleInTree() && control.AccessibilityName == "Undiscovered codex entry" && control.Disabled), "Undiscovered book entries stay locked");
        AuditText("Modal / discovered codex"); await Capture("16-codex-discovered");
        Send(new InputEventKey { Pressed = true, Keycode = Key.Escape }); await Wait(.2);
        Check(!menu.HasHomeModal && canvas.MapOffset == camera, "Escape dismisses the overlay and preserves the map");
        menu.OpenHomeDestination("more"); await Wait(.2);
        Send(new InputEventKey { Pressed = true, Keycode = Key.Tab });
        var modal = menu.GetNode<RealmModal>("HomeModal");
        Check(modal.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "Keyboard focus stays inside the open modal");
        var outside = new Vector2(18, 200);
        var outsideFood = state.Food; var outsideKnowledge = state.AdventureKnowledgeRevision;
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = outside, GlobalPosition = outside });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = outside, GlobalPosition = outside }); await Wait(.2);
        Check(!menu.HasHomeModal && state.Food == outsideFood && state.AdventureKnowledgeRevision == outsideKnowledge && canvas.MapOffset == camera, "Clicking the backdrop dismisses without moving the caravan");
        foreach (var path in new[] { SceneRouter.BountyScene, SceneRouter.ExpeditionScene, SceneRouter.ForgeScene, SceneRouter.CashShopScene, SceneRouter.LoginCalendarScene, SceneRouter.SeasonPassScene, SceneRouter.MultiplayerScene, SceneRouter.LanRaceScene, SceneRouter.ArenaScene, SceneRouter.GuildScene, SceneRouter.FriendsScene, SceneRouter.LeaderboardScene, SceneRouter.ProfileScene, SceneRouter.SkillTreeScene, SceneRouter.RaidScene, SceneRouter.EventScene, SceneRouter.LoadoutScene })
        {
            menu.OpenHomeDestination(path); await Wait(.35);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            AuditText("Modal / " + name); await Capture("modal-" + name);
            Check(GetTree().CurrentScene == menu && menu.HomeModalDestination == path, name + " remains above the existing map");
            menu.CloseHomeModal();
        }
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { baseline });
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
    }

    private async Task ReviewModalLaunches()
    {
        var state = GameState.Instance; state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu"); SceneRouter.Instance.GoToEndless(); await Wait(.3);
        var start = Walk(GetTree().CurrentScene).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == "Begin Endless March");
        await TapModal(start); await Wait(.8);
        Check(GetTree().CurrentScene.SceneFilePath == SceneRouter.BattleScene && state.CurrentBattleMode == BattleRunMode.Endless, "Starting endless from its modal enters the real endless battle");
        SceneRouter.Instance.GoToMainMenu(); await Wait(.8);
        Check(GetTree().CurrentScene is MainMenu && !((MapMenu)GetTree().CurrentScene).HasHomeModal, "Returning from battle restores the map home");
        state.PrepareCampaignBattle(); SceneRouter.Instance.GoToLoadout(); await Wait(.3);
        var food = state.Food; var cost = state.GetStageEntryFoodCost(state.SelectedStage);
        var deploy = Walk(GetTree().CurrentScene).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text.StartsWith("Deploy  ·"));
        Check(deploy.GetGlobalRect().End.Y <= GetTree().CurrentScene.GetViewport().GetVisibleRect().End.Y - 65, "The campaign Deploy button stays in the fixed action bar");
        await TapModal(deploy); await Wait(.8);
        Check(GetTree().CurrentScene.SceneFilePath == SceneRouter.BattleScene && state.CurrentBattleMode == BattleRunMode.Campaign && state.Food == food - cost, "Campaign preparation deploys from the modal with one entry charge");
        SceneRouter.Instance.GoToMainMenu(); await Wait(.8);
        var previous = GetTree().CurrentScene; SceneRouter.Instance.GoToSettings(); await Wait(.2); SceneRouter.Instance.ReloadHome(); await Wait(.8);
        Check(GetTree().CurrentScene is MainMenu && GetTree().CurrentScene != previous && !((MapMenu)GetTree().CurrentScene).HasHomeModal, "An account or restored save can reload the home independently of modal navigation");
    }

    private async Task TapModal(Button button)
    {
        for (var parent = button.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(button);
        await Wait(.05);
        var center = button.GetGlobalRect().GetCenter();
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        await Wait(.3);
    }
}
