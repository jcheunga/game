using System;
using System.Linq;
using Godot;

/// <summary>
/// The challenge tower on a painted board: the chosen floor's battlefield, enemy strength and rewards on the
/// left; the climb on the right, ten floors at a time over the painted tower; Climb prepares the chosen floor.
/// </summary>
public partial class TowerMenu : RoyalBoard
{
    private const int FloorsPerPage = 10;
    private int _selectedFloor = 1;
    private int _page;

    public TowerMenu() : base("Challenge Tower", "tower") { }

    protected override void Build()
    {
        _selectedFloor = Math.Clamp(GameState.Instance.TowerHighestFloor + 1, 1, ChallengeTowerCatalog.MaxFloor);
        _page = (_selectedFloor - 1) / FloorsPerPage;
        base.Build();
    }

    protected override void Compose()
    {
        var state = GameState.Instance;
        var highest = state.TowerHighestFloor;
        var stars = Enumerable.Range(1, ChallengeTowerCatalog.MaxFloor).Sum(state.GetTowerFloorStars);
        Chip("result-star", stars.ToString(), $"{stars} of {ChallengeTowerCatalog.MaxFloor * 3} tower stars");
        Chip("hubicon-tower", highest > 0 ? $"Best floor {highest}" : "No floors yet", "Highest floor cleared");
        var body = Board.Body; var footer = Board.Footer;
        // The left column starts below the banner hanging over the board's corner; its heading sits beside it.
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 536, footer.Position.Y - 10 - Board.Notch.End.Y);
        var right = new Rect2(left.End.X + 20, body.Position.Y + 10, body.End.X - left.End.X - 26, footer.Position.Y - 12 - body.Position.Y);
        FloorDetail(left, new Rect2(Board.Notch.End.X + 6, body.Position.Y + 4, left.End.X - Board.Notch.End.X - 6, left.Position.Y - body.Position.Y - 6));
        Climb(right);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y, left.End.X - footer.Position.X - 184, footer.Size.Y));
        var lockedFloor = IsLocked(_selectedFloor);
        Action(new Rect2(right.Position.X, footer.Position.Y - 2, footer.End.X - right.Position.X, footer.Size.Y + 4), $"Climb floor {_selectedFloor}", "icon-swords-dark", Deploy, Kind.Gold,
            !lockedFloor, lockedFloor ? "Clear the floor below to unlock" : $"Prepare for floor {_selectedFloor}", size: 21);
    }

    private bool IsLocked(int floor) => floor > GameState.Instance.TowerHighestFloor + 1;

    private void FloorDetail(Rect2 column, Rect2 heading)
    {
        var floor = ChallengeTowerCatalog.GetFloor(_selectedFloor);
        var stage = GameData.GetStage(floor.BaseStageNumber);
        var locked = IsLocked(floor.Floor);
        var x = column.Position.X; var y = column.Position.Y; var width = column.Size.X;

        // The floor's battlefield.
        var route = RouteCatalog.Normalize(stage?.MapId ?? "city");
        var path = $"res://assets/ui/royal/missions/{route}.png";
        Heading($"Floor {floor.Floor}", heading, 32);
        var art = new Rect2(x, y, width, 162);
        Picture(art, ResourceLoader.Exists(path) ? RoyalArt.Load(path) : null, locked);
        Shade(new Rect2(art.Position.X + 3, art.End.Y - 56, art.Size.X - 6, 53));
        Serif(stage != null ? $"{stage.MapName} · {stage.StageName}" : $"Stage {floor.BaseStageNumber}",
            new Rect2(art.Position.X + 18, art.End.Y - 40, art.Size.X - 36, 32), 20);
        if (locked) Image("icon-lock-small", new Rect2(art.End.X - 50, art.Position.Y + 16, 30, 36));
        else if (GameState.Instance.GetTowerFloorStars(floor.Floor) is var won and > 0) Stars(new Vector2(art.End.X - 106, art.Position.Y + 18), won, 28);

        // Enemy strength.
        y = art.End.Y + 12;
        var modifiers = floor.ForcedModifierIds ?? Array.Empty<string>();
        var facts = new[]
        {
            ("stat-health", "Enemy health", $"×{floor.EnemyHealthScale:0.0}", "Enemy health compared with the battlefield's campaign stage"),
            ("stat-damage", "Enemy damage", $"×{floor.EnemyDamageScale:0.0}", "Enemy damage compared with the battlefield's campaign stage"),
            ("icon-skull-red", "Modifiers", modifiers.Length == 0 ? "None" : modifiers.Length == 1 ? Pretty(modifiers[0]) : $"{modifiers.Length} active",
                modifiers.Length == 0 ? "No battle modifiers" : string.Join(" · ", modifiers.Select(Pretty)))
        };
        var tileWidth = (width - 24) / 3;
        for (var i = 0; i < facts.Length; i++)
        {
            var (icon, caption, value, hint) = facts[i];
            var tile = new Rect2(x + i * (tileWidth + 12), y, tileWidth, 78);
            Frame("tile", tile, 12).With(p => { p.TooltipText = hint; p.MouseFilter = MouseFilterEnum.Pass; });
            Caps(caption, new Rect2(tile.Position.X + 14, tile.Position.Y + 8, tile.Size.X - 28, 26), 16, Soft);
            Image(icon, new Rect2(tile.Position + new Vector2(12, 38), new Vector2(30, 30)));
            Serif(value, new Rect2(tile.Position.X + 48, tile.Position.Y + 36, tile.Size.X - 56, 34), 22, new Color("f6e2ad"), weight: 700);
        }

        // Rewards.
        y += 90;
        var room = Section(new Rect2(x, y, width, column.End.Y - y), "Floor rewards", "icon-scroll-gold",
            floor.Floor % 10 == 0 ? "Tenth-floor bonus" : null);
        var rewards = new System.Collections.Generic.List<(Texture2D, string, string)>();
        if (floor.RewardGold > 0) rewards.Add((RewardArt("gold"), $"{floor.RewardGold:N0}", $"{floor.RewardGold:N0} gold"));
        if (floor.RewardFood > 0) rewards.Add((RewardArt("food"), floor.RewardFood.ToString(), $"{floor.RewardFood} rations"));
        if (floor.RewardEssence > 0) rewards.Add((RewardArt("essence"), floor.RewardEssence.ToString(), $"{floor.RewardEssence} essence"));
        if (!string.IsNullOrEmpty(floor.MilestoneRelicId))
        {
            var relic = GameData.GetEquipment(floor.MilestoneRelicId);
            rewards.Add((RewardArt("relic", floor.MilestoneRelicId), relic?.DisplayName ?? Pretty(floor.MilestoneRelicId), "Milestone relic: " + (relic?.DisplayName ?? floor.MilestoneRelicId)));
        }
        var cell = Mathf.Min(170, room.Size.X / Math.Max(1, rewards.Count));
        var start = room.Position.X + (room.Size.X - cell * rewards.Count) / 2;
        for (var i = 0; i < rewards.Count; i++)
        {
            var at = new Rect2(start + i * cell, room.Position.Y - 6, cell, room.Size.Y + 10);
            var holder = new Control { Position = at.Position, Size = at.Size, TooltipText = rewards[i].Item3, MouseFilter = MouseFilterEnum.Pass };
            Live.AddChild(holder);
            Image(rewards[i].Item1, new Rect2(at.Position, new Vector2(at.Size.Y, at.Size.Y)), locked ? new Color(1, 1, 1, .45f) : null);
            Serif(rewards[i].Item2, new Rect2(at.Position.X + at.Size.Y + 6, at.Position.Y, at.Size.X - at.Size.Y - 10, at.Size.Y), 24, locked ? Soft : new Color("f6e2ad"), weight: 700);
        }
    }

    private void Climb(Rect2 column)
    {
        var state = GameState.Instance;
        var pages = Mathf.CeilToInt(ChallengeTowerCatalog.MaxFloor / (float)FloorsPerPage);
        var picture = new Rect2(column.Position, column.Size);
        var tower = ResourceLoader.Exists("res://assets/ui/royal/items/scene-tower.png") ? RoyalArt.Item("scene-tower") : RoyalArt.Item("activity-tower");
        Picture(picture, tower);

        // The block of ten floors, top floor first, over the misty side of the painting.
        var first = _page * FloorsPerPage + 1;
        var last = Math.Min(ChallengeTowerCatalog.MaxFloor, first + FloorsPerPage - 1);
        var band = new Rect2(picture.Position.X + 14, picture.Position.Y + 12, 252, 44);
        Frame("hub-card-bar", band, 12);
        Caps($"Floors {first}–{last}", new Rect2(band.Position.X + 30, band.Position.Y, band.Size.X - 60, band.Size.Y), 17, align: HorizontalAlignment.Center);
        Pager(band.Grow(-4), _page, pages, page => { _page = page; Refresh(); }, "floors", dots: false);
        var rows = last - first + 1;
        var top = band.End.Y + 8;
        var pitch = (picture.End.Y - 16 - top) / FloorsPerPage;
        for (var i = 0; i < rows; i++)
        {
            var number = last - i;
            var rect = new Rect2(band.Position.X, top + i * pitch, band.Size.X, pitch - 5);
            var locked = IsLocked(number);
            var cleared = number <= state.TowerHighestFloor;
            var selected = number == _selectedFloor;
            var floor = number;
            var row = Card(rect, $"Floor {number}", () => { _selectedFloor = floor; Refresh(); }, selected, "season-locked", "mp-tab-selected", !locked);
            row.TooltipText = locked ? "Clear the floor below to unlock" : cleared ? $"Floor {number} · {state.GetTowerFloorStars(number)} of 3 stars" : $"Floor {number} · next to climb";
            Caps($"Floor {number}", new Rect2(rect.Position.X + 16, rect.Position.Y, 130, rect.Size.Y), 16, locked ? new Color("a49a8a") : selected ? new Color("fff2cf") : Engraved);
            var mark = new Rect2(rect.End.X - 74, rect.Position.Y + (rect.Size.Y - 18) / 2, 62, 18);
            if (locked) Image("icon-lock-small", new Rect2(rect.End.X - 34, rect.Position.Y + (rect.Size.Y - 22) / 2, 20, 22), new Color(1, 1, 1, .7f));
            else if (cleared) Stars(mark.Position, state.GetTowerFloorStars(number), 18);
            else Image("icon-crossed-swords", new Rect2(rect.End.X - 38, rect.Position.Y + (rect.Size.Y - 24) / 2, 24, 24));
            // Milestone relics and tenth-floor gold are marked on the ladder.
            var prize = ChallengeTowerCatalog.GetFloor(number);
            if (!string.IsNullOrEmpty(prize.MilestoneRelicId)) Image(RewardArt("relic", prize.MilestoneRelicId), new Rect2(rect.Position.X + 132, rect.Position.Y + 2, rect.Size.Y - 4, rect.Size.Y - 4), locked ? new Color(1, 1, 1, .5f) : null);
            else if (number % 10 == 0) Image("coin-small", new Rect2(rect.Position.X + 134, rect.Position.Y + 4, rect.Size.Y - 8, rect.Size.Y - 8), locked ? new Color(1, 1, 1, .5f) : null);
        }
    }

    private void Stars(Vector2 at, int earned, float size)
    {
        for (var i = 0; i < 3; i++)
            Image("result-star", new Rect2(at + new Vector2(i * (size + 2), 0), new Vector2(size, size)), i < earned ? null : new Color(.25f, .22f, .2f, .85f));
    }

    private void Deploy()
    {
        if (IsLocked(_selectedFloor)) { Notify("Clear the floor below first."); return; }
        GameState.Instance.PrepareTowerBattle(_selectedFloor);
        SceneRouter.Instance.GoToLoadout();
    }
}
