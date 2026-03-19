using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class PrestigeColorVariant
{
	public string UnitId;
	public int PrestigeIndex; // 1, 2, or 3
	public string ColorHex;
	public string Title;
	public string RequiredAchievementId;

	public PrestigeColorVariant(string unitId, int prestigeIndex, string colorHex, string title, string requiredAchievementId)
	{
		UnitId = unitId;
		PrestigeIndex = prestigeIndex;
		ColorHex = colorHex;
		Title = title;
		RequiredAchievementId = requiredAchievementId;
	}
}

public static class PrestigeColorCatalog
{
	public const int MaxPrestigeIndex = 3;

	private const string CrimsonAchievementId = "boss_slayer";
	private const string FrostAchievementId = "endless_60";
	private const string GoldenAchievementId = "campaign_complete";

	private const string CrimsonTargetHex = "e63946";
	private const string FrostTargetHex = "7bdff2";
	private const string GoldenTargetHex = "ffd700";

	private const float BlendAmount = 0.4f;

	private static readonly Dictionary<string, List<PrestigeColorVariant>> VariantsByUnit = new(StringComparer.OrdinalIgnoreCase);
	private static bool _initialized;

	private static void EnsureInitialized()
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;

		var crimsonTarget = new Color(CrimsonTargetHex);
		var frostTarget = new Color(FrostTargetHex);
		var goldenTarget = new Color(GoldenTargetHex);

		foreach (var unit in GameData.GetPlayerUnits())
		{
			if (unit == null)
			{
				continue;
			}

			var baseColor = unit.GetTint();
			var displayName = unit.DisplayName;

			var variants = new List<PrestigeColorVariant>
			{
				new(
					unit.Id,
					1,
					BlendColor(baseColor, crimsonTarget).ToHtml(false),
					$"Crimson {displayName}",
					CrimsonAchievementId),
				new(
					unit.Id,
					2,
					BlendColor(baseColor, frostTarget).ToHtml(false),
					$"Frost {displayName}",
					FrostAchievementId),
				new(
					unit.Id,
					3,
					BlendColor(baseColor, goldenTarget).ToHtml(false),
					$"Golden {displayName}",
					GoldenAchievementId)
			};

			VariantsByUnit[unit.Id] = variants;
		}
	}

	public static IReadOnlyList<PrestigeColorVariant> GetVariantsForUnit(string unitId)
	{
		EnsureInitialized();
		return VariantsByUnit.TryGetValue(unitId, out var list)
			? list
			: Array.Empty<PrestigeColorVariant>();
	}

	public static IReadOnlyList<PrestigeColorVariant> GetUnlockedVariants(string unitId)
	{
		EnsureInitialized();

		if (!VariantsByUnit.TryGetValue(unitId, out var list))
		{
			return Array.Empty<PrestigeColorVariant>();
		}

		var state = GameState.Instance;
		if (state == null)
		{
			return Array.Empty<PrestigeColorVariant>();
		}

		return list
			.Where(v => state.IsAchievementUnlocked(v.RequiredAchievementId))
			.ToArray();
	}

	public static PrestigeColorVariant GetVariant(string unitId, int prestigeIndex)
	{
		EnsureInitialized();
		if (prestigeIndex < 1 || prestigeIndex > MaxPrestigeIndex)
		{
			return null;
		}

		if (!VariantsByUnit.TryGetValue(unitId, out var list))
		{
			return null;
		}

		return list.FirstOrDefault(v => v.PrestigeIndex == prestigeIndex);
	}

	public static Color? ResolvePrestigeColor(string unitId, int prestigeIndex)
	{
		if (prestigeIndex <= 0)
		{
			return null;
		}

		var variant = GetVariant(unitId, prestigeIndex);
		if (variant == null)
		{
			return null;
		}

		var state = GameState.Instance;
		if (state == null || !state.IsAchievementUnlocked(variant.RequiredAchievementId))
		{
			return null;
		}

		return new Color(variant.ColorHex);
	}

	private static Color BlendColor(Color from, Color to)
	{
		return from.Lerp(to, BlendAmount);
	}
}
