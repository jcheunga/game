using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// "More": the lantern caravan hub with its adventure, caravan and community activities, drawn on the
/// shared modal presentation, with only activity tabs and cards inside the frame.
/// </summary>
public partial class CaravanHub : RoyalScreen
{
    private static readonly string[] Sections = { "Adventure", "Caravan", "Community" };
    private int _section;
    private Control _layer;

    public CaravanHub(int section = 0)
    {
        Veil = new Color(.02f, .05f, .08f, .62f);
        _section = Mathf.Clamp(section, 0, Sections.Length - 1);
    }

    /// <summary>The open tab, so closing an activity opened from it returns here.</summary>
    public int Section => _section;

    private RoyalSpec Spec => RoyalSpec.For("hub");

    protected override void Build()
    {
        BuildFrame();
        _layer = Layer("Live");
        _layer.Position = new Vector2(0, -70);
        Rebuild();
    }

    // Reuse the painted frame in regions so the obsolete resource strip and footer
    // are never drawn. Header and brass rail retain their original proportions.
    private void BuildFrame()
    {
        void Region(Rect2 source, Rect2 destination)
        {
            AddChild(new TextureRect { Texture = RoyalArt.Cut("hub", source),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Position = destination.Position, Size = destination.Size, MouseFilter = MouseFilterEnum.Ignore });
        }
        Region(new Rect2(0, 92, 1280, 152), new Rect2(0, 22, 1280, 152));
        Region(new Rect2(0, 244, 1280, 391), new Rect2(0, 174, 1280, 503));
        Region(new Rect2(0, 689, 1280, 17), new Rect2(0, 677, 1280, 17));
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && !new Rect2(33, 22, 1214, 672).HasPoint(click.Position))
        {
            AcceptEvent();
            Close();
        }
    }

    private readonly record struct Activity(string Title, string Art, string Icon, Action Open, string Locked = null);

    private IEnumerable<Activity> Activities()
    {
        var state = GameState.Instance;
        var router = SceneRouter.Instance;
        switch (_section)
        {
            case 0:
                yield return new("Endless", "activity-endless", "hubicon-endless", router.GoToEndless);
                yield return new("Tower", "activity-tower", "hubicon-tower", router.GoToTower);
                yield return new("Bounties", "activity-bounties", "hubicon-bounties", router.GoToBounty);
                yield return new("Boss rush", "activity-bossrush", "hubicon-bossrush", router.GoToBossRush,
                    state.HighestUnlockedStage<=10?"Defeat the King's Road boss to unlock boss rush":null);
                yield return new("Weekly raid", "activity-weeklyraid", "hubicon-weeklyraid", router.GoToRaid,
                    state.HighestUnlockedStage <= CampaignPacing.StagesPerZone ? "Defeat the King's Road boss to unlock raids" : null);
                yield return new("Event", "activity-event", "hubicon-event", router.GoToEvent, state.GetActiveEvent() == null ? "No event is active" : null);
                break;
            case 1:
                yield return new("Expeditions", "activity-expeditions", "hubicon-bounties", router.GoToExpeditions);
                yield return new("Forge", "activity-forge", "icon-hammers", router.GoToForge);
                yield return new("Daily gifts", "activity-gifts", "hubicon-event", router.GoToLoginCalendar);
                yield return new("Season", "activity-season", "relicicon-crown", router.GoToSeasonPass);
                yield return new("Codex", "activity-codex", "tab-adviser", router.GoToCodex);
                yield return new("Progression", "activity-progression", "result-star", router.GoToProgression);
                yield return new("Store", "activity-store", "relicicon-talisman", router.GoToCashShop);
                if (state.CanPrestige)
                    yield return new("Prestige", "activity-prestige", "relicicon-crown", () => MedievalUi.ShowConfirmation(this, "Begin a new age?",
                        "Restart campaign progression for prestige rewards.", "Prestige", () => { GameState.Instance.TryPrestige(out _); SceneRouter.Instance.ReloadHome(); }));
                break;
            default:
                yield return new("Warband guild", "activity-guild", "hubtab-community", router.GoToGuild);
                yield return new("Friends", "activity-friends", "icon-profile", router.GoToFriends);
                yield return new("Challenges", "activity-challenges", "hubicon-weeklyraid", router.GoToMultiplayer);
                yield return new("Arena", "activity-arena", "hubicon-endless", router.GoToArena,
                    state.HighestUnlockedStage < ArenaCatalog.MinRequiredStage ? $"Win stage {ArenaCatalog.MinRequiredStage - 1} or higher to unlock the arena" : null);
                yield return new("Rankings", "activity-rankings", "relicicon-crown", router.GoToLeaderboard);
                break;
        }
    }

    private static Texture2D ActivityArt(Activity activity)
    {
        // The painted pictures first; the concept's own card cuts (label included) only as a fallback.
        foreach (var path in new[] { $"res://assets/ui/royal/items/{activity.Art}.png", $"res://assets/ui/royal/kit/{activity.Art}.png" })
            if (ResourceLoader.Exists(path)) return RoyalArt.Load(path);
        return RoyalKit.Texture("activity-endless");
    }

    private void Rebuild()
    {
        if (_layer == null) return;
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        _layer.AddChild(spec.Label("title", "The Lantern Caravan", 760));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
        for (var i = 0; i < Sections.Length; i++)
        {
            var index = i; var key = "tab." + Sections[i].ToLowerInvariant();
            var rect = spec.Rect(key);
            var tab = RoyalButton.Over(rect, Sections[i], () => { _section = index; Rebuild(); }, 5);
            if (i == _section) tab.SetStates(RoyalKit.Slice("hub-tab-selected", 14), 5);
            tab.MarkTab(i == _section);
            var icon = spec.Rect(key + ".icon");
            var label = spec.Label(key + ".label", Sections[i], 200, i == _section ? new Color("fff6d0") : new Color("cfc8c0"));
            // Icon and label stay together, centred on the tab, whatever the section names.
            var group = icon.Size.X + 14 + label.TextWidth(label.FontSize);
            var left = rect.Size.X / 2 - group / 2;
            tab.SetGlyph(RoyalKit.Texture("hubtab-" + Sections[i].ToLowerInvariant()), new Rect2(left, icon.Position.Y - rect.Position.Y, icon.Size.X, icon.Size.Y));
            label.Position = new Vector2(left + icon.Size.X + 14, label.Position.Y - rect.Position.Y);
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
            _layer.AddChild(tab);
        }
        BuildCards(spec);
    }

    /// <summary>
    /// Every tab's cards fit the panel without scrolling: three to a row for up to six activities, four to a row
    /// beyond that, and a short last row centred.
    /// </summary>
    private void BuildCards(RoyalSpec spec)
    {
        var activities = Activities().ToList();
        var first = spec.Rect("card.endless");
        var area = new Rect2(first.Position, new Vector2(spec.Rect("card.bounties").End.X - first.Position.X, spec.Rect("card.bossrush").End.Y + 112 - first.Position.Y));
        var columns = activities.Count > 6 ? 4 : 3;
        var rows = Math.Max(1, Mathf.CeilToInt(activities.Count / (float)columns));
        const float gapX = 14, gapY = 9;
        var size = new Vector2((area.Size.X - (columns - 1) * gapX) / columns, (area.Size.Y - (rows - 1) * gapY) / Math.Max(2, rows));
        for (var i = 0; i < activities.Count; i++)
        {
            var row = i / columns;
            var inRow = Math.Min(columns, activities.Count - row * columns);
            var offset = (columns - inRow) * (size.X + gapX) / 2;
            var rect = new Rect2(area.Position + new Vector2(offset + i % columns * (size.X + gapX), row * (size.Y + gapY)), size);
            _layer.AddChild(Card(spec, rect, activities[i]));
        }
    }

    private Control Card(RoyalSpec spec, Rect2 rect, Activity activity)
    {
        // Parts sit where the concept's card has them, measured from its edges.
        var w = rect.Size.X; var h = rect.Size.Y;
        var bar = new Rect2(7, h - 52, w - 11, 47);
        var locked = activity.Locked != null;
        var card = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = activity.Title, TooltipText = activity.Locked ?? activity.Title,
            MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.PointingHand, Disabled = locked || activity.Open == null };
        card.SetStates(null, 6);
        card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = ActivityArt(activity), Position = new Vector2(5, 5), Size = new Vector2(w - 9, h - 9), MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = locked ? new Color(.5f, .5f, .52f) : Colors.White });
        var ring = new Panel { Position = Vector2.Zero, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore };
        ring.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-card", 10, false));
        card.AddChild(ring);
        var strip = new Panel { Position = bar.Position, Size = bar.Size, MouseFilter = MouseFilterEnum.Ignore };
        strip.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-card-bar", 12));
        card.AddChild(strip);
        card.AddChild(RoyalKit.Image(activity.Icon, new Rect2(22, bar.Position.Y + 8, 34, 34), locked ? new Color(.7f, .7f, .7f) : null));
        var title = spec.Label("card.endless.title", activity.Title, w - 118, locked ? new Color("bcbab8") : null);
        title.Position = new Vector2(78, bar.Position.Y + 37 - title.Baseline);
        card.AddChild(title);
        card.AddChild(RoyalKit.Image("hub-chevron", new Rect2(w - 36, bar.Position.Y + 10, 19, 33), locked ? new Color(.6f, .6f, .6f) : null));
        if (activity.Locked == "In development")
        {
            var badge = spec.Rect("card.bossrush.badge");
            var at = new Rect2(badge.Position - spec.Rect("card.bossrush").Position, badge.Size);
            var origin = spec.Rect("card.bossrush").Position;
            var plate = new Panel { Position = at.Position, Size = at.Size, MouseFilter = MouseFilterEnum.Ignore };
            plate.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-badge", 10));
            card.AddChild(plate);
            var icon = spec.Rect("card.bossrush.badge.icon");
            card.AddChild(RoyalKit.Image("icon-tools", new Rect2(icon.Position - origin, icon.Size)));
            var label = spec.Label("card.bossrush.badge.label", "In development", 180);
            label.Position -= origin;
            card.AddChild(label);
        }
        if (activity.Open != null) card.Pressed += () => { if (!locked) activity.Open(); };
        return card;
    }

}
