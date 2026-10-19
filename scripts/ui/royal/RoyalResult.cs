using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The approved victory board: crowned banner, stars earned, up to four reward tiles, the mastery and
/// player level cards and the two actions. Defeats and finished runs use the same board with their own title.
/// </summary>
public partial class RoyalResult : Control
{
    public bool Won;
    public string Title = "Victory";
    public string Detail = "";
    public int Stars;
    public IReadOnlyList<BattleReward> Rewards = Array.Empty<BattleReward>();
    public string LeaveText = MapNavigation.Caption, RetryText = "Restart";
    public int RetryFoodCost;
    public Action Leave, Retry;
    public RoyalButton RetryButton { get; private set; }
    public RoyalButton LeaveButton { get; private set; }

    private static RoyalSpec Spec => RoyalSpec.For("victory");

    public RoyalResult() { MouseFilter = MouseFilterEnum.Stop; Name = "RoyalResult"; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color(.03f, .04f, .05f, .38f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var canvas = new Control { Position = Vector2.Zero, Size = RoyalArt.Canvas, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var board = Spec.Rect("panel.banner");
        canvas.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = RoyalArt.Cut("victory", board), Position = board.Position, Size = board.Size, MouseFilter = MouseFilterEnum.Ignore });
        Build(canvas);
    }

    private static (string Icon, string Caption) TileArt(BattleReward reward) => reward.Kind switch
    {
        "gold" => ("reward-gold", "GOLD"),
        "food" => ("reward-rations", "RATIONS"),
        "shards" => ("reward-shards", "SHARDS"),
        "season_xp" => ("reward-season", "SEASON"),
        "sigils" => ("relicicon-crown", "SIGILS"),
        "essence" => ("reward-shards", "ESSENCE"),
        "relic" => ("", "RELIC"),
        "unit" => ("", "RECRUIT"),
        "spell" => ("", "SPELL"),
        "training" => ("", "TRAINING"),
        _ => ("reward-gold", reward.Kind.ToUpperInvariant())
    };

    private static Texture2D TileTexture(BattleReward reward)
    {
        var (icon, _) = TileArt(reward);
        if (icon.Length > 0) return RoyalKit.Texture(icon);
        return reward.Kind switch
        {
            "relic" => ShopMenu.RelicArt(GameData.GetEquipment(reward.ItemId)),
            "spell" => GameData.TryGetSpell(reward.ItemId) is { } spell ? UiArtLoader.TryLoadSpellIcon(spell) : null,
            _ => GameData.TryGetUnit(reward.ItemId) is { } unit ? UnitFigure.For(unit) : null
        };
    }

    private void Build(Control canvas)
    {
        var spec = Spec;
        var title = spec.Label("title", Title, 600);
        if (!Won) { title.Gold = false; title.Ink = new Color("e2a597"); }
        canvas.AddChild(title);
        var starLeft = spec.Rect("star.1"); var starPitch = spec.Rect("star.2").Position.X - starLeft.Position.X;
        for (var i = 0; i < 3; i++)
            canvas.AddChild(RoyalKit.Image("result-star", new Rect2(starLeft.Position + new Vector2(i * starPitch, 0), starLeft.Size),
                i < Stars ? Colors.White : new Color(.22f, .2f, .2f, .85f)));
        canvas.AddChild(RoyalKit.Image("result-diamond", spec.Rect("stars.diamond.left")));
        canvas.AddChild(RoyalKit.Image("result-diamond", spec.Rect("stars.diamond.right")));

        var tiles = Rewards.Where(r => r.Amount > 0 && r.Kind is not ("mastery" or "player_xp")).Take(4).ToList();
        var mastery = Rewards.Where(r => r.Kind == "mastery" && GameData.PlayerRosterIds.Contains(r.ItemId)).ToList();
        var experience = Rewards.FirstOrDefault(r => r.Kind == "player_xp" && r.Amount > 0);
        var tileRects = Enumerable.Range(1, 4).Select(i => spec.Rect($"reward.{i}")).ToArray();
        var pitch = tileRects[1].Position.X - tileRects[0].Position.X;
        var rowWidth = tiles.Count * pitch - (pitch - tileRects[0].Size.X);
        var left = 641 - rowWidth / 2;
        for (var i = 0; i < tiles.Count; i++)
        {
            var origin = tileRects[0].Position;
            var rect = new Rect2(left + i * pitch, origin.Y, tileRects[0].Size.X, tileRects[0].Size.Y);
            var tile = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass, TooltipText = $"{tiles[i].Amount:N0} {TileArt(tiles[i]).Caption.ToLowerInvariant()}" };
            tile.AddThemeStyleboxOverride("panel", RoyalKit.Slice("result-tile", 12));
            var icon = spec.Rect("reward.1.icon");
            tile.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = TileTexture(tiles[i]), Position = new Vector2(rect.Size.X / 2 - icon.Size.X / 2 - 4, icon.Position.Y - origin.Y - 8), Size = icon.Size + new Vector2(8, 8),
                MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            var suffix = tiles[i].Kind == "season_xp" ? " XP" : "";
            var amount = spec.Label("reward.1.amount", tiles[i].Kind == "training" ? $"Lv {tiles[i].Amount}" : $"+{tiles[i].Amount:N0}{suffix}", rect.Size.X - 8);
            // The tile's painted rim is deeper than the concept's, so the text sits higher with air above the rim.
            amount.Position = new Vector2(4, amount.Position.Y - origin.Y - 7); amount.Size = new Vector2(rect.Size.X - 8, amount.Size.Y);
            tile.AddChild(amount);
            var caption = spec.Label("reward.1.caption", TileArt(tiles[i]).Caption, rect.Size.X - 8);
            caption.FontSize = Mathf.Min(caption.FontSize, 12);
            caption.Position = new Vector2(4, caption.Position.Y - origin.Y - 10); caption.Size = new Vector2(rect.Size.X - 8, caption.Size.Y);
            tile.AddChild(caption);
            canvas.AddChild(tile);
        }
        var cards = mastery.Count > 0 || experience != null;
        if (tiles.Count == 0 && (Detail.Length > 0 || !cards))
        {
            var none = RoyalText.Serif(Detail.Length > 0 ? Detail : "No rewards this time.", 24, new Color("e6d9be"), 500);
            none.Align = HorizontalAlignment.Center;
            RoyalText.Place(canvas, none, 340, cards ? 286 : 320, 600, 40);
        }
        else if (Detail.Length > 0)
        {
            var line = RoyalText.Serif(Detail, 18, new Color("e6d9be"), 500);
            line.Align = HorizontalAlignment.Center;
            RoyalText.Place(canvas, line, 340, 232, 600, 24);
        }
        // Mastery and the player's level share the row under the tiles: one card sits centred, two span the tile row.
        var slot = spec.Rect("mastery");
        if (tiles.Count == 0) slot.Position = new Vector2(slot.Position.X, 349);
        var paired = mastery.Count > 0 && experience != null;
        var row = new Rect2(tileRects[0].Position.X, slot.Position.Y, tileRects[3].End.X - tileRects[0].Position.X, slot.Size.Y);
        var masteryRect = paired ? new Rect2(row.Position, new Vector2(row.Size.X / 2 - 7, row.Size.Y)) : slot;
        var levelRect = paired ? new Rect2(row.Position.X + row.Size.X / 2 + 7, row.Position.Y, row.Size.X / 2 - 7, row.Size.Y) : slot;
        if (mastery.Count > 0)
        {
            var rect = masteryRect;
            var shift = rect.Position - spec.Rect("mastery").Position;
            canvas.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("result-mastery", 12))));
            var portrait = spec.Rect("mastery.portrait");
            portrait.Position += shift;
            var top = mastery.OrderByDescending(m => m.Amount).First();
            var figure = new UnitFigure { Position = portrait.Position + new Vector2(10, -6), Size = portrait.Size - new Vector2(20, 0) };
            figure.SetUnit(GameData.TryGetUnit(top.ItemId));
            canvas.AddChild(figure);
            // The text centres between the portrait and the card's right edge, wherever the card sits.
            var textShift = (portrait.End.X + rect.End.X) / 2 - (spec.Rect("mastery.portrait").End.X + spec.Rect("mastery").End.X) / 2;
            var value = spec.Label("mastery.value", $"+{mastery.Sum(m => m.Amount):N0} XP", 200);
            value.Position += new Vector2(textShift, shift.Y - 4);
            canvas.AddChild(value);
            var masteryCaption = spec.Label("mastery.caption", "MASTERY", 200);
            masteryCaption.FontSize = Mathf.Min(masteryCaption.FontSize, 14);
            masteryCaption.Position += new Vector2(textShift, shift.Y - 7);
            canvas.AddChild(masteryCaption);
        }
        if (experience != null) BuildLevelCard(canvas, levelRect, experience.Amount);

        // The pair sits on the plate's painted buttons, gold to leave and steel to try again, and reads as a pair:
        // one capitals size on one line, each button's icon and words centred together.
        var leaveName = MapNavigation.Normalize(LeaveText);
        LeaveButton = RoyalButton.Over(spec.Rect("button.back"), leaveName, () => Leave?.Invoke(), 8);
        var leave = RoyalText.Caps(leaveName.ToUpperInvariant(), ActionSize, new Color("1e0f04"), 700);
        Arrange(LeaveButton, leave, RoyalKit.Texture("icon-map-large"), new Vector2(48, 40));
        // Dark lettering pressed into the gold: a faint light edge below it, never a dark drop shadow.
        leave.ShadowOffset = new Vector2(0, 1); leave.ShadowInk = new Color(1f, .92f, .7f, .35f);
        canvas.AddChild(LeaveButton);
        RetryButton = RoyalButton.Over(spec.Rect("button.restart"), RetryText, () => Retry?.Invoke(), 8);
        var retry = RoyalText.Caps(RetryText.ToUpperInvariant(), ActionSize, new Color("fff1d0"), 700);
        var cost = RetryFoodCost > 0 ? RoyalText.Caps($"{RetryFoodCost} FOOD", 18, new Color("f3d48a"), 700) : null;
        Arrange(RetryButton, retry, RoyalKit.Texture("icon-restart"), new Vector2(28, 28), cost);
        canvas.AddChild(RetryButton);
    }

    private const int ActionSize = 22;

    /// <summary>
    /// Centres a button's icon and caption as one row. A cost follows after an engraved divider, and dims with
    /// the button when it is disabled (no food to try again).
    /// </summary>
    private static void Arrange(RoyalButton button, RoyalLabel caption, Texture2D glyph, Vector2 glyphSize, RoyalLabel cost = null)
    {
        const float gap = 12, rule = 20;
        var wheat = new Vector2(38, 32);
        var height = button.Size.Y;
        var words = Mathf.Ceil(caption.TextWidth(caption.FontSize));
        var priced = cost == null ? 0 : rule * 2 + 2 + wheat.X + 6 + Mathf.Ceil(cost.TextWidth(cost.FontSize));
        var x = Mathf.Round((button.Size.X - glyphSize.X - gap - words - priced) / 2);
        button.SetGlyph(glyph, new Rect2(x, Mathf.Round((height - glyphSize.Y) / 2), glyphSize));
        x += glyphSize.X + gap;
        button.SetCaption(caption, new Rect2(x, 0, words + 2, height));
        if (cost == null) return;
        var row = new Control { Name = "Cost", Position = new Vector2(x + words + rule, 0), Size = new Vector2(priced - rule, height), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(new ColorRect { Color = new Color(0, 0, 0, .45f), Position = new Vector2(0, height / 2 - 16), Size = new Vector2(1, 32), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(new ColorRect { Color = new Color(.95f, .82f, .55f, .4f), Position = new Vector2(1, height / 2 - 16), Size = new Vector2(1, 32), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(RoyalKit.Image("icon-wheat", new Rect2(2 + rule, Mathf.Round((height - wheat.Y) / 2), wheat)));
        RoyalText.Place(row, cost, 2 + rule + wheat.X + 6, 0, cost.TextWidth(cost.FontSize) + 2, height);
        button.AddChild(row);
        button.Draw += () => row.Modulate = button.Disabled ? new Color(1, 1, 1, .55f) : Colors.White;
    }

    /// <summary>The player's level: its medallion, the XP this battle paid and the bar toward the next level.</summary>
    private void BuildLevelCard(Control canvas, Rect2 rect, int gained)
    {
        var state = GameState.Instance;
        var level = state.PlayerLevel;
        var levelledUp = PlayerLevelCatalog.LevelForXP(state.PlayerXP - gained) < level;
        var progress = state.IsMaxPlayerLevel ? 1f : state.PlayerXPIntoLevel / (float)Mathf.Max(1, state.PlayerXPForNextLevel);
        var unlocks = string.Join(" · ", PlayerLevelCatalog.Unlocks(level).Select(u => u.Label.ToLowerInvariant()));
        var card = new Panel { Name = "LevelCard", Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass,
            TooltipText = levelledUp ? $"Level {level} reached · reward ready on the level track{(unlocks.Length > 0 ? $" · {unlocks}" : "")}"
                : state.IsMaxPlayerLevel ? $"Level {level} · highest level" : $"Level {level} · {state.PlayerXPIntoLevel:N0} / {state.PlayerXPForNextLevel:N0} XP" };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice("result-mastery", 12));
        canvas.AddChild(card);
        var medallion = new LevelMedallion { Level = level, Progress = progress, Lit = levelledUp, Position = new Vector2(18, 11), Size = new Vector2(87, 87) };
        card.AddChild(medallion);
        var left = 118f; var width = rect.Size.X - left - 18;
        var value = RoyalText.Serif($"+{gained:N0} XP", 34, new Color("f9ecce"));
        value.Align = HorizontalAlignment.Center;
        RoyalText.Place(card, value, left, 12, width, 40);
        card.AddChild(new Panel { Position = new Vector2(left + 8, 58), Size = new Vector2(width - 16, 12), MouseFilter = MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("ach-track", 7, 5, 7, 5))));
        if (progress > 0)
            card.AddChild(new Panel { Position = new Vector2(left + 10, 60), Size = new Vector2(Mathf.Max(10, (width - 20) * progress), 8), MouseFilter = MouseFilterEnum.Ignore }
                .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("ach-fill-gold", 5, 3, 5, 3))));
        var caption = RoyalText.Caps(levelledUp ? $"LEVEL UP · {level}" : $"LEVEL {level}", levelledUp ? 16 : 14,
            levelledUp ? new Color("f6d98f") : new Color("c8bca7"), levelledUp ? 700 : 550);
        caption.Align = HorizontalAlignment.Center;
        RoyalText.Place(card, caption, left, 74, width, 24);
        if (!levelledUp || (GameState.Instance?.ReducedMotion ?? false)) return;
        medallion.PivotOffset = medallion.Size / 2;
        var pulse = medallion.CreateTween().SetLoops(2);
        pulse.TweenProperty(medallion, "scale", new Vector2(1.12f, 1.12f), .28f).SetDelay(.45f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(medallion, "scale", Vector2.One, .32f).SetTrans(Tween.TransitionType.Sine);
    }

    public void Appear()
    {
        if (GameState.Instance?.ReducedMotion ?? false) return;
        Modulate = new Color(1, 1, 1, 0);
        var board = GetChild<Control>(1);
        board.PivotOffset = new Vector2(641, 360);
        board.Scale = new Vector2(.94f, .94f);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "modulate:a", 1f, .35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(board, "scale", Vector2.One, .4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}
