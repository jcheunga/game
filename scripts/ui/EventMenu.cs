using System;
using System.Linq;
using Godot;

public partial class EventMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _stagesPanel = null!;
	private PanelContainer _milestonesPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _stagesStack = null!;
	private VBoxContainer _milestonesStack = null!;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _stagesPanel, _milestonesPanel });
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
		var evt = GameState.Instance.GetActiveEvent();
		var bannerColor = evt != null ? new Color(evt.BannerColorHex) : new Color("cc6622");
		MenuBackdropComposer.AddSplitBackdrop(this, "event", new Color("1a1a2e"), new Color("16213e"), bannerColor, 104f);

		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);

		titleRow.AddChild(new Label
		{
			Text = evt?.Title ?? "No Active Event",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

		// Stages panel
		_stagesPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(700f, 480f) };
		AddChild(_stagesPanel);
		var stagesOuter = new MarginContainer();
		stagesOuter.AddThemeConstantOverride("margin_left", 8);
		stagesOuter.AddThemeConstantOverride("margin_right", 8);
		stagesOuter.AddThemeConstantOverride("margin_top", 8);
		stagesOuter.AddThemeConstantOverride("margin_bottom", 8);
		_stagesPanel.AddChild(stagesOuter);
		var stagesInner = new VBoxContainer();
		stagesInner.AddThemeConstantOverride("separation", 6);
		stagesOuter.AddChild(stagesInner);
		stagesInner.AddChild(new Label { Text = "Event Stages", HorizontalAlignment = HorizontalAlignment.Center });
		stagesInner.AddChild(new Label { Text = evt?.Description ?? "", HorizontalAlignment = HorizontalAlignment.Center });
		var stagesScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 360f) };
		stagesInner.AddChild(stagesScroll);
		_stagesStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_stagesStack.AddThemeConstantOverride("separation", 6);
		stagesScroll.AddChild(_stagesStack);

		// Milestones panel
		_milestonesPanel = new PanelContainer { Position = new Vector2(740f, 122f), Size = new Vector2(516f, 480f) };
		AddChild(_milestonesPanel);
		var msOuter = new MarginContainer();
		msOuter.AddThemeConstantOverride("margin_left", 8);
		msOuter.AddThemeConstantOverride("margin_right", 8);
		msOuter.AddThemeConstantOverride("margin_top", 8);
		msOuter.AddThemeConstantOverride("margin_bottom", 8);
		_milestonesPanel.AddChild(msOuter);
		var msInner = new VBoxContainer();
		msInner.AddThemeConstantOverride("separation", 6);
		msOuter.AddChild(msInner);
		msInner.AddChild(new Label { Text = "Milestones", HorizontalAlignment = HorizontalAlignment.Center });
		_milestonesStack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_milestonesStack.AddThemeConstantOverride("separation", 8);
		msInner.AddChild(_milestonesStack);

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
		RebuildResourcesRow(gs);

		var evt = gs.GetActiveEvent();
		RebuildStages(evt);
		RebuildMilestones(evt);
	}

	private void RebuildResourcesRow(GameState gs)
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", gs.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", gs.Food.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("sigils", "", gs.Sigils.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void RebuildStages(SeasonalEventDefinition evt)
	{
		foreach (var child in _stagesStack.GetChildren()) child.QueueFree();

		if (evt == null)
		{
			_stagesStack.AddChild(new Label { Text = "No event is active right now. Check back later!" });
			return;
		}

		var gs = GameState.Instance;
		var progress = gs.GetEventProgress(evt.Id);

		for (var i = 0; i < evt.Stages.Length; i++)
		{
			var stage = evt.Stages[i];
			var cleared = i < progress;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var stageData = GameData.GetStage(stage.BaseStageNumber);
			var stageName = stageData?.MapId ?? $"Stage {stage.BaseStageNumber}";
			var modifiers = stage.ForcedModifierIds.Length > 0 ? $"  [{string.Join(", ", stage.ForcedModifierIds)}]" : "";
			var scaleText = $"HP x{stage.EnemyHealthScale:F2}, DMG x{stage.EnemyDamageScale:F2}";

			var statusText = cleared ? "[CLEARED]" : (i == progress ? "[NEXT]" : "[LOCKED]");
			var rewardText = FormatReward(stage.CompletionReward);

			var label = new Label
			{
				Text = $"{i + 1}. {stageName} — {scaleText}{modifiers}  {statusText}  Reward: {rewardText}",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (cleared) label.AddThemeColorOverride("font_color", new Color("60d060"));
			row.AddChild(UiBadgeFactory.CreateRewardBadge(
				stage.CompletionReward?.Type,
				stage.CompletionReward?.ItemId,
				rewardText,
				new Vector2(30f, 30f)));
			row.AddChild(label);

			if (i == progress && !cleared)
			{
				var capturedEventId = evt.Id;
				var capturedIndex = i;
				var btn = new Button { Text = "Deploy" };
				btn.Pressed += () =>
				{
					gs.PrepareEventBattle(capturedEventId, capturedIndex);
					SceneRouter.Instance.GoToLoadout();
				};
				row.AddChild(btn);
			}

			_stagesStack.AddChild(row);
		}

		if (DateTime.TryParse(evt.EndDate, out var endDate))
		{
			var remaining = endDate.Date - DateTime.UtcNow.Date;
			if (remaining.TotalDays > 0)
			{
				_statusLabel.Text = $"Event ends in {(int)remaining.TotalDays} day(s).";
			}
			else
			{
				_statusLabel.Text = "Event ends today!";
			}
		}
	}

	private void RebuildMilestones(SeasonalEventDefinition evt)
	{
		foreach (var child in _milestonesStack.GetChildren()) child.QueueFree();

		if (evt == null)
		{
			_milestonesStack.AddChild(new Label { Text = "No milestones available." });
			return;
		}

		var gs = GameState.Instance;
		var progress = gs.GetEventProgress(evt.Id);

		for (var i = 0; i < evt.Milestones.Length; i++)
		{
			var ms = evt.Milestones[i];
			var claimed = gs.HasClaimedEventReward(evt.Id, i);
			var reachable = progress >= ms.StagesRequired;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var statusIcon = claimed ? "[CLAIMED]" : (reachable ? "[READY]" : $"[{progress}/{ms.StagesRequired}]");
			var rewardText = FormatReward(ms.Reward);
			var label = new Label
			{
				Text = $"{ms.Label}  {statusIcon}  Reward: {rewardText}",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center
			};
			if (claimed) label.AddThemeColorOverride("font_color", new Color("60d060"));
			row.AddChild(UiBadgeFactory.CreateRewardBadge(ms.Reward?.Type, ms.Reward?.ItemId, rewardText, new Vector2(30f, 30f)));
			row.AddChild(label);

			if (reachable && !claimed)
			{
				var capturedEventId = evt.Id;
				var capturedIndex = i;
				var btn = new Button { Text = "Claim" };
				btn.Pressed += () =>
				{
					if (gs.TryClaimEventReward(capturedEventId, capturedIndex, out var msg))
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
			}

			_milestonesStack.AddChild(row);
		}

		// Progress bar
		var totalStages = evt.Stages.Length;
		_milestonesStack.AddChild(new Label { Text = $"\nProgress: {progress}/{totalStages} stages cleared" });
	}

	private static string FormatReward(SeasonalEventReward reward)
	{
		if (reward == null) return "none";
		return reward.Type?.ToLowerInvariant() switch
		{
			"gold" => $"+{reward.Amount} gold",
			"food" => $"+{reward.Amount} food",
			"sigils" => $"+{reward.Amount} sigils",
			"shards" => $"+{reward.Amount} shards",
			"relic" => GameData.GetEquipment(reward.ItemId)?.DisplayName ?? reward.ItemId,
			_ => reward.Type ?? "unknown"
		};
	}
}
