using System;
using System.Linq;
using Godot;

/// <summary>
/// The warband guild on a painted board: the guild hall with your guild's name, rank, members, experience and
/// weekly goal on the left (or the invitation to found or join one), and the five guild perks on the right.
/// Contribute, Leave, Create and Join are the footer actions.
/// </summary>
public partial class GuildMenu : RoyalBoard
{
    private const int ContributionGoldCost = 50;
    private const int ContributionPointsGain = 10;
    private const int ContributionGuildXpGain = 10;

    public GuildMenu() : base("Warband Guild", "guild") { }

    private static Texture2D PerkArt(string id) => id switch
    {
        "guild_prosperity" => RoyalArt.Item("store_gold_pouch"),
        "guild_provisions" => RoyalArt.Item("store_food_rations"),
        _ => RoyalArt.Item("perk-" + id)
    };

    protected override void Compose()
    {
        var state = GameState.Instance;
        var guild = state.CachedGuildInfo;
        var hasGuild = !string.IsNullOrWhiteSpace(state.GuildId) && guild != null;
        Chip("coin-small", state.Gold.ToString("N0"), $"{state.Gold:N0} gold", RewardArt("gold"));
        if (hasGuild) Chip("icon-people-gold", $"{guild.MemberCount}/{GuildCatalog.GetTier(guild.Tier).MaxMembers}", "Members");
        var body = Board.Body; var footer = Board.Footer;
        Caps(hasGuild ? "Your guild" : "No guild yet", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 300, 40), 18);
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 520, footer.Position.Y - 10 - Board.Notch.End.Y);
        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        if (hasGuild) Hall(left, guild, state); else Invitation(left);
        Perks(right, hasGuild ? guild : null);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        if (hasGuild)
        {
            Action(new Rect2(footer.End.X - 420, footer.Position.Y - 2, 420, footer.Size.Y + 4), "Contribute", "icon-swords-dark", OnContribute, Kind.Gold,
                state.Gold >= ContributionGoldCost, $"+{ContributionPointsGain} contribution · +{ContributionGuildXpGain} guild XP", ContributionGoldCost.ToString(), RewardArt("gold"), 20);
            NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y - 2, footer.Size.X - 618, footer.Size.Y + 4));
        }
        else
        {
            Action(new Rect2(footer.End.X - 300, footer.Position.Y - 2, 300, footer.Size.Y + 4), "Create guild", "icon-swords-dark", OnCreateGuild, Kind.Gold, size: 20);
            Action(new Rect2(footer.End.X - 520, footer.Position.Y, 206, footer.Size.Y), "Join guild", "icon-people-gold", OnJoinGuild, size: 17);
            NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y - 2, footer.Size.X - 718, footer.Size.Y + 4));
        }
    }

    private void Invitation(Rect2 column)
    {
        var picture = new Rect2(column.Position, column.Size);
        Picture(picture, RoyalArt.Item("activity-guild"));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 190, picture.Size.X - 6, 187));
        Heading("Raise your banner", new Rect2(picture.Position.X + 24, picture.End.Y - 168, picture.Size.X - 48, 46), 32);
        Paragraph("Create or join a guild to unlock shared perks. Contributions earn guild experience; every rank opens the next perk.",
            new Rect2(picture.Position.X + 24, picture.End.Y - 116, picture.Size.X - 48, 96), 3, 20, new Color("e6dcc8"));
    }

    private void Hall(Rect2 column, GuildSnapshot guild, GameState state)
    {
        var tier = GuildCatalog.GetTier(guild.Tier);
        var next = GuildCatalog.GetAllTiers().FirstOrDefault(t => t.Tier == guild.Tier + 1);
        var picture = new Rect2(column.Position, new Vector2(column.Size.X, 176));
        Picture(picture, RoyalArt.Item("activity-guild"));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 70, picture.Size.X - 6, 67));
        Heading(guild.Name, new Rect2(picture.Position.X + 20, picture.End.Y - 66, picture.Size.X - 40, 40), 30);
        Serif($"{tier.Title} · tier {guild.Tier}", new Rect2(picture.Position.X + 20, picture.End.Y - 32, picture.Size.X - 40, 28), 19, new Color("f6e2ad"));
        // Leaving is kept away from Contribute, on the hall itself.
        Action(new Rect2(picture.End.X - 160, picture.Position.Y + 12, 148, 40), "Leave guild", null, OnLeaveGuild, Kind.Slate, size: 15);

        var facts = new (string Icon, string Name, string Value)[]
        {
            ("icon-people-gold", "Members", $"{guild.MemberCount}/{tier.MaxMembers}"),
            ("result-star", "Experience", guild.Experience.ToString("N0")),
            ("emblem-guild", "Your share", state.GuildContributionPoints.ToString("N0"))
        };
        var y = picture.End.Y + 12;
        var width = (column.Size.X - 24) / 3;
        for (var i = 0; i < facts.Length; i++)
        {
            var tile = new Rect2(column.Position.X + i * (width + 12), y, width, 82);
            Frame("tile", tile, 12);
            Image(facts[i].Icon, new Rect2(tile.Position + new Vector2(12, 23), new Vector2(34, 34)));
            Caps(facts[i].Name, new Rect2(tile.Position.X + 52, tile.Position.Y + 10, tile.Size.X - 58, 28), 16, Soft);
            Serif(facts[i].Value, new Rect2(tile.Position.X + 52, tile.Position.Y + 38, tile.Size.X - 58, 34), 23, new Color("f6e2ad"), weight: 700);
        }
        y += 94;
        var goal = Section(new Rect2(column.Position.X, y, column.Size.X, column.End.Y - y), "Weekly goal", "emblem-hourglass",
            next == null ? "Highest rank" : $"{Math.Max(0, next.ExperienceRequired - guild.Experience):N0} XP to {next.Title}");
        if (guild.WeeklyGoalTarget > 0)
        {
            Serif(guild.WeeklyGoalType, new Rect2(goal.Position.X + 4, goal.Position.Y - 6, goal.Size.X - 120, 30), 20);
            Serif($"{guild.WeeklyGoalProgress:N0}/{guild.WeeklyGoalTarget:N0}", new Rect2(goal.Position.X, goal.Position.Y - 6, goal.Size.X - 4, 30), 20, new Color("f6e2ad"), HorizontalAlignment.Right, 700);
            Meter(new Rect2(goal.Position.X + 4, goal.Position.Y + 30, goal.Size.X - 8, 14), guild.WeeklyGoalProgress / (float)guild.WeeklyGoalTarget);
        }
        else Serif("No weekly goal this week.", new Rect2(goal.Position.X + 4, goal.Position.Y, goal.Size.X, 30), 19, Soft);
    }

    private void Perks(Rect2 rect, GuildSnapshot guild)
    {
        var room = Section(rect, "Guild perks", "emblem-crown", guild == null ? "Earned by rank" : null);
        var perks = GuildCatalog.GetAllPerks();
        var active = guild?.ActivePerkIds ?? Array.Empty<string>();
        var pitch = room.Size.Y / perks.Count;
        for (var i = 0; i < perks.Count; i++)
        {
            var perk = perks[i];
            var unlocked = guild != null && guild.Tier >= perk.TierRequired;
            var on = unlocked && active.Contains(perk.Id);
            var row = new Rect2(room.Position.X, room.Position.Y + i * pitch, room.Size.X, pitch - 8);
            Frame(on ? "ach-card-ready" : "season-locked", row, 10);
            Image(PerkArt(perk.Id), new Rect2(row.Position.X + 8, row.Position.Y + 4, row.Size.Y - 8, row.Size.Y - 8), unlocked ? null : new Color(.5f, .5f, .52f));
            var x = row.Position.X + row.Size.Y + 8;
            Serif(perk.Title, new Rect2(x, row.Position.Y + 4, row.Size.X - (x - row.Position.X) - 130, 30), 20, unlocked ? RoyalText.Cream : Soft, weight: 700);
            Serif(perk.Description, new Rect2(x, row.Position.Y + 32, row.Size.X - (x - row.Position.X) - 12, 28), 18, Soft, weight: 500);
            Caps(on ? "Active" : unlocked ? "Unlocked" : $"Tier {perk.TierRequired}", new Rect2(row.Position.X, row.Position.Y + 6, row.Size.X - 14, 26), 16,
                on ? Green : unlocked ? Engraved : new Color("a49a8a"), HorizontalAlignment.Right);
        }
    }

    private void OnCreateGuild()
    {
        var gs = GameState.Instance;
        if (!string.IsNullOrWhiteSpace(gs.GuildId)) { Notify("You are already in a guild."); return; }
        gs.CachedGuildInfo = new GuildSnapshot
        {
            GuildId = $"local_guild_{Time.GetTicksMsec()}", Name = "New Warband", LeaderProfileId = "player", Tier = 1, Experience = 0, MemberCount = 1,
            ActivePerkIds = new[] { "guild_vitality" }, WeeklyGoalProgress = 0, WeeklyGoalTarget = 100, WeeklyGoalType = "Contribute"
        };
        gs.SetGuildId(gs.CachedGuildInfo.GuildId);
        Notify("Guild created! Welcome to your new Warband.");
        Refresh();
    }

    private void OnJoinGuild()
    {
        var gs = GameState.Instance;
        if (!string.IsNullOrWhiteSpace(gs.GuildId)) { Notify("You are already in a guild."); return; }
        gs.CachedGuildInfo = new GuildSnapshot
        {
            GuildId = $"mock_guild_{Time.GetTicksMsec()}", Name = "Ironclad Company", LeaderProfileId = "npc_leader", Tier = 2, Experience = 600, MemberCount = 7,
            ActivePerkIds = new[] { "guild_vitality", "guild_prosperity" }, WeeklyGoalProgress = 42, WeeklyGoalTarget = 200, WeeklyGoalType = "Gold Earned"
        };
        gs.SetGuildId(gs.CachedGuildInfo.GuildId);
        Notify("Joined Ironclad Company!");
        Refresh();
    }

    private void OnContribute()
    {
        var gs = GameState.Instance;
        if (string.IsNullOrWhiteSpace(gs.GuildId) || gs.CachedGuildInfo == null) { Notify("You are not in a guild."); return; }
        if (!gs.TryGuildContribute(ContributionGoldCost, ContributionPointsGain, out var resultMsg)) { Notify(resultMsg); return; }
        var guild = gs.CachedGuildInfo;
        guild.Experience += ContributionGuildXpGain;
        guild.WeeklyGoalProgress += ContributionPointsGain;
        var tier = GuildCatalog.GetTierByExperience(guild.Experience);
        if (tier.Tier > guild.Tier)
        {
            guild.Tier = tier.Tier;
            Notify($"Contributed! Guild promoted to {tier.Title}!");
        }
        else Notify(resultMsg);
        Refresh();
    }

    private void OnLeaveGuild()
    {
        var gs = GameState.Instance;
        gs.CachedGuildInfo = null;
        gs.SetGuildId("");
        Notify("You left the guild.");
        Refresh();
    }
}
