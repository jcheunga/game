using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ExpeditionMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _slotsPanel = null!;
	private PanelContainer _catalogPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _slotsStack = null!;
	private VBoxContainer _catalogStack = null!;
	private readonly List<string> _selectedUnitIds = new();

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

        MedievalUi.Apply(this);

		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Expeditions", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

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
		slotsInner.AddChild(RealmUi.SectionTitle("Underway"));
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
		catInner.AddChild(RealmUi.SectionTitle("Available"));
		var catScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
		var mapBtn = new RealmButton { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
		var shopBtn = new RealmButton { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		shopBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(shopBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		RebuildResourcesRow(gs);
		RebuildSlots();
		RebuildCatalog();
	}

	private void RebuildResourcesRow(GameState gs)
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", gs.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", gs.Food.ToString("N0"), new Vector2(24f, 24f)));

		var expeditionsLabel = new Label
		{
			Text = $"Expeditions {gs.ActiveExpeditionCount}/{ExpeditionCatalog.MaxSlots}",
			VerticalAlignment = VerticalAlignment.Center
		};
		expeditionsLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		_resourcesRow.AddChild(expeditionsLabel);
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
			var box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 6);
			box.AddChild(RealmUi.Heading(def.Title, 20));
			if (slot.AssignedUnitIds.Any())
				box.AddChild(BuildUnitBadgeRow(slot.AssignedUnitIds, 36f));

			if (complete)
			{
				var capturedIndex = i;
				var collectBtn = RealmUi.Button("gift", "Collect rewards", () =>
				{
					if (GameState.Instance.TryCollectExpedition(capturedIndex, out var resultMsg))
					{
						_statusLabel.Text = resultMsg;
						RefreshUi();
					}
				}, true);
				collectBtn.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
				box.AddChild(collectBtn);
			}
			else
			{
				box.AddChild(RealmUi.Label($"Returns in {(int)remaining.TotalMinutes}m {remaining.Seconds:00}s", 18, true));
			}

			_slotsStack.AddChild(box);
			_slotsStack.AddChild(new HSeparator());
		}

		var free = ExpeditionCatalog.MaxSlots - expeditions.Count;
		if (expeditions.Count == 0)
			_slotsStack.AddChild(RealmUi.EmptyState("map", "No expeditions underway", $"Send allies who are not in your squad to gather supplies. {free} slots free."));
		else if (free > 0)
			_slotsStack.AddChild(RealmUi.Label(free == 1 ? "1 slot free" : $"{free} slots free", 18, true));
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
			box.AddThemeConstantOverride("separation", 6);
			box.AddChild(RealmUi.KeyValue(def.Title, $"{def.DurationMinutes} min"));
			var title = box.GetChild<HBoxContainer>(0).GetChild<Label>(0);
			RealmUi.Display(title, 20); title.AddThemeColorOverride("font_color", ModalUi.Cream);
			box.AddChild(RealmUi.Label(def.Description, 18, true));
			box.AddChild(BuildRewardRow(def.BaseGoldReward, def.BaseFoodReward, def.RelicDropChance, def.MinUnits, def.MaxUnits));

			var idleUnits = gs.GetOwnedPlayerUnitIds()
				.Where(id => !gs.IsUnitInActiveDeck(id) && !gs.IsUnitOnExpedition(id))
				.ToArray();
			var actions = new HBoxContainer();
			actions.AddThemeConstantOverride("separation", 10);
			box.AddChild(actions);
			var unitPick = idleUnits.Take(def.MaxUnits).ToArray();
			var ready = slotsAvailable > 0 && idleUnits.Length >= def.MinUnits;
			if (ready)
			{
				actions.AddChild(BuildUnitBadgeRow(unitPick, 36f));
				actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			}
			else
			{
				var allies = def.MinUnits == 1 ? "1 ally" : $"{def.MinUnits} allies";
				var reason = RealmUi.Label(slotsAvailable <= 0 ? "All slots are in use." : $"Needs {allies} outside your squad.", 18, true);
				reason.VerticalAlignment = VerticalAlignment.Center;
				actions.AddChild(reason);
			}
			var capturedId = def.Id;
			var sendBtn = RealmUi.Button("flag", "Dispatch", () =>
			{
				if (gs.TryStartExpedition(capturedId, unitPick, out var msg))
				{
					_statusLabel.Text = msg;
					RefreshUi();
				}
				else
				{
					_statusLabel.Text = msg;
				}
			});
			sendBtn.Disabled = !ready;
			actions.AddChild(sendBtn);
			_catalogStack.AddChild(box);
			_catalogStack.AddChild(new HSeparator());
		}
	}

	private double _timerRefresh;

	public override void _Process(double delta)
	{
		// Countdowns tick once a second rather than rebuilding every frame.
		_timerRefresh += delta;
		if (_timerRefresh < 1 || GameState.Instance.GetActiveExpeditions().Count == 0) return;
		_timerRefresh = 0;
		RebuildSlots();
	}

	private static HBoxContainer BuildUnitBadgeRow(IEnumerable<string> unitIds, float badgeSize)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		foreach (var unitId in unitIds)
		{
			row.AddChild(UiBadgeFactory.CreateUnitBadge(TryGetUnit(unitId), new Vector2(badgeSize, badgeSize)));
		}
		row.TooltipText = string.Join(", ", unitIds.Select(ResolveUnitName));
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

	private static string ResolveUnitName(string unitId)
	{
		return TryGetUnit(unitId)?.DisplayName ?? unitId;
	}

	private static HBoxContainer BuildRewardRow(int gold, int food, float relicDropChance, int minUnits, int maxUnits)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		if (gold > 0) row.AddChild(HomeResourceUi.Amount("gold", $"~{gold:N0}", $"About {gold:N0} gold", 28));
		if (food > 0) row.AddChild(HomeResourceUi.Amount("food", $"~{food}", $"About {food} rations", 28));
		var details = $"{(int)(relicDropChance * 100)}% relic chance · {(minUnits == maxUnits ? $"{minUnits}" : $"{minUnits}–{maxUnits}")} allies";
		var label = RealmUi.Label(details, 18, true);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);
		return row;
	}
}
