using Godot;

public static class AppRatingPrompt
{
	private const int StagesBeforePrompt = 5;
	private const string RatingPromptSeenHintId = "rating_prompt_shown";

	public static bool ShouldShow()
	{
		if (GameState.Instance == null) return false;
		if (GameState.Instance.HasSeenHint(RatingPromptSeenHintId)) return false;
		return GameState.Instance.HighestUnlockedStage > StagesBeforePrompt;
	}

	public static void MarkShown()
	{
		GameState.Instance?.MarkHintSeen(RatingPromptSeenHintId);
	}

	public static void OpenStoreListing()
	{
		if (OS.HasFeature("ios"))
		{
			OS.ShellOpen("https://apps.apple.com/app/idXXXXXXXXXX");
		}
		else if (OS.HasFeature("android"))
		{
			OS.ShellOpen("https://play.google.com/store/apps/details?id=com.crownroad.game");
		}
		else
		{
			OS.ShellOpen("https://crownroad.game");
		}
	}

	public static void TryShowOn(Control parent)
	{
		if (!ShouldShow() || parent == null) return;

		var overlay = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.7f)
		};
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(overlay);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(460f, 220f)
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
			Text = "Enjoying Crownroad?",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		stack.AddChild(new Label
		{
			Text = "If the caravan march has been good to you, a rating helps other players find the game.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		});

		var buttonRow = new HBoxContainer();
		buttonRow.AddThemeConstantOverride("separation", 16);
		stack.AddChild(buttonRow);

		var rateButton = new Button
		{
			Text = "Rate Now",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 44f)
		};
		rateButton.Pressed += () =>
		{
			MarkShown();
			OpenStoreListing();
			overlay.QueueFree();
			center.QueueFree();
		};
		buttonRow.AddChild(rateButton);

		var laterButton = new Button
		{
			Text = "Maybe Later",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 44f)
		};
		laterButton.Pressed += () =>
		{
			MarkShown();
			overlay.QueueFree();
			center.QueueFree();
		};
		buttonRow.AddChild(laterButton);
	}
}
