using Godot;

public partial class ProfileMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _generalPanel = null!;
	private PanelContainer _combatPanel = null!;
	private PanelContainer _collectionPanel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _generalStack = null!;
	private VBoxContainer _combatStack = null!;
	private VBoxContainer _collectionStack = null!;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _generalPanel, _combatPanel, _collectionPanel });
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
		titleRow.AddChild(new Label { Text = "Player Profile", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });

		// Left panel — General & Campaign
		_generalPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(390f, 480f) };
		AddChild(_generalPanel);
		var generalOuter = new MarginContainer();
		generalOuter.AddThemeConstantOverride("margin_left", 8);
		generalOuter.AddThemeConstantOverride("margin_right", 8);
		generalOuter.AddThemeConstantOverride("margin_top", 8);
		generalOuter.AddThemeConstantOverride("margin_bottom", 8);
		_generalPanel.AddChild(generalOuter);
		var generalInner = new VBoxContainer();
		generalInner.AddThemeConstantOverride("separation", 6);
		generalOuter.AddChild(generalInner);
		generalInner.AddChild(RealmUi.SectionTitle("Caravan"));
		var generalScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		generalInner.AddChild(generalScroll);
		_generalStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_generalStack.AddThemeConstantOverride("separation", 6);
		generalScroll.AddChild(_generalStack);

		// Center panel — Combat & Records
		_combatPanel = new PanelContainer { Position = new Vector2(430f, 122f), Size = new Vector2(390f, 480f) };
		AddChild(_combatPanel);
		var combatOuter = new MarginContainer();
		combatOuter.AddThemeConstantOverride("margin_left", 8);
		combatOuter.AddThemeConstantOverride("margin_right", 8);
		combatOuter.AddThemeConstantOverride("margin_top", 8);
		combatOuter.AddThemeConstantOverride("margin_bottom", 8);
		_combatPanel.AddChild(combatOuter);
		var combatInner = new VBoxContainer();
		combatInner.AddThemeConstantOverride("separation", 6);
		combatOuter.AddChild(combatInner);
		combatInner.AddChild(RealmUi.SectionTitle("Battles"));
		var combatScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		combatInner.AddChild(combatScroll);
		_combatStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_combatStack.AddThemeConstantOverride("separation", 6);
		combatScroll.AddChild(_combatStack);

		// Right panel — Collection & Achievements
		_collectionPanel = new PanelContainer { Position = new Vector2(836f, 122f), Size = new Vector2(420f, 480f) };
		AddChild(_collectionPanel);
		var collectionOuter = new MarginContainer();
		collectionOuter.AddThemeConstantOverride("margin_left", 8);
		collectionOuter.AddThemeConstantOverride("margin_right", 8);
		collectionOuter.AddThemeConstantOverride("margin_top", 8);
		collectionOuter.AddThemeConstantOverride("margin_bottom", 8);
		_collectionPanel.AddChild(collectionOuter);
		var collectionInner = new VBoxContainer();
		collectionInner.AddThemeConstantOverride("separation", 6);
		collectionOuter.AddChild(collectionInner);
		collectionInner.AddChild(RealmUi.SectionTitle("Collection"));
		var collectionScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		collectionInner.AddChild(collectionScroll);
		_collectionStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_collectionStack.AddThemeConstantOverride("separation", 6);
		collectionScroll.AddChild(_collectionStack);

		// Status label
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		// Bottom nav
		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mapBtn = new RealmButton { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
		var armoryBtn = new RealmButton { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		armoryBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryBtn);
		var mainBtn = new RealmButton { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;

		// Update callsign in title
		var titleRow = _titlePanel.GetChild<HBoxContainer>(0);
		RealmUi.TrimChildren(titleRow, 1);
		var callsignLabel = new Label
		{
			Text = gs.PlayerCallsign,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		callsignLabel.AddThemeColorOverride("font_color", ModalUi.Muted);
		titleRow.AddChild(callsignLabel);

		RebuildGeneralPanel(gs);
		RebuildCombatPanel(gs);
		RebuildCollectionPanel(gs);
	}

	private void RebuildGeneralPanel(GameState gs)
	{
		foreach (var child in _generalStack.GetChildren()) child.QueueFree();

		AddStatRow(_generalStack, "Callsign", gs.PlayerCallsign);
		AddStatRow(_generalStack, "Prestige level", gs.PrestigeLevel.ToString());

		_generalStack.AddChild(MakeSubheading("Currencies"));

		AddRewardStatRow(_generalStack, "gold", "Gold", gs.Gold.ToString("N0"));
		AddRewardStatRow(_generalStack, "food", "Food", gs.Food.ToString("N0"));
		AddRewardStatRow(_generalStack, "sigils", "Sigils", gs.Sigils.ToString("N0"));
		AddRewardStatRow(_generalStack, "shards", "Shards", gs.RelicShards.ToString("N0"));
		AddRewardStatRow(_generalStack, "tomes", "Tomes", gs.Tomes.ToString("N0"));
		AddRewardStatRow(_generalStack, "essence", "Essence", gs.Essence.ToString("N0"));

		_generalStack.AddChild(MakeSubheading("Campaign"));

		AddStatRow(_generalStack, "Stages cleared", $"{gs.HighestUnlockedStage}/{gs.MaxStage}");
		AddStatRow(_generalStack, "Stars earned", gs.TotalStarsEarned.ToString());
		AddStatRow(_generalStack, "Hard mode cleared", gs.HardModeHighestCleared.ToString());
	}

	private void RebuildCombatPanel(GameState gs)
	{
		foreach (var child in _combatStack.GetChildren()) child.QueueFree();

		var totalBattles = gs.EndlessRuns + gs.ChallengeRuns + gs.BossRushRuns;
		AddStatRow(_combatStack, "Total battles", totalBattles.ToString("N0"));
		AddStatRow(_combatStack, "Endless runs", gs.EndlessRuns.ToString("N0"));
		AddStatRow(_combatStack, "Challenge runs", gs.ChallengeRuns.ToString("N0"));
		AddStatRow(_combatStack, "Boss rush runs", gs.BossRushRuns.ToString("N0"));

		_combatStack.AddChild(MakeSubheading("Records"));

		AddStatRow(_combatStack, "Best endless wave", gs.BestEndlessWave.ToString());
		AddStatRow(_combatStack, "Best boss rush wave", gs.BestBossRushWave.ToString());

		_combatStack.AddChild(MakeSubheading("Arena"));

		var tier = gs.GetArenaTier();
		AddStatRow(_combatStack, "Rating", $"{gs.ArenaRating} · {tier.Title}");
		AddStatRow(_combatStack, "Record", $"{gs.ArenaWins} wins · {gs.ArenaLosses} losses");


		_combatStack.AddChild(MakeSubheading("Expeditions"));
		AddStatRow(_combatStack, "Completed", gs.TotalExpeditionsCompleted.ToString());
	}

	private void RebuildCollectionPanel(GameState gs)
	{
		foreach (var child in _collectionStack.GetChildren()) child.QueueFree();

		var ownedUnits = gs.GetOwnedPlayerUnits().Count;
		var ownedSpells = gs.GetOwnedPlayerSpells().Count;
		var ownedRelics = gs.GetOwnedEquipment().Count;

		AddRewardStatRow(_collectionStack, "unit", "Allies", ownedUnits.ToString());
		AddRewardStatRow(_collectionStack, "spell", "Spells", ownedSpells.ToString());
		AddRewardStatRow(_collectionStack, "relic", "Relics", ownedRelics.ToString());

		_collectionStack.AddChild(MakeSubheading("Codex"));

		var codexDiscovered = gs.DiscoveredCodexCount;
		var codexTotal = CodexCatalog.TotalEntries;
		var codexPct = codexTotal > 0 ? (int)(codexDiscovered * 100f / codexTotal) : 0;
		AddStatRow(_collectionStack, "Entries discovered", $"{codexDiscovered}/{codexTotal} · {codexPct}%");

		_collectionStack.AddChild(MakeSubheading("Guild"));

		if (!string.IsNullOrWhiteSpace(gs.GuildId) && gs.CachedGuildInfo != null)
		{
			AddStatRow(_collectionStack, "Guild", gs.CachedGuildInfo.Name);
			AddStatRow(_collectionStack, "Contribution", gs.GuildContributionPoints.ToString("N0"));
		}
		else
		{
			AddStatRow(_collectionStack, "Guild", "None");
		}

		_collectionStack.AddChild(MakeSubheading("Achievements"));

		var totalAchievements = AchievementCatalog.GetAll().Count;
		AddStatRow(_collectionStack, "Completed", $"{gs.GetUnlockedAchievementCount()}/{totalAchievements}");
	}

	private static void AddStatRow(VBoxContainer parent, string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		var nameLabel = new Label
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};
		nameLabel.AddThemeColorOverride("font_color", ModalUi.Muted);
		row.AddChild(nameLabel);
		var valueLabel = new Label
		{
			Text = value,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};
		valueLabel.AddThemeColorOverride("font_color", ModalUi.Cream);
		row.AddChild(valueLabel);
		parent.AddChild(row);
	}

	private static void AddRewardStatRow(VBoxContainer parent, string rewardType, string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		row.AddChild(UiBadgeFactory.CreateRewardBadge(rewardType, "", label, new Vector2(32f, 32f)));

		var nameLabel = new Label
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};
		nameLabel.AddThemeColorOverride("font_color", ModalUi.Muted);
		row.AddChild(nameLabel);

		var valueLabel = new Label
		{
			Text = value,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};
		valueLabel.AddThemeColorOverride("font_color", ModalUi.Cream);
		row.AddChild(valueLabel);

		parent.AddChild(row);
	}

	private static Control MakeSubheading(string text)
	{
		var spaced = new MarginContainer();
		spaced.AddThemeConstantOverride("margin_top", 10);
		spaced.AddChild(RealmUi.SectionTitle(text, 18));
		return spaced;
	}
}
