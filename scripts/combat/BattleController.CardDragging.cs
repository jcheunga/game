using System.Linq;
using Godot;

public partial class BattleController
{
    private Control _battleUiRoot, _cardDragGhost;
    private TextureRect _cardDragPortrait;
    private Label _cardDragHint;
    private Button _dragCardButton;
    private UnitDefinition _dragUnit;
    private SpellDefinition _dragSpell, _previousDragSpell;
    private BattleSelectionMode _previousDragMode;
    private ScrollContainer _dragCardScroll;
    private bool _cardPointerDown, _cardDragging, _cardScrolling;
    private int _cardPointerId;
    private int? _discardCardRelease;
    private ulong _cardMouseBlockUntil;
    private Vector2 _cardPointerStart, _cardPointerPosition, _cardPointerLast;

    private void BuildCardDragPreview(Control root)
    {
        _battleUiRoot = root;
        var layer = new CanvasLayer { Name = "CardDragLayer", Layer = 50 };
        AddChild(layer);
        _cardDragGhost = new Control { Name = "CardDragPreview", MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false, Size = new Vector2(248, 132), Theme = root.Theme };
        layer.AddChild(_cardDragGhost);
        _cardDragPortrait = new TextureRect { Position = new Vector2(5, 0), Size = new Vector2(76, 88),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        _cardDragGhost.AddChild(_cardDragPortrait);
        var plate = new PanelContainer { Position = new Vector2(0, 90), Size = new Vector2(248, 36),
            MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddThemeStyleboxOverride("panel", MedievalUi.Engraved("inset", 8, 3));
        _cardDragGhost.AddChild(plate);
        _cardDragHint = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _cardDragHint.AddThemeFontSizeOverride("font_size", 20);
        plate.AddChild(_cardDragHint);
    }

    private static bool ScreenPointIn(Control control, Vector2 point)
    {
        if (!control.IsVisibleInTree()) return false;
        return new Rect2(Vector2.Zero, control.Size).HasPoint(control.GetGlobalTransformWithCanvas().AffineInverse() * point);
    }

    // Input is captured before GUI dispatch, so use current geometry (including
    // scroll clipping), not the mouse-only/stale hovered-control cache on touch.
    private static Control CardHitControl(Control root, Vector2 point)
    {
        if (!root.IsVisibleInTree() || (root.ClipContents && !ScreenPointIn(root, point))) return null;
        foreach (var child in root.GetChildren().OfType<Control>().Reverse())
        {
            var hit = CardHitControl(child, point);
            if (hit != null) return hit;
        }
        return root.MouseFilter != Control.MouseFilterEnum.Ignore && ScreenPointIn(root, point) ? root : null;
    }

    private bool CardActionAvailable => !_battleEnded && !_battlePaused && !_endlessCheckpointActive && !_mobileClearView;

    private bool DragCardAffordable() => _dragSpell != null && _courage >= GameState.Instance.BuildSpellStats(_dragSpell).CourageCost
        && _spellDeck.GetCooldownRemaining(_dragSpell.Id) <= .05f;

    private bool CanDropCard(Vector2 screen)
    {
        if (!CardActionAvailable || !DragCardAffordable() || !GetViewportRect().HasPoint(screen)) return false;
        if (_mobileHud != null && (screen.Y < _mobileFieldTop || screen.Y > _mobileFieldBottom)) return false;
        return CardHitControl(_battleUiRoot, screen) == null && IsInBattlefield(ScreenToBattle(screen));
    }

    private bool HandleCardDragInput(InputEvent input)
    {
        if (_battleUiRoot == null) return false;
        if (input is InputEventMouse && Time.GetTicksMsec() < _cardMouseBlockUntil)
        { GetViewport().SetInputAsHandled(); return true; }
        if (_cardPointerDown && input is InputEventKey key)
        {
            if (key.Pressed && key.Keycode is Key.Escape or Key.Backspace) CancelCardDrag();
            GetViewport().SetInputAsHandled(); return true;
        }
        if (_cardPointerDown && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { CancelCardDrag(); GetViewport().SetInputAsHandled(); return true; }

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
                if (_cardPointerDown && input is InputEventMouse or InputEventGesture)
                { GetViewport().SetInputAsHandled(); return true; }
                return false;
        }

        if (!_cardPointerDown)
        {
            if (pressed && _discardCardRelease == id) _discardCardRelease = null;
            if (released && _discardCardRelease == id)
            { _discardCardRelease = null; GetViewport().SetInputAsHandled(); return true; }
            if (!pressed || canceled || !CardActionAvailable || _mobilePointerDown || _battleCameraDragging) return false;
            var hit = CardHitControl(_battleUiRoot, point) as Button;
            if (hit == null || hit.Disabled) return false;
            _dragUnit = _deploySlots.FirstOrDefault(s => s.Button == hit)?.Definition;
            _dragSpell = _spellSlots.FirstOrDefault(s => s.Button == hit)?.Definition;
            if (_dragUnit == null && _dragSpell == null) return false;
            _cardPointerDown = true; _cardDragging = _cardScrolling = false;
            AudioDirector.Instance?.PlayCardPickup();
            _cardPointerId = id; _cardPointerStart = _cardPointerLast = _cardPointerPosition = point;
            _dragCardButton = hit;
            _previousDragMode = _selectionMode; _previousDragSpell = _spellDeck.ArmedSpell;
            _dragCardScroll = null;
            for (var parent = hit.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) { _dragCardScroll = scroll; break; }
            _cardDragPortrait.Texture = hit.GetChildren().OfType<BattleActionCard>().Single().Portrait.Texture;
            hit.GrabFocus();
            GetViewport().SetInputAsHandled(); return true;
        }

        // One gesture owns input until release. Secondary fingers and synthetic
        // mouse events cannot start a camera pan or spend another card.
        GetViewport().SetInputAsHandled();
        if (id != _cardPointerId) return true;
        _cardPointerPosition = point;
        if (canceled || !CardActionAvailable)
        { CancelCardDrag(!released); return true; }
        var distance = point - _cardPointerStart;
        if (!_cardDragging && !_cardScrolling && distance.Length() > 14)
        {
            var bar = _dragCardScroll?.GetHScrollBar();
            _cardScrolling = bar != null && bar.MaxValue > bar.Page + 1
                && ScreenPointIn(_dragCardScroll, point)
                && Mathf.Abs(distance.X) > Mathf.Abs(distance.Y) * 1.25f;
            // Only magic is aimed. A unit card is a button: lifting off it cancels.
            _cardDragging = !_cardScrolling && _dragSpell != null;
            if (_cardDragging) ArmSpell(_dragSpell);
        }
        if (_cardScrolling)
        {
            var transform = _dragCardScroll.GetGlobalTransformWithCanvas().AffineInverse();
            _dragCardScroll.ScrollHorizontal -= Mathf.RoundToInt((transform * point - transform * _cardPointerLast).X);
        }
        _cardPointerLast = point;
        UpdateCardDragPreview();
        if (!released) return true;

        var drop = _cardDragging && CanDropCard(point);
        var tap = !_cardDragging && !_cardScrolling && ScreenPointIn(_dragCardButton, point);
        var unit = _dragUnit; var spell = _dragSpell; var world = ScreenToBattle(point);
        var scrolled = _cardScrolling;
        if (drop)
        {
            EndCardGesture(false, false);
            AudioDirector.Instance?.PlayCardDrop();
            // Reuse the authoritative targeting, resource and cooldown paths.
            TryCastSpellAt(spell, world);
        }
        else
        {
            EndCardGesture(true, false);
            if (tap) { if (unit != null) { AudioDirector.Instance?.PlayCardDrop(); DeployPlayerUnit(unit); } else ArmSpell(spell); }
            else if (!scrolled) AudioDirector.Instance?.PlayCardCancel();
        }
        return true;
    }

    private void CancelCardDrag(bool discardRelease = true)
    {
        if (_cardPointerDown) EndCardGesture(true, discardRelease);
    }

    private void EndCardGesture(bool restoreSelection, bool discardRelease)
    {
        if (discardRelease) _discardCardRelease = _cardPointerId;
        if (_cardPointerId >= 0) _cardMouseBlockUntil = Time.GetTicksMsec() + 250;
        if (restoreSelection && _cardDragging)
        {
            _selectionMode = _previousDragMode;
            if (_previousDragSpell != null) _spellDeck.Arm(_previousDragSpell); else _spellDeck.Disarm();
        }
        _cardPointerDown = _cardDragging = _cardScrolling = false;
        _dragCardButton = null; _dragUnit = null; _dragSpell = null; _dragCardScroll = null;
        _cardDragGhost.Hide(); _cardDragPortrait.Texture = null;
        UpdateHud();
    }

    private void UpdateCardDragPreview()
    {
        if (!_cardPointerDown) return;
        if (!CardActionAvailable) { CancelCardDrag(); return; }
        _cardDragGhost.Visible = _cardDragging;
        if (!_cardDragging) return;
        var valid = CanDropCard(_cardPointerPosition);
        _cardDragPortrait.Modulate = valid ? new Color("d8ffebcc") : new Color("ffb5a2aa");
        _cardDragHint.Text = !DragCardAffordable() ? "Unavailable · cancel" : !valid ? "Move onto the battlefield" :
            "Release · " + _dragSpell.DisplayName;
        var scale = _mobileHud != null ? 1.4f : 1f;
        _cardDragGhost.Scale = Vector2.One * scale;
        var size = _cardDragGhost.Size * scale;
        var position = _cardPointerPosition + new Vector2(20, -142) * scale;
        var viewport = GetViewportRect().Size;
        _cardDragGhost.Position = new Vector2(Mathf.Clamp(position.X, 8, Mathf.Max(8, viewport.X - size.X - 8)),
            Mathf.Clamp(position.Y, 8, Mathf.Max(8, viewport.Y - size.Y - 8)));
    }
}
