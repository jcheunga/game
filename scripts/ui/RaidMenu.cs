using Godot;

public partial class RaidMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _bossPanel = null!;
	private PanelContainer _milestonesPanel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _bossStack = null!;
	private VBoxContainer _milestonesStack = null!;
	private ProgressBar _hpBar = null!;
	private Label _hpLabel = null!;

	private string _weekId = "";
	private RaidBossDefinition _boss = null!;

	public override void _Ready()
	{
		_weekId = RaidBossCatalog.GetCurrentWeekId();
		_boss = RaidBossCatalog.GetCurrentBoss();
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _bossPanel, _milestonesPanel });
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
		// Background
		AddChild(new ColorRect { Color = new Color("1a1a2e"), Position = Vector2.Zero, Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("16213e"), Position = new Vector2(0f, 360f), Size = new Vector2(1280f, 360f) });
		AddChild(new ColorRect { Color = new Color("60a0ff"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Weekly Raid", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		var weekLabel = new Label
		{
			Text = _weekId,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		weekLabel.AddThemeColorOverride("font_color", new Color("60a0ff"));
		titleRow.AddChild(weekLabel);

		// Left panel — Raid Boss
		_bossPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(600f, 480f) };
		AddChild(_bossPanel);
		var bossOuter = new MarginContainer();
		bossOuter.AddThemeConstantOverride("margin_left", 8);
		bossOuter.AddThemeConstantOverride("margin_right", 8);
		bossOuter.AddThemeConstantOverride("margin_top", 8);
		bossOuter.AddThemeConstantOverride("margin_bottom", 8);
		_bossPanel.AddChild(bossOuter);
		var bossInner = new VBoxContainer();
		bossInner.AddThemeConstantOverride("separation", 6);
		bossOuter.AddChild(bossInner);
		bossInner.AddChild(new Label { Text = "Raid Boss", HorizontalAlignment = HorizontalAlignment.Center });

		_bossStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_bossStack.AddThemeConstantOverride("separation", 8);
		bossInner.AddChild(_bossStack);

		// HP bar (placed in boss inner, below the dynamic stack)
		_hpBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = _boss.TotalHealthPool,
			Value = 0,
			CustomMinimumSize = new Vector2(0f, 28f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		bossInner.AddChild(_hpBar);
		_hpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_hpLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		bossInner.AddChild(_hpLabel);

		// Right panel — Milestones
		_milestonesPanel = new PanelContainer { Position = new Vector2(640f, 122f), Size = new Vector2(616f, 480f) };
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
		var msScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f) };
		msInner.AddChild(msScroll);
		_milestonesStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_milestonesStack.AddThemeConstantOverride("separation", 10);
		msScroll.AddChild(_milestonesStack);

		// Status label
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		// Bottom nav
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

		// In offline mode the community damage is just the player's own contribution.
		var communityDamage = (long)gs.RaidDamageContributed;

		RebuildBossInfo(gs, communityDamage);
		RebuildMilestones(gs, communityDamage);

		// Update HP bar
		_hpBar.MaxValue = _boss.TotalHealthPool;
		_hpBar.Value = Mathf.Min(communityDamage, _boss.TotalHealthPool);
		var pct = _boss.TotalHealthPool > 0 ? (int)(communityDamage * 100 / _boss.TotalHealthPool) : 0;
		_hpLabel.Text = $"{communityDamage:N0} / {_boss.TotalHealthPool:N0}  ({pct}%)";
	}

	private void RebuildBossInfo(GameState gs, long communityDamage)
	{
		foreach (var child in _bossStack.GetChildren()) child.QueueFree();

		// Boss name
		var nameLabel = new Label { Text = _boss.BossName, HorizontalAlignment = HorizontalAlignment.Center };
		nameLabel.AddThemeColorOverride("font_color", new Color("ff6060"));
		_bossStack.AddChild(nameLabel);

		// Lore from codex (if available)
		var codexEntry = CodexCatalog.GetById(_boss.BossUnitId);
		if (codexEntry != null && !string.IsNullOrWhiteSpace(codexEntry.LoreText))
		{
			var loreLabel = new Label
			{
				Text = codexEntry.LoreText,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(0f, 60f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			loreLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
			_bossStack.AddChild(loreLabel);
		}

		_bossStack.AddChild(new HSeparator());

		// Total health pool
		var hpNote = new Label { Text = $"Total Health Pool: {_boss.TotalHealthPool:N0}", HorizontalAlignment = HorizontalAlignment.Center };
		_bossStack.AddChild(hpNote);

		// Player contribution
		var contribLabel = new Label
		{
			Text = $"Your contribution: {gs.RaidDamageContributed:N0} damage",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		contribLabel.AddThemeColorOverride("font_color", new Color("60a0ff"));
		_bossStack.AddChild(contribLabel);
	}

	private void RebuildMilestones(GameState gs, long communityDamage)
	{
		foreach (var child in _milestonesStack.GetChildren()) child.QueueFree();

		var milestones = _boss.Milestones;
		for (var i = 0; i < milestones.Length; i++)
		{
			var ms = milestones[i];
			var reached = communityDamage >= ms.DamageThreshold;
			var claimed = gs.HasClaimedRaidReward(_weekId, i);

			var row = new VBoxContainer();
			row.AddThemeConstantOverride("separation", 2);

			// Milestone label
			var msLabel = new Label { Text = ms.Label };
			if (claimed)
				msLabel.AddThemeColorOverride("font_color", new Color("606060"));
			else if (reached)
				msLabel.AddThemeColorOverride("font_color", new Color("22c55e"));
			else
				msLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
			row.AddChild(msLabel);

			// Reward info
			var rewardText = FormatReward(ms.RewardType, ms.RewardItemId, ms.RewardAmount);
			var rewardLabel = new Label { Text = $"Reward: {rewardText}" };
			rewardLabel.AddThemeColorOverride("font_color", new Color("c8c8c8"));
			row.AddChild(rewardLabel);

			// Threshold
			var threshLabel = new Label { Text = $"Threshold: {ms.DamageThreshold:N0} damage" };
			threshLabel.AddThemeColorOverride("font_color", new Color("707880"));
			row.AddChild(threshLabel);

			// Status + button
			var actionRow = new HBoxContainer();
			actionRow.AddThemeConstantOverride("separation", 8);

			if (claimed)
			{
				var statusTag = new Label { Text = "[Claimed]", VerticalAlignment = VerticalAlignment.Center };
				statusTag.AddThemeColorOverride("font_color", new Color("606060"));
				actionRow.AddChild(statusTag);
			}
			else if (reached)
			{
				var statusTag = new Label { Text = "[Ready]", VerticalAlignment = VerticalAlignment.Center };
				statusTag.AddThemeColorOverride("font_color", new Color("22c55e"));
				actionRow.AddChild(statusTag);

				var capturedIndex = i;
				var claimBtn = new Button { Text = "Claim", CustomMinimumSize = new Vector2(80f, 0f) };
				claimBtn.Pressed += () => OnClaimMilestone(capturedIndex);
				actionRow.AddChild(claimBtn);
			}
			else
			{
				var statusTag = new Label { Text = "[Locked]", VerticalAlignment = VerticalAlignment.Center };
				statusTag.AddThemeColorOverride("font_color", new Color("90a0b0"));
				actionRow.AddChild(statusTag);
			}

			row.AddChild(actionRow);
			_milestonesStack.AddChild(row);

			if (i < milestones.Length - 1)
				_milestonesStack.AddChild(new HSeparator());
		}
	}

	private void OnClaimMilestone(int milestoneIndex)
	{
		var gs = GameState.Instance;
		if (gs.TryClaimRaidReward(_weekId, milestoneIndex, out var message))
		{
			_statusLabel.Text = message;
			RefreshUi();
		}
		else
		{
			_statusLabel.Text = message;
		}
	}

	private static string FormatReward(string rewardType, string rewardItemId, int amount)
	{
		return rewardType.ToLowerInvariant() switch
		{
			"gold" => $"{amount} Gold",
			"essence" => $"{amount} Essence",
			"relic" => !string.IsNullOrWhiteSpace(rewardItemId) ? $"{rewardItemId} x{amount}" : $"Relic x{amount}",
			_ => $"{amount} {rewardType}",
		};
	}
}
