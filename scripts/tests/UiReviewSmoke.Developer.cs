using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewDeveloperMode(MapMenu menu)
    {
        var state = GameState.Instance;
        var baseline = state.BuildSaveData();
        var canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var camera = canvas.MapOffset;
        var knowledge = state.AdventureKnowledgeRevision;
        var purchases = state.TotalPurchaseCount;
        var achievements = state.GetUnlockedAchievementCount();
        var stage = state.HighestUnlockedStage;
        var panel = menu.GetNode<PanelContainer>("HomeHud/DeveloperSupplies");
        Check(!state.DeveloperModeEnabled && !panel.IsVisibleInTree(), "Developer mode starts off and leaves the map clear");
        Check(!state.TryAddDeveloperResources(1000, 100) && state.Gold == baseline.Gold && state.Food == baseline.Food,
            "Resource grants are rejected while developer mode is off");

        // Developer tools open from their row in settings, in an inspector over the settings page.
        async Task<RealmModal> OpenTools(SettingsMenu page)
        {
            Button Row() => Walk(page).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && button.AccessibilityName == "Developer tools");
            foreach (var tab in new[] { "Sound", "Gameplay", "Online", "Account" })
            {
                if (Row() != null) break;
                await TapModal(Walk(page).OfType<Button>().First(button => button.IsVisibleInTree() && button.AccessibilityName == tab));
            }
            await TapModal(Row() ?? throw new InvalidOperationException("Settings has no Developer tools row"));
            return Walk(page).OfType<RealmModal>().Single(modal => modal.IsVisibleInTree());
        }
        await PressHint("Settings");
        var settings = Walk(menu).OfType<SettingsMenu>().Single();
        var tools = await OpenTools(settings);
        var toggle = Walk(settings).OfType<Button>().Single(button => button.Name == "DeveloperModeToggle");
        var grants = Walk(settings).OfType<Button>().Where(button => button.Name.ToString().StartsWith("Developer") && button != toggle).ToArray();
        Check(grants.Length >= 20 && grants.All(button => button.Disabled), "Developer tools stay disabled until the mode is enabled");
        AuditText("Developer / off"); await Capture("developer-settings-off");
        await TapModal(toggle);
        Check(state.DeveloperModeEnabled && toggle.Text == "Developer mode: On" && grants.All(button => !button.Disabled),
            "A native toggle enables the resource controls");
        var gold = baseline.Gold;
        var food = baseline.Food;
        foreach (var grant in new[] { (Name: "DeveloperGold1000", Gold: 1000, Food: 0), (Name: "DeveloperGold10000", Gold: 10000, Food: 0),
            (Name: "DeveloperFood10", Gold: 0, Food: 10), (Name: "DeveloperFood100", Gold: 0, Food: 100) })
        {
            var button = grants.Single(button => button.Name == grant.Name);
            await TapModal(button); gold += grant.Gold; food += grant.Food;
            Check(tools.Content.GetGlobalRect().Encloses(button.GetGlobalRect()), grant.Name + ": action fits inside the modal once scrolled to");
            Check(state.Gold == gold && state.Food == food, grant.Name + ": one native click adds the exact amount");
        }
        Check(Walk(settings).OfType<Label>().Any(label => label.Text == $"Gold · {gold:N0}")
            && Walk(settings).OfType<Label>().Any(label => label.Text == $"Food · {food:N0}"), "Developer balances refresh immediately");
        AuditText("Developer / on"); await Capture("developer-settings-on");
        menu.CloseHomeModal(); await Wait(.2);
        Check(panel.IsVisibleInTree() && canvas.MapOffset == camera, "Closing settings reveals the map top-ups and preserves the camera");
        await TapModal(Walk(panel).OfType<Button>().Single(button => button.Name == "DeveloperMapGold")); gold += 1000;
        await TapModal(Walk(panel).OfType<Button>().Single(button => button.Name == "DeveloperMapFood")); food += 100;
        Check(state.Gold == gold && state.Food == food, "Map top-ups each grant exactly once");
        var balances = Walk(menu.GetNode("HomeHud/Resources")).OfType<RoyalLabel>().Select(label => label.Text).ToArray();
        Check(balances.Contains(gold.ToString("N0")) && balances.Contains($"{food} / {GameState.FoodRechargeCap}"), "Map balances update immediately after top-ups");
        Check(state.TotalPurchaseCount == purchases && state.GetUnlockedAchievementCount() == achievements
            && state.HighestUnlockedStage == stage && canvas.MapOffset == camera && state.AdventureKnowledgeRevision == knowledge,
            "Testing supplies leave purchases, achievements, progression, fog and camera unchanged");
        Check(!Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice"), "Top-ups do not create a bottom message popup");
        AuditText("Developer / map supplies"); await Capture("developer-map-supplies");
        Check(!state.TryAddDeveloperResources(-1, 10) && !state.TryAddDeveloperResources(10, -1)
            && !state.TryAddDeveloperResources(0, 0) && state.Gold == gold && state.Food == food,
            "Invalid grants do not alter either resource");
        var execute = typeof(DebugConsole).GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic)!;
        execute.Invoke(null, new object[] { "gold 7" }); execute.Invoke(null, new object[] { "food 9" });
        gold += 7; food += 9;
        Check(state.Gold == gold && state.Food == food && state.TotalPurchaseCount == purchases,
            "Console grants use developer supplies without counting as purchases");
        Check(SaveSystem.Instance.TryLoad(out var saved) && saved.DeveloperModeEnabled && saved.Gold == gold && saved.Food == food,
            "Developer mode and both balances persist to disk");
        var apply = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state.SetDeveloperMode(false);
        apply.Invoke(state, new object[] { saved });
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
        Check(state.DeveloperModeEnabled && state.Gold == gold && state.Food == food && panel.IsVisibleInTree(),
            "Reloading a saved game restores the enabled mode and supplies");
        await PressHint("Settings");
        settings = Walk(menu).OfType<SettingsMenu>().Single();
        await OpenTools(settings);
        await ReviewDeveloperTools(settings);
        gold = state.Gold; food = state.Food;
        toggle = Walk(settings).OfType<Button>().Single(button => button.Name == "DeveloperModeToggle");
        await TapModal(toggle);
        Check(state.ActiveWaveVersionId == WaveVersions.StandardId && GameData.GetStage(1).Waves == _standardStageOneWaves
            && state.BattleSpeedScale == 1f, "Turning the mode off restores the standard waves and normal battle speed");
        Check(!state.DeveloperModeEnabled && Walk(settings).OfType<Button>().Where(button => button.Name.ToString().StartsWith("Developer") && button != toggle)
            .All(button => button.Disabled), "Turning developer mode off disables every modal grant");
        Check(!state.TryAddDeveloperResources(1000, 100) && state.Gold == gold && state.Food == food, "Turning the mode off rejects resource grants again");
        menu.CloseHomeModal(); await Wait(.2);
        Check(!panel.IsVisibleInTree(), "Turning the mode off removes the map top-ups");
        Check(SaveSystem.Instance.TryLoad(out var disabledSave) && !disabledSave.DeveloperModeEnabled, "The disabled mode also persists");

        var overflow = state.BuildSaveData(); overflow.DeveloperModeEnabled = true;
        overflow.Gold = int.MaxValue - 500; overflow.Food = 100;
        apply.Invoke(state, new object[] { overflow });
        Check(!state.TryAddDeveloperResources(1000, 10) && !state.TryAddDeveloperResources(0, int.MaxValue)
            && state.Gold == overflow.Gold && state.Food == overflow.Food, "Overflowing grants are rejected atomically");
        apply.Invoke(state, new object[] { baseline });
        state.SetDeveloperMode(baseline.DeveloperModeEnabled);
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
        await Wait(.2);
    }

    private StageWaveDefinition[] _standardStageOneWaves;

    /// <summary>Wave versions, battle speed, player level, campaign shortcuts and warband tools, each by one tap.</summary>
    private async Task ReviewDeveloperTools(SettingsMenu settings)
    {
        var state = GameState.Instance;
        Button Tool(string name) => Walk(settings).OfType<Button>().Single(button => button.Name == name);
        _standardStageOneWaves = GameData.GetStage(1).Waves;
        Check(WaveVersions.All.Select(version => version.Id).SequenceEqual(new[] { "standard", "horde", "regional" })
            && WaveVersions.All.All(version => Walk(settings).OfType<Button>().Any(button => button.Text == version.Name)),
            "Developer tools offer the standard, horde and regional wave versions");
        await TapModal(Tool("DeveloperWavesHorde"));
        var horde = GameData.GetStage(1).Waves;
        Check(state.ActiveWaveVersionId == "horde" && horde != _standardStageOneWaves && Tool("DeveloperWavesHorde").ButtonPressed
            && !Tool("DeveloperWavesStandard").ButtonPressed && horde.Sum(wave => wave.Entries.Sum(entry => entry.Count)) > _standardStageOneWaves.Sum(wave => wave.Entries.Sum(entry => entry.Count)),
            "Choosing Horde puts its larger waves on the campaign and lights its choice");
        await TapModal(Tool("DeveloperWavesRegional"));
        Check(state.ActiveWaveVersionId == "regional" && GameData.GetStage(8).Waves.SelectMany(wave => wave.Entries).Any(entry => entry.UnitId == "enemy_highwayman"),
            "Regional waves bring the zone's own enemies");
        await TapModal(Tool("DeveloperSpeed4"));
        Check(state.BattleSpeedScale == 4f && Tool("DeveloperSpeed4").ButtonPressed, "Battle speed 4× is chosen and lit");
        var level = state.PlayerLevel;
        await TapModal(Tool("DeveloperLevel1"));
        Check(state.PlayerLevel == level + 1, "+1 level raises the player level by exactly one");
        var next = state.NextUnbeatenStage;
        await TapModal(Tool("DeveloperWinNext"));
        Check(state.GetStageStars(next) == 3 && state.NextUnbeatenStage > next, $"Win next stage wins stage {next} with three stars");
        var boss = state.DeveloperZoneEndStage();
        await TapModal(Tool("DeveloperWinZone"));
        var nextZone = GameData.GetStage(Math.Min(state.MaxStage, boss + 1)).MapId;
        Check(Enumerable.Range(1, boss).All(stage => state.GetStageStars(stage) > 0) && state.GetStageStars(boss) == 3 && state.IsAdventureZoneUnlocked(nextZone),
            $"Win to zone boss clears through stage {boss}, boss included, and opens the next zone");
        await TapModal(Tool("DeveloperOpenZones"));
        Check(GameData.Stages.Select(stage => stage.MapId).Distinct().All(state.IsAdventureZoneUnlocked) && state.GetStageStars(11) == 0
            && state.IsAdventureSiteDiscovered(AdventureMapCatalog.Leader(95).Id), "Open every zone reveals every zone's stages without winning them");
        await TapModal(Tool("DeveloperRecruitAll"));
        Check(GameData.GetPlayerUnits().Where(unit => !unit.LegacyRecruit).All(unit => state.IsUnitOwned(unit.Id))
            && GameData.GetPlayerSpells().All(spell => state.IsSpellOwned(spell.Id)), "Recruit everything owns every recruit, spell and tool");
        await TapModal(Tool("DeveloperTrainAll"));
        Check(state.GetOwnedPlayerUnitIds().All(id => state.GetUnitLevel(id) >= Math.Min(state.MaxUnitLevel, state.UnitLevelCap)),
            "Train to level cap trains every owned unit");
        var essence = state.Essence;
        await TapModal(Tool("DeveloperEssence100"));
        Check(state.Essence == essence + 100, "Essence grants add the exact amount");
        Check(Walk(settings).OfType<Label>().Any(label => label.Name == "DeveloperStatus" && label.Text.Length > 0), "Each tool reports what it did");
        AuditText("Developer / tools"); await Capture("developer-settings-tools");
    }

    /// <summary>A battle under developer mode: its speed and wave version, and the pause menu's tools.</summary>
    private async Task ReviewDeveloperBattle()
    {
        var state = GameState.Instance;
        var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        T Read<T>(BattleController battle, string field) => (T)typeof(BattleController).GetField(field, hidden)!.GetValue(battle)!;
        var baseline = state.BuildSaveData();
        state.SetDeveloperMode(true);
        state.SetDeveloperWaveVersion("horde");
        state.SetDeveloperBattleSpeed(4f);
        state.SetSelectedStage(1);
        state.PrepareCampaignBattle(); await Open("Battle"); await Wait(1.5);
        var battle = (BattleController)GetTree().CurrentScene;
        var horde = WaveVersions.Find("horde")!.Zones[0].Stages[0].Waves;
        Check(Engine.TimeScale == 4f, "A campaign battle starts at the developer speed");
        Check(Read<StageDefinition>(battle, "_stageData").Waves.SequenceEqual(horde), "The battle plays the chosen wave version");
        typeof(BattleController).GetMethod("TogglePause", hidden)!.Invoke(battle, null); await Wait(.3);
        Button Tool(string name) => Walk(battle).OfType<Button>().Single(button => button.Name == name && button.IsVisibleInTree());
        var names = new[] { "DeveloperWinBattle", "DeveloperLoseBattle", "DeveloperFillResources", "DeveloperBattleSpeed" };
        Check(names.All(name => Walk(battle).OfType<Button>().Any(button => button.Name == name && button.IsVisibleInTree())), "The pause menu shows the developer battle tools");
        AuditText("Developer / battle pause"); await Capture("developer-battle-pause");
        await TapModal(Tool("DeveloperBattleSpeed"));
        Check(state.DeveloperBattleSpeed == 8f && Engine.TimeScale == 8f && Tool("DeveloperBattleSpeed").Text == "Speed 8×", "The speed button steps 4× to 8× and applies it");
        await TapModal(Tool("DeveloperFillResources"));
        Check(Read<float>(battle, "_courage") == Read<float>(battle, "_maxCourage") && Read<float>(battle, "_mana") == Read<float>(battle, "_maxMana"),
            "Fill courage and mana tops both bars up");
        await TapModal(Tool("DeveloperWinBattle")); await Wait(1);
        Check(Read<bool>(battle, "_battleEnded") && !Read<bool>(battle, "_battlePaused") && state.GetStageStars(1) > 0 && Engine.TimeScale == 1f,
            "Win battle ends the battle as a victory and shows the result at normal speed");
        await Capture("developer-battle-won");
        typeof(GameState).GetMethod("ApplySavedData", hidden)!.Invoke(state, new object[] { baseline });
        state.SetDeveloperMode(baseline.DeveloperModeEnabled);
    }
}
