using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ExpeditionMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _slotsPanel = null!;
	private PanelContainer _catalogPanel = null!;
	private Label _resourcesLabel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _slotsStack = null!;
	private VBoxContainer _catalogStack = null!;
	private readonly List<string> _selectedUnitIds = new();
	private string _selectedExpeditionId = "";

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _slotsPanel, _catalogPanel });
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
		AddChild(new ColorRect { Color = new Color("22c55e"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Expeditions", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titleRow.AddChild(_resourcesLabel);

		// Active slots panel
		_slotsPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(600f, 480f) };
		AddChild(_slotsPanel);
		var slotsOuter = new MarginContainer();
		slotsOuter.AddThemeConstantOverride("margin_left", 8);
		slotsOuter.AddThemeConstantOverride("margin_right", 8);
		slotsOuter.AddThemeConstantOverride("margin_top", 8);
		slotsOuter.AddThemeConstantOverride("margin_bottom", 8);
		_slotsPanel.AddChild(slotsOuter);
		var slotsInner = new VBoxContainer();
		slotsInner.AddThemeConstantOverride("separation", 6);
		slotsOuter.AddChild(slotsInner);
		slotsInner.AddChild(new Label { Text = "Active Expeditions", HorizontalAlignment = HorizontalAlignment.Center });
		_slotsStack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_slotsStack.AddThemeConstantOverride("separation", 8);
		slotsInner.AddChild(_slotsStack);

		// Catalog panel
		_catalogPanel = new PanelContainer { Position = new Vector2(640f, 122f), Size = new Vector2(616f, 480f) };
		AddChild(_catalogPanel);
		var catOuter = new MarginContainer();
		catOuter.AddThemeConstantOverride("margin_left", 8);
		catOuter.AddThemeConstantOverride("margin_right", 8);
		catOuter.AddThemeConstantOverride("margin_top", 8);
		catOuter.AddThemeConstantOverride("margin_bottom", 8);
		_catalogPanel.AddChild(catOuter);
		var catInner = new VBoxContainer();
		catInner.AddThemeConstantOverride("separation", 6);
		catOuter.AddChild(catInner);
		catInner.AddChild(new Label { Text = "Available Expeditions", HorizontalAlignment = HorizontalAlignment.Center });
		var catScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f) };
		catInner.AddChild(catScroll);
		_catalogStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_catalogStack.AddThemeConstantOverride("separation", 6);
		catScroll.AddChild(_catalogStack);

		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mapBtn = new Button { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
		var shopBtn = new Button { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		shopBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(shopBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		_resourcesLabel.Text = $"Gold: {gs.Gold}  |  Food: {gs.Food}  |  Expeditions: {gs.ActiveExpeditionCount}/{ExpeditionCatalog.MaxSlots}";
		RebuildSlots();
		RebuildCatalog();
	}

	private void RebuildSlots()
	{
		foreach (var child in _slotsStack.GetChildren()) child.QueueFree();

		var expeditions = GameState.Instance.GetActiveExpeditions();
		for (var i = 0; i < expeditions.Count; i++)
		{
			var slot = expeditions[i];
			var def = ExpeditionCatalog.Get(slot.ExpeditionId);
			if (def == null) continue;

			var complete = GameState.Instance.IsExpeditionComplete(i);
			var remaining = GameState.Instance.GetExpeditionTimeRemaining(i);
			var unitNames = string.Join(", ", slot.AssignedUnitIds.Select(id =>
			{
				try { return GameData.GetUnit(id)?.DisplayName ?? id; } catch { return id; }
			}));

			var box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 2);
			box.AddChild(new Label { Text = $"{def.Title} — {unitNames}" });

			if (complete)
			{
				var capturedIndex = i;
				var collectBtn = new Button { Text = "Collect Rewards" };
				collectBtn.Pressed += () =>
				{
					if (GameState.Instance.TryCollectExpedition(capturedIndex, out var resultMsg))
					{
						_statusLabel.Text = resultMsg;
						RefreshUi();
					}
				};
				box.AddChild(collectBtn);
			}
			else
			{
				var mins = (int)remaining.TotalMinutes;
				var secs = remaining.Seconds;
				box.AddChild(new Label { Text = $"Time remaining: {mins}m {secs}s" });
			}

			_slotsStack.AddChild(box);
		}

		for (var i = expeditions.Count; i < ExpeditionCatalog.MaxSlots; i++)
		{
			_slotsStack.AddChild(new Label { Text = $"Slot {i + 1}: Empty" });
		}
	}

	private void RebuildCatalog()
	{
		foreach (var child in _catalogStack.GetChildren()) child.QueueFree();
		_selectedUnitIds.Clear();

		var gs = GameState.Instance;
		var slotsAvailable = ExpeditionCatalog.MaxSlots - gs.ActiveExpeditionCount;

		foreach (var def in ExpeditionCatalog.GetAll())
		{
			var box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 4);
			box.AddChild(new Label { Text = $"{def.Title} ({def.DurationMinutes}m)" });
			box.AddChild(new Label { Text = def.Description });
			box.AddChild(new Label { Text = $"Reward: ~{def.BaseGoldReward} gold, ~{def.BaseFoodReward} food  |  Relic chance: {(int)(def.RelicDropChance * 100)}%" });
			box.AddChild(new Label { Text = $"Units: {def.MinUnits}-{def.MaxUnits}" });

			if (slotsAvailable > 0)
			{
				// Build a unit picker: show idle (owned, not in deck, not on expedition) units
				var idleUnits = gs.GetOwnedPlayerUnitIds()
					.Where(id => !gs.IsUnitInActiveDeck(id) && !gs.IsUnitOnExpedition(id))
					.ToArray();

				if (idleUnits.Length >= def.MinUnits)
				{
					var unitPick = idleUnits.Take(def.MaxUnits).ToArray();
					var unitLabel = string.Join(", ", unitPick.Select(id =>
					{
						try { return GameData.GetUnit(id)?.DisplayName ?? id; } catch { return id; }
					}));
					box.AddChild(new Label { Text = $"Send: {unitLabel}" });

					var capturedId = def.Id;
					var capturedUnits = unitPick;
					var sendBtn = new Button { Text = "Dispatch" };
					sendBtn.Pressed += () =>
					{
						if (gs.TryStartExpedition(capturedId, capturedUnits, out var msg))
						{
							_statusLabel.Text = msg;
							RefreshUi();
						}
						else
						{
							_statusLabel.Text = msg;
						}
					};
					box.AddChild(sendBtn);
				}
				else
				{
					box.AddChild(new Label { Text = "Not enough idle units." });
				}
			}
			else
			{
				box.AddChild(new Label { Text = "All slots in use." });
			}

			_catalogStack.AddChild(box);
			_catalogStack.AddChild(new HSeparator());
		}
	}

	public override void _Process(double delta)
	{
		// Refresh timer displays periodically
		var expeditions = GameState.Instance.GetActiveExpeditions();
		if (expeditions.Count > 0)
		{
			RebuildSlots();
		}
	}

	private IEnumerable<string> GetOwnedPlayerUnitIds()
	{
		// Helper; GameState should expose this
		return GameState.Instance.GetOwnedPlayerUnitIds();
	}
}
