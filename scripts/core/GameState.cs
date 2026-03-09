using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GameState : Node
{
	private const int DefaultGold = 120;
	private const int DefaultFood = 12;
	private const int DefaultUnlockedStage = 1;
	private const int MaxDeckSize = 3;
	private const int DefaultUnitLevel = 1;
	private const int MaxPlayerUnitLevel = 5;
	private const int MaxPersistentBaseUpgradeLevel = 5;
	private const string DefaultEndlessRouteId = "city";
	private const string DefaultEndlessBoonId = EndlessBoonCatalog.SurplusCourageId;
	private const string DefaultReport = "Pick a district and clear the route.";
	private const bool DefaultShowDevUi = true;
	private const bool DefaultShowFpsCounter = true;
	private static readonly string DefaultAsyncChallengeCode =
		AsyncChallengeCatalog.Create(DefaultUnlockedStage, AsyncChallengeCatalog.PressureSpikeId, 1001).Code;
	private static readonly string[] DefaultDeckUnitIds =
	{
		GameData.PlayerBrawlerId,
		GameData.PlayerShooterId,
		GameData.PlayerDefenderId
	};

	public static GameState Instance { get; private set; }

	public int Gold { get; private set; } = DefaultGold;
	public int Food { get; private set; } = DefaultFood;
	public int Scrap => Gold;
	public int Fuel => Food;
	public int HighestUnlockedStage { get; private set; } = DefaultUnlockedStage;
	public int SelectedStage { get; private set; } = DefaultUnlockedStage;
	public string SelectedAsyncChallengeCode { get; private set; } = DefaultAsyncChallengeCode;
	public string SelectedEndlessRouteId { get; private set; } = DefaultEndlessRouteId;
	public string SelectedEndlessBoonId { get; private set; } = DefaultEndlessBoonId;
	public string LastResultMessage { get; private set; } = DefaultReport;
	public bool ShowDevUi { get; private set; } = DefaultShowDevUi;
	public bool ShowFpsCounter { get; private set; } = DefaultShowFpsCounter;
	public BattleRunMode CurrentBattleMode { get; private set; } = BattleRunMode.Campaign;
	public IReadOnlyList<string> ActiveDeckUnitIds => _activeDeckUnitIds;
	public int BestEndlessWave { get; private set; }
	public float BestEndlessTimeSeconds { get; private set; }
	public int EndlessRuns { get; private set; }
	public int ChallengeRuns { get; private set; }

	public int MaxStage => GameData.MaxStage;
	public int DeckSizeLimit => MaxDeckSize;
	public int MaxUnitLevel => MaxPlayerUnitLevel;
	public int MaxBaseUpgradeLevel => MaxPersistentBaseUpgradeLevel;
	public bool HasFullDeck => _activeDeckUnitIds.Count >= MaxDeckSize;

	private readonly List<string> _activeDeckUnitIds = new();
	private readonly List<int> _stageStars = new();
	private readonly HashSet<string> _ownedPlayerUnitIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _unitUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _baseUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _challengeBestScores = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<ChallengeRunRecord> _challengeHistory = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		LoadOrInitialize();
	}

	public void ResetProgress()
	{
		ApplyDefaults();
		LastResultMessage = "Progress reset. Route is clear for stage 1.";
		Persist();
	}

	public void SetSelectedStage(int stage)
	{
		SelectedStage = Mathf.Clamp(stage, 1, MaxStage);
		Persist();
	}

	public void SetSelectedEndlessRoute(string routeId)
	{
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Persist();
	}

	public void PrepareCampaignBattle()
	{
		CurrentBattleMode = BattleRunMode.Campaign;
	}

	public void PrepareEndlessBattle(string routeId)
	{
		CurrentBattleMode = BattleRunMode.Endless;
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Persist();
	}

	public AsyncChallengeDefinition GetSelectedAsyncChallenge()
	{
		if (!AsyncChallengeCatalog.TryParse(SelectedAsyncChallengeCode, out var challenge, out _))
		{
			challenge = AsyncChallengeCatalog.Create(DefaultUnlockedStage, AsyncChallengeCatalog.PressureSpikeId, 1001);
		}

		return AsyncChallengeCatalog.Create(
			Mathf.Clamp(challenge.Stage, 1, MaxStage),
			challenge.MutatorId,
			challenge.Seed);
	}

	public bool TrySetSelectedAsyncChallengeCode(string code, out string message)
	{
		if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out message))
		{
			return false;
		}

		SelectedAsyncChallengeCode = AsyncChallengeCatalog.Create(
			Mathf.Clamp(challenge.Stage, 1, MaxStage),
			challenge.MutatorId,
			challenge.Seed).Code;
		Persist();
		message = $"Loaded challenge {SelectedAsyncChallengeCode}.";
		return true;
	}

	public void GenerateAsyncChallenge(int stage, string mutatorId)
	{
		var challenge = AsyncChallengeCatalog.Generate(Mathf.Clamp(stage, 1, MaxStage), mutatorId);
		SelectedAsyncChallengeCode = challenge.Code;
		Persist();
	}

	public bool CanStartAsyncChallenge(out string message)
	{
		if (!CanStartBattle(out message))
		{
			return false;
		}

		var challenge = GetSelectedAsyncChallenge();
		if (challenge.Stage > HighestUnlockedStage)
		{
			message = $"Challenge stage {challenge.Stage} is not explored yet.";
			return false;
		}

		message = $"Challenge {challenge.Code} ready on stage {challenge.Stage}.";
		return true;
	}

	public bool PrepareAsyncChallenge(string code, out string message)
	{
		if (!TrySetSelectedAsyncChallengeCode(code, out message))
		{
			return false;
		}

		if (!CanStartAsyncChallenge(out message))
		{
			return false;
		}

		CurrentBattleMode = BattleRunMode.AsyncChallenge;
		LastResultMessage = $"Challenge {SelectedAsyncChallengeCode} queued for deployment.";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public void SetSelectedEndlessBoon(string boonId)
	{
		SelectedEndlessBoonId = NormalizeEndlessBoonId(boonId);
		Persist();
	}

	public void UnlockNextStage(int clearedStage)
	{
		UnlockNextStageInternal(clearedStage);
		Persist();
	}

	public void ApplyVictory(int stage, int rewardGold, int rewardFood, int starsEarned)
	{
		Gold += Math.Max(0, rewardGold);
		Food += Math.Max(0, rewardFood);
		var bestStars = RecordStageStars(stage, starsEarned);
		var nextStageHint = stage < MaxStage
			? $" Explore stage {stage + 1} for {GetStageExploreFoodCost(stage + 1)} food when the convoy is ready."
			: "";
		LastResultMessage =
			$"Stage {stage} cleared. +{Math.Max(0, rewardGold)} gold, +{Math.Max(0, rewardFood)} food. Stars: {bestStars}/3.{nextStageHint}";
		Persist();
	}

	public void ApplyDefeat(int stage)
	{
		LastResultMessage = $"Stage {stage} failed. The bus line was overrun.";
		Persist();
	}

	public void ApplyRetreat(int stage)
	{
		LastResultMessage = $"Retreated from stage {stage}. No rewards earned.";
		Persist();
	}

	public void ApplyEndlessResult(string routeId, int waveReached, float elapsedSeconds, int enemyDefeats, int rewardGold, int rewardFood, bool retreated)
	{
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Gold += Math.Max(0, rewardGold);
		Food += Math.Max(0, rewardFood);
		BestEndlessWave = Math.Max(BestEndlessWave, Math.Max(0, waveReached));
		BestEndlessTimeSeconds = Math.Max(BestEndlessTimeSeconds, Math.Max(0f, elapsedSeconds));
		EndlessRuns++;

		var routeLabel = GameData.GetLatestStageForMap(SelectedEndlessRouteId).MapName;
		var outcome = retreated ? "withdrew" : "was overrun";
		LastResultMessage =
			$"Endless run {outcome} on {routeLabel}. Wave {Math.Max(0, waveReached)}, {elapsedSeconds:0.0}s, {enemyDefeats} defeats. " +
			$"+{Math.Max(0, rewardGold)} gold, +{Math.Max(0, rewardFood)} food.";
		Persist();
	}

	public int GetAsyncChallengeBestScore(string code)
	{
		var normalized = AsyncChallengeCatalog.NormalizeCode(code);
		return _challengeBestScores.TryGetValue(normalized, out var score)
			? Math.Max(0, score)
			: 0;
	}

	public IReadOnlyList<ChallengeRunRecord> GetRecentChallengeHistory(int maxCount = 6, string codeFilter = "")
	{
		var normalizedCode = string.IsNullOrWhiteSpace(codeFilter)
			? ""
			: AsyncChallengeCatalog.NormalizeCode(codeFilter);
		return _challengeHistory
			.Where(entry =>
				string.IsNullOrWhiteSpace(normalizedCode) ||
				entry.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(entry => entry.PlayedAtUnixSeconds)
			.ThenByDescending(entry => entry.Score)
			.Take(Math.Max(1, maxCount))
			.Select(CloneChallengeHistoryRecord)
			.ToArray();
	}

	public void ApplyAsyncChallengeResult(string code, int score, float elapsedSeconds, int enemyDefeats, int starsEarned, bool won, bool retreated)
	{
		AsyncChallengeCatalog.TryParse(code, out var challenge, out _);
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(challenge.Code);
		var previousBest = GetAsyncChallengeBestScore(normalizedCode);
		var newBest = Math.Max(previousBest, Math.Max(0, score));
		_challengeBestScores[normalizedCode] = newBest;
		ChallengeRuns++;
		_challengeHistory.Insert(0, new ChallengeRunRecord
		{
			Code = normalizedCode,
			Stage = Mathf.Clamp(challenge.Stage, 1, MaxStage),
			MutatorId = challenge.MutatorId,
			Score = Math.Max(0, score),
			Won = won,
			Retreated = retreated,
			ElapsedSeconds = Math.Max(0f, elapsedSeconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			StarsEarned = Mathf.Clamp(starsEarned, 0, 3),
			PlayedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});
		NormalizeChallengeHistory();

		if (retreated)
		{
			LastResultMessage =
				$"Challenge {normalizedCode} was abandoned. Best score remains {newBest}.";
		}
		else if (won)
		{
			LastResultMessage =
				$"Challenge {normalizedCode} cleared. Score {score}. Stars {starsEarned}/3. " +
				(newBest > previousBest ? "New personal best recorded." : $"Best remains {newBest}.");
		}
		else
		{
			LastResultMessage =
				$"Challenge {normalizedCode} failed after {elapsedSeconds:0.0}s and {enemyDefeats} defeats. " +
				$"Score {score}. Best: {newBest}.";
		}

		Persist();
	}

	public void SetShowDevUi(bool enabled)
	{
		ShowDevUi = enabled;
		Persist();
	}

	public void SetShowFpsCounter(bool enabled)
	{
		ShowFpsCounter = enabled;
		Persist();
	}

	public IReadOnlyList<UnitDefinition> GetActiveDeckUnits()
	{
		return GameData.GetUnitsByIds(_activeDeckUnitIds);
	}

	public IReadOnlyList<UnitDefinition> GetUnlockedPlayerUnits()
	{
		return GetOwnedPlayerUnits();
	}

	public IReadOnlyList<UnitDefinition> GetOwnedPlayerUnits()
	{
		return GameData.GetPlayerUnits()
			.Where(unit => IsUnitOwned(unit.Id))
			.ToArray();
	}

	public int GetStageStars(int stage)
	{
		if (stage < 1 || stage > _stageStars.Count)
		{
			return 0;
		}

		return _stageStars[stage - 1];
	}

	public bool IsUnitUnlocked(string unitId)
	{
		return IsUnitOwned(unitId);
	}

	public bool IsUnitOwned(string unitId)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				return true;
			}

			return _ownedPlayerUnitIds.Contains(definition.Id);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool IsUnitAvailableForPurchase(string unitId)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				return false;
			}

			return HighestUnlockedStage >= Mathf.Max(1, definition.UnlockStage);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public int GetUnitPurchaseCost(string unitId)
	{
		var definition = GameData.GetUnit(unitId);
		if (!definition.IsPlayerSide)
		{
			return 0;
		}

		if (definition.GoldCost > 0)
		{
			return definition.GoldCost;
		}

		if (DefaultDeckUnitIds.Contains(definition.Id, StringComparer.OrdinalIgnoreCase))
		{
			return 0;
		}

		return 120 + (definition.Cost * 2) + (Math.Max(0, definition.UnlockStage - 1) * 35);
	}

	public bool TryPurchaseUnit(string unitId, out string message)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				message = "Enemy units cannot be purchased.";
				return false;
			}

			if (IsUnitOwned(definition.Id))
			{
				message = $"{definition.DisplayName} is already owned.";
				return false;
			}

			if (!IsUnitAvailableForPurchase(definition.Id))
			{
				message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				return false;
			}

			var cost = GetUnitPurchaseCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to buy {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_ownedPlayerUnitIds.Add(definition.Id);
			LastResultMessage = $"{definition.DisplayName} purchased for {cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public int GetUnitLevel(string unitId)
	{
		return _unitUpgradeLevels.TryGetValue(unitId, out var level)
			? level
			: DefaultUnitLevel;
	}

	public int GetUnitUpgradeCost(string unitId)
	{
		var definition = GameData.GetUnit(unitId);
		var level = GetUnitLevel(unitId);
		return definition.Cost + 35 + ((level - 1) * 30);
	}

	public bool TryUpgradeUnit(string unitId, out string message)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				message = "Only player units can be upgraded.";
				return false;
			}

			if (!IsUnitOwned(definition.Id))
			{
				message = $"Buy {definition.DisplayName} in the shop before upgrading it.";
				return false;
			}

			var currentLevel = GetUnitLevel(definition.Id);
			if (currentLevel >= MaxPlayerUnitLevel)
			{
				message = $"{definition.DisplayName} is already max level.";
				return false;
			}

			var cost = GetUnitUpgradeCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to upgrade {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_unitUpgradeLevels[definition.Id] = currentLevel + 1;
			LastResultMessage = $"{definition.DisplayName} upgraded to level {currentLevel + 1}. -{cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public int GetBaseUpgradeLevel(string upgradeId)
	{
		return _baseUpgradeLevels.TryGetValue(upgradeId, out var level)
			? level
			: 0;
	}

	public int GetBaseUpgradeCost(string upgradeId)
	{
		var level = GetBaseUpgradeLevel(upgradeId);
		return upgradeId switch
		{
			BaseUpgradeCatalog.HullPlatingId => 90 + (level * 70),
			BaseUpgradeCatalog.PantryId => 80 + (level * 65),
			BaseUpgradeCatalog.DispatchConsoleId => 95 + (level * 75),
			_ => 100 + (level * 60)
		};
	}

	public bool TryUpgradeBase(string upgradeId, out string message)
	{
		try
		{
			var definition = BaseUpgradeCatalog.Get(upgradeId);
			var currentLevel = GetBaseUpgradeLevel(upgradeId);
			if (currentLevel >= definition.MaxLevel)
			{
				message = $"{definition.Title} is already max level.";
				return false;
			}

			var cost = GetBaseUpgradeCost(upgradeId);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to upgrade {definition.Title}.";
				return false;
			}

			Gold -= cost;
			_baseUpgradeLevels[upgradeId] = currentLevel + 1;
			LastResultMessage = $"{definition.Title} upgraded to level {currentLevel + 1}. -{cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Base upgrade data was not found.";
			return false;
		}
	}

	public float ApplyPlayerBaseHealthUpgrade(float baseHealth)
	{
		return baseHealth * GetPlayerBaseHealthScale();
	}

	public float ApplyPlayerCourageMaxUpgrade(float baseMax)
	{
		return baseMax + GetPlayerCourageMaxBonus();
	}

	public float ApplyPlayerCourageGainUpgrade(float baseGain)
	{
		return baseGain * GetPlayerCourageGainScale();
	}

	public float ApplyPlayerDeployCooldownUpgrade(float baseCooldown)
	{
		return Mathf.Max(1.5f, baseCooldown * GetPlayerDeployCooldownScale());
	}

	public UnitStats BuildPlayerUnitStats(UnitDefinition definition)
	{
		return BuildPlayerUnitStatsForLevel(definition, GetUnitLevel(definition.Id), 1f, 1f, 0f, 0);
	}

	public UnitStats BuildPlayerUnitStatsAtLevel(UnitDefinition definition, int level)
	{
		return BuildPlayerUnitStatsForLevel(definition, level, 1f, 1f, 0f, 0);
	}

	public UnitStats BuildPlayerUnitStats(
		UnitDefinition definition,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		return BuildPlayerUnitStatsForLevel(
			definition,
			GetUnitLevel(definition.Id),
			bonusHealthScale,
			bonusDamageScale,
			bonusCooldownReduction,
			bonusBaseDamage);
	}

	public float GetPlayerBaseHealthScaleAtLevel(int level)
	{
		return 1f + (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.12f);
	}

	public float GetPlayerCourageMaxBonusAtLevel(int level)
	{
		return Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 6f;
	}

	public float GetPlayerCourageGainScaleAtLevel(int level)
	{
		return 1f + (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.06f);
	}

	public float GetPlayerDeployCooldownScaleAtLevel(int level)
	{
		return Mathf.Max(0.55f, 1f - (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.06f));
	}

	private UnitStats BuildPlayerUnitStatsForLevel(
		UnitDefinition definition,
		int level,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		var bonusLevel = Math.Max(0, level - DefaultUnitLevel);
		var healthScale = (1f + (bonusLevel * 0.12f)) * Math.Max(0.1f, bonusHealthScale);
		var damageScale = (1f + (bonusLevel * 0.1f)) * Math.Max(0.1f, bonusDamageScale);
		var cooldownReduction = (bonusLevel * 0.03f) + Math.Max(0f, bonusCooldownReduction);
		var baseDamageBonus = (bonusLevel * 2) + Math.Max(0, bonusBaseDamage);

		return new UnitStats(
			definition,
			healthScale,
			damageScale,
			cooldownReduction,
			baseDamageBonus);
	}

	public bool IsUnitInActiveDeck(string unitId)
	{
		return _activeDeckUnitIds.Contains(unitId, StringComparer.OrdinalIgnoreCase);
	}

	public bool CanStartBattle(out string message)
	{
		if (_activeDeckUnitIds.Count < MaxDeckSize)
		{
			message = $"Fill all {MaxDeckSize} squad cards in Convoy Shop before deploying.";
			return false;
		}

		message = "Squad ready for deployment.";
		return true;
	}

	public bool CanStartCampaignBattle(int stage, out string message)
	{
		if (!CanStartBattle(out message))
		{
			return false;
		}

		if (stage < 1 || stage > HighestUnlockedStage)
		{
			message = "Explore this route segment before deploying there.";
			return false;
		}

		var foodCost = GetStageEntryFoodCost(stage);
		if (Food < foodCost)
		{
			message = $"Need {foodCost} food to begin stage {stage}.";
			return false;
		}

		message = $"Convoy ready. Stage entry costs {foodCost} food.";
		return true;
	}

	public bool TrySpendStageEntryFood(int stage, out string message)
	{
		if (!CanStartCampaignBattle(stage, out message))
		{
			return false;
		}

		var foodCost = GetStageEntryFoodCost(stage);
		Food -= foodCost;
		LastResultMessage = $"Convoy dispatched to stage {stage}. -{foodCost} food.";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public bool CanExploreNextStage(out StageDefinition nextStage, out string message)
	{
		if (HighestUnlockedStage >= MaxStage)
		{
			nextStage = GameData.GetStage(MaxStage);
			message = "All route segments are already explored.";
			return false;
		}

		nextStage = GameData.GetStage(HighestUnlockedStage + 1);
		var foodCost = GetStageExploreFoodCost(nextStage.StageNumber);
		if (Food < foodCost)
		{
			message = $"Need {foodCost} food to explore stage {nextStage.StageNumber}.";
			return false;
		}

		message = $"Explore stage {nextStage.StageNumber} for {foodCost} food.";
		return true;
	}

	public bool TryExploreNextStage(out string message)
	{
		if (!CanExploreNextStage(out var nextStage, out message))
		{
			return false;
		}

		var previousHighestUnlockedStage = HighestUnlockedStage;
		var foodCost = GetStageExploreFoodCost(nextStage.StageNumber);
		Food -= foodCost;
		HighestUnlockedStage = nextStage.StageNumber;
		SelectedStage = nextStage.StageNumber;
		var newlyAvailableUnits = GetNewlyAvailablePlayerUnits(previousHighestUnlockedStage);
		var availabilitySuffix = newlyAvailableUnits.Count > 0
			? $" New shop unit available: {string.Join(", ", newlyAvailableUnits.Select(unit => unit.DisplayName))}."
			: "";
		LastResultMessage =
			$"Explored {nextStage.MapName} - Stage {nextStage.StageNumber}: {nextStage.StageName}. -{foodCost} food.{availabilitySuffix}";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public int GetStageEntryFoodCost(int stage)
	{
		var definition = GameData.GetStage(Mathf.Clamp(stage, 1, MaxStage));
		return Math.Max(1, definition.EntryFoodCost);
	}

	public int GetStageExploreFoodCost(int stage)
	{
		var definition = GameData.GetStage(Mathf.Clamp(stage, 1, MaxStage));
		return Math.Max(1, definition.ExploreFoodCost);
	}

	public bool ToggleDeckUnit(string unitId, out string message)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			message = "Invalid unit.";
			return false;
		}

		var normalizedId = unitId.Trim();
		if (_activeDeckUnitIds.Contains(normalizedId, StringComparer.OrdinalIgnoreCase))
		{
			if (_activeDeckUnitIds.Count <= 1)
			{
				message = "Your deck needs at least one unit.";
				return false;
			}

			_activeDeckUnitIds.RemoveAll(id => id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
			Persist();
			message = "Unit moved to reserve.";
			return true;
		}

		if (_activeDeckUnitIds.Count >= MaxDeckSize)
		{
			message = $"Deck is full ({MaxDeckSize} cards). Remove one first.";
			return false;
		}

		try
		{
			var definition = GameData.GetUnit(normalizedId);
			if (!definition.IsPlayerSide)
			{
				message = "Only player units can be added to the deck.";
				return false;
			}

			if (!IsUnitOwned(definition.Id))
			{
				if (IsUnitAvailableForPurchase(definition.Id))
				{
					message = $"{definition.DisplayName} is in the shop for {GetUnitPurchaseCost(definition.Id)} gold.";
				}
				else
				{
					message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				}

				return false;
			}

			_activeDeckUnitIds.Add(definition.Id);
			Persist();
			message = $"{definition.DisplayName} added to deck.";
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	private float GetPlayerBaseHealthScale()
	{
		return GetPlayerBaseHealthScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId));
	}

	private float GetPlayerCourageMaxBonus()
	{
		return GetPlayerCourageMaxBonusAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId));
	}

	private float GetPlayerCourageGainScale()
	{
		return GetPlayerCourageGainScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId));
	}

	private float GetPlayerDeployCooldownScale()
	{
		return GetPlayerDeployCooldownScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId));
	}

	private void LoadOrInitialize()
	{
		if (SaveSystem.Instance != null && SaveSystem.Instance.TryLoad(out var saved))
		{
			ApplySavedData(saved);
		}
		else
		{
			ApplyDefaults();
		}

		ClampState();
		Persist();
	}

	private void ApplyDefaults()
	{
		Gold = DefaultGold;
		Food = DefaultFood;
		HighestUnlockedStage = DefaultUnlockedStage;
		SelectedStage = DefaultUnlockedStage;
		SelectedAsyncChallengeCode = DefaultAsyncChallengeCode;
		SelectedEndlessRouteId = DefaultEndlessRouteId;
		SelectedEndlessBoonId = DefaultEndlessBoonId;
		LastResultMessage = DefaultReport;
		ShowDevUi = DefaultShowDevUi;
		ShowFpsCounter = DefaultShowFpsCounter;
		CurrentBattleMode = BattleRunMode.Campaign;
		_activeDeckUnitIds.Clear();
		_activeDeckUnitIds.AddRange(DefaultDeckUnitIds);
		_ownedPlayerUnitIds.Clear();
		foreach (var unitId in DefaultDeckUnitIds)
		{
			_ownedPlayerUnitIds.Add(unitId);
		}

		_stageStars.Clear();
		_unitUpgradeLevels.Clear();
		_baseUpgradeLevels.Clear();
		_challengeBestScores.Clear();
		_challengeHistory.Clear();
		BestEndlessWave = 0;
		BestEndlessTimeSeconds = 0f;
		EndlessRuns = 0;
		ChallengeRuns = 0;
	}

	private void ApplySavedData(GameSaveData saved)
	{
		Gold = saved.Version >= 8 ? saved.Gold : saved.Scrap;
		Food = saved.Version >= 8 ? saved.Food : saved.Fuel;
		HighestUnlockedStage = saved.HighestUnlockedStage;
		SelectedStage = saved.SelectedStage;
		SelectedAsyncChallengeCode = saved.Version >= 9
			? AsyncChallengeCatalog.NormalizeCode(saved.SelectedAsyncChallengeCode)
			: DefaultAsyncChallengeCode;
		SelectedEndlessRouteId = saved.Version >= 6
			? NormalizeRouteId(saved.SelectedEndlessRouteId)
			: DefaultEndlessRouteId;
		SelectedEndlessBoonId = saved.Version >= 7
			? NormalizeEndlessBoonId(saved.SelectedEndlessBoonId)
			: DefaultEndlessBoonId;
		LastResultMessage = string.IsNullOrWhiteSpace(saved.LastResultMessage)
			? DefaultReport
			: saved.LastResultMessage;
		CurrentBattleMode = BattleRunMode.Campaign;
		if (saved.Version >= 2)
		{
			ShowDevUi = saved.ShowDevUi;
			ShowFpsCounter = saved.ShowFpsCounter;
		}
		else
		{
			ShowDevUi = DefaultShowDevUi;
			ShowFpsCounter = DefaultShowFpsCounter;
		}

		_activeDeckUnitIds.Clear();
		if (saved.Version >= 3 && saved.ActiveDeckUnitIds != null)
		{
			foreach (var unitId in saved.ActiveDeckUnitIds)
			{
				if (string.IsNullOrWhiteSpace(unitId))
				{
					continue;
				}

				_activeDeckUnitIds.Add(unitId);
			}
		}

		_ownedPlayerUnitIds.Clear();
		if (saved.Version >= 8 && saved.OwnedPlayerUnitIds != null && saved.OwnedPlayerUnitIds.Length > 0)
		{
			foreach (var unitId in saved.OwnedPlayerUnitIds)
			{
				if (!string.IsNullOrWhiteSpace(unitId))
				{
					_ownedPlayerUnitIds.Add(unitId);
				}
			}
		}
		else
		{
			foreach (var unit in GameData.GetPlayerUnits())
			{
				if (unit.UnlockStage <= HighestUnlockedStage)
				{
					_ownedPlayerUnitIds.Add(unit.Id);
				}
			}
		}

		_stageStars.Clear();
		if (saved.Version >= 4 && saved.StageStars != null)
		{
			foreach (var stars in saved.StageStars)
			{
				_stageStars.Add(stars);
			}
		}

		_unitUpgradeLevels.Clear();
		if (saved.Version >= 5 && saved.UnitLevels != null)
		{
			foreach (var pair in saved.UnitLevels)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				_unitUpgradeLevels[pair.Key] = pair.Value;
			}
		}

		_baseUpgradeLevels.Clear();
		if (saved.Version >= 8 && saved.BaseUpgradeLevels != null)
		{
			foreach (var pair in saved.BaseUpgradeLevels)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					_baseUpgradeLevels[pair.Key] = pair.Value;
				}
			}
		}

		if (saved.Version >= 6)
		{
			BestEndlessWave = Math.Max(0, saved.BestEndlessWave);
			BestEndlessTimeSeconds = Math.Max(0f, saved.BestEndlessTimeSeconds);
			EndlessRuns = Math.Max(0, saved.EndlessRuns);
		}
		else
		{
			BestEndlessWave = 0;
			BestEndlessTimeSeconds = 0f;
			EndlessRuns = 0;
		}

		_challengeBestScores.Clear();
		if (saved.Version >= 9 && saved.ChallengeBestScores != null)
		{
			foreach (var pair in saved.ChallengeBestScores)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					_challengeBestScores[AsyncChallengeCatalog.NormalizeCode(pair.Key)] = Math.Max(0, pair.Value);
				}
			}
		}

		ChallengeRuns = saved.Version >= 9
			? Math.Max(0, saved.ChallengeRuns)
			: 0;

		_challengeHistory.Clear();
		if (saved.Version >= 10 && saved.ChallengeHistory != null)
		{
			foreach (var entry in saved.ChallengeHistory)
			{
				if (entry == null)
				{
					continue;
				}

				_challengeHistory.Add(CloneChallengeHistoryRecord(entry));
			}
		}
	}

	private void ClampState()
	{
		Gold = Math.Max(0, Gold);
		Food = Math.Max(0, Food);

		if (MaxStage <= 0)
		{
			HighestUnlockedStage = 1;
			SelectedStage = 1;
			return;
		}

		if (HighestUnlockedStage < 1)
		{
			HighestUnlockedStage = 1;
		}
		else if (HighestUnlockedStage > MaxStage)
		{
			HighestUnlockedStage = MaxStage;
		}

		if (SelectedStage < 1)
		{
			SelectedStage = 1;
		}
		else if (SelectedStage > HighestUnlockedStage)
		{
			SelectedStage = HighestUnlockedStage;
		}

		NormalizeOwnedUnits();
		NormalizeDeck();
		NormalizeStageStars();
		NormalizeUnitLevels();
		NormalizeBaseUpgrades();
		NormalizeChallengeScores();
		NormalizeChallengeHistory();
		SelectedEndlessRouteId = NormalizeRouteId(SelectedEndlessRouteId);
		SelectedEndlessBoonId = NormalizeEndlessBoonId(SelectedEndlessBoonId);
		SelectedAsyncChallengeCode = GetSelectedAsyncChallenge().Code;
		BestEndlessWave = Math.Max(0, BestEndlessWave);
		BestEndlessTimeSeconds = Math.Max(0f, BestEndlessTimeSeconds);
		EndlessRuns = Math.Max(0, EndlessRuns);
		ChallengeRuns = Math.Max(0, ChallengeRuns);
	}

	private void UnlockNextStageInternal(int clearedStage)
	{
		var nextStage = clearedStage + 1;
		if (nextStage > HighestUnlockedStage)
		{
			HighestUnlockedStage = nextStage;
		}

		if (HighestUnlockedStage > MaxStage)
		{
			HighestUnlockedStage = MaxStage;
		}
	}

	private GameSaveData BuildSaveData()
	{
		return new GameSaveData
		{
			Gold = Gold,
			Food = Food,
			HighestUnlockedStage = HighestUnlockedStage,
			SelectedStage = SelectedStage,
			SelectedAsyncChallengeCode = SelectedAsyncChallengeCode,
			SelectedEndlessRouteId = SelectedEndlessRouteId,
			SelectedEndlessBoonId = SelectedEndlessBoonId,
			LastResultMessage = LastResultMessage,
			ShowDevUi = ShowDevUi,
			ShowFpsCounter = ShowFpsCounter,
			ActiveDeckUnitIds = _activeDeckUnitIds.ToArray(),
			OwnedPlayerUnitIds = _ownedPlayerUnitIds.ToArray(),
			StageStars = _stageStars.ToArray(),
			UnitLevels = new Dictionary<string, int>(_unitUpgradeLevels),
			BaseUpgradeLevels = new Dictionary<string, int>(_baseUpgradeLevels),
			BestEndlessWave = BestEndlessWave,
			BestEndlessTimeSeconds = BestEndlessTimeSeconds,
			EndlessRuns = EndlessRuns,
			ChallengeBestScores = new Dictionary<string, int>(_challengeBestScores),
			ChallengeRuns = ChallengeRuns,
			ChallengeHistory = _challengeHistory
				.Select(CloneChallengeHistoryRecord)
				.ToList()
		};
	}

	private void NormalizeOwnedUnits()
	{
		var validPlayerIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		_ownedPlayerUnitIds.RemoveWhere(unitId => !validPlayerIds.Contains(unitId));
		foreach (var defaultId in DefaultDeckUnitIds)
		{
			_ownedPlayerUnitIds.Add(defaultId);
		}
	}

	private void NormalizeDeck()
	{
		var validPlayerIds = new HashSet<string>(_ownedPlayerUnitIds, StringComparer.OrdinalIgnoreCase);
		_activeDeckUnitIds.RemoveAll(unitId => !validPlayerIds.Contains(unitId));

		for (var i = _activeDeckUnitIds.Count - 1; i >= 0; i--)
		{
			var currentId = _activeDeckUnitIds[i];
			for (var j = 0; j < i; j++)
			{
				if (!_activeDeckUnitIds[j].Equals(currentId, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				_activeDeckUnitIds.RemoveAt(i);
				break;
			}
		}

		if (_activeDeckUnitIds.Count > MaxDeckSize)
		{
			_activeDeckUnitIds.RemoveRange(MaxDeckSize, _activeDeckUnitIds.Count - MaxDeckSize);
		}

		foreach (var defaultId in DefaultDeckUnitIds)
		{
			if (_activeDeckUnitIds.Count >= MaxDeckSize)
			{
				break;
			}

			if (!IsUnitOwned(defaultId))
			{
				continue;
			}

			if (_activeDeckUnitIds.Contains(defaultId, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			_activeDeckUnitIds.Add(defaultId);
		}
	}

	private void NormalizeStageStars()
	{
		for (var i = 0; i < _stageStars.Count; i++)
		{
			_stageStars[i] = Mathf.Clamp(_stageStars[i], 0, 3);
		}

		if (_stageStars.Count > MaxStage)
		{
			_stageStars.RemoveRange(MaxStage, _stageStars.Count - MaxStage);
		}

		while (_stageStars.Count < MaxStage)
		{
			_stageStars.Add(0);
		}
	}

	private int RecordStageStars(int stage, int starsEarned)
	{
		NormalizeStageStars();
		if (stage < 1 || stage > _stageStars.Count)
		{
			return 0;
		}

		var bestStars = Mathf.Max(_stageStars[stage - 1], Mathf.Clamp(starsEarned, 0, 3));
		_stageStars[stage - 1] = bestStars;
		return bestStars;
	}

	private void NormalizeUnitLevels()
	{
		var validPlayerIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var invalidKeys = new List<string>();

		foreach (var pair in _unitUpgradeLevels)
		{
			if (!validPlayerIds.Contains(pair.Key))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			_unitUpgradeLevels[pair.Key] = Mathf.Clamp(pair.Value, DefaultUnitLevel, MaxPlayerUnitLevel);
		}

		foreach (var invalidKey in invalidKeys)
		{
			_unitUpgradeLevels.Remove(invalidKey);
		}

		foreach (var unitId in GameData.PlayerRosterIds)
		{
			if (_unitUpgradeLevels.ContainsKey(unitId))
			{
				continue;
			}

			_unitUpgradeLevels[unitId] = DefaultUnitLevel;
		}
	}

	private void NormalizeBaseUpgrades()
	{
		var validUpgradeIds = new HashSet<string>(
			BaseUpgradeCatalog.GetAll().Select(upgrade => upgrade.Id),
			StringComparer.OrdinalIgnoreCase);
		var invalidKeys = new List<string>();

		foreach (var pair in _baseUpgradeLevels)
		{
			if (!validUpgradeIds.Contains(pair.Key))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			_baseUpgradeLevels[pair.Key] = Mathf.Clamp(pair.Value, 0, MaxPersistentBaseUpgradeLevel);
		}

		foreach (var invalidKey in invalidKeys)
		{
			_baseUpgradeLevels.Remove(invalidKey);
		}

		foreach (var upgrade in BaseUpgradeCatalog.GetAll())
		{
			if (_baseUpgradeLevels.ContainsKey(upgrade.Id))
			{
				continue;
			}

			_baseUpgradeLevels[upgrade.Id] = 0;
		}
	}

	private void NormalizeChallengeScores()
	{
		var invalidKeys = _challengeBestScores.Keys
			.Where(key => string.IsNullOrWhiteSpace(key))
			.ToArray();

		foreach (var invalidKey in invalidKeys)
		{
			_challengeBestScores.Remove(invalidKey);
		}

		foreach (var key in _challengeBestScores.Keys.ToArray())
		{
			var normalized = AsyncChallengeCatalog.NormalizeCode(key);
			var score = Math.Max(0, _challengeBestScores[key]);
			if (!key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
			{
				_challengeBestScores.Remove(key);
			}

			_challengeBestScores[normalized] = score;
		}
	}

	private void NormalizeChallengeHistory()
	{
		_challengeHistory.RemoveAll(entry => entry == null);
		for (var i = 0; i < _challengeHistory.Count; i++)
		{
			var entry = _challengeHistory[i];
			entry.Code = AsyncChallengeCatalog.NormalizeCode(entry.Code);
			entry.Stage = Mathf.Clamp(entry.Stage, 1, MaxStage);
			entry.MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(entry.MutatorId);
			entry.Score = Math.Max(0, entry.Score);
			entry.ElapsedSeconds = Math.Max(0f, entry.ElapsedSeconds);
			entry.EnemyDefeats = Math.Max(0, entry.EnemyDefeats);
			entry.StarsEarned = Mathf.Clamp(entry.StarsEarned, 0, 3);
			entry.PlayedAtUnixSeconds = Math.Max(0L, entry.PlayedAtUnixSeconds);
		}

		_challengeHistory.Sort((left, right) =>
		{
			var playedComparison = right.PlayedAtUnixSeconds.CompareTo(left.PlayedAtUnixSeconds);
			return playedComparison != 0 ? playedComparison : right.Score.CompareTo(left.Score);
		});

		const int maxEntries = 18;
		if (_challengeHistory.Count > maxEntries)
		{
			_challengeHistory.RemoveRange(maxEntries, _challengeHistory.Count - maxEntries);
		}
	}

	private static ChallengeRunRecord CloneChallengeHistoryRecord(ChallengeRunRecord record)
	{
		return new ChallengeRunRecord
		{
			Code = AsyncChallengeCatalog.NormalizeCode(record.Code),
			Stage = Math.Max(1, record.Stage),
			MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(record.MutatorId),
			Score = Math.Max(0, record.Score),
			Won = record.Won,
			Retreated = record.Retreated,
			ElapsedSeconds = Math.Max(0f, record.ElapsedSeconds),
			EnemyDefeats = Math.Max(0, record.EnemyDefeats),
			StarsEarned = Mathf.Clamp(record.StarsEarned, 0, 3),
			PlayedAtUnixSeconds = Math.Max(0L, record.PlayedAtUnixSeconds)
		};
	}

	private IReadOnlyList<UnitDefinition> GetNewlyAvailablePlayerUnits(int previousHighestUnlockedStage)
	{
		return GameData.GetPlayerUnits()
			.Where(unit => unit.UnlockStage > previousHighestUnlockedStage && unit.UnlockStage <= HighestUnlockedStage)
			.ToArray();
	}

	private void Persist()
	{
		SaveSystem.Instance?.Save(BuildSaveData());
	}

	private static string NormalizeRouteId(string routeId)
	{
		return RouteCatalog.Normalize(routeId);
	}

	private static string NormalizeEndlessBoonId(string boonId)
	{
		return EndlessBoonCatalog.Normalize(boonId);
	}
}
