using System;
using System.Linq;
using Godot;

/// <summary>
/// Season rewards on the approved concept: the season band with the tier and XP toward the next,
/// a rail of five tiers starting just before the current one, and free and premium reward cards
/// that are claimed, ready to claim or locked.
/// </summary>
public partial class SeasonPassMenu : RoyalScreen
{
    private int _first = -1;
    private Control _layer;

    public SeasonPassMenu() { PlateName = "season"; }

    private static RoyalSpec Spec => RoyalSpec.For("season");

    protected override void Build()
    {
        _layer = Layer("Live");
        Refresh();
    }

    private static string RewardArt(string type, int amount) => type switch
    {
        "gold" => amount >= 200 ? "season-gold-large" : "season-gold",
        "food" => "season-food",
        "tomes" or "tome" => "season-tome",
        "sigils" or "sigil" => "relicicon-crown",
        "shards" => "forge-shard",
        _ => ""
    };

    private void Refresh()
    {
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        var state = GameState.Instance;
        var tiers = SeasonPassCatalog.GetAll();
        var current = state.SeasonPassTier;
        if (_first < 1) _first = Math.Clamp(current - 1, 1, Math.Max(1, tiers.Count - 4));
        _layer.AddChild(spec.Label("title", "Season Rewards", 600));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));

        var ink = new Color("080605");
        var season = spec.Label("band.season", $"Season {SeasonPassCatalog.CurrentSeasonId.TrimStart('S')}", 200, ink);
        season.ShadowOffset = Vector2.Zero; _layer.AddChild(season);
        var tierLabel = spec.Label("band.tier", $"Tier {current}", 140, ink);
        tierLabel.ShadowOffset = Vector2.Zero; _layer.AddChild(tierLabel);
        var max = current >= SeasonPassCatalog.MaxTier;
        var from = current > 0 ? SeasonPassCatalog.GetXPForTier(current) : 0;
        var to = SeasonPassCatalog.GetXPForTier(Math.Min(current + 1, SeasonPassCatalog.MaxTier));
        var progress = max ? 1f : Mathf.Clamp((state.SeasonPassXP - from) / (float)Math.Max(1, to - from), 0, 1);
        var fill = spec.Rect("band.xp.fill");
        var track = spec.Rect("band.xp.track");
        if (progress > 0)
            _layer.AddChild(new Panel { Position = fill.Position, Size = new Vector2(Mathf.Max(12, (track.Size.X - 10) * progress), fill.Size.Y), MouseFilter = MouseFilterEnum.Ignore }
                .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("season-xp-fill", 6, 4, 6, 4))));
        var xp = spec.Label("band.xp.label", max ? "MAX TIER" : $"{state.SeasonPassXP - from:N0} / {to - from:N0} XP", 200, new Color("0c0a07"));
        xp.ShadowOffset = Vector2.Zero; _layer.AddChild(xp);

        for (var i = 0; i < 5; i++)
        {
            var tierNumber = _first + i;
            var tier = tiers.FirstOrDefault(t => t.Tier == tierNumber);
            var badgeKey = $"tier.{7 + i}";
            if (tierNumber == current) _layer.AddChild(RoyalKit.Image("season-tier-current", spec.Rect(badgeKey).Grow(6)));
            var number = spec.Label(badgeKey + ".label", tierNumber.ToString(), 60);
            _layer.AddChild(number);
            if (tier == null) continue;
            Card(spec, $"reward.free.{7 + i}", tier.Tier, tier.FreeRewardType, tier.FreeRewardAmount, tier.FreeRewardLabel, "",
                state.HasClaimedSeasonFreeTier(tier.Tier), tier.Tier <= current, false);
            Card(spec, $"reward.premium.{7 + i}", tier.Tier, tier.PremiumRewardType, tier.PremiumRewardAmount, tier.PremiumRewardLabel, tier.PremiumRewardItemId,
                state.HasClaimedSeasonPremiumTier(tier.Tier), tier.Tier <= current && state.HasPremiumPass, true);
        }
        _layer.AddChild(RoyalKit.Image("season-row-free", spec.Rect("row.free")));
        _layer.AddChild(RoyalKit.Image("season-row-premium", spec.Rect("row.premium")));
        _layer.AddChild(spec.Label("row.free.label", "FREE", 100));
        _layer.AddChild(spec.Label("row.premium.label", "PREMIUM", 120));
        var rail = spec.Rect("rail");
        for (var side = -1; side <= 1; side += 2)
        {
            var direction = side;
            var rect = new Rect2(side < 0 ? rail.Position.X - 30 : rail.End.X - 2, rail.GetCenter().Y - 16, 30, 32);
            var arrow = RoyalButton.Over(rect, side < 0 ? "Earlier tiers" : "Later tiers", () => { _first = Math.Clamp(_first + direction * 5, 1, Math.Max(1, tiers.Count - 4)); Refresh(); }, 4);
            arrow.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(9, 6, 12, 20));
            arrow.Disabled = side < 0 ? _first <= 1 : _first + 5 > tiers.Count;
            _layer.AddChild(arrow);
        }
        var back = spec.Rect("button.map");
        var backButton = RoyalButton.Over(back, "Back to map", Close, 6);
        var label = spec.Label("button.map.label", "Back to Map", 220);
        label.Position -= back.Position;
        backButton.SetCaption(label, new Rect2(label.Position, label.Size));
        _layer.AddChild(backButton);
        if (!state.HasPremiumPass)
        {
            var premium = spec.Rect("row.premium");
            var unlock = RoyalButton.Over(new Rect2(premium.Position.X + 20, premium.End.Y - 44, premium.Size.X - 40, 34), "Unlock premium", () => SceneRouter.Instance.GoToCashShop(), 6);
            unlock.SetStates(RoyalKit.Slice("season-claim", 12), 6);
            var text = RoyalText.Caps("UNLOCK", 15, new Color("1a0d04"), 700);
            text.Align = HorizontalAlignment.Center; text.ShadowOffset = Vector2.Zero;
            unlock.SetCaption(text, new Rect2(0, 0, premium.Size.X - 40, 34));
            _layer.AddChild(unlock);
        }
    }

    private void Card(RoyalSpec spec, string key, int tier, string type, int amount, string label, string itemId, bool claimed, bool unlocked, bool premium)
    {
        var rect = spec.Rect(key);
        var ready = unlocked && !claimed;
        var card = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass,
            TooltipText = claimed ? $"{label} · claimed" : ready ? $"Claim {label}" : premium && !GameState.Instance.HasPremiumPass ? $"{label} · premium" : $"{label} · tier {tier}" };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice(ready ? "season-card-ready" : "season-card", 12, 12, 12, 54));
        _layer.AddChild(card);
        var artRect = spec.Rect(key + ".art");
        var artKey = RewardArt(type, amount);
        var art = artKey.Length > 0 ? RoyalKit.Texture(artKey) : UiArtLoader.TryLoadRewardIcon(type, itemId);
        card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = art, Position = new Vector2(rect.Size.X / 2 - 55, artRect.Position.Y - rect.Position.Y - 4), Size = new Vector2(110, artRect.Size.Y + 8),
            MouseFilter = MouseFilterEnum.Ignore, SelfModulate = unlocked || claimed ? Colors.White : new Color(.75f, .75f, .78f) });
        var caption = spec.Label(key + ".caption", label.ToUpperInvariant(), rect.Size.X - 12);
        caption.Position = new Vector2(6, caption.Position.Y - rect.Position.Y); caption.Size = new Vector2(rect.Size.X - 12, caption.Size.Y);
        card.AddChild(caption);
        var buttonKey = key + ".button";
        var buttonRect = new Rect2(spec.Rect(buttonKey).Position - rect.Position, spec.Rect(buttonKey).Size);
        if (claimed)
        {
            card.AddChild(new Panel { Position = buttonRect.Position, Size = buttonRect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("season-claimed", 22, 8, 10, 8))));
            card.AddChild(RoyalKit.Image("icon-check-green", new Rect2(buttonRect.Position + new Vector2(30, 12), new Vector2(18, 17))));
            var text = RoyalText.Serif("CLAIMED", 16, new Color("a4daa0"));
            text.Position = buttonRect.Position + new Vector2(62, 0); text.Size = new Vector2(100, buttonRect.Size.Y);
            card.AddChild(text);
            card.AddChild(RoyalKit.Image("season-corner-check", new Rect2(rect.Size.X - 39, 0, 39, 39)));
        }
        else if (ready)
        {
            var claim = RoyalButton.Over(buttonRect, $"Claim {label}", () =>
            {
                var ok = GameState.Instance.TryClaimSeasonReward(tier, premium, out var message);
                RoyalToast.Show(this, message);
                if (ok) Refresh();
            }, 6);
            claim.SetStates(RoyalKit.Slice("season-claim", 12), 6);
            var text = RoyalText.Serif("CLAIM", 21, new Color("080601"));
            text.Align = HorizontalAlignment.Center; text.ShadowOffset = Vector2.Zero;
            claim.SetCaption(text, new Rect2(0, 0, buttonRect.Size.X, buttonRect.Size.Y));
            card.AddChild(claim);
        }
        else
        {
            card.AddChild(new Panel { Position = buttonRect.Position, Size = buttonRect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("season-locked", 18, 8, 10, 8))));
            card.AddChild(RoyalKit.Image("icon-lock-small", new Rect2(buttonRect.Position + new Vector2(32, 11), new Vector2(15, 20))));
            var text = RoyalText.Serif("LOCKED", 17, new Color("b7b6b6"));
            text.Position = buttonRect.Position + new Vector2(66, 0); text.Size = new Vector2(100, buttonRect.Size.Y);
            card.AddChild(text);
        }
    }
}
