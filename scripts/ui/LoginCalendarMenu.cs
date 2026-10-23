using System;
using Godot;

/// <summary>
/// Daily gifts on a painted board: today's gift with Claim on the left, and the month's calendar on the right,
/// ten painted day cards to a page (collected days ticked, today lit).
/// </summary>
public partial class LoginCalendarMenu : RoyalBoard
{
    private const int DaysPerPage = 10;
    private int _page = -1;

    public LoginCalendarMenu() : base("Daily Gifts", "gifts", Look.Camp) { }

    protected override void Compose()
    {
        var state = GameState.Instance;
        var claimed = state.LoginCalendarDay;
        var canClaim = state.CanClaimLoginReward();
        var today = Math.Min(LoginCalendarCatalog.TotalDays, claimed + 1);
        if (_page < 0) _page = (today - 1) / DaysPerPage;
        Chip("result-star", $"{claimed}/{LoginCalendarCatalog.TotalDays}", $"{claimed} of {LoginCalendarCatalog.TotalDays} gifts collected this month");
        var body = Board.Body; var footer = Board.Footer;
        // Today's gift stands on the right; the calendar fills the left above the camp props in its corner.
        var hero = new Rect2(body.End.X - 384, body.Position.Y + 10, 372, body.Size.Y - 18);
        var left = body.Position.X + 12;
        Today(hero, today, claimed, canClaim);
        Calendar(new Rect2(left, hero.Position.Y, hero.Position.X - 20 - left, Board.Props.Position.Y - 8 - hero.Position.Y), claimed, canClaim);
        Back(new Rect2(hero.Position.X - 190, footer.Position.Y, 170, footer.Size.Y));
        NoticeAt(new Rect2(footer.Position.X, footer.Position.Y, hero.Position.X - 206 - footer.Position.X, footer.Size.Y));
    }

    private void Today(Rect2 column, int day, int claimed, bool canClaim)
    {
        var reward = LoginCalendarCatalog.GetDay(day);
        var picture = new Rect2(column.Position, new Vector2(column.Size.X, 176));
        Picture(picture, RoyalArt.Item("activity-gifts"));
        Plaque(picture.Position + new Vector2(14, 14), DateTime.UtcNow.ToString("MMMM yyyy"), 22, 40);
        var done = claimed >= LoginCalendarCatalog.TotalDays;
        var room = Section(new Rect2(column.Position.X, picture.End.Y + 12, column.Size.X, column.End.Y - picture.End.Y - 84),
            canClaim ? "Today's gift" : done ? "Calendar complete" : "Tomorrow's gift", "emblem-gifts");
        if (reward != null)
        {
            Image(RewardArt(reward.RewardType, reward.RewardItemId), new Rect2(room.Position.X + 4, room.Position.Y + 6, 128, 112));
            Heading($"Day {reward.Day}", new Rect2(room.Position.X + 146, room.Position.Y + 10, room.Size.X - 150, 44), 32);
            Serif(reward.Label, new Rect2(room.Position.X + 146, room.Position.Y + 58, room.Size.X - 150, 34), 24, new Color("f6e2ad"), weight: 700);
        }
        var track = new Rect2(room.Position.X + 4, room.End.Y - 30, room.Size.X - 8, 14);
        Caps($"{claimed} of {LoginCalendarCatalog.TotalDays} collected", new Rect2(track.Position.X, track.Position.Y - 34, track.Size.X, 28), 16, Soft);
        Meter(track, claimed / (float)LoginCalendarCatalog.TotalDays);
        var claim = new Rect2(column.Position.X, column.End.Y - 58, column.Size.X, 58);
        if (canClaim)
            Action(claim, "Claim", null, () =>
            {
                var got = GameState.Instance.TryClaimLoginReward(out var message);
                AudioDirector.Claimed(got);
                _page = -1;
                Notify(message);
                Refresh();
            }, Kind.Gold, size: 22);
        else Action(claim, done ? "All gifts collected" : "Come back tomorrow", null, null, Kind.Claimed, false,
            done ? "Every gift this month is collected" : "Today's gift is collected", size: 18);
    }

    private void Calendar(Rect2 column, int claimed, bool canClaim)
    {
        var pages = (LoginCalendarCatalog.TotalDays + DaysPerPage - 1) / DaysPerPage;
        var tabWidth = (column.Size.X - (pages - 1) * 10) / pages;
        for (var i = 0; i < pages; i++)
        {
            var index = i;
            var first = i * DaysPerPage + 1;
            var caption = $"Days {first}–{Math.Min(LoginCalendarCatalog.TotalDays, first + DaysPerPage - 1)}";
            var rect = new Rect2(column.Position.X + i * (tabWidth + 10), column.Position.Y, tabWidth, 46);
            Tab(rect, caption, i == _page, () => { _page = index; Refresh(); });
        }

        var grid = new Rect2(column.Position.X, column.Position.Y + 58, column.Size.X, column.Size.Y - 58);
        var width = (grid.Size.X - 4 * 12) / 5;
        var height = (grid.Size.Y - 12) / 2;
        for (var i = 0; i < DaysPerPage; i++)
        {
            var reward = LoginCalendarCatalog.GetDay(_page * DaysPerPage + i + 1);
            if (reward == null) continue;
            var rect = new Rect2(grid.Position.X + i % 5 * (width + 12), grid.Position.Y + i / 5 * (height + 12), width, height);
            Day(rect, reward, claimed, canClaim);
        }
    }

    private void Day(Rect2 rect, LoginCalendarReward reward, int claimed, bool canClaim)
    {
        var collected = reward.Day <= claimed;
        var current = reward.Day == claimed + 1;
        var lit = current && canClaim;
        var card = Frame(lit ? "ach-card-ready" : "result-tile", rect, 12);
        card.TooltipText = collected ? $"Day {reward.Day} · {reward.Label} · collected" : current ? $"Day {reward.Day} · {reward.Label} · {(canClaim ? "today" : "tomorrow")}" : $"Day {reward.Day} · {reward.Label}";
        card.MouseFilter = MouseFilterEnum.Pass;
        Caps($"Day {reward.Day}", new Rect2(rect.Position.X, rect.Position.Y + 8, rect.Size.X, 28), 17, lit ? new Color("fff2cf") : Engraved, HorizontalAlignment.Center);
        var art = new Rect2(rect.Position.X + 14, rect.Position.Y + 40, rect.Size.X - 28, rect.Size.Y - 92);
        Image(RewardArt(reward.RewardType, reward.RewardItemId), art, collected ? new Color(1, 1, 1, .45f) : null);
        Serif(reward.Label.Replace(" + Bonus!", " +"), new Rect2(rect.Position.X + 6, rect.End.Y - 46, rect.Size.X - 12, 32), 19,
            collected ? new Color("a99f8d") : new Color("f6e2ad"), HorizontalAlignment.Center, 700);
        if (collected) Image("season-corner-check", new Rect2(rect.End.X - 40, rect.Position.Y + 4, 36, 36));
    }
}
