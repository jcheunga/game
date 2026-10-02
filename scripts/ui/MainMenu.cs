using Godot;

// Home and campaign share the same atlas; startup-only prompts stay here.
public partial class MainMenu : MapMenu
{
    public override void _Ready()
    {
        base._Ready();
        TryShowConsentPrompt();
        TryHandleDeepLink();
    }

    private void TryHandleDeepLink()
    {
        if (DeepLinkHandler.Instance == null || !DeepLinkHandler.Instance.HasPendingChallenge())
            return;

        var code = DeepLinkHandler.Instance.ConsumePendingChallenge();
        if (string.IsNullOrWhiteSpace(code)) return;

        GameState.Instance?.TrySetSelectedAsyncChallengeCode(code, out _);
        SceneRouter.Instance?.GoToMultiplayer();
    }

    private void TryShowConsentPrompt()
    {
        if (GameState.Instance == null || GameState.Instance.HasShownConsentPrompt)
            return;

        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.7f)
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520f, 280f)
        };
        center.AddChild(panel);

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 24);
        padding.AddThemeConstantOverride("margin_right", 24);
        padding.AddThemeConstantOverride("margin_top", 24);
        padding.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Privacy & Analytics",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.AddChild(new Label
        {
            Text = "Allow optional gameplay analytics to help improve balance and difficulty? Events include your player ID, game version, platform, and gameplay results.\n\nOnline features also use your player ID, saves, and purchase records to work. Crash reports are off unless you enable them separately in Settings. You can change either choice there at any time.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 16);
        stack.AddChild(buttonRow);

        var acceptButton = new RealmButton
        {
            Text = "Allow Analytics",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        acceptButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(true);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(acceptButton);

        var declineButton = new RealmButton
        {
            Text = "No Thanks",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        declineButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(false);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(declineButton);
    }

}
