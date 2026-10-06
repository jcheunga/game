using Godot;

public partial class LoginCalendarMenu : Control
{
    private VBoxContainer _days;
    private Label _status;
    private int _page;

    public override void _Ready()
    {

        MedievalUi.Apply(this);
        RealmUi.Header(this, System.DateTime.UtcNow.ToString("MMMM yyyy"), "Gifts of the realm", () => SceneRouter.Instance.GoToMainMenu(), "gift");
        var body = RealmUi.Panel(this, new Rect2(24, 110, 1232, 492), out _);
        RealmUi.Tabs(body, page => { _page = page; RefreshUi(); }, "Days 1–10", "Days 11–20", "Days 21–30");
        _days = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(_days);
        var footer = RealmUi.Panel(this, new Rect2(24, 618, 1232, 80), out _);
        _status = RealmUi.Label("Return each day to collect the next gift.", 18, true);
        footer.AddChild(_status);
        RefreshUi();
    }

    private void RefreshUi()
    {
        RealmUi.Clear(_days);
        var rewards = LoginCalendarCatalog.GetAll();
        var claimed = GameState.Instance.LoginCalendarDay;
        var canClaim = GameState.Instance.CanClaimLoginReward();
        for (var row = 0; row < 2; row++)
        {
            var strip = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            _days.AddChild(strip);
            for (var col = 0; col < 5; col++)
            {
                var index = _page * 10 + row * 5 + col;
                if (index >= rewards.Count) continue;
                var reward = rewards[index];
                var current = reward.Day == claimed + 1;
                var collected = reward.Day <= claimed;
                var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(RealmModal.Embedded(this) ? 0 : 220, 0) };
                if (current) frame.AddThemeStyleboxOverride("panel", RealmUi.Surface(new Color("433623"), RealmUi.Gold));
                strip.AddChild(frame);
                var card = new VBoxContainer(); frame.AddChild(card);
                var title = RealmUi.Heading($"Day {reward.Day}", 22);
                title.HorizontalAlignment = HorizontalAlignment.Center; card.AddChild(title);
                var rewardRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
                card.AddChild(rewardRow);
                var badge = UiBadgeFactory.CreateRewardBadge(reward.RewardType, reward.RewardItemId, reward.Label, new Vector2(38, 38));
                rewardRow.AddChild(badge);
                var description = RealmUi.Label(reward.Label, 20);
                description.VerticalAlignment = VerticalAlignment.Center;
                description.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                rewardRow.AddChild(description);
                // Every card reserves the same footer height so rewards line up across the grid.
                var footer = new CenterContainer { CustomMinimumSize = new Vector2(0, 48) };
                footer.SetMeta(RealmModal.KeepMinimum, true);
                card.AddChild(footer);
                if (current && canClaim)
                {
                    var claim = RealmUi.Button("gift", "Claim", () =>
                    {
                        AudioDirector.Claimed(GameState.Instance.TryClaimLoginReward(out var message));
                        _status.Text = message; RefreshUi();
                    }, true);
                    footer.AddChild(claim);
                }
                else
                {
                    var state = RealmUi.Label(collected ? "Collected" : current ? "Tomorrow" : "Locked", 18, true);
                    state.HorizontalAlignment = HorizontalAlignment.Center; state.AutowrapMode = TextServer.AutowrapMode.Off; footer.AddChild(state);
                    if (collected) { badge.Modulate = new Color(1, 1, 1, .55f); description.AddThemeColorOverride("font_color", ModalUi.Muted); }
                }
            }
        }
    }
}
