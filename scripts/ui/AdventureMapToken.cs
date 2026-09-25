using Godot;

/// <summary>Keyboard-focusable world markers with the full site name in their tooltip.</summary>
public partial class AdventureMapToken : RealmButton
{
    public AdventureMapNode Site { get; set; }
    public bool Selected { get; set; }
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = Site.Kind == AdventureSiteKind.Leader ? $"{Site.Title}\nStage {Site.Stage} · {GameData.GetStage(Site.Stage).StageName}" : Site.Title;
        AccessibilityName = Site.Title;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        if (Site == null) return;
        var center = Size / 2;
        var leader = Site.Kind == AdventureSiteKind.Leader;
        var visited = GameState.Instance.HasVisitedAdventureSite(Site.Id);
        var locked = leader && !GameState.Instance.IsCampaignStageUnlocked(Site.Stage);
        var cleared = leader && GameState.Instance.GetStageStars(Site.Stage) > 0;
        var color = locked ? new Color("899399") : cleared || (visited && !leader) ? new Color("99cfa3") : leader ? new Color("df8067") : RealmUi.Gold;
        var radius = leader ? 31f : 22f;
        DrawCircle(center + new Vector2(0, 6), radius + 3, new Color(0, 0, 0, .55f));
        if (leader) { DrawCircle(center, radius + 2, color.Darkened(.3f)); DrawCircle(center, radius, new Color("182a2b")); }
        if (leader)
        {
            DrawTextureRect(AdventureMapArt.Leader(Site.Portrait), new Rect2(center - new Vector2(24, 24), new Vector2(48, 48)), false);
            DrawColoredPolygon(new[] { center + new Vector2(20,-39), center + new Vector2(38,-35), center + new Vector2(34,-18), center + new Vector2(20,-23) }, color);
            DrawLine(center + new Vector2(19,-40), center + new Vector2(19,-13), new Color("e5c997"), 2, true);
        }
        else DrawTextureRect(AdventureMapArt.Miniature(Site.Kind), new Rect2(center - new Vector2(37,43), new Vector2(74,74)), false, visited ? new Color(.78f,.84f,.8f) : Colors.White);
        if (cleared || (visited && !leader)) DrawTextureRect(RealmUi.Icon("check"), new Rect2(center + new Vector2(10,9), new Vector2(19,19)), false, new Color("c4efb6"));
        if (locked) DrawTextureRect(RealmUi.Icon("lock"), new Rect2(center + new Vector2(9,8), new Vector2(22,22)), false, RealmUi.Gold);
        if (Selected || IsHovered() || HasFocus()) DrawArc(center, radius + 7, 0, Mathf.Tau, 48, new Color("ffe0a0"), 2, true);
    }
}
