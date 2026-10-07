using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewArmoryDetails()
    {
        _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/armory-details/small" : "res://artifacts/armory-details/desktop");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance; state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        var camera = Walk(menu).OfType<MapPathCanvas>().Single().MapOffset;
        // The royal profile names each stat tile by its hint ("Health: 53") and each action by what it does.
        // (The audio director moves hover hints into accessible names for touch play.)
        bool Shows(string stat) => Walk(menu).OfType<Control>().Any(control => control.IsVisibleInTree() && (control.TooltipText == stat || control.AccessibilityName == stat));
        RoyalButton Action(string name) => Walk(menu).OfType<RoyalButton>().FirstOrDefault(button => button.IsVisibleInTree() && !button.Disabled
            && (button.AccessibilityName ?? "").StartsWith(name));
        SceneRouter.Instance.GoToShop(); await Wait(.3);
        var unit = GameData.GetPlayerUnits().First(); await PressHint(unit.DisplayName);
        var stats = state.BuildPlayerUnitStats(unit);
        Check(Shows($"Health: {stats.MaxHealth:0}"), "Unit profile shows the actual trained health beside its icon");
        AuditText("Profile / fresh unit"); await Capture("01-unit-profile");
        var food = state.Food; var gold = state.Gold; var level = state.GetUnitLevel(unit.Id); var cost = state.GetUnitUpgradeCost(unit.Id);
        var upgrade = Action("Upgrade");
        Check(upgrade != null && GetViewport().GetVisibleRect().Encloses(upgrade.GetGlobalRect()), "Unit training stays visible in the profile's action bar");
        await TapModal(upgrade);
        stats = state.BuildPlayerUnitStats(unit);
        Check(state.GetUnitLevel(unit.Id) == level + 1 && state.Gold == gold - cost && Shows($"Health: {stats.MaxHealth:0}"),
            "A native training click spends once and updates the displayed stats");
        // The role chip beside the name is a label; the profile shows everything there is.
        Check(!Walk(menu).OfType<BaseButton>().Any(button => button.IsVisibleInTree() && button.AccessibilityName == "Traits & training"),
            "The role chip opens no extra breakdown");
        menu.CloseHomeModal();
        var fixture = state.BuildSaveData(); fixture.Gold = 100000; fixture.Sigils = 100; fixture.HighestUnlockedStage = state.MaxStage;
        fixture.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Select(entry => entry.Id).ToArray();
        fixture.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Select(entry => entry.Id).ToArray();
        fixture.UnitLevels = GameData.GetPlayerUnits().ToDictionary(entry => entry.Id, _ => state.MaxUnitLevel);
        fixture.SpellLevels = GameData.GetPlayerSpells().ToDictionary(entry => entry.Id, _ => 2);
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        SceneRouter.Instance.GoToShop(); await Wait(.3);
        foreach (var entry in GameData.GetPlayerUnits())
        {
            await PressHint(entry.DisplayName);
            AuditText("Profile / " + entry.DisplayName);
            var trained = state.BuildPlayerUnitStats(entry);
            Check(Shows($"Health: {trained.MaxHealth:0}") && Shows($"Damage: {trained.AttackDamage:0.#}"), entry.DisplayName + ": core stats show at maximum level");
        }
        await PressHint(unit.DisplayName);
        if (Action("Promote") is { } promote)
        {
            await TapModal(promote);
            Check(state.IsUnitPromoted(unit.Id), "The profile action still promotes eligible units");
        }
        menu.CloseHomeModal(); SceneRouter.Instance.GoToShop(1); await Wait(.3);
        foreach (var spell in GameData.GetPlayerSpells())
        {
            await PressHint(spell.DisplayName);
            var resolved = state.BuildSpellStats(spell);
            Check(Shows($"Cooldown: {resolved.Cooldown:0.#}s") && Shows($"Mana: {resolved.ManaCost}"), spell.DisplayName + ": profile uses the battle's resolved cost and cooldown");
            AuditText("Profile / " + spell.DisplayName);
            await Capture("spell-" + spell.EffectType);
        }
        var firstSpell = GameData.GetPlayerSpells().First(); await PressHint(firstSpell.DisplayName);
        gold = state.Gold; cost = state.GetSpellUpgradeCost(firstSpell.Id); level = state.GetSpellLevel(firstSpell.Id);
        await TapModal(Action("Upgrade"));
        var scribed = state.BuildSpellStats(firstSpell);
        Check(state.GetSpellLevel(firstSpell.Id) == level + 1 && state.Gold == gold - cost && Shows($"Damage: {scribed.Power:0.#}"),
            "Spell training spends once and refreshes the real damage value");
        AuditText("Profile / spell training"); await Capture("04-spell-training");
        Check(state.Food == food && Walk(menu).OfType<MapPathCanvas>().Single().MapOffset == camera, "Profile browsing and training preserve the map and travel supplies");
        menu.CloseHomeModal();
        state.ToggleDeckSpell(firstSpell.Id, out _); state.PrepareCampaignBattle(); SceneRouter.Instance.GoToLoadout(); await Wait(.3);
        var view = Walk(menu).OfType<Button>().First(button => button.IsVisibleInTree() && (button.AccessibilityName ?? "").StartsWith("View "));
        view.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.6);
        Check(Walk(menu).OfType<ModelShowcase>().Any(), "Preparation opens the visual unit inspector");
        AuditText("Profile / unit inspector"); await Capture("05-unit-inspector");
        Check(Walk(menu).OfType<RealmModal>().Count() == 2, "The unit inspector opens as a modal above preparation");
        await PressHint("Close details");
        SpellShowcase.Show(Walk(menu).OfType<LoadoutMenu>().Single(), firstSpell); await Wait(.3);
        Check(Walk(menu).OfType<SpellShowcase>().Any(), "Preparation can inspect a spell with the same visual stat cards");
        AuditText("Profile / spell inspector"); await Capture("06-spell-inspector");
        Check(Walk(menu).OfType<RealmModal>().Count() == 2, "The spell inspector opens as a modal above preparation");
        await PressHint("Close details");
        Check(!Walk(menu).OfType<SpellShowcase>().Any(), "Closing the spell inspector returns to preparation");
        menu.CloseHomeModal();
        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit));
        GD.Print($"ARMORY_DETAILS_REVIEW_RESULT: {_failures} failures");
        await Open("MainMenu"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
