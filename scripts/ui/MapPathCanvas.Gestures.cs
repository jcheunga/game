using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Map gestures. The land moves exactly with the finger or mouse and glides on a little after a flick. Two fingers
/// pinch to zoom about their midpoint while they pan, the wheel and trackpad pinch zoom about the pointer, and a
/// trackpad's two-finger scroll pans. A press on a landmark pans the map too; only a tap opens it.
/// Touch is read from the touch events alone: Godot also emulates a mouse from the first finger, which would move
/// the map twice.
/// </summary>
public partial class MapPathCanvas
{
    // Opening framing: close enough to read the landmarks around the caravan, with the next ones in sight.
    private const float DefaultZoom = .85f;
    private const float MaxZoom = 1.4f;
    private const float WheelZoomStep = 1.12f;
    // A press that moves less than this (canvas pixels) is a tap.
    private const float TapSlop = 14f;
    // A flick glides on, slowing by this rate each second, from the pointer's speed over the last moment.
    private const float GlideFriction = 5f;
    private const float GlideMinSpeed = 150f;
    private const float GlideRestSpeed = 12f;
    private const float FlingMaxSpeed = 3600f;
    private const ulong FlingWindowUsec = 80_000;
    // Gliding faster than this, a press only stops the map.
    private const float CatchSpeed = 120f;
    // Godot reports a trackpad scroll at a thirtieth of the fingers' travel in points.
    private const float PanGestureScale = 33f;
    // The real mouse is pointer -1; touches use their own indices.
    private const int MousePointer = -1;

    private float MinZoom => Mathf.Max(.3f, Mathf.Max(Size.X / MapBounds.Size.X, Size.Y / MapBounds.Size.Y));
    // Where the pointers are, in canvas pixels.
    private readonly Dictionary<int, Vector2> _pointers = new();
    private readonly List<(ulong Time, Vector2 Point)> _panSamples = new();
    private Vector2 _pressPoint, _lastCentroid, _glideVelocity;
    private float _lastSpread;
    // True once a press has moved past a tap (or a second finger joined), until it lets go.
    private bool _dragging;
    private bool _tapButton, _pinched, _caughtGlide;
    private BaseButton _pressLandmark;

    public override void _GuiInput(InputEvent input)
    {
        if (HandleMapInput(input, null)) AcceptEvent();
        if (input is InputEventMouseMotion hover && !_dragging)
            _hoverTile = AdventureTileCatalog.At(ActiveMapId, (hover.Position - MapOffset) / Zoom) is { } tile && GameState.Instance.IsAdventureTileFrontier(tile) ? tile : null;
    }

    /// <summary>
    /// A landmark passes its pointer input to the map, so a drag that starts on it pans, and the map decides whether
    /// it was a tap. Its own press is left to the keyboard and accessibility.
    /// </summary>
    private void AddLandmark(BaseButton landmark)
    {
        landmark.ButtonMask = 0;
        landmark.GuiInput += input => HandleMapInput(input.XformedBy(landmark.GetTransform()), landmark);
        AddChild(landmark);
    }

    private bool HandleMapInput(InputEvent input, BaseButton landmark)
    {
        // Emulated from a touch that arrives here as well.
        if (input is InputEventMouse && input.Device == InputEvent.DeviceIdEmulation) return false;
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                var notches = wheel.Factor > 0 ? wheel.Factor : 1;
                ChangeZoom(Mathf.Pow(WheelZoomStep, wheel.ButtonIndex == MouseButton.WheelUp ? notches : -notches), wheel.Position);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle } mouse:
                if (mouse.Pressed) PressPointer(MousePointer, mouse.Position, landmark, mouse.ButtonIndex == MouseButton.Left);
                else if (_pointers.ContainsKey(MousePointer)) ReleasePointer(MousePointer, mouse.Position, mouse.Canceled);
                return true;
            case InputEventMouseMotion motion when _pointers.ContainsKey(MousePointer):
                MovePointer(MousePointer, motion.Position);
                return true;
            case InputEventScreenTouch touch when touch.Device != InputEvent.DeviceIdEmulation:
                if (touch.Pressed) PressPointer(touch.Index, touch.Position, landmark, true);
                else if (_pointers.ContainsKey(touch.Index)) ReleasePointer(touch.Index, touch.Position, touch.Canceled);
                return true;
            case InputEventScreenDrag drag when drag.Device != InputEvent.DeviceIdEmulation && _pointers.ContainsKey(drag.Index):
                MovePointer(drag.Index, drag.Position);
                return true;
            case InputEventMagnifyGesture magnify:
                ChangeZoom(magnify.Factor, magnify.Position);
                return true;
            case InputEventPanGesture pan:
                _glideVelocity = Vector2.Zero;
                MapOffset -= pan.Delta * PanGestureScale; UpdateView();
                return true;
        }
        return false;
    }

    private void PressPointer(int id, Vector2 point, BaseButton landmark, bool tapButton)
    {
        // A press for a pointer already down means its release was lost; start over.
        if (_pointers.ContainsKey(id)) ResetGesture();
        if (_pointers.Count == 0)
        {
            _caughtGlide = _glideVelocity.Length() > CatchSpeed;
            _dragging = _pinched = false;
            _pressPoint = point; _pressLandmark = landmark; _tapButton = tapButton;
        }
        else _pinched = _dragging = true;
        _glideVelocity = Vector2.Zero;
        _pointers[id] = point;
        RestartGestureBaseline();
    }

    private void MovePointer(int id, Vector2 point)
    {
        _pointers[id] = point;
        if (!_dragging)
        {
            // Within the slop the map holds still, so a tap never nudges it.
            if (point.DistanceTo(_pressPoint) < TapSlop) return;
            _dragging = true; _hoverTile = null;
            RestartGestureBaseline();
            return;
        }
        var (centroid, spread) = GestureShape();
        // The ground under the fingers' midpoint stays under it as they move and spread.
        var world = (_lastCentroid - MapOffset) / Zoom;
        if (_pointers.Count > 1 && _lastSpread > 1 && spread > 1)
            Zoom = Mathf.Clamp(Zoom * spread / _lastSpread, MinZoom, MaxZoom);
        MapOffset = centroid - world * Zoom; UpdateView();
        _lastCentroid = centroid; _lastSpread = spread;
        SamplePan(centroid);
    }

    private void ReleasePointer(int id, Vector2 point, bool canceled)
    {
        _pointers[id] = point;
        if (_pointers.Count > 1)
        {
            _pointers.Remove(id);
            RestartGestureBaseline();
            return;
        }
        if (_dragging) MovePointer(id, point);
        var tap = !_dragging && !_caughtGlide && !canceled && _tapButton;
        var fling = _dragging && !canceled ? FlingVelocity() : Vector2.Zero;
        var landmark = _pressLandmark;
        // A finger that stayed down after a pinch only flicks after it has moved on its own for a moment.
        if (_pinched && _panSamples.Count > 0 && _panSamples[^1].Time - _panSamples[0].Time < FlingWindowUsec) fling = Vector2.Zero;
        ResetGesture();
        _glideVelocity = fling.Length() >= GlideMinSpeed ? fling : Vector2.Zero;
        if (!tap) return;
        // A tapped landmark presses itself, with its sound; otherwise the tile under the tap is chosen.
        if (landmark == null) SelectAt(point);
        else if (IsInstanceValid(landmark) && !landmark.Disabled) landmark.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void SelectAt(Vector2 point)
    {
        if (AdventureTileCatalog.At(ActiveMapId, (point - MapOffset) / Zoom) is not { } tile || !GameState.Instance.IsAdventureTileRevealed(tile)) return;
        if (tile.Site != null && GameState.Instance.IsAdventureSiteDiscovered(tile.Id)) SiteSelected?.Invoke(tile.Site);
        else if (tile.Discovery != null) DiscoverySelected?.Invoke(tile.Discovery);
        else if (GameState.Instance.IsAdventureTileFrontier(tile) && !IsTravelling) GroundSelected?.Invoke(tile);
    }

    /// <summary>A finger joins or leaves: measure from where the fingers are now, so the map doesn't jump.</summary>
    private void RestartGestureBaseline()
    {
        (_lastCentroid, _lastSpread) = GestureShape();
        _panSamples.Clear();
        SamplePan(_lastCentroid);
    }

    private (Vector2 Centroid, float Spread) GestureShape()
    {
        if (_pointers.Count == 0) return (_lastCentroid, 0);
        var centroid = _pointers.Values.Aggregate(Vector2.Zero, (sum, point) => sum + point) / _pointers.Count;
        return (centroid, _pointers.Values.Average(point => point.DistanceTo(centroid)));
    }

    private void SamplePan(Vector2 point)
    {
        var now = Time.GetTicksUsec();
        _panSamples.Add((now, point));
        // The samples inside the window, and the last one before it to measure a slow drag against.
        while (_panSamples.Count > 2 && now - _panSamples[1].Time > FlingWindowUsec) _panSamples.RemoveAt(0);
    }

    private Vector2 FlingVelocity()
    {
        if (_panSamples.Count < 2) return Vector2.Zero;
        var (start, end) = (_panSamples[0], _panSamples[^1]);
        var seconds = (end.Time - start.Time) / 1e6f;
        return seconds < .008f ? Vector2.Zero : ((end.Point - start.Point) / seconds).LimitLength(FlingMaxSpeed);
    }

    private void ResetGesture()
    {
        _pointers.Clear(); _panSamples.Clear();
        _dragging = _pinched = _caughtGlide = false;
        _pressLandmark = null;
    }

    private void UpdateGlide(float delta)
    {
        if (_glideVelocity == Vector2.Zero || _pointers.Count > 0) return;
        // Exponential friction, solved exactly so every frame rate glides alike.
        var decay = Mathf.Exp(-GlideFriction * delta);
        var target = MapOffset + _glideVelocity * (1 - decay) / GlideFriction;
        MapOffset = target; UpdateView();
        _glideVelocity *= decay;
        // The painting's edge stops the glide along it.
        if (!Mathf.IsEqualApprox(MapOffset.X, target.X)) _glideVelocity.X = 0;
        if (!Mathf.IsEqualApprox(MapOffset.Y, target.Y)) _glideVelocity.Y = 0;
        if (_glideVelocity.Length() < GlideRestSpeed) _glideVelocity = Vector2.Zero;
    }

    public override void _Notification(int what)
    {
        // A system interruption or a hidden map may swallow the release.
        if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut || what == NotificationWMWindowFocusOut
            || (what == NotificationVisibilityChanged && !IsVisibleInTree()))
        {
            ResetGesture(); _glideVelocity = Vector2.Zero;
        }
    }
}
