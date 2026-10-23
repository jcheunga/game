using System.Linq;
using Godot;

/// <summary>
/// Boss rush on a painted board: the fourteen commanders in order as a gallery of portraits (cleared ones
/// ticked) on the left, and the gauntlet's record and rules on the right, with Start boss rush.
/// </summary>
public partial class BossRushMenu : RoyalBoard
{
    public BossRushMenu() : base("Boss Rush", "bossrush") { }

    protected override void Compose()
    {
        var state = GameState.Instance;
        var waves = BossRushCatalog.Waves;
        Chip("result-star", $"Best {state.BestBossRushWave}/{waves.Length}", "Most commanders defeated in one rush");
        var body = Board.Body; var footer = Board.Footer;
        Caps("Commanders", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 300, 40), 18);
        var gallery = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 530, footer.Position.Y - 10 - Board.Notch.End.Y);
        var columns = 5;
        var rows = (waves.Length + columns - 1) / columns;
        var cell = new Vector2((gallery.Size.X - (columns - 1) * 10) / columns, (gallery.Size.Y - (rows - 1) * 10) / rows);
        for (var i = 0; i < waves.Length; i++)
        {
            var wave = waves[i];
            var level = GameData.GetStage(wave.SourceStage)?.EnemyLevel ?? 1;
            var cleared = i < state.BestBossRushWave;
            var rect = new Rect2(gallery.Position + new Vector2(i % columns * (cell.X + 10), i / columns * (cell.Y + 10)), cell);
            var tile = Frame(cleared ? "ach-card-ready" : "relic-card", rect, 10);
            tile.TooltipText = $"{i + 1}. {wave.BossName} · Lv {level} · {wave.RewardGold} gold";
            tile.MouseFilter = MouseFilterEnum.Pass;
            UnitDefinition boss;
            try { boss = GameData.GetUnit(wave.BossUnitId); } catch { boss = null; }
            var figure = new UnitFigure { Position = rect.Position + new Vector2(6, 6), Size = rect.Size - new Vector2(12, 34), Fill = .98f };
            figure.SetUnit(boss);
            Live.AddChild(figure);
            Image("endless-tab", new Rect2(rect.Position + new Vector2(4, 4), new Vector2(30, 30)));
            Caps((i + 1).ToString(), new Rect2(rect.Position + new Vector2(4, 4), new Vector2(30, 30)), 16, Ink, HorizontalAlignment.Center);
            Caps($"Lv {level}", new Rect2(rect.Position.X, rect.End.Y - 30, rect.Size.X, 26), 16, cleared ? new Color("fff2cf") : Soft, HorizontalAlignment.Center);
            if (cleared) Image("forge-check", new Rect2(rect.End.X - 32, rect.Position.Y + 4, 28, 29));
        }

        var right = new Rect2(gallery.End.X + 16, body.Position.Y + 10, body.End.X - gallery.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        var picture = new Rect2(right.Position, new Vector2(right.Size.X, 172));
        Picture(picture, RoyalArt.Item("activity-bossrush"));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 56, picture.Size.X - 6, 53));
        Serif($"{waves.Length} commanders and their escorts", new Rect2(picture.Position.X + 18, picture.End.Y - 42, picture.Size.X - 36, 32), 21, weight: 700);
        var facts = new (string Icon, string Name, string Value)[]
        {
            ("result-star", "Best rush", $"{state.BestBossRushWave} of {waves.Length}"),
            ("icon-scroll-gold", "Runs", state.BossRushRuns.ToString()),
            ("coin-small", "Rewards", $"{waves.Sum(w => w.RewardGold):N0} gold")
        };
        var width = (right.Size.X - 24) / 3;
        for (var i = 0; i < facts.Length; i++)
        {
            var tile = new Rect2(right.Position.X + i * (width + 12), picture.End.Y + 12, width, 78);
            Frame("tile", tile, 12);
            Image(facts[i].Icon, new Rect2(tile.Position + new Vector2(10, 22), new Vector2(32, 32)));
            Caps(facts[i].Name, new Rect2(tile.Position.X + 48, tile.Position.Y + 9, tile.Size.X - 54, 28), 16, Soft);
            Serif(facts[i].Value, new Rect2(tile.Position.X + 48, tile.Position.Y + 36, tile.Size.X - 54, 32), 21, new Color("f6e2ad"), weight: 700);
        }
        var rules = Section(new Rect2(right.Position.X, picture.End.Y + 102, right.Size.X, right.End.Y - picture.End.Y - 102), "Between rounds", "emblem-horn");
        Paragraph("Troops regroup, courage resets to 40, starting mana is restored and the wagon repairs fully. Enemy levels rise from 2 to 20. No food cost; rewards are banked at the end.",
            new Rect2(rules.Position.X + 4, rules.Position.Y - 4, rules.Size.X - 8, rules.Size.Y + 6), 4, 18, new Color("e6dcc8"));

        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        Action(new Rect2(footer.Position.X + 184, footer.Position.Y, 200, footer.Size.Y), "Edit warband", "icon-people-gold", () => SceneRouter.Instance.GoToShop(0), size: 16);
        Action(new Rect2(footer.Position.X + 396, footer.Position.Y, 190, footer.Size.Y), "Edit utilities", "icon-flame-gold", () => SceneRouter.Instance.GoToShop(1), size: 16);
        var locked = state.HighestUnlockedStage <= 10;
        var ready = !locked && state.GetActiveDeckUnits().Count > 0;
        Action(new Rect2(footer.Position.X + 600, footer.Position.Y - 2, footer.Size.X - 600, footer.Size.Y + 4), "Start boss rush", "icon-swords-dark", () =>
        {
            if (GameState.Instance.PrepareBossRush(out var message)) SceneRouter.Instance.GoToBattle();
            else Notify(message);
        }, Kind.Gold, ready, locked ? "Defeat the King's Road boss to unlock boss rush" : ready ? "Start the rush with your warband" : "Choose a warband first", size: 20);
        NoticeAt(new Rect2(right.Position.X, right.End.Y - 36, right.Size.X, 30));
    }
}
