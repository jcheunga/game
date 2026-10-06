using System.Collections.Generic;
using Godot;

public partial class ArenaMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _opponentsPanel = null!;
	private PanelContainer _ladderPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _opponentsStack = null!;
	private VBoxContainer _ladderStack = null!;
	private readonly List<ArenaOpponentSnapshot> _opponents = new();

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _opponentsPanel, _ladderPanel });
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
		titleRow.AddChild(new Label { Text = "PvP Arena", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

		// Opponents panel (left)
		_opponentsPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(600f, 480f) };
		AddChild(_opponentsPanel);
		var oppOuter = new MarginContainer();
		oppOuter.AddThemeConstantOverride("margin_left", 8);
		oppOuter.AddThemeConstantOverride("margin_right", 8);
		oppOuter.AddThemeConstantOverride("margin_top", 8);
		oppOuter.AddThemeConstantOverride("margin_bottom", 8);
		_opponentsPanel.AddChild(oppOuter);
		var oppInner = new VBoxContainer();
		oppInner.AddThemeConstantOverride("separation", 6);
		oppOuter.AddChild(oppInner);
		oppInner.AddChild(RealmUi.SectionTitle("Opponents"));
		var oppScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f) };
		oppInner.AddChild(oppScroll);
		_opponentsStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_opponentsStack.AddThemeConstantOverride("separation", 8);
		oppScroll.AddChild(_opponentsStack);

		// Ladder panel (right)
		_ladderPanel = new PanelContainer { Position = new Vector2(640f, 122f), Size = new Vector2(616f, 480f) };
		AddChild(_ladderPanel);
		var ladOuter = new MarginContainer();
		ladOuter.AddThemeConstantOverride("margin_left", 8);
		ladOuter.AddThemeConstantOverride("margin_right", 8);
		ladOuter.AddThemeConstantOverride("margin_top", 8);
		ladOuter.AddThemeConstantOverride("margin_bottom", 8);
		_ladderPanel.AddChild(ladOuter);
		var ladInner = new VBoxContainer();
		ladInner.AddThemeConstantOverride("separation", 6);
		ladOuter.AddChild(ladInner);
		ladInner.AddChild(RealmUi.SectionTitle("Tiers"));
		_ladderStack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_ladderStack.AddThemeConstantOverride("separation", 6);
		ladInner.AddChild(_ladderStack);

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
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var tier = gs.GetArenaTier();
		RebuildResourcesRow(gs);

		// Update title row info
		var titleRow = _titlePanel.GetChild<HBoxContainer>(0);
		// Remove old dynamic labels if any
		RealmUi.TrimChildren(titleRow, 2);
		titleRow.AddChild(UiBadgeFactory.CreateMetaMetric("arena_rating", $"{gs.ArenaRating} · {tier.Title} · {gs.ArenaWins}–{gs.ArenaLosses}", new Vector2(24f, 24f)));

		GenerateOpponents();
		RebuildOpponents();
		RebuildLadder();
	}

	private void RebuildResourcesRow(GameState gs)
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", gs.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", gs.Food.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void GenerateOpponents()
	{
		_opponents.Clear();
		var rating = GameState.Instance.ArenaRating;
		for (var i = 0; i < 3; i++)
		{
			_opponents.Add(ArenaCatalog.GenerateLocalOpponent(rating, i));
		}
	}

	private void RebuildOpponents()
	{
		foreach (var child in _opponentsStack.GetChildren()) child.QueueFree();

		for (var i = 0; i < _opponents.Count; i++)
		{
			var opponent = _opponents[i];
			var card = new HBoxContainer();
			card.AddThemeConstantOverride("separation", 12);
			var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			info.AddThemeConstantOverride("separation", 4);
			card.AddChild(info);

			var name = RealmUi.Heading(opponent.Callsign, 20);
			info.AddChild(name);
			var ratingTier = ArenaCatalog.GetTier(opponent.ArenaRating);
			info.AddChild(RealmUi.Label($"{ratingTier.Title} · {opponent.ArenaRating} rating · {opponent.PowerRating} power", 18, true));
			info.AddChild(BuildUnitBadgeRow(opponent.DeckUnitIds, 36f));

			var capturedOpponent = opponent;
			var challengeBtn = RealmUi.Button("sword", "Challenge", () =>
			{
				GameState.Instance.PrepareArenaBattle(capturedOpponent);
				_statusLabel.Text = $"Challenging {capturedOpponent.Callsign}…";
				SceneRouter.Instance.GoToLoadout();
			});
			challengeBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			card.AddChild(challengeBtn);

			_opponentsStack.AddChild(card);
			if (i < _opponents.Count - 1) _opponentsStack.AddChild(new HSeparator());
		}
	}

	private void RebuildLadder()
	{
		foreach (var child in _ladderStack.GetChildren()) child.QueueFree();

		var gs = GameState.Instance;
		var playerTier = gs.GetArenaTier();
		var allTiers = ArenaCatalog.GetAllTiers();
		_ladderStack.AddChild(RealmUi.Label($"Your rating {gs.ArenaRating} · {gs.ArenaWins} wins · {gs.ArenaLosses} losses", 18, true));

		// Highest tier first; the player's tier is lit instead of marked with symbols.
		for (var i = allTiers.Count - 1; i >= 0; i--)
		{
			var tier = allTiers[i];
			var current = tier.Id == playerTier.Id;
			var range = tier.MaxRating < 99999 ? $"{tier.MinRating}–{tier.MaxRating}" : $"{tier.MinRating}+";
			var row = RealmUi.KeyValue(tier.Title, range);
			var name = row.GetChild<Label>(0);
			name.AddThemeColorOverride("font_color", new Color(tier.ColorHex).Lerp(ModalUi.Cream, .35f));
			var panel = new PanelContainer();
			panel.SetMeta("modal_unframed", true);
			panel.AddThemeStyleboxOverride("panel", current ? new ModalSurface(ModalMaterial.Tab, 10, new Color("b39257"), true) : new StyleBoxEmpty { ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6 });
			panel.AddChild(row);
			if (current) name.Text = $"{tier.Title} · you";
			_ladderStack.AddChild(panel);
		}
	}

	private static HBoxContainer BuildUnitBadgeRow(IEnumerable<string> unitIds, float badgeSize)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		var names = new List<string>();
		foreach (var unitId in unitIds)
		{
			var unit = TryGetUnit(unitId);
			names.Add(unit?.DisplayName ?? unitId);
			row.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(badgeSize, badgeSize)));
		}
		// The badges carry the deck; their names live in the row's tooltip.
		row.TooltipText = string.Join(", ", names);
		row.MouseFilter = MouseFilterEnum.Pass;
		return row;
	}

	private static UnitDefinition TryGetUnit(string unitId)
	{
		try
		{
			return GameData.GetUnit(unitId);
		}
		catch
		{
			return null;
		}
	}
}
