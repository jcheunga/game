using Godot;

public partial class AdventureDiscoveryToken : RealmButton
{
    public AdventureDiscovery Discovery { get; set; }
    public Vector2 MarkerCenter => new(Size.X / 2, 64);
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        RefreshRating(); MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void RefreshRating()
    {
        AccessibilityName = $"{Discovery.Title}, {Discovery.RewardText}";
        TooltipText = $"{Discovery.Title}\n{Discovery.RewardText}\nOpens surrounding tiles";
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point) => Discovery != null && AdventureMapMarkerStyle.Contains(point, MarkerCenter,
        AdventureTileCatalog.Find(Discovery.MapId, Discovery.Id));
}
