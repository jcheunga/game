using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CampaignDistrictPlan
{
    public CampaignDistrictPlan(
        int order,
        string id,
        string title,
        int stageTarget,
        int rewardGold,
        int rewardFood,
        string chapterSummary,
        string threatSummary,
        string rewardRelicId = "")
    {
        Order = order;
        Id = id;
        Title = title;
        StageTarget = stageTarget;
        RewardGold = rewardGold;
        RewardFood = rewardFood;
        ChapterSummary = chapterSummary;
        ThreatSummary = threatSummary;
        RewardRelicId = rewardRelicId ?? "";
    }

    public int Order { get; }
    public string Id { get; }
    public string Title { get; }
    public int StageTarget { get; }
    public int RewardGold { get; }
    public int RewardFood { get; }
    public string ChapterSummary { get; }
    public string ThreatSummary { get; }
    public string RewardRelicId { get; }
}

public static class CampaignPlanCatalog
{
    private static readonly CampaignDistrictPlan[] Districts =
    {
        new(
            1,
            RouteCatalog.CityId,
            "King's Road",
            5,
            40,
            1,
            "Outer farms, pilgrim roads, and bell-tower wards where the Lantern Caravan first meets the Rotbound Host.",
            "Fast ghoul scouts, early blight fire, and tighter bell-tower surge timing.",
            "relic_iron_pendant"),
        new(
            2,
            RouteCatalog.HarborId,
            "Saltwake Docks",
            5,
            50,
            1,
            "Flooded quays, chain cranes, and corpse-choked wreck piers where heavy undead push through narrow kill lanes.",
            "Heavier dead, tide-surge crushes, and Grave Lord pressure around dockside choke points.",
            "relic_sharpened_edge"),
        new(
            3,
            RouteCatalog.FoundryId,
            "Emberforge March",
            5,
            60,
            1,
            "Smelter rows, railyards, and furnace crowns where the Host learns to crack the war wagon with industry and fire.",
            "Sapper dives, bone nests, furnace hazards, and steadier heavy escorts.",
            "relic_swift_boots"),
        new(
            4,
            RouteCatalog.QuarantineId,
            "Ashen Ward",
            5,
            70,
            2,
            "Purge cloisters, sealed courts, and black-vault checkpoints where curse support and containment hazards define the field.",
            "Hexers, heralds, repeated breach timing, and hazard-heavy ward lines.",
            "relic_battle_drum"),
        new(
            5,
            RouteCatalog.ThornwallId,
            "Thornwall Pass",
            6,
            80,
            2,
            "Cliff roads, watch forts, and avalanche shrines that introduce harsher weather, vertical kill zones, and mountain sieges.",
            "Frost-bitten dead, beast-rider raids, and lane-breaking cliff events.",
            "relic_guardian_shield"),
        new(
            6,
            RouteCatalog.BasilicaId,
            "Hollow Basilica",
            6,
            90,
            2,
            "Ruined cathedrals, ossuary plazas, and reliquary vaults where faith relics and necromantic pageantry collide.",
            "Bone nests, blight casters, hexers, and elite relic-guard formations.",
            "relic_war_brand"),
        new(
            7,
            RouteCatalog.MireId,
            "Mire of Saints",
            6,
            100,
            3,
            "Bog causeways, drowned chapels, and plague ferries that slow the march and test attrition discipline.",
            "Rot mist, drowned dead, split broods, and hull-grind attrition packs.",
            "relic_windrunner_cloak"),
        new(
            8,
            RouteCatalog.SteppeId,
            "Sunfall Steppe",
            6,
            110,
            3,
            "Burned waystations, open grassland forts, and roaming siege camps that push pace and flanking pressure.",
            "Fast rider strikes, howler-led raids, and repeated breach dives through open lanes.",
            "relic_sages_ring"),
        new(
            9,
            RouteCatalog.GloamwoodId,
            "Gloamwood Verge",
            6,
            120,
            3,
            "Thorn groves, witch circles, and haunted timber roads where curse traps and ambush timing rule the route.",
            "Ambush packs, snare hazards, hex support, and staggered assault waves from the tree line.",
            "relic_crown_of_valor"),
        new(
            10,
            RouteCatalog.CitadelId,
            "Crownfall Citadel",
            6,
            140,
            4,
            "Bridge forts, breach yards, and the inner keep where every prior threat pattern converges into the capital siege.",
            "Mixed-faction command waves, siege engines, and the final gate breach.",
            "relic_blade_of_ruin")
    };

    public static IReadOnlyList<CampaignDistrictPlan> GetAll()
    {
        return Districts;
    }

    public static int GetTargetDistrictCount()
    {
        return Districts.Length;
    }

    public static int GetTargetStageCount()
    {
        return Districts.Sum(district => district.StageTarget);
    }

    public static int GetAuthoredDistrictCount()
    {
        var count = 0;
        foreach (var district in Districts)
        {
            if (GetAuthoredStageCount(district.Id) > 0)
            {
                count++;
            }
        }

        return count;
    }

    public static int GetAuthoredStageCount()
    {
        return GameData.Stages.Length;
    }

    public static int GetAuthoredStageCount(string districtId)
    {
        return GameData.GetStagesForMap(Normalize(districtId)).Count;
    }

    public static string BuildCampaignStatusSummary()
    {
        var nextFrontier = TryGetNextIncomplete(out var district)
            ? district.Title
            : "Full campaign target locked";
        return
            $"Campaign buildout: {GetAuthoredDistrictCount()}/{GetTargetDistrictCount()} districts authored  |  " +
            $"{GetAuthoredStageCount()}/{GetTargetStageCount()} stages playable  |  " +
            $"Next frontier: {nextFrontier}";
    }

    public static string BuildRoutePlanSummary(string districtId)
    {
        if (!TryGet(districtId, out var district))
        {
            return BuildCampaignStatusSummary();
        }

        var authoredStages = GetAuthoredStageCount(district.Id);
        return
            $"Campaign line: district {district.Order}/{GetTargetDistrictCount()}  |  " +
            $"Authored {authoredStages}/{district.StageTarget} stages  |  " +
            $"Target: {GetTargetStageCount()} total stages";
    }

    public static string BuildDistrictRewardSummary(string districtId)
    {
        if (!TryGet(districtId, out var district))
        {
            return "District reward: none";
        }

        var relicHint = "";
        if (!string.IsNullOrEmpty(district.RewardRelicId))
        {
            var relic = GameData.GetEquipment(district.RewardRelicId);
            if (relic != null)
            {
                relicHint = $", +{relic.DisplayName} ({relic.Rarity} relic)";
            }
        }

        return $"District reward: +{district.RewardGold} gold, +{district.RewardFood} food{relicHint}";
    }

    public static bool TryGet(string districtId, out CampaignDistrictPlan district)
    {
        var normalizedId = Normalize(districtId);
        foreach (var item in Districts)
        {
            if (item.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                district = item;
                return true;
            }
        }

        district = Districts[0];
        return false;
    }

    private static bool TryGetNextIncomplete(out CampaignDistrictPlan district)
    {
        foreach (var item in Districts)
        {
            if (GetAuthoredStageCount(item.Id) < item.StageTarget)
            {
                district = item;
                return true;
            }
        }

        district = Districts[Districts.Length - 1];
        return false;
    }

    private static string Normalize(string districtId)
    {
        return string.IsNullOrWhiteSpace(districtId)
            ? RouteCatalog.CityId
            : districtId.Trim().ToLowerInvariant();
    }
}
