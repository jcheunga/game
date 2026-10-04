using Godot;

public partial class MapMenu
{
    private PanelContainer _developerPanel;

    private void BuildDeveloperControls()
    {
        if (!GameState.DeveloperModeAvailable) return;
        _developerPanel = new PanelContainer { Name = "DeveloperSupplies", Visible = false };
        _developerPanel.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 10));
        _hud.AddChild(_developerPanel);
        HomeMapUi.Place(_developerPanel, 0, 0, new Rect2(22, 94, 318, 90));
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        _developerPanel.AddChild(stack);
        stack.AddChild(RealmUi.Label("Developer mode", 18));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);
        var gold = RealmUi.Button("gold", "+1,000 gold", () => GameState.Instance.TryAddDeveloperResources(1000, 0));
        gold.Name = "DeveloperMapGold";
        var food = RealmUi.Button("food", "+100 food", () => GameState.Instance.TryAddDeveloperResources(0, 100));
        food.Name = "DeveloperMapFood";
        gold.CustomMinimumSize = new Vector2(150, 44);
        food.CustomMinimumSize = new Vector2(140, 44);
        foreach (var button in new[] { gold, food })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize);
            HomeMapUi.StyleButton(button);
            row.AddChild(button);
        }
    }
}
