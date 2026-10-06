using System.Collections.Generic;
using Godot;

// Troops leave the caravan through the ramp door in its hold: the door drops, each unit steps out of the
// lit doorway and walks down the ramp, and joins the fight where the ramp meets the battle line.
public partial class BattleController
{
    private const float WagonDoorSwingSeconds = .14f;
    private const float WagonDoorHoldSeconds = .7f;
    private const float WagonExitHiddenSeconds = .12f;   // the door swings down before the unit appears
    private const float WagonExitSeconds = .45f;
    private readonly Dictionary<Unit, float> _wagonExits = new();
    private float _wagonDoorAmount, _wagonDoorHold;
    private bool _wagonDoorOpening;

    // The wagon stands so the foot of its troop ramp lands on the centre line.
    private Vector2 WagonGround => WagonArt is { Door: { } door } art
        ? PlayerBaseCorePosition + new Vector2(0, (art.Anchor.Y - door.Foot.Y) * art.Size.Y) : PlayerBaseCorePosition;
    // Every unit marches out from the foot of the ramp, on the wagon's centre line.
    private Vector2 CaravanDeployPosition => WagonArt is { Door: { } door } art
        ? art.Point(WagonGround, door.Foot) : new Vector2(PlayerSpawnX, BaseCenterY);
    private Vector2 WagonDoorExit => WagonArt is { Door: { } door } art ? art.Point(WagonGround, door.Exit) : CaravanDeployPosition;
    // Units in the doorway must draw over the wagon, so the wagon sorts just behind it.
    private Vector2 WagonSortPosition => new(WagonGround.X, Mathf.Min(WagonGround.Y, WagonDoorExit.Y - 2));
    private int WagonDoorFrame => WagonArt?.Door is { } door ? Mathf.RoundToInt(_wagonDoorAmount * door.Frames) - 1 : -1;

    private void BeginWagonExit(Unit unit)
    {
        _wagonDoorHold = WagonDoorHoldSeconds;
        if (WagonArt?.Door == null) return;
        _wagonExits[unit] = 0f;
        unit.Position = WagonDoorExit;
        unit.Visible = false;
    }

    // Moves a unit along its exit from the doorway to the ramp foot. Returns false once it is free to fight.
    private bool TickWagonExit(Unit unit, float delta)
    {
        if (!_wagonExits.TryGetValue(unit, out var elapsed)) return false;
        elapsed += delta;
        var t = Mathf.Clamp((elapsed - WagonExitHiddenSeconds) / (WagonExitSeconds - WagonExitHiddenSeconds), 0f, 1f);
        unit.Visible = elapsed >= WagonExitHiddenSeconds;
        unit.Modulate = new Color(1, 1, 1, Mathf.Clamp(t * 3f, 0f, 1f));
        unit.Position = WagonDoorExit.Lerp(CaravanDeployPosition, t);
        if (t < 1f)
        {
            _wagonExits[unit] = elapsed;
            return true;
        }
        _wagonExits.Remove(unit);
        unit.Modulate = Colors.White;
        return false;
    }

    private void UpdateWagonDoor(float delta)
    {
        if (_wagonExits.Count > 0)
        {
            var gone = new List<Unit>();
            foreach (var unit in _wagonExits.Keys)
                if (!IsInstanceValid(unit) || unit.IsDead) gone.Add(unit);
            foreach (var unit in gone) _wagonExits.Remove(unit);
        }
        _wagonDoorHold = Mathf.Max(0f, _wagonDoorHold - delta);
        var open = _wagonDoorHold > 0f || _wagonExits.Count > 0;
        // The door creaks open when it starts to swing out and thuds shut as it closes.
        if (open != _wagonDoorOpening && WagonArt?.Door != null)
        {
            _wagonDoorOpening = open;
            AudioDirector.Instance?.PlayWagonDoor(open, WagonDoorExit);
        }
        _wagonDoorAmount = Mathf.MoveToward(_wagonDoorAmount, open ? 1f : 0f, delta / WagonDoorSwingSeconds);
    }
}
