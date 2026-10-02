using Godot;

public partial class FoodBalance : HBoxContainer
{
    private Label _amount, _recharge;
    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddChild(UiBadgeFactory.CreateRewardBadge("food", "", "", new Vector2(24,24)));
        var text = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter }; text.AddThemeConstantOverride("separation", 0); AddChild(text);
        _amount = new Label(); _amount.AddThemeFontSizeOverride("font_size", 18); text.AddChild(_amount);
        _recharge = new Label(); _recharge.AddThemeFontSizeOverride("font_size", 12); _recharge.AddThemeColorOverride("font_color", RealmUi.Muted); text.AddChild(_recharge);
        UpdateBalance();
    }
    public override void _Process(double delta) => UpdateBalance();
    private void UpdateBalance()
    {
        if (_amount == null) return;
        var state = GameState.Instance;
        _amount.Text = $"{state.Food}/{GameState.FoodRechargeCap}";
        _recharge.Text = state.Food >= GameState.FoodRechargeCap ? "" : state.FoodRechargeText.Split('·')[1].Trim();
        _recharge.Visible = _recharge.Text.Length > 0;
    }
}
