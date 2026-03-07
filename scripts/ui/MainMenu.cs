using Godot;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1d2d44")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(460, 360)
        };
        center.AddChild(panel);

        var content = new MarginContainer();
        content.AddThemeConstantOverride("margin_left", 24);
        content.AddThemeConstantOverride("margin_top", 24);
        content.AddThemeConstantOverride("margin_right", 24);
        content.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(content);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        content.AddChild(stack);

        var title = new Label
        {
            Text = "ROADLINE: ZOMBIE SIEGE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "Dead Ahead-style prototype\nBuild squads, hold lanes, push the route.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(subtitle);

        var startButton = BuildButton("Start Campaign");
        startButton.Pressed += () => SceneRouter.Instance.GoToMap();
        stack.AddChild(startButton);

        var resetButton = BuildButton("Reset Progress");
        resetButton.Pressed += () =>
        {
            GameState.Instance.ResetProgress();
            SceneRouter.Instance.GoToMap();
        };
        stack.AddChild(resetButton);

        var quitButton = BuildButton("Quit");
        quitButton.Pressed += () => GetTree().Quit();
        stack.AddChild(quitButton);
    }

    private static Button BuildButton(string text)
    {
        return new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 52)
        };
    }
}
