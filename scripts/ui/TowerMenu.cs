using System;
using Godot;

public partial class TowerMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _floorListPanel = null!;
	private PanelContainer _detailPanel = null!;
	private Label _highestFloorLabel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _floorStack = null!;

	// Detail panel references
	private Label _detailFloorLabel = null!;
	private Label _detailStageLabel = null!;
	private Label _detailScalingLabel = null!;
	private Label _detailModifiersLabel = null!;
	private HBoxContainer _detailRewardsRow = null!;
	private Label _detailRewardsLabel = null!;
	private Label _detailMilestoneLabel = null!;
	private Button _deployButton = null!;

	private int _selectedFloor = 1;

	public override void _Ready()
	{
		BuildUi();
		_selectedFloor = Math.Max(1, Math.Min(GameState.Instance.TowerHighestFloor + 1, ChallengeTowerCatalog.MaxFloor));
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _floorListPanel, _detailPanel });
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
		MenuBackdropComposer.AddSplitBackdrop(this, "tower", new Color("1a1a2e"), new Color("16213e"), new Color("38bdf8"), 104f);

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Challenge Tower", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_highestFloorLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_highestFloorLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		titleRow.AddChild(_highestFloorLabel);

		// Floor list panel (left)
		_floorListPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(380f, 480f) };
		AddChild(_floorListPanel);
		var listOuter = new MarginContainer();
		listOuter.AddThemeConstantOverride("margin_left", 8);
		listOuter.AddThemeConstantOverride("margin_right", 8);
		listOuter.AddThemeConstantOverride("margin_top", 8);
		listOuter.AddThemeConstantOverride("margin_bottom", 8);
		_floorListPanel.AddChild(listOuter);
		var listInner = new VBoxContainer();
		listInner.AddThemeConstantOverride("separation", 4);
		listOuter.AddChild(listInner);
		listInner.AddChild(new Label { Text = "Floors", HorizontalAlignment = HorizontalAlignment.Center });
		var floorScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 420f) };
		listInner.AddChild(floorScroll);
		_floorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_floorStack.AddThemeConstantOverride("separation", 2);
		floorScroll.AddChild(_floorStack);

		// Detail panel (right)
		_detailPanel = new PanelContainer { Position = new Vector2(420f, 122f), Size = new Vector2(836f, 480f) };
		AddChild(_detailPanel);
		var detailOuter = new MarginContainer();
		detailOuter.AddThemeConstantOverride("margin_left", 16);
		detailOuter.AddThemeConstantOverride("margin_right", 16);
		detailOuter.AddThemeConstantOverride("margin_top", 16);
		detailOuter.AddThemeConstantOverride("margin_bottom", 16);
		_detailPanel.AddChild(detailOuter);
		var detailStack = new VBoxContainer();
		detailStack.AddThemeConstantOverride("separation", 10);
		detailOuter.AddChild(detailStack);

		_detailFloorLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		detailStack.AddChild(_detailFloorLabel);

		_detailStageLabel = new Label();
		_detailStageLabel.AddThemeColorOverride("font_color", new Color("b0b8c8"));
		detailStack.AddChild(_detailStageLabel);

		_detailScalingLabel = new Label();
		_detailScalingLabel.AddThemeColorOverride("font_color", new Color("b0b8c8"));
		detailStack.AddChild(_detailScalingLabel);

		_detailModifiersLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(780f, 0f)
		};
		_detailModifiersLabel.AddThemeColorOverride("font_color", new Color("e07050"));
		detailStack.AddChild(_detailModifiersLabel);

		_detailRewardsRow = new HBoxContainer();
		_detailRewardsRow.AddThemeConstantOverride("separation", 8);
		detailStack.AddChild(_detailRewardsRow);
		_detailRewardsLabel = new Label
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		_detailRewardsLabel.AddThemeColorOverride("font_color", new Color("ffd700"));
		_detailRewardsRow.AddChild(_detailRewardsLabel);

		_detailMilestoneLabel = new Label();
		_detailMilestoneLabel.AddThemeColorOverride("font_color", new Color("a855f7"));
		detailStack.AddChild(_detailMilestoneLabel);

		// Spacer pushes deploy button toward bottom
		detailStack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		_deployButton = new Button { Text = "Deploy", CustomMinimumSize = new Vector2(200f, 44f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		_deployButton.Pressed += OnDeployPressed;
		detailStack.AddChild(_deployButton);

		// Status + nav
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mapBtn = new Button { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var highest = gs.TowerHighestFloor;
		_highestFloorLabel.Text = highest > 0 ? $"Floor {highest}" : "No floors cleared";

		RebuildFloorList();
		RefreshDetailPanel();
	}

	private void RebuildFloorList()
	{
		foreach (var child in _floorStack.GetChildren()) child.QueueFree();

		var gs = GameState.Instance;
		var highest = gs.TowerHighestFloor;

		for (var floor = 1; floor <= ChallengeTowerCatalog.MaxFloor; floor++)
		{
			var capturedFloor = floor;
			var stars = gs.GetTowerFloorStars(floor);
			var isLocked = floor > highest + 1;
			var isCleared = floor <= highest;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var floorLabel = new Label
			{
				Text = isCleared ? $"Floor {floor}  {"*".PadLeft(stars, '*')}" : $"Floor {floor}",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};

			if (capturedFloor == _selectedFloor)
				floorLabel.AddThemeColorOverride("font_color", new Color("38bdf8"));
			else if (isLocked)
				floorLabel.AddThemeColorOverride("font_color", new Color("505860"));
			else if (isCleared)
				floorLabel.AddThemeColorOverride("font_color", new Color("70c870"));

			row.AddChild(floorLabel);

			var selectBtn = new Button
			{
				Text = capturedFloor == _selectedFloor ? ">" : "Select",
				CustomMinimumSize = new Vector2(70f, 0f),
				Disabled = isLocked
			};
			selectBtn.Pressed += () =>
			{
				_selectedFloor = capturedFloor;
				RefreshUi();
			};
			row.AddChild(selectBtn);

			_floorStack.AddChild(row);
		}
	}

	private void RefreshDetailPanel()
	{
		var gs = GameState.Instance;
		var highest = gs.TowerHighestFloor;
		var isLocked = _selectedFloor > highest + 1;
		var def = ChallengeTowerCatalog.GetFloor(_selectedFloor);

		_detailFloorLabel.Text = $"Floor {def.Floor}";
		_detailStageLabel.Text = $"Base Stage: {def.BaseStageNumber}";
		_detailScalingLabel.Text = $"Enemy HP x{def.EnemyHealthScale:F1}  |  DMG x{def.EnemyDamageScale:F1}";

		if (def.ForcedModifierIds != null && def.ForcedModifierIds.Length > 0)
			_detailModifiersLabel.Text = $"Modifiers: {string.Join(", ", def.ForcedModifierIds)}";
		else
			_detailModifiersLabel.Text = "Modifiers: None";

		var rewards = "";
		if (def.RewardGold > 0) rewards += $"{def.RewardGold} gold  ";
		if (def.RewardFood > 0) rewards += $"{def.RewardFood} food  ";
		if (def.RewardTomes > 0) rewards += $"{def.RewardTomes} tomes  ";
		if (def.RewardEssence > 0) rewards += $"{def.RewardEssence} essence  ";
		RebuildRewardBadges(def);
		_detailRewardsLabel.Text = rewards.Length > 0 ? $"Rewards: {rewards.TrimEnd()}" : "Rewards: None";

		if (!string.IsNullOrEmpty(def.MilestoneRelicId))
			_detailMilestoneLabel.Text = $"Milestone Relic: {def.MilestoneRelicId}";
		else
			_detailMilestoneLabel.Text = "";

		_deployButton.Disabled = isLocked;
		_deployButton.Text = isLocked ? "Locked" : "Deploy";
	}

	private void RebuildRewardBadges(TowerFloorDefinition def)
	{
		foreach (var child in _detailRewardsRow.GetChildren())
		{
			if (child != _detailRewardsLabel)
			{
				child.QueueFree();
			}
		}

		if (def.RewardGold > 0)
		{
			_detailRewardsRow.AddChild(UiBadgeFactory.CreateRewardBadge("gold", "", $"{def.RewardGold} Gold", new Vector2(30f, 30f)));
			_detailRewardsRow.MoveChild(_detailRewardsLabel, _detailRewardsRow.GetChildCount() - 1);
		}

		if (def.RewardFood > 0)
		{
			_detailRewardsRow.AddChild(UiBadgeFactory.CreateRewardBadge("food", "", $"{def.RewardFood} Food", new Vector2(30f, 30f)));
			_detailRewardsRow.MoveChild(_detailRewardsLabel, _detailRewardsRow.GetChildCount() - 1);
		}

		if (def.RewardTomes > 0)
		{
			_detailRewardsRow.AddChild(UiBadgeFactory.CreateRewardBadge("tomes", "", $"{def.RewardTomes} Tomes", new Vector2(30f, 30f)));
			_detailRewardsRow.MoveChild(_detailRewardsLabel, _detailRewardsRow.GetChildCount() - 1);
		}

		if (def.RewardEssence > 0)
		{
			_detailRewardsRow.AddChild(UiBadgeFactory.CreateRewardBadge("essence", "", $"{def.RewardEssence} Essence", new Vector2(30f, 30f)));
			_detailRewardsRow.MoveChild(_detailRewardsLabel, _detailRewardsRow.GetChildCount() - 1);
		}
	}

	private void OnDeployPressed()
	{
		var gs = GameState.Instance;
		if (_selectedFloor > gs.TowerHighestFloor + 1)
		{
			_statusLabel.Text = "Floor is locked.";
			return;
		}

		gs.PrepareTowerBattle(_selectedFloor);
		SceneRouter.Instance.GoToLoadout();
	}
}
