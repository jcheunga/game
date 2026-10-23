using System;
using Godot;

/// <summary>
/// Rankings on a painted board, one page per contest (arena, tower, endless, daily): your standing on the
/// contest's painting on the left, and the realm's table on the right with your own row pinned under it.
/// </summary>
public partial class LeaderboardMenu : RoyalBoard
{
    private enum Tab { Arena, Tower, Endless, Daily }
    private static readonly (Tab Tab, string Name, string Emblem)[] Tabs =
    {
        (Tab.Arena, "Arena", "emblem-arena"), (Tab.Tower, "Tower", "emblem-tower"), (Tab.Endless, "Endless", "emblem-hourglass"), (Tab.Daily, "Daily", "emblem-gifts")
    };
    private Tab _tab = Tab.Arena;

    public LeaderboardMenu() : base("Rankings", "rankings") { }

    protected override void Compose()
    {
        var body = Board.Body; var footer = Board.Footer;
        var left = Board.Notch.End.X + 6;
        var width = (body.End.X - 6 - left - 3 * 12) / 4;
        for (var i = 0; i < Tabs.Length; i++)
        {
            var (tab, name, emblem) = Tabs[i];
            var rect = new Rect2(left + i * (width + 12), body.Position.Y + 8, width, 48);
            Tab(rect, name, tab == _tab, () => { _tab = tab; Refresh(); }, emblem);
        }
        var page = new Rect2(body.Position.X + 6, Board.Notch.End.Y + 2, body.Size.X - 12, footer.Position.Y - 12 - Board.Notch.End.Y);
        var standing = new Rect2(page.Position, new Vector2(430, page.Size.Y));
        Standing(standing);
        Table(new Rect2(standing.End.X + 16, page.Position.Y, page.End.X - standing.End.X - 16, page.Size.Y));
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        Action(new Rect2(footer.Position.X + 184, footer.Position.Y, 220, footer.Size.Y), "Player profile", "icon-profile", () => SceneRouter.Instance.GoToProfile(), size: 17);
    }

    private (string Name, string Value)[] Facts()
    {
        var state = GameState.Instance;
        switch (_tab)
        {
            case Tab.Tower:
                return new[] { ("Highest floor", state.TowerHighestFloor > 0 ? $"Floor {state.TowerHighestFloor}" : "None yet") };
            case Tab.Endless:
                var time = TimeSpan.FromSeconds(state.BestEndlessTimeSeconds);
                return new[] { ("Best wave", state.BestEndlessWave.ToString()), ("Best time", $"{(int)time.TotalMinutes}:{time.Seconds:D2}") };
            case Tab.Daily:
                return new[] { ("Daily streak", state.DailyStreak == 1 ? "1 day" : $"{state.DailyStreak} days") };
            default:
                var tier = state.GetArenaTier();
                return new[] { ("Rating", $"{state.ArenaRating} · {tier.Title}"), ("Record", $"{state.ArenaWins} wins · {state.ArenaLosses} losses") };
        }
    }

    private Texture2D Painting() => _tab switch
    {
        Tab.Tower => RoyalArt.Item("scene-tower"),
        Tab.Endless => RoyalArt.Item("activity-endless"),
        Tab.Daily => RoyalArt.Item("activity-gifts"),
        _ => RoyalArt.Item("activity-arena")
    };

    private void Standing(Rect2 column)
    {
        var picture = new Rect2(column.Position, new Vector2(column.Size.X, 196));
        Picture(picture, Painting());
        Plaque(picture.Position + new Vector2(14, 14), "Your standing", 22, 40);
        var facts = Facts();
        var room = new Rect2(column.Position.X, picture.End.Y + 12, column.Size.X, column.End.Y - picture.End.Y - 12);
        var height = (room.Size.Y - (facts.Length - 1) * 12) / facts.Length;
        for (var i = 0; i < facts.Length; i++)
        {
            var tile = new Rect2(room.Position.X, room.Position.Y + i * (height + 12), room.Size.X, height);
            Frame("ach-card", tile, 14);
            Caps(facts[i].Name, new Rect2(tile.Position.X + 22, tile.GetCenter().Y - 40, tile.Size.X - 44, 30), 17, Soft);
            Heading(facts[i].Value, new Rect2(tile.Position.X + 22, tile.GetCenter().Y - 8, tile.Size.X - 44, 44), 30);
        }
    }

    private void Table(Rect2 rect)
    {
        var state = GameState.Instance;
        var room = Section(rect, $"{Tabs[(int)_tab].Name} rankings", "emblem-rankings", "Realm-wide");
        Caps("Rank", new Rect2(room.Position.X + 12, room.Position.Y, 80, 28), 16, Soft);
        Caps("Caravan", new Rect2(room.Position.X + 100, room.Position.Y, 220, 28), 16, Soft);
        Caps("Score", new Rect2(room.Position.X, room.Position.Y, room.Size.X - 12, 28), 16, Soft, HorizontalAlignment.Right);
        // The realm's server keeps the table; offline, the hall stands empty and only your own row shows.
        var hall = new Rect2(room.Position.X, room.Position.Y + 36, room.Size.X, room.Size.Y - 36 - 70);
        Image(RoyalArt.Item("activity-rankings"), new Rect2(hall.GetCenter().X - 110, hall.Position.Y + 6, 220, 110), new Color(1, 1, 1, .55f));
        Heading("Rankings are offline", new Rect2(hall.Position.X, hall.Position.Y + 126, hall.Size.X, 40), 26, HorizontalAlignment.Center);
        Paragraph("Connect to the realm's server to compare caravans.", new Rect2(hall.Position.X + 40, hall.Position.Y + 170, hall.Size.X - 80, 30), 1, 19, Soft, HorizontalAlignment.Center);
        var you = new Rect2(room.Position.X, room.End.Y - 58, room.Size.X, 54);
        Frame("mp-tab-selected", you, 10);
        Caps("—", new Rect2(you.Position.X + 12, you.Position.Y, 80, you.Size.Y), 18);
        Serif(state.PlayerCallsign, new Rect2(you.Position.X + 100, you.Position.Y, you.Size.X - 280, you.Size.Y), 21, weight: 700);
        Serif(Facts()[0].Value, new Rect2(you.Position.X, you.Position.Y, you.Size.X - 14, you.Size.Y), 21, new Color("f6e2ad"), HorizontalAlignment.Right, 700);
    }
}
