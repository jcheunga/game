using System.Collections.Generic;
using Godot;

public partial class SettingsMenu
{
    private Button _developerModeButton;
    private Label _developerGold, _developerFood;
    private readonly List<Button> _developerGrantButtons = new();

    private void BuildDeveloperPage(VBoxContainer page)
    {
        _developerGrantButtons.Clear();
        page.AddChild(RealmUi.Heading("Testing supplies", 24));
        page.AddChild(RealmUi.Label("Add supplies for test runs. Quick top-ups also appear below your map balances.", 18, true));
        _developerModeButton = RealmUi.Button("gear", "Developer mode: Off", () =>
        {
            GameState.Instance.SetDeveloperMode(!GameState.Instance.DeveloperModeEnabled);
            RefreshDeveloperPage();
        });
        _developerModeButton.Name = "DeveloperModeToggle";
        page.AddChild(_developerModeButton);

        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 16);
        page.AddChild(cards);
        Label Supplies(string icon, string title, int small, int large, bool gold)
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cards.AddChild(panel);
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 12);
            panel.AddChild(stack);
            stack.AddChild(RealmUi.Heading(title, 22));
            var balance = RealmUi.Label("", 20);
            stack.AddChild(balance);
            foreach (var amount in new[] { small, large })
            {
                var button = RealmUi.Button(icon, $"+{amount:N0} {title.ToLowerInvariant()}", () =>
                {
                    GameState.Instance.TryAddDeveloperResources(gold ? amount : 0, gold ? 0 : amount);
                    RefreshDeveloperPage();
                });
                button.Name = $"Developer{title}{amount}";
                _developerGrantButtons.Add(button);
                stack.AddChild(button);
            }
            return balance;
        }
        _developerGold = Supplies("gold", "Gold", 1000, 10000, true);
        _developerFood = Supplies("food", "Food", 10, 100, false);
    }

    private void RefreshDeveloperPage()
    {
        // The tools live in an inspector that may have been closed since.
        if (!IsInstanceValid(_developerModeButton)) return;
        var state = GameState.Instance;
        _developerModeButton.Text = state.DeveloperModeEnabled ? "Developer mode: On" : "Developer mode: Off";
        _developerModeButton.TooltipText = _developerModeButton.Text;
        _developerGold.Text = $"Balance · {state.Gold:N0}";
        _developerFood.Text = $"Balance · {state.Food:N0}";
        foreach (var button in _developerGrantButtons) button.Disabled = !state.DeveloperModeEnabled;
    }
}
