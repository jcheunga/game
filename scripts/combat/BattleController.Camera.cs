using System.Collections.Generic;
using Godot;

// The battle camera. Every screen gets the same one: it frames the band at the soldier scale between the HUD
// rows and stays where the player puts it. Touch and mouse drags (BattleController.Mobile.cs) and middle-drag
// glide on after a flick and stretch past either end of the field, springing back when let go; the mouse
// wheel, trackpad and keyboard pan directly.
public partial class BattleController
{
    private const float BattleScrollStep = 100f;
    // A flick glides on, its speed decaying by this rate each second, and stops below the rest speed (world units).
    private const float BattleScrollFriction = 2.8f;
    private const float BattleScrollRestSpeed = 6f;
    // The fastest flick, in screen pixels a second, and how far back the pointer's speed is measured.
    private const float BattleFlingMaxSpeed = 7000f;
    private const ulong BattleFlingWindowUsec = 100_000;
    // Faster than this (screen pixels a second), a touch on the field only stops it.
    private const float BattleCatchSpeed = 60f;
    // Past either end the field stretches, less the further it is dragged, to at most this fraction of the view;
    // a critically damped spring this stiff pulls it back.
    private const float BattleStretchFraction = .25f;
    private const float BattleStretchSpring = 13f;
    // Kept in shot around the walking band: room above the top line for a soldier's sprite and
    // health bar, and below the bottom line for feet and shadows.
    private const float FrameHeadroom = 52f;
    private const float FrameFootroom = 14f;
    // Where the band sits within the clear rows when it fits with room to spare: low on screen, like a
    // road under the scenery, with the bases and troops standing up into the space above.
    // .81 puts the road where the approved battle concept has it (screen rows ~411-516 at 1280x720).
    private const float BandRowFraction = .81f;
    // Match the existing left-side margin at the far end of the field.
    private float BattleWorldWidth => BattlefieldRight + BattlefieldLeft;
    private float FrameTop => BattlefieldTop + SpawnVerticalPadding - FrameHeadroom;
    private float FrameBottom => BattlefieldBottom - SpawnVerticalPadding + FrameFootroom;
    // Screen rows the HUD leaves clear, between the meters and the card tray (set by the HUD layout).
    private float FieldScreenTop => _mobileFieldTop;
    private float FieldScreenBottom => _mobileFieldBottom;
    // The zoom that shows ViewWidth of the field (the soldier scale) and the band with its headroom.
    private float BattleFitZoom => Mathf.Min(GetViewportRect().Size.X / Mathf.Min(_combat.ViewWidth, BattleWorldWidth),
        Mathf.Max(1f, FieldScreenBottom - FieldScreenTop) / (FrameBottom - FrameTop));
    private bool BattleFieldFitsView(Camera2D camera) => GetViewportRect().Size.X / camera.Zoom.X >= BattleWorldWidth - .5f;
    private Camera2D _battleCamera;
    private bool _battleCameraDragging, _battleCameraGliding;
    private Vector2 _battleCameraPointer;
    // While gliding, the camera's speed in world units a second.
    private float _battleCameraVelocity;
    // A drag's unstretched camera X: the field tracks the pointer exactly between the ends and stretches past them.
    private float _battleCameraDragX;
    private readonly List<(ulong Time, float X)> _battleCameraSamples = new();

    private void ClampBattleCamera(Camera2D camera) => camera.Position = CameraPositionFor(camera.Position, camera.Zoom.X);

    // The camera X range that keeps the view on the field; a view wider than the field is centred on it.
    private (float Min, float Max) BattleCameraXRange(float zoom)
    {
        var halfWidth = GetViewportRect().Size.X / zoom * .5f;
        return halfWidth * 2 >= BattleWorldWidth ? (BattleWorldWidth * .5f, BattleWorldWidth * .5f)
            : (halfWidth, BattleWorldWidth - halfWidth);
    }

    private Vector2 CameraPositionFor(Vector2 desired, float zoom)
    {
        var (minX, maxX) = BattleCameraXRange(zoom);
        var x = Mathf.Clamp(desired.X, minX, maxX);
        // The band sits in the rows the HUD leaves clear: low within them when it fits, otherwise never
        // scrolled past its frame.
        var offset = ((FieldScreenTop + FieldScreenBottom) * .5f - GetViewportRect().Size.Y * .5f) / zoom;
        var windowHalf = (FieldScreenBottom - FieldScreenTop) / zoom * .5f;
        float center;
        if (windowHalf * 2 >= FrameBottom - FrameTop)
        {
            // World row shown at the clear rows' centre, so the band lands at BandRowFraction down them.
            var bandCenter = (BattlefieldTop + BattlefieldBottom) * .5f;
            var bandDrop = (BandRowFraction - .5f) * (FieldScreenBottom - FieldScreenTop) / zoom;
            center = Mathf.Clamp(bandCenter - bandDrop, FrameBottom - windowHalf, FrameTop + windowHalf);
        }
        else center = Mathf.Clamp(desired.Y + offset, FrameTop + windowHalf, FrameBottom - windowHalf);
        // The view stays above the bottom of the painting.
        var y = center - offset;
        if (PaintedGroundBottom is float ground) y = Mathf.Min(y, ground - GetViewportRect().Size.Y / zoom * .5f);
        return new Vector2(x, y);
    }

    private float? PaintedGroundBottom
    {
        get
        {
            if (_stageBackdrop == null) return null;
            var bottom = float.MinValue;
            foreach (var layer in _stageBackdrop.Layers) bottom = Mathf.Max(bottom, layer.Rect.End.Y);
            return bottom;
        }
    }

    private void SetBattleCameraX(float x)
    {
        if (_battleCamera == null || _battlePaused || _battleEnded || _endlessCheckpointActive) return;
        StopBattleCameraGlide();
        _battleCamera.Position = new Vector2(x, _battleCamera.Position.Y);
        ClampBattleCamera(_battleCamera);
        _battleCamera.ForceUpdateScroll();
    }

    // Puts the camera at x, which may lie past the ends while the field is stretched.
    private void PlaceBattleCamera(float x)
    {
        _battleCamera.Position = new Vector2(x, CameraPositionFor(_battleCamera.Position, _battleCamera.Zoom.X).Y);
        _battleCamera.ForceUpdateScroll();
    }

    // The most the field stretches past an end, in world units.
    private float BattleStretchLimit(float zoom) => GetViewportRect().Size.X / zoom * BattleStretchFraction;

    // How far past an end the field shows for a drag that far past it: less and less, never the whole limit.
    private float StretchedX(float x, float zoom)
    {
        var (min, max) = BattleCameraXRange(zoom);
        var limit = BattleStretchLimit(zoom);
        float Stretch(float past) => limit * (1 - 1 / (past * .55f / limit + 1));
        return x < min ? min - Stretch(min - x) : x > max ? max + Stretch(x - max) : x;
    }

    private float UnstretchedX(float x, float zoom)
    {
        var (min, max) = BattleCameraXRange(zoom);
        var limit = BattleStretchLimit(zoom);
        float Unstretch(float shown) => limit / .55f * (1 / (1 - Mathf.Min(shown, limit * .99f) / limit) - 1);
        return x < min ? min - Unstretch(min - x) : x > max ? max + Unstretch(x - max) : x;
    }

    private bool BattleCameraStretched
    {
        get
        {
            var (min, max) = BattleCameraXRange(_battleCamera.Zoom.X);
            return _battleCamera.Position.X < min - .01f || _battleCamera.Position.X > max + .01f;
        }
    }

    /// <summary>True while the field is still gliding or springing back fast enough that a touch should only stop it.</summary>
    private bool BattleCameraMoving => _battleCamera != null && _battleCameraGliding &&
        (Mathf.Abs(_battleCameraVelocity) * _battleCamera.Zoom.X > BattleCatchSpeed || BattleCameraStretched);

    private void StopBattleCameraGlide()
    {
        _battleCameraGliding = false;
        _battleCameraVelocity = 0;
        _battleCameraSamples.Clear();
    }

    /// <summary>Let the field come to rest where it is, springing back if it is stretched past an end.</summary>
    private void SettleBattleCamera()
    {
        _battleCameraSamples.Clear();
        _battleCameraVelocity = 0;
        _battleCameraGliding = _battleCamera != null;
    }

    /// <summary>A pointer goes down on the field: it catches any glide and the drag starts from here.</summary>
    private void GrabBattleCamera()
    {
        StopBattleCameraGlide();
        _battleCameraDragX = UnstretchedX(_battleCamera.Position.X, _battleCamera.Zoom.X);
        SampleBattleCameraDrag();
    }

    /// <summary>The pointer moves by screen pixels; the ground moves with it.</summary>
    private void DragBattleCamera(float screenDelta)
    {
        _battleCameraDragX -= screenDelta / _battleCamera.Zoom.X;
        PlaceBattleCamera(StretchedX(_battleCameraDragX, _battleCamera.Zoom.X));
        SampleBattleCameraDrag();
    }

    private void SampleBattleCameraDrag()
    {
        var now = Time.GetTicksUsec();
        _battleCameraSamples.Add((now, _battleCameraDragX));
        // The samples inside the window, and the last one before it to measure a slow drag against.
        while (_battleCameraSamples.Count > 2 && now - _battleCameraSamples[1].Time > BattleFlingWindowUsec)
            _battleCameraSamples.RemoveAt(0);
    }

    /// <summary>The pointer lets go: the field glides on at the pointer's speed, or springs back from past an end.</summary>
    private void ReleaseBattleCamera()
    {
        SampleBattleCameraDrag();
        var (start, end) = (_battleCameraSamples[0], _battleCameraSamples[^1]);
        var seconds = (end.Time - start.Time) / 1e6f;
        var maxSpeed = BattleFlingMaxSpeed / _battleCamera.Zoom.X;
        var velocity = seconds >= .008f ? Mathf.Clamp((end.X - start.X) / seconds, -maxSpeed, maxSpeed) : 0;
        // Flung further out from past an end, the field stretches only a little more before springing back.
        var (min, max) = BattleCameraXRange(_battleCamera.Zoom.X);
        var x = _battleCamera.Position.X;
        if ((x < min && velocity < 0) || (x > max && velocity > 0)) velocity *= .3f;
        SettleBattleCamera();
        _battleCameraVelocity = velocity;
    }

    private void UpdateBattleCamera(float delta)
    {
        var camera = _battleCamera;
        if (camera == null || !_battleCameraGliding || _battleCameraDragging || _mobilePointerDown) return;
        var zoom = camera.Zoom.X;
        var (min, max) = BattleCameraXRange(zoom);
        var x = camera.Position.X;
        var velocity = _battleCameraVelocity;
        var done = false;
        if (x < min || x > max)
        {
            // A critically damped spring back to the end, solved exactly so every frame rate settles alike.
            var edge = x < min ? min : max;
            var limit = BattleStretchLimit(zoom);
            var past = x - edge;
            var lead = velocity + BattleStretchSpring * past;
            var decay = Mathf.Exp(-BattleStretchSpring * delta);
            past = (past + lead * delta) * decay;
            velocity = (lead - BattleStretchSpring * (past / decay)) * decay;
            if (Mathf.Abs(past) >= limit) { past = Mathf.Sign(past) * limit; velocity = 0; }
            if (Mathf.Abs(past) < .25f && Mathf.Abs(velocity) < BattleScrollRestSpeed) { past = 0; done = true; }
            x = edge + past;
        }
        // A pause or the end of the battle stops a glide; a stretched field still springs back.
        else if (_battlePaused || _battleEnded || _endlessCheckpointActive) done = true;
        else
        {
            var decay = Mathf.Exp(-BattleScrollFriction * delta);
            x += velocity * (1 - decay) / BattleScrollFriction;
            velocity *= decay;
            // Gliding into an end, the field runs on past it and the spring takes over next frame.
            done = Mathf.Abs(velocity) < BattleScrollRestSpeed && x >= min && x <= max;
        }
        PlaceBattleCamera(x);
        if (done) StopBattleCameraGlide();
        else _battleCameraVelocity = velocity;
    }

    private bool HandleBattleCameraInput(InputEvent input)
    {
        var camera = _battleCamera;
        if (camera == null || _battlePaused || _battleEnded || _endlessCheckpointActive) return false;
        // The whole field is on screen; there is nothing to pan to.
        if (BattleFieldFitsView(camera)) return false;
        // Controls may pass wheel events through even when they stop clicks.
        if (input is InputEventMouseButton or InputEventPanGesture && GetViewport().GuiGetHoveredControl() != null)
            return false;
        float amount;
        if (input is InputEventMouseButton wheel && wheel.Pressed &&
            wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
        {
            var direction = wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelLeft ? -1 : 1;
            amount = direction * BattleScrollStep * wheel.Factor;
        }
        else if (input is InputEventPanGesture pan)
        {
            amount = (Mathf.Abs(pan.Delta.X) > Mathf.Abs(pan.Delta.Y) ? pan.Delta.X : pan.Delta.Y) * 48f;
        }
        else if (input is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Middle)
        {
            _battleCameraDragging = true;
            _battleCameraPointer = mouse.Position;
            GrabBattleCamera();
            GetViewport().SetInputAsHandled();
            return true;
        }
        else if (input is InputEventKey key && key.Pressed && key.Keycode is Key.Left or Key.Right or Key.Home or Key.End)
        {
            SetBattleCameraX(key.Keycode switch
            {
                Key.Home => 0,
                Key.End => BattleWorldWidth,
                Key.Left => camera.Position.X - BattleScrollStep / camera.Zoom.X,
                _ => camera.Position.X + BattleScrollStep / camera.Zoom.X
            });
            GetViewport().SetInputAsHandled();
            return true;
        }
        else return false;

        // Scroll amounts are screen pixels; the camera moves in world units.
        _mobilePointerDown = false;
        SetBattleCameraX(camera.Position.X + amount / camera.Zoom.X);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool ContinueBattleCameraDrag(InputEvent input)
    {
        if (!_battleCameraDragging || _battleCamera == null) return false;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Middle && !mouse.Pressed)
        {
            _battleCameraDragging = false;
            ReleaseBattleCamera();
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (input is not InputEventMouseMotion motion) return false;
        if (_battlePaused || _battleEnded || _endlessCheckpointActive) { _battleCameraDragging = false; SettleBattleCamera(); }
        else DragBattleCamera(motion.Position.X - _battleCameraPointer.X);
        _battleCameraPointer = motion.Position;
        GetViewport().SetInputAsHandled();
        return true;
    }
}
