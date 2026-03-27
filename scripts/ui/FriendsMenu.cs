#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class FriendsMenu : Control
{
	private PanelContainer _titlePanel = null!;
	private PanelContainer _friendListPanel = null!;
	private PanelContainer _actionsPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private HBoxContainer _summaryMetricsRow = null!;
	private Label _statusLabel = null!;
	private HBoxContainer _giftStatusRow = null!;
	private HBoxContainer _giftRewardRow = null!;
	private VBoxContainer _friendStack = null!;
	private LineEdit _addFriendInput = null!;
	private Button _removeBtn = null!;
	private string? _selectedFriendId;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _friendListPanel, _actionsPanel });
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
		MenuBackdropComposer.AddSplitBackdrop(this, "friends", new Color("1a1a2e"), new Color("16213e"), new Color("f472b6"), 104f);

		// Title panel
		_titlePanel = new PanelContainer { Position = new Vector2(24f, 20f), Size = new Vector2(1232f, 82f) };
		AddChild(_titlePanel);
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);
		titleRow.AddChild(new Label { Text = "Friends", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);
		_summaryMetricsRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_summaryMetricsRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_summaryMetricsRow);

		// Friend List panel (left)
		_friendListPanel = new PanelContainer { Position = new Vector2(24f, 122f), Size = new Vector2(620f, 480f) };
		AddChild(_friendListPanel);
		var listOuter = new MarginContainer();
		listOuter.AddThemeConstantOverride("margin_left", 8);
		listOuter.AddThemeConstantOverride("margin_right", 8);
		listOuter.AddThemeConstantOverride("margin_top", 8);
		listOuter.AddThemeConstantOverride("margin_bottom", 8);
		_friendListPanel.AddChild(listOuter);
		var listInner = new VBoxContainer();
		listInner.AddThemeConstantOverride("separation", 4);
		listOuter.AddChild(listInner);
		listInner.AddChild(new Label { Text = "Friend List", HorizontalAlignment = HorizontalAlignment.Center });
		var friendScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 400f) };
		listInner.AddChild(friendScroll);
		_friendStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_friendStack.AddThemeConstantOverride("separation", 4);
		friendScroll.AddChild(_friendStack);

		// Actions panel (right)
		_actionsPanel = new PanelContainer { Position = new Vector2(660f, 122f), Size = new Vector2(596f, 480f) };
		AddChild(_actionsPanel);
		var actionsOuter = new MarginContainer();
		actionsOuter.AddThemeConstantOverride("margin_left", 8);
		actionsOuter.AddThemeConstantOverride("margin_right", 8);
		actionsOuter.AddThemeConstantOverride("margin_top", 8);
		actionsOuter.AddThemeConstantOverride("margin_bottom", 8);
		_actionsPanel.AddChild(actionsOuter);
		var actionsInner = new VBoxContainer();
		actionsInner.AddThemeConstantOverride("separation", 10);
		actionsOuter.AddChild(actionsInner);
		actionsInner.AddChild(new Label { Text = "Actions", HorizontalAlignment = HorizontalAlignment.Center });

		// Add Friend section
		var addSectionLabel = new Label { Text = "Add Friend" };
		addSectionLabel.AddThemeColorOverride("font_color", new Color("f472b6"));
		actionsInner.AddChild(addSectionLabel);
		var addRow = new HBoxContainer();
		addRow.AddThemeConstantOverride("separation", 6);
		actionsInner.AddChild(addRow);
		_addFriendInput = new LineEdit { PlaceholderText = "Enter profile ID...", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		addRow.AddChild(_addFriendInput);
		var addBtn = new Button { Text = "Add", CustomMinimumSize = new Vector2(80f, 0f) };
		addBtn.Pressed += OnAddFriendPressed;
		addRow.AddChild(addBtn);

		// Gift Info section
		actionsInner.AddChild(new HSeparator());
		var giftSectionLabel = new Label { Text = "Gift Info" };
		giftSectionLabel.AddThemeColorOverride("font_color", new Color("f472b6"));
		actionsInner.AddChild(giftSectionLabel);
		_giftStatusRow = new HBoxContainer();
		_giftStatusRow.AddThemeConstantOverride("separation", 8);
		actionsInner.AddChild(_giftStatusRow);
		_giftRewardRow = new HBoxContainer();
		_giftRewardRow.AddThemeConstantOverride("separation", 8);
		actionsInner.AddChild(_giftRewardRow);

		// Remove Friend section
		actionsInner.AddChild(new HSeparator());
		var removeSectionLabel = new Label { Text = "Remove Friend" };
		removeSectionLabel.AddThemeColorOverride("font_color", new Color("f472b6"));
		actionsInner.AddChild(removeSectionLabel);
		_removeBtn = new Button { Text = "Remove Selected Friend", Disabled = true, CustomMinimumSize = new Vector2(200f, 0f) };
		_removeBtn.Pressed += OnRemoveFriendPressed;
		actionsInner.AddChild(_removeBtn);

		// Status label
		_statusLabel = new Label { Position = new Vector2(24f, 618f), Size = new Vector2(1232f, 30f), HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeColorOverride("font_color", new Color("90a0b0"));
		AddChild(_statusLabel);

		// Bottom nav
		var bottomRow = new HBoxContainer { Position = new Vector2(24f, 660f), Size = new Vector2(1232f, 40f) };
		bottomRow.AddThemeConstantOverride("separation", 12);
		AddChild(bottomRow);
		var profileBtn = new Button { Text = "Player Profile", CustomMinimumSize = new Vector2(140f, 0f) };
		profileBtn.Pressed += () => SceneRouter.Instance.GoToProfile();
		bottomRow.AddChild(profileBtn);
		var mapBtn = new Button { Text = "Campaign Map", CustomMinimumSize = new Vector2(140f, 0f) };
		mapBtn.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapBtn);
	}

	private void RefreshUi()
	{
		var gs = GameState.Instance;
		var friends = gs.GetFriendIds();
		RebuildResourcesRow(gs);
		RebuildSummaryMetricsRow(friends.Count);
		RebuildGiftStatusRow(gs.GiftsSentToday);
		RebuildGiftRewardRow();
		_selectedFriendId = null;
		_removeBtn.Disabled = true;
		RebuildFriendList(friends);
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

	private void RebuildSummaryMetricsRow(int friendCount)
	{
		foreach (var child in _summaryMetricsRow.GetChildren())
		{
			child.QueueFree();
		}

		_summaryMetricsRow.AddChild(UiBadgeFactory.CreateMetaMetric("friends", friendCount.ToString(), new Vector2(24f, 24f)));
	}

	private void RebuildGiftStatusRow(int giftsSentToday)
	{
		foreach (var child in _giftStatusRow.GetChildren())
		{
			child.QueueFree();
		}

		_giftStatusRow.AddChild(UiBadgeFactory.CreateMetaMetric("friends", $"Gifts sent today: {giftsSentToday}/3", new Vector2(24f, 24f)));
	}

	private void RebuildGiftRewardRow()
	{
		foreach (var child in _giftRewardRow.GetChildren())
		{
			child.QueueFree();
		}

		_giftRewardRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", "50", new Vector2(24f, 24f)));
		_giftRewardRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", "2", new Vector2(24f, 24f)));
	}

	private void RebuildFriendList(IReadOnlyCollection<string> friends)
	{
		foreach (var child in _friendStack.GetChildren()) child.QueueFree();

		if (friends.Count == 0)
		{
			_friendStack.AddChild(UiBadgeFactory.CreateMetaMetric("friends", "No friends yet.", new Vector2(24f, 24f)));
			return;
		}

		var gs = GameState.Instance;
		var giftsSentToday = gs.GiftsSentToday;

		foreach (var friendId in friends.OrderBy(id => id))
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var truncatedId = friendId.Length > 12 ? friendId[..12] + "..." : friendId;
			row.AddChild(UiBadgeFactory.CreateMetaBadge("friends", truncatedId, new Vector2(28f, 28f)));
			var selectBtn = new Button
			{
				Text = truncatedId,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				ToggleMode = true
			};
			var capturedId = friendId;
			selectBtn.Pressed += () => OnFriendSelected(capturedId, selectBtn);
			row.AddChild(selectBtn);

			var giftBtn = new Button { Text = "Send Gift", CustomMinimumSize = new Vector2(100f, 0f) };
			// Disable if already sent 3 gifts today
			if (giftsSentToday >= 3)
			{
				giftBtn.Disabled = true;
				giftBtn.TooltipText = "Daily gift limit reached (3/3).";
			}
			var capturedGiftId = friendId;
			giftBtn.Pressed += () => OnSendGiftPressed(capturedGiftId);
			row.AddChild(giftBtn);

			_friendStack.AddChild(row);
		}
	}

	private void OnFriendSelected(string friendId, Button selectBtn)
	{
		// Deselect all other buttons
		foreach (var child in _friendStack.GetChildren())
		{
			if (child is HBoxContainer hbox)
			{
				foreach (var inner in hbox.GetChildren())
				{
					if (inner is Button btn && btn.ToggleMode && btn != selectBtn)
						btn.ButtonPressed = false;
				}
			}
		}

		if (selectBtn.ButtonPressed)
		{
			_selectedFriendId = friendId;
			_removeBtn.Disabled = false;
		}
		else
		{
			_selectedFriendId = null;
			_removeBtn.Disabled = true;
		}
	}

	private void OnAddFriendPressed()
	{
		var profileId = _addFriendInput.Text.Trim();
		if (string.IsNullOrEmpty(profileId))
		{
			_statusLabel.Text = "Enter a profile ID to add.";
			return;
		}

		GameState.Instance.AddFriend(profileId);
		_addFriendInput.Text = "";
		_statusLabel.Text = $"Added {profileId} as a friend.";
		RefreshUi();
	}

	private void OnRemoveFriendPressed()
	{
		if (_selectedFriendId == null) return;
		var removed = _selectedFriendId;
		GameState.Instance.RemoveFriend(_selectedFriendId);
		_statusLabel.Text = $"Removed {removed} from friends.";
		RefreshUi();
	}

	private void OnSendGiftPressed(string friendId)
	{
		if (GameState.Instance.TrySendGift(friendId, out var message))
		{
			_statusLabel.Text = message;
		}
		else
		{
			_statusLabel.Text = message;
		}
		RefreshUi();
	}
}
