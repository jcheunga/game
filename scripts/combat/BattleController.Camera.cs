using Godot;

public partial class BattleController
{
    private const float BattleScrollStep = 100f;
    // Kept in shot around the walking band: room above the top line for a soldier's sprite and
    // health bar, and below the bottom line for feet and shadows.
    private const float FrameHeadroom = 52f;
    private const float FrameFootroom = 14f;
    // Screen rows the desktop HUD leaves clear, between the meters and the card tray.
    private const float DesktopFieldTop = 92f;
    private const float DesktopFieldBottomInset = 168f;
    private const float BattleCameraFollowRate = 3f;
    // Where the band sits within the clear rows when it fits with room to spare: low on screen, like a
    // road under the scenery, with the bases and troops standing up into the space above.
    // .81 puts the road where the approved battle concept has it (screen rows ~411-516 at 1280x720).
    private const float BandRowFraction = .81f;
    // Match the existing left-side margin at the far end of the field.
    private float BattleWorldWidth => BattlefieldRight + BattlefieldLeft;
    private float FrameTop => BattlefieldTop + SpawnVerticalPadding - FrameHeadroom;
    private float FrameBottom => BattlefieldBottom - SpawnVerticalPadding + FrameFootroom;
    private float FieldScreenTop => _mobileCamera != null ? _mobileFieldTop : DesktopFieldTop;
    private float FieldScreenBottom => _mobileCamera != null ? _mobileFieldBottom : GetViewportRect().Size.Y - DesktopFieldBottomInset;
    // The zoom that shows ViewWidth of the field (the soldier scale) and the band with its headroom.
    private float BattleFitZoom => Mathf.Min(GetViewportRect().Size.X / Mathf.Min(_combat.ViewWidth, BattleWorldWidth),
        Mathf.Max(1f, FieldScreenBottom - FieldScreenTop) / (FrameBottom - FrameTop));
    private bool BattleFieldFitsView(Camera2D camera) => GetViewportRect().Size.X / camera.Zoom.X >= BattleWorldWidth - .5f;
    // World offset from the camera centre to the centre of the clear rows.
    private float FieldScreenOffset(Camera2D camera) =>
        ((FieldScreenTop + FieldScreenBottom) * .5f - GetViewportRect().Size.Y * .5f) / camera.Zoom.Y;
    private Camera2D _battleCamera;
    private Button _battleFollowButton;
    private bool _battleCameraDragging, _battleCameraFollow = true;
    private Vector2 _battleCameraPointer;

    private void ConfigureBattleCamera(Control root)
    {
        if (MobilePresentation.Enabled) return;
        _battleCamera = new Camera2D
        {
            Name = "BattleCamera",
            Position = new Vector2(0, (FrameTop + FrameBottom) * .5f),
            PositionSmoothingEnabled = false
        };
        AddChild(_battleCamera);
        _battleCamera.MakeCurrent();
        _battleFollowButton = RealmUi.IconButton("sword", "Follow the fighting [F]", () => SetBattleCameraFollow(true));
        _battleFollowButton.Visible = false;
        root.AddChild(_battleFollowButton);

        GetViewport().SizeChanged += RefreshBattleCamera;
        RefreshBattleCamera();
    }

    private void RefreshBattleCamera()
    {
        if (_battleCamera == null) return;
        CancelCardDrag();
        _battleCameraDragging = false;
        _battleCamera.Zoom = Vector2.One * BattleFitZoom;
        ClampBattleCamera(_battleCamera);
        _battleCamera.ForceUpdateScroll();
    }

    private void ClampBattleCamera(Camera2D camera) => camera.Position = CameraPositionFor(camera.Position, camera.Zoom.X);

    private Vector2 CameraPositionFor(Vector2 desired, float zoom)
    {
        var halfWidth = GetViewportRect().Size.X / zoom * .5f;
        var x = halfWidth * 2 >= BattleWorldWidth
            ? BattleWorldWidth * .5f : Mathf.Clamp(desired.X, halfWidth, BattleWorldWidth - halfWidth);
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
        return new Vector2(x, center - offset);
    }

    private void SetBattleCameraFollow(bool follow)
    {
        _battleCameraFollow = follow;
        if (_battleFollowButton != null) _battleFollowButton.Visible = !follow && !_battleEnded;
    }

    private void SetBattleCameraX(float x)
    {
        if (_battleCamera == null || _battlePaused || _battleEnded || _endlessCheckpointActive) return;
        SetBattleCameraFollow(false);
        _battleCamera.Position = new Vector2(x, _battleCamera.Position.Y);
        ClampBattleCamera(_battleCamera);
        _battleCamera.ForceUpdateScroll();
    }

    // The point to keep centred: an enemy that has got past the front line, else the clash at the
    // front, else the ground ahead of the leading soldier. With no soldiers out, the wagon.
    private Vector2 ResolveBattleFollowFocus(float halfView)
    {
        Unit front = null, rearEnemy = null;
        foreach (var unit in _units)
        {
            if (unit.IsDead) continue;
            if (unit.Team == Team.Player) { if (front == null || unit.Position.X > front.Position.X) front = unit; }
            else if (rearEnemy == null || unit.Position.X < rearEnemy.Position.X) rearEnemy = unit;
        }
        var bandCenter = (BattlefieldTop + BattlefieldBottom) * .5f;
        if (front == null) return new Vector2(PlayerBaseX, bandCenter);
        if (rearEnemy != null && rearEnemy.Position.X <= front.Position.X)
            return new Vector2(rearEnemy.Position.X + halfView * .3f, rearEnemy.Position.Y);
        if (rearEnemy != null && rearEnemy.Position.X - front.Position.X < halfView * 1.2f)
            return (front.Position + rearEnemy.Position) * .5f;
        return new Vector2(front.Position.X + halfView * .35f, front.Position.Y);
    }

    private void UpdateBattleCamera(float delta)
    {
        var camera = _battleCamera ?? _mobileCamera;
        if (camera == null || _battlePaused || _battleEnded || _endlessCheckpointActive || _cardPointerDown) return;
        if (_battleCamera != null ? !_battleCameraFollow || _battleCameraDragging
            : _mobileOverview || !_mobileFollow || _mobilePointerDown) return;
        var focus = ResolveBattleFollowFocus(GetViewportRect().Size.X / camera.Zoom.X * .5f);
        var destination = new Vector2(focus.X, focus.Y - FieldScreenOffset(camera));
        camera.Position = camera.Position.Lerp(destination, 1 - Mathf.Exp(-delta * BattleCameraFollowRate));
        ClampBattleCamera(camera);
    }

    private bool HandleBattleCameraInput(InputEvent input)
    {
        var camera = _battleCamera ?? _mobileCamera;
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
        else if (_battleCamera != null && input is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Middle)
        {
            _battleCameraDragging = true;
            _battleCameraPointer = mouse.Position;
            GetViewport().SetInputAsHandled();
            return true;
        }
        else if (_battleCamera != null && input is InputEventKey { Pressed: true, Keycode: Key.F })
        {
            SetBattleCameraFollow(true);
            GetViewport().SetInputAsHandled();
            return true;
        }
        else if (_battleCamera != null && input is InputEventKey key && key.Pressed &&
            key.Keycode is Key.Left or Key.Right or Key.Home or Key.End)
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
        if (_battleCamera != null) SetBattleCameraX(camera.Position.X + amount / camera.Zoom.X);
        else if (!_mobileOverview)
        {
            _mobilePointerDown = false;
            PauseMobileFollow();
            camera.Position += new Vector2(amount / camera.Zoom.X, 0);
            ClampMobileCamera();
            camera.ForceUpdateScroll();
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool ContinueBattleCameraDrag(InputEvent input)
    {
        if (!_battleCameraDragging || _battleCamera == null) return false;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Middle && !mouse.Pressed)
        {
            _battleCameraDragging = false;
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (input is not InputEventMouseMotion motion) return false;
        if (_battlePaused || _battleEnded || _endlessCheckpointActive) _battleCameraDragging = false;
        else SetBattleCameraX(_battleCamera.Position.X - (motion.Position.X - _battleCameraPointer.X) / _battleCamera.Zoom.X);
        _battleCameraPointer = motion.Position;
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void CleanupBattleCamera()
    {
        if (_battleCamera != null) GetViewport().SizeChanged -= RefreshBattleCamera;
    }

}
