using Godot;

public partial class AdventureDiscoveryToken : RealmButton
{
    public AdventureDiscovery Discovery { get; set; }
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        RefreshRating(); MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void RefreshRating()
    {
        var cost = GameState.Instance.GetAdventureTileTravelFoodCost(AdventureTileCatalog.Find(Discovery.MapId, Discovery.Id));
        AccessibilityName = $"{Discovery.Title}, {Discovery.RewardText}, travel {cost} food";
        TooltipText = $"{Discovery.Title}\n{Discovery.RewardText}\nTravel: {cost} food · Opens surrounding tiles";
        QueueRedraw();
    }
    public override void _Draw()
    {
        var center = new Vector2(Size.X / 2, 24);
        var tint = Discovery.Kind == AdventureDiscoveryKind.Food ? new Color("adbd7b") : new Color("c9b17a");
        DrawCircle(center + new Vector2(0, 3), 23, new Color("101c1cbb"));
        DrawCircle(center, 20, new Color("273837"));
        DrawArc(center, 21, 0, Mathf.Tau, 32, tint, 1.5f, true);
        DrawTextureRect(HomeMapArt.Icon(Discovery.Icon), new Rect2(center - new Vector2(19, 19), new Vector2(38, 38)), false);
        var text = Discovery.Kind == AdventureDiscoveryKind.Survey ? "CHART" : $"+{Discovery.Amount} {Discovery.Kind.ToString().ToLowerInvariant()}";
        DrawRect(new Rect2(0, 48, Size.X, 23), new Color("17292de8"));
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2((Size.X - font.GetStringSize(text, fontSize: 14).X) / 2, 65), text, fontSize: 14, modulate: new Color("eee1bc"));
        var tile = AdventureTileCatalog.Find(Discovery.MapId, Discovery.Id);
        var travelText = GameState.Instance.GetAdventureTileTravelFoodCost(tile) == 0 ? "Free travel" : "Travel 1 food";
        DrawRect(new Rect2(0, 71, Size.X, 19), new Color("17292de8"));
        DrawString(font, new Vector2((Size.X - font.GetStringSize(travelText, fontSize: 12).X) / 2, 85), travelText, fontSize: 12, modulate: new Color("c9c6a6"));
        if (IsHovered() || HasFocus()) DrawArc(center, 25, 0, Mathf.Tau, 32, new Color("ffe3ae"), 2, true);
    }
}
