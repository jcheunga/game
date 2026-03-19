using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class DebugConsole : CanvasLayer
{
	public static DebugConsole Instance { get; private set; }

	private bool _visible;
	private PanelContainer _panel;
	private LineEdit _input;
	private Label _output;
	private readonly List<string> _history = new();
	private int _historyIndex = -1;
	private const int MaxOutputLines = 20;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		Layer = 200;
		BuildUi();
		_panel.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Quoteleft)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}

		if (!_visible) return;

		if (@event is InputEventKey navKey && navKey.Pressed)
		{
			if (navKey.Keycode == Key.Up && _history.Count > 0)
			{
				_historyIndex = Math.Max(0, _historyIndex - 1);
				_input.Text = _history[_historyIndex];
				_input.CaretColumn = _input.Text.Length;
				GetViewport().SetInputAsHandled();
			}
			else if (navKey.Keycode == Key.Down && _history.Count > 0)
			{
				_historyIndex = Math.Min(_history.Count, _historyIndex + 1);
				_input.Text = _historyIndex < _history.Count ? _history[_historyIndex] : "";
				_input.CaretColumn = _input.Text.Length;
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void Toggle()
	{
		_visible = !_visible;
		_panel.Visible = _visible;
		if (_visible)
		{
			_input.GrabFocus();
		}
	}

	private void BuildUi()
	{
		_panel = new PanelContainer
		{
			AnchorRight = 1f,
			AnchorBottom = 0.4f,
			OffsetLeft = 10f,
			OffsetTop = 10f,
			OffsetRight = -10f
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0.88f),
			BorderColor = new Color("e2b714"),
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 8,
			ContentMarginBottom = 8
		};
		_panel.AddThemeStyleboxOverride("panel", style);
		AddChild(_panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		_panel.AddChild(vbox);

		var headerLabel = new Label
		{
			Text = "Debug Console (` to toggle, type 'help' for commands)"
		};
		headerLabel.AddThemeColorOverride("font_color", new Color("e2b714"));
		vbox.AddChild(headerLabel);

		_output = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		_output.AddThemeColorOverride("font_color", new Color("8b949e"));
		vbox.AddChild(_output);

		_input = new LineEdit
		{
			PlaceholderText = "Enter command...",
			CustomMinimumSize = new Vector2(0f, 36f)
		};
		_input.TextSubmitted += OnSubmit;
		vbox.AddChild(_input);
	}

	private void OnSubmit(string text)
	{
		_input.Clear();
		if (string.IsNullOrWhiteSpace(text)) return;

		_history.Add(text);
		_historyIndex = _history.Count;
		var result = Execute(text.Trim());
		AppendOutput($"> {text}");
		if (!string.IsNullOrWhiteSpace(result))
			AppendOutput(result);
	}

	private void AppendOutput(string line)
	{
		var lines = (_output.Text ?? "").Split('\n').ToList();
		lines.Add(line);
		while (lines.Count > MaxOutputLines)
			lines.RemoveAt(0);
		_output.Text = string.Join("\n", lines);
	}

	private static string Execute(string command)
	{
		var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return "";

		var cmd = parts[0].ToLowerInvariant();
		var args = parts.Skip(1).ToArray();

		return cmd switch
		{
			"help" => "Commands: help, gold <n>, food <n>, unlock <stage>, unlockall, reset, stage <n>, " +
				"difficulty <id>, speed <1-3>, stats, units, spells, stages, achievement <id>, " +
				"relic <id>, equip <unitId> <relicId>, cloud upload, cloud download, analytics flush, screenshot",
			"gold" => SetGold(args),
			"food" => SetFood(args),
			"unlock" => UnlockStage(args),
			"unlockall" => UnlockAll(),
			"reset" => ResetProgress(),
			"stage" => SelectStage(args),
			"difficulty" => SetDifficulty(args),
			"speed" => SetSpeed(args),
			"stats" => ShowStats(),
			"units" => ShowUnits(),
			"spells" => ShowSpells(),
			"stages" => ShowStages(args),
			"achievement" => GrantAchievement(args),
			"relic" => GrantRelic(args),
			"equip" => EquipRelic(args),
			"cloud" => CloudCommand(args),
			"analytics" => AnalyticsCommand(args),
			"screenshot" => TakeScreenshot(),
			_ => $"Unknown command: {cmd}. Type 'help' for available commands."
		};
	}

	private static string SetGold(string[] args)
	{
		if (args.Length == 0 || !int.TryParse(args[0], out var amount))
			return "Usage: gold <amount>";
		var gs = GameState.Instance;
		if (gs == null) return "GameState not available.";
		// Use the purchase reward system to add gold
		gs.TryApplyPurchaseReward(new PurchaseValidationResult
		{
			Status = "ok", ProductId = "debug_gold", GoldCredited = amount
		});
		return $"Added {amount} gold. Total: {gs.Gold}";
	}

	private static string SetFood(string[] args)
	{
		if (args.Length == 0 || !int.TryParse(args[0], out var amount))
			return "Usage: food <amount>";
		var gs = GameState.Instance;
		if (gs == null) return "GameState not available.";
		gs.TryApplyPurchaseReward(new PurchaseValidationResult
		{
			Status = "ok", ProductId = "debug_food", FoodCredited = amount
		});
		return $"Added {amount} food. Total: {gs.Food}";
	}

	private static string UnlockStage(string[] args)
	{
		if (args.Length == 0 || !int.TryParse(args[0], out var stage))
			return "Usage: unlock <stage_number>";
		var gs = GameState.Instance;
		if (gs == null) return "GameState not available.";
		// Grant a fake victory to unlock the stage
		for (var s = gs.HighestUnlockedStage; s < stage; s++)
		{
			gs.ApplyVictory(s, 0, 0, 3);
		}
		return $"Unlocked up to stage {gs.HighestUnlockedStage}.";
	}

	private static string UnlockAll()
	{
		var gs = GameState.Instance;
		if (gs == null) return "GameState not available.";
		for (var s = gs.HighestUnlockedStage; s <= gs.MaxStage; s++)
		{
			gs.ApplyVictory(s, 0, 0, 3);
		}
		return $"All {gs.MaxStage} stages unlocked.";
	}

	private static string ResetProgress()
	{
		GameState.Instance?.ResetProgress();
		return "Progress reset.";
	}

	private static string SelectStage(string[] args)
	{
		if (args.Length == 0 || !int.TryParse(args[0], out var stage))
			return "Usage: stage <number>";
		GameState.Instance?.SetSelectedStage(stage);
		return $"Selected stage {stage}.";
	}

	private static string SetDifficulty(string[] args)
	{
		if (args.Length == 0) return "Usage: difficulty <apprentice|warden|champion|legend>";
		GameState.Instance?.SetDifficulty(args[0]);
		return $"Difficulty set to: {GameState.Instance?.DifficultyId}";
	}

	private static string SetSpeed(string[] args)
	{
		if (args.Length == 0 || !float.TryParse(args[0], out var speed))
			return "Usage: speed <multiplier> (e.g. 1, 1.5, 2, 3)";
		Engine.TimeScale = Mathf.Clamp(speed, 0.25, 5.0);
		return $"Engine.TimeScale = {Engine.TimeScale}";
	}

	private static string ShowStats()
	{
		var gs = GameState.Instance;
		if (gs == null) return "GameState not available.";
		return $"Gold: {gs.Gold}  Food: {gs.Food}\n" +
			$"Stage: {gs.HighestUnlockedStage}/{gs.MaxStage}  Selected: {gs.SelectedStage}\n" +
			$"Difficulty: {gs.DifficultyId}  Purchases: {gs.TotalPurchaseCount}\n" +
			$"Endless best: wave {gs.BestEndlessWave}  Challenges: {gs.ChallengeRuns}\n" +
			$"Daily streak: {gs.DailyStreak}  Achievements: {gs.GetUnlockedAchievementCount()}";
	}

	private static string ShowUnits()
	{
		var units = GameData.GetPlayerUnits();
		var lines = new List<string> { $"Player units ({units.Count}):" };
		foreach (var u in units.OrderBy(x => x.UnlockStage))
		{
			var owned = GameState.Instance?.IsUnitOwned(u.Id) == true ? "owned" : "locked";
			var level = GameState.Instance?.GetUnitLevel(u.Id) ?? 1;
			lines.Add($"  {u.DisplayName} (cost:{u.Cost} hp:{u.MaxHealth:F0} atk:{u.AttackDamage:F0}) unlock:S{u.UnlockStage} [{owned} lv{level}]");
		}
		return string.Join("\n", lines);
	}

	private static string ShowSpells()
	{
		var spells = GameData.GetPlayerSpells();
		var lines = new List<string> { $"Spells ({spells.Count}):" };
		foreach (var s in spells.OrderBy(x => x.UnlockStage))
		{
			var owned = GameState.Instance?.IsSpellOwned(s.Id) == true ? "owned" : "locked";
			lines.Add($"  {s.DisplayName} (courage:{s.CourageCost} power:{s.Power:F1} cd:{s.Cooldown:F1}s) unlock:S{s.UnlockStage} [{owned}]");
		}
		return string.Join("\n", lines);
	}

	private static string ShowStages(string[] args)
	{
		var start = 1;
		var end = 10;
		if (args.Length >= 1 && int.TryParse(args[0], out var s)) start = s;
		if (args.Length >= 2 && int.TryParse(args[1], out var e)) end = e;

		var lines = new List<string>();
		for (var i = start; i <= Math.Min(end, GameData.MaxStage); i++)
		{
			var stage = GameData.GetStage(i);
			if (stage == null) continue;
			lines.Add($"  S{i} {stage.StageName} [{stage.MapName}] hp:{stage.EnemyHealthScale:F2}x dmg:{stage.EnemyDamageScale:F2}x gold:{stage.RewardGold} food:{stage.RewardFood} entry:{stage.EntryFoodCost}");
		}
		return lines.Count > 0 ? string.Join("\n", lines) : "No stages in range.";
	}

	private static string GrantAchievement(string[] args)
	{
		if (args.Length == 0) return "Usage: achievement <id>";
		var unlocked = GameState.Instance?.TryUnlockAchievement(args[0]);
		return unlocked == true ? $"Achievement unlocked: {args[0]}" : $"Already unlocked or invalid: {args[0]}";
	}

	private static string GrantRelic(string[] args)
	{
		if (args.Length == 0) return "Usage: relic <id>";
		GameState.Instance?.TryGrantEquipment(args[0]);
		return $"Relic granted: {args[0]}";
	}

	private static string EquipRelic(string[] args)
	{
		if (args.Length < 2) return "Usage: equip <unitId> <relicId>";
		GameState.Instance?.TryEquipItem(args[0], args[1]);
		return $"Equipped {args[1]} on {args[0]}.";
	}

	private static string CloudCommand(string[] args)
	{
		if (args.Length == 0) return "Usage: cloud <upload|download>";
		if (args[0].Equals("upload", StringComparison.OrdinalIgnoreCase))
		{
			CloudSaveService.Upload(out var msg);
			return msg;
		}
		if (args[0].Equals("download", StringComparison.OrdinalIgnoreCase))
		{
			CloudSaveService.Download(out var msg);
			return msg;
		}
		return "Usage: cloud <upload|download>";
	}

	private static string AnalyticsCommand(string[] args)
	{
		if (args.Length > 0 && args[0].Equals("flush", StringComparison.OrdinalIgnoreCase))
		{
			AnalyticsService.TryFlush();
			return "Analytics queue flushed.";
		}
		return "Usage: analytics flush";
	}

	private static string TakeScreenshot()
	{
		var path = ScreenshotCapture.Capture("debug");
		return string.IsNullOrWhiteSpace(path) ? "Screenshot failed." : $"Saved: {path}";
	}
}
