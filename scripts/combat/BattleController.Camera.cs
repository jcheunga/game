using Godot;

public partial class BattleController
{
    private const float BattleWorldHeight = 720f;
    private const float BattleScrollStep = 100f;
    // Match the existing left-side margin at the far end of the field.
    private float BattleWorldWidth => BattlefieldRight + BattlefieldLeft;
    private Camera2D _battleCamera;
    private HScrollBar _battleScrollBar;
    private bool _battleCameraDragging;
    private Vector2 _battleCameraPointer;

    private void ConfigureBattleCamera(Control root)
    {
        if (MobilePresentation.Enabled) return;
        _battleCamera = new Camera2D
        {
            Name = "BattleCamera",
            Position = GetViewportRect().Size * .5f,
            PositionSmoothingEnabled = false
        };
        AddChild(_battleCamera);
        _battleCamera.MakeCurrent();

        var navigation = new HBoxContainer
        {
            Name = "BattlefieldNavigation",
            Position = new Vector2(440, 100),
            Size = new Vector2(360, 28)
        };
        navigation.AddThemeConstantOverride("separation", 8);
        root.AddChild(navigation);
        // Keep reports and modal overlays above navigation.
        root.MoveChild(navigation, 0);
        var wagon = new Button { Text = "Wagon", TooltipText = "View the war wagon [Home]" };
        wagon.Pressed += () => SetBattleCameraX(0);
        navigation.AddChild(wagon);
        _battleScrollBar = new HScrollBar
        {
            CustomMinimumSize = new Vector2(190, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Step = 0,
            TooltipText = "Scroll to pan · Middle-drag · Left / Right arrows",
            AccessibilityName = "Battlefield horizontal view"
        };
        navigation.AddChild(_battleScrollBar);
        _battleScrollBar.ValueChanged += value => SetBattleCameraX((float)value + GetViewportRect().Size.X * .5f);
        var gate = new Button { Text = "Gate", TooltipText = "View the enemy end [End]" };
        gate.Pressed += () => SetBattleCameraX(BattleWorldWidth);
        navigation.AddChild(gate);
        GetViewport().SizeChanged += RefreshBattleCamera;
        RefreshBattleCamera();
    }

    private void RefreshBattleCamera()
    {
        if (_battleCamera == null) return;
        CancelCardDrag();
        _battleCameraDragging = false;
        ClampBattleCamera(_battleCamera);
        _battleCamera.ForceUpdateScroll();
        _battleScrollBar.MaxValue = BattleWorldWidth;
        _battleScrollBar.Page = Mathf.Min(BattleWorldWidth, GetViewportRect().Size.X);
        _battleScrollBar.SetValueNoSignal(_battleCamera.Position.X - GetViewportRect().Size.X * .5f);
    }

    private void ClampBattleCamera(Camera2D camera)
    {
        var half = GetViewportRect().Size / camera.Zoom * .5f;
        static float ClampAxis(float value, float halfView, float extent) => halfView * 2 >= extent
            ? extent * .5f : Mathf.Clamp(value, halfView, extent - halfView);
        camera.Position = new Vector2(ClampAxis(camera.Position.X, half.X, BattleWorldWidth),
            ClampAxis(camera.Position.Y, half.Y, BattleWorldHeight));
    }

    private void SetBattleCameraX(float x)
    {
        if (_battleCamera == null || _battlePaused || _battleEnded || _endlessCheckpointActive) return;
        _battleCamera.Position = new Vector2(x, _battleCamera.Position.Y);
        ClampBattleCamera(_battleCamera);
        _battleCamera.ForceUpdateScroll();
        _battleScrollBar.SetValueNoSignal(_battleCamera.Position.X - GetViewportRect().Size.X * .5f);
    }

    private bool HandleBattleCameraInput(InputEvent input)
    {
        var camera = _battleCamera ?? _mobileCamera;
        if (camera == null || _battlePaused || _battleEnded || _endlessCheckpointActive) return false;
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
        else if (_battleCamera != null && input is InputEventKey key && key.Pressed &&
            key.Keycode is Key.Left or Key.Right or Key.Home or Key.End)
        {
            SetBattleCameraX(key.Keycode switch
            {
                Key.Home => 0,
                Key.End => BattleWorldWidth,
                Key.Left => camera.Position.X - BattleScrollStep,
                _ => camera.Position.X + BattleScrollStep
            });
            GetViewport().SetInputAsHandled();
            return true;
        }
        else return false;

        if (_battleCamera != null) SetBattleCameraX(camera.Position.X + amount);
        else if (!_mobileOverview)
        {
            _mobilePointerDown = false;
            _mobileFollow = false;
            _mobileViewButton.Text = "Follow";
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
        else SetBattleCameraX(_battleCamera.Position.X - (motion.Position.X - _battleCameraPointer.X));
        _battleCameraPointer = motion.Position;
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void CleanupBattleCamera()
    {
        if (_battleCamera != null) GetViewport().SizeChanged -= RefreshBattleCamera;
    }

    private void DrawBattleBackground(Texture2D texture)
    {
        // The overview can see above and below the world; give that space a deliberate backdrop.
        DrawSetTransformMatrix(GetGlobalTransformWithCanvas().AffineInverse());
        DrawRect(GetViewportRect(), ResolveTerrainPalette().SkyColor.Darkened(.55f));
        DrawSetTransform(Vector2.Zero);
        // Mirror alternating panels to join existing landscape art without stretching it.
        var panelWidth = texture.GetWidth() * BattleWorldHeight / texture.GetHeight();
        for (var i = 0; i * panelWidth < BattleWorldWidth; i++)
        {
            var mirrored = i % 2 != 0;
            DrawSetTransform(new Vector2((i + (mirrored ? 1 : 0)) * panelWidth, 0), 0,
                new Vector2(mirrored ? -1 : 1, 1));
            DrawTextureRect(texture, new Rect2(0, 0, panelWidth, BattleWorldHeight), false);
        }
        DrawSetTransform(Vector2.Zero);
    }
}
