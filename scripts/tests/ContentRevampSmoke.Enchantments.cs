using System;
using System.Linq;

public partial class ContentRevampSmoke
{
    private void CheckEnchantments(GameState state)
    {
        var fixture=state.BuildSaveData();fixture.Gold=100000;fixture.Essence=100;
        fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;
        fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
        fixture.PromotedUnitIds=new[]{"player_brawler"};fixture.ActiveDeckUnitIds=new[]{"player_brawler","player_shooter"};
        fixture.OwnedEquipmentIds=new[]{"relic_guardian_shield","relic_tower_charm","relic_sharpened_edge"};
        fixture.UnitEquipmentSlots=new() {{"player_brawler","relic_guardian_shield"}};
        fixture.UnitEquipmentSlot2=new() {{"player_brawler","relic_tower_charm"}};
        fixture.RelicEnchantments=new() {{"relic_guardian_shield","ench_thorns"}};
        Call(state,"ApplySavedData",fixture);
        var unit=GameData.GetUnit("player_brawler");
        bool Matches(UnitStats a,UnitStats b)=>a.MaxHealth==b.MaxHealth && Math.Abs(a.AttackDamage-b.AttackDamage)<.001f &&
            a.Armor==b.Armor && a.MagicalResistance==b.MagicalResistance && a.CriticalChance==b.CriticalChance &&
            a.CriticalDamageBonus==b.CriticalDamageBonus && Math.Abs(a.Speed-b.Speed)<.001f && Math.Abs(a.AttackCooldown-b.AttackCooldown)<.001f;
        Check(EnchantmentCatalog.GetAll().Count==10,"All ten Forge enchantments are available");
        foreach(var enchantment in EnchantmentCatalog.GetAll())
        {
            var saved=System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData());
            var preview=state.BuildRelicEnchantmentPreview("relic_tower_charm",enchantment.Id);
            Check(preview.CanApply && preview.Wearers.Single().Slot==2 && preview.Wearers.Single().Unit.Id==unit.Id,$"{enchantment.Title} previews the actual second-slot wearer");
            Check(saved==System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData()),$"{enchantment.Title} preview leaves currency and enchantments unchanged");
            var gold=state.Gold;var essence=state.Essence;
            Check(state.TryApplyEnchantment("relic_tower_charm",enchantment.Id,out _) && state.Gold==gold-enchantment.GoldCost && state.Essence==essence-enchantment.EssenceCost,$"{enchantment.Title} charges its published gold and Essence exactly once");
            Check(Matches(state.BuildPlayerUnitStats(unit),preview.Wearers.Single().After),$"{enchantment.Title} applies the previewed battle stats and shared caps");
            gold=state.Gold;essence=state.Essence;
            Check(!state.TryApplyEnchantment("relic_tower_charm",enchantment.Id,out _) && state.Gold==gold && state.Essence==essence,$"{enchantment.Title} repeated confirmation cannot charge twice");
            Check(state.TryRemoveEnchantment("relic_tower_charm",out _) && state.Gold==gold && state.Essence==essence && state.GetUnitEquipment2(unit.Id)?.Id=="relic_tower_charm","Free removal preserves the relic, slot and remaining wallet");
        }
        state.TryApplyEnchantment("relic_tower_charm","ench_crit_strike",out _);
        var replace=state.BuildRelicEnchantmentPreview("relic_tower_charm","ench_fortify");
        Check(replace.Current.Id=="ench_crit_strike" && replace.Wearers.Single().After.CriticalChance<replace.Wearers.Single().Current.CriticalChance && replace.Wearers.Single().After.Speed<replace.Wearers.Single().Current.Speed,"Replacing an enchantment removes old critical bonuses and previews Fortify's speed tradeoff");
        Check(replace.Wearers.Single().Limits.Any(l=>l.Label=="Armor" && l.Supplied>l.Applied),"Enchantment preview explains armor exceeding the shared two-slot budget");
        state.TryApplyEnchantment("relic_tower_charm","ench_fortify",out _);
        state.ReloadFromDisk();
        Check(state.GetRelicEnchantmentId("relic_tower_charm")=="ench_fortify" && Matches(state.BuildPlayerUnitStats(unit),replace.Wearers.Single().After),"Saved enchantment replacement reloads with the same battle stats");
        Check(state.GetRelicEnchantmentId("relic_guardian_shield")=="ench_thorns","Changing the second relic preserves the first relic's enchantment");
        var stored=state.BuildRelicEnchantmentPreview("relic_sharpened_edge","ench_lifesteal");
        Check(stored.Wearers.Count==0 && stored.CanApply,"Stored relics can be enchanted without inventing a current wearer");
        state.TryApplyEnchantment("relic_sharpened_edge","ench_lifesteal",out _);
        Check(state.GetRelicEnchantmentId("relic_sharpened_edge")=="ench_lifesteal","A stored relic retains its new enchantment");
        var savedAgain=state.BuildSaveData();savedAgain.Gold=1000;savedAgain.Essence=0;Call(state,"ApplySavedData",savedAgain);
        var unavailable=state.BuildRelicEnchantmentPreview("relic_sharpened_edge","ench_haste");
        Check(!unavailable.CanApply && unavailable.Reason.Contains("Essence"),"Insufficient Essence is explained before applying");
        var beforeFailure=System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData());
        Check(!state.TryApplyEnchantment("relic_sharpened_edge","ench_haste",out _) && beforeFailure==System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData()),"An unaffordable enchantment cannot spend resources or replace the old effect");
        savedAgain.Gold=0;savedAgain.Essence=100;Call(state,"ApplySavedData",savedAgain);
        Check(!state.CanApplyEnchantment("relic_sharpened_edge","ench_haste",out var reason) && reason.Contains("gold"),"Insufficient gold has its own affordability check");
        Check(!state.TryApplyEnchantment("relic_war_brand","ench_haste",out _) && !state.TryApplyEnchantment("relic_sharpened_edge","unknown_enchantment",out _) && !state.TryApplyEnchantment("relic_sharpened_edge",null,out _),"Unowned relics and unknown or missing enchantments cannot be applied");
        var remove=state.BuildRelicEnchantmentPreview("relic_tower_charm",null);
        Check(remove.CanApply && remove.GoldCost==0 && remove.EssenceCost==0 && remove.Wearers.Single().After.Speed>remove.Wearers.Single().Current.Speed,"Removal previews the lost bonuses and removes Fortify's speed penalty");
        state.TryRemoveEnchantment("relic_tower_charm",out _);state.ReloadFromDisk();
        Check(state.GetRelicEnchantmentId("relic_tower_charm")==null && state.GetOwnedEquipment().Contains("relic_tower_charm") && state.GetUnitEquipment2(unit.Id)?.Id=="relic_tower_charm","Enchantment removal survives saving without deleting or unequipping the relic");
    }
}
