using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleSummaryMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _combatPanel = null!;
	private PanelContainer _rewardsPanel = null!;
	private PanelContainer _masteryPanel = null!;
	private Button _continueBtn = null!;

	public override void _Ready()
	{
		BuildUi();
		AnimateEntrance(new Control[] { _titlePanel, _combatPanel, _rewardsPanel, _masteryPanel, _continueBtn });
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
		var data = BattleSummaryData.Current;

		if (data == null)
		{
			MenuBackdropComposer.AddSplitBackdrop(this, "battle_summary", new Color("1a1a2e"), new Color("16213e"), new Color("64748b"), 104f);
			BuildNullState();
			return;
		}

		var victoryColor = new Color("22c55e");
		var defeatColor = new Color("ef4444");
		var accentColor = data.Won ? victoryColor : defeatColor;
		MenuBackdropComposer.AddSplitBackdrop(this, "battle_summary", new Color("1a1a2e"), new Color("16213e"), accentColor, 104f);

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label
		{
			Text = "Battle Summary",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});
		var outcomeLabel = new Label
		{
			Text = data.Won ? "VICTORY" : "DEFEAT",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		outcomeLabel.AddThemeColorOverride("font_color", accentColor);
		titleRow.AddChild(outcomeLabel);

		// Three-column panels
		_combatPanel = BuildCombatPanel(data);
		_rewardsPanel = BuildRewardsPanel(data, accentColor);
		_masteryPanel = BuildMasteryPanel(data);

		// Continue button
		_continueBtn = new Button
		{
			Text = "Continue",
			Position = new Vector2(540f, 660f),
			Size = new Vector2(200f, 40f)
		};
		_continueBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		AddChild(_continueBtn);
	}

	private void BuildNullState()
	{
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		_titlePanel.AddChild(new Label
		{
			Text = "No battle data",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		});

		_combatPanel = new PanelContainer { Visible = false };
		_rewardsPanel = new PanelContainer { Visible = false };
		_masteryPanel = new PanelContainer { Visible = false };

		_continueBtn = new Button
		{
			Text = "Continue",
			Position = new Vector2(540f, 660f),
			Size = new Vector2(200f, 40f)
		};
		_continueBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		AddChild(_continueBtn);
	}

	private PanelContainer BuildCombatPanel(BattleSummaryData data)
	{
		var panel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(380f, 520f) };
		AddChild(panel);

		var outer = CreateMarginContainer(8);
		panel.AddChild(outer);
		var stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 6);
		outer.AddChild(stack);

		stack.AddChild(CreateSectionHeader("Combat Stats"));

		var minutes = (int)data.ElapsedSeconds / 60;
		var seconds = (int)data.ElapsedSeconds % 60;
		var timeStr = $"{minutes}:{seconds:D2}";

		stack.AddChild(CreateStatRow("Enemies Defeated", data.EnemiesDefeated.ToString()));
		stack.AddChild(CreateStatRow("Bosses Killed", data.BossesKilled.ToString()));
		stack.AddChild(CreateStatRow("Units Deployed", data.UnitsDeployed.ToString()));
		stack.AddChild(CreateStatRow("Units Lost", data.UnitsLost.ToString()));
		stack.AddChild(CreateStatRow("Spells Cast", data.SpellsCast.ToString()));
		stack.AddChild(CreateStatRow("Damage Dealt", data.TotalDamageDealt.ToString("N0")));
		stack.AddChild(CreateStatRow("Damage Taken", data.TotalDamageTaken.ToString("N0")));
		stack.AddChild(CreateStatRow("Time Elapsed", timeStr));

		return panel;
	}

	private PanelContainer BuildRewardsPanel(BattleSummaryData data, Color accentColor)
	{
		var panel = new PanelContainer { Position = new Vector2(420f, 122f), Size = new Vector2(380f, 520f) };
		AddChild(panel);

		var outer = CreateMarginContainer(8);
		panel.AddChild(outer);
		var stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 6);
		outer.AddChild(stack);

		stack.AddChild(CreateSectionHeader("Rewards"));

		stack.AddChild(CreateRewardSummaryRow("gold", "Gold Earned", data.GoldEarned.ToString()));
		stack.AddChild(CreateRewardSummaryRow("food", "Food Earned", data.FoodEarned.ToString()));
		stack.AddChild(CreateRewardSummaryRow("season_xp", "Season XP", data.SeasonXPEarned.ToString()));

		// Star rating
		var starText = "";
		for (var i = 0; i < 3; i++)
			starText += i < data.StarsEarned ? "\u2605" : "\u2606";
		var starLabel = new Label
		{
			Text = starText,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		starLabel.AddThemeColorOverride("font_color", accentColor);
		stack.AddChild(starLabel);

		// Mutator gold multiplier
		if (data.MutatorGoldMultiplier > 1f)
		{
			var mutatorLabel = new Label
			{
				Text = $"Mutator Gold: x{data.MutatorGoldMultiplier:F1}",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			mutatorLabel.AddThemeColorOverride("font_color", new Color("fbbf24"));
			stack.AddChild(mutatorLabel);
		}

		return panel;
	}

	private PanelContainer BuildMasteryPanel(BattleSummaryData data)
	{
		var panel = new PanelContainer { Position = new Vector2(816f, 122f), Size = new Vector2(440f, 520f) };
		AddChild(panel);

		var outer = CreateMarginContainer(8);
		panel.AddChild(outer);
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		outer.AddChild(inner);

		inner.AddChild(CreateSectionHeader("Unit Mastery"));

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 420f) };
		inner.AddChild(scroll);
		var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		stack.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(stack);

		if (data.MasteryXPPerUnit == null || data.MasteryXPPerUnit.Count == 0)
		{
			stack.AddChild(new Label { Text = "No mastery XP earned." });
		}
		else
		{
			foreach (var (unitId, xp) in data.MasteryXPPerUnit.OrderByDescending(kvp => kvp.Value))
			{
				stack.AddChild(CreateMasteryRow(unitId, xp));
			}
		}

		return panel;
	}

	private static MarginContainer CreateMarginContainer(int margin)
	{
		var container = new MarginContainer();
		container.AddThemeConstantOverride("margin_left", margin);
		container.AddThemeConstantOverride("margin_right", margin);
		container.AddThemeConstantOverride("margin_top", margin);
		container.AddThemeConstantOverride("margin_bottom", margin);
		return container;
	}

	private static Label CreateSectionHeader(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeColorOverride("font_color", new Color("e2e8f0"));
		return label;
	}

	private static HBoxContainer CreateStatRow(string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var nameLabel = new Label
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};
		nameLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		row.AddChild(nameLabel);
		var valueLabel = new Label
		{
			Text = value,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		row.AddChild(valueLabel);
		return row;
	}

	private static HBoxContainer CreateMasteryRow(string unitId, int xp)
	{
		var unit = GameData.GetUnit(unitId);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(40f, 40f)));

		var nameLabel = new Label
		{
			Text = unit?.DisplayName ?? unitId,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};
		nameLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		row.AddChild(nameLabel);

		var valueLabel = new Label
		{
			Text = $"+{xp} XP",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		row.AddChild(valueLabel);
		return row;
	}

	private static HBoxContainer CreateRewardSummaryRow(string rewardType, string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(UiBadgeFactory.CreateRewardBadge(rewardType, "", label, new Vector2(36f, 36f)));

		var nameLabel = new Label
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};
		nameLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		row.AddChild(nameLabel);

		var valueLabel = new Label
		{
			Text = value,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		row.AddChild(valueLabel);
		return row;
	}
}
