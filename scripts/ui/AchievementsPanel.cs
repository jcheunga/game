using System;
using System.Linq;
using Godot;

/// <summary>
/// Achievements on the approved concept: six category tabs and six illustrated cards per page with
/// progress, and the claim / claimed state of each reward.
/// </summary>
public partial class AchievementsPanel : RoyalScreen
{
    private static readonly string[] Categories = { "All", "Campaign", "Combat", "Endless", "Collection", "Mastery" };
    private static readonly string[] CardKeys = { "firstblood", "districtmarshal", "bossslayer", "untouchable", "survivor", "arcanescholar" };
    private const int PageSize = 6;
    private int _category, _page;
    private Control _layer;

    public AchievementsPanel() { PlateName = "achievements"; }

    private static RoyalSpec Spec => RoyalSpec.For("achievements");

    protected override void Build()
    {
        _layer = Layer("Live");
        Refresh();
    }

    /// <summary>One of the six concept dioramas that best fits an achievement.</summary>
    private static string Diorama(AchievementDefinition entry)
    {
        var id = entry.Id;
        if (id == "first_blood") return "firstblood";
        if (id.Contains("endless") || id.Contains("tower") || id.Contains("streak") || id.Contains("daily")) return "survivor";
        if (id.Contains("boss") || id.Contains("raid") || id.Contains("hard_mode") || id.Contains("arena")) return "bossslayer";
        if (id.Contains("no_damage") || id.Contains("combo") || id.Contains("speed")) return "untouchable";
        if (id.Contains("spell") || id.Contains("codex") || id.Contains("talent") || id.Contains("enchant") || id.Contains("forge") || id.Contains("mastery") || id.Contains("master")) return "arcanescholar";
        if (entry.Category.Equals("campaign", StringComparison.OrdinalIgnoreCase) || id.Contains("district") || id.Contains("expedition") || id.Contains("guild")) return "districtmarshal";
        if (entry.Category.Equals("collection", StringComparison.OrdinalIgnoreCase)) return "arcanescholar";
        return "firstblood";
    }

    private void Refresh()
    {
        if (_layer == null) return;
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        var state = GameState.Instance;
        _layer.AddChild(spec.Label("title", "Achievements", 700));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
        for (var i = 0; i < Categories.Length; i++)
        {
            var index = i; var key = "tab." + Categories[i].ToLowerInvariant();
            var rect = spec.Rect(key);
            var tab = RoyalButton.Over(rect, Categories[i], () => { _category = index; _page = 0; Refresh(); }, 4);
            if (i == _category) tab.SetStates(RoyalKit.Slice("ach-tab-selected", 12), 4);
            tab.MarkTab(i == _category);
            var icon = spec.Rect(key + ".icon");
            tab.SetGlyph(RoyalKit.Texture("achtab-" + Categories[i].ToLowerInvariant()), new Rect2(icon.Position - rect.Position, icon.Size));
            var label = spec.Label(key + ".label", Categories[i], 140, i == _category ? new Color("f4eabf") : new Color("dcdddf"));
            label.Position -= rect.Position;
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
            _layer.AddChild(tab);
        }

        var entries = AchievementCatalog.GetAll().Where(a => _category == 0 || a.Category.Equals(Categories[_category], StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (entries.Length + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var shown = entries.Skip(_page * PageSize).Take(PageSize).ToArray();
        for (var i = 0; i < shown.Length; i++) _layer.AddChild(Card(spec, i, shown[i]));

        var back = spec.Rect("button.back");
        var backButton = RoyalButton.Over(back, "Back to map", Close, 6);
        backButton.SetGlyph(RoyalKit.Texture("icon-back-chevron"), new Rect2(spec.Rect("button.back.icon").Position - back.Position, spec.Rect("button.back.icon").Size));
        var backLabel = spec.Label("button.back.label", "BACK TO MAP", 200);
        backLabel.Position -= back.Position;
        backButton.SetCaption(backLabel, new Rect2(backLabel.Position, backLabel.Size));
        _layer.AddChild(backButton);
        if (pages > 1)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var direction = side;
                var rect = new Rect2(back.GetCenter().X + side * (back.Size.X / 2 + 50) - 21, back.GetCenter().Y - 21, 42, 42);
                var arrow = RoyalButton.Over(rect, side < 0 ? "Previous achievement page" : "Next achievement page", () => { _page += direction; Refresh(); }, 6);
                arrow.SetStates(RoyalKit.Slice("chevron-button", 8), 6);
                arrow.SetGlyph(RoyalKit.Texture("chevron"), new Rect2(14, 10, 14, 22));
                if (side < 0) arrow.Glyph.FlipH = true;
                arrow.Disabled = side < 0 ? _page == 0 : _page >= pages - 1;
                _layer.AddChild(arrow);
            }
            var page = RoyalText.Caps($"{_page + 1} / {pages}", 15, RoyalText.Muted);
            page.Align = HorizontalAlignment.Center;
            RoyalText.Place(_layer, page, back.GetCenter().X + back.Size.X / 2 + 76, back.GetCenter().Y - 12, 60, 24);
        }
    }

    private Control Card(RoyalSpec spec, int slot, AchievementDefinition entry)
    {
        var state = GameState.Instance;
        // Every card uses the concept's card at its grid position; the first-row card supplies the layout.
        var key = "card." + CardKeys[slot];
        var cell = spec.Rect(key);
        var template = slot < 3 ? "card.firstblood" : "card.untouchable";
        var origin = spec.Rect(template).Position;
        Rect2 Part(string part) => new(spec.Rect(template + part).Position - origin, spec.Rect(template + part).Size);
        bool done = state.IsAchievementUnlocked(entry.Id), claimed = state.HasClaimedAchievementReward(entry.Id);
        var ready = done && !claimed;
        var card = new Control { Position = cell.Position, Size = new Vector2(cell.Size.X, slot < 3 ? 190 : 201), MouseFilter = MouseFilterEnum.Pass };
        card.AddChild(new Panel { Size = card.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice(ready ? "ach-card-ready" : "ach-card", 14))));
        var art = Part(".art");
        card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = RoyalKit.Texture("ach-art-" + Diorama(entry)), Position = art.Position, Size = art.Size, MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = done ? Colors.White : new Color(.72f, .72f, .74f) });
        card.AddChild(RoyalKit.Image("achicon-" + Diorama(entry), Part(".icon")));
        var title = spec.Label(template + ".title", entry.Title, card.Size.X - (spec.Number(template + ".title", "pen_x", 0) - origin.X) - 10);
        title.Position -= origin;
        card.AddChild(title);
        var description = RoyalText.Paragraph(entry.Description, 17, new Color("dedfdf"), 450);
        var descTop = spec.Number(template + ".desc.1", "baseline", 0) - origin.Y - 15;
        var trackRect = Part(".track");
        description.Position = new Vector2(spec.Number(template + ".desc.1", "x", 0) - origin.X, descTop);
        description.Size = new Vector2(card.Size.X - description.Position.X - 12, trackRect.Position.Y - descTop - 2);
        description.AddThemeConstantOverride("line_spacing", -2);
        RoyalText.FitLines(description, slot < 3 ? 2 : 3, 13);
        card.AddChild(description);

        var (value, target) = Progress(entry.Id);
        if (done) value = target;
        card.AddChild(new Panel { Position = trackRect.Position, Size = trackRect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("ach-track", 7, 5, 7, 5))));
        var fill = Part(".fill");
        var fraction = target <= 0 ? 0 : Mathf.Clamp(value / (float)target, 0, 1);
        if (fraction > 0)
            card.AddChild(new Panel { Position = fill.Position, Size = new Vector2(Mathf.Max(10, fill.Size.X * fraction), fill.Size.Y), MouseFilter = MouseFilterEnum.Ignore }
                .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice(done ? "ach-fill-gold" : "ach-fill-blue", 5, 3, 5, 3))));
        var progress = spec.Label(template + ".progress", $"{value} / {target}", 120);
        progress.Position -= origin;
        card.AddChild(progress);

        var reward = AchievementRewardCatalog.GetForAchievement(entry.Id);
        var buttonRect = Part(".button");
        if (ready && reward != null)
        {
            var claim = RoyalButton.Over(buttonRect, $"Claim {reward.RewardLabel}", () =>
            {
                state.TryClaimAchievementReward(entry.Id, out var message);
                RoyalToast.Show(this, message);
                Refresh();
            }, 6);
            claim.SetStates(RoyalKit.Slice("ach-claim", 12), 6);
            var label = RoyalText.Caps($"CLAIM +{reward.RewardAmount:N0}", 17, new Color("341f0b"), 700);
            label.ShadowInk = new Color(1, .95f, .8f, .3f);
            label.Align = HorizontalAlignment.Center;
            claim.SetCaption(label, new Rect2(8, 0, buttonRect.Size.X - 52, buttonRect.Size.Y));
            claim.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = UiArtLoader.TryLoadRewardIcon(reward.RewardType, reward.RewardItemId) ?? RoyalKit.Texture("coin-small"),
                Position = new Vector2(buttonRect.Size.X - 46, 8), Size = new Vector2(34, buttonRect.Size.Y - 16), MouseFilter = MouseFilterEnum.Ignore });
            card.AddChild(claim);
        }
        else if (claimed)
        {
            var plate = new Panel { Position = buttonRect.Position, Size = buttonRect.Size, MouseFilter = MouseFilterEnum.Pass,
                TooltipText = reward != null ? $"Claimed {reward.RewardLabel}" : "Claimed" };
            plate.AddThemeStyleboxOverride("panel", RoyalKit.Slice("ach-claimed", 24, 10, 24, 10));
            card.AddChild(plate);
            var label = RoyalText.Caps("CLAIMED", 16, new Color("e5c79b"), 600);
            var width = label.TextWidth(16);
            label.Position = new Vector2(buttonRect.GetCenter().X - width / 2 + 11, buttonRect.Position.Y); label.Size = new Vector2(width + 4, buttonRect.Size.Y);
            card.AddChild(label);
            card.AddChild(RoyalKit.Image("icon-check", new Rect2(label.Position.X - 25, buttonRect.GetCenter().Y - 7, 17, 14)));
        }
        else if (reward != null)
        {
            var hint = RoyalText.Serif($"Reward · {reward.RewardLabel}", 15, new Color("a9a49a"), 500);
            hint.Align = HorizontalAlignment.Center;
            hint.Position = buttonRect.Position; hint.Size = buttonRect.Size;
            card.AddChild(hint);
        }
        card.AccessibilityName = entry.Title;
        return card;
    }

    private static (int Value, int Target) Progress(string id)
    {
        var s = GameState.Instance;
        (int, int) result = id switch {
            "campaign_complete" => (CampaignPlanCatalog.GetAll().Count(d => s.IsDistrictCleared(d.Id)), CampaignPlanCatalog.GetAll().Count),
            "all_stars" => (Enumerable.Range(1, s.MaxStage).Count(stage => s.GetStageStars(stage) >= 3), 40),
            "endless_30" => (s.BestEndlessWave, 30), "endless_60" => (s.BestEndlessWave, 60), "endless_90" => (s.BestEndlessWave, 90),
            "relic_collector" => (s.GetOwnedEquipment().Count, 6), "full_armory" => (s.GetOwnedEquipment().Count, 12),
            "full_roster" => (GameData.GetPlayerUnits().Count(unit => s.IsUnitOwned(unit.Id)), GameData.PlayerRosterIds.Length),
            "all_spells" => (GameData.GetPlayerSpells().Count(spell => s.IsSpellOwned(spell.Id)), GameData.PlayerSpellIds.Length),
            "codex_10" => (s.DiscoveredCodexCount, 10), "codex_complete" => (s.DiscoveredCodexCount, CodexCatalog.TotalEntries),
            "tower_25" => (s.TowerHighestFloor, 25), "tower_50" => (s.TowerHighestFloor, 50), "tower_100" => (s.TowerHighestFloor, 100),
            "expedition_10" => (s.TotalExpeditionsCompleted, 10), "hard_mode_10" => (s.HardModeClearedCount, 10),
            _ => (s.IsAchievementUnlocked(id) ? 1 : 0, 1) };
        return (Math.Min(result.Item1, result.Item2), result.Item2);
    }
}
