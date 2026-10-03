using Godot;

/// <summary>The painted landmark is the hit target; earned stars remain above its roof.</summary>
public partial class AdventureMapToken : RealmButton
{
    public AdventureMapNode Site { get; set; }
    public Vector2 MarkerCenter => new(Size.X / 2,
        Site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food ? 64
        : Site.Kind == AdventureSiteKind.Leader && GameState.Instance.IsAdventureBoss(Site.Stage) ? 140 : 112);
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        RefreshRating(); MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void RefreshRating()
    {
        var state = GameState.Instance;
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var rating = leader ? $"{state.GetStageStars(Site.Stage)}/3 stars" : "";
        var reward = Site.Kind == AdventureSiteKind.Gold ? $"{Site.GoldReward} gold" : Site.Kind == AdventureSiteKind.Food ? $"{Site.FoodReward} food" : Site.Title;
        TooltipText = leader ? $"Stage {Site.Stage} · {GameData.GetStage(Site.Stage).StageName}\n{Site.Title}\n{rating}\nEntry: {state.GetStageEntryFoodCost(Site.Stage)} food" : $"{reward}\nOpens surrounding tiles";
        AccessibilityName = leader ? $"{Site.Title}, stage {Site.Stage}, {rating}, entry {state.GetStageEntryFoodCost(Site.Stage)} food" : TooltipText;
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point) => Site != null && AdventureMapMarkerStyle.Contains(point, MarkerCenter,
        AdventureTileCatalog.Find(Site.MapId, Site.Id));
    public override void _Draw()
    {
        if (Site == null) return;
        var state = GameState.Instance;
        var tile = AdventureTileCatalog.Find(Site.MapId, Site.Id);
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var cleared = state.IsAdventureTileComplete(tile);
        if (leader && cleared)
        {
            var roof = MarkerCenter.Y - AdventureAtlasArt.LandmarkHeight(tile) * AdventureAtlasArt.GroundAnchor(AdventureAtlasArt.LandmarkSprite(tile)).Y;
            StageStarRating.DrawStars(this, new Rect2(new Vector2(Size.X / 2 - 32, roof - 25), new Vector2(64, 22)), state.GetStageStars(Site.Stage));
        }
    }
}
