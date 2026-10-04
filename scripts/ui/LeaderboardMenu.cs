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

        MedievalUi.Apply(this);

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
        tabRow.SetMeta("realm_tabs", true);
		tabRow.AddThemeConstantOverride("separation", 8);
		_tabPanel.AddChild(tabRow);

		_arenaTabBtn = new RealmButton { Text = "Arena", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_arenaTabBtn.Pressed += () => SwitchTab(Tab.Arena);
		tabRow.AddChild(_arenaTabBtn);

		_towerTabBtn = new RealmButton { Text = "Tower", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_towerTabBtn.Pressed += () => SwitchTab(Tab.Tower);
		tabRow.AddChild(_towerTabBtn);

		_endlessTabBtn = new RealmButton { Text = "Endless", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_endlessTabBtn.Pressed += () => SwitchTab(Tab.Endless);
		tabRow.AddChild(_endlessTabBtn);

		_dailyTabBtn = new RealmButton { Text = "Daily", CustomMinimumSize = new Vector2(140f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dailyTabBtn.Pressed += () => SwitchTab(Tab.Daily);
		tabRow.AddChild(_dailyTabBtn);

		// Content panel
		_contentPanel = new PanelContainer { Position = new Vector2(24f, 198f), Size = new Vector2(1232f, 404f) };
		AddChild(_contentPanel);
		var contentOuter = new MarginContainer();
		contentOuter.AddThemeConstantOverride("margin_left", 16);
		contentOuter.AddThemeConstantOverride("margin_right", 16);
		contentOuter.AddThemeConstantOverride("margin_top", 16);
		contentOuter.AddThemeConstantOverride("margin_bottom", 16);
		_contentPanel.AddChild(contentOuter);
		var contentScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
		var mainMenuBtn = new RealmButton { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainMenuBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainMenuBtn);
		var profileBtn = new RealmButton { Text = "Player Profile", CustomMinimumSize = new Vector2(140f, 0f) };
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
		// Selection uses the shared tab styling instead of tinting the buttons.
		foreach (var (button, tab) in new[] { (_arenaTabBtn, Tab.Arena), (_towerTabBtn, Tab.Tower), (_endlessTabBtn, Tab.Endless), (_dailyTabBtn, Tab.Daily) })
		{
			button.ToggleMode = true;
			button.SetPressedNoSignal(_activeTab == tab);
		}
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
		AddStanding(("Rating", $"{gs.ArenaRating} · {tier.Title}"), ("Record", $"{gs.ArenaWins} wins · {gs.ArenaLosses} losses"));
		AddPlaceholderRankings();
	}

	private void BuildTowerContent()
	{
		var gs = GameState.Instance;
		AddStanding(("Highest floor", gs.TowerHighestFloor > 0 ? $"Floor {gs.TowerHighestFloor}" : "None yet"));
		AddPlaceholderRankings();
	}

	private void BuildEndlessContent()
	{
		var gs = GameState.Instance;
		var timeSpan = TimeSpan.FromSeconds(gs.BestEndlessTimeSeconds);
		AddStanding(("Best wave", gs.BestEndlessWave.ToString()), ("Best time", $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}"));
		AddPlaceholderRankings();
	}

	private void BuildDailyContent()
	{
		var gs = GameState.Instance;
		AddStanding(("Daily streak", gs.DailyStreak == 1 ? "1 day" : $"{gs.DailyStreak} days"));
		AddPlaceholderRankings();
	}

	private void AddStanding(params (string Name, string Value)[] facts)
	{
		_contentStack.AddChild(RealmUi.SectionTitle("Your standing"));
		// A narrow column keeps each value close to its name.
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(480f, 0f), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		column.SetMeta(RealmModal.KeepMinimum, true);
		foreach (var (name, value) in facts) column.AddChild(RealmUi.KeyValue(name, value));
		_contentStack.AddChild(column);
		_contentStack.AddChild(new HSeparator());
	}

	private void AddPlaceholderRankings()
	{
		_contentStack.AddChild(RealmUi.EmptyState("crown", "Rankings are offline", "Connect to the realm's server to compare caravans."));
	}
}
