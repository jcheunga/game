using System;
using System.Collections.Generic;

public partial class GameState
{
    public bool CanEquipRelic(string unitId,string equipmentId,int slot,out string reason)
    {
        reason="";
        if(slot is not (1 or 2)) {reason="Choose a relic slot.";return false;}
        if(string.IsNullOrWhiteSpace(unitId) || !IsUnitOwned(unitId)) {reason="Recruit this ally first.";return false;}
        if(slot==2 && !IsUnitPromoted(unitId)) {reason="Promote this ally to unlock the second slot.";return false;}
        if(string.IsNullOrWhiteSpace(equipmentId) || !_ownedEquipmentIds.Contains(equipmentId))
        {reason="Acquire this relic first.";return false;}
        try {GameData.GetEquipment(equipmentId);}
        catch(InvalidOperationException) {reason="This relic is unavailable.";return false;}
        foreach(var pair in _unitEquipmentSlots)
            if(pair.Value.Equals(equipmentId,StringComparison.OrdinalIgnoreCase) &&
                (slot!=1 || !pair.Key.Equals(unitId,StringComparison.OrdinalIgnoreCase)))
            {reason=RelicAssignmentReason(pair.Key,1);return false;}
        foreach(var pair in _unitEquipmentSlot2)
            if(pair.Value.Equals(equipmentId,StringComparison.OrdinalIgnoreCase) &&
                (slot!=2 || !pair.Key.Equals(unitId,StringComparison.OrdinalIgnoreCase)))
            {reason=RelicAssignmentReason(pair.Key,2);return false;}
        return true;
    }

    private static string RelicAssignmentReason(string unitId,int slot)
    {
        var name=unitId;
        try {name=GameData.GetUnit(unitId).DisplayName;} catch(InvalidOperationException) {}
        return $"Already equipped on {name} in slot {slot}. Remove it there first.";
    }

    public RelicEquipmentPreview BuildRelicEquipmentPreview(string unitId,EquipmentDefinition candidate,int slot=1)
    {
        if(slot is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(slot));
        var unit=GameData.GetUnit(unitId);
        var first=GetUnitEquipment(unitId);
        var second=GetUnitEquipment2(unitId);
        var replaced=slot==1?first:second;
        var supplied=CollectPlayerUnitBonuses(unit,GetActiveDeckUnits(),slot==1?candidate:first,slot==2?candidate:second);
        string reason;
        var canApply=candidate!=null ? CanEquipRelic(unitId,candidate.Id,slot,out reason)
            : CanRemoveRelic(unitId,slot,out reason);
        return new RelicEquipmentPreview(unit,slot,replaced,candidate,BuildPlayerUnitStats(unit),
            ResolvePlayerUnitStats(unit,GetUnitLevel(unitId),supplied),supplied.GetLimitNotices(),canApply,reason);
    }

    private bool CanRemoveRelic(string unitId,int slot,out string reason)
    {
        reason="";
        if(slot is not (1 or 2)) {reason="Choose a relic slot.";return false;}
        if(!IsUnitOwned(unitId)) {reason="Recruit this ally first.";return false;}
        if(slot==2 && !IsUnitPromoted(unitId)) {reason="Promote this ally to unlock the second slot.";return false;}
        if((slot==1?GetUnitEquipment(unitId):GetUnitEquipment2(unitId))==null) {reason="This slot is empty.";return false;}
        return true;
    }

    public bool TryUnequipRelic(string unitId,int slot,out string reason)
    {
        if(!CanRemoveRelic(unitId,slot,out reason))return false;
        if(slot==1)UnequipItem(unitId);else UnequipItem2(unitId);
        return true;
    }

    public IReadOnlyList<EquipmentDefinition> GetBattleEquipment(string unitId)
    {
        var result=new List<EquipmentDefinition>();
        var first=GetUnitEquipment(unitId);
        if(first!=null) result.Add(first);
        if(_promotedUnitIds.Contains(unitId))
        {
            var second=GetUnitEquipment2(unitId);
            if(second!=null) result.Add(second);
        }
        return result;
    }
}
