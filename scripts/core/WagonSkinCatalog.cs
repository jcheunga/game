using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WagonSkinDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public string ColorHex { get; }
	public string UnlockCondition { get; }

	public WagonSkinDefinition(string id, string title, string description, string colorHex, string unlockCondition)
	{
		Id = id;
		Title = title;
		Description = description;
		ColorHex = colorHex;
		UnlockCondition = unlockCondition;
	}
}

public static class WagonSkinCatalog
{
	public const string DefaultSkinId = "skin_default";

	private static readonly WagonSkinDefinition[] Skins =
	{
		new("skin_default", "Standard Caravan", "The trusty Lantern Caravan. Battle-tested and road-worn.", "c8a060", "Always unlocked"),
		new("skin_iron", "Iron Plated", "Reinforced with forge-hardened iron plates.", "8090a0", "Reach Prestige 1"),
		new("skin_royal", "Royal Guard", "Draped in the king's colors. Reserved for campaign victors.", "ffd700", "Complete the campaign"),
		new("skin_bone", "Bone Collector", "Adorned with trophies from every known enemy.", "d0c8b0", "Complete the Codex"),
		new("skin_flame", "Flame Forged", "Wreathed in embers from the Challenge Tower's forge.", "ff6030", "Reach Tower floor 50"),
		new("skin_shadow", "Shadow Runner", "A midnight-black caravan that strikes fear.", "404060", "Reach Arena Gold tier"),
		new("skin_guild", "Guild Banner", "Flies the banner of your warband. Unity is strength.", "40a060", "Reach Guild tier 3"),
		new("skin_legendary", "Legendary", "The ultimate caravan. Only the most accomplished bear this mark.", "e0b0ff", "Unlock all achievements"),
	};

	private static readonly Dictionary<string, WagonSkinDefinition> ById;

	static WagonSkinCatalog()
	{
		ById = new Dictionary<string, WagonSkinDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var s in Skins)
		{
			ById[s.Id] = s;
		}
	}

	public static IReadOnlyList<WagonSkinDefinition> GetAll() => Skins;

	public static WagonSkinDefinition GetById(string id)
	{
		return ById.TryGetValue(id, out var s) ? s : Skins[0];
	}

	public static bool IsSkinUnlocked(string skinId, GameState gs)
	{
		return skinId switch
		{
			"skin_default" => true,
			"skin_iron" => gs.PrestigeLevel >= 1,
			"skin_royal" => gs.HighestUnlockedStage >= gs.MaxStage,
			"skin_bone" => gs.DiscoveredCodexCount >= CodexCatalog.TotalEntries,
			"skin_flame" => gs.TowerHighestFloor >= 50,
			"skin_shadow" => gs.ArenaRating >= 1300,
			"skin_guild" => gs.CachedGuildInfo != null && gs.CachedGuildInfo.Tier >= 3,
			"skin_legendary" => gs.AchievementUnlockedCount >= AchievementCatalog.GetAll().Count,
			_ => false
		};
	}

	public static IReadOnlyList<WagonSkinDefinition> GetUnlocked(GameState gs)
	{
		return Skins.Where(s => IsSkinUnlocked(s.Id, gs)).ToArray();
	}
}
