using System;
using Godot;

public partial class LeaderboardMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _tabPanel = null!;
	private PanelContainer _contentPanel = null!;
	private HBoxContainer _resourcesRow = null!;
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
		MenuBackdropComposer.AddSplitBackdrop(this, "leaderboard", new Color("1a1a2e"), new Color("16213e"), new Color("f59e0b"), 104f);

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
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

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
		RebuildResourcesRow();
		UpdateTabHighlights();
		RebuildContent();
	}

	private void RebuildResourcesRow()
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
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
		var arenaStatsRow = CreateMetaSummaryRow("arena_rating", $"Your Rating: {gs.ArenaRating}  |  Tier: {tier.Title}  |  W: {gs.ArenaWins}  L: {gs.ArenaLosses}", new Color("ffd700"));
		_contentStack.AddChild(arenaStatsRow);

		_contentStack.AddChild(new HSeparator());

		// Placeholder rankings
		AddPlaceholderRankings();

		// Player's own entry at the bottom
		_contentStack.AddChild(new HSeparator());
		_contentStack.AddChild(CreatePlayerEntryRow("arena_rating", $"{gs.ArenaRating}", new Color("ffd700")));
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
		_contentStack.AddChild(CreateMetaSummaryRow("tower_floor", $"Your Highest Floor: {floorText}", new Color("ffd700")));

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		_contentStack.AddChild(CreatePlayerEntryRow("tower_floor", floorText, new Color("ffd700")));
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
		_contentStack.AddChild(CreateMetaSummaryRow("endless_wave", $"Your Best Wave: {gs.BestEndlessWave}  |  Best Time: {timeText}", new Color("ffd700")));

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		_contentStack.AddChild(CreatePlayerEntryRow("endless_wave", $"Wave {gs.BestEndlessWave}", new Color("ffd700")));
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
		_contentStack.AddChild(CreateMetaSummaryRow("daily_streak", $"Your Daily Streak: {gs.DailyStreak}", new Color("ffd700")));

		_contentStack.AddChild(new HSeparator());

		AddPlaceholderRankings();

		// Player's own entry
		_contentStack.AddChild(new HSeparator());
		_contentStack.AddChild(CreatePlayerEntryRow("daily_streak", $"Streak: {gs.DailyStreak}", new Color("ffd700")));
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

	private static Control CreateMetaSummaryRow(string metaId, string text, Color color)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(UiBadgeFactory.CreateMetaBadge(metaId, text, new Vector2(30f, 30f)));

		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		label.AddThemeColorOverride("font_color", color);
		row.AddChild(label);
		return row;
	}

	private static Control CreatePlayerEntryRow(string metaId, string scoreText, Color color)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(UiBadgeFactory.CreateMetaBadge(metaId, scoreText, new Vector2(28f, 28f)));

		var playerName = new Label
		{
			Text = "You",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		playerName.AddThemeColorOverride("font_color", color);
		row.AddChild(playerName);

		var playerScore = new Label
		{
			Text = scoreText,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		playerScore.AddThemeColorOverride("font_color", color);
		row.AddChild(playerScore);
		return row;
	}
}
