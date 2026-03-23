using System;
using Godot;

public partial class BountyMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _bountyCard0 = null!;
	private PanelContainer _bountyCard1 = null!;
	private PanelContainer _bountyCard2 = null!;
	private Label _statusLabel = null!;

	// Per-card UI references
	private readonly Label[] _titleLabels = new Label[3];
	private readonly Label[] _descLabels = new Label[3];
	private readonly ProgressBar[] _progressBars = new ProgressBar[3];
	private readonly Label[] _progressLabels = new Label[3];
	private readonly Label[] _rewardLabels = new Label[3];
	private readonly Button[] _claimButtons = new Button[3];

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _bountyCard0, _bountyCard1, _bountyCard2 });
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
		AddChild(new ColorRect { Color = new Color("e6a817"), Position = new Vector2(0f, 104f), Size = new Vector2(1280f, 6f) });

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Bounty Board", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		var dateLabel = new Label
		{
			Text = BountyBoardCatalog.GetDateKey(),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		dateLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		titleRow.AddChild(dateLabel);

		// Bounty cards
		_bountyCard0 = BuildBountyCard(0, new Vector2(24f, 122f));
		_bountyCard1 = BuildBountyCard(1, new Vector2(434f, 122f));
		_bountyCard2 = BuildBountyCard(2, new Vector2(844f, 122f));

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
		var armoryBtn = new Button { Text = "Armory", CustomMinimumSize = new Vector2(140f, 0f) };
		armoryBtn.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryBtn);
	}

	private PanelContainer BuildBountyCard(int index, Vector2 position)
	{
		var card = new PanelContainer { Position = position, Size = new Vector2(396f, 480f) };
		AddChild(card);

		var outer = new MarginContainer();
		outer.AddThemeConstantOverride("margin_left", 12);
		outer.AddThemeConstantOverride("margin_right", 12);
		outer.AddThemeConstantOverride("margin_top", 12);
		outer.AddThemeConstantOverride("margin_bottom", 12);
		card.AddChild(outer);

		var stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 10);
		outer.AddChild(stack);

		_titleLabels[index] = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		stack.AddChild(_titleLabels[index]);

		_descLabels[index] = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(360f, 0f)
		};
		_descLabels[index].AddThemeColorOverride("font_color", new Color("b0b8c8"));
		stack.AddChild(_descLabels[index]);

		// Spacer
		stack.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 8f) });

		_progressBars[index] = new ProgressBar
		{
			CustomMinimumSize = new Vector2(360f, 24f),
			MinValue = 0,
			MaxValue = 1,
			Value = 0,
			ShowPercentage = false
		};
		stack.AddChild(_progressBars[index]);

		_progressLabels[index] = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		stack.AddChild(_progressLabels[index]);

		// Spacer
		stack.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 8f) });

		_rewardLabels[index] = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_rewardLabels[index].AddThemeColorOverride("font_color", new Color("ffd700"));
		stack.AddChild(_rewardLabels[index]);

		// Spacer pushes button toward bottom
		stack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		_claimButtons[index] = new Button { Text = "Claim", CustomMinimumSize = new Vector2(160f, 40f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		var capturedIndex = index;
		_claimButtons[index].Pressed += () => OnClaimPressed(capturedIndex);
		stack.AddChild(_claimButtons[index]);

		return card;
	}

	private void RefreshUi()
	{
		var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
		var gs = GameState.Instance;

		for (var i = 0; i < 3; i++)
		{
			if (i >= bounties.Length)
			{
				_titleLabels[i].Text = "---";
				_descLabels[i].Text = "";
				_progressLabels[i].Text = "";
				_progressBars[i].Value = 0;
				_rewardLabels[i].Text = "";
				_claimButtons[i].Disabled = true;
				continue;
			}

			var def = bounties[i];
			var progress = gs.GetBountyProgress(def.Id);
			var completed = gs.IsBountyCompleted(def.Id);
			var reachedTarget = progress >= def.TargetCount;

			_titleLabels[i].Text = def.Title;
			_descLabels[i].Text = def.Description;
			_progressBars[i].MaxValue = def.TargetCount;
			_progressBars[i].Value = Math.Min(progress, def.TargetCount);
			_progressLabels[i].Text = $"Progress: {Math.Min(progress, def.TargetCount)}/{def.TargetCount}";
			_rewardLabels[i].Text = $"+{def.RewardAmount} {def.RewardType}";
			_claimButtons[i].Disabled = !reachedTarget || completed;
			_claimButtons[i].Text = completed ? "Claimed" : "Claim";
		}
	}

	private void OnClaimPressed(int index)
	{
		var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
		if (index >= bounties.Length) return;

		var def = bounties[index];
		if (GameState.Instance.TryClaimBounty(def.Id, out var message))
		{
			_statusLabel.Text = message;
			RefreshUi();
		}
		else
		{
			_statusLabel.Text = message;
		}
	}
}
