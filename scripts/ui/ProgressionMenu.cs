using System;
using System.Linq;
using Godot;

/// <summary>
/// Caravan progression on a painted board, in three pages: Awakening (pick an ally, train tokens, awaken a
/// star and see its stats change), Collection (each collection's milestones with Claim) and Wagon looks
/// (every caravan look, its painting and how it is earned).
/// </summary>
public partial class ProgressionMenu : RoyalBoard
{
    private static readonly string[] Pages = { "Awakening", "Collection", "Wagon looks" };
    private const int AlliesPerPage = 12;
    private int _tab;
    private int _alliesPage = -1;
    private string _unitId = "";

    public ProgressionMenu() : base("Caravan Progression", "progression") { }

    protected override void Compose()
    {
        var state = GameState.Instance;
        Chip("result-star", state.Sigils.ToString("N0"), $"{state.Sigils} sigils", RewardArt("sigils"));
        Chip("icon-flame-gold", state.Essence.ToString("N0"), $"{state.Essence} essence", RewardArt("essence"));
        Chip("coin-small", state.Gold.ToString("N0"), $"{state.Gold:N0} gold", RewardArt("gold"));
        var body = Board.Body; var footer = Board.Footer;
        // The page tabs sit beside the banner hanging over the board's corner.
        var tabsLeft = Board.Notch.End.X + 6;
        for (var i = 0; i < Pages.Length; i++)
        {
            var index = i;
            var rect = new Rect2(tabsLeft + i * 226, body.Position.Y + 8, 214, 46);
            Tab(rect, Pages[i], i == _tab, () => { _tab = index; Refresh(); });
        }
        var page = new Rect2(body.Position.X + 6, Board.Notch.End.Y + 2, body.Size.X - 12, footer.Position.Y - 12 - Board.Notch.End.Y);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        if (_tab == 0) Awakening(page, footer);
        else if (_tab == 1) Collection(page);
        else Looks(page);
        NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y, _tab == 0 ? 360 : footer.Size.X - 184, footer.Size.Y));
    }

    // ---- Awakening --------------------------------------------------------------------------------------------

    private void Awakening(Rect2 page, Rect2 footer)
    {
        var state = GameState.Instance;
        var units = state.GetOwnedPlayerUnits().ToArray();
        if (units.All(unit => unit.Id != _unitId)) _unitId = units.First().Id;
        var pages = Math.Max(1, (units.Length + AlliesPerPage - 1) / AlliesPerPage);
        if (_alliesPage < 0) _alliesPage = Array.FindIndex(units, unit => unit.Id == _unitId) / AlliesPerPage;
        _alliesPage = Math.Clamp(_alliesPage, 0, pages - 1);

        var roster = Section(new Rect2(page.Position, new Vector2(430, page.Size.Y)), "Allies", "icon-people-gold", $"{units.Length} recruited");
        var grid = new Rect2(roster.Position.X, roster.Position.Y, roster.Size.X, roster.Size.Y - 44);
        var cell = new Vector2((grid.Size.X - 3 * 10) / 4, (grid.Size.Y - 2 * 10) / 3);
        for (var i = 0; i < AlliesPerPage; i++)
        {
            var at = _alliesPage * AlliesPerPage + i;
            if (at >= units.Length) break;
            var unit = units[at];
            var rect = new Rect2(grid.Position + new Vector2(i % 4 * (cell.X + 10), i / 4 * (cell.Y + 10)), cell);
            var id = unit.Id;
            var card = Card(rect, unit.DisplayName, () => { _unitId = id; Refresh(); }, id == _unitId, "relic-card", "cell-selected-empty");
            card.TooltipText = $"{unit.DisplayName} · {state.GetUnitStarLevel(id)} of {AwakeningCatalog.MaxStars} stars";
            var figure = new UnitFigure { Position = rect.Position + new Vector2(6, 6), Size = rect.Size - new Vector2(12, 26) };
            figure.SetUnit(unit);
            Live.AddChild(figure);
            Stars(new Vector2(rect.GetCenter().X - 5 * 8, rect.End.Y - 20), state.GetUnitStarLevel(id), AwakeningCatalog.MaxStars, 14, 16);
        }
        Pager(new Rect2(roster.Position.X, roster.End.Y - 38, roster.Size.X, 38), _alliesPage, pages, p => { _alliesPage = p; Refresh(); }, "allies");

        // The chosen ally.
        var preview = state.BuildAwakeningPreview(_unitId);
        var detail = new Rect2(page.Position.X + 444, page.Position.Y, page.Size.X - 444, page.Size.Y);
        var portrait = new Rect2(detail.Position, new Vector2(186, detail.Size.Y));
        Frame("ach-card", portrait, 14);
        var big = new UnitFigure { Position = portrait.Position + new Vector2(10, 16), Size = new Vector2(portrait.Size.X - 20, portrait.Size.Y - 104) };
        big.SetUnit(preview.Unit);
        Live.AddChild(big);
        Serif(preview.Unit.DisplayName, new Rect2(portrait.Position.X + 8, portrait.End.Y - 84, portrait.Size.X - 16, 34), 22, align: HorizontalAlignment.Center, weight: 700);
        Stars(new Vector2(portrait.GetCenter().X - 5 * 16, portrait.End.Y - 42), preview.Stars, AwakeningCatalog.MaxStars, 30, 32);

        var info = new Rect2(portrait.End.X + 16, detail.Position.Y, detail.End.X - portrait.End.X - 16, detail.Size.Y);
        var tokens = state.GetUnitTokens(_unitId);
        var next = preview.Next;
        var room = Section(new Rect2(info.Position, new Vector2(info.Size.X, 136)), "Tokens", "emblem-progression",
            next == null ? "Fully awakened" : $"{tokens} of {next.TokenCost} for star {next.Stars}");
        Meter(new Rect2(room.Position.X + 4, room.Position.Y + 2, room.Size.X - 8, 12), next == null ? 1 : Mathf.Min(1, tokens / (float)Math.Max(1, next.TokenCost)));
        Action(new Rect2(room.Position.X, room.Position.Y + 22, room.Size.X, 42), "Train token", null, () =>
        {
            GameState.Instance.TryTrainAwakeningToken(_unitId, out var message);
            Notify(message);
            Refresh();
        }, Kind.Navy, next != null && state.Gold >= GameState.AwakeningTokenGoldCost && state.Essence >= GameState.AwakeningTokenEssenceCost,
            $"One token for {GameState.AwakeningTokenGoldCost} gold and {GameState.AwakeningTokenEssenceCost} essence",
            $"{GameState.AwakeningTokenGoldCost} gold · {GameState.AwakeningTokenEssenceCost} essence", null, 16);

        var stats = Section(new Rect2(info.Position.X, info.Position.Y + 148, info.Size.X, info.Size.Y - 148), next == null ? "Awakened strength" : $"With star {next.Stars}", "stat-health");
        Comparison(stats, preview);

        Action(new Rect2(footer.End.X - 420, footer.Position.Y - 2, 420, footer.Size.Y + 4), next == null ? "Fully awakened" : "Awaken", "icon-swords-dark", () =>
        {
            GameState.Instance.TryAwakenUnit(_unitId, out var message);
            Notify(message);
            Refresh();
        }, Kind.Gold, next != null && tokens >= next.TokenCost && state.Gold >= next.GoldCost,
            next == null ? "Maximum awakening reached" : $"Awaken for {next.GoldCost:N0} gold and {next.TokenCost} tokens",
            next == null ? null : $"{next.GoldCost:N0}", next == null ? null : RewardArt("gold"), 20);
    }

    private void Comparison(Rect2 room, AwakeningPreview preview)
    {
        var before = preview.Current; var after = preview.After;
        var rows = new (string Name, float Now, float Then, string Format, string Suffix, bool LowerBetter)[]
        {
            ("Health", before.MaxHealth, after.MaxHealth, "0", "", false),
            ("Damage", before.AttackDamage, after.AttackDamage, "0.#", "", false),
            ("Armor", before.Armor, after.Armor, "0.#", "", false),
            ("Attack interval", before.AttackCooldown, after.AttackCooldown, "0.##", " s", true)
        };
        var pitch = Mathf.Min(34, (room.Size.Y - (preview.Limits.Count > 0 ? 52 : 0)) / rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            var (name, now, then, format, suffix, lower) = rows[i];
            var y = room.Position.Y + i * pitch;
            var changed = Math.Abs(then - now) > .0001f;
            Serif(name, new Rect2(room.Position.X + 4, y, 150, pitch), 19, Soft, weight: 500);
            Serif(now.ToString(format) + suffix, new Rect2(room.Position.X + 150, y, 76, pitch), 20, weight: 600);
            if (!changed) continue;
            Image("icon-chevron-right", new Rect2(room.Position.X + 228, y + pitch / 2 - 8, 10, 16), new Color(1, 1, 1, .6f));
            var better = lower ? then < now : then > now;
            Serif(then.ToString(format) + suffix, new Rect2(room.Position.X + 246, y, room.Size.X - 246, pitch), 20, better ? Green : new Color("e2ac91"), weight: 700);
        }
        // The stat limits explain a smaller rise than the star promises; the first one is shown.
        if (preview.Limits.Count > 0)
            Paragraph(RelicStatText.LimitNotice(preview.Limits[0]) + (preview.Limits.Count > 1 ? $" (+{preview.Limits.Count - 1} more)" : ""),
                new Rect2(room.Position.X + 4, room.End.Y - 50, room.Size.X - 8, 52), 2, 18);
    }

    // ---- Collection ---------------------------------------------------------------------------------------------

    private void Collection(Rect2 page)
    {
        var state = GameState.Instance;
        var groups = CollectionMilestoneCatalog.GetAll().GroupBy(m => m.Category).ToArray();
        const int columns = 3;
        var rows = (groups.Length + columns - 1) / columns;
        var size = new Vector2((page.Size.X - (columns - 1) * 12) / columns, (page.Size.Y - (rows - 1) * 12) / rows);
        for (var g = 0; g < groups.Length; g++)
        {
            var group = groups[g];
            var progress = CollectionMilestoneCatalog.GetCollectionPercent(group.Key, state);
            var rect = new Rect2(page.Position + new Vector2(g % columns * (size.X + 12), g / columns * (size.Y + 12)), size);
            Frame("ach-card", rect, 14);
            Image(CollectionGlyph(group.Key), new Rect2(rect.Position + new Vector2(16, 12), new Vector2(26, 26)));
            Caps(group.First().CategoryLabel, new Rect2(rect.Position.X + 50, rect.Position.Y + 10, rect.Size.X - 130, 30), 17);
            Caps($"{progress}%", new Rect2(rect.Position.X, rect.Position.Y + 10, rect.Size.X - 18, 30), 17, new Color("f6e2ad"), HorizontalAlignment.Right);
            Meter(new Rect2(rect.Position.X + 16, rect.Position.Y + 44, rect.Size.X - 32, 12), progress / 100f);
            var milestones = group.ToArray();
            var top = rect.Position.Y + 64;
            var pitch = (rect.End.Y - 12 - top) / milestones.Length;
            for (var i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                var claimed = state.HasClaimedCollectionMilestone(milestone.Id);
                var reached = progress >= milestone.ThresholdPercent;
                var row = new Rect2(rect.Position.X + 14, top + i * pitch, rect.Size.X - 28, pitch - 4);
                if (reached && !claimed) Frame("ach-card-ready", row, 8);
                Caps($"{milestone.ThresholdPercent}%", new Rect2(row.Position.X + 6, row.Position.Y, 56, row.Size.Y), 16, reached ? Engraved : Soft);
                Image(RewardArt(milestone.RewardType), new Rect2(row.Position.X + 62, row.Position.Y + 1, row.Size.Y - 2, row.Size.Y - 2), claimed ? new Color(1, 1, 1, .45f) : null);
                Serif(milestone.RewardLabel, new Rect2(row.Position.X + 64 + row.Size.Y, row.Position.Y, row.Size.X - 172 - row.Size.Y, row.Size.Y), 19,
                    claimed ? Soft : new Color("f6e2ad"), weight: 700);
                var id = milestone.Id;
                var button = new Rect2(row.End.X - 100, row.Position.Y + 1, 98, row.Size.Y - 2);
                if (claimed) Image("forge-check", new Rect2(row.End.X - 34, row.Position.Y + (row.Size.Y - 28) / 2, 27, 28));
                else Action(button, "Claim", null, () =>
                {
                    var got = GameState.Instance.TryClaimCollectionMilestone(id, out var message);
                    AudioDirector.Claimed(got);
                    Notify(message);
                    Refresh();
                }, reached ? Kind.Claim : Kind.Slate, reached, reached ? $"Claim {milestone.RewardLabel}" : $"Reach {milestone.ThresholdPercent}% to claim", size: 16, name: $"Claim {id}");
            }
        }
    }

    private static string CollectionGlyph(string category) => category switch
    {
        "codex" => "icon-open-book", "relics" => "relic-title-icon", "units" => "icon-people-gold", "spells" => "icon-flame-gold",
        "tower" => "hubicon-tower", _ => "result-star"
    };

    // ---- Wagon looks --------------------------------------------------------------------------------------------

    private void Looks(Rect2 page)
    {
        var state = GameState.Instance;
        var skins = WagonSkinCatalog.GetAll();
        var columns = 4;
        var rows = (skins.Count + columns - 1) / columns;
        var size = new Vector2((page.Size.X - (columns - 1) * 12) / columns, (page.Size.Y - (rows - 1) * 12) / rows);
        for (var i = 0; i < skins.Count; i++)
        {
            var skin = skins[i];
            var unlocked = WagonSkinCatalog.IsSkinUnlocked(skin.Id, state);
            var selected = state.SelectedWagonSkinId == skin.Id;
            var rect = new Rect2(page.Position + new Vector2(i % columns * (size.X + 12), i / columns * (size.Y + 12)), size);
            Frame(selected ? "ach-card-ready" : "ach-card", rect, 14);
            var art = BattleStructureArt.Load(skin.Id == WagonSkinCatalog.DefaultSkinId ? "war_wagon" : "war_wagon_" + skin.Id) ?? BattleStructureArt.Load("war_wagon");
            Image(Trimmed(art?.Texture), new Rect2(rect.Position + new Vector2(14, 8), new Vector2(rect.Size.X - 28, rect.Size.Y - 104)), unlocked ? null : new Color(.55f, .55f, .58f, .95f));
            if (!unlocked) Image("icon-lock-small", new Rect2(rect.End.X - 40, rect.Position.Y + 14, 24, 28));
            Serif(skin.Title, new Rect2(rect.Position.X + 14, rect.End.Y - 98, rect.Size.X - 28, 30), 21, unlocked ? RoyalText.Cream : Soft, weight: 700);
            Serif(unlocked ? "Unlocked" : skin.UnlockCondition, new Rect2(rect.Position.X + 14, rect.End.Y - 70, rect.Size.X - 28, 26), 18, unlocked ? Green : Soft, weight: 500);
            var id = skin.Id;
            Action(new Rect2(rect.Position.X + 12, rect.End.Y - 42, rect.Size.X - 24, 32), selected ? "In use" : unlocked ? "Use look" : "Locked", null,
                () => { GameState.Instance.SetWagonSkin(id); Refresh(); }, unlocked && !selected ? Kind.Gold : Kind.Slate, unlocked && !selected,
                $"{skin.Title} · {skin.Description}", size: 16, name: $"Use wagon {id}");
        }
    }

    private static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> TrimCache = new();

    /// <summary>The wagon plates carry room around the wagon for its battle anchor; cards show only the painting.</summary>
    private static Texture2D Trimmed(Texture2D texture)
    {
        if (texture == null) return null;
        if (TrimCache.TryGetValue(texture, out var trimmed)) return trimmed;
        var image = texture.GetImage();
        if (image == null) return TrimCache[texture] = texture;
        if (image.IsCompressed()) image.Decompress();
        var used = image.GetUsedRect();
        var scale = texture.GetSize() / image.GetSize();
        return TrimCache[texture] = used.Size.X <= 0 ? texture : new AtlasTexture { Atlas = texture, Region = new Rect2(used.Position * scale, used.Size * scale) };
    }

    private void Stars(Vector2 at, int earned, int total, float size, float pitch)
    {
        for (var i = 0; i < total; i++)
            Image("result-star", new Rect2(at + new Vector2(i * pitch, 0), new Vector2(size, size)), i < earned ? null : new Color(.22f, .2f, .18f, .9f));
    }
}
