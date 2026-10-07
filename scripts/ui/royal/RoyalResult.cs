using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The approved victory board: crowned banner, stars earned, up to four reward tiles, the mastery card
/// and the two actions. Defeats and finished runs use the same board with their own title.
/// </summary>
public partial class RoyalResult : Control
{
    public bool Won;
    public string Title = "Victory";
    public string Detail = "";
    public int Stars;
    public IReadOnlyList<BattleReward> Rewards = Array.Empty<BattleReward>();
    public string LeaveText = "Back to map", RetryText = "Restart";
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
        "tomes" => ("icon-open-book", "TOMES"),
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

        var tiles = Rewards.Where(r => r.Amount > 0 && r.Kind != "mastery").Take(4).ToList();
        var mastery = Rewards.Where(r => r.Kind == "mastery" && GameData.PlayerRosterIds.Contains(r.ItemId)).ToList();
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
        if (tiles.Count == 0)
        {
            var none = RoyalText.Serif(Detail.Length > 0 ? Detail : "No rewards this time.", 24, new Color("e6d9be"), 500);
            none.Align = HorizontalAlignment.Center;
            RoyalText.Place(canvas, none, 340, 320, 600, 40);
        }
        else if (Detail.Length > 0)
        {
            var line = RoyalText.Serif(Detail, 18, new Color("e6d9be"), 500);
            line.Align = HorizontalAlignment.Center;
            RoyalText.Place(canvas, line, 340, 232, 600, 24);
        }
        if (mastery.Count > 0)
        {
            var rect = spec.Rect("mastery");
            canvas.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("result-mastery", 12))));
            var portrait = spec.Rect("mastery.portrait");
            var top = mastery.OrderByDescending(m => m.Amount).First();
            var figure = new UnitFigure { Position = portrait.Position + new Vector2(10, -6), Size = portrait.Size - new Vector2(20, 0) };
            figure.SetUnit(GameData.TryGetUnit(top.ItemId));
            canvas.AddChild(figure);
            var value = spec.Label("mastery.value", $"+{mastery.Sum(m => m.Amount):N0} XP", 200);
            value.Position -= new Vector2(0, 4);
            canvas.AddChild(value);
            var masteryCaption = spec.Label("mastery.caption", "MASTERY", 200);
            masteryCaption.FontSize = Mathf.Min(masteryCaption.FontSize, 14);
            masteryCaption.Position -= new Vector2(0, 7);
            canvas.AddChild(masteryCaption);
        }

        LeaveButton = RoyalButton.Over(spec.Rect("button.back"), LeaveText, () => Leave?.Invoke(), 8);
        LeaveButton.SetGlyph(RoyalKit.Texture("icon-map-large"), new Rect2(spec.Rect("button.back.icon").Position - spec.Rect("button.back").Position, spec.Rect("button.back.icon").Size));
        var leave = spec.Label("button.back.label", LeaveText.ToUpperInvariant(), 240);
        leave.Ink = new Color("160a03"); leave.ShadowInk = new Color(1, .95f, .8f, .3f);
        leave.Position -= spec.Rect("button.back").Position;
        LeaveButton.SetCaption(leave, new Rect2(leave.Position, leave.Size));
        canvas.AddChild(LeaveButton);
        var retryRect = spec.Rect("button.restart");
        RetryButton = RoyalButton.Over(retryRect, RetryText, () => Retry?.Invoke(), 8);
        RetryButton.SetGlyph(RoyalKit.Texture("icon-restart"), new Rect2(spec.Rect("button.restart.icon").Position - retryRect.Position, spec.Rect("button.restart.icon").Size));
        var retry = spec.Label("button.restart.label", RetryText.ToUpperInvariant(), RetryFoodCost > 0 ? 100 : 260);
        retry.Position -= retryRect.Position;
        RetryButton.SetCaption(retry, new Rect2(retry.Position, retry.Size));
        if (RetryFoodCost > 0)
        {
            RetryButton.AddChild(RoyalKit.Image("icon-wheat", new Rect2(spec.Rect("button.restart.cost.icon").Position - retryRect.Position, spec.Rect("button.restart.cost.icon").Size)));
            var cost = spec.Label("button.restart.cost", $"{RetryFoodCost} FOOD", 100);
            cost.Position -= retryRect.Position;
            RetryButton.AddChild(cost);
        }
        else
        {
            // No entry cost: the divider area is painted over so the action reads as one label.
            var plug = new ColorRect { Color = new Color("1f2a33"), Position = spec.Rect("button.restart.divider").Position - retryRect.Position - new Vector2(1, 0), Size = new Vector2(4, spec.Rect("button.restart.divider").Size.Y), MouseFilter = MouseFilterEnum.Ignore };
            RetryButton.AddChild(plug);
        }
        canvas.AddChild(RetryButton);
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
