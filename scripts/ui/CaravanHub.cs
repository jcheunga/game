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

    public CaravanHub() { Veil = new Color(.02f, .05f, .08f, .62f); }

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
                yield return new("Progression", "activity-forge", "icon-hammers", router.GoToProgression);
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
        foreach (var path in new[] { $"res://assets/ui/royal/kit/{activity.Art}.png", $"res://assets/ui/royal/items/{activity.Art}.png" })
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

    private void BuildCards(RoyalSpec spec)
    {
        var keys = new[] { "endless", "tower", "bounties", "bossrush", "weeklyraid", "event" };
        var activities = Activities().ToList();
        var content = spec.Rect("content");
        content.Size += new Vector2(0, 112);
        var scroll = new ScrollContainer { Position = content.Position, Size = content.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _layer.AddChild(scroll);
        var rows = Mathf.CeilToInt(activities.Count / 3f);
        var pitchY = spec.Rect("card.bossrush").Position.Y - spec.Rect("card.endless").Position.Y + 56;
        var holder = new Control { CustomMinimumSize = new Vector2(content.Size.X, Mathf.Max(content.Size.Y, (rows - 2) * pitchY + content.Size.Y)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(holder);
        for (var i = 0; i < activities.Count; i++)
        {
            var key = "card." + keys[i % 6];
            var rect = spec.Rect(key);
            rect.Position = new Vector2(rect.Position.X, spec.Rect("card.endless").Position.Y + i / 3 * pitchY) - content.Position;
            rect.Size += new Vector2(0, 56);
            holder.AddChild(Card(spec, key, rect, activities[i]));
        }
    }

    private Control Card(RoyalSpec spec, string key, Rect2 rect, Activity activity)
    {
        var origin = spec.Rect(key).Position;
        Rect2 Local(string part)
        {
            var local = new Rect2(spec.Rect(key + part).Position - origin, spec.Rect(key + part).Size);
            if (part == ".art") local.Size += new Vector2(0, 56);
            else local.Position += new Vector2(0, 56);
            return local;
        }
        var locked = activity.Locked != null;
        var card = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = activity.Title, TooltipText = activity.Locked ?? activity.Title,
            MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.PointingHand, Disabled = locked || activity.Open == null };
        card.SetStates(null, 6);
        card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = ActivityArt(activity), Position = Local(".art").Position, Size = Local(".art").Size, MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = locked ? new Color(.5f, .5f, .52f) : Colors.White });
        var ring = new Panel { Position = Vector2.Zero, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore };
        ring.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-card", 10, false));
        card.AddChild(ring);
        var bar = new Panel { Position = Local(".bar").Position, Size = Local(".bar").Size, MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-card-bar", 12));
        card.AddChild(bar);
        card.AddChild(RoyalKit.Image(activity.Icon, Local(".icon").Grow(1), locked ? new Color(.7f, .7f, .7f) : null));
        var title = spec.Label(key + ".title", activity.Title, 240, locked ? new Color("bcbab8") : null);
        title.Position += new Vector2(0, 56) - origin;
        card.AddChild(title);
        card.AddChild(RoyalKit.Image("hub-chevron", Local(".chevron").Grow(2), locked ? new Color(.6f, .6f, .6f) : null));
        if (activity.Locked == "In development")
        {
            var badge = spec.Rect("card.bossrush.badge");
            var at = new Rect2(badge.Position - spec.Rect("card.bossrush").Position, badge.Size);
            var plate = new Panel { Position = at.Position, Size = at.Size, MouseFilter = MouseFilterEnum.Ignore };
            plate.AddThemeStyleboxOverride("panel", RoyalKit.Slice("hub-badge", 10));
            card.AddChild(plate);
            var icon = spec.Rect("card.bossrush.badge.icon");
            card.AddChild(RoyalKit.Image("icon-tools", new Rect2(icon.Position - spec.Rect("card.bossrush").Position, icon.Size)));
            var label = spec.Label("card.bossrush.badge.label", "In development", 180);
            label.Position -= spec.Rect("card.bossrush").Position;
            card.AddChild(label);
        }
        if (activity.Open != null) card.Pressed += () => { if (!locked) activity.Open(); };
        return card;
    }

}
