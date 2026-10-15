using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The level track on the season board's plate. The band shows the nickname (tap to rename), the level
/// and XP toward the next. Below it a scrolling grid holds all hundred levels, five to a row, each with
/// the reward it pays and badges for the caps it lifts. The rail's five badges jump to levels 1, 21, 41,
/// 61 and 81. Text keeps the phone reading sizes (capitals at 16 canvas px).
/// </summary>
public partial class PlayerLevelMenu : RoyalScreen
{
    private const int Columns = 5;
    // Three rows exactly fill the view, so no sliver of a fourth row shows under them.
    private const float Gap = 10, CellHeight = 102;
    private static readonly Rect2 View = new(62, 290, 1144, 332);
    private static readonly Vector2 Cell = new((View.Size.X - 4 - Gap * (Columns - 1)) / Columns, CellHeight);
    private static float RowPitch => CellHeight + Gap;
    private static int Rows => (PlayerLevelCatalog.MaxLevel + Columns - 1) / Columns;
    private const int SectionRows = 4;

    private Control _layer;
    private ScrollContainer _scroll;
    private float _scrollValue = -1;
    private readonly List<RoyalLabel> _sections = new();

    public PlayerLevelMenu() { PlateName = "season"; Name = "PlayerLevelMenu"; }

    private static RoyalSpec Spec => RoyalSpec.For("season");

    protected override void Build()
    {
        _layer = Layer("Live");
        Refresh();
    }

    private void Refresh()
    {
        RoyalUiTools.Clear(_layer);
        _sections.Clear();
        var spec = Spec;
        var state = GameState.Instance;
        var level = state.PlayerLevel;
        _layer.AddChild(spec.Label("title", "Level Rewards", 600));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
        BuildBand(spec, state);
        BuildGrid(state);
        BuildRail(spec);

        // The caps the current level allows and the next level that lifts one.
        var caps = RoyalText.Caps($"UNITS LV {state.UnitLevelCap} · WAGON {state.WagonRankCap}", 16, new Color("e9dcc0"), 650);
        RoyalText.Place(_layer, caps, 392, 632, 250, 24);
        var next = Enumerable.Range(level + 1, Math.Max(0, PlayerLevelCatalog.MaxLevel - level))
            .Select(at => (at, unlocks: PlayerLevelCatalog.Unlocks(at))).FirstOrDefault(entry => entry.unlocks.Count > 0);
        var nextText = RoyalText.Serif(next.unlocks == null ? "Every cap is open" : $"Next cap at level {next.at}", 18, new Color("b9ad96"), 500);
        nextText.TooltipText = next.unlocks == null ? "" : string.Join(" · ", next.unlocks.Select(u => u.Label));
        RoyalText.Place(_layer, nextText, 392, 656, 250, 26);

        var map = spec.Rect("button.map");
        _layer.AddChild(MapNavigation.Fixed(map, Close));
        var ready = state.ClaimableLevelRewardCount;
        if (ready > 1)
        {
            var rect = new Rect2(map.Position.X - map.Size.X - 16, map.Position.Y + 4, map.Size.X, map.Size.Y - 8);
            var all = RoyalButton.Over(rect, $"Claim all {ready} rewards", () =>
            {
                AudioDirector.Claimed(GameState.Instance.ClaimAllLevelRewards() > 0);
                Refresh();
            }, 6);
            all.Name = "ClaimAllLevelRewards";
            all.SetStates(RoyalKit.Slice("season-claim", 12), 6);
            var text = RoyalText.Caps($"CLAIM ALL · {ready}", 18, new Color("1a0d04"), 700);
            text.Align = HorizontalAlignment.Center; text.ShadowOffset = Vector2.Zero;
            all.SetCaption(text, new Rect2(0, 0, rect.Size.X, rect.Size.Y));
            _layer.AddChild(all);
        }
    }

    private void BuildBand(RoyalSpec spec, GameState state)
    {
        var ink = new Color("080605");
        var name = spec.Label("band.season", state.PlayerCallsign, 170, ink);
        name.ShadowOffset = Vector2.Zero;
        _layer.AddChild(name);
        var nameWidth = Mathf.Min(name.TextWidth(name.FittedSize()), 170);
        var rename = RoyalButton.Over(new Rect2(name.Position.X - 8, 156, nameWidth + 46, 58), "Change nickname", () => NicknameDialog.Show(this, false, Refresh), 6);
        rename.Name = "ChangeNickname";
        rename.SetGlyph(RoyalKit.Texture("icon-quill"), new Rect2(nameWidth + 16, 18, 20, 22), new Color("3a2c1c"));
        _layer.AddChild(rename);
        var levelLabel = spec.Label("band.tier", $"Level {state.PlayerLevel}", 122, ink);
        levelLabel.ShadowOffset = Vector2.Zero;
        _layer.AddChild(levelLabel);
        var progress = state.IsMaxPlayerLevel ? 1f : state.PlayerXPIntoLevel / (float)Math.Max(1, state.PlayerXPForNextLevel);
        var fill = spec.Rect("band.xp.fill");
        var track = spec.Rect("band.xp.track");
        if (progress > 0)
            _layer.AddChild(new Panel { Position = fill.Position, Size = new Vector2(Mathf.Max(12, (track.Size.X - 10) * progress), fill.Size.Y), MouseFilter = MouseFilterEnum.Ignore }
                .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("season-xp-fill", 6, 4, 6, 4))));
        var xp = spec.Label("band.xp.label", state.IsMaxPlayerLevel ? "MAX LEVEL" : $"{state.PlayerXPIntoLevel:N0} / {state.PlayerXPForNextLevel:N0} XP", 130, new Color("0c0a07"));
        xp.ShadowOffset = Vector2.Zero; _layer.AddChild(xp);
    }

    private void BuildGrid(GameState state)
    {
        _scroll = new ScrollContainer { Name = "LevelGrid", Position = View.Position, Size = View.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, ScrollDeadzone = 8 };
        _layer.AddChild(_scroll);
        var grid = new Control { CustomMinimumSize = new Vector2(View.Size.X - 2, Rows * RowPitch - Gap + 4), MouseFilter = MouseFilterEnum.Pass };
        _scroll.AddChild(grid);
        for (var level = 1; level <= PlayerLevelCatalog.MaxLevel; level++)
        {
            var at = new Vector2(2 + (level - 1) % Columns * (Cell.X + Gap), 2 + (level - 1) / Columns * RowPitch);
            grid.AddChild(LevelCell(state, level, new Rect2(at, Cell)));
        }
        _layer.AddChild(new RoyalScrollHint { Name = "LevelGridScrollbar", Position = new Vector2(View.End.X + 6, View.Position.Y + 4), Size = new Vector2(7, View.Size.Y - 8), Scroll = _scroll });
        if (_scrollValue < 0)
        {
            // Open on the earliest reward waiting to be claimed, otherwise on the current level's row.
            var focus = PlayerLevelCatalog.RewardLevels.Where(state.CanClaimLevelReward).DefaultIfEmpty(state.PlayerLevel).First();
            _scrollValue = Mathf.Max(0, (focus - 1) / Columns * RowPitch);
        }
        _scroll.GetVScrollBar().ValueChanged += value => { _scrollValue = (float)value; MarkSection(); };
        Callable.From(() => { if (GodotObject.IsInstanceValid(_scroll)) { _scroll.ScrollVertical = (int)_scrollValue; MarkSection(); } }).CallDeferred();
    }

    /// <summary>The rail's five badges jump to each block of twenty levels; the chevrons step between blocks.</summary>
    private void BuildRail(RoyalSpec spec)
    {
        for (var i = 0; i < 5; i++)
        {
            var section = i;
            var badge = spec.Rect($"tier.{7 + i}");
            var first = i * SectionRows * Columns + 1;
            var number = spec.Label($"tier.{7 + i}.label", first.ToString(), 60);
            _sections.Add(number);
            _layer.AddChild(number);
            var jump = RoyalButton.Over(badge.Grow(4), $"Levels {first}–{first + SectionRows * Columns - 1}", () => ScrollToSection(section), 6);
            _layer.AddChild(jump);
        }
        var rail = spec.Rect("rail");
        for (var side = -1; side <= 1; side += 2)
        {
            var direction = side;
            var rect = new Rect2(side < 0 ? rail.Position.X - 30 : rail.End.X - 2, rail.GetCenter().Y - 16, 30, 32);
            var arrow = RoyalButton.Over(rect, side < 0 ? "Earlier levels" : "Later levels", () => ScrollToSection(CurrentSection() + direction), 4);
            arrow.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(9, 6, 12, 20));
            _layer.AddChild(arrow);
        }
    }

    private int CurrentSection() => Math.Clamp(Mathf.RoundToInt(_scrollValue / RowPitch) / SectionRows, 0, 4);

    private void ScrollToSection(int section)
    {
        if (!GodotObject.IsInstanceValid(_scroll)) return;
        section = Math.Clamp(section, 0, 4);
        _scroll.ScrollVertical = (int)(section * SectionRows * RowPitch);
        _scrollValue = _scroll.ScrollVertical;
        MarkSection();
    }

    private void MarkSection()
    {
        var current = CurrentSection();
        for (var i = 0; i < _sections.Count; i++)
        {
            if (!GodotObject.IsInstanceValid(_sections[i])) continue;
            _sections[i].Gold = i == current;
            _sections[i].OutlineSize = i == current ? 2 : 0;
            if (i != current) _sections[i].Ink = new Color("cfc3a8");
        }
    }

    private static Texture2D RewardArt(PlayerLevelReward reward) => reward.Type switch
    {
        "gold" => RoyalKit.Texture(reward.Amount >= 500 ? "season-gold-large" : "season-gold"),
        "food" => RoyalKit.Texture("season-food"),
        "shards" => RoyalKit.Texture("reward-shards"),
        "essence" => HomeMapArt.Icon("flame"),
        _ => UiArtLoader.TryLoadRewardIcon(reward.Type)
    };

    /// <summary>One level: its art, number, the caps it lifts, its reward and whether that is claimed, ready or locked.</summary>
    private Control LevelCell(GameState state, int level, Rect2 rect)
    {
        var reward = PlayerLevelCatalog.Reward(level);
        var unlocks = PlayerLevelCatalog.Unlocks(level);
        var reached = level <= state.PlayerLevel;
        var claimed = state.HasClaimedLevelReward(level);
        var ready = state.CanClaimLevelReward(level);
        var current = level == state.PlayerLevel;
        var tip = $"Level {level} · {reward.Label}{(claimed ? " · claimed" : "")}";
        if (unlocks.Count > 0) tip += " · " + string.Join(" · ", unlocks.Select(u => u.Label.ToLowerInvariant()));
        var cell = new Panel { Name = $"Level{level}", Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass, TooltipText = tip };
        cell.AddThemeStyleboxOverride("panel", RoyalKit.Slice(ready ? "codex-cell-selected" : "codex-cell", 10));
        if (current || level % 10 == 0)
        {
            // The current level wears a gold rim; every tenth level a quieter bronze one.
            var rim = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = current ? new Color("f0c977") : new Color(.78f, .6f, .33f, .55f), AntiAliasing = true };
            rim.SetBorderWidthAll(current ? 2 : 1); rim.SetCornerRadiusAll(5);
            cell.AddChild(new Panel { Position = new Vector2(2, 2), Size = rect.Size - new Vector2(4, 4), MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", rim)));
        }
        var dim = !reached || claimed;
        cell.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = RewardArt(reward), Position = new Vector2(10, 12), Size = new Vector2(76, rect.Size.Y - 24), MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = dim ? new Color(.62f, .62f, .66f) : Colors.White, TextureFilter = TextureFilterEnum.LinearWithMipmaps });

        var left = 94f; var width = rect.Size.X - left - 10;
        var number = RoyalText.Caps($"LV {level}", 16, reached ? new Color("f0d48e") : new Color("a59b8a"), 700);
        RoyalText.Place(cell, number, left, 9, 64, 22);
        // The caps this level lifts, as the armory's warband (helmet) and war wagon (hammer) medallions.
        for (var i = 0; i < unlocks.Count; i++)
        {
            var unlock = unlocks[unlocks.Count - 1 - i];
            cell.AddChild(RoyalKit.Image(unlock.IsWagon ? "dock-upgrades" : "dock-warband", new Rect2(rect.Size.X - 34 - i * 27, 8, 26, 26),
                reached ? Colors.White : new Color(.7f, .7f, .72f)));
        }
        var caption = RoyalText.Caps(reward.Label.ToUpperInvariant(), 16, dim ? new Color("bdb4a6") : new Color("f4ecd8"), 650);
        RoyalText.Place(cell, caption, left, 36, width, 22);

        var foot = new Rect2(left, rect.Size.Y - 38, width, 28);
        if (ready)
        {
            var claim = RoyalButton.Over(foot, $"Claim {reward.Label}", () =>
            {
                if (AudioDirector.Claimed(GameState.Instance.TryClaimLevelReward(level, out _))) Refresh();
            }, 5);
            claim.SetStates(RoyalKit.Slice("season-claim", 9), 5);
            var text = RoyalText.Caps("CLAIM", 16, new Color("1a0d04"), 700);
            text.Align = HorizontalAlignment.Center; text.ShadowOffset = Vector2.Zero;
            claim.SetCaption(text, new Rect2(Vector2.Zero, foot.Size));
            cell.AddChild(claim);
            return cell;
        }
        var icon = claimed ? "icon-check-green" : "icon-lock-small";
        var size = claimed ? new Vector2(16, 15) : new Vector2(13, 17);
        cell.AddChild(RoyalKit.Image(icon, new Rect2(new Vector2(foot.Position.X + 2, foot.GetCenter().Y - size.Y / 2), size)));
        var status = RoyalText.Caps(claimed ? "CLAIMED" : "LOCKED", 16, claimed ? new Color("a4daa0") : new Color("9c968c"), 650);
        RoyalText.Place(cell, status, foot.Position.X + size.X + 10, foot.Position.Y, width - size.X - 10, foot.Size.Y);
        return cell;
    }
}
