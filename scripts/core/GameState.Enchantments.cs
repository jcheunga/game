using System;
using System.Collections.Generic;

public sealed record EnchantmentWearerPreview(UnitDefinition Unit,int Slot,UnitStats Current,UnitStats After,IReadOnlyList<UnitBonusLimit> Limits);
public sealed record RelicEnchantmentPreview(EquipmentDefinition Relic,EnchantmentDefinition Current,
    EnchantmentDefinition Candidate,IReadOnlyList<EnchantmentWearerPreview> Wearers,bool CanApply,string Reason)
{
    public int GoldCost=>Candidate?.GoldCost??0;
    public int EssenceCost=>Candidate?.EssenceCost??0;
}

public partial class GameState
{
    public bool CanApplyEnchantment(string relicId,string enchantmentId,out string message)
    {
        message="";
        if(string.IsNullOrWhiteSpace(relicId) || !_ownedEquipmentIds.Contains(relicId))
        {message="Acquire this relic first.";return false;}
        try {GameData.GetEquipment(relicId);}
        catch(InvalidOperationException) {message="This relic is unavailable.";return false;}
        var enchantment=EnchantmentCatalog.GetById(enchantmentId);
        if(enchantment==null) {message="Choose an enchantment.";return false;}
        if(string.Equals(GetRelicEnchantmentId(relicId),enchantment.Id,StringComparison.OrdinalIgnoreCase))
        {message="This enchantment is already applied.";return false;}
        if(Gold<enchantment.GoldCost) {message=$"Need {enchantment.GoldCost} gold (have {Gold}).";return false;}
        if(Essence<enchantment.EssenceCost) {message=$"Need {enchantment.EssenceCost} Essence (have {Essence}).";return false;}
        return true;
    }

    private bool CanRemoveEnchantment(string relicId,out string message)
    {
        message="";
        if(string.IsNullOrWhiteSpace(relicId) || !_ownedEquipmentIds.Contains(relicId))
        {message="Acquire this relic first.";return false;}
        if(GetRelicEnchantment(relicId)==null) {message="This relic has no enchantment.";return false;}
        return true;
    }

    public bool TryRemoveEnchantment(string relicId,out string message)
    {
        if(!CanRemoveEnchantment(relicId,out message))return false;
        _relicEnchantments.Remove(relicId);Persist();message="Enchantment removed. The relic stays in your collection.";return true;
    }

    public RelicEnchantmentPreview BuildRelicEnchantmentPreview(string relicId,string enchantmentId)
    {
        var relic=GameData.GetEquipment(relicId);
        var candidate=EnchantmentCatalog.GetById(enchantmentId);
        var removing=string.IsNullOrWhiteSpace(enchantmentId);
        string reason;
        var canApply=removing?CanRemoveEnchantment(relicId,out reason):CanApplyEnchantment(relicId,enchantmentId,out reason);
        var wearers=new List<EnchantmentWearerPreview>();
        foreach(var unit in GetOwnedPlayerUnits())
        {
            var first=GetUnitEquipment(unit.Id);var second=GetUnitEquipment2(unit.Id);
            var slot=string.Equals(first?.Id,relicId,StringComparison.OrdinalIgnoreCase)?1:
                string.Equals(second?.Id,relicId,StringComparison.OrdinalIgnoreCase)?2:0;
            if(slot==0)continue;
            var bonuses=CollectPlayerUnitBonuses(unit,GetActiveDeckUnits(),first,second,
                enchantmentRelicId:relicId,enchantmentOverride:removing?null:candidate??GetRelicEnchantment(relicId));
            wearers.Add(new EnchantmentWearerPreview(unit,slot,BuildPlayerUnitStats(unit),
                ResolvePlayerUnitStats(unit,GetUnitLevel(unit.Id),bonuses),bonuses.GetLimitNotices()));
        }
        return new RelicEnchantmentPreview(relic,GetRelicEnchantment(relicId),candidate,wearers,canApply,reason);
    }
}
