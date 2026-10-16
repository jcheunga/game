using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Developer tools (debug builds): the mode switch, then enemy wave versions, supplies, player level, campaign
/// shortcuts, warband and battle speed. Every tool is disabled while developer mode is off.
/// </summary>
public partial class SettingsMenu
{
    private Button _developerModeButton;
    private Label _developerStatus, _developerWaveNote, _developerWaveFile, _developerLevel, _developerCampaign;
    private readonly List<Button> _developerGrantButtons = new();
    private readonly Dictionary<string, Label> _developerBalances = new();
    private readonly Dictionary<string, Button> _developerWaveButtons = new();
    private readonly Dictionary<float, Button> _developerSpeedButtons = new();
    private Button _developerWinZone;
    private string _developerMessage = "";

    private void BuildDeveloperPage(VBoxContainer page)
    {
        _developerGrantButtons.Clear(); _developerBalances.Clear(); _developerWaveButtons.Clear(); _developerSpeedButtons.Clear();
        _developerModeButton = RealmUi.Button("gear", "Developer mode: Off", () =>
        {
            GameState.Instance.SetDeveloperMode(!GameState.Instance.DeveloperModeEnabled);
            _developerMessage = "";
            RefreshDeveloperPage();
        });
        _developerModeButton.Name = "DeveloperModeToggle";
        page.AddChild(_developerModeButton);
        _developerStatus = RealmUi.Label("", 18, true);
        _developerStatus.Name = "DeveloperStatus";
        page.AddChild(_developerStatus);

        page.AddChild(RealmUi.SectionTitle("Enemy waves"));
        var waves = DeveloperChoices(page);
        var waveGroup = new ButtonGroup();
        foreach (var version in WaveVersions.All)
        {
            var id = version.Id;
            _developerWaveButtons[id] = Choice(waves, waveGroup, "DeveloperWaves" + Capitalize(id), version.Name, () => GameState.Instance.SetDeveloperWaveVersion(id));
        }
        _developerWaveNote = RealmUi.Label("", 18, true);
        page.AddChild(_developerWaveNote);
        _developerWaveFile = RealmUi.Label("", 18, true);
        page.AddChild(_developerWaveFile);

        page.AddChild(RealmUi.SectionTitle("Supplies"));
        SupplyRow(page, "gold", "Gold", ("DeveloperGold1000", "+1,000", () => GameState.Instance.TryAddDeveloperResources(1000, 0)),
            ("DeveloperGold10000", "+10,000", () => GameState.Instance.TryAddDeveloperResources(10000, 0)));
        SupplyRow(page, "food", "Food", ("DeveloperFood10", "+10", () => GameState.Instance.TryAddDeveloperResources(0, 10)),
            ("DeveloperFood100", "+100", () => GameState.Instance.TryAddDeveloperResources(0, 100)));
        SupplyRow(page, "crystal", "Essence", ("DeveloperEssence100", "+100", () => GameState.Instance.TryAddDeveloperCurrency("essence", 100)),
            ("DeveloperEssence1000", "+1,000", () => GameState.Instance.TryAddDeveloperCurrency("essence", 1000)));
        SupplyRow(page, "hammer", "Relic shards", ("DeveloperShards100", "+100", () => GameState.Instance.TryAddDeveloperCurrency("shards", 100)),
            ("DeveloperShards1000", "+1,000", () => GameState.Instance.TryAddDeveloperCurrency("shards", 1000)));
        SupplyRow(page, "star", "Sigils", ("DeveloperSigils10", "+10", () => GameState.Instance.TryAddDeveloperCurrency("sigils", 10)),
            ("DeveloperSigils100", "+100", () => GameState.Instance.TryAddDeveloperCurrency("sigils", 100)));

        page.AddChild(RealmUi.SectionTitle("Player level"));
        _developerLevel = RealmUi.Label("", 18, true);
        page.AddChild(_developerLevel);
        var levels = DeveloperRow(page);
        Action(levels, "DeveloperLevel1", "plus", "+1 level", () => Report(GameState.Instance.TryDeveloperLevelUp(1, out var m), m));
        Action(levels, "DeveloperLevel10", "plus", "+10 levels", () => Report(GameState.Instance.TryDeveloperLevelUp(10, out var m), m));
        Action(levels, "DeveloperLevelMax", "crown", "Max level", () => Report(GameState.Instance.TryDeveloperLevelUp(PlayerLevelCatalog.MaxLevel, out var m), m));

        page.AddChild(RealmUi.SectionTitle("Campaign"));
        _developerCampaign = RealmUi.Label("", 18, true);
        page.AddChild(_developerCampaign);
        var campaign = DeveloperRow(page);
        Action(campaign, "DeveloperWinNext", "sword", "Win next stage",
            () => Report(GameState.Instance.TryDeveloperWinStages(GameState.Instance.NextUnbeatenStage, out var m), m));
        _developerWinZone = Action(campaign, "DeveloperWinZone", "flag", "Win to zone boss",
            () => Report(GameState.Instance.TryDeveloperWinStages(GameState.Instance.DeveloperZoneEndStage(), out var m), m));
        Action(campaign, "DeveloperWinAll", "crown", "Win every stage",
            () => Report(GameState.Instance.TryDeveloperWinStages(GameState.Instance.MaxStage, out var m), m));
        var map = DeveloperRow(page);
        Action(map, "DeveloperOpenZones", "map", "Open every zone", () => Report(GameState.Instance.TryDeveloperOpenAllZones(out var m), m));

        page.AddChild(RealmUi.SectionTitle("Warband"));
        var warband = DeveloperRow(page);
        Action(warband, "DeveloperRecruitAll", "people", "Recruit everything", () => Report(GameState.Instance.TryDeveloperUnlockWarband(out var m), m));
        Action(warband, "DeveloperTrainAll", "hammer", "Train to level cap", () => Report(GameState.Instance.TryDeveloperTrainWarband(out var m), m));

        page.AddChild(RealmUi.SectionTitle("Battle and time"));
        var speeds = DeveloperChoices(page);
        var speedGroup = new ButtonGroup();
        foreach (var speed in GameState.DeveloperBattleSpeeds)
        {
            var value = speed;
            _developerSpeedButtons[value] = Choice(speeds, speedGroup, $"DeveloperSpeed{value:0}", $"Speed {value:0}×", () => GameState.Instance.SetDeveloperBattleSpeed(value));
        }
        var time = DeveloperRow(page);
        Action(time, "DeveloperSkip1h", "clock", "Skip 1 hour", () => Report(GameState.Instance.TryDeveloperSkipTime(3600, out var m), m));
        Action(time, "DeveloperSkip8h", "clock", "Skip 8 hours", () => Report(GameState.Instance.TryDeveloperSkipTime(8 * 3600, out var m), m));
        page.AddChild(RealmUi.Label("Pause a battle for win, lose, full courage and mana, and speed buttons.", 18, true));
    }

    private static string Capitalize(string id) => id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..];

    private static HBoxContainer DeveloperRow(VBoxContainer page)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        page.AddChild(row);
        return row;
    }

    /// <summary>A tab-style row where the selected choice is lit.</summary>
    private static HBoxContainer DeveloperChoices(VBoxContainer page)
    {
        var row = DeveloperRow(page);
        row.SetMeta("realm_tabs", true);
        return row;
    }

    private Button Action(HBoxContainer row, string name, string icon, string text, Action run)
    {
        var button = RealmUi.Button(icon, text, () => { run(); RefreshDeveloperPage(); });
        button.Name = name;
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _developerGrantButtons.Add(button);
        row.AddChild(button);
        return button;
    }

    private Button Choice(HBoxContainer row, ButtonGroup group, string name, string text, Action run)
    {
        var button = Action(row, name, "check", text, run);
        button.Icon = null;
        button.ToggleMode = true;
        button.ButtonGroup = group;
        return button;
    }

    private void SupplyRow(VBoxContainer page, string icon, string title, params (string Name, string Text, Func<bool> Grant)[] grants)
    {
        var row = DeveloperRow(page);
        var balance = RealmUi.Label("", 20);
        balance.SizeFlagsStretchRatio = 1.4f;
        balance.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(balance);
        _developerBalances[title] = balance;
        foreach (var grant in grants)
            Action(row, grant.Name, icon, $"{grant.Text} {title.ToLowerInvariant()}", () => grant.Grant());
    }

    private void Report(bool done, string message)
    {
        _developerMessage = message ?? "";
        if (!done && _developerMessage.Length == 0) _developerMessage = "Nothing changed.";
    }

    private void RefreshDeveloperPage()
    {
        // The tools live in an inspector that may have been closed since.
        if (!IsInstanceValid(_developerModeButton)) return;
        var state = GameState.Instance;
        var on = state.DeveloperToolsActive;
        _developerModeButton.Text = on ? "Developer mode: On" : "Developer mode: Off";
        _developerModeButton.TooltipText = _developerModeButton.Text;
        _developerStatus.Text = !on ? "Turn developer mode on to use these tools. Waves and speed return to normal while it is off."
            : _developerMessage.Length > 0 ? _developerMessage : "Changes save at once. New waves apply from the next battle.";

        var wave = WaveVersions.Find(state.DeveloperWaveVersionId) ?? WaveVersions.Active;
        foreach (var (id, button) in _developerWaveButtons) button.SetPressedNoSignal(id == wave.Id);
        _developerWaveNote.Text = wave.Description;
        _developerWaveFile.Text = wave.Id == WaveVersions.StandardId
            ? $"Edit {wave.Path} · {state.MaxStage} stages"
            : $"Edit {wave.Path} · {wave.StageCount} stages · missing stages keep standard waves";

        _developerBalances["Gold"].Text = $"Gold · {state.Gold:N0}";
        _developerBalances["Food"].Text = $"Food · {state.Food:N0}";
        _developerBalances["Essence"].Text = $"Essence · {state.Essence:N0}";
        _developerBalances["Relic shards"].Text = $"Relic shards · {state.RelicShards:N0}";
        _developerBalances["Sigils"].Text = $"Sigils · {state.Sigils:N0}";

        _developerLevel.Text = state.IsMaxPlayerLevel
            ? $"Level {state.PlayerLevel} · top level · {state.ClaimableLevelRewardCount} rewards to claim"
            : $"Level {state.PlayerLevel} · {state.PlayerXPIntoLevel:N0} / {state.PlayerXPForNextLevel:N0} XP · {state.ClaimableLevelRewardCount} rewards to claim";

        var next = state.NextUnbeatenStage;
        var done = next > state.MaxStage;
        var zoneEnd = done ? state.MaxStage : state.DeveloperZoneEndStage();
        _developerCampaign.Text = done ? $"All {state.MaxStage} stages won · {state.TotalStarsEarned} stars"
            : $"Next unbeaten · stage {next} · {GameData.GetStage(next).MapName}";
        _developerWinZone.Text = done || zoneEnd == next ? "Win zone boss" : $"Win to stage {zoneEnd}";

        foreach (var (speed, button) in _developerSpeedButtons) button.SetPressedNoSignal(speed == state.DeveloperBattleSpeed);
        foreach (var button in _developerGrantButtons) button.Disabled = !on;
        foreach (var name in new[] { "DeveloperWinNext", "DeveloperWinZone", "DeveloperWinAll" })
            if (_developerGrantButtons.FirstOrDefault(button => button.Name == name) is { } win) win.Disabled = !on || done;
    }
}
