using System;
using Godot;

public partial class LeaderboardMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _tabPanel = null!;
	private PanelContainer _contentPanel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _contentStack = null!;

	private Button _arenaTabBtn = null!;
	private Button _towerTabBtn = null!;
	private Button _endlessTabBtn = null!;
	private Button _dailyTabBtn = null!;

	private enum Tab { Arena, Tower, Endless, Daily }
	private Tab _activeTab = Tab.Arena;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _tabPanel, _contentPanel });
	}

	private void AnimateEntrance(Control[] panels)
	{
		for (var i = 0; i < panels.Length; i++)
		{
			var panel = panels[i];
			if (panel == null) continue;
			panel.Modulate = new Color(1f, 1f, 1f, 0f);
			var delay = 0.06f + (i * 0.05f);
			var tween = CreateTween();
			tween.TweenProperty(panel, "modulate:a", 1f, 0.22f)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}
	}

	private void BuildUi()
	{
		AddChild(new ColorRect { Color = new Color("1a1a2e"), Position = Vector2.Zero, Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("16213e"), Position = new Vector2(0f, 360f), Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("f59e0b"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label
		{
			Text = "Leaderboards",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});

		// Tab bar panel
		_tabPanel = new PanelContainer { Position = new Vector2(24f, 112f), Size = new Vector2(1232f, 48f) };
		AddChild(_tabPanel);
		var tabRow = new HBoxContainer();
		tabRow.AddThemeConstantOverride("separation", 8);
		_tabPanel.AddChild(tabRow);

		_arenaTabBtn = new Button { Text = "Arena", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_arenaTabBtn.Pressed += () => SwitchTab(Tab.Arena);
		tabRow.AddChild(_arenaTabBtn);

		_towerTabBtn = new Button { Text = "Tower", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_towerTabBtn.Pressed += () => SwitchTab(Tab.Tower);
		tabRow.AddChild(_towerTabBtn);

		_endlessTabBtn = new Button { Text = "Endless", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_endlessTabBtn.Pressed += () => SwitchTab(Tab.Endless);
		tabRow.AddChild(_endlessTabBtn);

		_dailyTabBtn = new Button { Text = "Daily", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dailyTabBtn.Pressed += () => SwitchTab(Tab.Daily);
		tabRow.AddChild(_dailyTabBtn);

		// Content panel
		_contentPanel = new PanelContainer { Position = new Vector2(24f, 170f), Size = new Vector2(1232f, 432f) };
		AddChild(_contentPanel);
		var contentOuter = new MarginContainer();
		contentOuter.AddThemeConstantOverride("margin_left", 16);
		contentOuter.AddThemeConstantOverride("margin_right", 16);
		contentOuter.AddThemeConstantOverride("margin_top", 16);
		contentOuter.AddThemeConstantOverride("margin_bottom", 16);
		_contentPanel.AddChild(contentOuter);
		var contentScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		contentOuter.AddChild(contentScroll);
		_contentStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_contentStack.AddThemeConstantOverride("separation", 6);
		contentScroll.AddChild(_contentStack);

		// Status label
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		// Bottom nav
		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mainMenuBtn = new Button { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainMenuBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainMenuBtn);
		var profileBtn = new Button { Text = "Player Profile", CustomMinimumSize = new Vector2(140f, 0f) };
		profileBtn.Pressed += () => SceneRouter.Instance.GoToProfile();
		bottomRow.AddChild(profileBtn);
	}

	private void SwitchTab(Tab tab)
	{
		_activeTab = tab;
		RefreshUi();
	}

	private void RefreshUi()
	{
		UpdateTabHighlights();
		RebuildContent();
	}

	private void UpdateTabHighlights()
	{
		var activeColor = new Color("f59e0b");
		var normalColor = new Color("c8c8c8");

		_arenaTabBtn.Modulate = _activeTab == Tab.Arena ? activeColor : normalColor;
		_towerTabBtn.Modulate = _activeTab == Tab.Tower ? activeColor : normalColor;
		_endlessTabBtn.Modulate = _activeTab == Tab.Endless ? activeColor : normalColor;
		_dailyTabBtn.Modulate = _activeTab == Tab.Daily ? activeColor : normalColor;
	}

	private void RebuildContent()
	{
		foreach (var child in _contentStack.GetChildren()) child.QueueFree();

		switch (_activeTab)
		{
			case Tab.Arena:
				BuildArenaContent();
				break;
			case Tab.Tower:
				BuildTowerContent();
				break;
			case Tab.Endless:
				BuildEndlessContent();
				break;
			case Tab.Daily:
				BuildDailyContent();
				break;
		}
	}

	private void BuildArenaContent()
	{
		var gs = GameState.Instance;
		var tier = gs.GetArenaTier();

		var headerLabel = new Label
		{
			Text = "Arena Rankings",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		headerLabel.AddThemeColorOverride("font_color", new Color("f59e0b"));
		_contentStack.AddChild(headerLabel);

		_contentStack.AddChild(new HSeparator());

		// Player stats
		var statsLabel = new Label
		{
			Text = $"Your Rating: {gs.ArenaRating}  |  Tier: {tier.Title}  |  W: {gs.ArenaWins}  L: {gs.ArenaLosses}",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		statsLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_contentStack.AddChild(statsLabel);

		_contentStack.AddChild(new HSeparator());

		// Placeholder rankings
		AddPlaceholderRankings();

		// Player's own entry at the bottom
		_contentStack.AddChild(new HSeparator());
		var playerRow = new HBoxContainer();
		playerRow.AddThemeConstantOverride("separation", 16);
		playerRow.AddChild(new Label { Text = "---", CustomMinimumSize = new Vector2(40f, 0f) });
		var playerName = new Label { Text = "You", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		playerName.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerName);
		var playerScore = new Label { Text = $"{gs.ArenaRating}", HorizontalAlignment = HorizontalAlignment.Right };
		playerScore.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerScore);
		_contentStack.AddChild(playerRow);
	}

	private void BuildTowerContent()
	{
		var gs = GameState.Instance;

		var headerLabel = new Label
		{
			Text = "Tower Rankings",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		headerLabel.AddThemeColorOverride("font_color", new Color("38bdf8"));
		_contentStack.AddChild(headerLabel);

		_contentStack.AddChild(new HSeparator());

		// Player stats
		var floorText = gs.TowerHighestFloor > 0 ? $"Floor {gs.TowerHighestFloor}" : "No floors cleared";
		var statsLabel = new Label
		{
			Text = $"Your Highest Floor: {floorText}",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		statsLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_contentStack.AddChild(statsLabel);

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		var playerRow = new HBoxContainer();
		playerRow.AddThemeConstantOverride("separation", 16);
		playerRow.AddChild(new Label { Text = "---", CustomMinimumSize = new Vector2(40f, 0f) });
		var playerName = new Label { Text = "You", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		playerName.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerName);
		var playerScore = new Label { Text = floorText, HorizontalAlignment = HorizontalAlignment.Right };
		playerScore.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerScore);
		_contentStack.AddChild(playerRow);
	}

	private void BuildEndlessContent()
	{
		var gs = GameState.Instance;

		var headerLabel = new Label
		{
			Text = "Endless Rankings",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		headerLabel.AddThemeColorOverride("font_color", new Color("a855f7"));
		_contentStack.AddChild(headerLabel);

		_contentStack.AddChild(new HSeparator());

		// Player stats
		var timeSpan = TimeSpan.FromSeconds(gs.BestEndlessTimeSeconds);
		var timeText = $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
		var statsLabel = new Label
		{
			Text = $"Your Best Wave: {gs.BestEndlessWave}  |  Best Time: {timeText}",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		statsLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_contentStack.AddChild(statsLabel);

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		var playerRow = new HBoxContainer();
		playerRow.AddThemeConstantOverride("separation", 16);
		playerRow.AddChild(new Label { Text = "---", CustomMinimumSize = new Vector2(40f, 0f) });
		var playerName = new Label { Text = "You", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		playerName.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerName);
		var playerScore = new Label { Text = $"Wave {gs.BestEndlessWave}", HorizontalAlignment = HorizontalAlignment.Right };
		playerScore.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerScore);
		_contentStack.AddChild(playerRow);
	}

	private void BuildDailyContent()
	{
		var gs = GameState.Instance;

		var headerLabel = new Label
		{
			Text = "Daily Challenge Rankings",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		headerLabel.AddThemeColorOverride("font_color", new Color("22c55e"));
		_contentStack.AddChild(headerLabel);

		_contentStack.AddChild(new HSeparator());

		// Player stats
		var statsLabel = new Label
		{
			Text = $"Your Daily Streak: {gs.DailyStreak}",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		statsLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_contentStack.AddChild(statsLabel);

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		var playerRow = new HBoxContainer();
		playerRow.AddThemeConstantOverride("separation", 16);
		playerRow.AddChild(new Label { Text = "---", CustomMinimumSize = new Vector2(40f, 0f) });
		var playerName = new Label { Text = "You", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		playerName.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerName);
		var playerScore = new Label { Text = $"Streak: {gs.DailyStreak}", HorizontalAlignment = HorizontalAlignment.Right };
		playerScore.AddThemeColorOverride("font_color", new Color("ffd700"));
		playerRow.AddChild(playerScore);
		_contentStack.AddChild(playerRow);
	}

	private void AddPlaceholderRankings()
	{
		var placeholderLabel = new Label
		{
			Text = "Online leaderboards require server connection.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		placeholderLabel.AddThemeColorOverride("font_color", new Color("606870"));
		_contentStack.AddChild(placeholderLabel);

		// Show empty rank rows as visual placeholders
		for (var rank = 1; rank <= 10; rank++)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 16);

			var rankLabel = new Label { Text = $"#{rank}", CustomMinimumSize = new Vector2(40f, 0f) };
			rankLabel.AddThemeColorOverride("font_color", new Color("505860"));
			row.AddChild(rankLabel);

			var nameLabel = new Label { Text = "---", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			nameLabel.AddThemeColorOverride("font_color", new Color("505860"));
			row.AddChild(nameLabel);

			var scoreLabel = new Label { Text = "---", HorizontalAlignment = HorizontalAlignment.Right };
			scoreLabel.AddThemeColorOverride("font_color", new Color("505860"));
			row.AddChild(scoreLabel);

			_contentStack.AddChild(row);
		}
	}
}
