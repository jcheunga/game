using System;
using System.Collections.Generic;

public sealed class ArenaTier
{
	public string Id { get; }
	public string Title { get; }
	public int MinRating { get; }
	public int MaxRating { get; }
	public string ColorHex { get; }

	public ArenaTier(string id, string title, int minRating, int maxRating, string colorHex)
	{
		Id = id;
		Title = title;
		MinRating = minRating;
		MaxRating = maxRating;
		ColorHex = colorHex;
	}
}

public sealed class ArenaOpponentSnapshot
{
	public string ProfileId { get; set; } = "";
	public string Callsign { get; set; } = "Unknown";
	public string[] DeckUnitIds { get; set; } = Array.Empty<string>();
	public string[] DeckSpellIds { get; set; } = Array.Empty<string>();
	public Dictionary<string, int> UnitLevels { get; set; } = new();
	public Dictionary<string, string> UnitEquipmentIds { get; set; } = new();
	public int PowerRating { get; set; }
	public int ArenaRating { get; set; } = 1000;
}

public static class ArenaCatalog
{
	public const int DefaultRating = 1000;
	public const int EloK = 32;
	public const int MinRequiredStage = 20;

	private static readonly ArenaTier[] Tiers =
	{
		new("bronze", "Bronze", 0, 999, "cd7f32"),
		new("silver", "Silver", 1000, 1299, "c0c0c0"),
		new("gold", "Gold", 1300, 1599, "ffd700"),
		new("platinum", "Platinum", 1600, 1899, "e5e4e2"),
		new("diamond", "Diamond", 1900, 99999, "b9f2ff"),
	};

	public static IReadOnlyList<ArenaTier> GetAllTiers() => Tiers;

	public static ArenaTier GetTier(int rating)
	{
		foreach (var tier in Tiers)
		{
			if (rating >= tier.MinRating && rating <= tier.MaxRating)
			{
				return tier;
			}
		}

		return Tiers[0];
	}

	public static int CalculateElo(int playerRating, int opponentRating, bool won)
	{
		var expected = 1.0 / (1.0 + Math.Pow(10, (opponentRating - playerRating) / 400.0));
		var actual = won ? 1.0 : 0.0;
		var newRating = playerRating + (int)(EloK * (actual - expected));
		return Math.Max(0, newRating);
	}

	public static ArenaOpponentSnapshot GenerateLocalOpponent(int targetRating, int index)
	{
		var rng = new Godot.RandomNumberGenerator();
		rng.Seed = (ulong)(targetRating * 137 + index * 911);

		var allUnits = GameData.GetPlayerUnits();
		var allSpells = GameData.GetPlayerSpells();
		var deckUnits = new List<string>();
		var deckSpells = new List<string>();
		var levels = new Dictionary<string, int>();

		for (var i = 0; i < 3 && i < allUnits.Count; i++)
		{
			var idx = rng.RandiRange(0, allUnits.Count - 1);
			var unitId = allUnits[idx].Id;
			if (!deckUnits.Contains(unitId))
			{
				deckUnits.Add(unitId);
				levels[unitId] = rng.RandiRange(2, 5);
			}
		}

		for (var i = 0; i < 3 && i < allSpells.Count; i++)
		{
			var idx = rng.RandiRange(0, allSpells.Count - 1);
			var spellId = allSpells[idx].Id;
			if (!deckSpells.Contains(spellId))
			{
				deckSpells.Add(spellId);
			}
		}

		var names = new[] { "Sir Aldric", "Dame Elara", "Thane Orik", "Warden Kael", "Lady Mireya", "Lord Voss" };
		return new ArenaOpponentSnapshot
		{
			ProfileId = $"local_{index}_{targetRating}",
			Callsign = names[rng.RandiRange(0, names.Length - 1)],
			DeckUnitIds = deckUnits.ToArray(),
			DeckSpellIds = deckSpells.ToArray(),
			UnitLevels = levels,
			PowerRating = targetRating / 2,
			ArenaRating = targetRating + rng.RandiRange(-100, 100),
		};
	}
}
