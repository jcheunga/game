using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ArenaMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _opponentsPanel = null!;
	private PanelContainer _ladderPanel = null!;
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
		AddChild(new ColorRect { Color = new Color("1a1a2e"), Position = Vector2.Zero, Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("16213e"), Position = new Vector2(0f, 360f), Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("e74c3c"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "PvP Arena", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });

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
		oppInner.AddChild(new Label { Text = "Choose Opponent", HorizontalAlignment = HorizontalAlignment.Center });
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
		ladInner.AddChild(new Label { Text = "Tier Ladder", HorizontalAlignment = HorizontalAlignment.Center });
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
		var mapBtn = new Button { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
		var armoryBtn = new Button { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		armoryBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var tier = gs.GetArenaTier();

		// Update title row info
		var titleRow = _titlePanel.GetChild<HBoxContainer>(0);
		// Remove old dynamic labels if any
		while (titleRow.GetChildCount() > 1) titleRow.GetChild(titleRow.GetChildCount() - 1).QueueFree();
		var infoLabel = new Label
		{
			Text = $"Rating: {gs.ArenaRating}  |  {tier.Title}  |  W: {gs.ArenaWins}  L: {gs.ArenaLosses}",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		infoLabel.AddThemeColorOverride("font_color", new Color(tier.ColorHex));
		titleRow.AddChild(infoLabel);

		GenerateOpponents();
		RebuildOpponents();
		RebuildLadder();
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
			var card = new VBoxContainer();
			card.AddThemeConstantOverride("separation", 4);

			card.AddChild(new Label { Text = opponent.Callsign });

			var ratingTier = ArenaCatalog.GetTier(opponent.ArenaRating);
			var ratingLabel = new Label { Text = $"Rating: {opponent.ArenaRating} ({ratingTier.Title})" };
			ratingLabel.AddThemeColorOverride("font_color", new Color(ratingTier.ColorHex));
			card.AddChild(ratingLabel);

			var unitNames = string.Join(", ", opponent.DeckUnitIds.Select(id =>
			{
				try { return GameData.GetUnit(id)?.DisplayName ?? id; } catch { return id; }
			}));
			card.AddChild(new Label { Text = $"Deck: {unitNames}" });
			card.AddChild(new Label { Text = $"Power: {opponent.PowerRating}" });

			var capturedOpponent = opponent;
			var challengeBtn = new Button { Text = "Challenge", CustomMinimumSize = new Vector2(120f, 0f) };
			challengeBtn.Pressed += () =>
			{
				GameState.Instance.PrepareArenaBattle(capturedOpponent);
				_statusLabel.Text = $"Challenging {capturedOpponent.Callsign}...";
				SceneRouter.Instance.GoToLoadout();
			};
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

		// Show tiers from highest to lowest
		for (var i = allTiers.Count - 1; i >= 0; i--)
		{
			var tier = allTiers[i];
			var isPlayerTier = tier.Id == playerTier.Id;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var marker = new Label { Text = isPlayerTier ? ">>>" : "   ", CustomMinimumSize = new Vector2(40f, 0f) };
			row.AddChild(marker);

			var tierLabel = new Label
			{
				Text = $"{tier.Title}  ({tier.MinRating} - {(tier.MaxRating < 99999 ? tier.MaxRating.ToString() : "---")})",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			tierLabel.AddThemeColorOverride("font_color", new Color(tier.ColorHex));
			row.AddChild(tierLabel);

			if (isPlayerTier)
			{
				var posLabel = new Label { Text = $"[{gs.ArenaRating}]" };
				posLabel.AddThemeColorOverride("font_color", new Color("ffffff"));
				row.AddChild(posLabel);
			}

			_ladderStack.AddChild(row);
		}

		_ladderStack.AddChild(new HSeparator());
		_ladderStack.AddChild(new Label { Text = $"Your Rating: {gs.ArenaRating}  |  Tier: {playerTier.Title}" });
	}
}
