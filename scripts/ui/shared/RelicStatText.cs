using System;
using System.Collections.Generic;

public static class RelicStatText
{
    public static IReadOnlyList<ArmoryDetailUi.Stat> Stats(EquipmentDefinition relic)
    {
        var rows=new List<ArmoryDetailUi.Stat>();
        void Scale(string icon,string label,float value)
        {
            if(Math.Abs(value-1f)>.0001f)rows.Add(new(icon,label,$"{(value>=1?"+":"")}{(value-1)*100:0.##}%"));
        }
        void Points(string icon,string label,float value,string suffix="")
        {
            if(Math.Abs(value)>.0001f)rows.Add(new(icon,label,$"{(value>=0?"+":"")}{value:0.##}{suffix}"));
        }
        Scale("sword","Damage",relic.DamageScale);
        Scale("heart","Health",relic.HealthScale);
        Points("shield","Armor",relic.ArmorBonus);
        Points("shield","Phys resist",relic.PhysicalResistanceBonus," pp");
        Points("crystal","Magic resist",relic.MagicalResistanceBonus," pp");
        Points("sword","Crit chance",relic.CriticalChanceBonus," pp");
        Points("sword","Crit bonus",relic.CriticalDamageBonus," pp");
        Points("clock","Faster attacks",relic.AttackIntervalReductionPct,"%");
        if(relic.CooldownReduction>0)rows.Add(new("clock","Attack interval",$"−{relic.CooldownReduction:0.##}s"));
        Scale("arrow","Speed",relic.SpeedScale);
        Scale("eye","Reach",relic.RangeScale);
        Scale("hammer","Gate damage",relic.StructureDamageScale);
        Points("hammer","Gate damage",relic.BaseDamageBonus);
        if(relic.Id=="relic_banner_ribbon")rows.Add(new("people","Aura reach","+5%"));
        return rows;
    }

    public static string LimitNotice(UnitBonusLimit limit)
    {
        var suffix=limit.Unit=="%"?"%":" points";
        return $"{limit.Label}: {limit.Supplied:+0.##;-0.##;0}{suffix} supplied; {limit.Applied:+0.##;-0.##;0}{suffix} applied. {Math.Abs(limit.Excess):0.##}{suffix} {(limit.Excess>0?"exceeds the limit":"is below the minimum")}.";
    }
}
