using System.Linq;
using Godot;

/// <summary>
/// Friends on a painted board: your caravan's profile ID and today's gifts on the left; on the right, add a
/// friend by their ID and the list of friends, six to a page, each with Send gift and Remove.
/// </summary>
public partial class FriendsMenu : RoyalBoard
{
    private const int PerPage = 6;
    private const int GiftsPerDay = 3;
    private int _page;
    private LineEdit _input;
    private string _draft = "";

    public FriendsMenu() : base("Friends", "friends") { }

    protected override void Compose()
    {
        var state = GameState.Instance;
        var friends = state.GetFriendIds().OrderBy(id => id).ToArray();
        Chip("emblem-friends", friends.Length == 1 ? "1 friend" : $"{friends.Length} friends", "Friends in your list");
        var body = Board.Body; var footer = Board.Footer;
        Caps("Your caravan", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 300, 40), 18);
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 380, footer.Position.Y - 10 - Board.Notch.End.Y);
        Caravan(left, state);
        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        AddRow(new Rect2(right.Position, new Vector2(right.Size.X, 64)));
        List(new Rect2(right.Position.X, right.Position.Y + 76, right.Size.X, right.Size.Y - 76), friends, state);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        Action(new Rect2(footer.Position.X + 184, footer.Position.Y, 220, footer.Size.Y), "Player profile", "icon-profile", () => SceneRouter.Instance.GoToProfile(), size: 17);
        NoticeAt(new Rect2(footer.Position.X + 420, footer.Position.Y, footer.Size.X - 420, footer.Size.Y));
    }

    private void Caravan(Rect2 column, GameState state)
    {
        var picture = new Rect2(column.Position, new Vector2(column.Size.X, 168));
        Picture(picture, RoyalArt.Item("activity-friends"));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 56, picture.Size.X - 6, 53));
        Serif(state.PlayerCallsign, new Rect2(picture.Position.X + 18, picture.End.Y - 42, picture.Size.X - 36, 34), 23, weight: 700);
        var id = Section(new Rect2(column.Position.X, picture.End.Y + 12, column.Size.X, 104), "Profile ID", "icon-profile");
        Serif(string.IsNullOrEmpty(state.PlayerProfileId) ? "Not signed in" : state.PlayerProfileId, new Rect2(id.Position.X + 4, id.Position.Y - 2, id.Size.X - 8, 34), 21,
            new Color("f6e2ad"), weight: 700);
        var top = picture.End.Y + 128;
        var gifts = Section(new Rect2(column.Position.X, top, column.Size.X, column.End.Y - top), "Daily gifts", "emblem-gifts", $"{state.GiftsSentToday}/{GiftsPerDay} sent");
        // Each gift sends 50 gold and 2 rations.
        var y = gifts.Position.Y + (gifts.Size.Y - 44) / 2;
        Caps("Each sends", new Rect2(gifts.Position.X + 4, y, 120, 44), 16, Soft);
        Image(RewardArt("gold"), new Rect2(gifts.Position.X + 128, y, 46, 44));
        Serif("50", new Rect2(gifts.Position.X + 178, y, 60, 44), 23, new Color("f6e2ad"), weight: 700);
        Image(RewardArt("food"), new Rect2(gifts.Position.X + 236, y, 46, 44));
        Serif("2", new Rect2(gifts.Position.X + 286, y, 40, 44), 23, new Color("f6e2ad"), weight: 700);
    }

    private void AddRow(Rect2 row)
    {
        Frame("ach-card", row, 14);
        var field = new Rect2(row.Position.X + 14, row.Position.Y + 10, row.Size.X - 200, row.Size.Y - 20);
        _input = new LineEdit { Position = field.Position, Size = field.Size, PlaceholderText = "Friend's profile ID", Text = _draft, AccessibilityName = "Profile ID" };
        var box = RoyalKit.Slice("season-locked", 10);
        box.ContentMarginLeft = 16; box.ContentMarginRight = 12;
        foreach (var state in new[] { "normal", "focus", "read_only" }) _input.AddThemeStyleboxOverride(state, box);
        _input.AddThemeFontOverride("font", RoyalFonts.Body(600));
        _input.AddThemeFontSizeOverride("font_size", 21);
        _input.AddThemeColorOverride("font_color", RoyalText.Cream);
        _input.AddThemeColorOverride("font_placeholder_color", new Color("8f8676"));
        _input.AddThemeColorOverride("caret_color", RealmUi.Gold);
        _input.TextChanged += text => _draft = text;
        _input.TextSubmitted += _ => AddFriend();
        Live.AddChild(_input);
        Action(new Rect2(row.End.X - 176, row.Position.Y + 8, 164, row.Size.Y - 16), "Add friend", "icon-people-gold", AddFriend, size: 16);
    }

    private void List(Rect2 rect, string[] friends, GameState state)
    {
        var pages = System.Math.Max(1, (friends.Length + PerPage - 1) / PerPage);
        _page = System.Math.Clamp(_page, 0, pages - 1);
        var room = Section(rect, "Friends", "emblem-friends", friends.Length == 0 ? null : $"Page {_page + 1} of {pages}");
        if (friends.Length == 0)
        {
            Image("emblem-friends", new Rect2(room.GetCenter().X - 60, room.Position.Y + 10, 120, 108), new Color(1, 1, 1, .75f));
            Heading("No friends yet", new Rect2(room.Position.X, room.Position.Y + 124, room.Size.X, 40), 26, HorizontalAlignment.Center);
            Paragraph("Add a friend by their profile ID to trade daily gifts.", new Rect2(room.Position.X + 30, room.Position.Y + 170, room.Size.X - 60, 30), 1, 19, Soft, HorizontalAlignment.Center);
            return;
        }
        var list = new Rect2(room.Position, new Vector2(room.Size.X, room.Size.Y - (pages > 1 ? 40 : 0)));
        var pitch = list.Size.Y / PerPage;
        var canGift = state.GiftsSentToday < GiftsPerDay;
        for (var i = 0; i < PerPage; i++)
        {
            var at = _page * PerPage + i;
            if (at >= friends.Length) break;
            var friend = friends[at];
            var row = new Rect2(list.Position.X, list.Position.Y + i * pitch, list.Size.X, pitch - 6);
            Frame("season-locked", row, 10);
            Image("emblem-friends", new Rect2(row.Position.X + 10, row.Position.Y + 4, row.Size.Y - 8, row.Size.Y - 8));
            Serif(friend.Length > 22 ? friend[..22] + "…" : friend, new Rect2(row.Position.X + row.Size.Y + 12, row.Position.Y, row.Size.X - 360, row.Size.Y), 20, weight: 600);
            var id = friend;
            Action(new Rect2(row.End.X - 252, row.Position.Y + 5, 176, row.Size.Y - 10), "Send gift", null, () =>
            {
                GameState.Instance.TrySendGift(id, out var message);
                Notify(message);
                Refresh();
            }, Kind.Claim, canGift, canGift ? $"Send {id} 50 gold and 2 rations" : "Daily gift limit reached", size: 15, name: $"Send gift to {id}");
            var remove = Action(new Rect2(row.End.X - 66, row.Position.Y + 5, 56, row.Size.Y - 10), "", "icon-close-x", () =>
            {
                GameState.Instance.RemoveFriend(id);
                Notify($"Removed {id} from friends.");
                Refresh();
            }, Kind.Slate, hint: $"Remove {id}", name: $"Remove {id}");
            remove.Glyph.Size = new Vector2(18, 18);
            remove.Glyph.Position = (remove.Size - remove.Glyph.Size) / 2;
        }
        Pager(new Rect2(room.Position.X, room.End.Y - 36, room.Size.X, 36), _page, pages, page => { _page = page; Refresh(); }, "friends");
    }

    private void AddFriend()
    {
        var profileId = (_input?.Text ?? _draft).Trim();
        if (profileId.Length == 0) { Notify("Enter a profile ID to add."); return; }
        GameState.Instance.AddFriend(profileId);
        _draft = "";
        Notify($"Added {profileId} as a friend.");
        Refresh();
    }
}
