using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// "More": the lantern caravan hub with its adventure, caravan and community activities, drawn on the
/// approved hub concept. Its top bar repeats the home resources and zone controls.
/// </summary>
public partial class CaravanHub : RoyalScreen
{
    private static readonly string[] Sections = { "Adventure", "Caravan", "Community" };
    private readonly MapMenu _home;
    private int _section;
    private Control _layer;

    public CaravanHub() { }
    public CaravanHub(MapMenu home) { _home = home; PlateName = "hub"; Veil = new Color(.02f, .05f, .08f, .55f); }

    private RoyalSpec Spec => RoyalSpec.For("hub");

    protected override void Build()
    {
        _layer = Layer("Live");
        GameState.Instance.FoodChanged += Rebuild;
        Rebuild();
    }

    public override void _ExitTree() => GameState.Instance.FoodChanged -= Rebuild;

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
                yield return new("Boss rush", "activity-bossrush", "hubicon-bossrush", null, "In development");
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
        var state = GameState.Instance;
        BuildTopBar(spec, state);
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
        BuildFooter(spec);
    }

    private void BuildTopBar(RoyalSpec spec, GameState state)
    {
        // The hub plate paints the strip; the pairs are spaced from their real widths so large balances fit.
        var strip = new RoyalResourceBar { DrawsBar = false, BarRect = spec.Rect("resources"), Ink = new Color("e9e8e0") };
        _layer.AddChild(strip);
        strip.Add(spec, "resources", "0", HomeMapArt.Icon("gold"), "Royal storehouse", SceneRouter.Instance.GoToCashShop, "00,000");
        strip.Add(spec, "resources", "1", HomeMapArt.Icon("food"), state.FoodRechargeText, SceneRouter.Instance.GoToCashShop, "00 / 00");
        strip.Add(spec, "resources", "2", HomeMapArt.Icon("star"), "Player profile", SceneRouter.Instance.GoToProfile, "000");
        strip.SetValues(state.Gold.ToString("N0"), $"{state.Food} / {GameState.FoodRechargeCap}", state.TotalStarsEarned.ToString());
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var map = _home?.ActiveMapId ?? maps[0];
        var index = Array.IndexOf(maps, map);
        _layer.AddChild(spec.Label("zone.title", RouteCatalog.Get(map).Title, 200));
        _layer.AddChild(spec.Label("zone.subtitle", $"ZONE {index + 1:00}", 120));
        var previous = RoyalButton.Over(spec.Rect("zone.prev"), "Previous zone", () => { _home?.StepZone(-1); Rebuild(); }, 6);
        previous.SetGlyph(RoyalKit.Texture("icon-arrow-left"), new Rect2(spec.Rect("zone.prev.icon").Position - spec.Rect("zone.prev").Position, spec.Rect("zone.prev.icon").Size));
        previous.Disabled = index <= 0;
        var next = RoyalButton.Over(spec.Rect("zone.next"), "Next zone", () => { _home?.StepZone(1); Rebuild(); }, 6);
        next.SetGlyph(RoyalKit.Texture("icon-arrow-right"), new Rect2(spec.Rect("zone.next.icon").Position - spec.Rect("zone.next").Position, spec.Rect("zone.next.icon").Size));
        next.Disabled = index + 1 >= maps.Length || !GameState.Instance.IsAdventureZoneUnlocked(maps[index + 1]);
        _layer.AddChild(previous); _layer.AddChild(next);
        var settings = RoyalButton.Over(spec.Rect("settings"), "Settings", () => SceneRouter.Instance.GoToSettings(), 8);
        settings.SetGlyph(HomeMapArt.Icon("gear"), new Rect2(spec.Rect("settings.icon").Position - spec.Rect("settings").Position, spec.Rect("settings.icon").Size));
        _layer.AddChild(settings);
    }

    private void BuildCards(RoyalSpec spec)
    {
        var keys = new[] { "endless", "tower", "bounties", "bossrush", "weeklyraid", "event" };
        var activities = Activities().ToList();
        var content = spec.Rect("content");
        var scroll = new ScrollContainer { Position = content.Position, Size = content.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _layer.AddChild(scroll);
        var rows = Mathf.CeilToInt(activities.Count / 3f);
        var pitchY = spec.Rect("card.bossrush").Position.Y - spec.Rect("card.endless").Position.Y;
        var holder = new Control { CustomMinimumSize = new Vector2(content.Size.X, Mathf.Max(content.Size.Y, (rows - 2) * pitchY + content.Size.Y)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(holder);
        for (var i = 0; i < activities.Count; i++)
        {
            var key = "card." + keys[i % 6];
            var rect = spec.Rect(key);
            rect.Position += new Vector2(0, i / 6 * 2 * pitchY) - content.Position;
            holder.AddChild(Card(spec, key, rect, activities[i]));
        }
    }

    private Control Card(RoyalSpec spec, string key, Rect2 rect, Activity activity)
    {
        var origin = spec.Rect(key).Position;
        Rect2 Local(string part) => new(spec.Rect(key + part).Position - origin, spec.Rect(key + part).Size);
        var locked = activity.Locked != null;
        var card = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = activity.Title, TooltipText = activity.Locked ?? activity.Title,
            MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.PointingHand, Disabled = locked && activity.Open == null };
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
        title.Position -= origin;
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
        if (activity.Open != null) card.Pressed += () => { if (activity.Locked == null) activity.Open(); else RoyalToast.Show(this, activity.Locked); };
        return card;
    }

    private void BuildFooter(RoyalSpec spec)
    {
        void Button(string key, string icon, string text, Action action, Color? ink = null)
        {
            var rect = spec.Rect($"button.{key}");
            var button = RoyalButton.Over(rect, text, action, 6);
            var iconRect = spec.Rect($"button.{key}.icon");
            button.SetGlyph(RoyalKit.Texture(icon), new Rect2(iconRect.Position - rect.Position, iconRect.Size));
            var label = spec.Label($"button.{key}.label", text, rect.End.X - spec.Number($"button.{key}.label", "pen_x", rect.Position.X) - 6, ink);
            label.Position -= rect.Position;
            button.SetCaption(label, new Rect2(label.Position, label.Size));
            _layer.AddChild(button);
        }
        Button("account", "icon-account", "Account", () => AccountDialog.Show(this));
        Button("profile", "icon-profile", "Player profile", () => SceneRouter.Instance.GoToProfile());
        Button("quit", "icon-quit", "Quit game", () => MedievalUi.ShowConfirmation(this, "Leave Crownroad?", "Your progress is saved.", "Quit", () => GetTree().Quit()));
        Button("back", "icon-map-scroll", "BACK TO MAP", Close, new Color("391f0d"));
    }
}
