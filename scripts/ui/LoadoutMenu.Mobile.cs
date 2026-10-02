using System.Linq;
using Godot;

public partial class LoadoutMenu
{
    private void BuildMobileUi()
    {
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
        var briefing=RealmUi.Button("book","Briefing",ShowMobileBriefing);
        MobilePresentation.TouchButton(briefing); header.AddChild(briefing);
        var edit=RealmUi.Button("sword","Edit squad",()=>SceneRouter.Instance.GoToShop());
        MobilePresentation.TouchButton(edit); header.AddChild(edit);

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
            card.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(140, 150)));
            var level=RealmUi.Label($"Level {GameState.Instance.GetUnitLevel(unit.Id)} · {unit.Cost} courage",20,true);
            card.AddChild(level);
            var inspect=RealmUi.Button("eye","Details",()=>ModelShowcase.Show(this,roster,unit.Id));
            MobilePresentation.TouchButton(inspect); card.AddChild(inspect);
        }

        var footer=new HBoxContainer(); footer.AddThemeConstantOverride("separation",12); stack.AddChild(footer);
        var spells=string.Join(" · ",GameState.Instance.GetActiveDeckSpells().Select(s=>s.DisplayName));
        _status=RealmUi.Label(string.IsNullOrEmpty(spells)?"Ready":$"Rites: {spells}",20,true);
        _status.VerticalAlignment=VerticalAlignment.Center; footer.AddChild(_status);
        var canDeploy=GameState.Instance.CanStartCampaignBattle(_stage.StageNumber,out var reason);
        var deploy=RealmUi.Button("flag",$"Deploy · {GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)} food",()=>
        {
            if(!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber,out var message)) {_status.Text=message;return;}
            GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToBattle();
        },true);
        MobilePresentation.TouchButton(deploy); deploy.Disabled=!canDeploy; _deployButton=deploy; footer.AddChild(deploy);
        if(!canDeploy) _status.Text=reason;
    }

    private void ShowMobileBriefing()
    {
        var spells=string.Join("\n\n",GameState.Instance.GetActiveDeckSpells().Select(SpellText.BuildInlineSummary));
        MobilePresentation.ShowReport(this,_stage.StageName,
            StageObjectives.BuildSummaryText(_stage,GameState.Instance.GetStageStars(_stage.StageNumber))+"\n\n"+
            StageEncounterIntel.BuildCampaignEncounterIntel(_stage)+"\n\n"+
            StageModifiers.BuildSummaryText(_stage)+"\n\n"+StageHazards.BuildSummaryText(_stage)+"\n\n"+
            GameState.Instance.BuildCampaignDirectiveStatusText(_stage.StageNumber)+"\n\n"+
            AdventureMapCatalog.Leader(_stage.StageNumber).Description+"\n\n"+_stage.Description+"\n\n"+
            StageMissionEvents.BuildCampaignSummaryText(_stage)+"\n\n"+spells+"\n\n"+
            GameState.Instance.BuildActiveDeckSynergySummary());
    }
}
