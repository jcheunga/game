using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RelicForgeRecipe
{
    public string TargetRelicId { get; }
    public int ShardCost { get; }
    public int GoldCost { get; }

    public RelicForgeRecipe(string targetRelicId, int shardCost, int goldCost)
    {
        TargetRelicId = targetRelicId;
        ShardCost = shardCost;
        GoldCost = goldCost;
    }
}

public static class RelicForgeCatalog
{
    public const int RelicsRequiredForFusion = 3;
    public const int ShardsPerDismantle_Common = 1;
    public const int ShardsPerDismantle_Rare = 3;
    public const int ShardsPerDismantle_Epic = 9;

    private const int CraftCost_Common_Shards = 5;
    private const int CraftCost_Common_Gold = 200;
    private const int CraftCost_Rare_Shards = 15;
    private const int CraftCost_Rare_Gold = 600;
    private const int CraftCost_Epic_Shards = 40;
    private const int CraftCost_Epic_Gold = 1500;

    private static readonly Dictionary<string, RelicForgeRecipe> Recipes;

    static RelicForgeCatalog()
    {
        Recipes = new Dictionary<string, RelicForgeRecipe>(StringComparer.OrdinalIgnoreCase);
    }

    public static void EnsureLoaded()
    {
        if (Recipes.Count > 0)
        {
            return;
        }

        foreach (var equip in GameData.GetAllEquipment())
        {
            var (shards, gold) = equip.Rarity?.ToLowerInvariant() switch
            {
                "rare" => (CraftCost_Rare_Shards, CraftCost_Rare_Gold),
                "epic" => (CraftCost_Epic_Shards, CraftCost_Epic_Gold),
                _ => (CraftCost_Common_Shards, CraftCost_Common_Gold)
            };
            Recipes[equip.Id] = new RelicForgeRecipe(equip.Id, shards, gold);
        }
    }

    public static int GetDismantleShards(string rarity)
    {
        return rarity?.ToLowerInvariant() switch
        {
            "rare" => ShardsPerDismantle_Rare,
            "epic" => ShardsPerDismantle_Epic,
            _ => ShardsPerDismantle_Common
        };
    }

    public static string GetFusionTargetRarity(string sourceRarity)
    {
        return sourceRarity?.ToLowerInvariant() switch
        {
            "common" => "rare",
            "rare" => "epic",
            _ => null
        };
    }

    public static IReadOnlyList<EquipmentDefinition> GetRelicsByRarity(string rarity)
    {
        return GameData.GetAllEquipment()
            .Where(e => string.Equals(e.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static RelicForgeRecipe GetCraftRecipe(string relicId)
    {
        EnsureLoaded();
        return Recipes.TryGetValue(relicId, out var recipe) ? recipe : null;
    }

    public static IReadOnlyList<RelicForgeRecipe> GetAllRecipes()
    {
        EnsureLoaded();
        return Recipes.Values.ToArray();
    }
}
