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
        SceneRouter.Instance.GoToShop(); await Wait(.3);
        var unit = GameData.GetPlayerUnits().First();
        var stats = state.BuildPlayerUnitStats(unit);
        Check(Walk(menu).OfType<PanelContainer>().Any(control => control.IsVisibleInTree() && control.AccessibilityName == $"Health: {stats.MaxHealth:0}"), "Unit profile shows the actual trained health beside its icon");
        Check(!Walk(menu).OfType<Control>().Single(control => control.Name == "ExtraProfileDetails").Visible, "Unit traits and training start collapsed");
        var disclosure = Walk(menu).OfType<Button>().Single(button => button.Name == "ProfileDisclosure");
        var profileScroll = disclosure.GetParent();
        while (profileScroll is not ScrollContainer) profileScroll = profileScroll.GetParent();
        Check(((ScrollContainer)profileScroll).GetGlobalRect().Encloses(disclosure.GetGlobalRect()), "The resource bar leaves the unit profile disclosure fully visible");
        AuditText("Profile / fresh unit"); await Capture("01-unit-profile");
        var food = state.Food; var gold = state.Gold; var level = state.GetUnitLevel(unit.Id); var cost = state.GetUnitUpgradeCost(unit.Id);
        var upgrade = Walk(menu).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text.StartsWith("Upgrade ") && !button.Disabled);
        Check(upgrade.GetGlobalRect().End.Y < 630, "Unit training stays visible in the fixed action bar");
        await TapModal(upgrade);
        stats = state.BuildPlayerUnitStats(unit);
        Check(state.GetUnitLevel(unit.Id) == level + 1 && state.Gold == gold - cost
            && Walk(menu).OfType<PanelContainer>().Any(control => control.IsVisibleInTree() && control.AccessibilityName == $"Health: {stats.MaxHealth:0}"), "A native training click spends once and updates the displayed stats");
        CheckArmoryBalances(menu, "Unit training refreshes the visible resource balances");
        await Press("Traits & training");
        Check(Walk(menu).OfType<Control>().Single(control => control.Name == "ExtraProfileDetails").Visible, "The optional unit breakdown expands inside the same profile");
        var extraActions = Walk(menu.GetNode<RealmModal>("HomeModal")).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text.StartsWith("Upgrade "));
        Check(extraActions.GetGlobalRect().End.Y < 630, "Expanding training keeps the main action outside the scroller");
        AuditText("Profile / expanded unit"); await Capture("02-unit-training");
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
            Check(Walk(menu).OfType<GridContainer>().First(grid => grid.Name == "ProfileStats").GetGlobalRect().End.Y < 605, entry.DisplayName + ": core stats fit at maximum level");
        }
        await PressHint(unit.DisplayName); await Press("Traits & training");
        var doctrine = state.GetUnitDoctrineOptions(unit.Id).First();
        await Press("Choose " + doctrine.Title);
        Check(state.GetUnitDoctrineId(unit.Id) == doctrine.Id, "Doctrines remain selectable from the expanded profile");
        AuditText("Profile / doctrine"); await Capture("03-unit-doctrine");
        var promote = Walk(menu).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && button.Text.StartsWith("Promote") && !button.Disabled);
        if (promote != null)
        {
            await TapModal(promote);
            Check(state.IsUnitPromoted(unit.Id), "The fixed profile action still promotes eligible units");
            CheckArmoryBalances(menu, "Promotion refreshes the displayed gold and sigils");
        }
        menu.CloseHomeModal(); SceneRouter.Instance.GoToShop(1); await Wait(.3);
        foreach (var spell in GameData.GetPlayerSpells())
        {
            await PressHint(spell.DisplayName);
            var resolved = state.BuildSpellStats(spell);
            Check(Walk(menu).OfType<PanelContainer>().Any(tile => tile.IsVisibleInTree() && tile.AccessibilityName == $"Cooldown: {resolved.Cooldown:0.#}s")
                && Walk(menu).OfType<PanelContainer>().Any(tile => tile.IsVisibleInTree() && tile.AccessibilityName == $"Courage: {resolved.CourageCost}"), spell.DisplayName + ": profile uses the battle's resolved cost and cooldown");
            AuditText("Profile / " + spell.DisplayName);
            await Capture("spell-" + spell.EffectType);
        }
        var firstSpell = GameData.GetPlayerSpells().First(); await PressHint(firstSpell.DisplayName);
        gold = state.Gold; cost = state.GetSpellUpgradeCost(firstSpell.Id); level = state.GetSpellLevel(firstSpell.Id);
        upgrade = Walk(menu).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text.StartsWith("Upgrade Lv"));
        await TapModal(upgrade);
        var trained = state.BuildSpellStats(firstSpell);
        Check(state.GetSpellLevel(firstSpell.Id) == level + 1 && state.Gold == gold - cost
            && Walk(menu).OfType<PanelContainer>().Any(tile => tile.IsVisibleInTree() && tile.AccessibilityName == $"Damage: {trained.Power:0.#}"), "Spell training spends once and refreshes the real damage value");
        CheckArmoryBalances(menu, "Spell training refreshes the visible resource balances");
        await Press("Effects & training"); AuditText("Profile / expanded spell"); await Capture("04-spell-training");
        Check(state.Food == food && Walk(menu).OfType<MapPathCanvas>().Single().MapOffset == camera, "Profile browsing and training preserve the map and travel supplies");
        menu.CloseHomeModal();
        state.ToggleDeckSpell(firstSpell.Id, out _); state.PrepareCampaignBattle(); SceneRouter.Instance.GoToLoadout(); await Wait(.3);
        await Press("Details");
        Check(Walk(menu).OfType<ModelShowcase>().Any(), "Preparation opens the visual unit inspector");
        AuditText("Profile / unit inspector"); await Capture("05-unit-inspector");
        await Press("Close");
        SpellShowcase.Show(Walk(menu).OfType<LoadoutMenu>().Single(), firstSpell); await Wait(.3);
        Check(Walk(menu).OfType<SpellShowcase>().Any(), "Preparation can inspect a spell with the same visual stat cards");
        AuditText("Profile / spell inspector"); await Capture("06-spell-inspector"); await Press("Close");
        menu.CloseHomeModal();
        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit));
        GD.Print($"ARMORY_DETAILS_REVIEW_RESULT: {_failures} failures");
        await Open("MainMenu"); GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void CheckArmoryBalances(MapMenu menu, string message)
    {
        var state = GameState.Instance;
        var balances = Walk(menu).OfType<HFlowContainer>().Single(control => control.Name == "ResourceBalances");
        var expected = new[] { ("gold", state.Gold), ("food", state.Food), ("sigils", state.Sigils),
            ("tomes", state.Tomes), ("shards", state.RelicShards), ("essence", state.Essence) };
        Check(balances.IsVisibleInTree() && expected.All(resource => {
            var row = balances.GetNode<HBoxContainer>("Balance" + resource.Item1);
            return row.IsVisibleInTree() && balances.GetGlobalRect().Encloses(row.GetGlobalRect())
                && row.GetChild<Label>(1).Text == resource.Item2.ToString("N0")
                && row.GetChild<TextureRect>(0).Texture != null;
        }), message);
    }
}
