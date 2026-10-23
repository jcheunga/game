using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Readable, icon-led profiles shared by the armory and spell inspector.</summary>
public static class ArmoryDetailUi
{
    public readonly record struct Stat(string Icon, string Label, string Value);

    /// <summary>A compact stat ledger (see <see cref="StatSheet"/>).</summary>
    public static StatSheet Stats(IEnumerable<Stat> values, int columns = 2) => new(values, columns);

    public static Stat[] UnitStats(UnitDefinition unit)
    {
        var state = GameState.Instance;
        var stats = unit.IsPlayerSide ? state.BuildPlayerUnitStats(unit) : new UnitStats(unit);
        return new[] {
            new Stat("heart", "Health", $"{stats.MaxHealth:0}"), new Stat("sword", "Damage", $"{stats.AttackDamage:0.#}"),
            new Stat("siege", "Gate damage", $"{stats.BaseDamage:0.##}"), new Stat("bolt", "Courage", $"{unit.Cost}"),
            new Stat("eye", "Range", $"{stats.AttackRange:0.#}"),
            new Stat("clock", "Recovery", $"{(unit.IsPlayerSide ? state.ApplyPlayerDeployCooldownUpgrade(unit.DeployCooldown) : unit.DeployCooldown):0.#}s"),
            new Stat("shield", "Armor", $"{stats.Armor:0.#}"),
            new Stat("shield", "Phys resist", $"{stats.PhysicalResistance:0.#}%"),
            new Stat("mana", "Magic resist", $"{stats.MagicalResistance:0.#}%"),
            new Stat("sword", "Crit chance", $"{stats.CriticalChance:0.#}%"),
            new Stat("sword", "Crit bonus", $"+{stats.CriticalDamageBonus:0.#}%")
        };
    }

    private sealed record ComparisonRow(string Label,float Before,float After,string Suffix="",bool LowerBetter=false,string Format="0.##");

    public static GridContainer CompareStats(UnitStats before,UnitStats after,bool changesFirst=false)
    {
        var table=new GridContainer {Columns=3,Name="EquipmentComparison",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        table.AddThemeConstantOverride("h_separation",20);table.AddThemeConstantOverride("v_separation",6);
        foreach(var title in new[]{"Stat","Now","After"})table.AddChild(RealmUi.SectionTitle(title));
        var rows=new List<ComparisonRow> {
            new("Health",before.MaxHealth,after.MaxHealth,Format:"0"),new("Damage",before.AttackDamage,after.AttackDamage),
            new("Armor",before.Armor,after.Armor),new("Physical resist",before.PhysicalResistance,after.PhysicalResistance,"%"),
            new("Magic resist",before.MagicalResistance,after.MagicalResistance,"%"),new("Crit chance",before.CriticalChance,after.CriticalChance,"%"),
            new("Crit bonus",before.CriticalDamageBonus,after.CriticalDamageBonus,"%"),new("Attack interval",before.AttackCooldown,after.AttackCooldown,"s",true),
            new("Move speed",before.Speed,after.Speed),new("Reach",before.AttackRange,after.AttackRange),new("Gate damage",before.BaseDamage,after.BaseDamage)
        };
        if(before.AuraRadius>0 || after.AuraRadius>0)rows.Add(new("Aura reach",before.AuraRadius,after.AuraRadius));
        foreach(var row in changesFirst?rows.OrderByDescending(r=>Math.Abs(r.After-r.Before)>.0001f):rows.AsEnumerable())
        {
            var delta=row.After-row.Before;var changed=Math.Abs(delta)>.0001f;
            var oldText=row.Before.ToString(row.Format)+row.Suffix;var newText=row.After.ToString(row.Format)+row.Suffix;
            var gain=changed?" ("+delta.ToString("+0.##;-0.##")+(row.Suffix=="%"?" pp":row.Suffix)+")":"";
            var key=RealmUi.Label(row.Label,20,true);key.AutowrapMode=TextServer.AutowrapMode.Word;table.AddChild(key);
            var current=RealmUi.Label(oldText,20);current.AutowrapMode=TextServer.AutowrapMode.Off;table.AddChild(current);
            var next=RealmUi.Label(newText+gain,20);next.AutowrapMode=TextServer.AutowrapMode.Off;
            next.AccessibilityName=$"{row.Label}: {oldText} → {newText}";
            if(changed)next.AddThemeColorOverride("font_color",(row.LowerBetter?delta<0:delta>0)?new Color("bad394"):new Color("e2ac91"));
            table.AddChild(next);
        }
        return table;
    }

    /// <summary>A spell's stats, named short enough for a two-column sheet (Courage, not "Courage per pack").</summary>
    public static List<Stat> SpellStats(ResolvedSpellStats spell)
    {
        var primary = spell.EffectType switch {
            "aegis_bolt" => new Stat("shield", "Shield", $"{spell.Power:0.#}"),
            "purifying_light" or "verdant_renewal" or "tactic_healing_shrine" or "tactic_repair_cache" => new Stat("heart", "Recovery", $"{spell.Power:0.#}"),
            "tactic_supply_crate" => new Stat("bolt", "Courage", $"{spell.Power:0.#}"),
            "mirror_ward" => new Stat("shield", "Reflection", $"{spell.Power*100:0}%"),
            "withering_seal" => new Stat("heart", "Healing", $"−{(1-spell.SecondaryPower)*100:0}%"),
            "soul_harvest" => new Stat("mana", "Mana gain", $"{spell.Power:0.#}"),
            "rallying_advance" => new Stat("sword", "Attack", "+8%"),
            "anchor_hex" or "royal_edict" => new Stat("duration", "Control", $"{spell.Duration:0.#}s"),
            "gravitation_well" or "tactic_smoke_pot" or "tactic_war_horn" => new Stat("duration", "Duration", $"{spell.Duration:0.#}s"),
            "gale_step" => new Stat("speed", "Advance", $"{spell.SecondaryPower:0.#}"),
            "tactic_palisade_kit" or "tactic_decoy_lantern" or "tactic_purifying_censer" or "tactic_grounding_rod" => new Stat("shield", "Durability", $"{spell.Definition.ObjectHealth:0.#}"),
            "heal" => new Stat("heart", "Healing", $"{spell.Power:0.#}"),
            "barrier_ward" => new Stat("shield", "Protection", $"{Mathf.RoundToInt((1 - Mathf.Clamp(spell.Power, 0, 1)) * 100)}%"),
            "stone_barricade" => new Stat("shield", "Durability", $"{spell.Power:0.#}"),
            "war_cry" => new Stat("sword", "Attack", $"+{Mathf.RoundToInt((spell.Power - 1) * 100)}%"),
            "resurrect" => new Stat("heart", "Revives at", $"{Mathf.RoundToInt(spell.Power * 100)}%"),
            "polymorph" => new Stat("people", "Targets", "1 enemy"),
            _ => new Stat("sword", "Damage", $"{spell.Power:0.#}")
        };
        var values = new List<Stat> { primary, new("mana", "Mana", spell.ManaCost.ToString()), new("clock", "Cooldown", $"{spell.Cooldown:0.#}s") };
        // A wall's radius is its width, and self-cast spells have none.
        if (spell.Radius > 0 && spell.EffectType != "stone_barricade") values.Add(new("target", "Radius", $"{spell.Radius:0.#}"));
        if (spell.Duration > 0 && primary.Label is not ("Duration" or "Control")) values.Add(new("duration", "Duration", $"{spell.Duration:0.#}s"));
        if (spell.EffectType == "heal") values.Add(new("siege", "Repair", $"{spell.SecondaryPower:0.#}"));
        if (spell.EffectType == "war_cry") values.Add(new("speed", "Speed", $"+{Mathf.RoundToInt((spell.SecondaryPower - 1) * 100)}%"));
        return values;
    }

    public static string SpellRole(string effect) => effect.StartsWith("tactic_") ? "Tactical Gear" : effect switch {
        "aegis_bolt" or "mirror_ward" or "purifying_light" or "verdant_renewal" => "Protection",
        "radiant_rebuke" or "revealing_flare" or "banish" or "withering_seal" => "Counter",
        "tidal_push" or "anchor_hex" or "gravitation_well" or "bramble_snare" => "Control",
        "rallying_advance" or "royal_edict" => "Command",
        "soul_harvest" or "consecrated_ground" => "Ritual",
        "heal" or "resurrect" => "Healing", "barrier_ward" or "stone_barricade" => "Protection",
        "frost_burst" or "earthquake" or "polymorph" => "Control", "war_cry" => "Support", _ => "Damage"
    };

    public static string SpellPurpose(string effect) => effect switch {
        "fireball" => "Blast a group of enemies.", "heal" => "Heal allies and repair your wagon.",
        "frost_burst" => "Damage and slow nearby enemies.", "lightning_strike" => "Strike up to three enemies.",
        "barrier_ward" => "Protect nearby allies from damage.", "stone_barricade" => "Block the enemy with a stone wall.",
        "war_cry" => "Boost every ally's attack and speed.", "earthquake" => "Damage and slow enemies over a wide area.",
        "polymorph" => "Suppress one enemy's attack and movement.", "resurrect" => "Bring back your last eligible fallen ally.",
        _ => GameData.GetPlayerSpells().FirstOrDefault(s=>s.EffectType==effect)?.Description.Split('.')[0] ?? ""
    };

}
