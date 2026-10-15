using System;
using System.Linq;
using Godot;

// The battle's touch controls, on every screen: the placement hint and Cancel, and the gestures (drag to pan, tap
// to place). The HUD keeps the battle concept's sizes; MobilePresentation.BattleHudScale scales it as a whole.
public partial class BattleController
{
    private Control _mobileHud;
    private Button _mobileCancelButton;
    private Label _mobilePlacementHint;
    private bool _mobilePointerDown, _mobileDragging;
    private int _mobilePointerId;
    private Vector2 _mobilePointerStart, _mobilePointerLast;
    private Action _mobileResize;
    private float _mobileFieldTop, _mobileFieldBottom;
    // Screen rows the meters (top) and the card tray (bottom) keep, at the HUD's own scale.
    private const float HudTopRows = 92f, HudCardRows = 168f;

    private void ConfigureMobileBattleUi(Control root, PanelContainer cards, HBoxContainer cardRow)
    {
        _mobileHud=root;
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        root.Scale=Vector2.One*MobilePresentation.BattleHudScale;
        _battleCamera=new Camera2D { Name="BattleCamera", Position=new Vector2(0,(BattlefieldTop+BattlefieldBottom)*.5f),
            PositionSmoothingEnabled=false };
        AddChild(_battleCamera); _battleCamera.MakeCurrent();
        _mobilePlacementHint=new Label { MouseFilter=Control.MouseFilterEnum.Ignore, ClipText=true };
        _mobilePlacementHint.AddThemeFontSizeOverride("font_size",20);
        root.AddChild(_mobilePlacementHint);
        _mobileCancelButton=new Button { Text="Cancel", Visible=false };
        MobilePresentation.TouchButton(_mobileCancelButton);
        _mobileCancelButton.Pressed+=ClearArmedSelection;
        root.AddChild(_mobileCancelButton);
        cards.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        cardRow.AddThemeConstantOverride("separation",6);
        // Horizontal scrolling supports unusually large decks without shrinking cards.
        var cardScroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode=ScrollContainer.ScrollMode.Disabled, CustomMinimumSize=new Vector2(0,102) };
        var oldScroll=(ScrollContainer)cardRow.GetParent(); var cardParent=oldScroll.GetParent(); cardRow.Reparent(cardScroll); cardParent.AddChild(cardScroll); oldScroll.QueueFree();
        cardRow.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        foreach(var button in cardRow.GetChildren().OfType<Button>())
        {
            button.CustomMinimumSize=new Vector2(112,96);
            button.ClipContents=true;
        }
        foreach(var button in _pauseOverlay.FindChildren("*","Button",true,false).OfType<Button>()) MobilePresentation.TouchButton(button);
        _endPanel.CustomMinimumSize=new Vector2(680,380);
        MobilePresentation.TouchButton(_endPrimaryButton); MobilePresentation.TouchButton(_endSecondaryButton);
        foreach(var scroll in _endPanel.FindChildren("*","ScrollContainer",true,false).OfType<ScrollContainer>()) scroll.CustomMinimumSize=new Vector2(0,150);
        // Checkpoint reports can be long. Scroll the report and choices together
        // instead of allowing the modal to grow beyond a phone's height.
        _draftPanel.CustomMinimumSize=new Vector2(680,400);
        var draftStack=(VBoxContainer)_draftLabel.GetParent();
        var draftParent=draftStack.GetParent();
        var draftScroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill, SizeFlagsVertical=Control.SizeFlags.ExpandFill };
        draftStack.Reparent(draftScroll); draftParent.AddChild(draftScroll);
        draftStack.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        foreach(var button in _draftButtons)
        {
            MobilePresentation.TouchButton(button);
            button.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        }
        // Modal overlays must stay above the touch hint and Cancel action.
        foreach(var overlay in root.GetChildren().OfType<CenterContainer>().ToArray()) root.MoveChild(overlay,-1);
        _mobileResize=() =>
        {
            if (!IsInstanceValid(root)) return;
            CancelCardDrag();
            CancelCouragePickupGesture();
            _mobilePointerDown=false;
            var scale=MobilePresentation.BattleHudScale;
            root.Size=GetViewportRect().Size/scale;
            var left=(SafeAreaService.Instance?.MarginLeft ?? 0)/scale+10;
            var right=(SafeAreaService.Instance?.MarginRight ?? 0)/scale+10;
            var top=(SafeAreaService.Instance?.MarginTop ?? 0)/scale+8;
            var bottom=(SafeAreaService.Instance?.MarginBottom ?? 0)/scale+8;
            var width=root.Size.X-left-right;
            _hudLayout?.Invoke();
            // The caravan spell's slot stands taller than the dock, so the hint and Cancel keep above it.
            var trayTop=Mathf.Min(cards.Position.Y,CaravanSpellTop);
            _mobilePlacementHint.Position=new Vector2(left,trayTop-29);
            _mobilePlacementHint.Size=new Vector2(width-120,26);
            _mobileCancelButton.Position=new Vector2(root.Size.X-right-102,trayTop-60);
            _mobileCancelButton.Size=new Vector2(102,56);
            var pauseVeil=_pauseOverlay.GetChildren().OfType<ColorRect>().FirstOrDefault();
            if(pauseVeil!=null) pauseVeil.CustomMinimumSize=root.Size;
            _mobileFieldTop=(top+HudTopRows)*scale;
            _mobileFieldBottom=(root.Size.Y-bottom-HudCardRows)*scale;
            _battleCamera.Zoom=Vector2.One*BattleFitZoom;
            ClampBattleCamera(_battleCamera);
            _battleCamera.ForceUpdateScroll();
        };
        GetViewport().SizeChanged+=_mobileResize;
        _mobileResize();
    }

    private void RefreshMobileHud()
    {
        if (_mobileHud==null) return;
        var armed=_selectionMode==BattleSelectionMode.Spell ? _spellDeck.ArmedSpell?.DisplayName : null;
        _mobilePlacementHint.Text=_cardDragging?"Release on the field · Return to the cards to cancel":
            string.IsNullOrEmpty(armed)?"":$"{armed} · Tap the field to cast";
        // A held card is cancelled by returning it to the bar; the button is for magic armed by keyboard.
        _mobileCancelButton.Visible=!_cardDragging && !string.IsNullOrEmpty(armed);
    }

    private Vector2 ScreenToBattle(Vector2 point) => GetGlobalTransformWithCanvas().AffineInverse()*point;

    private bool BeginMobileGesture(InputEvent input)
    {
        if (_battleCamera==null) return false;
        Vector2 point; int id;
        if(input is InputEventScreenTouch touch && touch.Pressed) { point=touch.Position; id=touch.Index; }
        else if(input is InputEventMouseButton mouse && mouse.ButtonIndex==MouseButton.Left && mouse.Pressed)
        { if(DisplayServer.IsTouchscreenAvailable()) return true; point=mouse.Position; id=-2; }
        else return false;
        if(_mobilePointerDown || point.Y<_mobileFieldTop || point.Y>_mobileFieldBottom) return true;
        _mobilePointerDown=true; _mobileDragging=false; _mobilePointerId=id;
        _mobilePointerStart=_mobilePointerLast=point;
        GetViewport().SetInputAsHandled();
        return true;
    }

    public override void _Input(InputEvent input)
    {
        if (_couragePickupPointerDown && HandleCouragePickupInput(input)) return;
        if (HandleBattleMenuInput(input)) return;
        if (HandleCaravanSpellKey(input)) return;
        if(HandleCardDragInput(input)) return;
        if(ContinueBattleCameraDrag(input)) return;
        if(HandleCouragePickupInput(input)) return;
        if(!_mobilePointerDown || _battleCamera==null) return;
        Vector2 point; bool released=false, canceled=false;
        if(input is InputEventScreenDrag drag && drag.Index==_mobilePointerId) point=drag.Position;
        else if(input is InputEventScreenTouch touch && !touch.Pressed && touch.Index==_mobilePointerId) { point=touch.Position; released=true; canceled=touch.Canceled; }
        else if(_mobilePointerId==-2 && input is InputEventMouseMotion motion) point=motion.Position;
        else if(_mobilePointerId==-2 && input is InputEventMouseButton mouse && !mouse.Pressed && mouse.ButtonIndex==MouseButton.Left) {point=mouse.Position; released=true; canceled=mouse.Canceled;}
        else return;
        GetViewport().SetInputAsHandled();
        if(canceled || _battlePaused || _battleEnded || _endlessCheckpointActive) { _mobilePointerDown=false; return; }
        _mobileDragging |= point.DistanceTo(_mobilePointerStart)>14;
        if(_mobileDragging)
        {
            SetBattleCameraFollow(false);
            _battleCamera.Position-=(point-_mobilePointerLast)/_battleCamera.Zoom;
            ClampBattleCamera(_battleCamera); _battleCamera.ForceUpdateScroll();
        }
        _mobilePointerLast=point;
        if(!released) return;
        if (_mobilePointerId >= 0) _cardMouseBlockUntil = Time.GetTicksMsec() + 250;
        _mobilePointerDown=false;
        var world=ScreenToBattle(point);
        if(!_mobileDragging && point.Y>=_mobileFieldTop && point.Y<=_mobileFieldBottom && IsInBattlefield(world)) TryUseSelectionAt(world);
    }

    public override void _Notification(int what)
    {
        // A system interruption may consume the release event entirely.
        if(what==NotificationApplicationPaused || what==NotificationApplicationFocusOut || what==NotificationWMWindowFocusOut)
        {
            CancelCardDrag();
            CancelCouragePickupGesture();
            _mobilePointerDown=false;
            _battleCameraDragging=false;
        }
    }

    private void CleanupMobilePresentation()
    {
        if(_mobileResize!=null) GetViewport().SizeChanged-=_mobileResize;
    }
}
