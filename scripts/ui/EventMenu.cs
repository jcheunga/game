using System;
using Godot;

/// <summary>
/// The seasonal event on a painted board: the event's stages in order on the left (the next one with Deploy),
/// its milestones with Claim on the right. Between events the board shows the festival painting and what an
/// event brings.
/// </summary>
public partial class EventMenu : RoyalBoard
{
    /// <summary>Reviews show an event's board outside its dates.</summary>
    public static SeasonalEventDefinition Preview;

    public EventMenu() : base("Seasonal Event", "event") { }

    private static SeasonalEventDefinition Active => Preview ?? GameState.Instance.GetActiveEvent();

    protected override void Build()
    {
        if (Active is { } evt) Title = evt.Title;
        base.Build();
    }

    protected override void Compose()
    {
        var state = GameState.Instance;
        var evt = Active;
        Chip("result-star", state.Sigils.ToString("N0"), $"{state.Sigils} sigils", RewardArt("sigils"));
        var body = Board.Body; var footer = Board.Footer;
        var left = new Rect2(body.Position.X + 6, Board.Notch.End.Y - 2, 560, footer.Position.Y - 10 - Board.Notch.End.Y);
        var right = new Rect2(left.End.X + 16, body.Position.Y + 10, body.End.X - left.End.X - 22, footer.Position.Y - 12 - body.Position.Y);
        Back(new Rect2(footer.Position, new Vector2(170, footer.Size.Y)));
        NoticeAt(new Rect2(footer.Position.X + 184, footer.Position.Y, footer.Size.X - 184, footer.Size.Y));
        if (evt == null) { Quiet(left, right); return; }
        Caps(EndsIn(evt) ?? "Stages", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 420, 40), 18);
        Stages(left, evt);
        Milestones(right, evt);
    }

    private void Quiet(Rect2 left, Rect2 right)
    {
        Caps("Between festivals", new Rect2(Board.Notch.End.X + 6, Board.Body.Position.Y + 6, 420, 40), 18);
        Picture(left, RoyalArt.Item("activity-event"));
        Shade(new Rect2(left.Position.X + 3, left.End.Y - 170, left.Size.X - 6, 167));
        Heading("No event right now", new Rect2(left.Position.X + 24, left.End.Y - 150, left.Size.X - 48, 46), 32);
        Paragraph("Seasonal events bring limited stages and rewards. Check back soon.", new Rect2(left.Position.X + 24, left.End.Y - 98, left.Size.X - 48, 70), 2, 20, new Color("e6dcc8"));
        var room = Section(right, "What an event brings", "emblem-event");
        var features = new (Texture2D Art, string Name, string Text)[]
        {
            (RoyalKit.Texture("emblem-tower"), "Limited stages", "A run of festival battles on familiar roads, with their own modifiers."),
            (RewardArt("sigils"), "Event rewards", "Each stage cleared pays out, many in sigils."),
            (RoyalKit.Texture("emblem-crown"), "Milestones", "Clear enough stages to claim the festival's prizes.")
        };
        var pitch = room.Size.Y / features.Length;
        for (var i = 0; i < features.Length; i++)
        {
            var row = new Rect2(room.Position.X, room.Position.Y + i * pitch, room.Size.X, pitch - 10);
            Frame("season-locked", row, 10);
            Image(features[i].Art, new Rect2(row.Position.X + 12, row.Position.Y + 10, row.Size.Y - 20, row.Size.Y - 20));
            var x = row.Position.X + row.Size.Y + 6;
            Serif(features[i].Name, new Rect2(x, row.Position.Y + 10, row.End.X - x - 12, 32), 21, weight: 700);
            Paragraph(features[i].Text, new Rect2(x, row.Position.Y + 44, row.End.X - x - 14, row.Size.Y - 52), 2, 18);
        }
    }

    private void Stages(Rect2 rect, SeasonalEventDefinition evt)
    {
        var state = GameState.Instance;
        var progress = state.GetEventProgress(evt.Id);
        var room = Section(rect, "Stages", "emblem-tower", $"{progress}/{evt.Stages.Length} cleared");
        var pitch = Mathf.Min(84, room.Size.Y / Mathf.Max(1, evt.Stages.Length));
        for (var i = 0; i < evt.Stages.Length; i++)
        {
            var stage = evt.Stages[i];
            var cleared = i < progress;
            var current = i == progress;
            var data = GameData.GetStage(stage.BaseStageNumber);
            var row = new Rect2(room.Position.X, room.Position.Y + i * pitch, room.Size.X, pitch - 8);
            Frame(current ? "ach-card-ready" : "season-locked", row, 10);
            Image(RewardArt(stage.CompletionReward?.Type, stage.CompletionReward?.ItemId), new Rect2(row.Position.X + 8, row.Position.Y + 4, row.Size.Y - 8, row.Size.Y - 8),
                cleared ? new Color(1, 1, 1, .45f) : null);
            var x = row.Position.X + row.Size.Y + 6;
            Serif($"{i + 1}. {(data != null ? $"{data.MapName} · {data.StageName}" : $"Stage {stage.BaseStageNumber}")}", new Rect2(x, row.Position.Y + 3, row.End.X - x - 150, 30), 19,
                cleared ? Soft : RoyalText.Cream, weight: 700);
            // The reward is the picture on the left (named in its tooltip); the line below is the foe's strength.
            Serif($"Enemy health ×{stage.EnemyHealthScale:0.##} · damage ×{stage.EnemyDamageScale:0.##}", new Rect2(x, row.Position.Y + 30, row.End.X - x - 150, 28), 18, Soft, weight: 500);
            Live.AddChild(new Control { Position = row.Position, Size = new Vector2(row.Size.Y, row.Size.Y), TooltipText = Reward(stage.CompletionReward), MouseFilter = MouseFilterEnum.Pass });
            var button = new Rect2(row.End.X - 140, row.Position.Y + (row.Size.Y - 40) / 2, 128, 40);
            if (current)
            {
                var id = evt.Id; var index = i;
                Action(button, "Deploy", "icon-swords-dark", () => { GameState.Instance.PrepareEventBattle(id, index); SceneRouter.Instance.GoToLoadout(); }, Kind.Gold, size: 16);
            }
            else Caps(cleared ? "Cleared" : "Locked", button, 16, cleared ? Green : new Color("a49a8a"), HorizontalAlignment.Center);
        }
    }

    private void Milestones(Rect2 rect, SeasonalEventDefinition evt)
    {
        var state = GameState.Instance;
        var progress = state.GetEventProgress(evt.Id);
        var room = Section(rect, "Milestones", "emblem-crown");
        Meter(new Rect2(room.Position.X + 4, room.Position.Y + 2, room.Size.X - 8, 14), progress / (float)Math.Max(1, evt.Stages.Length));
        var top = room.Position.Y + 28;
        var pitch = Mathf.Min(84, (room.End.Y - top) / Mathf.Max(1, evt.Milestones.Length));
        for (var i = 0; i < evt.Milestones.Length; i++)
        {
            var milestone = evt.Milestones[i];
            var claimed = state.HasClaimedEventReward(evt.Id, i);
            var reachable = progress >= milestone.StagesRequired;
            var row = new Rect2(room.Position.X, top + i * pitch, room.Size.X, pitch - 8);
            Frame(reachable && !claimed ? "ach-card-ready" : "season-locked", row, 10);
            Image(RewardArt(milestone.Reward?.Type, milestone.Reward?.ItemId), new Rect2(row.Position.X + 8, row.Position.Y + 4, row.Size.Y - 8, row.Size.Y - 8), claimed ? new Color(1, 1, 1, .45f) : null);
            var x = row.Position.X + row.Size.Y + 6;
            Serif(milestone.Label, new Rect2(x, row.Position.Y + 4, row.End.X - x - 140, 30), 19, claimed ? Soft : RoyalText.Cream, weight: 700);
            Serif(Reward(milestone.Reward), new Rect2(x, row.Position.Y + 34, row.End.X - x - 140, 28), 18, Soft, weight: 500);
            var button = new Rect2(row.End.X - 124, row.Position.Y + (row.Size.Y - 40) / 2, 112, 40);
            var id = evt.Id; var index = i;
            if (claimed) Caps("Claimed", button, 16, Green, HorizontalAlignment.Center);
            else if (!reachable) Caps($"{progress}/{milestone.StagesRequired}", button, 17, Soft, HorizontalAlignment.Center);
            else Action(button, "Claim", null, () =>
            {
                var got = GameState.Instance.TryClaimEventReward(id, index, out var message);
                AudioDirector.Claimed(got);
                Notify(message);
                if (got) Refresh();
            }, Kind.Claim, size: 16);
        }
    }

    private static string EndsIn(SeasonalEventDefinition evt)
    {
        if (!DateTime.TryParse(evt.EndDate, out var end)) return null;
        var days = (int)(end.Date - DateTime.UtcNow.Date).TotalDays;
        return days > 1 ? $"Ends in {days} days" : days == 1 ? "Ends tomorrow" : "Ends today";
    }

    private static string Reward(SeasonalEventReward reward)
    {
        if (reward == null) return "No reward";
        return reward.Type?.ToLowerInvariant() switch
        {
            "gold" => $"+{reward.Amount} gold",
            "food" => $"+{reward.Amount} food",
            "sigils" => $"+{reward.Amount} sigils",
            "shards" => $"+{reward.Amount} shards",
            "relic" => GameData.GetEquipment(reward.ItemId)?.DisplayName ?? reward.ItemId,
            _ => reward.Type ?? "Reward"
        };
    }
}
