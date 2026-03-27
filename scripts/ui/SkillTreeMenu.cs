#nullable enable
using System;
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
		MenuBackdropComposer.AddSplitBackdrop(this, "skill_tree", new Color("1a1a2e"), new Color("16213e"), new Color("facc15"), 104f);

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
		listInner.AddChild(new Label { Text = "Units", HorizontalAlignment = HorizontalAlignment.Center });
		var listScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 400f) };
		listInner.AddChild(listScroll);
		_unitStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_unitStack.AddThemeConstantOverride("separation", 4);
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
		var treeScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 430f) };
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
		var armoryBtn = new Button { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
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
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(38f, 38f)));

			var label = new Label
			{
				Text = $"{displayName}  ({unlockedCount}/{tree.Nodes.Length} nodes)",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (_selectedUnitId == capturedId)
				label.AddThemeColorOverride("font_color", new Color("facc15"));
			else if (unlockedCount == tree.Nodes.Length)
				label.AddThemeColorOverride("font_color", new Color("4ade80"));
			row.AddChild(label);

			var btn = new Button { Text = "View", CustomMinimumSize = new Vector2(60f, 0f) };
			btn.Pressed += () =>
			{
				_selectedUnitId = capturedId;
				RefreshUi();
			};
			row.AddChild(btn);
			_unitStack.AddChild(row);
		}

		if (ownedUnitIds.Count == 0)
		{
			_unitStack.AddChild(new Label { Text = "No units owned." });
		}
	}

	private void RebuildTree()
	{
		foreach (var child in _treeStack.GetChildren()) child.QueueFree();

		if (_selectedUnitId == null)
		{
			_treeStack.AddChild(new Label { Text = "Select a unit to view its skill tree.", HorizontalAlignment = HorizontalAlignment.Center });
			return;
		}

		var gs = GameState.Instance;
		var tree = UnitSkillTreeCatalog.GetTree(_selectedUnitId);
		if (tree == null)
		{
			_treeStack.AddChild(new Label { Text = "No skill tree found for this unit." });
			return;
		}

		var unit = GameData.GetUnit(_selectedUnitId);
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 10);
		headerRow.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(52f, 52f)));
		var headerLabel = new Label { Text = $"{unit?.DisplayName ?? _selectedUnitId} - Skill Tree", VerticalAlignment = VerticalAlignment.Center };
		headerLabel.AddThemeColorOverride("font_color", new Color("facc15"));
		headerRow.AddChild(headerLabel);
		_treeStack.AddChild(headerRow);

		_treeStack.AddChild(new HSeparator());

		// Display nodes in a vertical tree layout (root at top, diamond shape)
		// Node order: 0=root, 1=left, 2=right, 3=left-deep, 4=convergence
		// Visual diamond:  0
		//                 1  2
		//                3    (skip)
		//                  4

		for (var i = 0; i < tree.Nodes.Length; i++)
		{
			var node = tree.Nodes[i];
			var isUnlocked = gs.IsSkillNodeUnlocked(_selectedUnitId, node.Id);
			var capturedNodeId = node.Id;
			var capturedUnitId = _selectedUnitId;

			// Indentation to approximate diamond shape
			var indent = i switch
			{
				0 => 300, // center (root)
				1 => 140, // left branch
				2 => 460, // right branch
				3 => 140, // left-deep
				4 => 300, // convergence (center bottom)
				_ => 300
			};

			// Connector lines
			if (i == 1)
			{
				var connectorLabel = new Label { Text = "/          \\", HorizontalAlignment = HorizontalAlignment.Center };
				connectorLabel.AddThemeColorOverride("font_color", new Color("606060"));
				_treeStack.AddChild(connectorLabel);
			}
			else if (i == 3)
			{
				var connectorLabel = new Label { Text = "\\          /", HorizontalAlignment = HorizontalAlignment.Center };
				connectorLabel.AddThemeColorOverride("font_color", new Color("606060"));
				_treeStack.AddChild(connectorLabel);
			}

			var nodeContainer = new HBoxContainer();

			// Spacer for indentation
			var spacer = new Control { CustomMinimumSize = new Vector2(indent, 0f) };
			nodeContainer.AddChild(spacer);

			var nodePanel = new PanelContainer { CustomMinimumSize = new Vector2(260f, 0f) };
			nodeContainer.AddChild(nodePanel);

			var nodeMargin = new MarginContainer();
			nodeMargin.AddThemeConstantOverride("margin_left", 6);
			nodeMargin.AddThemeConstantOverride("margin_right", 6);
			nodeMargin.AddThemeConstantOverride("margin_top", 4);
			nodeMargin.AddThemeConstantOverride("margin_bottom", 4);
			nodePanel.AddChild(nodeMargin);

			var nodeVbox = new VBoxContainer();
			nodeVbox.AddThemeConstantOverride("separation", 2);
			nodeMargin.AddChild(nodeVbox);

			// Node status marker
			var statusIcon = isUnlocked ? "[UNLOCKED]" : "[LOCKED]";
			var titleText = $"{statusIcon}  {node.Title}";
			var titleLabel = new Label { Text = titleText };
			if (isUnlocked)
				titleLabel.AddThemeColorOverride("font_color", new Color("4ade80"));
			else
				titleLabel.AddThemeColorOverride("font_color", new Color("c8c8c8"));
			nodeVbox.AddChild(titleLabel);

			// Stat bonuses
			var bonusParts = new System.Collections.Generic.List<string>();
			if (node.HealthScale > 1.001f) bonusParts.Add($"HP +{(node.HealthScale - 1f) * 100:0}%");
			if (node.DamageScale > 1.001f) bonusParts.Add($"DMG +{(node.DamageScale - 1f) * 100:0}%");
			if (node.SpeedScale > 1.001f) bonusParts.Add($"SPD +{(node.SpeedScale - 1f) * 100:0}%");
			if (node.CooldownReduction > 0.001f) bonusParts.Add($"CD -{node.CooldownReduction:0.00}s");
			var bonusLabel = new Label { Text = bonusParts.Count > 0 ? string.Join(", ", bonusParts) : node.Description };
			bonusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
			nodeVbox.AddChild(bonusLabel);

			// Cost
			var costLabel = new Label { Text = $"Cost: {node.TomeCost} Tomes, {node.GoldCost} Gold" };
			costLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
			nodeVbox.AddChild(costLabel);

			// Unlock button (only for locked nodes)
			if (!isUnlocked)
			{
				var canAfford = gs.Tomes >= node.TomeCost && gs.Gold >= node.GoldCost;
				var prereqsMet = true;
				if (!string.IsNullOrWhiteSpace(node.PrerequisiteNodeId))
				{
					if (!gs.IsSkillNodeUnlocked(capturedUnitId, node.PrerequisiteNodeId))
					{
						prereqsMet = false;
					}
				}

				var unlockBtn = new Button
				{
					Text = !prereqsMet ? "Prereqs Not Met" : (!canAfford ? "Can't Afford" : "Unlock"),
					CustomMinimumSize = new Vector2(120f, 0f),
					Disabled = !canAfford || !prereqsMet
				};
				unlockBtn.Pressed += () =>
				{
					if (gs.TryUnlockSkillNode(capturedUnitId, capturedNodeId, out var message))
					{
						_statusLabel.Text = message;
						RefreshUi();
					}
					else
					{
						_statusLabel.Text = message;
					}
				};
				nodeVbox.AddChild(unlockBtn);
			}

			_treeStack.AddChild(nodeContainer);
		}
	}
}
