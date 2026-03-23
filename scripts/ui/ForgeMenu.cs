using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ForgeMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _dismantlePanel = null!;
	private PanelContainer _fusePanel = null!;
	private PanelContainer _craftPanel = null!;
	private Label _resourcesLabel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _dismantleStack = null!;
	private VBoxContainer _fuseStack = null!;
	private VBoxContainer _craftStack = null!;
	private readonly List<string> _selectedFuseRelics = new();

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _dismantlePanel, _fusePanel, _craftPanel });
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
		AddChild(new ColorRect { Color = new Color("a855f7"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Relic Forge", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titleRow.AddChild(_resourcesLabel);

		// Dismantle panel
		_dismantlePanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(380f, 480f) };
		AddChild(_dismantlePanel);
		var dismantleOuter = new MarginContainer();
		dismantleOuter.AddThemeConstantOverride("margin_left", 8);
		dismantleOuter.AddThemeConstantOverride("margin_right", 8);
		dismantleOuter.AddThemeConstantOverride("margin_top", 8);
		dismantleOuter.AddThemeConstantOverride("margin_bottom", 8);
		_dismantlePanel.AddChild(dismantleOuter);
		var dismantleInner = new VBoxContainer();
		dismantleInner.AddThemeConstantOverride("separation", 4);
		dismantleOuter.AddChild(dismantleInner);
		dismantleInner.AddChild(new Label { Text = "Dismantle", HorizontalAlignment = HorizontalAlignment.Center });
		dismantleInner.AddChild(new Label { Text = "Break relics into shards.", HorizontalAlignment = HorizontalAlignment.Center });
		var dismantleScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 340f) };
		dismantleInner.AddChild(dismantleScroll);
		_dismantleStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dismantleStack.AddThemeConstantOverride("separation", 4);
		dismantleScroll.AddChild(_dismantleStack);

		// Fuse panel
		_fusePanel = new PanelContainer { Position = new Vector2(420f, 122f), Size = new Vector2(380f, 480f) };
		AddChild(_fusePanel);
		var fuseOuter = new MarginContainer();
		fuseOuter.AddThemeConstantOverride("margin_left", 8);
		fuseOuter.AddThemeConstantOverride("margin_right", 8);
		fuseOuter.AddThemeConstantOverride("margin_top", 8);
		fuseOuter.AddThemeConstantOverride("margin_bottom", 8);
		_fusePanel.AddChild(fuseOuter);
		var fuseInner = new VBoxContainer();
		fuseInner.AddThemeConstantOverride("separation", 4);
		fuseOuter.AddChild(fuseInner);
		fuseInner.AddChild(new Label { Text = "Fuse", HorizontalAlignment = HorizontalAlignment.Center });
		fuseInner.AddChild(new Label { Text = "Combine 3 same-rarity relics.", HorizontalAlignment = HorizontalAlignment.Center });
		var fuseScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 340f) };
		fuseInner.AddChild(fuseScroll);
		_fuseStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_fuseStack.AddThemeConstantOverride("separation", 4);
		fuseScroll.AddChild(_fuseStack);

		// Craft panel
		_craftPanel = new PanelContainer { Position = new Vector2(816f, 122f), Size = new Vector2(440f, 480f) };
		AddChild(_craftPanel);
		var craftOuter = new MarginContainer();
		craftOuter.AddThemeConstantOverride("margin_left", 8);
		craftOuter.AddThemeConstantOverride("margin_right", 8);
		craftOuter.AddThemeConstantOverride("margin_top", 8);
		craftOuter.AddThemeConstantOverride("margin_bottom", 8);
		_craftPanel.AddChild(craftOuter);
		var craftInner = new VBoxContainer();
		craftInner.AddThemeConstantOverride("separation", 4);
		craftOuter.AddChild(craftInner);
		craftInner.AddChild(new Label { Text = "Craft", HorizontalAlignment = HorizontalAlignment.Center });
		craftInner.AddChild(new Label { Text = "Forge specific relics with shards + gold.", HorizontalAlignment = HorizontalAlignment.Center });
		var craftScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 340f) };
		craftInner.AddChild(craftScroll);
		_craftStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_craftStack.AddThemeConstantOverride("separation", 4);
		craftScroll.AddChild(_craftStack);

		// Status + nav
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var backBtn = new Button { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		backBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(backBtn);
		var mapBtn = new Button { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		_resourcesLabel.Text = $"Gold: {gs.Gold}  |  Shards: {gs.RelicShards}";
		RebuildDismantlePanel();
		RebuildFusePanel();
		RebuildCraftPanel();
	}

	private void RebuildDismantlePanel()
	{
		foreach (var child in _dismantleStack.GetChildren()) child.QueueFree();
		var owned = GameState.Instance.GetOwnedEquipment();
		foreach (var relicId in owned.OrderBy(id => id))
		{
			var equip = GameData.GetEquipment(relicId);
			if (equip == null) continue;
			var shards = RelicForgeCatalog.GetDismantleShards(equip.Rarity);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			var rarityColor = GetRarityColor(equip.Rarity);
			var label = new Label
			{
				Text = $"{equip.DisplayName} ({equip.Rarity}) -> +{shards} shards",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			label.AddThemeColorOverride("font_color", rarityColor);
			row.AddChild(label);
			var capturedId = relicId;
			var btn = new Button { Text = "Dismantle", CustomMinimumSize = new Vector2(90f, 0f) };
			btn.Pressed += () =>
			{
				if (GameState.Instance.TryDismantleRelic(capturedId, out var gained))
				{
					_statusLabel.Text = $"Dismantled for +{gained} shards.";
					_selectedFuseRelics.Clear();
					RefreshUi();
				}
			};
			row.AddChild(btn);
			_dismantleStack.AddChild(row);
		}

		if (owned.Count == 0)
		{
			_dismantleStack.AddChild(new Label { Text = "No relics to dismantle." });
		}
	}

	private void RebuildFusePanel()
	{
		foreach (var child in _fuseStack.GetChildren()) child.QueueFree();

		// Group owned relics by rarity
		var owned = GameState.Instance.GetOwnedEquipment();
		var byRarity = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var id in owned)
		{
			var equip = GameData.GetEquipment(id);
			if (equip == null) continue;
			var target = RelicForgeCatalog.GetFusionTargetRarity(equip.Rarity);
			if (target == null) continue; // can't fuse epics
			if (!byRarity.ContainsKey(equip.Rarity)) byRarity[equip.Rarity] = new List<string>();
			byRarity[equip.Rarity].Add(id);
		}

		foreach (var (rarity, relics) in byRarity.OrderBy(p => p.Key))
		{
			var targetRarity = RelicForgeCatalog.GetFusionTargetRarity(rarity);
			var label = new Label { Text = $"{rarity} ({relics.Count}/3) -> {targetRarity}" };
			label.AddThemeColorOverride("font_color", GetRarityColor(rarity));
			_fuseStack.AddChild(label);

			if (relics.Count >= RelicForgeCatalog.RelicsRequiredForFusion)
			{
				var capturedRelics = relics.Take(3).ToArray();
				var btn = new Button { Text = $"Fuse 3 {rarity} relics" };
				btn.Pressed += () =>
				{
					if (GameState.Instance.TryFuseRelics(capturedRelics, out var resultId))
					{
						var resultEquip = GameData.GetEquipment(resultId);
						_statusLabel.Text = $"Fused into {resultEquip?.DisplayName ?? resultId}!";
						RefreshUi();
					}
					else
					{
						_statusLabel.Text = "Fusion failed.";
					}
				};
				_fuseStack.AddChild(btn);
			}
		}

		if (byRarity.Count == 0)
		{
			_fuseStack.AddChild(new Label { Text = "No fuseable relics." });
		}
	}

	private void RebuildCraftPanel()
	{
		foreach (var child in _craftStack.GetChildren()) child.QueueFree();
		var owned = GameState.Instance.GetOwnedEquipment();

		foreach (var equip in GameData.GetAllEquipment().OrderBy(e => e.Rarity).ThenBy(e => e.DisplayName))
		{
			if (owned.Contains(equip.Id)) continue;
			var recipe = RelicForgeCatalog.GetCraftRecipe(equip.Id);
			if (recipe == null) continue;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			var rarityColor = GetRarityColor(equip.Rarity);
			var label = new Label
			{
				Text = $"{equip.DisplayName} ({equip.Rarity})  {recipe.ShardCost} shards + {recipe.GoldCost} gold",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			label.AddThemeColorOverride("font_color", rarityColor);
			row.AddChild(label);

			var canAfford = GameState.Instance.RelicShards >= recipe.ShardCost && GameState.Instance.Gold >= recipe.GoldCost;
			var capturedId = equip.Id;
			var btn = new Button { Text = "Craft", CustomMinimumSize = new Vector2(80f, 0f), Disabled = !canAfford };
			btn.Pressed += () =>
			{
				if (GameState.Instance.TryForgeRelic(capturedId, out var msg))
				{
					_statusLabel.Text = msg;
					RefreshUi();
				}
				else
				{
					_statusLabel.Text = msg;
				}
			};
			row.AddChild(btn);
			_craftStack.AddChild(row);
		}
	}

	private static Color GetRarityColor(string rarity)
	{
		return rarity?.ToLowerInvariant() switch
		{
			"rare" => new Color("4488ff"),
			"epic" => new Color("a855f7"),
			_ => new Color("c8c8c8")
		};
	}
}
