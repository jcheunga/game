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

	private static readonly Color ColorClaimed = new("22c55e");
	private static readonly Color ColorCurrent = new("eab308");
	private static readonly Color ColorFuture = new("555566");
	private static readonly Color ColorPremiumLocked = new("44444a");
	private static readonly Color ColorFreeReward = new("3b82f6");
	private static readonly Color ColorPremiumReward = new("a855f7");

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
		MenuBackdropComposer.AddSplitBackdrop(this, "season_pass", new Color("1a1a2e"), new Color("16213e"), new Color("eab308"), 104f);

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
		_trackPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(1232f, 380f) };
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

		trackVBox.AddChild(new Label
		{
			Text = "Free Reward",
			HorizontalAlignment = HorizontalAlignment.Left
		});

		var trackScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 300f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		trackVBox.AddChild(trackScroll);

		_tierStrip = new HBoxContainer();
		_tierStrip.AddThemeConstantOverride("separation", 6);
		trackScroll.AddChild(_tierStrip);

		// ── XP progress panel ──
		_progressPanel = new PanelContainer { Position = new Vector2(24f, 514f), Size = new Vector2(1232f, 56f) };
		AddChild(_progressPanel);
		var progressMargin = new MarginContainer();
		progressMargin.AddThemeConstantOverride("margin_left", 12);
		progressMargin.AddThemeConstantOverride("margin_right", 12);
		progressMargin.AddThemeConstantOverride("margin_top", 8);
		progressMargin.AddThemeConstantOverride("margin_bottom", 8);
		_progressPanel.AddChild(progressMargin);
		var progressStack = new VBoxContainer();
		progressStack.AddThemeConstantOverride("separation", 2);
		progressMargin.AddChild(progressStack);
		_xpBarLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_xpBarLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		progressStack.AddChild(_xpBarLabel);
		_xpBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0f, 18f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ShowPercentage = false
		};
		progressStack.AddChild(_xpBar);

		// ── Bottom section: upgrade + status + nav ──
		_bottomPanel = new PanelContainer { Position = new Vector2(24f, 582f), Size = new Vector2(1232f, 80f) };
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

		_upgradeBtn = new Button
		{
			Text = "Upgrade to Premium",
			CustomMinimumSize = new Vector2(200f, 0f),
			Visible = !GameState.Instance.HasPremiumPass
		};
		_upgradeBtn.AddThemeColorOverride("font_color", ColorPremiumReward);
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

		var mainMenuBtn = new Button { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainMenuBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainMenuBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var currentTier = gs.SeasonPassTier;
		var currentXp = gs.SeasonPassXP;

		_tierXpLabel.Text = $"Tier {currentTier}  |  XP: {currentXp}";
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
			? "Max Tier Reached!"
			: $"{xpProgress} / {xpRange} XP to Tier {nextTier}";

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
			var col = new VBoxContainer { CustomMinimumSize = new Vector2(110f, 0f) };
			col.AddThemeConstantOverride("separation", 4);

			// Tier number label
			var tierColor = tier.Tier < currentTier ? ColorClaimed
				: tier.Tier == currentTier ? ColorCurrent
				: ColorFuture;
			var tierLabel = new Label
			{
				Text = $"Tier {tier.Tier}",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			tierLabel.AddThemeColorOverride("font_color", tierColor);
			col.AddChild(tierLabel);

			var freePreview = new CenterContainer();
			freePreview.AddChild(UiBadgeFactory.CreateRewardBadge(tier.FreeRewardType, "", tier.FreeRewardLabel, new Vector2(34f, 34f)));
			col.AddChild(freePreview);

			// Free reward button
			var freeClaimed = gs.HasClaimedSeasonFreeTier(tier.Tier);
			var freeUnlocked = tier.Tier <= currentTier;
			var freeBtn = new Button
			{
				Text = freeClaimed ? $"{tier.FreeRewardLabel} [Claimed]" : tier.FreeRewardLabel,
				CustomMinimumSize = new Vector2(110f, 52f),
				Disabled = freeClaimed || !freeUnlocked
			};
			if (freeClaimed)
			{
				freeBtn.AddThemeColorOverride("font_color", ColorClaimed);
			}
			else if (freeUnlocked)
			{
				freeBtn.AddThemeColorOverride("font_color", ColorFreeReward);
			}
			else
			{
				freeBtn.AddThemeColorOverride("font_color", ColorFuture);
			}

			var capturedTierFree = tier.Tier;
			freeBtn.Pressed += () => OnClaimReward(capturedTierFree, isPremium: false);
			col.AddChild(freeBtn);

			var premiumPreview = new CenterContainer();
			premiumPreview.AddChild(UiBadgeFactory.CreateRewardBadge(tier.PremiumRewardType, tier.PremiumRewardItemId, tier.PremiumRewardLabel, new Vector2(34f, 34f)));
			col.AddChild(premiumPreview);

			// Premium reward button
			var premClaimed = gs.HasClaimedSeasonPremiumTier(tier.Tier);
			var premUnlocked = tier.Tier <= currentTier && gs.HasPremiumPass;
			var premBtn = new Button
			{
				Text = premClaimed ? $"{tier.PremiumRewardLabel} [Claimed]" : tier.PremiumRewardLabel,
				CustomMinimumSize = new Vector2(110f, 52f),
				Disabled = premClaimed || !premUnlocked
			};
			if (premClaimed)
			{
				premBtn.AddThemeColorOverride("font_color", ColorClaimed);
			}
			else if (!gs.HasPremiumPass)
			{
				premBtn.AddThemeColorOverride("font_color", ColorPremiumLocked);
			}
			else if (premUnlocked)
			{
				premBtn.AddThemeColorOverride("font_color", ColorPremiumReward);
			}
			else
			{
				premBtn.AddThemeColorOverride("font_color", ColorFuture);
			}

			var capturedTierPrem = tier.Tier;
			premBtn.Pressed += () => OnClaimReward(capturedTierPrem, isPremium: true);
			col.AddChild(premBtn);

			_tierStrip.AddChild(col);
		}
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
			_statusLabel.AddThemeColorOverride("font_color", new Color("ef4444"));
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
