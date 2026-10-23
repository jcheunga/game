using System;
using Godot;

/// <summary>
/// Today's bounties on a painted board, as three wanted posters: the objective's picture, its progress, the
/// reward and Claim. The board resets at midnight UTC.
/// </summary>
public partial class BountyMenu : RoyalBoard
{
    private static readonly Color Parchment = new("2a1608");
    private static readonly Color Faded = new("5a4330");

    public BountyMenu() : base("Bounty Board", "bounties") { }

    protected override void Compose()
    {
        var left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
        Chip("emblem-hourglass", left.TotalHours >= 1 ? $"Resets in {(int)left.TotalHours} h" : $"Resets in {left.Minutes} min", "New bounties are posted at midnight UTC");
        var body = Board.Body; var footer = Board.Footer;
        var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
        // The posters start beside the banner hanging over the board's corner.
        var row = new Rect2(Board.Notch.End.X + 6, body.Position.Y + 10, body.End.X - Board.Notch.End.X - 12, footer.Position.Y - 12 - body.Position.Y);
        var width = (row.Size.X - 2 * 20) / 3;
        for (var i = 0; i < BountyBoardCatalog.DailyBountyCount; i++)
        {
            var rect = new Rect2(row.Position.X + i * (width + 20), row.Position.Y, width, row.Size.Y);
            if (i < bounties.Length) Poster(rect, bounties[i]);
            else Empty(rect);
        }
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        Action(new Rect2(footer.Position.X + 184, footer.Position.Y, 176, footer.Size.Y), "Armory", "icon-armory", () => SceneRouter.Instance.GoToShop(), size: 17);
        NoticeAt(new Rect2(footer.Position.X + 376, footer.Position.Y, footer.Size.X - 376, footer.Size.Y));
    }

    private void Poster(Rect2 rect, BountyDefinition bounty)
    {
        var state = GameState.Instance;
        var progress = Math.Min(state.GetBountyProgress(bounty.Id), bounty.TargetCount);
        var completed = state.IsBountyCompleted(bounty.Id);
        var ready = progress >= bounty.TargetCount && !completed;
        Frame("paper-clean", rect, 28);
        var x = rect.Position.X + 24; var width = rect.Size.X - 48;
        Caps(completed ? "Claimed" : ready ? "Reward ready" : "Wanted", new Rect2(x, rect.Position.Y + 18, width, 26), 16, completed ? new Color("3f6a34") : new Color("7a1f14"), HorizontalAlignment.Center);
        var art = ResourceLoader.Exists($"res://assets/ui/royal/items/bounty-{bounty.TrackingType}.png") ? RoyalArt.Item("bounty-" + bounty.TrackingType) : RoyalArt.Item("activity-bounties");
        Image(art, new Rect2(rect.GetCenter().X - 78, rect.Position.Y + 48, 156, 150), completed ? new Color(1, 1, 1, .55f) : null);
        Caps(bounty.Title, new Rect2(x, rect.Position.Y + 204, width, 34), 21, Parchment, HorizontalAlignment.Center).ShadowOffset = Vector2.Zero;
        Paragraph(bounty.Description, new Rect2(x, rect.Position.Y + 240, width, 52), 2, 19, Faded, HorizontalAlignment.Center)
            .AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        var track = new Rect2(x + 8, rect.Position.Y + 304, width - 16, 14);
        Meter(track, progress / (float)Math.Max(1, bounty.TargetCount));
        Caps($"{progress:N0}/{bounty.TargetCount:N0}", new Rect2(x, track.End.Y + 2, width, 26), 16, Parchment, HorizontalAlignment.Center).ShadowOffset = Vector2.Zero;
        var reward = $"+{bounty.RewardAmount:N0} {bounty.RewardType.ToLowerInvariant()}";
        var rewardLabel = RoyalText.Serif(reward, 21, Parchment, 700);
        rewardLabel.ShadowOffset = Vector2.Zero;
        var group = 44 + rewardLabel.TextWidth(rewardLabel.FontSize);
        var left = rect.GetCenter().X - group / 2;
        Image(RewardArt(bounty.RewardType), new Rect2(left, rect.End.Y - 118, 38, 36));
        Place(rewardLabel, new Rect2(left + 44, rect.End.Y - 120, group - 40, 40));
        var id = bounty.Id;
        Action(new Rect2(x, rect.End.Y - 72, width, 48), completed ? "Claimed" : "Claim", null, () =>
        {
            var claimed = GameState.Instance.TryClaimBounty(id, out var message);
            AudioDirector.Claimed(claimed);
            Notify(message);
            if (claimed) Refresh();
        }, completed ? Kind.Claimed : ready ? Kind.Gold : Kind.Slate, ready,
            completed ? "Already claimed today" : ready ? $"Claim {reward}" : $"{bounty.TargetCount - progress:N0} to go", size: 18, name: $"Claim {bounty.Title}");
    }

    private void Empty(Rect2 rect)
    {
        Frame("paper-clean", rect, 28);
        Caps("No bounty", new Rect2(rect.Position.X, rect.GetCenter().Y - 20, rect.Size.X, 40), 20, Faded, HorizontalAlignment.Center).ShadowOffset = Vector2.Zero;
    }
}
