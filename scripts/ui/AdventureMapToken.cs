using Godot;

/// <summary>Keyboard-focusable world markers with accessible site names.</summary>
public partial class AdventureMapToken : RealmButton
{
    public AdventureMapNode Site { get; set; }
    public bool Selected { get; set; }
    public Vector2 MarkerCenter => Site.Kind == AdventureSiteKind.Leader
        ? new Vector2(Size.X / 2, Size.Y - 32) : Size / 2;
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        RefreshRating();
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void RefreshRating()
    {
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var rating = leader ? $"{GameState.Instance.GetStageStars(Site.Stage)}/3 stars" : "";
        TooltipText = leader ? $"{Site.Title}\nStage {Site.Stage} · {GameData.GetStage(Site.Stage).StageName}\nBest: {rating}\n\n{StageStarScore.RulesText}" : Site.Title;
        AccessibilityName = leader ? $"{Site.Title}, stage {Site.Stage}, {rating}" : Site.Title;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (Site == null) return;
        var center = MarkerCenter;
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var visited = GameState.Instance.HasVisitedAdventureSite(Site.Id);
        var locked = leader && !GameState.Instance.IsCampaignStageUnlocked(Site.Stage);
        var cleared = leader && GameState.Instance.GetStageStars(Site.Stage) > 0;
        var color = locked ? new Color("899399") : cleared || (visited && !leader) ? new Color("99cfa3") : leader ? new Color("df8067") : RealmUi.Gold;
        var radius = leader ? 25f : 18f;
        DrawCircle(center + new Vector2(0, 6), radius + 3, new Color(0, 0, 0, .55f));
        if (leader) { DrawCircle(center, radius + 2, color.Darkened(.3f)); DrawCircle(center, radius, new Color("182a2b")); }
        if (leader)
        {
            DrawTextureRect(AdventureMapArt.Leader(Site.Portrait), new Rect2(center - new Vector2(20, 20), new Vector2(40, 40)), false);
            DrawColoredPolygon(new[] { center + new Vector2(20,-39), center + new Vector2(38,-35), center + new Vector2(34,-18), center + new Vector2(20,-23) }, color);
            DrawLine(center + new Vector2(19,-40), center + new Vector2(19,-13), new Color("e5c997"), 2, true);
        }
        else DrawTextureRect(AdventureMapArt.Miniature(Site.Kind), new Rect2(center - new Vector2(25,30), new Vector2(50,50)), false, visited ? new Color(.78f,.84f,.8f) : Colors.White);
        if (cleared || (visited && !leader)) DrawTextureRect(RealmUi.Icon("check"), new Rect2(center + new Vector2(10,9), new Vector2(19,19)), false, new Color("c4efb6"));
        if (locked) DrawTextureRect(RealmUi.Icon("lock"), new Rect2(center + new Vector2(9,8), new Vector2(22,22)), false, RealmUi.Gold);
        if (Selected || IsHovered() || HasFocus()) DrawArc(center, radius + 7, 0, Mathf.Tau, 48, new Color("ffe0a0"), 2, true);
        if (leader) StageStarRating.DrawStars(this, new Rect2(center + new Vector2(-30, -60), new Vector2(60, 28)), GameState.Instance.GetStageStars(Site.Stage), archRise: 8f);
    }
}
