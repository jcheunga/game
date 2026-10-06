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
	private VBoxContainer _detailFacts = null!;
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

        MedievalUi.Apply(this);

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
		listInner.AddChild(RealmUi.SectionTitle("Floors"));
		var floorScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 420f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		listInner.AddChild(floorScroll);
		_floorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_floorStack.AddThemeConstantOverride("separation", 6);
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
		detailStack.AddThemeConstantOverride("separation", 12);
		detailOuter.AddChild(detailStack);

		_detailFloorLabel = RealmUi.Heading("", 24);
		detailStack.AddChild(_detailFloorLabel);
		_detailFacts = new VBoxContainer();
		_detailFacts.AddThemeConstantOverride("separation", 10);
		detailStack.AddChild(_detailFacts);

		// Spacer pushes deploy button toward bottom
		detailStack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		_deployButton = RealmUi.Button("sword", "Deploy", OnDeployPressed, true);
		_deployButton.CustomMinimumSize = new Vector2(220f, 48f);
		_deployButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		detailStack.AddChild(_deployButton);

		// Status + nav
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mapBtn = new RealmButton { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
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

			// Each floor is one selectable row: cleared floors show their stars, locked floors a lock.
			var row = new RealmButton
			{
				Text = isCleared ? $"Floor {floor} · {stars}/3" : $"Floor {floor}",
				ToggleMode = true,
				ButtonPressed = capturedFloor == _selectedFloor,
				Disabled = isLocked,
				Alignment = HorizontalAlignment.Left,
				Icon = isLocked ? RealmUi.Icon("lock") : isCleared ? HomeMapArt.Icon("star") : null,
				IconAlignment = HorizontalAlignment.Right,
				ExpandIcon = true,
				CustomMinimumSize = new Vector2(0f, 44f),
				TooltipText = isLocked ? "Clear the floor below to unlock" : isCleared ? $"{stars} of 3 stars" : "Next floor",
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			row.AddThemeConstantOverride("icon_max_width", 22);
			row.SetMeta("realm_toggle", true);
			row.Pressed += () =>
			{
				_selectedFloor = capturedFloor;
				RefreshUi();
			};
			if (RealmModal.Embedded(this)) ModalUi.StyleButton(row);
			_floorStack.AddChild(row);
		}
	}

	private void RefreshDetailPanel()
	{
		var gs = GameState.Instance;
		var highest = gs.TowerHighestFloor;
		var isLocked = _selectedFloor > highest + 1;
		var def = ChallengeTowerCatalog.GetFloor(_selectedFloor);

		RealmUi.SetDisplayText(_detailFloorLabel, $"Floor {def.Floor}");
		RealmUi.Clear(_detailFacts);
		var stage = GameData.GetStage(def.BaseStageNumber);
		_detailFacts.AddChild(RealmUi.KeyValue("Battlefield", stage != null ? $"{stage.MapName} · {stage.StageName}" : $"Stage {def.BaseStageNumber}"));
		_detailFacts.AddChild(RealmUi.KeyValue("Enemy strength", $"Health ×{def.EnemyHealthScale:0.0} · damage ×{def.EnemyDamageScale:0.0}"));
		_detailFacts.AddChild(RealmUi.KeyValue("Modifiers", def.ForcedModifierIds is { Length: > 0 } ? string.Join(", ", def.ForcedModifierIds) : "None"));
		if (!string.IsNullOrEmpty(def.MilestoneRelicId))
			_detailFacts.AddChild(RealmUi.KeyValue("Milestone relic", GameData.GetEquipment(def.MilestoneRelicId)?.DisplayName ?? def.MilestoneRelicId, RealmUi.Gold));

		var rewards = new HBoxContainer();
		rewards.AddThemeConstantOverride("separation", 18);
		var caption = RealmUi.Label("Rewards", 18, true);
		caption.VerticalAlignment = VerticalAlignment.Center;
		rewards.AddChild(caption);
		var amounts = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
		amounts.AddThemeConstantOverride("separation", 18);
		rewards.AddChild(amounts);
		if (def.RewardGold > 0) amounts.AddChild(HomeResourceUi.Amount("gold", $"{def.RewardGold:N0}", $"{def.RewardGold:N0} gold", 28));
		if (def.RewardFood > 0) amounts.AddChild(HomeResourceUi.Amount("food", $"{def.RewardFood}", $"{def.RewardFood} rations", 28));
		if (def.RewardTomes > 0) amounts.AddChild(HomeResourceUi.Amount("tomes", $"{def.RewardTomes}", $"{def.RewardTomes} tomes", 28));
		if (def.RewardEssence > 0) amounts.AddChild(HomeResourceUi.Amount("essence", $"{def.RewardEssence}", $"{def.RewardEssence} essence", 28));
		_detailFacts.AddChild(rewards);

		_deployButton.Disabled = isLocked;
		_deployButton.Text = isLocked ? "Locked" : "Deploy";
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
