using System;
using System.Linq;
using Godot;

/// <summary>
/// The Rooms page while internet rooms are on: the room the player has joined (a painted bar with Ready, Launch,
/// Leave and the room tools), the open rooms as the concept's room rows, and the connection details. Rooms can
/// outnumber the board, so this one page scrolls.
/// </summary>
public partial class MultiplayerMenu
{
    private ScrollContainer _roomsScroll;
    private VBoxContainer _roomsList;

    private void BuildRoomsList()
    {
        _roomsList = RealmUi.Scroll(this);
        _roomsScroll = _roomsList.GetParent<ScrollContainer>();
        _roomsScroll.Position = new Vector2(48, 289); _roomsScroll.Size = new Vector2(1188, 316);
        _roomsList.AddThemeConstantOverride("separation", 8);
        _roomsScroll.Visible = false;
    }

    private void FillRoomsList()
    {
        RealmUi.Clear(_roomsList);
        if (OnlineRoomJoinService.GetCachedTicket() != null) _roomsList.AddChild(BuildJoinedRoomBar());
        var rooms = OnlineRoomDirectoryService.GetCachedRooms();
        if (rooms.Count == 0)
            _roomsList.AddChild(RealmUi.EmptyState("people", "No open rooms", "Refresh to find rooms, or host one for your friends."));
        var index = 0;
        foreach (var room in rooms) _roomsList.AddChild(BuildRoyalRoomRow(room, index++));
        // Service diagnostics stay behind Connection details; the page leads with rooms.
        var details = new Control { CustomMinimumSize = new Vector2(1182, 52), MouseFilter = MouseFilterEnum.Ignore };
        new RoyalPage(details).Action(new Rect2(1182 - 280, 2, 280, 48), "Connection details", "icon-tools", () => RealmUi.Details(this, "Connection details",
            OnlineRoomDirectoryService.BuildSnapshotSummary() + "\n\n" + OnlineRoomCreateService.BuildStatusSummary() + "\n" + OnlineRoomJoinService.BuildStatusSummary() + "\n" +
            OnlineRoomSessionService.BuildStatusSummary() + "\n" + OnlineRoomTelemetryService.BuildStatusSummary() + "\n\n" + BuildOnlineRoomAutoRefreshSummary()), size: 16);
        _roomsList.AddChild(details);
    }

    /// <summary>The joined room as one painted row: its name and state, then Ready, Launch, Leave and the room tools.</summary>
    private Control BuildJoinedRoomBar()
    {
        var ticket = OnlineRoomJoinService.GetCachedTicket();
        var snapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
        var bar = new Control { Name = "JoinedRoom", CustomMinimumSize = new Vector2(1182, 103), MouseFilter = MouseFilterEnum.Pass };
        var page = new RoyalPage(bar);
        page.Frame("ach-card-ready", new Rect2(0, 0, 1182, 103), 14);
        page.Image("mp-players", new Rect2(24, 20, 34, 26));
        page.Serif(string.IsNullOrWhiteSpace(ticket.RoomTitle) ? "Online room" : ticket.RoomTitle, new Rect2(68, 12, 440, 40), 26, weight: 700);
        page.Serif(JoinedRoomState(ticket, snapshot), new Rect2(24, 54, 500, 34), 21, RoyalBoard.Soft);
        var ready = page.Action(new Rect2(536, 26, 196, 52), OnlineRoomActionService.BuildToggleReadyLabel(), "icon-check", ToggleOnlineRoomReady, RoyalBoard.Kind.Navy,
            OnlineRoomActionService.CanToggleReady(), size: 16, name: "Ready up");
        ready.TooltipText = "Tell the room you are ready";
        page.Action(new Rect2(744, 24, 210, 56), "Launch round", "icon-swords-dark", LaunchOnlineRoomRound, RoyalBoard.Kind.Gold,
            OnlineRoomActionService.CanLaunchRound(), OnlineRoomActionService.BuildLaunchRoundLabel(), size: 17);
        page.Action(new Rect2(966, 26, 132, 52), "Leave", "icon-quit", LeaveOnlineRoom, RoyalBoard.Kind.Slate, size: 16, name: "Leave room");
        page.IconButton(new Rect2(1110, 26, 52, 52), "icon-tools", "Room tools", () => ShowRoomTools(bar));
        return bar;
    }

    private static string JoinedRoomState(OnlineRoomJoinTicket ticket, MultiplayerRoomSnapshot snapshot)
    {
        var seat = OnlineRoomJoinService.IsTicketExpired(ticket) ? "Seat expired"
            : ticket.Status?.ToLowerInvariant() switch { "hosted" => "Hosting", "spectate" => "Watching", "waitlist" => "Waitlisted", _ => "Runner seat" };
        if (snapshot == null || !snapshot.HasRoom) return $"{seat} · {ticket.BoardCode}";
        var round = snapshot.RaceCountdownActive ? $"starting in {snapshot.RaceCountdownRemainingSeconds:0}s"
            : snapshot.RoundComplete ? "round complete"
            : snapshot.RoundLocked ? "race live"
            : $"{snapshot.Peers.Count(peer => peer.IsReady)} of {snapshot.Peers.Count} ready";
        return $"{seat} · {ticket.BoardCode} · {round}";
    }

    /// <summary>The joined room's less frequent actions, in one menu beside the bar.</summary>
    private void ShowRoomTools(Control bar)
    {
        var menu = new PopupMenu();
        menu.AddItem("Refresh room", 0);
        menu.AddItem("Refresh scoreboard", 1);
        menu.AddItem(_onlineRoomAutoRefreshEnabled ? "Pause auto refresh" : "Resume auto refresh", 2);
        menu.AddItem(BuildRenewSeatButtonLabel(), 3);
        menu.AddItem("Reset round", 4);
        menu.SetItemDisabled(menu.GetItemIndex(4), !OnlineRoomActionService.CanResetRound());
        menu.AddSeparator();
        var reasons = OnlineRoomReportReasonCatalog.GetAll().ToArray();
        for (var i = 0; i < reasons.Length; i++)
        {
            menu.AddItem($"Report: {reasons[i].Title}", 100 + i);
            menu.SetItemDisabled(menu.GetItemIndex(100 + i), !OnlineRoomReportService.CanSubmitJoinedRoomReport());
        }
        menu.IdPressed += id =>
        {
            switch (id)
            {
                case 0: RefreshJoinedOnlineRoom(); break;
                case 1: RefreshOnlineRoomScoreboard(); break;
                case 2: ToggleOnlineRoomAutoRefresh(); break;
                case 3: RenewOnlineRoomSeat(); break;
                case 4: ResetOnlineRoomRound(); break;
                default:
                    // A report can't be taken back, and its menu items sit beside the room's everyday tools.
                    var reason = reasons[(int)id - 100];
                    MedievalUi.ShowConfirmation(this, "Report this room?", $"Send a report for: {reason.Title}.", "Send report", () =>
                    {
                        _selectedRoomReportReasonId = OnlineRoomReportReasonCatalog.NormalizeId(reason.Id);
                        SubmitOnlineRoomReport();
                    }, "Cancel");
                    break;
            }
        };
        AddChild(menu);
        menu.Theme = MedievalUiTheme();
        // Screen coordinates: a popup is its own window wherever subwindows aren't embedded.
        menu.Popup(new Rect2I((Vector2I)(bar.GetScreenTransform() * new Vector2(1110, 82)), Vector2I.Zero));
        menu.PopupHide += menu.QueueFree;
    }
}
