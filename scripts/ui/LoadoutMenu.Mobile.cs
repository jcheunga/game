using System.Linq;
using Godot;

public partial class LoadoutMenu
{
    private void BuildMobileUi()
    {
        if (RealmModal.Embedded(this)) { BuildMobilePreparation(); return; }
        var route=RouteCatalog.Get(_stage.MapId);
        MenuBackdropComposer.AddSolidBackdrop(this,"loadout",new Color("101d26"),route.Id);
        var canvas=new ResponsiveUiCanvas {Name="MobileLoadout"}; AddChild(canvas);
        var stack=new VBoxContainer(); stack.AddThemeConstantOverride("separation",8); canvas.Content.AddChild(stack);
        var header=new HBoxContainer(); stack.AddChild(header);
        var back=RealmUi.IconButton("back","Return to map",()=>SceneRouter.Instance.GoToMap());
        MobilePresentation.TouchButton(back); header.AddChild(back);
        var title=RealmUi.Heading($"{_stage.StageNumber:00} · {_stage.StageName}",26);
        title.AutowrapMode=TextServer.AutowrapMode.Off; title.ClipText=true;
        title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
        title.VerticalAlignment=VerticalAlignment.Center; header.AddChild(title);
        var edit=RealmUi.Button("sword","Edit squad",()=>SceneRouter.Instance.GoToShop());
        MobilePresentation.TouchButton(edit); header.AddChild(edit);
        stack.AddChild(BuildVictoryRewards(compact: true));

        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,
            VerticalScrollMode=ScrollContainer.ScrollMode.Disabled,HorizontalScrollMode=ScrollContainer.ScrollMode.Auto};
        stack.AddChild(scroll);
        var cards=new HBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};
        cards.AddThemeConstantOverride("separation",8); scroll.AddChild(cards);
        var roster=GameState.Instance.GetActiveDeckUnits().ToArray();
        foreach(var unit in roster)
        {
            var frame=new PanelContainer {CustomMinimumSize=new Vector2(238,0),SizeFlagsHorizontal=SizeFlags.ExpandFill};
            frame.AddThemeStyleboxOverride("panel",MedievalUi.Engraved("engraved_panel",10,8)); cards.AddChild(frame);
            var card=new VBoxContainer(); frame.AddChild(card);
            var name=RealmUi.Label(unit.DisplayName,20); name.ClipText=true;
            name.AutowrapMode=TextServer.AutowrapMode.Off; card.AddChild(name);
            card.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(140, 120)));
            var level=RealmUi.Label($"Level {GameState.Instance.GetUnitLevel(unit.Id)} · {unit.Cost} courage",20,true);
            card.AddChild(level);
            var inspect=RealmUi.Button("eye","Details",()=>ModelShowcase.Show(this,roster,unit.Id));
            MobilePresentation.TouchButton(inspect); card.AddChild(inspect);
        }

        var footer=new HBoxContainer(); footer.AddThemeConstantOverride("separation",12); stack.AddChild(footer);
        var spells=string.Join(" · ",GameState.Instance.GetActiveDeckSpells().Select(s=>s.DisplayName));
        _status=RealmUi.Label(string.IsNullOrEmpty(spells)?"Ready":$"Spells: {spells}",20,true);
        _status.VerticalAlignment=VerticalAlignment.Center; footer.AddChild(_status);
        var canDeploy=GameState.Instance.CanStartCampaignBattle(_stage.StageNumber,out var reason);
        var deploy=RealmUi.Button("flag","Deploy",()=>
        {
            if(!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber,out var message)) {_status.Text=message;return;}
            GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToBattle();
        },true);
        HomeResourceUi.SetEntryCost(deploy,GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber));
        MobilePresentation.TouchButton(deploy); deploy.Disabled=!canDeploy; _deployButton=deploy; footer.AddChild(deploy);
        if(!canDeploy) _status.Text=reason;
    }

    private void BuildMobilePreparation()
    {
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 8);
        AddChild(stack); stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(BuildVictoryRewards(compact: true));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        stack.AddChild(scroll);
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 10); scroll.AddChild(cards);
        var roster = GameState.Instance.GetActiveDeckUnits().ToArray();
        foreach (var unit in roster)
        {
            var button = new RealmButton { CustomMinimumSize = new Vector2(140, 108),
                TooltipText = $"{unit.DisplayName} · Level {GameState.Instance.GetUnitLevel(unit.Id)} · {unit.Cost} courage",
                AccessibilityName = $"View {unit.DisplayName}" };
            button.Pressed += () => ModelShowcase.Show(this, roster, unit.Id);
            ModalUi.StyleButton(button); cards.AddChild(button);
            var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(padding); padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right" }) padding.AddThemeConstantOverride("margin_" + side, 8);
            foreach (var side in new[] { "top", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 4);
            var card = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; padding.AddChild(card);
            card.AddChild(new TextureRect { Texture = UiArtLoader.TryLoadUnitIcon(unit), CustomMinimumSize = new Vector2(80, 64),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore });
            var name = RealmUi.Label(unit.DisplayName, 17); name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.Off; name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.MouseFilter = MouseFilterEnum.Ignore; card.AddChild(name);
        }
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 12); stack.AddChild(footer);
        var edit = RealmUi.Button("sword", "Warband", () => SceneRouter.Instance.GoToShop()); footer.AddChild(edit);
        _status = RealmUi.Label("", 16, true); _status.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(_status);
        _deployButton = RealmUi.Button("flag", "Deploy", () => {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out var reason)) { _status.Text = reason; return; }
            GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToBattle();
        }, true);
        HomeResourceUi.SetEntryCost(_deployButton, GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)); footer.AddChild(_deployButton);
        _deployButton.CustomMinimumSize = new Vector2(190, 48);
        RealmModal.Polish(stack);
    }

}
