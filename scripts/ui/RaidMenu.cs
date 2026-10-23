using Godot;

/// <summary>
/// The weekly raid on a painted board: this week's boss in its lair with the raid's progress and your damage on
/// the left, and the damage milestones with their rewards and Claim on the right.
/// </summary>
public partial class RaidMenu : RoyalBoard
{
    private string _weekId = "";
    private RaidBossDefinition _boss = null!;

    public RaidMenu() : base("Weekly Raid", "raid") { }

    protected override void Build()
    {
        _weekId = RaidBossCatalog.GetCurrentWeekId();
        _boss = RaidBossCatalog.GetCurrentBoss();
        base.Build();
    }

    protected override void Compose()
    {
        var state = GameState.Instance;
        // In offline mode the community damage is just the player's own contribution.
        var damage = (long)state.RaidDamageContributed;
        Chip("emblem-raid", _weekId, "Raid week");
        var body = Board.Body; var footer = Board.Footer;
        Caps("This week's boss", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 320, 40), 18);
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 530, footer.Position.Y - 10 - Board.Notch.End.Y);
        Lair(new Rect2(left.Position, new Vector2(left.Size.X, 222)));
        var progress = Section(new Rect2(left.Position.X, left.Position.Y + 234, left.Size.X, left.Size.Y - 234), "Raid progress", "emblem-crown",
            $"{(_boss.TotalHealthPool > 0 ? damage * 100 / _boss.TotalHealthPool : 0)}%");
        Meter(new Rect2(progress.Position.X + 4, progress.Position.Y + 4, progress.Size.X - 8, 16), _boss.TotalHealthPool > 0 ? damage / (float)_boss.TotalHealthPool : 0);
        Caps("Health pool", new Rect2(progress.Position.X + 4, progress.Position.Y + 32, 240, 28), 16, Soft);
        Serif(_boss.TotalHealthPool.ToString("N0"), new Rect2(progress.Position.X + 4, progress.Position.Y + 58, 240, 32), 22, new Color("f6e2ad"), weight: 700);
        Caps("Your damage", new Rect2(progress.Position.X + 260, progress.Position.Y + 32, progress.Size.X - 264, 28), 16, Soft);
        Serif(state.RaidDamageContributed.ToString("N0"), new Rect2(progress.Position.X + 260, progress.Position.Y + 58, progress.Size.X - 264, 32), 22, new Color("f6e2ad"), weight: 700);

        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        Milestones(right, damage);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y, footer.Size.X - 184, footer.Size.Y));
    }

    private void Lair(Rect2 rect)
    {
        // Each raid boss has its own painting; the boss stands on the right, its story on the dark left.
        var painting = ResourceLoader.Exists($"res://assets/ui/royal/items/scene-{_boss.Id}.png") ? RoyalArt.Item("scene-" + _boss.Id) : RoyalArt.Item("activity-weeklyraid");
        Picture(rect, painting);
        Shade(new Rect2(rect.Position.X + 3, rect.Position.Y + 70, rect.Size.X - 6, rect.Size.Y - 73));
        var entry = CodexCatalog.GetById(_boss.BossUnitId);
        var lore = entry?.LoreText;
        Heading(_boss.BossName, new Rect2(rect.Position.X + 20, rect.End.Y - (string.IsNullOrWhiteSpace(lore) ? 62 : 104), rect.Size.X - 40, 44), 30);
        if (!string.IsNullOrWhiteSpace(lore))
            Paragraph(lore, new Rect2(rect.Position.X + 20, rect.End.Y - 60, rect.Size.X * .62f, 52), 2, 18, new Color("e6dcc8"));
    }

    private void Milestones(Rect2 rect, long damage)
    {
        var state = GameState.Instance;
        var room = Section(rect, "Milestones", "emblem-rankings");
        var milestones = _boss.Milestones;
        var pitch = Mathf.Min(86, room.Size.Y / Mathf.Max(1, milestones.Length));
        for (var i = 0; i < milestones.Length; i++)
        {
            var milestone = milestones[i];
            var reached = damage >= milestone.DamageThreshold;
            var claimed = state.HasClaimedRaidReward(_weekId, i);
            var row = new Rect2(room.Position.X, room.Position.Y + i * pitch, room.Size.X, pitch - 8);
            Frame(reached && !claimed ? "ach-card-ready" : "season-locked", row, 10);
            Image(RewardArt(milestone.RewardType, milestone.RewardItemId), new Rect2(row.Position.X + 8, row.Position.Y + 4, row.Size.Y - 8, row.Size.Y - 8), claimed ? new Color(1, 1, 1, .45f) : null);
            var x = row.Position.X + row.Size.Y + 6;
            Serif(milestone.Label, new Rect2(x, row.Position.Y + 4, row.End.X - x - 150, 30), 20, claimed ? Soft : RoyalText.Cream, weight: 700);
            Serif($"{milestone.DamageThreshold:N0} damage", new Rect2(x, row.Position.Y + 34, row.End.X - x - 150, 28), 18, Soft, weight: 500);
            var index = i;
            var button = new Rect2(row.End.X - 140, row.Position.Y + (row.Size.Y - 40) / 2, 128, 40);
            if (claimed) Action(button, "Claimed", null, null, Kind.Claimed, false, size: 15);
            else Action(button, reached ? "Claim" : "Locked", null, () =>
            {
                var got = GameState.Instance.TryClaimRaidReward(_weekId, index, out var message);
                AudioDirector.Claimed(got);
                Notify(message);
                if (got) Refresh();
            }, reached ? Kind.Claim : Kind.Slate, reached, reached ? $"Claim {Reward(milestone)}" : $"Deal {milestone.DamageThreshold:N0} damage to unlock", size: 16);
        }
    }

    private static string Reward(RaidBossMilestone milestone) => milestone.RewardType.ToLowerInvariant() switch
    {
        "gold" => $"{milestone.RewardAmount:N0} gold",
        "essence" => $"{milestone.RewardAmount} essence",
        "relic" => GameData.GetEquipment(milestone.RewardItemId)?.DisplayName ?? milestone.RewardItemId,
        "spell" => GameData.GetSpell(milestone.RewardItemId)?.DisplayName ?? milestone.RewardItemId,
        "unit" => GameData.GetUnit(milestone.RewardItemId)?.DisplayName ?? milestone.RewardItemId,
        _ => $"{milestone.RewardAmount} {milestone.RewardType}"
    };
}
