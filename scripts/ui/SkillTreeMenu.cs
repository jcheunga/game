#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class SkillTreeMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _unitListPanel = null!;
	private PanelContainer _treePanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _unitStack = null!;
	private VBoxContainer _treeStack = null!;
	private string? _selectedUnitId;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _unitListPanel, _treePanel });
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
		titleRow.AddChild(new Label { Text = "Skill Trees", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

		// Left panel: unit list
		_unitListPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(380f, 480f) };
		AddChild(_unitListPanel);
		var listOuter = new MarginContainer();
		listOuter.AddThemeConstantOverride("margin_left", 8);
		listOuter.AddThemeConstantOverride("margin_right", 8);
		listOuter.AddThemeConstantOverride("margin_top", 8);
		listOuter.AddThemeConstantOverride("margin_bottom", 8);
		_unitListPanel.AddChild(listOuter);
		var listInner = new VBoxContainer();
		listInner.AddThemeConstantOverride("separation", 4);
		listOuter.AddChild(listInner);
		listInner.AddChild(RealmUi.SectionTitle("Allies"));
		var listScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 400f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		listInner.AddChild(listScroll);
		_unitStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_unitStack.AddThemeConstantOverride("separation", 6);
		listScroll.AddChild(_unitStack);

		// Right panel: skill tree
		_treePanel = new PanelContainer { Position = new Vector2(420f, 122f), Size = new Vector2(836f, 480f) };
		AddChild(_treePanel);
		var treeOuter = new MarginContainer();
		treeOuter.AddThemeConstantOverride("margin_left", 8);
		treeOuter.AddThemeConstantOverride("margin_right", 8);
		treeOuter.AddThemeConstantOverride("margin_top", 8);
		treeOuter.AddThemeConstantOverride("margin_bottom", 8);
		_treePanel.AddChild(treeOuter);
		var treeInner = new VBoxContainer();
		treeInner.AddThemeConstantOverride("separation", 4);
		treeOuter.AddChild(treeInner);
		var treeScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 430f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		treeInner.AddChild(treeScroll);
		_treeStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_treeStack.AddThemeConstantOverride("separation", 8);
		treeScroll.AddChild(_treeStack);

		// Status + nav
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var armoryBtn = new RealmButton { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		armoryBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		RebuildResourcesRow(gs);
		RebuildUnitList();
		RebuildTree();
	}

	private void RebuildResourcesRow(GameState gs)
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("tomes", "", gs.Tomes.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", gs.Gold.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void RebuildUnitList()
	{
		foreach (var child in _unitStack.GetChildren()) child.QueueFree();

		var gs = GameState.Instance;
		var ownedUnitIds = gs.GetOwnedPlayerUnitIds();
		var allTrees = UnitSkillTreeCatalog.GetAll();

		foreach (var unitId in ownedUnitIds.OrderBy(id => id))
		{
			var tree = UnitSkillTreeCatalog.GetTree(unitId);
			if (tree == null) continue;

			var unit = GameData.GetUnit(unitId);
			var displayName = unit?.DisplayName ?? unitId;

			var unlockedCount = 0;
			foreach (var node in tree.Nodes)
			{
				if (gs.IsSkillNodeUnlocked(unitId, node.Id))
					unlockedCount++;
			}

			var capturedId = unitId;
			_selectedUnitId ??= capturedId;
			var row = new RealmButton
			{
				Text = $"{displayName} · {unlockedCount}/{tree.Nodes.Length}",
				Icon = UiArtLoader.TryLoadUnitIcon(unit),
				ExpandIcon = true,
				Alignment = HorizontalAlignment.Left,
				ToggleMode = true,
				ButtonPressed = _selectedUnitId == capturedId,
				CustomMinimumSize = new Vector2(0f, 52f),
				TooltipText = $"{unlockedCount} of {tree.Nodes.Length} talents unlocked",
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			row.AddThemeConstantOverride("icon_max_width", 36);
			row.SetMeta("realm_toggle", true);
			row.Pressed += () =>
			{
				_selectedUnitId = capturedId;
				RefreshUi();
			};
			if (RealmModal.Embedded(this)) ModalUi.StyleButton(row);
			_unitStack.AddChild(row);
		}

		if (ownedUnitIds.Count == 0)
		{
			_unitStack.AddChild(RealmUi.Label("Recruit allies in the armory to train their talents.", 18, true));
		}
	}

	private void RebuildTree()
	{
		foreach (var child in _treeStack.GetChildren()) child.QueueFree();

		if (_selectedUnitId == null)
		{
			_treeStack.AddChild(RealmUi.EmptyState("hammer", "No talents yet", "Recruit an ally to open their talent tree."));
			return;
		}

		var gs = GameState.Instance;
		var tree = UnitSkillTreeCatalog.GetTree(_selectedUnitId);
		if (tree == null)
		{
			_treeStack.AddChild(RealmUi.Label("This ally has no talents.", 18, true));
			return;
		}

		var unit = GameData.GetUnit(_selectedUnitId);
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 12);
		headerRow.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(52f, 52f)));
		var heading = RealmUi.Heading(unit?.DisplayName ?? _selectedUnitId, 24);
		heading.VerticalAlignment = VerticalAlignment.Center;
		headerRow.AddChild(heading);
		var balance = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
		balance.AddThemeConstantOverride("separation", 16);
		balance.AddChild(HomeResourceUi.Amount("tomes", gs.Tomes.ToString("N0"), $"Tomes: {gs.Tomes:N0}", 28));
		balance.AddChild(HomeResourceUi.Amount("gold", gs.Gold.ToString("N0"), $"Gold: {gs.Gold:N0}", 28));
		headerRow.AddChild(balance);
		_treeStack.AddChild(headerRow);

		// Talents unlock in order; each card names the talent it requires.
		var grid = new GridContainer { Columns = MobilePresentation.Enabled ? 1 : 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		_treeStack.AddChild(grid);
		foreach (var node in tree.Nodes)
		{
			var isUnlocked = gs.IsSkillNodeUnlocked(_selectedUnitId, node.Id);
			var capturedNodeId = node.Id;
			var capturedUnitId = _selectedUnitId;
			var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			grid.AddChild(card);
			var stack = new VBoxContainer();
			stack.AddThemeConstantOverride("separation", 6);
			card.AddChild(stack);

			var title = RealmUi.KeyValue(node.Title, isUnlocked ? "Unlocked" : "", isUnlocked ? new Color("9fd49a") : null);
			var titleLabel = title.GetChild<Label>(0);
			titleLabel.AddThemeColorOverride("font_color", ModalUi.Cream);
			titleLabel.AddThemeFontOverride("font", ModalUi.HeadingFont);
			titleLabel.AddThemeFontSizeOverride("font_size", 20);
			stack.AddChild(title);

			var bonusParts = new List<string>();
			if (node.HealthScale > 1.001f) bonusParts.Add($"+{(node.HealthScale - 1f) * 100:0}% health");
			if (node.DamageScale > 1.001f) bonusParts.Add($"+{(node.DamageScale - 1f) * 100:0}% damage");
			if (node.SpeedScale > 1.001f) bonusParts.Add($"+{(node.SpeedScale - 1f) * 100:0}% speed");
			if (node.CooldownReduction > 0.001f) bonusParts.Add($"−{node.CooldownReduction:0.##}s cooldown");
			stack.AddChild(RealmUi.Label(bonusParts.Count > 0 ? string.Join(" · ", bonusParts) : node.Description, 18, true));

			if (isUnlocked) continue;
			var prerequisite = string.IsNullOrWhiteSpace(node.PrerequisiteNodeId) ? null : tree.Nodes.FirstOrDefault(n => n.Id == node.PrerequisiteNodeId);
			var prereqsMet = prerequisite == null || gs.IsSkillNodeUnlocked(capturedUnitId, prerequisite.Id);
			var canAfford = gs.Tomes >= node.TomeCost && gs.Gold >= node.GoldCost;
			var actions = new HBoxContainer();
			actions.AddThemeConstantOverride("separation", 16);
			stack.AddChild(actions);
			if (!prereqsMet)
			{
				var requirement = RealmUi.Label($"Requires {prerequisite!.Title}", 18, true);
				requirement.VerticalAlignment = VerticalAlignment.Center;
				actions.AddChild(requirement);
				continue;
			}
			actions.AddChild(HomeResourceUi.Amount("tomes", node.TomeCost.ToString(), $"{node.TomeCost} tomes", 26));
			actions.AddChild(HomeResourceUi.Amount("gold", node.GoldCost.ToString("N0"), $"{node.GoldCost:N0} gold", 26));
			actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var unlockBtn = new RealmButton { Text = "Unlock", CustomMinimumSize = new Vector2(120f, 44f), Disabled = !canAfford,
				TooltipText = canAfford ? "Unlock this talent" : "Not enough tomes or gold" };
			if (canAfford) unlockBtn.SetMeta("realm_primary", true);
			unlockBtn.Pressed += () =>
			{
				gs.TryUnlockSkillNode(capturedUnitId, capturedNodeId, out var message);
				_statusLabel.Text = message;
				RefreshUi();
			};
			actions.AddChild(unlockBtn);
		}
	}
}
