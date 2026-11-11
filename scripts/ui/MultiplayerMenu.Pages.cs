using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The board pages under the challenge briefing, composed from the same painted kit as the activity boards and
/// fitted to the plate's board without scrolling: Records (the loaded code's best, its medal ladder and the runs
/// on it), Daily (today's board and its leaders), Featured and Saved (board cards, three to a page) and Squad (the
/// cards that race). With internet rooms on, the first page lists rooms instead (MultiplayerMenu.Rooms.cs).
/// </summary>
public partial class MultiplayerMenu
{
    /// <summary>The plate's board, inside its painted rim.</summary>
    private static readonly Rect2 PageArea = new(58, 296, 1164, 302);
    private const int CardsPerPage = 3, RunsPerPage = 4;

    private static readonly Color Gold = new("f6e2ad");
    private static readonly Color Loss = new("e2907f");

    private Control _pageLayer;
    private RoyalPage _page;
    private int _runsPage, _featuredPage, _savedPage;

    private void BuildPageLayer()
    {
        _pageLayer = new Control { Name = "BoardPage", Position = Vector2.Zero, Size = RoyalArt.Canvas, MouseFilter = MouseFilterEnum.Ignore };
        _royalLayer.AddChild(_pageLayer);
        _page = new RoyalPage(_pageLayer);
        BuildRoomsList();
    }

    private bool _pageRefreshQueued;

    /// <summary>A refresh's recompose. Recomposing frees every button on the page, so while a finger is down on one
    /// (a room auto-refresh can land mid-press) it waits for the release instead of dropping the tap.</summary>
    private void RefreshPage()
    {
        if (!PageButtonHeld(_pageLayer) && !PageButtonHeld(_roomsList)) { ComposePage(); return; }
        if (_pageRefreshQueued) return;
        _pageRefreshQueued = true;
        GetTree().CreateTimer(.1).Timeout += () =>
        {
            _pageRefreshQueued = false;
            if (IsInstanceValid(this) && IsInsideTree()) RefreshPage();
        };
    }

    private static bool PageButtonHeld(Node node)
    {
        if (node == null) return false;
        if (node is BaseButton { ToggleMode: false } button && button.IsPressed()) return true;
        foreach (var child in node.GetChildren())
            if (PageButtonHeld(child)) return true;
        return false;
    }

    /// <summary>Recomposes the selected page from the current state.</summary>
    private void ComposePage()
    {
        if (_page == null) return;
        RoyalUiTools.Clear(_pageLayer);
        var rooms = _board == 0 && LaunchFeatures.InternetRooms;
        _roomsScroll.Visible = rooms;
        switch (_board)
        {
            case 0 when rooms: FillRoomsList(); break;
            case 0: ComposeRecords(); break;
            case 1: ComposeDaily(); break;
            case 2: ComposeFeatured(); break;
            case 3: ComposeSaved(); break;
            default: ComposeSquad(); break;
        }
    }

    // ---- Records --------------------------------------------------------------------------------------------

    private void ComposeRecords()
    {
        var state = GameState.Instance;
        var challenge = state.GetSelectedAsyncChallenge();
        var best = state.GetAsyncChallengeBestScore(challenge.Code);
        var column = new Rect2(PageArea.Position, new Vector2(372, PageArea.Size.Y));
        _page.Frame("ach-card", column, 14);
        var x = column.Position.X + 22;
        var width = column.Size.X - 44;
        _page.Caps("Best on this code", new Rect2(x, column.Position.Y + 16, width, 28), 17, RoyalBoard.Soft);
        if (best > 0)
        {
            var medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, best);
            _page.Heading(best.ToString("N0"), new Rect2(x, column.Position.Y + 48, width - 96, 56), 44);
            _page.Serif(medal == "No Medal" ? "No medal yet" : $"{medal} medal", new Rect2(x, column.Position.Y + 104, width - 96, 30), 22, RoyalBoard.Soft);
            if (MedalArt(medal) is { } crest) _page.Image(crest, new Rect2(column.End.X - 22 - 88, column.Position.Y + 30, 88, 88));
        }
        else
        {
            _page.Heading("No score yet", new Rect2(x, column.Position.Y + 48, width, 50), 32);
            _page.Serif("Start the challenge to set one.", new Rect2(x, column.Position.Y + 100, width, 30), 22, RoyalBoard.Soft);
        }
        MedalLadder(new Rect2(x, column.Position.Y + 148, width, 92), challenge, best);
        _page.Rule(new Rect2(column.Position.X + 18, column.End.Y - 52, column.Size.X - 36, 1));
        var runs = state.ChallengeRuns;
        _page.Serif(runs == 1 ? "1 challenge run in all" : $"{runs} challenge runs in all", new Rect2(x, column.End.Y - 44, width, 30), 20, RoyalBoard.Soft, HorizontalAlignment.Center);

        var history = state.GetRecentChallengeHistory(RunsPerPage * 3, challenge.Code);
        var table = new Rect2(column.End.X + 14, PageArea.Position.Y, PageArea.End.X - column.End.X - 14, PageArea.Size.Y);
        var pages = Math.Max(1, (history.Count + RunsPerPage - 1) / RunsPerPage);
        _runsPage = Math.Clamp(_runsPage, 0, pages - 1);
        var room = _page.Section(table, "Runs on this code", "icon-open-book", pages > 1 ? null : history.Count == 0 ? null : history.Count == 1 ? "1 run" : $"{history.Count} runs");
        _page.Pager(new Rect2(table.End.X - 20 - 130, table.Position.Y + 8, 130, 40), _runsPage, pages, page => { _runsPage = page; ComposePage(); }, "runs");
        if (history.Count == 0)
        {
            EmptyNote(room, "No runs yet", "Start the challenge to post your first score on this code.");
            return;
        }
        // Result · score · medal · stars · time · when.
        float[] columns = { 0, 118, 240, 392, 490, room.Size.X };
        string[] headers = { "Result", "Score", "Medal", "Stars", "Time" };
        for (var i = 0; i < headers.Length; i++)
            _page.Caps(headers[i], new Rect2(room.Position.X + 12 + columns[i], room.Position.Y - 4, columns[i + 1] - columns[i] - 8, 28), 16, RoyalBoard.Soft);
        _page.Caps("Played", new Rect2(room.Position.X, room.Position.Y - 4, room.Size.X - 12, 28), 16, RoyalBoard.Soft, HorizontalAlignment.Right);
        for (var i = 0; i < RunsPerPage; i++)
        {
            var at = _runsPage * RunsPerPage + i;
            if (at >= history.Count) break;
            RunRow(new Rect2(room.Position.X, room.Position.Y + 30 + i * 46, room.Size.X, 40), columns, history[at], challenge);
        }
    }

    private void RunRow(Rect2 row, float[] columns, ChallengeRunRecord run, AsyncChallengeDefinition challenge)
    {
        _page.Frame("season-locked", row, 10);
        float Left(int column) => row.Position.X + 12 + columns[column];
        float Width(int column) => columns[column + 1] - columns[column] - 8;
        var (result, ink) = run.Retreated ? ("Retreat", RoyalBoard.Soft) : run.Won ? ("Win", RoyalBoard.Green) : ("Defeat", Loss);
        _page.Caps(result, new Rect2(Left(0), row.Position.Y, Width(0), row.Size.Y), 18, ink);
        _page.Serif(run.Score.ToString("N0"), new Rect2(Left(1), row.Position.Y, Width(1), row.Size.Y), 22, Gold, weight: 700);
        var medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, run.Score);
        if (MedalArt(medal) is { } crest)
        {
            _page.Image(crest, new Rect2(Left(2), row.Position.Y + 6, 28, 28));
            _page.Serif(medal, new Rect2(Left(2) + 34, row.Position.Y, Width(2) - 34, row.Size.Y), 22);
        }
        else _page.Serif("—", new Rect2(Left(2), row.Position.Y, Width(2), row.Size.Y), 22, RoyalBoard.Soft);
        for (var star = 0; star < 3; star++)
            _page.Image("result-star", new Rect2(Left(3) + star * 26, row.Position.Y + 9, 22, 22), star < run.StarsEarned ? Colors.White : new Color(1, 1, 1, .22f));
        var time = TimeSpan.FromSeconds(Math.Max(0, run.ElapsedSeconds));
        _page.Serif($"{(int)time.TotalMinutes}:{time.Seconds:D2}", new Rect2(Left(4), row.Position.Y, Width(4), row.Size.Y), 22);
        _page.Serif(WhenPlayed(run.PlayedAtUnixSeconds), new Rect2(row.Position.X, row.Position.Y, row.Size.X - 12, row.Size.Y), 20, RoyalBoard.Soft, HorizontalAlignment.Right);
    }

    /// <summary>The four medals with the score each needs; the ones a best score has reached are lit.</summary>
    private void MedalLadder(Rect2 rect, AsyncChallengeDefinition challenge, int best)
    {
        var targets = AsyncChallengeCatalog.GetTargetScores(challenge);
        (string Medal, int Score)[] rungs = { ("Bronze", targets.Bronze), ("Silver", targets.Silver), ("Gold", targets.Gold), ("Crown Ace", targets.Ace) };
        var pitch = rect.Size.X / rungs.Length;
        for (var i = 0; i < rungs.Length; i++)
        {
            var reached = best >= rungs[i].Score;
            var cell = new Rect2(rect.Position.X + i * pitch, rect.Position.Y, pitch, rect.Size.Y);
            var crest = _page.Image(MedalArt(rungs[i].Medal), new Rect2(cell.GetCenter().X - 27, cell.Position.Y, 54, 54), reached ? Colors.White : new Color(.42f, .42f, .46f, .7f));
            crest.TooltipText = $"{rungs[i].Medal} · {rungs[i].Score:N0}"; crest.MouseFilter = MouseFilterEnum.Pass;
            _page.Serif(rungs[i].Score.ToString("N0"), new Rect2(cell.Position.X, cell.Position.Y + 58, cell.Size.X, 30), 20, reached ? Gold : RoyalBoard.Soft, HorizontalAlignment.Center, 700);
        }
    }

    private static Texture2D MedalArt(string medal) => medal switch
    {
        "Bronze" => RoyalArt.Item("arena-bronze"),
        "Silver" => RoyalArt.Item("arena-silver"),
        "Gold" => RoyalArt.Item("arena-gold"),
        "Crown Ace" => RoyalArt.Item("arena-platinum"),
        _ => null
    };

    private static string WhenPlayed(long unixSeconds)
    {
        if (unixSeconds <= 0) return "—";
        var played = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (played.Date == today) return $"Today {played:HH:mm}";
        if (played.Date == today.AddDays(-1)) return $"Yesterday {played:HH:mm}";
        return played.ToString(played.Year == today.Year ? "MMM d" : "MMM d yyyy");
    }

    /// <summary>A quiet empty state: the challenges painting, a heading and one line.</summary>
    private void EmptyNote(Rect2 room, string heading, string line)
    {
        _page.Image(RoyalArt.Item("activity-challenges"), new Rect2(room.GetCenter().X - 120, room.Position.Y + 4, 240, 108), new Color(1, 1, 1, .6f));
        _page.Heading(heading, new Rect2(room.Position.X, room.Position.Y + 118, room.Size.X, 40), 26, HorizontalAlignment.Center);
        _page.Paragraph(line, new Rect2(room.Position.X + 30, room.Position.Y + 162, room.Size.X - 60, 56), 2, 20, RoyalBoard.Soft, HorizontalAlignment.Center);
    }

    // ---- Daily ----------------------------------------------------------------------------------------------

    private void ComposeDaily()
    {
        var state = GameState.Instance;
        var daily = GameState.GetDailyChallenge();
        var stage = GameData.GetStage(Mathf.Clamp(daily.StageIndex, 1, state.MaxStage));
        var completed = state.HasCompletedDailyChallenge();
        var unlocked = stage.StageNumber <= state.HighestUnlockedStage;
        var card = new Rect2(PageArea.Position, new Vector2(420, PageArea.Size.Y));
        _page.Frame(completed ? "ach-card-ready" : "ach-card", card, 14);
        var picture = new Rect2(card.Position + new Vector2(14, 14), new Vector2(card.Size.X - 28, 150));
        BoardPicture(picture, stage, daily.BoardLabel, !unlocked && !completed);
        if (completed) Badge(picture, "icon-check-green", "Done");

        var y = picture.End.Y + 10;
        var x = picture.Position.X;
        if (daily.LockedSquad && daily.LockedDeckUnitIds.Length > 0)
        {
            _page.Caps("Set squad", new Rect2(x + 2, y, 130, 46), 18, RoyalBoard.Soft);
            SquadStrip(new Vector2(x + 136, y), daily.LockedDeckUnitIds);
        }
        else _page.Serif("Bring your own squad", new Rect2(x + 2, y, picture.Size.X, 46), 22, RoyalBoard.Soft);

        var button = new Rect2(x, card.End.Y - 14 - 52, picture.Size.X, 52);
        if (!unlocked && !completed)
            _page.Action(button, $"Unlocks at stage {stage.StageNumber}", "icon-lock-small", null, RoyalBoard.Kind.Slate, false, size: 17, name: "Play challenge");
        else
            _page.Action(button, completed ? "Replay challenge" : "Play challenge", "icon-swords-dark", () => LoadDailyChallenge(daily), RoyalBoard.Kind.Gold, size: 18);

        var leaders = new Rect2(card.End.X + 14, PageArea.Position.Y, PageArea.End.X - card.End.X - 14, PageArea.Size.Y);
        var room = _page.Section(leaders, "Today's leaders", "emblem-rankings", DateTime.TryParse(daily.Date, out var date) ? date.ToString("MMMM d") : daily.Date);
        var entries = _cachedDailyLeaderboard?.Entries ?? new List<DailyLeaderboardEntry>();
        if (string.IsNullOrWhiteSpace(BuildDailyLeaderboardBaseUrl()))
        {
            EmptyNote(room, "Rankings are offline", "Connect to the realm's server to compare today's scores.");
            return;
        }
        if (entries.Count == 0)
        {
            EmptyNote(room, "No scores yet", "Be the first to post a score on today's board.");
            return;
        }
        // Five rows fit. A player ranked below fifth takes the last row with their own rank, so their score shows.
        const int rows = 5;
        var ranks = Enumerable.Range(0, Math.Min(rows, entries.Count)).ToList();
        var mine = entries.FindIndex(entry => entry.Player == LeaderboardTag.You);
        if (mine >= rows) ranks[rows - 1] = mine;
        var pitch = room.Size.Y / rows;
        for (var i = 0; i < ranks.Count; i++)
        {
            var rank = ranks[i];
            var entry = entries[rank];
            var row = new Rect2(room.Position.X, room.Position.Y + i * pitch, room.Size.X, pitch - 4);
            var you = entry.Player == LeaderboardTag.You;
            if (you) _page.Frame("mp-tab-selected", row, 10);
            _page.Caps($"#{rank + 1}", new Rect2(row.Position.X + 12, row.Position.Y, 70, row.Size.Y), 18, rank == 0 ? Gold : RoyalBoard.Engraved);
            _page.Serif(entry.Player, new Rect2(row.Position.X + 90, row.Position.Y, row.Size.X - 260, row.Size.Y), 22, weight: 700);
            _page.Serif(entry.Score.ToString("N0"), new Rect2(row.Position.X, row.Position.Y, row.Size.X - 14, row.Size.Y), 22, Gold, HorizontalAlignment.Right, 700);
        }
    }

    // ---- Featured and Saved ---------------------------------------------------------------------------------

    private void ComposeFeatured()
    {
        var state = GameState.Instance;
        var boards = FeaturedChallengeCatalog.GetDailyRotation(state.HighestUnlockedStage, state.MaxStage)
            .Concat(ChallengeBoardFeedService.Instance?.GetCachedFeaturedChallenges() ?? new List<FeaturedChallengeDefinition>())
            .GroupBy(featured => AsyncChallengeCatalog.NormalizeCode(featured.Challenge.Code)).Select(group => group.First())
            .ToList();
        var cards = boards.Select(featured => (Action<Rect2>)(rect => FeaturedCard(rect, featured))).ToList();
        CardPages(cards, _featuredPage, page => _featuredPage = page, "boards");
    }

    private void ComposeSaved()
    {
        var state = GameState.Instance;
        var loaded = state.GetSelectedAsyncChallenge().Code;
        var pinned = state.GetPinnedChallengeCodes();
        var canPin = !state.IsChallengeCodePinned(loaded);
        if (pinned.Count == 0)
        {
            var room = _page.Section(PageArea, "Saved codes", "mptab-saved");
            _page.Picture(new Rect2(room.Position.X + 6, room.Position.Y + 4, 360, room.Size.Y - 8), RoyalArt.Item("activity-challenges"));
            var text = new Rect2(room.Position.X + 396, room.Position.Y + 18, room.Size.X - 420, room.Size.Y - 18);
            _page.Heading("No saved codes", new Rect2(text.Position.X, text.Position.Y, text.Size.X, 44), 30);
            _page.Paragraph("Pin a board to keep its code here for rematches.",
                new Rect2(text.Position.X, text.Position.Y + 52, text.Size.X, 64), 2, 21, RoyalBoard.Soft);
            _page.Action(new Rect2(text.Position.X, text.Position.Y + 136, 320, 52), $"Pin {loaded}", "mptab-saved", () => TogglePinnedChallenge(loaded), size: 17, name: "Pin loaded code");
            return;
        }
        var cards = pinned.Select(code => (Action<Rect2>)(rect => SavedCard(rect, code))).ToList();
        if (canPin) cards.Add(rect => PinCard(rect, loaded));
        CardPages(cards, _savedPage, page => _savedPage = page, "codes");
    }

    /// <summary>Lays cards three to a page; with more, chevrons at the board's ends turn the pages.</summary>
    private void CardPages(List<Action<Rect2>> cards, int page, Action<int> turnTo, string noun)
    {
        var pages = Math.Max(1, (cards.Count + CardsPerPage - 1) / CardsPerPage);
        page = Math.Clamp(page, 0, pages - 1);
        turnTo(page);
        var inset = pages > 1 ? 40 : 0;
        var row = new Rect2(PageArea.Position.X + inset, PageArea.Position.Y, PageArea.Size.X - 2 * inset, PageArea.Size.Y);
        var width = (row.Size.X - (CardsPerPage - 1) * 14) / CardsPerPage;
        for (var i = 0; i < CardsPerPage; i++)
        {
            var at = page * CardsPerPage + i;
            if (at >= cards.Count) break;
            cards[at](new Rect2(row.Position.X + i * (width + 14), row.Position.Y, width, row.Size.Y));
        }
        _page.Pager(PageArea, page, pages, turn => { turnTo(turn); ComposePage(); }, noun, dots: false);
    }

    private void FeaturedCard(Rect2 rect, FeaturedChallengeDefinition featured)
    {
        var state = GameState.Instance;
        var challenge = featured.Challenge;
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, state.MaxStage));
        var locked = challenge.Stage > state.HighestUnlockedStage;
        var loaded = IsLoaded(challenge.Code);
        var pinned = state.IsChallengeCodePinned(challenge.Code);
        var inner = BoardCard(rect, stage, featured.Title, loaded, locked);
        MutatorAndBest(new Rect2(inner.Position.X, inner.Position.Y, inner.Size.X, 30), challenge);
        var y = inner.Position.Y + 36;
        if (featured.LockedDeckUnitIds.Count > 0)
        {
            _page.Caps("Set squad", new Rect2(inner.Position.X + 2, y, 130, 46), 18, RoyalBoard.Soft);
            SquadStrip(new Vector2(inner.Position.X + 136, y), featured.LockedDeckUnitIds);
        }
        else _page.Serif("Bring your own squad", new Rect2(inner.Position.X + 2, y, inner.Size.X, 46), 22, RoyalBoard.Soft);
        CardButtons(inner, loaded, locked ? $"Unlocks at stage {challenge.Stage}" : null, () => LoadFeaturedChallenge(featured), pinned, () => TogglePinnedChallenge(challenge.Code), featured.Title);
    }

    private void SavedCard(Rect2 rect, string code)
    {
        var state = GameState.Instance;
        if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out _))
        {
            _page.Frame("ach-card", rect, 14);
            _page.Serif(code, new Rect2(rect.Position.X + 20, rect.Position.Y + 20, rect.Size.X - 40, 32), 22);
            _page.IconButton(new Rect2(rect.End.X - 14 - 50, rect.End.Y - 14 - 50, 50, 50), "mptab-saved", $"Unpin {code}", () => TogglePinnedChallenge(code), RoyalBoard.Kind.Gold);
            return;
        }
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, state.MaxStage));
        var locked = challenge.Stage > state.HighestUnlockedStage;
        var loaded = IsLoaded(challenge.Code);
        var inner = BoardCard(rect, stage, challenge.Code, loaded, locked);
        MutatorAndBest(new Rect2(inner.Position.X, inner.Position.Y, inner.Size.X, 30), challenge);
        var latest = state.GetRecentChallengeHistory(1, challenge.Code);
        var line = latest.Count == 0 ? "No runs yet"
            : $"Last run {WhenPlayed(latest[0].PlayedAtUnixSeconds).ToLowerInvariant()} · {(latest[0].Retreated ? "retreat" : latest[0].Won ? "win" : "defeat")}";
        _page.Serif(line, new Rect2(inner.Position.X + 2, inner.Position.Y + 36, inner.Size.X, 46), 21, RoyalBoard.Soft);
        CardButtons(inner, loaded, locked ? $"Unlocks at stage {challenge.Stage}" : null, () => LoadChallengeCode(challenge.Code, $"Loaded pinned code {challenge.Code}."),
            true, () => TogglePinnedChallenge(challenge.Code), challenge.Code);
    }

    /// <summary>The card that pins the loaded code, so a code from a friend can be kept.</summary>
    private void PinCard(Rect2 rect, string code)
    {
        var card = _page.Card(rect, "Pin loaded code", () => TogglePinnedChallenge(code), slice: "season-locked");
        card.TooltipText = $"Pin {code}";
        _page.Image("empty-plus", new Rect2(rect.GetCenter().X - 24, rect.GetCenter().Y - 70, 48, 48));
        _page.Caps("Pin this code", new Rect2(rect.Position.X, rect.GetCenter().Y - 6, rect.Size.X, 34), 20, RoyalBoard.Engraved, HorizontalAlignment.Center);
        _page.Serif(code, new Rect2(rect.Position.X, rect.GetCenter().Y + 32, rect.Size.X, 32), 22, RoyalBoard.Soft, HorizontalAlignment.Center);
    }

    /// <summary>A board card's frame and picture; returns the room under the picture.</summary>
    private Rect2 BoardCard(Rect2 rect, StageDefinition stage, string title, bool loaded, bool locked)
    {
        _page.Frame(loaded ? "ach-card-ready" : "ach-card", rect, 14);
        var picture = new Rect2(rect.Position + new Vector2(12, 12), new Vector2(rect.Size.X - 24, 124));
        BoardPicture(picture, stage, title, locked);
        return new Rect2(picture.Position.X, picture.End.Y + 8, picture.Size.X, rect.End.Y - 12 - picture.End.Y - 8);
    }

    /// <summary>The board's battlefield with its name plaque and, across its foot, the zone and stage.</summary>
    private void BoardPicture(Rect2 picture, StageDefinition stage, string title, bool locked)
    {
        var mission = $"res://assets/ui/royal/missions/{RouteCatalog.Normalize(stage.MapId)}.png";
        _page.Picture(picture, ResourceLoader.Exists(mission) ? RoyalArt.Load(mission) : RoyalArt.Item("activity-challenges"), locked);
        _page.Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 50, picture.Size.X - 6, 47));
        _page.Plaque(picture.Position + new Vector2(10, 10), title, 20, 36, picture.Size.X - 20);
        var caption = new Rect2(picture.Position.X + 14, picture.End.Y - 38, picture.Size.X - 28, 32);
        // A board past the player's progress is dimmed and padlocked; its button says when it opens.
        if (locked)
        {
            _page.Image("icon-lock-small", new Rect2(caption.Position.X, caption.Position.Y + 4, 22, 24));
            caption = new Rect2(caption.Position.X + 30, caption.Position.Y, caption.Size.X - 30, caption.Size.Y);
        }
        _page.Serif($"{stage.MapName.Replace("'", "’")} · stage {stage.StageNumber}", caption, 22, weight: 700);
    }

    /// <summary>A small dark plaque on a picture's top-right corner ("Loaded", "Done").</summary>
    private void Badge(Rect2 picture, string icon, string text)
    {
        var label = RoyalText.Caps(text, 17, RoyalBoard.Green, 700);
        var width = Mathf.Ceil(label.TextWidth(label.FontSize)) + 56;
        var rect = new Rect2(picture.End.X - 10 - width, picture.Position.Y + 10, width, 36);
        _page.Frame("value-box", rect, 10);
        _page.Image(icon, new Rect2(rect.Position.X + 10, rect.Position.Y + 7, 22, 22));
        _page.Place(label, new Rect2(rect.Position.X + 38, rect.Position.Y, width - 44, rect.Size.Y));
    }

    private void MutatorAndBest(Rect2 row, AsyncChallengeDefinition challenge)
    {
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var best = GameState.Instance.GetAsyncChallengeBestScore(challenge.Code);
        _page.Image("icon-skull-red", new Rect2(row.Position.X, row.Position.Y + 2, 26, 26));
        _page.Serif(mutator.Title, new Rect2(row.Position.X + 32, row.Position.Y, row.Size.X - 150, row.Size.Y), 22, RoyalBoard.Soft);
        if (best <= 0) return;
        var label = _page.Serif(best.ToString("N0"), new Rect2(row.Position.X, row.Position.Y, row.Size.X - 2, row.Size.Y), 22, Gold, HorizontalAlignment.Right, 700);
        if (MedalArt(AsyncChallengeCatalog.ResolveMedalLabel(challenge, best)) is { } crest)
            _page.Image(crest, new Rect2(row.End.X - 2 - label.TextWidth(label.FittedSize()) - 32, row.Position.Y + 1, 28, 28));
    }

    /// <summary>Load (or Loaded) beside a pin toggle, lit gold while the code is pinned.</summary>
    private void CardButtons(Rect2 inner, bool loaded, string lockedUntil, Action load, bool pinned, Action pin, string name)
    {
        var row = new Rect2(inner.Position.X, inner.End.Y - 48, inner.Size.X, 48);
        var loadRect = new Rect2(row.Position, new Vector2(row.Size.X - 60, row.Size.Y));
        if (lockedUntil != null) _page.Action(loadRect, lockedUntil, "icon-lock-small", null, RoyalBoard.Kind.Slate, false, size: 17, name: $"Load {name}");
        else if (loaded) _page.Action(loadRect, "Loaded", "icon-check", null, RoyalBoard.Kind.Claimed, false, size: 17, name: $"Load {name}");
        else _page.Action(loadRect, "Load board", "icon-scroll-gold", load, size: 17, name: $"Load {name}");
        _page.IconButton(new Rect2(row.End.X - 48, row.Position.Y, 48, 48), "mptab-saved", pinned ? $"Unpin {name}" : $"Pin {name}", pin,
            pinned ? RoyalBoard.Kind.Gold : RoyalBoard.Kind.Navy);
    }

    /// <summary>A board's set squad as small framed figures, named for touch readers.</summary>
    private void SquadStrip(Vector2 at, IEnumerable<string> unitIds)
    {
        var x = at.X;
        foreach (var unit in unitIds.Select(GameData.TryGetUnit).Where(unit => unit != null).Take(4))
        {
            var slot = new Rect2(x, at.Y, 46, 46);
            _page.Frame("relic-card", slot, 8).With(p => { p.TooltipText = unit.DisplayName; p.MouseFilter = MouseFilterEnum.Pass; });
            _page.Figure(unit, slot.Grow(-3));
            x += 52;
        }
    }

    private static bool IsLoaded(string code) =>
        AsyncChallengeCatalog.NormalizeCode(code) == AsyncChallengeCatalog.NormalizeCode(GameState.Instance.GetSelectedAsyncChallenge().Code);

    // ---- Squad ----------------------------------------------------------------------------------------------

    private void ComposeSquad()
    {
        var state = GameState.Instance;
        var locked = state.HasSelectedAsyncChallengeLockedDeck;
        var units = state.GetSelectedAsyncChallengeDeckUnits();
        var spells = locked ? new List<SpellDefinition>() : state.GetSelectedAsyncChallengeDeckSpells().ToList();
        var slots = Math.Max(state.LoadoutSizeLimit, units.Count + spells.Count);
        var head = new Rect2(PageArea.Position.X + 4, PageArea.Position.Y, PageArea.Size.X - 8, 36);
        var heading = _page.Caps(locked ? "Set squad · spells locked" : $"Your squad · {units.Count} units · {spells.Count} spells", head, 20);
        var synergies = state.GetSelectedAsyncChallengeDeckSynergies();
        var left = head.Position.X + Mathf.Ceil(heading.TextWidth(heading.FontSize)) + 30;
        var aside = _page.Serif(synergies.Count == 0 ? "No synergy · pair two cards of one role" : string.Join(" · ", synergies.Select(s => s.Title)),
            new Rect2(left, head.Position.Y, head.End.X - left, head.Size.Y), 22, synergies.Count == 0 ? RoyalBoard.Soft : Gold, HorizontalAlignment.Right, synergies.Count == 0 ? 500 : 700);
        aside.TooltipText = string.Join("\n", synergies.Select(s => $"{s.Title}: {s.Summary}")); aside.MouseFilter = MouseFilterEnum.Pass;

        var top = head.End.Y + 8;
        var width = (PageArea.Size.X - (slots - 1) * 12) / slots;
        var height = PageArea.End.Y - 38 - top;
        string[] tints = { "warm", "cool", "gold", "cool", "warm", "cool" };
        for (var i = 0; i < slots; i++)
        {
            var rect = new Rect2(PageArea.Position.X + i * (width + 12), top, width, height);
            var stats = new Rect2(rect.Position.X, rect.End.Y + 6, rect.Size.X, 30);
            if (i < units.Count) UnitCard(rect, stats, units[i], units, tints[i % tints.Length]);
            else if (i - units.Count < spells.Count) SpellCard(rect, stats, spells[i - units.Count]);
            else EmptyCard(rect, locked);
        }
    }

    private void UnitCard(Rect2 rect, Rect2 statsRow, UnitDefinition unit, IReadOnlyList<UnitDefinition> squad, string tint)
    {
        var state = GameState.Instance;
        var stats = state.BuildPlayerUnitStatsForDeck(unit, squad);
        var card = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass,
            TooltipText = $"{unit.DisplayName} · level {state.GetUnitLevel(unit.Id)} · {SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag)}" };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice("prep-card-" + tint, 10, 10, 10, 54));
        _pageLayer.AddChild(card);
        _page.Figure(unit, new Rect2(rect.Position.X + 8, rect.Position.Y + 8, rect.Size.X - 16, rect.Size.Y - 54 - 26));
        _page.Caps($"Lv {state.GetUnitLevel(unit.Id)}", new Rect2(rect.Position.X + 14, rect.Position.Y + 8, 80, 30), 18);
        var medallion = new Rect2(rect.GetCenter().X - 21, rect.End.Y - 54 - 24, 42, 42);
        _page.Image("prep-class-sword", medallion);
        _page.Image(UnitClassIcon.For(unit), medallion.Grow(-8));
        _page.Serif(unit.DisplayName, new Rect2(rect.Position.X + 8, rect.End.Y - 38, rect.Size.X - 16, 32), 22, align: HorizontalAlignment.Center, weight: 700);
        Stats(statsRow, ("stat-courage", unit.Cost.ToString(), "Courage to deploy"), ("stat-health", Mathf.RoundToInt(stats.MaxHealth).ToString("N0"), "Health"),
            ("stat-damage", stats.AttackDamage.ToString("0.#"), "Damage"));
    }

    private void SpellCard(Rect2 rect, Rect2 statsRow, SpellDefinition spell)
    {
        var card = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass,
            TooltipText = $"{spell.DisplayName} · level {GameState.Instance.GetSpellLevel(spell.Id)}" };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice("prep-card-cool", 10, 10, 10, 54));
        _pageLayer.AddChild(card);
        var art = ResourceLoader.Exists($"res://assets/ui/royal/items/{spell.Id}.png") ? RoyalArt.Load($"res://assets/ui/royal/items/{spell.Id}.png") : UiArtLoader.TryLoadSpellIcon(spell);
        _page.Image(art, new Rect2(rect.Position.X + 16, rect.Position.Y + 16, rect.Size.X - 32, rect.Size.Y - 54 - 32));
        _page.Serif(spell.DisplayName, new Rect2(rect.Position.X + 8, rect.End.Y - 42, rect.Size.X - 16, 36), 22, align: HorizontalAlignment.Center, weight: 700);
        Stats(statsRow, ("hud-mana", spell.ManaCost.ToString(), "Mana to cast"));
    }

    private void EmptyCard(Rect2 rect, bool spellLock)
    {
        var card = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass,
            TooltipText = spellLock ? "Spells are locked on this board" : "Empty loadout slot" };
        card.AddThemeStyleboxOverride("panel", RoyalKit.Slice("prep-card-cool", 10, 10, 10, 54).Tinted(new Color(.36f, .36f, .4f)));
        _pageLayer.AddChild(card);
        var mark = new Rect2(rect.GetCenter().X - 20, rect.Position.Y + (rect.Size.Y - 54) / 2 - 20, 40, 40);
        if (spellLock)
        {
            _page.Image("icon-lock-small", mark, new Color(1, 1, 1, .55f));
            _page.Caps("Locked", new Rect2(rect.Position.X, rect.End.Y - 42, rect.Size.X, 36), 18, RoyalBoard.Soft, HorizontalAlignment.Center);
        }
        else _page.Image("empty-plus", mark);
    }

    /// <summary>Icon and figure pairs under a card, sharing its width.</summary>
    private void Stats(Rect2 row, params (string Icon, string Value, string Hint)[] stats)
    {
        var pitch = row.Size.X / stats.Length;
        for (var i = 0; i < stats.Length; i++)
        {
            var label = RoyalText.Serif(stats[i].Value, 22, RoyalText.Cream, 700);
            var width = Mathf.Min(pitch - 30, Mathf.Ceil(label.TextWidth(label.FontSize)) + 2);
            var x = row.Position.X + i * pitch + (pitch - 26 - width) / 2;
            var icon = _page.Image(stats[i].Icon, new Rect2(x, row.Position.Y + 4, 22, 22));
            icon.TooltipText = stats[i].Hint; icon.MouseFilter = MouseFilterEnum.Pass;
            _page.Place(label, new Rect2(x + 26, row.Position.Y, width, row.Size.Y));
        }
    }
}
