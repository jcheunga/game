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

        MedievalUi.Apply(this);

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
		weekLabel.AddThemeColorOverride("font_color", ModalUi.Muted);
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
		bossInner.AddChild(RealmUi.SectionTitle("This week's boss"));

		_bossStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_bossStack.AddThemeConstantOverride("separation", 8);
		bossInner.AddChild(_bossStack);

		// HP bar (placed in boss inner, below the dynamic stack)
		_hpBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = _boss.TotalHealthPool,
			Value = 0,
			CustomMinimumSize = new Vector2(0f, 10f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ShowPercentage = false,
		};
		_hpLabel = RealmUi.Label("", 18, true);
		bossInner.AddChild(_hpLabel);
		bossInner.AddChild(_hpBar);

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
		msInner.AddChild(RealmUi.SectionTitle("Milestones"));
		var msScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
		var mapBtn = new RealmButton { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
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
		_hpLabel.Text = $"Raid progress · {pct}%";
	}

	private void RebuildBossInfo(GameState gs, long communityDamage)
	{
		foreach (var child in _bossStack.GetChildren()) child.QueueFree();

		var codexEntry = CodexCatalog.GetById(_boss.BossUnitId);
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 16);
		_bossStack.AddChild(header);
		var portrait = codexEntry != null
			? UiBadgeFactory.CreateCodexPortrait(codexEntry, new Vector2(132f, 132f))
			: UiBadgeFactory.CreateMysteryBadge(new Vector2(132f, 132f));
		portrait.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		header.AddChild(portrait);
		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		text.AddThemeConstantOverride("separation", 6);
		header.AddChild(text);
		var nameLabel = RealmUi.Heading(_boss.BossName, 22);
		nameLabel.AddThemeColorOverride("font_color", new Color("e58f84"));
		text.AddChild(nameLabel);
		if (codexEntry != null && !string.IsNullOrWhiteSpace(codexEntry.LoreText))
			text.AddChild(RealmUi.Label(codexEntry.LoreText, 18, true));

		_bossStack.AddChild(RealmUi.KeyValue("Health pool", $"{_boss.TotalHealthPool:N0}"));
		_bossStack.AddChild(RealmUi.KeyValue("Your damage", $"{gs.RaidDamageContributed:N0}"));
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

			// Badge, milestone and reward on the left; its state or claim action on the right.
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			var rewardBadge = BuildRewardBadge(ms);
			if (rewardBadge != null) row.AddChild(rewardBadge);
			var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			text.AddThemeConstantOverride("separation", 2);
			row.AddChild(text);
			var msLabel = RealmUi.Label(ms.Label, 18);
			if (claimed) msLabel.AddThemeColorOverride("font_color", ModalUi.Muted);
			text.AddChild(msLabel);
			text.AddChild(RealmUi.Label($"{FormatReward(ms.RewardType, ms.RewardItemId, ms.RewardAmount)} · {ms.DamageThreshold:N0} damage", 18, true));

			var actionRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
			if (reached && !claimed)
			{
				var capturedIndex = i;
				var claimBtn = RealmUi.Button("gift", "Claim", () => OnClaimMilestone(capturedIndex), true);
				actionRow.AddChild(claimBtn);
			}
			else
			{
				var statusTag = RealmUi.Label(claimed ? "Claimed" : "Locked", 18, true);
				statusTag.AutowrapMode = TextServer.AutowrapMode.Off;
				if (claimed) statusTag.AddThemeColorOverride("font_color", new Color("9fd49a"));
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
			"gold" => $"{amount:N0} gold",
			"essence" => $"{amount} essence",
			"relic" => amount > 1 ? $"{GameData.GetEquipment(rewardItemId)?.DisplayName ?? rewardItemId} ×{amount}" : GameData.GetEquipment(rewardItemId)?.DisplayName ?? rewardItemId,
			"spell" => amount > 1 ? $"{GameData.GetSpell(rewardItemId)?.DisplayName ?? rewardItemId} ×{amount}" : GameData.GetSpell(rewardItemId)?.DisplayName ?? rewardItemId,
			"unit" => amount > 1 ? $"{GameData.GetUnit(rewardItemId)?.DisplayName ?? rewardItemId} ×{amount}" : GameData.GetUnit(rewardItemId)?.DisplayName ?? rewardItemId,
			_ => $"{amount} {rewardType}",
		};
	}

	private static Control BuildRewardBadge(RaidBossMilestone milestone)
	{
		return UiBadgeFactory.CreateRewardBadge(
			milestone.RewardType,
			milestone.RewardItemId,
			FormatReward(milestone.RewardType, milestone.RewardItemId, milestone.RewardAmount),
			new Vector2(34f, 34f));
	}
}
