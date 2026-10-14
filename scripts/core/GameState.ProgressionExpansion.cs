using System.Collections.Generic;

public sealed record AwakeningPreview(UnitDefinition Unit,int Stars,AwakeningLevel Next,UnitStats Current,UnitStats After,IReadOnlyList<UnitBonusLimit> Limits);

public partial class GameState
{
    public const int AwakeningTokenGoldCost=100,AwakeningTokenEssenceCost=2;
    public bool TryTrainAwakeningToken(string unitId,out string reason)
    {
        reason="";
        if(!IsUnitOwned(unitId)){reason="Recruit this ally first.";return false;}
        if(GetUnitStarLevel(unitId)>=AwakeningCatalog.MaxStars){reason="Maximum awakening reached.";return false;}
        if(Gold<AwakeningTokenGoldCost || Essence<AwakeningTokenEssenceCost){reason="A token costs 100 gold and 2 Essence.";return false;}
        Gold-=AwakeningTokenGoldCost;Essence-=AwakeningTokenEssenceCost;
        GrantUnitTokens(unitId,1);reason="Trained one awakening token.";return true;
    }
    public AwakeningPreview BuildAwakeningPreview(string unitId)
    {
        var unit=GameData.GetUnit(unitId);var stars=GetUnitStarLevel(unitId);var next=AwakeningCatalog.GetNextLevel(stars);
        var bonuses=CollectPlayerUnitBonuses(unit,GetActiveDeckUnits(),GetUnitEquipment(unitId),GetUnitEquipment2(unitId),awakeningStars:next?.Stars??stars);
        return new AwakeningPreview(unit,stars,next,BuildPlayerUnitStats(unit),ResolvePlayerUnitStats(unit,GetUnitLevel(unitId),bonuses),bonuses.GetLimitNotices());
    }
}
