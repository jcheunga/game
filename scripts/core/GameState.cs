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

	public UnitStats BuildPlayerUnitStats(UnitDefinition definition)
	{
		return BuildPlayerUnitStats(definition, 1f, 1f, 0f, 0);
	}

	public UnitStats BuildPlayerUnitStats(
		UnitDefinition definition,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		var level = GetUnitLevel(definition.Id);
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
			message = $"Fill all {MaxDeckSize} squad cards before deploying.";
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
		return 1f + (GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId) * 0.12f);
	}

	private float GetPlayerCourageMaxBonus()
	{
		return GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId) * 6f;
	}

	private float GetPlayerCourageGainScale()
	{
		return 1f + (GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId) * 0.06f);
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
		BestEndlessWave = 0;
		BestEndlessTimeSeconds = 0f;
		EndlessRuns = 0;
	}

	private void ApplySavedData(GameSaveData saved)
	{
		Gold = saved.Version >= 8 ? saved.Gold : saved.Scrap;
		Food = saved.Version >= 8 ? saved.Food : saved.Fuel;
		HighestUnlockedStage = saved.HighestUnlockedStage;
		SelectedStage = saved.SelectedStage;
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
		SelectedEndlessRouteId = NormalizeRouteId(SelectedEndlessRouteId);
		SelectedEndlessBoonId = NormalizeEndlessBoonId(SelectedEndlessBoonId);
		BestEndlessWave = Math.Max(0, BestEndlessWave);
		BestEndlessTimeSeconds = Math.Max(0f, BestEndlessTimeSeconds);
		EndlessRuns = Math.Max(0, EndlessRuns);
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
			EndlessRuns = EndlessRuns
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
		if (string.IsNullOrWhiteSpace(routeId))
		{
			return DefaultEndlessRouteId;
		}

		return routeId.Trim().ToLowerInvariant();
	}

	private static string NormalizeEndlessBoonId(string boonId)
	{
		return EndlessBoonCatalog.Normalize(boonId);
	}
}
