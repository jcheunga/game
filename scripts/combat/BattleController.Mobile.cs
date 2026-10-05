using System;
using System.Linq;
using Godot;

public partial class BattleController
{
    private Camera2D _mobileCamera;
    private Control _mobileHud;
    private Button _mobileViewButton, _mobileCancelButton, _mobileZoomButton, _mobileClearButton;
    private Label _mobilePlacementHint;
    private bool _mobileOverview, _mobileFollow = true;
    private bool _mobileClearView;
    // Combat zoom steps relative to the fit that shows the whole band between the HUD rows.
    private static readonly float[] MobileZoomSteps = { 1f, 1.25f, 1.5f };
    private int _mobileZoomStep;
    private float MobileCombatZoom => BattleFitZoom * MobileZoomSteps[_mobileZoomStep];
    private Vector2 _mobileClosePosition;
    private bool _mobilePointerDown, _mobileDragging;
    private int _mobilePointerId;
    private Vector2 _mobilePointerStart, _mobilePointerLast;
    private Action _mobileResize;
    private float _mobileFieldTop, _mobileFieldBottom;

    private void ConfigureMobileBattleUi(Control root, PanelContainer cards, HBoxContainer cardRow)
    {
        if (!MobilePresentation.Enabled) return;
        _mobileHud=root;
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        root.Scale=Vector2.One*MobilePresentation.HudScale;
        _mobileCamera=new Camera2D { Name="MobileBattleCamera", Position=new Vector2(0,(BattlefieldTop+BattlefieldBottom)*.5f),
            PositionSmoothingEnabled=false };
        AddChild(_mobileCamera); _mobileCamera.MakeCurrent();
        var views = new HBoxContainer(); views.AddThemeConstantOverride("separation", 6); root.AddChild(views);
        _mobileViewButton=RealmUi.IconButton("map", "Battlefield overview or follow combat", ToggleMobileOverview);
        _mobileZoomButton=RealmUi.IconButton("eye", "Change combat zoom", CycleMobileZoom);
        _mobileClearButton=RealmUi.IconButton("people", "Toggle clear combat view", ToggleMobileClearView);
        foreach(var button in new[] {_mobileViewButton,_mobileZoomButton,_mobileClearButton})
        { MobilePresentation.TouchButton(button); views.AddChild(button); }
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
            _mobilePointerDown=false;
            var scale=MobilePresentation.HudScale;
            root.Size=GetViewportRect().Size/scale;
            var left=(SafeAreaService.Instance?.MarginLeft ?? 0)/scale+10;
            var right=(SafeAreaService.Instance?.MarginRight ?? 0)/scale+10;
            var top=(SafeAreaService.Instance?.MarginTop ?? 0)/scale+8;
            var bottom=(SafeAreaService.Instance?.MarginBottom ?? 0)/scale+8;
            var width=root.Size.X-left-right;
            _hudLayout?.Invoke();
            views.Position=new Vector2(root.Size.X-right-views.GetCombinedMinimumSize().X,top+50);
            cards.Visible=!_mobileClearView;
            _mobilePlacementHint.Position=new Vector2(left,_mobileClearView?root.Size.Y-bottom-28:cards.Position.Y-29);
            _mobilePlacementHint.Size=new Vector2(width-120,26);
            _mobileCancelButton.Position=new Vector2(root.Size.X-right-102,cards.Position.Y-60);
            _mobileCancelButton.Size=new Vector2(102,56);
            var pauseVeil=_pauseOverlay.GetChildren().OfType<ColorRect>().FirstOrDefault();
            if(pauseVeil!=null) pauseVeil.CustomMinimumSize=root.Size;
            _mobileFieldTop=(top+70)*scale;
            _mobileFieldBottom=(_mobileClearView?root.Size.Y-bottom-32:cards.Position.Y-30)*scale;
            _mobileCamera.Zoom=Vector2.One*(_mobileOverview?MobileOverviewZoom:MobileCombatZoom);
            ClampMobileCamera();
            _mobileCamera.ForceUpdateScroll();
        };
        GetViewport().SizeChanged+=_mobileResize;
        _mobileResize();
    }

    private void RefreshMobileHud()
    {
        if (_mobileHud==null) return;
        var armed=_selectionMode==BattleSelectionMode.Spell ? _spellDeck.ArmedSpell?.DisplayName : null;
        _mobilePlacementHint.Text=_mobileClearView?"View only · Drag to explore · Tap Cards to deploy":
            _cardDragging?"Release on the field · Return to the cards to cancel":
            string.IsNullOrEmpty(armed)?"":$"{armed} · Tap the field to cast";
        _mobileCancelButton.Visible=!_mobileClearView && !string.IsNullOrEmpty(armed);
    }

    private void ToggleMobileOverview()
    {
        _mobilePointerDown=false;
        if (!_mobileFollow && !_mobileOverview) _mobileFollow=true;
        else if(_mobileOverview)
        {
            _mobileOverview=false; _mobileFollow=true;
            _mobileCamera.Position=_mobileClosePosition;
        }
        else { _mobileClosePosition=_mobileCamera.Position; _mobileOverview=true; }
        _mobileViewButton.Icon=RealmUi.Icon(_mobileOverview?"sword":"map");
        _mobileViewButton.TooltipText=_mobileOverview?"Return to close combat":"Show the whole battlefield";
        _mobileCamera.Zoom=Vector2.One*(_mobileOverview?MobileOverviewZoom:MobileCombatZoom);
        if (_mobileOverview) _mobileCamera.Position=new Vector2(BattleWorldWidth*.5f,(FrameTop+FrameBottom)*.5f);
        else _mobileFollow=true;
        ClampMobileCamera(); _mobileCamera.ForceUpdateScroll();
    }

    // A hand pan stops tracking; the view button then offers to resume it.
    private void PauseMobileFollow()
    {
        _mobileFollow=false;
        _mobileViewButton.Icon=RealmUi.Icon("sword");
        _mobileViewButton.TooltipText="Follow the fighting";
    }

    private Vector2 MobileVisibleCenter => new(GetViewportRect().Size.X*.5f,(_mobileFieldTop+_mobileFieldBottom)*.5f);
    private float MobileOverviewZoom => Mathf.Min(GetViewportRect().Size.X / BattleWorldWidth, BattleFitZoom);

    private void CycleMobileZoom()
    {
        _mobilePointerDown=false;
        if(_mobileOverview) ToggleMobileOverview();
        var focus=ScreenToBattle(MobileVisibleCenter);
        _mobileZoomStep=(_mobileZoomStep+1)%MobileZoomSteps.Length;
        _mobileCamera.Zoom=Vector2.One*MobileCombatZoom;
        _mobileCamera.Position=focus-(MobileVisibleCenter-GetViewportRect().Size*.5f)/MobileCombatZoom;
        ClampMobileCamera(); _mobileCamera.ForceUpdateScroll();
    }

    private void ToggleMobileClearView()
    {
        var focus=ScreenToBattle(MobileVisibleCenter);
        _mobileClearView=!_mobileClearView;
        _mobileClearButton.Icon=RealmUi.Icon(_mobileClearView?"people":"eye");
        _mobileClearButton.TooltipText=_mobileClearView?"Restore deployment cards":"Hide cards for a clear view";
        _mobileResize(); RefreshMobileHud();
        if(!_mobileOverview)
            _mobileCamera.Position=focus-(MobileVisibleCenter-GetViewportRect().Size*.5f)/MobileCombatZoom;
        ClampMobileCamera(); _mobileCamera.ForceUpdateScroll();
    }

    private void ClampMobileCamera()
    {
        if (_mobileCamera==null) return;
        if (_mobileOverview) _mobileCamera.Zoom=Vector2.One*MobileOverviewZoom;
        ClampBattleCamera(_mobileCamera);
    }

    private Vector2 ScreenToBattle(Vector2 point) => GetGlobalTransformWithCanvas().AffineInverse()*point;

    private bool BeginMobileGesture(InputEvent input)
    {
        if (_mobileCamera==null) return false;
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
        if (HandleBattleMenuInput(input)) return;
        if(HandleCardDragInput(input)) return;
        if(ContinueBattleCameraDrag(input)) return;
        if(!_mobilePointerDown || _mobileCamera==null) return;
        Vector2 point; bool released=false, canceled=false;
        if(input is InputEventScreenDrag drag && drag.Index==_mobilePointerId) point=drag.Position;
        else if(input is InputEventScreenTouch touch && !touch.Pressed && touch.Index==_mobilePointerId) { point=touch.Position; released=true; canceled=touch.Canceled; }
        else if(_mobilePointerId==-2 && input is InputEventMouseMotion motion) point=motion.Position;
        else if(_mobilePointerId==-2 && input is InputEventMouseButton mouse && !mouse.Pressed && mouse.ButtonIndex==MouseButton.Left) {point=mouse.Position; released=true; canceled=mouse.Canceled;}
        else return;
        GetViewport().SetInputAsHandled();
        if(canceled || _battlePaused || _battleEnded || _endlessCheckpointActive) { _mobilePointerDown=false; return; }
        _mobileDragging |= point.DistanceTo(_mobilePointerStart)>14;
        if(_mobileDragging && !_mobileOverview)
        {
            PauseMobileFollow();
            _mobileCamera.Position-=(point-_mobilePointerLast)/_mobileCamera.Zoom;
            ClampMobileCamera(); _mobileCamera.ForceUpdateScroll();
        }
        _mobilePointerLast=point;
        if(!released) return;
        _mobilePointerDown=false;
        var world=ScreenToBattle(point);
        if(!_mobileClearView && !_mobileDragging && point.Y>=_mobileFieldTop && point.Y<=_mobileFieldBottom && IsInBattlefield(world)) TryUseSelectionAt(world);
    }

    public override void _Notification(int what)
    {
        // A system interruption may consume the release event entirely.
        if(what==NotificationApplicationPaused || what==NotificationApplicationFocusOut || what==NotificationWMWindowFocusOut)
        {
            CancelCardDrag();
            _mobilePointerDown=false;
            _battleCameraDragging=false;
        }
    }

    private void CleanupMobilePresentation()
    {
        if(_mobileResize!=null) GetViewport().SizeChanged-=_mobileResize;
    }
}
