using System.Collections.Generic;
using Godot;

public partial class BattleController
{
    private readonly RandomNumberGenerator _courageDropRng = new();
    private readonly List<CouragePickup> _couragePickups = new();
    private int _courageDropMisses;
    private CouragePickup _pressedCouragePickup;
    private bool _couragePickupPointerDown;
    private int _couragePickupPointerId;
    private int? _discardCouragePickupRelease;
    private Vector2 _couragePickupPointerStart;
    private bool CouragePickupActionAvailable => !_battleEnded && !_battlePaused && !_endlessCheckpointActive &&
        !_lanStartBarrierActive && !_onlineRoomStartBarrierActive;

    private void InitializeCourageDrops(ulong battleSeed)
    {
        // Cosmetic/combat RNG consumption cannot change the pickup sequence for a seeded challenge.
        _courageDropRng.Seed = battleSeed ^ 0x43524F574E524F41UL;
        _courageDropMisses = 0;
    }

    private void TryDropCourage(Unit defeated)
    {
        if (_battleEnded || defeated.Team != Team.Enemy || !defeated.IsDead || !defeated.CanDropCourage ||
            _combat.CourageDropChance <= 0f || _combat.CourageDropAmount <= 0f) return;
        defeated.CanDropCourage = false;
        var roll = _courageDropRng.Randf();
        var guaranteed = _combat.CourageDropGuaranteeAfterMisses > 0 &&
            _courageDropMisses >= _combat.CourageDropGuaranteeAfterMisses;
        if (!guaranteed && roll >= _combat.CourageDropChance)
        {
            _courageDropMisses++;
            return;
        }
        _courageDropMisses = 0; // A missed or expired pack does not award another guaranteed roll.
        SpawnCouragePickup(defeated.Position);
    }

    private CouragePickup SpawnCouragePickup(Vector2 position, float? amount = null)
    {
        var pickup = new CouragePickup { Name = "CouragePack" };
        pickup.Setup(position, amount ?? _combat.CourageDropAmount, _combat.CourageDropLifetime, _combat.CourageDropFadeDuration);
        AddChild(pickup);
        _couragePickups.Add(pickup);
        return pickup;
    }

    private void TickCouragePickups(float delta)
    {
        if (!CouragePickupActionAvailable) return;
        var target = _courageBar == null ? PlayerBaseCorePosition :
            ScreenToBattle(_courageBar.GetGlobalTransformWithCanvas() * (_courageBar.Size * .5f));
        for (var i = _couragePickups.Count - 1; i >= 0; i--)
        {
            var pickup = _couragePickups[i];
            pickup.Tick(delta, target, IsReducedMotionEnabled());
            if (!pickup.Finished) continue;
            _couragePickups.RemoveAt(i);
            pickup.QueueFree();
        }
    }

    private CouragePickup CouragePickupAt(Vector2 screen)
    {
        if (!GetViewportRect().HasPoint(screen) || screen.Y < FieldScreenTop || screen.Y > FieldScreenBottom ||
            (_battleUiRoot != null && CardHitControl(_battleUiRoot, screen) != null)) return null;
        CouragePickup closest = null;
        var best = float.MaxValue;
        foreach (var pickup in _couragePickups)
        {
            if (!pickup.Available) continue;
            var transform = pickup.GetGlobalTransformWithCanvas();
            var center = transform * new Vector2(0, -7);
            var radius = Mathf.Max(24f, (transform * new Vector2(12, 0) - transform.Origin).Length());
            var distance = screen.DistanceSquaredTo(center);
            if (distance <= radius * radius && distance < best)
            { closest = pickup; best = distance; }
        }
        return closest;
    }

    private bool CollectCouragePickup(CouragePickup pickup)
    {
        if (!CouragePickupActionAvailable || !IsInstanceValid(pickup) || !pickup.Available || !_couragePickups.Contains(pickup)) return false;
        var amount = Mathf.Min(pickup.Amount, Mathf.Max(0f, _maxCourage - _courage));
        if (amount <= .001f) return false; // Let the player spend first; its expiry keeps running.
        _courage += amount;
        pickup.Collect();
        AudioDirector.Instance?.PlayCoin();
        SpawnCombatNumber(pickup.Position + new Vector2(0, -23), $"+{Mathf.RoundToInt(amount)}", new Color("ffd18b"), .55f);
        UpdateHud();
        return true;
    }

    private bool HandleCouragePickupInput(InputEvent input)
    {
        if (_couragePickupPointerDown && input is InputEventKey key)
        {
            if (key.Pressed && key.Keycode is Key.Escape or Key.Backspace) CancelCouragePickupGesture();
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (_couragePickupPointerDown && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right or MouseButton.Middle })
        { CancelCouragePickupGesture(); return false; }

        Vector2 point; int id; bool pressed = false, released = false, canceled = false;
        switch (input)
        {
            case InputEventScreenTouch touch:
                point = touch.Position; id = touch.Index; pressed = touch.Pressed; released = !touch.Pressed; canceled = touch.Canceled; break;
            case InputEventScreenDrag drag:
                point = drag.Position; id = drag.Index; break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left:
                point = mouse.Position; id = -2; pressed = mouse.Pressed; released = !mouse.Pressed; canceled = mouse.Canceled; break;
            case InputEventMouseMotion motion:
                point = motion.Position; id = -2; break;
            default:
                if (!_couragePickupPointerDown) return false;
                GetViewport().SetInputAsHandled(); return true;
        }

        if (!_couragePickupPointerDown)
        {
            if (pressed && _discardCouragePickupRelease == id) _discardCouragePickupRelease = null;
            if (released && _discardCouragePickupRelease == id)
            { _discardCouragePickupRelease = null; GetViewport().SetInputAsHandled(); return true; }
            if (!pressed || canceled || !CouragePickupActionAvailable || _cardPointerDown || _mobilePointerDown || _battleCameraDragging) return false;
            var pickup = CouragePickupAt(point);
            if (pickup == null) return false;
            _pressedCouragePickup = pickup;
            _couragePickupPointerDown = true;
            _couragePickupPointerId = id;
            _couragePickupPointerStart = point;
            GetViewport().SetInputAsHandled(); return true;
        }

        GetViewport().SetInputAsHandled();
        if (id != _couragePickupPointerId) return true;
        if (canceled || !CouragePickupActionAvailable)
        { CancelCouragePickupGesture(!released); return true; }
        if (point.DistanceTo(_couragePickupPointerStart) > 14f)
        {
            // A touch drag beginning on a pack becomes an ordinary camera pan, not a pickup.
            if (!released)
            {
                _mobilePointerDown = true; _mobileDragging = true; _mobilePointerId = id;
                _mobilePointerStart = _mobilePointerLast = _couragePickupPointerStart;
                _couragePickupPointerDown = false; _pressedCouragePickup = null;
                return false;
            }
            CancelCouragePickupGesture(!released);
            return true;
        }
        if (!released) return true;
        var selected = _pressedCouragePickup;
        CancelCouragePickupGesture(false);
        if (IsInstanceValid(selected) && CouragePickupAt(point) == selected) CollectCouragePickup(selected);
        return true;
    }

    private void CancelCouragePickupGesture(bool discardRelease = true)
    {
        if (!_couragePickupPointerDown) return;
        if (discardRelease) _discardCouragePickupRelease = _couragePickupPointerId;
        if (_couragePickupPointerId >= 0) _cardMouseBlockUntil = Time.GetTicksMsec() + 250;
        _couragePickupPointerDown = false;
        _pressedCouragePickup = null;
    }

    private void UpdateCouragePickupPresentation()
    {
        if (_battleEnded)
        {
            CancelCouragePickupGesture();
            foreach (var pickup in _couragePickups) pickup.QueueFree();
            _couragePickups.Clear();
        }
        else if (!CouragePickupActionAvailable) CancelCouragePickupGesture();
    }
}
