using Godot;

public partial class LoginCalendarMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _gridPanel = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _gridStack = null!;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _gridPanel });
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
		MenuBackdropComposer.AddSplitBackdrop(this, "login_calendar", new Color("1a1a2e"), new Color("16213e"), new Color("ffd700"), 104f);

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label
		{
			Text = $"Login Calendar  —  {LoginCalendarCatalog.GetCurrentMonth()}",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});

		// Grid panel
		_gridPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(1232f, 480f) };
		AddChild(_gridPanel);
		var gridOuter = new MarginContainer();
		gridOuter.AddThemeConstantOverride("margin_left", 12);
		gridOuter.AddThemeConstantOverride("margin_right", 12);
		gridOuter.AddThemeConstantOverride("margin_top", 12);
		gridOuter.AddThemeConstantOverride("margin_bottom", 12);
		_gridPanel.AddChild(gridOuter);
		_gridStack = new VBoxContainer();
		_gridStack.AddThemeConstantOverride("separation", 8);
		gridOuter.AddChild(_gridStack);

		// Status label
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		// Bottom nav
		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var mainMenuBtn = new Button { Text = "Main Menu", CustomMinimumSize = new Vector2(140f, 0f) };
		mainMenuBtn.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(mainMenuBtn);
	}

	private void RefreshUi()
	{
		RebuildGrid();
	}

	private void RebuildGrid()
	{
		foreach (var child in _gridStack.GetChildren()) child.QueueFree();

		var rewards = LoginCalendarCatalog.GetAll();
		var daysClaimed = GameState.Instance.LoginCalendarDay;
		var canClaim = GameState.Instance.CanClaimLoginReward();

		// 6 rows x 5 columns
		var index = 0;
		for (var row = 0; row < 6; row++)
		{
			var rowContainer = new HBoxContainer();
			rowContainer.AddThemeConstantOverride("separation", 8);
			_gridStack.AddChild(rowContainer);

			for (var col = 0; col < 5; col++)
			{
				if (index >= rewards.Count)
				{
					// Empty spacer for remaining cells
					rowContainer.AddChild(new Control { CustomMinimumSize = new Vector2(224f, 66f) });
					index++;
					continue;
				}

				var reward = rewards[index];
				var dayNum = reward.Day;
				var isClaimed = dayNum <= daysClaimed;
				var isCurrent = dayNum == daysClaimed + 1;
				var isLocked = dayNum > daysClaimed + 1;

				var tile = new PanelContainer { CustomMinimumSize = new Vector2(224f, 66f) };
				rowContainer.AddChild(tile);

				var tileContent = new VBoxContainer();
				tileContent.AddThemeConstantOverride("separation", 2);
				tile.AddChild(tileContent);

				// Day number label
				var dayLabel = new Label
				{
					Text = $"Day {dayNum}",
					HorizontalAlignment = HorizontalAlignment.Center
				};

				if (isClaimed)
					dayLabel.AddThemeColorOverride("font_color", new Color("70c870")); // green
				else if (isCurrent)
					dayLabel.AddThemeColorOverride("font_color", new Color("ffd700")); // gold
				else
					dayLabel.AddThemeColorOverride("font_color", new Color("606870")); // gray

				tileContent.AddChild(dayLabel);

				// Reward label + badge
				var rewardCenter = new CenterContainer();
				tileContent.AddChild(rewardCenter);
				var rewardRow = new HBoxContainer();
				rewardRow.AddThemeConstantOverride("separation", 6);
				rewardCenter.AddChild(rewardRow);
				rewardRow.AddChild(UiBadgeFactory.CreateRewardBadge(reward.RewardType, reward.RewardItemId, reward.Label, new Vector2(26f, 26f)));
				var rewardLabel = new Label
				{
					Text = reward.Label,
					VerticalAlignment = VerticalAlignment.Center
				};

				if (isClaimed)
					rewardLabel.AddThemeColorOverride("font_color", new Color("508050"));
				else if (isCurrent)
					rewardLabel.AddThemeColorOverride("font_color", new Color("ccaa00"));
				else
					rewardLabel.AddThemeColorOverride("font_color", new Color("505860"));

				rewardRow.AddChild(rewardLabel);

				// Status or claim button
				if (isClaimed)
				{
					var statusLbl = new Label
					{
						Text = "Claimed",
						HorizontalAlignment = HorizontalAlignment.Center
					};
					statusLbl.AddThemeColorOverride("font_color", new Color("70c870"));
					tileContent.AddChild(statusLbl);
				}
				else if (isCurrent && canClaim)
				{
					var claimBtn = new Button
					{
						Text = "Claim",
						SizeFlagsHorizontal = SizeFlags.ShrinkCenter
					};
					claimBtn.Pressed += () =>
					{
						if (GameState.Instance.TryClaimLoginReward(out var message))
						{
							_statusLabel.Text = message;
							RefreshUi();
						}
						else
						{
							_statusLabel.Text = message;
						}
					};
					tileContent.AddChild(claimBtn);
				}
				else
				{
					var lockedLbl = new Label
					{
						Text = isCurrent ? "Not yet" : "Locked",
						HorizontalAlignment = HorizontalAlignment.Center
					};
					lockedLbl.AddThemeColorOverride("font_color", new Color("505860"));
					tileContent.AddChild(lockedLbl);
				}

				index++;
			}
		}
	}
}
