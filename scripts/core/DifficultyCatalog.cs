using System.Collections.Generic;
using System.Linq;

public readonly struct DifficultyDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public float EnemyHealthScale { get; }
	public float EnemyDamageScale { get; }
	public float CourageGainScale { get; }
	public float GoldRewardScale { get; }
	public float FoodRewardScale { get; }

	public DifficultyDefinition(
		string id,
		string title,
		string description,
		float enemyHealthScale,
		float enemyDamageScale,
		float courageGainScale,
		float goldRewardScale,
		float foodRewardScale)
	{
		Id = id;
		Title = title;
		Description = description;
		EnemyHealthScale = enemyHealthScale;
		EnemyDamageScale = enemyDamageScale;
		CourageGainScale = courageGainScale;
		GoldRewardScale = goldRewardScale;
		FoodRewardScale = foodRewardScale;
	}
}

public static class DifficultyCatalog
{
	public const string EasyId = "easy";
	public const string NormalId = "normal";
	public const string HardId = "hard";
	public const string IronmanId = "ironman";

	private static readonly DifficultyDefinition[] All =
	{
		new(
			EasyId,
			"Apprentice",
			"Gentler enemies and faster courage for learning the ropes.",
			enemyHealthScale: 0.75f,
			enemyDamageScale: 0.75f,
			courageGainScale: 1.15f,
			goldRewardScale: 0.8f,
			foodRewardScale: 1.0f),
		new(
			NormalId,
			"Warden",
			"The intended challenge. Balanced for steady progression.",
			enemyHealthScale: 1.0f,
			enemyDamageScale: 1.0f,
			courageGainScale: 1.0f,
			goldRewardScale: 1.0f,
			foodRewardScale: 1.0f),
		new(
			HardId,
			"Champion",
			"Tougher enemies hit harder and take more punishment. Better gold rewards.",
			enemyHealthScale: 1.25f,
			enemyDamageScale: 1.2f,
			courageGainScale: 0.9f,
			goldRewardScale: 1.3f,
			foodRewardScale: 1.0f),
		new(
			IronmanId,
			"Legend",
			"Relentless pressure. Only the best convoys survive. Maximum gold payouts.",
			enemyHealthScale: 1.5f,
			enemyDamageScale: 1.4f,
			courageGainScale: 0.85f,
			goldRewardScale: 1.6f,
			foodRewardScale: 1.0f),
	};

	private static readonly Dictionary<string, DifficultyDefinition> ById =
		All.ToDictionary(d => d.Id);

	public static IReadOnlyList<DifficultyDefinition> GetAll() => All;

	public static DifficultyDefinition GetById(string id)
	{
		if (!string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id, out var def))
		{
			return def;
		}

		return GetDefault();
	}

	public static DifficultyDefinition GetDefault() => ById[NormalId];
}
