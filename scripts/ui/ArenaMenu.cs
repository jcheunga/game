using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The arena on a painted board: your rank insignia, rating and record over the tier ladder on the left, and
/// three opponents on the right, each with their leading fighter in the arena, their rank, power, warband and
/// Challenge.
/// </summary>
public partial class ArenaMenu : RoyalBoard
{
    private readonly List<ArenaOpponentSnapshot> _opponents = new();

    public ArenaMenu() : base("Arena", "arena") { }

    private static Texture2D Insignia(ArenaTier tier) => RoyalArt.Item("arena-" + tier.Id);

    protected override void Build()
    {
        var rating = GameState.Instance.ArenaRating;
        for (var i = 0; i < 3; i++) _opponents.Add(ArenaCatalog.GenerateLocalOpponent(rating, i));
        base.Build();
    }

    protected override void Compose()
    {
        var state = GameState.Instance;
        Chip("coin-small", state.Gold.ToString("N0"), $"{state.Gold:N0} gold", RewardArt("gold"));
        Chip("emblem-arena", $"{state.ArenaRating} rating", "Your arena rating");
        var body = Board.Body; var footer = Board.Footer;
        Caps("Your standing", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 300, 40), 18);
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 340, footer.Position.Y - 10 - Board.Notch.End.Y);
        Standing(left);
        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        var width = (right.Size.X - 24) / 3;
        for (var i = 0; i < _opponents.Count; i++)
            Opponent(new Rect2(right.Position.X + i * (width + 12), right.Position.Y, width, right.Size.Y), _opponents[i]);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        Action(new Rect2(footer.Position.X + 184, footer.Position.Y, 176, footer.Size.Y), "Armory", "icon-armory", () => SceneRouter.Instance.GoToShop(), size: 17);
        NoticeAt(new Rect2(footer.Position.X + 376, footer.Position.Y, footer.Size.X - 376, footer.Size.Y));
    }

    private void Standing(Rect2 column)
    {
        var state = GameState.Instance;
        var tier = state.GetArenaTier();
        var tiers = ArenaCatalog.GetAllTiers();
        var card = new Rect2(column.Position, new Vector2(column.Size.X, 206));
        Frame("ach-card", card, 14);
        Image(Insignia(tier), new Rect2(card.Position + new Vector2(12, 14), new Vector2(124, 124)));
        Heading(tier.Title, new Rect2(card.Position.X + 146, card.Position.Y + 18, card.Size.X - 156, 40), 30);
        Serif($"{state.ArenaRating} rating", new Rect2(card.Position.X + 146, card.Position.Y + 62, card.Size.X - 156, 30), 22, new Color("f6e2ad"), weight: 700);
        Serif($"{state.ArenaWins} wins · {state.ArenaLosses} losses", new Rect2(card.Position.X + 146, card.Position.Y + 94, card.Size.X - 156, 30), 19, Soft, weight: 500);
        var index = tiers.ToList().FindIndex(t => t.Id == tier.Id);
        var next = index + 1 < tiers.Count ? tiers[index + 1] : null;
        var track = new Rect2(card.Position.X + 18, card.End.Y - 30, card.Size.X - 36, 14);
        Caps(next == null ? "Highest rank" : $"{next.MinRating - state.ArenaRating} to {next.Title}", new Rect2(track.Position.X, track.Position.Y - 32, track.Size.X, 28), 16, Soft);
        Meter(track, next == null ? 1 : (state.ArenaRating - tier.MinRating) / (float)Math.Max(1, next.MinRating - tier.MinRating));

        // Highest rank first; yours is lit.
        var ladder = Section(new Rect2(column.Position.X, card.End.Y + 12, column.Size.X, column.End.Y - card.End.Y - 12), "Ranks", "icon-crossed-swords");
        var pitch = ladder.Size.Y / tiers.Count;
        for (var i = 0; i < tiers.Count; i++)
        {
            var rank = tiers[tiers.Count - 1 - i];
            var current = rank.Id == tier.Id;
            var row = new Rect2(ladder.Position.X, ladder.Position.Y + i * pitch, ladder.Size.X, pitch - 4);
            if (current) Frame("ach-card-ready", row, 10);
            Image(Insignia(rank), new Rect2(row.Position.X + 8, row.Position.Y + 2, row.Size.Y - 4, row.Size.Y - 4));
            Serif(current ? $"{rank.Title} · you" : rank.Title, new Rect2(row.Position.X + row.Size.Y + 10, row.Position.Y, 150, row.Size.Y), 19,
                new Color(rank.ColorHex).Lerp(RoyalText.Cream, .45f), weight: 700);
            Serif(rank.MaxRating < 99999 ? $"{rank.MinRating}–{rank.MaxRating}" : $"{rank.MinRating}+", new Rect2(row.Position.X, row.Position.Y, row.Size.X - 12, row.Size.Y), 18, Soft,
                HorizontalAlignment.Right, 500);
        }
    }

    private void Opponent(Rect2 rect, ArenaOpponentSnapshot opponent)
    {
        var tier = ArenaCatalog.GetTier(opponent.ArenaRating);
        Frame("ach-card", rect, 14);
        var units = opponent.DeckUnitIds.Select(Unit).Where(unit => unit != null).ToArray();
        // The leading fighter stands in the arena.
        var stage = new Rect2(rect.Position + new Vector2(8, 8), new Vector2(rect.Size.X - 16, 176));
        Picture(stage, RoyalArt.Item("activity-arena"));
        Shade(new Rect2(stage.Position.X + 3, stage.Position.Y + 60, stage.Size.X - 6, stage.Size.Y - 63));
        if (units.Length > 0)
        {
            var figure = new UnitFigure { Position = stage.Position + new Vector2(20, 14), Size = new Vector2(stage.Size.X - 40, stage.Size.Y - 18) };
            figure.SetUnit(units.OrderByDescending(unit => unit.Cost).First());
            Live.AddChild(figure);
        }
        Image(Insignia(tier), new Rect2(stage.End.X - 58, stage.Position.Y + 8, 50, 50));

        var x = rect.Position.X + 16; var width = rect.Size.X - 32;
        var y = stage.End.Y + 8;
        Serif(opponent.Callsign, new Rect2(x, y, width, 34), 24, weight: 700);
        Serif($"{tier.Title} · {opponent.ArenaRating}", new Rect2(x, y + 32, width, 28), 19, new Color(tier.ColorHex).Lerp(RoyalText.Cream, .45f), weight: 600);
        Image("stat-damage", new Rect2(x, y + 66, 24, 26));
        Serif($"{opponent.PowerRating:N0} power", new Rect2(x + 30, y + 62, width - 30, 32), 19, Soft, weight: 500);
        var cell = Mathf.Min(58, (width - 8 * (units.Length - 1)) / Math.Max(1, units.Length));
        var squad = new Control { Position = new Vector2(x, y + 100), Size = new Vector2(width, cell), MouseFilter = MouseFilterEnum.Pass,
            TooltipText = string.Join(", ", units.Select(unit => unit.DisplayName)) };
        Live.AddChild(squad);
        for (var i = 0; i < units.Length; i++)
        {
            var slot = new Rect2(x + i * (cell + 8), y + 100, cell, cell);
            Frame("relic-card", slot, 8);
            var figure = new UnitFigure { Position = slot.Position + new Vector2(3, 3), Size = slot.Size - new Vector2(6, 6) };
            figure.SetUnit(units[i]);
            Live.AddChild(figure);
        }
        var target = opponent;
        Action(new Rect2(rect.Position.X + 12, rect.End.Y - 58, rect.Size.X - 24, 46), "Challenge", "icon-swords-dark", () =>
        {
            GameState.Instance.PrepareArenaBattle(target);
            Notify($"Challenging {target.Callsign}…");
            SceneRouter.Instance.GoToLoadout();
        }, Kind.Gold, hint: $"Challenge {opponent.Callsign}", size: 18);
    }

    private static UnitDefinition Unit(string id)
    {
        try { return GameData.GetUnit(id); }
        catch { return null; }
    }
}
