using System;
using System.Linq;
using Godot;

public partial class GuildMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _infoPanel = null!;
	private PanelContainer _actionsPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _infoStack = null!;
	private VBoxContainer _actionsStack = null!;

	private const int ContributionGoldCost = 50;
	private const int ContributionPointsGain = 10;
	private const int ContributionGuildXpGain = 10;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _infoPanel, _actionsPanel });
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
		MenuBackdropComposer.AddSplitBackdrop(this, "guild", new Color("1a1a2e"), new Color("16213e"), new Color("f59e0b"), 104f);

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Warband", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

		// Info panel (left)
		_infoPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(600f, 480f) };
		AddChild(_infoPanel);
		var infoOuter = new MarginContainer();
		infoOuter.AddThemeConstantOverride("margin_left", 8);
		infoOuter.AddThemeConstantOverride("margin_right", 8);
		infoOuter.AddThemeConstantOverride("margin_top", 8);
		infoOuter.AddThemeConstantOverride("margin_bottom", 8);
		_infoPanel.AddChild(infoOuter);
		var infoInner = new VBoxContainer();
		infoInner.AddThemeConstantOverride("separation", 6);
		infoOuter.AddChild(infoInner);
		infoInner.AddChild(new Label { Text = "Guild Info", HorizontalAlignment = HorizontalAlignment.Center });
		var infoScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 380f) };
		infoInner.AddChild(infoScroll);
		_infoStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_infoStack.AddThemeConstantOverride("separation", 6);
		infoScroll.AddChild(_infoStack);

		// Actions panel (right)
		_actionsPanel = new PanelContainer { Position = new Vector2(640f, 122f), Size = new Vector2(616f, 480f) };
		AddChild(_actionsPanel);
		var actOuter = new MarginContainer();
		actOuter.AddThemeConstantOverride("margin_left", 8);
		actOuter.AddThemeConstantOverride("margin_right", 8);
		actOuter.AddThemeConstantOverride("margin_top", 8);
		actOuter.AddThemeConstantOverride("margin_bottom", 8);
		_actionsPanel.AddChild(actOuter);
		var actInner = new VBoxContainer();
		actInner.AddThemeConstantOverride("separation", 6);
		actOuter.AddChild(actInner);
		actInner.AddChild(new Label { Text = "Actions", HorizontalAlignment = HorizontalAlignment.Center });
		_actionsStack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_actionsStack.AddThemeConstantOverride("separation", 10);
		actInner.AddChild(_actionsStack);

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
		var guild = gs.CachedGuildInfo;
		var hasGuild = !string.IsNullOrWhiteSpace(gs.GuildId) && guild != null;
		RebuildResourcesRow(gs);

		// Update title metrics
		var titleRow = _titlePanel.GetChild<HBoxContainer>(0);
		while (titleRow.GetChildCount() > 2) titleRow.GetChild(titleRow.GetChildCount() - 1).QueueFree();
		titleRow.AddChild(UiBadgeFactory.CreateMetaMetric("guild", hasGuild ? guild!.Name : "No Guild", new Vector2(24f, 24f)));
		if (hasGuild)
		{
			var tierDef = GuildCatalog.GetTier(guild!.Tier);
			titleRow.AddChild(UiBadgeFactory.CreateMetaMetric("members", $"{guild.MemberCount}/{tierDef.MaxMembers}", new Vector2(24f, 24f)));
		}

		RebuildInfo(gs, guild, hasGuild);
		RebuildActions(gs, guild, hasGuild);
	}

	private void RebuildResourcesRow(GameState gs)
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", gs.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", gs.Food.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void RebuildInfo(GameState gs, GuildSnapshot guild, bool hasGuild)
	{
		foreach (var child in _infoStack.GetChildren()) child.QueueFree();

		if (!hasGuild)
		{
			_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("guild", "No guild joined.", new Vector2(26f, 26f)));
			_infoStack.AddChild(new Label { Text = "Create or join one to unlock perks." });
			return;
		}

		var tierDef = GuildCatalog.GetTier(guild!.Tier);
		_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("guild", $"{tierDef.Title} (Tier {guild.Tier})", new Vector2(26f, 26f)));
		_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("guild", $"Experience: {guild.Experience}", new Vector2(26f, 26f)));
		_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("members", $"Members: {guild.MemberCount} / {tierDef.MaxMembers}", new Vector2(26f, 26f)));
		_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("guild", $"Your Contribution: {gs.GuildContributionPoints}", new Vector2(26f, 26f)));

		_infoStack.AddChild(new HSeparator());

		// Weekly goal progress
		if (guild.WeeklyGoalTarget > 0)
		{
			_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("challenge", $"Weekly Goal: {guild.WeeklyGoalType}", new Vector2(26f, 26f)));
			var progressFraction = Mathf.Clamp((float)guild.WeeklyGoalProgress / guild.WeeklyGoalTarget, 0f, 1f);
			var progressBar = new ProgressBar
			{
				MinValue = 0,
				MaxValue = guild.WeeklyGoalTarget,
				Value = guild.WeeklyGoalProgress,
				CustomMinimumSize = new Vector2(0f, 24f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_infoStack.AddChild(progressBar);
			_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("challenge", $"{guild.WeeklyGoalProgress} / {guild.WeeklyGoalTarget}  ({(int)(progressFraction * 100)}%)", new Vector2(26f, 26f)));
		}
		else
		{
			_infoStack.AddChild(UiBadgeFactory.CreateMetaMetric("challenge", "No weekly goal active.", new Vector2(26f, 26f)));
		}

		_infoStack.AddChild(new HSeparator());
		_infoStack.AddChild(new Label { Text = "Perks", HorizontalAlignment = HorizontalAlignment.Center });

		var allPerks = GuildCatalog.GetAllPerks();
		var activePerkIds = guild.ActivePerkIds ?? Array.Empty<string>();

		foreach (var perk in allPerks)
		{
			var active = activePerkIds.Contains(perk.Id) && guild.Tier >= perk.TierRequired;
			var unlocked = guild.Tier >= perk.TierRequired;
			var statusText = active ? "[Active]" : (unlocked ? "[Unlocked]" : $"[Tier {perk.TierRequired}]");

			var perkLabel = new Label { Text = $"{statusText} {perk.Title} - {perk.Description}" };
			if (!unlocked)
			{
				perkLabel.AddThemeColorOverride("font_color", new Color("606060"));
			}
			else if (active)
			{
				perkLabel.AddThemeColorOverride("font_color", new Color("22c55e"));
			}
			_infoStack.AddChild(perkLabel);
		}
	}

	private void RebuildActions(GameState gs, GuildSnapshot guild, bool hasGuild)
	{
		foreach (var child in _actionsStack.GetChildren()) child.QueueFree();

		if (!hasGuild)
		{
			var createBtn = new Button { Text = "Create Guild", CustomMinimumSize = new Vector2(200f, 36f) };
			createBtn.Pressed += OnCreateGuild;
			_actionsStack.AddChild(createBtn);

			var joinBtn = new Button { Text = "Join Guild", CustomMinimumSize = new Vector2(200f, 36f) };
			joinBtn.Pressed += OnJoinGuild;
			_actionsStack.AddChild(joinBtn);
		}
		else
		{
			var contributeBtn = new Button
			{
				Text = $"Contribute ({ContributionGoldCost} Gold)",
				CustomMinimumSize = new Vector2(200f, 36f),
			};
			contributeBtn.Pressed += OnContribute;
			_actionsStack.AddChild(contributeBtn);

			_actionsStack.AddChild(new Label { Text = $"Adds {ContributionPointsGain} contribution + {ContributionGuildXpGain} guild XP" });

			var leaveBtn = new Button { Text = "Leave Guild", CustomMinimumSize = new Vector2(200f, 36f) };
			leaveBtn.Pressed += OnLeaveGuild;
			_actionsStack.AddChild(leaveBtn);
		}
	}

	private void OnCreateGuild()
	{
		var gs = GameState.Instance;
		if (!string.IsNullOrWhiteSpace(gs.GuildId))
		{
			_statusLabel.Text = "You are already in a guild.";
			return;
		}

		var guildId = $"local_guild_{Time.GetTicksMsec()}";
		var snapshot = new GuildSnapshot
		{
			GuildId = guildId,
			Name = "New Warband",
			LeaderProfileId = "player",
			Tier = 1,
			Experience = 0,
			MemberCount = 1,
			ActivePerkIds = new[] { "guild_vitality" },
			WeeklyGoalProgress = 0,
			WeeklyGoalTarget = 100,
			WeeklyGoalType = "Contribute",
		};

		gs.CachedGuildInfo = snapshot;
		gs.SetGuildId(guildId);
		_statusLabel.Text = "Guild created! Welcome to your new Warband.";
		RefreshUi();
	}

	private void OnJoinGuild()
	{
		var gs = GameState.Instance;
		if (!string.IsNullOrWhiteSpace(gs.GuildId))
		{
			_statusLabel.Text = "You are already in a guild.";
			return;
		}

		var guildId = $"mock_guild_{Time.GetTicksMsec()}";
		var snapshot = new GuildSnapshot
		{
			GuildId = guildId,
			Name = "Ironclad Company",
			LeaderProfileId = "npc_leader",
			Tier = 2,
			Experience = 600,
			MemberCount = 7,
			ActivePerkIds = new[] { "guild_vitality", "guild_prosperity" },
			WeeklyGoalProgress = 42,
			WeeklyGoalTarget = 200,
			WeeklyGoalType = "Gold Earned",
		};

		gs.CachedGuildInfo = snapshot;
		gs.SetGuildId(guildId);
		_statusLabel.Text = "Joined Ironclad Company!";
		RefreshUi();
	}

	private void OnContribute()
	{
		var gs = GameState.Instance;
		if (string.IsNullOrWhiteSpace(gs.GuildId) || gs.CachedGuildInfo == null)
		{
			_statusLabel.Text = "You are not in a guild.";
			return;
		}

		if (!gs.TryGuildContribute(ContributionGoldCost, ContributionPointsGain, out var resultMsg))
		{
			_statusLabel.Text = resultMsg;
			return;
		}

		var guild = gs.CachedGuildInfo;
		guild.Experience += ContributionGuildXpGain;
		guild.WeeklyGoalProgress += ContributionPointsGain;

		// Check for tier-up
		var newTierDef = GuildCatalog.GetTierByExperience(guild.Experience);
		if (newTierDef.Tier > guild.Tier)
		{
			guild.Tier = newTierDef.Tier;
			_statusLabel.Text = $"Contributed! Guild promoted to {newTierDef.Title}!";
		}
		else
		{
			_statusLabel.Text = resultMsg;
		}

		RefreshUi();
	}

	private void OnLeaveGuild()
	{
		var gs = GameState.Instance;
		gs.CachedGuildInfo = null;
		gs.SetGuildId("");
		_statusLabel.Text = "You left the guild.";
		RefreshUi();
	}
}
