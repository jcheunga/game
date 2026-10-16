using Godot;

/// <summary>
/// Developer mode's battle tools, under the pause menu: settle the battle, fill courage and mana, and change the
/// battle speed. Shared LAN and online rooms keep everyone on the same clock, so they get none of these.
/// </summary>
public partial class BattleController
{
    private Button _developerSpeedButton;

    private bool DeveloperBattleToolsAvailable => GameState.Instance.DeveloperToolsActive && !IsLanRaceMode && !IsOnlineRoomMode;

    /// <summary>Sets the developer battle speed. Without developer mode the time scale is left to whoever set it.</summary>
    private void ApplyDeveloperBattleSpeed()
    {
        if (DeveloperBattleToolsAvailable) Engine.TimeScale = GameState.Instance.BattleSpeedScale;
    }

    private void BuildDeveloperBattleTools(VBoxContainer stack)
    {
        if (!DeveloperBattleToolsAvailable) return;
        stack.AddChild(RealmUi.SectionTitle("Developer", 18));
        HBoxContainer Row()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            stack.AddChild(row);
            return row;
        }
        Button Add(HBoxContainer row, string name, string icon, string text, System.Action action)
        {
            var button = RealmUi.Button(icon, text, action);
            button.Name = name;
            button.CustomMinimumSize = new Vector2(0, 52);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            ModalUi.StyleButton(button);
            row.AddChild(button);
            return button;
        }
        var settle = Row();
        // Endless and boss rush runs have no stronghold to break; they end only when the wagon falls.
        if (!IsEndlessMode && !IsBossRushMode) Add(settle, "DeveloperWinBattle", "crown", "Win battle", () => DeveloperSettleBattle(true));
        Add(settle, "DeveloperLoseBattle", "flag", IsEndlessMode || IsBossRushMode ? "End run" : "Lose battle", () => DeveloperSettleBattle(false));
        var tune = Row();
        Add(tune, "DeveloperFillResources", "bolt", "Fill courage and mana", DeveloperFillResources);
        _developerSpeedButton = Add(tune, "DeveloperBattleSpeed", "clock", "", CycleDeveloperBattleSpeed);
        RefreshDeveloperSpeedButton();
    }

    private void DeveloperSettleBattle(bool won)
    {
        if (_battleEnded || _restartPending) return;
        if (_battlePaused) TogglePause();
        if (won) _enemyBaseHealth = 0f;
        else _playerBaseHealth = 0f;
        CheckBattleEnd();
    }

    private void DeveloperFillResources()
    {
        _courage = _maxCourage;
        _mana = _maxMana;
        UpdateHud();
    }

    private void CycleDeveloperBattleSpeed()
    {
        var speeds = GameState.DeveloperBattleSpeeds;
        var index = System.Array.IndexOf(speeds, GameState.Instance.DeveloperBattleSpeed);
        GameState.Instance.SetDeveloperBattleSpeed(speeds[(index + 1) % speeds.Length]);
        ApplyDeveloperBattleSpeed();
        RefreshDeveloperSpeedButton();
    }

    private void RefreshDeveloperSpeedButton()
    {
        if (!IsInstanceValid(_developerSpeedButton)) return;
        _developerSpeedButton.Text = $"Speed {GameState.Instance.BattleSpeedScale:0}×";
        _developerSpeedButton.TooltipText = "Change the battle speed (1×, 2×, 4×, 8×)";
        _developerSpeedButton.AccessibilityName = _developerSpeedButton.Text;
    }
}
