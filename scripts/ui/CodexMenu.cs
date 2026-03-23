using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CodexMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _filterPanel = null!;
	private PanelContainer _listPanel = null!;
	private PanelContainer _detailPanel = null!;
	private Label _countLabel = null!;
	private VBoxContainer _entryStack = null!;
	private VBoxContainer _detailStack = null!;
	private string _activeCategory = "All";
	private string? _selectedEntryId;

	private static readonly string[] Categories = { "All", "Enemies", "Bosses", "Units", "Spells", "Relics" };

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _filterPanel, _listPanel, _detailPanel });
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
		AddChild(new ColorRect { Color = new Color("38bdf8"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Codex", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_countLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titleRow.AddChild(_countLabel);

		// Category filter panel
		_filterPanel = new PanelContainer { Position = new Vector2(24f, 112f), Size = new Vector2(1232f, 44f) };
		AddChild(_filterPanel);
		var filterRow = new HBoxContainer();
		filterRow.AddThemeConstantOverride("separation", 8);
		_filterPanel.AddChild(filterRow);
		foreach (var category in Categories)
		{
			var capturedCat = category;
			var btn = new Button { Text = category, CustomMinimumSize = new Vector2(100f, 0f), ToggleMode = true, ButtonPressed = category == "All" };
			btn.Pressed += () =>
			{
				_activeCategory = capturedCat;
				_selectedEntryId = null;
				RefreshUi();
			};
			filterRow.AddChild(btn);
		}

		// Left panel: entry grid
		_listPanel = new PanelContainer { Position = new Vector2(24f, 168f), Size = new Vector2(500f, 440f) };
		AddChild(_listPanel);
		var listOuter = new MarginContainer();
		listOuter.AddThemeConstantOverride("margin_left", 8);
		listOuter.AddThemeConstantOverride("margin_right", 8);
		listOuter.AddThemeConstantOverride("margin_top", 8);
		listOuter.AddThemeConstantOverride("margin_bottom", 8);
		_listPanel.AddChild(listOuter);
		var listInner = new VBoxContainer();
		listInner.AddThemeConstantOverride("separation", 4);
		listOuter.AddChild(listInner);
		listInner.AddChild(new Label { Text = "Entries", HorizontalAlignment = HorizontalAlignment.Center });
		var listScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 370f) };
		listInner.AddChild(listScroll);
		_entryStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_entryStack.AddThemeConstantOverride("separation", 4);
		listScroll.AddChild(_entryStack);

		// Right panel: detail view
		_detailPanel = new PanelContainer { Position = new Vector2(540f, 168f), Size = new Vector2(716f, 440f) };
		AddChild(_detailPanel);
		var detailOuter = new MarginContainer();
		detailOuter.AddThemeConstantOverride("margin_left", 8);
		detailOuter.AddThemeConstantOverride("margin_right", 8);
		detailOuter.AddThemeConstantOverride("margin_top", 8);
		detailOuter.AddThemeConstantOverride("margin_bottom", 8);
		_detailPanel.AddChild(detailOuter);
		var detailInner = new VBoxContainer();
		detailInner.AddThemeConstantOverride("separation", 4);
		detailOuter.AddChild(detailInner);
		var detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 400f) };
		detailInner.AddChild(detailScroll);
		_detailStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_detailStack.AddThemeConstantOverride("separation", 6);
		detailScroll.AddChild(_detailStack);

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
		_countLabel.Text = $"Discovered: {gs.DiscoveredCodexCount}";
		RebuildFilterButtons();
		RebuildEntryList();
		RebuildDetailView();
	}

	private void RebuildFilterButtons()
	{
		var filterRow = _filterPanel.GetChild(0) as HBoxContainer;
		if (filterRow == null) return;
		foreach (var child in filterRow.GetChildren())
		{
			if (child is Button btn)
			{
				btn.ButtonPressed = btn.Text == _activeCategory;
			}
		}
	}

	private void RebuildEntryList()
	{
		foreach (var child in _entryStack.GetChildren()) child.QueueFree();

		var entries = _activeCategory == "All"
			? CodexCatalog.GetAll()
			: CodexCatalog.GetByCategory(_activeCategory);

		var gs = GameState.Instance;

		foreach (var entry in entries.OrderBy(e => e.Title))
		{
			var discovered = gs.IsCodexEntryDiscovered(entry.Id);
			var capturedId = entry.Id;
			var capturedTitle = discovered ? entry.Title : "???";

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var label = new Label
			{
				Text = capturedTitle,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (!discovered)
				label.AddThemeColorOverride("font_color", new Color("606060"));
			else if (_selectedEntryId == capturedId)
				label.AddThemeColorOverride("font_color", new Color("38bdf8"));
			row.AddChild(label);

			var btn = new Button { Text = "View", CustomMinimumSize = new Vector2(60f, 0f), Disabled = !discovered };
			btn.Pressed += () =>
			{
				_selectedEntryId = capturedId;
				RefreshUi();
			};
			row.AddChild(btn);
			_entryStack.AddChild(row);
		}

		if (entries.Count == 0)
		{
			_entryStack.AddChild(new Label { Text = "No entries in this category." });
		}
	}

	private void RebuildDetailView()
	{
		foreach (var child in _detailStack.GetChildren()) child.QueueFree();

		if (_selectedEntryId == null)
		{
			_detailStack.AddChild(new Label { Text = "Select an entry to view details.", HorizontalAlignment = HorizontalAlignment.Center });
			return;
		}

		var gs = GameState.Instance;
		if (!gs.IsCodexEntryDiscovered(_selectedEntryId))
		{
			_detailStack.AddChild(new Label { Text = "Entry not yet discovered." });
			return;
		}

		var allEntries = CodexCatalog.GetAll();
		var entry = allEntries.FirstOrDefault(e => e.Id == _selectedEntryId);
		if (entry == null)
		{
			_detailStack.AddChild(new Label { Text = "Entry not found." });
			return;
		}

		// Title
		var titleLabel = new Label { Text = entry.Title };
		titleLabel.AddThemeColorOverride("font_color", new Color("38bdf8"));
		_detailStack.AddChild(titleLabel);

		// Category
		var catLabel = new Label { Text = $"Category: {entry.Category}" };
		catLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		_detailStack.AddChild(catLabel);

		// Separator
		_detailStack.AddChild(new HSeparator());

		// Lore text
		var loreLabel = new Label
		{
			Text = entry.LoreText,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(680f, 0f)
		};
		_detailStack.AddChild(loreLabel);

		_detailStack.AddChild(new HSeparator());

		// Kill count (relevant for enemies/bosses)
		var killCount = gs.GetCodexKillCount(_selectedEntryId);
		if (killCount > 0)
		{
			var killLabel = new Label { Text = $"Defeated: {killCount} time{(killCount == 1 ? "" : "s")}" };
			killLabel.AddThemeColorOverride("font_color", new Color("f87171"));
			_detailStack.AddChild(killLabel);
		}

		// First seen date
		var firstSeenUnix = gs.GetCodexFirstSeenAt(_selectedEntryId);
		if (firstSeenUnix > 0)
		{
			var firstSeenDt = DateTimeOffset.FromUnixTimeSeconds(firstSeenUnix).LocalDateTime;
			var firstSeenLabel = new Label { Text = $"First Seen: {firstSeenDt:yyyy-MM-dd HH:mm}" };
			firstSeenLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
			_detailStack.AddChild(firstSeenLabel);
		}
	}
}
