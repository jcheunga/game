using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The LAN race on a painted board: the armed challenge board (its battlefield, mutator and squad) and the room
/// controls (host, join by address, broadcast, close) on the left; the runners in the room with their ready state
/// and live race telemetry, or the round's results and the session's standings, on the right. Ready up and
/// Launch are the footer actions.
/// </summary>
public partial class LanRaceMenu : RoyalBoard
{
    private static readonly string[] Pages = { "Runners", "Results" };
    private const int RunnersPerPage = 4;
    private int _tab, _page;
    private string _address = "127.0.0.1";
    private string _message = "";
    private LineEdit _addressEdit;

    public LanRaceMenu() : base("LAN Race", "horn") { }

    private static LanChallengeService Service => LanChallengeService.Instance;

    protected override void Build()
    {
        if (Service != null) Service.StateChanged += OnStateChanged;
        base.Build();
    }

    public override void _ExitTree()
    {
        if (Service != null) Service.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged()
    {
        // A room event must not throw away an address being typed.
        var typing = GodotObject.IsInstanceValid(_addressEdit) && _addressEdit.HasFocus();
        Refresh();
        if (typing && GodotObject.IsInstanceValid(_addressEdit)) { _addressEdit.GrabFocus(); _addressEdit.CaretColumn = _addressEdit.Text.Length; }
    }

    protected override void Compose()
    {
        var service = Service;
        var snapshot = service?.BuildRoomSnapshot();
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        Chip("icon-scroll-gold", snapshot?.HasRoom == true ? snapshot.SharedChallengeCode : challenge.Code, "Challenge board code");
        Chip("icon-people-gold", snapshot?.HasRoom == true ? $"{snapshot.RoleLabel} · {snapshot.PeerCount}" : "No room",
            snapshot?.HasRoom == true ? $"{snapshot.PeerCount} in the room" : "No LAN room open");
        var body = Board.Body; var footer = Board.Footer;
        Caps("Armed board", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 260, 40), 18);
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 420, footer.Position.Y - 10 - Board.Notch.End.Y);
        BoardCard(new Rect2(left.Position, new Vector2(left.Size.X, 212)), challenge);
        Room(new Rect2(left.Position.X, left.Position.Y + 222, left.Size.X, left.Size.Y - 222), snapshot);
        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        for (var i = 0; i < Pages.Length; i++)
        {
            var index = i;
            Tab(new Rect2(right.Position.X + i * 200, right.Position.Y, 188, 46), Pages[i], i == _tab, () => { _tab = index; _page = 0; Refresh(); });
        }
        var page = new Rect2(right.Position.X, right.Position.Y + 58, right.Size.X, right.Size.Y - 58);
        if (_tab == 0) Runners(page, snapshot); else Results(page, snapshot);

        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        var ready = Action(new Rect2(footer.End.X - 520, footer.Position.Y, 196, footer.Size.Y), ReadyCaption(service), "icon-check", ToggleReady, Kind.Navy,
            service != null && service.HasRoom && (!service.RoundLocked || service.RoundComplete), service?.HasRoom == true ? "Tell the room you are ready" : "Host or join a room first", size: 16);
        ready.AccessibilityName = "Ready up";
        var canLaunch = service != null && service.IsHosting && !service.RoundLocked && !string.IsNullOrWhiteSpace(service.SharedChallengeCode)
            && service.AllPeersReady && service.AllPeersDecksReady;
        Action(new Rect2(footer.End.X - 310, footer.Position.Y - 2, 310, footer.Size.Y + 4), "Launch LAN race", "icon-swords-dark", LaunchRace, Kind.Gold, canLaunch,
            canLaunch ? "Start the race for every runner" : service?.IsHosting == true ? "Every runner must be ready with a full squad" : "Only the host launches the race", size: 18);
        NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y - 2, footer.Size.X - 718, footer.Size.Y + 4));
        // The last action's result, or else the service's own status (room opened, runner joined, launch blocked).
        Notify(_message.Length > 0 ? _message : service?.SessionStatus ?? "LAN play is unavailable.");
    }

    private static string ReadyCaption(LanChallengeService service) =>
        service == null || !service.HasRoom ? "Ready up"
        : service.LocalSpectating ? service.RoundComplete ? "Join rematch" : "Spectating"
        : service.LocalReady ? "Unready"
        : service.RoundComplete ? "Ready rematch" : "Ready up";

    private void BoardCard(Rect2 rect, AsyncChallengeDefinition challenge)
    {
        var state = GameState.Instance;
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, state.MaxStage));
        var route = RouteCatalog.Normalize(stage?.MapId ?? "city");
        var path = $"res://assets/ui/royal/missions/{route}.png";
        var picture = new Rect2(rect.Position, new Vector2(rect.Size.X, 120));
        Picture(picture, ResourceLoader.Exists(path) ? RoyalArt.Load(path) : RoyalArt.Item("activity-challenges"));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 52, picture.Size.X - 6, 49));
        Plaque(picture.Position + new Vector2(12, 12), challenge.Code, 20, 38);
        Serif(stage != null ? $"{stage.MapName} · stage {stage.StageNumber}" : $"Stage {challenge.Stage}", new Rect2(picture.Position.X + 16, picture.End.Y - 40, picture.Size.X - 32, 32), 20, weight: 700);
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var y = picture.End.Y + 8;
        Image("icon-skull-red", new Rect2(rect.Position.X + 4, y + 2, 26, 26));
        Serif(mutator.Title, new Rect2(rect.Position.X + 36, y - 2, rect.Size.X - 186, 32), 19, Soft, weight: 600);
        Action(new Rect2(rect.End.X - 142, y - 2, 142, 36), "Briefing", "icon-open-book", () => RealmUi.Details(this, "Board briefing", Briefing(challenge, stage, mutator)), size: 15);
        // The squad that races: the board's locked squad, or the player's own.
        y += 40;
        var units = state.GetSelectedAsyncChallengeDeckUnits();
        var spells = state.GetSelectedAsyncChallengeDeckSpells();
        var cell = 46f;
        Caps(state.HasSelectedAsyncChallengeLockedDeck ? "Locked" : "Squad", new Rect2(rect.Position.X + 4, y, 70, cell), 16, Soft);
        var x = rect.Position.X + 78;
        foreach (var unit in units.Take(6))
        {
            var slot = new Rect2(x, y, cell, cell);
            Frame("relic-card", slot, 8).With(p => { p.TooltipText = unit.DisplayName; p.MouseFilter = MouseFilterEnum.Pass; });
            var figure = new UnitFigure { Position = slot.Position + new Vector2(3, 3), Size = slot.Size - new Vector2(6, 6) };
            figure.SetUnit(unit);
            Live.AddChild(figure);
            x += cell + 6;
        }
        foreach (var spell in spells.Take(Math.Max(0, 6 - units.Count)))
        {
            var slot = new Rect2(x, y, cell, cell);
            Frame("endless-spell-slot", slot, 10).With(p => { p.TooltipText = spell.DisplayName; p.MouseFilter = MouseFilterEnum.Pass; });
            Image(UiArtLoader.TryLoadSpellIcon(spell), slot.Grow(-6));
            x += cell + 6;
        }
        if (spells.Count == 0 && units.Count < 6)
            Serif(state.HasSelectedAsyncChallengeLockedDeck ? "Spell lock" : "No spells", new Rect2(x + 4, y, rect.End.X - x - 4, cell), 18, Soft);
    }

    private static string Briefing(AsyncChallengeDefinition challenge, StageDefinition stage, AsyncChallengeMutatorDefinition mutator)
    {
        var state = GameState.Instance;
        var squad = string.Join(", ", state.GetSelectedAsyncChallengeDeckUnits().Select(unit => unit.DisplayName));
        return $"{challenge.Code}\n{stage.MapName} · stage {stage.StageNumber}: {stage.StageName}\nMutator: {mutator.Title}\n" +
            $"{(state.HasSelectedAsyncChallengeLockedDeck ? "Locked squad" : "Your squad")}: {squad}\n{state.BuildSelectedAsyncChallengeDeckSynergyInlineSummary()}\n\n" +
            $"{StageEncounterIntel.BuildCompactSummary(stage)}\n\nChange the armed board on the Challenges screen, then come back here to host or broadcast it.";
    }

    private void Room(Rect2 rect, MultiplayerRoomSnapshot snapshot)
    {
        var service = Service;
        var hosting = service?.IsHosting == true;
        var room = Section(rect, "Room", "emblem-friends", hosting && !string.IsNullOrWhiteSpace(snapshot?.JoinAddressSummary) ? $"Share {snapshot.JoinAddressSummary}" : null);
        var field = new Rect2(room.Position.X, room.Position.Y, room.Size.X - 132, 44);
        _addressEdit = new LineEdit { Position = field.Position, Size = field.Size, PlaceholderText = "Host address, e.g. 192.168.0.12", Text = _address, AccessibilityName = "Host address" };
        var box = RoyalKit.Slice("season-locked", 10);
        box.ContentMarginLeft = 14; box.ContentMarginRight = 10;
        foreach (var look in new[] { "normal", "focus", "read_only" }) _addressEdit.AddThemeStyleboxOverride(look, box);
        _addressEdit.AddThemeFontOverride("font", RoyalFonts.Body(600));
        _addressEdit.AddThemeFontSizeOverride("font_size", 20);
        _addressEdit.AddThemeColorOverride("font_color", RoyalText.Cream);
        _addressEdit.AddThemeColorOverride("font_placeholder_color", new Color("8f8676"));
        _addressEdit.AddThemeColorOverride("caret_color", RealmUi.Gold);
        _addressEdit.TextChanged += text => _address = text;
        _addressEdit.TextSubmitted += _ => JoinHost();
        Live.AddChild(_addressEdit);
        Action(new Rect2(room.End.X - 120, room.Position.Y, 120, 44), "Join", "icon-people-gold", JoinHost, Kind.Navy,
            service == null || !service.HasRoom || service.IsClient, "Join the host at this address", size: 16, name: "Join host");
        var y = room.Position.Y + 54;
        var width = (room.Size.X - 16) / 3;
        Action(new Rect2(room.Position.X, y, width, 42), hosting ? "Rehost" : "Host", null, HostBoard, Kind.Navy,
            !(hosting && service.RoundLocked), "Host the armed board for the room", size: 15, name: hosting ? "Rehost board" : "Host board");
        Action(new Rect2(room.Position.X + width + 8, y, width, 42), "Broadcast", null, RefreshHostedBoard, Kind.Navy,
            hosting && !service.RoundLocked, "Send the armed board to every runner", size: 15, name: "Broadcast update");
        Action(new Rect2(room.Position.X + 2 * (width + 8), y, width, 42), "Close", null, () => { Service?.CloseRoom(); _message = ""; Refresh(); }, Kind.Slate,
            service?.HasRoom == true, "Close the room", size: 15, name: "Close room");
    }

    private void Runners(Rect2 rect, MultiplayerRoomSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.HasRoom)
        {
            var room = Section(rect, "No room open", "emblem-horn");
            Picture(new Rect2(room.Position.X + 10, room.Position.Y + 4, room.Size.X - 20, 190), RoyalArt.Item("activity-challenges"));
            Heading("Race on one network", new Rect2(room.Position.X, room.Position.Y + 208, room.Size.X, 40), 26, HorizontalAlignment.Center);
            Paragraph("Host the armed board, or join a host by its address. Everyone readies up, then the host launches the race.",
                new Rect2(room.Position.X + 24, room.Position.Y + 252, room.Size.X - 48, 60), 2, 19, Soft, HorizontalAlignment.Center);
            return;
        }
        var peers = snapshot.Peers;
        var pages = Math.Max(1, (peers.Count + RunnersPerPage - 1) / RunnersPerPage);
        _page = Math.Clamp(_page, 0, pages - 1);
        var list = Section(rect, $"{snapshot.RoleLabel} room", "emblem-horn", RoomStatus(snapshot));
        var area = new Rect2(list.Position, new Vector2(list.Size.X, list.Size.Y - (pages > 1 ? 40 : 0)));
        var pitch = area.Size.Y / RunnersPerPage;
        for (var i = 0; i < RunnersPerPage; i++)
        {
            var at = _page * RunnersPerPage + i;
            if (at >= peers.Count) break;
            Runner(new Rect2(area.Position.X, area.Position.Y + i * pitch, area.Size.X, pitch - 6), peers[at]);
        }
        Pager(new Rect2(list.Position.X, list.End.Y - 36, list.Size.X, 36), _page, pages, page => { _page = page; Refresh(); }, "runners");
    }

    private static string RoomStatus(MultiplayerRoomSnapshot snapshot)
    {
        var runners = snapshot.Peers.Where(peer => peer.IsLaunchEligible).ToArray();
        if (snapshot.RaceCountdownActive) return $"Starting in {snapshot.RaceCountdownRemainingSeconds:0}";
        if (snapshot.RoundComplete) return "Round complete";
        if (snapshot.RoundLocked) return $"{runners.Count(peer => peer.PostedScore >= 0)}/{runners.Length} finished";
        return $"{runners.Count(peer => peer.IsReady)}/{runners.Length} ready";
    }

    private void Runner(Rect2 row, MultiplayerRoomPeerSnapshot peer)
    {
        var racing = peer.Phase is "racing" or "loading";
        var done = peer.PostedScore >= 0;
        var lit = peer.IsReady && !racing && !done;
        Frame(lit ? "ach-card-ready" : "season-locked", row, 10).With(p => { p.TooltipText = peer.DeckText; p.MouseFilter = MouseFilterEnum.Pass; });
        var portrait = new Rect2(row.Position + new Vector2(8, 4), new Vector2(row.Size.Y - 8, row.Size.Y - 8));
        UnitDefinition lead = null;
        foreach (var id in peer.DeckUnitIds)
        {
            try { lead = GameData.GetUnit(id); } catch { lead = null; }
            if (lead != null) break;
        }
        if (lead != null)
        {
            var figure = new UnitFigure { Position = portrait.Position, Size = portrait.Size };
            figure.SetUnit(lead);
            Live.AddChild(figure);
        }
        else Image("emblem-friends", portrait.Grow(-6));
        var x = portrait.End.X + 10;
        Serif(peer.IsLocalPlayer ? $"{peer.Label} · you" : peer.Label, new Rect2(x, row.Position.Y + 8, row.End.X - x - 150, 30), 20, weight: 700);
        Serif(Presence(peer), new Rect2(x, row.Position.Y + 38, row.End.X - x - 150, 28), 18, Soft, weight: 500);
        var badge = new Rect2(row.End.X - 140, row.Position.Y, 128, row.Size.Y);
        if (done) Caps(peer.PostedRank > 0 ? $"#{peer.PostedRank} · {peer.PostedScore:N0}" : $"{peer.PostedScore:N0} pts", badge, 17, new Color("f6e2ad"), HorizontalAlignment.Right);
        else if (racing) Caps(peer.Phase == "loading" ? "Loading" : "Racing", badge, 17, Engraved, HorizontalAlignment.Right);
        else if (!peer.IsLaunchEligible) Caps("Watching", badge, 17, Soft, HorizontalAlignment.Right);
        else Caps(peer.IsReady ? "Ready" : "Not ready", badge, 17, peer.IsReady ? Green : new Color("a49a8a"), HorizontalAlignment.Right);
    }

    /// <summary>The runner's state in a few words: their squad before the race, live telemetry during it.</summary>
    private static string Presence(MultiplayerRoomPeerSnapshot peer)
    {
        if (peer.Phase is "racing" or "submitted" && peer.RaceElapsedSeconds >= 0)
            return $"{peer.RaceElapsedSeconds:0}s · hull {peer.HullPercent}% · {peer.EnemyDefeats} defeated";
        if (peer.Phase is "racing" or "loading") return peer.PresenceText;
        return peer.HasFullDeck ? "Full squad" : "Squad incomplete";
    }

    private void Results(Rect2 rect, MultiplayerRoomSnapshot snapshot)
    {
        var finished = snapshot?.Peers.Where(peer => peer.PostedScore >= 0).OrderBy(peer => peer.PostedRank).ToArray() ?? Array.Empty<MultiplayerRoomPeerSnapshot>();
        var top = new Rect2(rect.Position, new Vector2(rect.Size.X, Mathf.Floor(rect.Size.Y * .5f) - 6));
        var round = Section(top, "This round", "emblem-rankings", snapshot?.HasRoom == true ? snapshot.SharedChallengeCode : null);
        if (finished.Length == 0) Serif("No results posted yet.", new Rect2(round.Position.X + 4, round.Position.Y, round.Size.X, 32), 19, Soft);
        var pitch = Mathf.Min(38, round.Size.Y / 4);
        for (var i = 0; i < Math.Min(4, finished.Length); i++)
        {
            var peer = finished[i];
            var y = round.Position.Y + i * pitch;
            Caps($"#{peer.PostedRank}", new Rect2(round.Position.X + 4, y, 50, pitch), 17, i == 0 ? new Color("f6e2ad") : Engraved);
            Serif(peer.IsLocalPlayer ? $"{peer.Label} · you" : peer.Label, new Rect2(round.Position.X + 60, y, round.Size.X - 200, pitch), 19, weight: 700);
            Serif($"{peer.PostedScore:N0} pts", new Rect2(round.Position.X, y, round.Size.X - 4, pitch), 19, new Color("f6e2ad"), HorizontalAlignment.Right, 700);
        }
        var standings = Service?.SessionStandings ?? Array.Empty<(string Name, int Races, int Wins, int TotalScore, int BestScore)>();
        var session = Section(new Rect2(rect.Position.X, top.End.Y + 12, rect.Size.X, rect.End.Y - top.End.Y - 12), "This session", "emblem-crown",
            standings.Count == 0 ? null : "Wins · races · best");
        if (standings.Count == 0) Serif("No completed races yet.", new Rect2(session.Position.X + 4, session.Position.Y, session.Size.X, 32), 19, Soft);
        pitch = Mathf.Min(38, session.Size.Y / 4);
        for (var i = 0; i < Math.Min(4, standings.Count); i++)
        {
            var entry = standings[i];
            var y = session.Position.Y + i * pitch;
            Caps($"{i + 1}", new Rect2(session.Position.X + 4, y, 50, pitch), 17, i == 0 ? new Color("f6e2ad") : Engraved);
            Serif(entry.Name, new Rect2(session.Position.X + 60, y, session.Size.X - 260, pitch), 19, weight: 700);
            Serif($"{entry.Wins} · {entry.Races} · {entry.BestScore:N0}", new Rect2(session.Position.X, y, session.Size.X - 4, pitch), 19, new Color("f6e2ad"), HorizontalAlignment.Right, 700);
        }
    }

    private void Run(Func<LanChallengeService, (bool Ok, string Message)> action)
    {
        if (Service == null) { _message = "LAN play is unavailable."; Refresh(); return; }
        _message = action(Service).Message;
        Refresh();
    }

    private void HostBoard() => Run(service => (service.HostSelectedBoard(out var message), message));
    private void RefreshHostedBoard() => Run(service => (service.RefreshHostedBoard(out var message), message));
    private void JoinHost() => Run(service => (service.JoinRoom(_address, out var message), message));
    private void ToggleReady() => Run(service => (service.ToggleLocalReady(out var message), message));
    private void LaunchRace() => Run(service => (service.LaunchRace(out var message), message));
}
