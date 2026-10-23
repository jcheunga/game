using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Expeditions on a painted board: the three slots underway across the top (time left, or Collect), and the
/// three destinations below as painted cards with their duration, rewards, the allies who would go and Dispatch.
/// Allies outside the battle squad and not already away are sent, strongest first.
/// </summary>
public partial class ExpeditionMenu : RoyalBoard
{
    private readonly List<(int Slot, RoyalLabel Time, Rect2 Meter, Panel Fill)> _timers = new();
    private double _tick;

    public ExpeditionMenu() : base("Expeditions", "expeditions") { }

    private static Texture2D Scene(string id) => ResourceLoader.Exists($"res://assets/ui/royal/items/scene-expedition-{id}.png")
        ? RoyalArt.Item("scene-expedition-" + id) : RoyalArt.Item("activity-expeditions");

    protected override void Compose()
    {
        var state = GameState.Instance;
        Chip("icon-wheat", state.Food.ToString("N0"), $"{state.Food} rations", RewardArt("food"));
        Chip("coin-small", state.Gold.ToString("N0"), $"{state.Gold:N0} gold", RewardArt("gold"));
        _timers.Clear();
        var body = Board.Body; var footer = Board.Footer;
        var x = body.Position.X + 6; var width = body.Size.X - 12;
        var cardWidth = (width - 28) / 3;
        // The row captions sit beside the banner hanging over the board's corner.
        Caps($"Underway · {state.ActiveExpeditionCount}/{ExpeditionCatalog.MaxSlots}", new Rect2(Board.Notch.End.X + 6, body.Position.Y + 6, 420, 40), 18);
        var y = Board.Notch.End.Y - 2;
        var expeditions = state.GetActiveExpeditions();
        for (var i = 0; i < ExpeditionCatalog.MaxSlots; i++)
        {
            var rect = new Rect2(x + i * (cardWidth + 14), y, cardWidth, 120);
            if (i < expeditions.Count) Underway(rect, i, expeditions[i]);
            else FreeSlot(rect);
        }
        y += 132;
        var height = footer.Position.Y - 10 - y;
        var destinations = ExpeditionCatalog.GetAll();
        for (var i = 0; i < destinations.Count && i < 3; i++)
            Destination(new Rect2(x + i * (cardWidth + 14), y, cardWidth, height), destinations[i]);
        Back(new Rect2(footer.End.X - 170, footer.Position.Y, 170, footer.Size.Y));
        Action(new Rect2(footer.End.X - 360, footer.Position.Y, 176, footer.Size.Y), "Armory", "icon-armory", () => SceneRouter.Instance.GoToShop(), size: 17);
        NoticeAt(new Rect2(footer.Position.X, footer.Position.Y, footer.Size.X - 380, footer.Size.Y));
    }

    private void Underway(Rect2 rect, int slot, ExpeditionSlotState expedition)
    {
        var state = GameState.Instance;
        var definition = ExpeditionCatalog.Get(expedition.ExpeditionId);
        var complete = state.IsExpeditionComplete(slot);
        Frame(complete ? "ach-card-ready" : "ach-card", rect, 14);
        var picture = new Rect2(rect.Position + new Vector2(10, 10), new Vector2(118, rect.Size.Y - 20));
        Picture(picture, Scene(expedition.ExpeditionId));
        var left = picture.End.X + 12; var room = rect.End.X - left - 14;
        Serif(definition?.Title ?? Pretty(expedition.ExpeditionId), new Rect2(left, rect.Position.Y + 8, room, 32), 21, weight: 700);
        Allies(new Rect2(left, rect.Position.Y + 40, room, 36), expedition.AssignedUnitIds);
        var foot = new Rect2(left, rect.End.Y - 46, room, 36);
        if (complete)
        {
            Action(foot, "Collect", null, () =>
            {
                var collected = GameState.Instance.TryCollectExpedition(slot, out var message);
                AudioDirector.Claimed(collected);
                Notify(message);
                if (collected) Refresh();
            }, Kind.Claim, size: 16);
            return;
        }
        var total = TimeSpan.FromMinutes(definition?.DurationMinutes ?? 30);
        var meter = new Rect2(foot.Position.X, foot.Position.Y + 22, foot.Size.X, 12);
        Frame("ach-track", meter, 6);
        var fill = Frame("ach-fill-gold", new Rect2(meter.Position + new Vector2(3, 3), new Vector2(meter.Size.Y - 6, meter.Size.Y - 6)), 3);
        var time = Caps("", new Rect2(foot.Position.X, foot.Position.Y - 6, foot.Size.X, 26), 16, Soft);
        _timers.Add((slot, time, meter, fill));
        UpdateTimer(_timers.Count - 1, total);
    }

    private void FreeSlot(Rect2 rect)
    {
        Frame("season-locked", rect, 14);
        Image("empty-plus", new Rect2(rect.Position.X + 26, rect.GetCenter().Y - 24, 48, 48), new Color(1, 1, 1, .55f));
        Caps("Free slot", new Rect2(rect.Position.X + 92, rect.Position.Y + 30, rect.Size.X - 110, 30), 18, Soft);
        Serif("Choose a destination below", new Rect2(rect.Position.X + 92, rect.Position.Y + 62, rect.Size.X - 110, 30), 18, new Color("a99f8d"));
    }

    private void Destination(Rect2 rect, ExpeditionDefinition definition)
    {
        var state = GameState.Instance;
        Frame("ach-card", rect, 14);
        var picture = new Rect2(rect.Position + new Vector2(8, 8), new Vector2(rect.Size.X - 16, Mathf.Round(rect.Size.Y * .42f)));
        Picture(picture, Scene(definition.Id));
        Shade(new Rect2(picture.Position.X + 3, picture.End.Y - 52, picture.Size.X - 6, 49));
        Serif(definition.Title, new Rect2(picture.Position.X + 16, picture.End.Y - 40, picture.Size.X - 32, 32), 23, weight: 700);
        if (definition.RelicDropRarity != "common")
        {
            var tag = new Rect2(picture.End.X - 92, picture.Position.Y + 10, 80, 30);
            Frame("hub-badge", tag, 8);
            Caps(Pretty(definition.RelicDropRarity), tag, 16, new Color("c9b7ff"), HorizontalAlignment.Center);
        }

        var y = picture.End.Y + 10;
        var x = rect.Position.X + 18; var room = rect.Size.X - 36;
        var allies = definition.MinUnits == definition.MaxUnits ? $"{definition.MinUnits}" : $"{definition.MinUnits}–{definition.MaxUnits}";
        Image("emblem-hourglass", new Rect2(x - 2, y, 28, 28));
        Serif(definition.DurationMinutes >= 60 ? $"{definition.DurationMinutes / 60} h" : $"{definition.DurationMinutes} min", new Rect2(x + 30, y - 2, 110, 32), 20);
        Image("icon-people-gold", new Rect2(x + 118, y + 2, 30, 26));
        Serif($"{allies} allies", new Rect2(x + 154, y - 2, room - 154, 32), 20);
        y += 38;
        var rewards = new (Texture2D Art, string Amount, string Hint)[]
        {
            (RewardArt("gold"), $"~{definition.BaseGoldReward:N0}", $"About {definition.BaseGoldReward:N0} gold"),
            (RewardArt("food"), $"~{definition.BaseFoodReward}", $"About {definition.BaseFoodReward} rations"),
            (RoyalKit.Texture("forge-shard"), $"{(int)(definition.RelicDropChance * 100)}%", $"{(int)(definition.RelicDropChance * 100)}% chance of a {definition.RelicDropRarity} relic")
        };
        var step = room / rewards.Length;
        for (var i = 0; i < rewards.Length; i++)
        {
            var at = new Vector2(x + i * step, y);
            var holder = new Control { Position = at, Size = new Vector2(step, 40), TooltipText = rewards[i].Hint, MouseFilter = MouseFilterEnum.Pass };
            Live.AddChild(holder);
            Image(rewards[i].Art, new Rect2(at, new Vector2(40, 38)));
            Caps(rewards[i].Amount, new Rect2(at.X + 44, at.Y, step - 46, 38), 18, new Color("f6e2ad"));
        }

        var team = Team(definition);
        var free = ExpeditionCatalog.MaxSlots - state.ActiveExpeditionCount;
        var ready = free > 0 && team.Length >= definition.MinUnits;
        var foot = new Rect2(x, rect.End.Y - 58, room, 46);
        if (ready) Allies(new Rect2(foot.Position.X, foot.Position.Y, room - 150, foot.Size.Y), team);
        else
        {
            var needs = definition.MinUnits == 1 ? "1 ally" : $"{definition.MinUnits} allies";
            Paragraph(free <= 0 ? "All slots are in use." : $"Needs {needs} outside your squad.", new Rect2(foot.Position.X, foot.Position.Y - 4, room - 156, 52), 2, 18);
        }
        var id = definition.Id;
        Action(new Rect2(foot.End.X - 142, foot.Position.Y, 142, foot.Size.Y), "Dispatch", "icon-map-pin", () =>
        {
            var started = GameState.Instance.TryStartExpedition(id, Team(definition), out var message);
            Notify(message);
            if (started) Refresh();
        }, Kind.Gold, ready, ready ? $"Send {string.Join(", ", team.Select(Name))}" : "Not enough free allies", size: 16);
    }

    /// <summary>Owned allies who are neither in the battle squad nor away, strongest first, as many as the trip takes.</summary>
    private static string[] Team(ExpeditionDefinition definition)
    {
        var state = GameState.Instance;
        return state.GetOwnedPlayerUnitIds()
            .Where(id => !state.IsUnitInActiveDeck(id) && !state.IsUnitOnExpedition(id))
            .OrderByDescending(state.GetUnitLevel)
            .Take(definition.MaxUnits).ToArray();
    }

    private static string Name(string unitId)
    {
        try { return GameData.GetUnit(unitId)?.DisplayName ?? unitId; }
        catch { return unitId; }
    }

    private void Allies(Rect2 rect, IEnumerable<string> unitIds)
    {
        var ids = unitIds.ToArray();
        var holder = new Control { Position = rect.Position, Size = rect.Size, TooltipText = string.Join(", ", ids.Select(Name)), MouseFilter = MouseFilterEnum.Pass };
        Live.AddChild(holder);
        for (var i = 0; i < ids.Length; i++)
        {
            UnitDefinition unit;
            try { unit = GameData.GetUnit(ids[i]); } catch { unit = null; }
            var cell = new Rect2(rect.Position.X + i * (rect.Size.Y + 4), rect.Position.Y, rect.Size.Y, rect.Size.Y);
            Frame("relic-card", cell, 8);
            var figure = new UnitFigure { Position = cell.Position + new Vector2(2, 2), Size = cell.Size - new Vector2(4, 4) };
            figure.SetUnit(unit);
            Live.AddChild(figure);
        }
    }

    private void UpdateTimer(int index, TimeSpan total)
    {
        var (slot, time, meter, fill) = _timers[index];
        var remaining = GameState.Instance.GetExpeditionTimeRemaining(slot);
        time.Text = remaining.TotalHours >= 1 ? $"Returns in {(int)remaining.TotalHours} h {remaining.Minutes:00} min" : $"Returns in {remaining.Minutes} min {remaining.Seconds:00} s";
        var fraction = total.TotalSeconds <= 0 ? 1 : Mathf.Clamp(1 - (float)(remaining.TotalSeconds / total.TotalSeconds), 0, 1);
        fill.Visible = fraction > 0;
        fill.Size = new Vector2(Mathf.Max(meter.Size.Y - 6, (meter.Size.X - 6) * fraction), meter.Size.Y - 6);
    }

    public override void _Process(double delta)
    {
        // Countdowns tick once a second; a trip that returns rebuilds the board for its Collect button.
        _tick += delta;
        if (_tick < 1 || _timers.Count == 0) return;
        _tick = 0;
        var state = GameState.Instance;
        var expeditions = state.GetActiveExpeditions();
        for (var i = 0; i < _timers.Count; i++)
        {
            if (state.IsExpeditionComplete(_timers[i].Slot)) { Refresh(); return; }
            var definition = _timers[i].Slot < expeditions.Count ? ExpeditionCatalog.Get(expeditions[_timers[i].Slot].ExpeditionId) : null;
            UpdateTimer(i, TimeSpan.FromMinutes(definition?.DurationMinutes ?? 30));
        }
    }
}
