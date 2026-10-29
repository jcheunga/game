using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// --guides: the reading pages players open for help — the battle prep's encounter briefing, the battle menu and its
/// combat guide, the endless field guide and run history, a LAN board's briefing and the challenge code menu's
/// briefing and records — each captured and run through the text audit. Captures go to artifacts/guides
/// (artifacts/guides-WxH with --window).
/// </summary>
public partial class UiReviewSmoke
{
    private async Task ReviewGuides()
    {
        var window = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--window="))?["--window=".Length..];
        _output = ProjectSettings.GlobalizePath(window == null ? "res://artifacts/guides" : $"res://artifacts/guides-{window}");
        System.IO.Directory.CreateDirectory(_output);
        SeedConceptSave();
        var state = GameState.Instance;
        async Task Look(string name)
        {
            await Wait(.35); await Capture("guide-" + name); AuditText("Guides / " + name);
        }
        RoyalButton Find(Node root, string name) => Walk(root).OfType<RoyalButton>()
            .LastOrDefault(b => b.IsVisibleInTree() && (b.AccessibilityName == name || b.Text == name || b.TooltipText == name));

        // Battle prep on a stage that brings a new enemy, so the briefing shows its warning and counters.
        state.SetSelectedStage(4);
        await Open("LoadoutMenu");
        var loadout = GetTree().CurrentScene;
        EncounterBriefing.Show(loadout, GameData.GetStage(4));
        await Look("briefing");
        loadout.GetNode("EncounterBriefing").Free();
        state.SetSelectedStage(10);
        EncounterBriefing.Show(loadout, GameData.GetStage(10));
        await Look("briefing-boss");
        loadout.GetNode("EncounterBriefing").Free();

        await Open("MainMenu");
        var home = (MapMenu)GetTree().CurrentScene;
        home.OpenHomeDestination(SceneRouter.EndlessScene); await Wait(.5);
        Find(home, "Field Guide")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Look("endless-field-guide");
        CloseReading();
        Find(home, "Run History")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Look("endless-run-history");
        CloseReading();

        home.CloseHomeModal(); home.OpenHomeDestination(SceneRouter.LanRaceScene); await Wait(.5);
        Find(home, "Briefing")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Look("lan-briefing");
        CloseReading();

        home.CloseHomeModal(); home.OpenHomeDestination(SceneRouter.MultiplayerScene); await Wait(.5);
        var challenges = Walk(home).OfType<MultiplayerMenu>().FirstOrDefault();
        foreach (var (id, name) in new[] { (3L, "challenge-briefing"), (4L, "challenge-records") })
        {
            if (challenges == null) break;
            typeof(MultiplayerMenu).GetMethod("ShowCodeMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(challenges, null);
            var popup = Walk(challenges).OfType<PopupMenu>().Last();
            popup.EmitSignal(PopupMenu.SignalName.IdPressed, id); popup.Hide();
            await Look(name);
            CloseReading();
        }
        home.CloseHomeModal();

        state.SetSelectedStage(4); state.PrepareCampaignBattle();
        await Open("Battle");
        var battle = GetTree().CurrentScene;
        await PressHint("Battle menu [Escape]");
        await Look("battle-menu");
        await Press("Battle guide");
        await Look("combat-guide");
        foreach (var tab in new[] { "Signs", "Caravan spell" })
        {
            await PressTab(tab);
            await Look("combat-guide-" + tab.Split(' ')[0].ToLowerInvariant());
        }
        // With high contrast on, the chips the body otherwise shows are listed too.
        state.SetHighContrast(true); await PressTab("Signs");
        await Look("combat-guide-signs-contrast");
        state.SetHighContrast(false);
        battle.GetNode("CombatGuide").Free(); await Wait(.1);
        await Press("Resume");
        GD.Print($"GUIDES_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }

    /// <summary>Closes whichever reading page is open: a stacked inspector layer or a dialog window.</summary>
    private void CloseReading()
    {
        foreach (var node in Walk(GetTree().Root).Where(n => n is AcceptDialog || (n is RealmModal r && r.Name == "InspectorModal")).ToArray())
            (node is RealmModal ? node.GetParent() : node).Free();
    }
}
