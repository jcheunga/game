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

        await PressHint("Settings");
        var settings = Walk(menu).OfType<SettingsMenu>().Single();
        await TapModal(Walk(settings).OfType<Button>().Single(button => button.Text == "Developer"));
        var toggle = Walk(settings).OfType<Button>().Single(button => button.Name == "DeveloperModeToggle");
        var grants = Walk(settings).OfType<Button>().Where(button => button.Name.ToString().StartsWith("Developer") && button != toggle).ToArray();
        Check(grants.Length == 4 && grants.All(button => button.Disabled), "Developer tab shows four disabled top-ups until the mode is enabled");
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
            Check(menu.GetNode<RealmModal>("HomeModal").Content.GetGlobalRect().Encloses(button.GetGlobalRect()), grant.Name + ": action fits inside the modal");
            await TapModal(button); gold += grant.Gold; food += grant.Food;
            Check(state.Gold == gold && state.Food == food, grant.Name + ": one native click adds the exact amount");
        }
        Check(Walk(settings).OfType<Label>().Any(label => label.Text == $"Balance · {gold:N0}")
            && Walk(settings).OfType<Label>().Any(label => label.Text == $"Balance · {food:N0}"), "Developer balances refresh immediately");
        AuditText("Developer / on"); await Capture("developer-settings-on");
        menu.CloseHomeModal(); await Wait(.2);
        Check(panel.IsVisibleInTree() && canvas.MapOffset == camera, "Closing settings reveals the map top-ups and preserves the camera");
        await TapModal(Walk(panel).OfType<Button>().Single(button => button.Name == "DeveloperMapGold")); gold += 1000;
        await TapModal(Walk(panel).OfType<Button>().Single(button => button.Name == "DeveloperMapFood")); food += 100;
        Check(state.Gold == gold && state.Food == food, "Map top-ups each grant exactly once");
        var balances = Walk(menu.GetNode("HomeHud/Resources")).OfType<Label>().Select(label => label.Text).ToArray();
        Check(balances.Contains(gold.ToString("N0")) && balances.Contains($"{food}/{GameState.FoodRechargeCap}"), "Map balances update immediately after top-ups");
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
        await TapModal(Walk(settings).OfType<Button>().Single(button => button.Text == "Developer"));
        toggle = Walk(settings).OfType<Button>().Single(button => button.Name == "DeveloperModeToggle");
        await TapModal(toggle);
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
}
