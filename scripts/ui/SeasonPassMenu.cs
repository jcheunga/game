using System;
using Godot;

public partial class SeasonPassMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _trackPanel = null!;
	private PanelContainer _progressPanel = null!;
	private PanelContainer _bottomPanel = null!;
	private Label _tierXpLabel = null!;
	private Label _statusLabel = null!;
	private ProgressBar _xpBar = null!;
	private Label _xpBarLabel = null!;
	private HBoxContainer _tierStrip = null!;
	private Button _upgradeBtn = null!;

	private static readonly Color ColorClaimed = new("9fd49a");

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _trackPanel, _progressPanel, _bottomPanel });
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

		// ── Title panel ──
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label
		{
			Text = "Season Pass",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});
		titleRow.AddChild(new Label
		{
			Text = $"Season {SeasonPassCatalog.CurrentSeasonId}",
			VerticalAlignment = VerticalAlignment.Center
		});
		_tierXpLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		titleRow.AddChild(_tierXpLabel);

		// ── Tier track panel ──
		_trackPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(1232f, 470f) };
		AddChild(_trackPanel);
		var trackMargin = new MarginContainer();
		trackMargin.AddThemeConstantOverride("margin_left", 8);
		trackMargin.AddThemeConstantOverride("margin_right", 8);
		trackMargin.AddThemeConstantOverride("margin_top", 8);
		trackMargin.AddThemeConstantOverride("margin_bottom", 8);
		_trackPanel.AddChild(trackMargin);

		var trackVBox = new VBoxContainer();
		trackVBox.AddThemeConstantOverride("separation", 6);
		trackMargin.AddChild(trackVBox);

		_xpBarLabel = RealmUi.Label("", 18, true);
		trackVBox.AddChild(_xpBarLabel);
		_xpBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0f, 10f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ShowPercentage = false
		};
		trackVBox.AddChild(_xpBar);

		var trackRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		trackRow.AddThemeConstantOverride("separation", 10);
		trackVBox.AddChild(trackRow);
		// Row names line up with the tier columns: header, free reward, premium reward.
		var legend = new VBoxContainer { CustomMinimumSize = new Vector2(96f, 0f) };
		legend.AddThemeConstantOverride("separation", TrackGap);
		trackRow.AddChild(legend);
		var header = new Control { CustomMinimumSize = new Vector2(0f, TierHeaderHeight) };
		header.SetMeta(RealmModal.KeepMinimum, true);
		legend.AddChild(header);
		foreach (var name in new[] { "Free", "Premium" })
		{
			var label = RealmUi.Label(name, 18, true);
			label.CustomMinimumSize = new Vector2(0f, RewardHeight);
			label.AutowrapMode = TextServer.AutowrapMode.Off;
			label.SetMeta(RealmModal.KeepMinimum, true);
			label.VerticalAlignment = VerticalAlignment.Center;
			legend.AddChild(label);
		}
		var trackScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 220f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		trackRow.AddChild(trackScroll);
		_tierStrip = new HBoxContainer();
		_tierStrip.AddThemeConstantOverride("separation", 8);
		trackScroll.AddChild(_tierStrip);

		// The progress readout now sits above the track; this panel only reserves the old layout band.
		_progressPanel = new PanelContainer { Visible = false };
		_progressPanel.SetMeta("modal_hidden", true);
		AddChild(_progressPanel);

		// ── Bottom section: upgrade + status + nav ──
		_bottomPanel = new PanelContainer { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 80f) };
		AddChild(_bottomPanel);
		var bottomMargin = new MarginContainer();
		bottomMargin.AddThemeConstantOverride("margin_left", 8);
		bottomMargin.AddThemeConstantOverride("margin_right", 8);
		bottomMargin.AddThemeConstantOverride("margin_top", 4);
		bottomMargin.AddThemeConstantOverride("margin_bottom", 4);
		_bottomPanel.AddChild(bottomMargin);
		var bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 12);
		bottomMargin.AddChild(bottomRow);

		_upgradeBtn = new RealmButton
		{
			Text = "Unlock premium",
			CustomMinimumSize = new Vector2(200f, 0f),
			Visible = !GameState.Instance.HasPremiumPass
		};
		_upgradeBtn.Pressed += OnUpgradePremium;
		bottomRow.AddChild(_upgradeBtn);

		_statusLabel = new Label
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		bottomRow.AddChild(_statusLabel);

		var mainMenuBtn = new RealmButton { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainMenuBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainMenuBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var currentTier = gs.SeasonPassTier;
		var currentXp = gs.SeasonPassXP;

		_tierXpLabel.Text = $"Tier {currentTier} · {currentXp:N0} XP";
		_upgradeBtn.Visible = !gs.HasPremiumPass;

		// XP progress toward next tier
		var nextTier = Math.Min(currentTier + 1, 50);
		var xpForCurrent = currentTier > 0 ? SeasonPassCatalog.GetXPForTier(currentTier) : 0;
		var xpForNext = SeasonPassCatalog.GetXPForTier(nextTier);
		var xpRange = Math.Max(1, xpForNext - xpForCurrent);
		var xpProgress = currentXp - xpForCurrent;
		_xpBar.MinValue = 0;
		_xpBar.MaxValue = xpRange;
		_xpBar.Value = currentTier >= 50 ? xpRange : Math.Clamp(xpProgress, 0, xpRange);
		_xpBarLabel.Text = currentTier >= 50
			? $"Tier {currentTier} · maximum tier reached"
			: $"Tier {currentTier} · {Math.Max(0, xpProgress):N0}/{xpRange:N0} XP to tier {nextTier}";

		RebuildTierStrip();
	}

	private void RebuildTierStrip()
	{
		foreach (var child in _tierStrip.GetChildren()) child.QueueFree();

		var tiers = SeasonPassCatalog.GetAll();
		var gs = GameState.Instance;
		var currentTier = gs.SeasonPassTier;

		foreach (var tier in tiers)
		{
			var col = new VBoxContainer { CustomMinimumSize = new Vector2(128f, 0f) };
			col.AddThemeConstantOverride("separation", TrackGap);

			var tierLabel = RealmUi.Label($"Tier {tier.Tier}", 18, tier.Tier > currentTier);
			tierLabel.HorizontalAlignment = HorizontalAlignment.Center;
			tierLabel.CustomMinimumSize = new Vector2(0f, TierHeaderHeight);
			tierLabel.VerticalAlignment = VerticalAlignment.Center;
			tierLabel.SetMeta(RealmModal.KeepMinimum, true);
			if (tier.Tier == currentTier) tierLabel.AddThemeColorOverride("font_color", RealmUi.Gold);
			col.AddChild(tierLabel);

			var freeClaimed = gs.HasClaimedSeasonFreeTier(tier.Tier);
			var capturedTierFree = tier.Tier;
			col.AddChild(RewardButton(tier.FreeRewardType, "", tier.FreeRewardLabel, freeClaimed, tier.Tier <= currentTier,
				() => OnClaimReward(capturedTierFree, isPremium: false)));

			var premClaimed = gs.HasClaimedSeasonPremiumTier(tier.Tier);
			var capturedTierPrem = tier.Tier;
			col.AddChild(RewardButton(tier.PremiumRewardType, tier.PremiumRewardItemId, tier.PremiumRewardLabel, premClaimed,
				tier.Tier <= currentTier && gs.HasPremiumPass, () => OnClaimReward(capturedTierPrem, isPremium: true)));

			_tierStrip.AddChild(col);
		}
	}

	private const int TrackGap = 8, TierHeaderHeight = 32, RewardHeight = 64;

	// The reward's icon and amount form the button; an unlocked reward is the primary action.
	private Button RewardButton(string type, string itemId, string label, bool claimed, bool unlocked, Action claim)
	{
		var button = new RealmButton
		{
			Text = claimed ? "Claimed" : label,
			Icon = UiArtLoader.TryLoadRewardIcon(type, itemId),
			ExpandIcon = true,
			CenterIconAndText = true,
			CustomMinimumSize = new Vector2(128f, RewardHeight),
			Disabled = claimed || !unlocked,
			TooltipText = claimed ? $"{label} · claimed" : unlocked ? $"Claim {label}" : label,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		button.AddThemeConstantOverride("icon_max_width", 28);
		button.SetMeta("painted_resource_icon", true);
		if (unlocked && !claimed) button.SetMeta("realm_primary", true);
		button.Pressed += claim;
		return button;
	}

	private void OnClaimReward(int tier, bool isPremium)
	{
		if (GameState.Instance.TryClaimSeasonReward(tier, isPremium, out var message))
		{
			_statusLabel.Text = message;
			_statusLabel.AddThemeColorOverride("font_color", ColorClaimed);
			RefreshUi();
		}
		else
		{
			_statusLabel.Text = message;
			_statusLabel.AddThemeColorOverride("font_color", new Color("e58f84"));
		}
	}

	private void OnUpgradePremium()
	{
		_statusLabel.Text = "Opening premium purchase...";
		// Delegate to the shop flow; after purchase GameState.HasPremiumPass
		// will be true and RefreshUi will unlock premium rows.
		SceneRouter.Instance.GoToMainMenu();
	}
}
