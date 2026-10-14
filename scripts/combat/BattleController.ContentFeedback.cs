using System.Collections.Generic;
using Godot;

public partial class BattleController
{
    private readonly Queue<UnitDefinition> _enemyLessons=new();
    private PanelContainer _enemyLesson;
    private float _enemyLessonRemaining;
    private void TeachEnemy(UnitDefinition definition,bool previouslySeen)
    {
        if(previouslySeen || !GameState.Instance.ShowHints || definition.NonCombatant)return;
        _enemyLessons.Enqueue(definition);
    }
    private void TickEnemyLessons(float delta)
    {
        if(_enemyLesson!=null && (_enemyLessonRemaining-=delta)<=0){_enemyLesson.QueueFree();_enemyLesson=null;}
        if(_enemyLesson!=null || _enemyLessons.Count==0 || _battleHudRoot==null)return;
        var unit=_enemyLessons.Dequeue();
        var area=GetViewportRect().Size/_battleHudRoot.Scale.X;var width=Mathf.Min(500,area.X-32);
        _enemyLesson=new PanelContainer {Name="EnemyLesson",Position=new Vector2((area.X-width)/2,90),Size=new Vector2(width,0),MouseFilter=Control.MouseFilterEnum.Ignore};
        _enemyLesson.AddThemeStyleboxOverride("panel",new ModalSurface(ModalMaterial.Steel,8));
        _battleHudRoot.AddChild(_enemyLesson);
        var body=new VBoxContainer();_enemyLesson.AddChild(body);
        body.AddChild(RealmUi.SectionTitle("First sighting · "+unit.DisplayName,20));
        body.AddChild(RealmUi.Label(unit.Description,18));
        body.AddChild(RealmUi.Label("Counter: "+(string.IsNullOrEmpty(unit.Weakness)?"Protect your melee line and watch the amber attack warnings.":unit.Weakness),18,true));
        _enemyLessonRemaining=8;
    }
    private void ClearContentCast(Unit unit)
    {
        if(_contentCasts.Remove(unit,out var cast))cast.Warning?.QueueFree();
        unit.MarkedTime=0;
    }
}

public partial class BattleContentWarning : Node2D
{
    public string Label="CAST";
    public float Progress;
    public Color Tint=new("e8b876");
    public override void _Draw()
    {
        DrawRect(new Rect2(-32,-12,64,24),new Color(Tint,.15f));
        DrawRect(new Rect2(-32,-12,64,24),new Color(Tint,.75f),false,2);
        DrawLine(new Vector2(-25,15),new Vector2(-25+50*Progress,15),Tint,3,true);
        DrawString(RoyalFonts.Body(600),new Vector2(-28,-17),Label,fontSize:13,modulate:Tint);
    }
}
