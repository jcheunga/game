using Godot;

/// <summary>Stage and landmark pins above a single atlas tile, with earned stars and clear food costs.</summary>
public partial class AdventureMapToken : RealmButton
{
    public AdventureMapNode Site { get; set; }
    public bool Selected { get; set; }
    public Vector2 MarkerCenter => new(Size.X / 2, 99);
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        RefreshRating(); MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void RefreshRating()
    {
        var state = GameState.Instance;
        var tile = AdventureTileCatalog.Find(Site.MapId, Site.Id);
        var travel = state.GetAdventureTileTravelFoodCost(tile);
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var rating = leader ? $"{state.GetStageStars(Site.Stage)}/3 stars" : "";
        var reward = Site.Kind == AdventureSiteKind.Gold ? $"{Site.GoldReward} gold" : Site.Kind == AdventureSiteKind.Food ? $"{Site.FoodReward} food" : Site.Title;
        TooltipText = leader ? $"Stage {Site.Stage} · {GameData.GetStage(Site.Stage).StageName}\n{Site.Title}\n{rating}\nTravel: {travel} food · Entry: {state.GetStageEntryFoodCost(Site.Stage)} food" : $"{reward}\nTravel: {travel} food\nOpens surrounding tiles";
        AccessibilityName = leader ? $"{Site.Title}, stage {Site.Stage}, {rating}, travel {travel} food" : TooltipText;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (Site == null) return;
        var state = GameState.Instance;
        var tile = AdventureTileCatalog.Find(Site.MapId, Site.Id);
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var cleared = state.IsAdventureTileComplete(tile);
        var locked = leader && !state.IsCampaignStageUnlocked(Site.Stage);
        var center = new Vector2(Size.X / 2, 35);
        var color = locked ? new Color("858d88") : cleared ? new Color("9cbd85") : leader ? new Color("d88b43") : new Color("c1b27a");
        if (leader && cleared)
        {
            StageStarRating.DrawStars(this, new Rect2(center - new Vector2(40, 20), new Vector2(80, 32)), state.GetStageStars(Site.Stage), archRise: 5);
        }
        else
        {
            DrawColoredPolygon(new[] { center + new Vector2(-17, 13), center + new Vector2(17, 13), center + new Vector2(0, 38) }, color.Darkened(.25f));
            DrawCircle(center + new Vector2(0, 2), 25, new Color("171f20"));
            DrawCircle(center, 23, color.Darkened(.3f));
            DrawCircle(center, 19, new Color("342e27"));
            DrawArc(center, 22, 0, Mathf.Tau, 40, color, 2, true);
            DrawTextureRect(HomeMapArt.Icon(locked ? "lock" : Site.Icon), new Rect2(center - new Vector2(17, 17), new Vector2(34, 34)), false);
            if (cleared) DrawTextureRect(RealmUi.Icon("check"), new Rect2(center + new Vector2(9, 8), new Vector2(19, 19)), false, new Color("c4efb6"));
        }
        var text = leader ? $"STAGE {Site.Stage:00}" : Site.Kind == AdventureSiteKind.Gold ? $"{Site.GoldReward} gold" : Site.Kind == AdventureSiteKind.Food ? $"{Site.FoodReward} food" : Site.Kind == AdventureSiteKind.Camp ? "CAMP" : Site.Kind == AdventureSiteKind.Watchtower ? "SCOUT" : "SHRINE";
        DrawRect(new Rect2(2, 76, Size.X - 4, 27), new Color("15282ae8"));
        DrawLine(new Vector2(2, 76), new Vector2(Size.X - 2, 76), color.Darkened(.3f), 1);
        var font = ThemeDB.FallbackFont; var width = font.GetStringSize(text, fontSize: 15).X;
        DrawString(font, new Vector2((Size.X - width) / 2, 95), text, fontSize: 15, modulate: new Color("f1e3c2"));
        if (Site.Kind != AdventureSiteKind.Camp && !cleared)
        {
            var cost = state.GetAdventureTileTravelFoodCost(tile);
            var costText = cost == 0 ? "Free travel" : $"Travel {cost} food";
            DrawRect(new Rect2(2, 103, Size.X - 4, 20), new Color("15282ae8"));
            var costWidth = font.GetStringSize(costText, fontSize: 13).X;
            DrawString(font, new Vector2((Size.X - costWidth) / 2, 117), costText, fontSize: 13, modulate: new Color("c9c6a6"));
        }
        if (Selected || IsHovered() || HasFocus()) DrawArc(center, 28, 0, Mathf.Tau, 48, new Color("ffe0a0"), 2, true);
    }
}
