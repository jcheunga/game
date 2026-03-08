using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GameState : Node
{
	private const int DefaultScrap = 120;
	private const int DefaultFuel = 10;
	private const int DefaultUnlockedStage = 1;
	private const int MaxDeckSize = 3;
	private const int DefaultUnitLevel = 1;
	private const int MaxPlayerUnitLevel = 5;
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

	public int Scrap { get; private set; } = DefaultScrap;
	public int Fuel { get; private set; } = DefaultFuel;
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
	public bool HasFullDeck => _activeDeckUnitIds.Count >= MaxDeckSize;

	private readonly List<string> _activeDeckUnitIds = new();
	private readonly List<int> _stageStars = new();
	private readonly Dictionary<string, int> _unitUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);

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

	public void ApplyVictory(int stage, int rewardScrap, int rewardFuel, int starsEarned)
	{
		var previousHighestUnlockedStage = HighestUnlockedStage;
		Scrap += rewardScrap;
		Fuel += rewardFuel;
		UnlockNextStageInternal(stage);
		var bestStars = RecordStageStars(stage, starsEarned);
		var newlyUnlockedUnits = GetNewlyUnlockedPlayerUnits(previousHighestUnlockedStage);
		var unlockSuffix = newlyUnlockedUnits.Count > 0
			? $" New unit unlocked: {string.Join(", ", newlyUnlockedUnits.Select(unit => unit.DisplayName))}."
			: "";
		LastResultMessage =
			$"Stage {stage} cleared. +{rewardScrap} scrap, +{rewardFuel} fuel. Stars: {bestStars}/3.{unlockSuffix}";
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

	public void ApplyEndlessResult(string routeId, int waveReached, float elapsedSeconds, int enemyDefeats, int rewardScrap, int rewardFuel, bool retreated)
	{
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Scrap += Math.Max(0, rewardScrap);
		Fuel += Math.Max(0, rewardFuel);
		BestEndlessWave = Math.Max(BestEndlessWave, Math.Max(0, waveReached));
		BestEndlessTimeSeconds = Math.Max(BestEndlessTimeSeconds, Math.Max(0f, elapsedSeconds));
		EndlessRuns++;

		var routeLabel = GameData.GetLatestStageForMap(SelectedEndlessRouteId).MapName;
		var outcome = retreated ? "withdrew" : "was overrun";
		LastResultMessage =
			$"Endless run {outcome} on {routeLabel}. Wave {Math.Max(0, waveReached)}, {elapsedSeconds:0.0}s, {enemyDefeats} defeats. " +
			$"+{Math.Max(0, rewardScrap)} scrap, +{Math.Max(0, rewardFuel)} fuel.";
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
		return GameData.GetPlayerUnits()
			.Where(unit => IsUnitUnlocked(unit.Id))
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
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				return true;
			}

			return HighestUnlockedStage >= Mathf.Max(1, definition.UnlockStage);
		}
		catch (Exception)
		{
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
		return definition.Cost + 20 + ((level - 1) * 25);
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

			if (!IsUnitUnlocked(definition.Id))
			{
				message = $"{definition.DisplayName} unlocks at stage {definition.UnlockStage}.";
				return false;
			}

			var currentLevel = GetUnitLevel(definition.Id);
			if (currentLevel >= MaxPlayerUnitLevel)
			{
				message = $"{definition.DisplayName} is already max level.";
				return false;
			}

			var cost = GetUnitUpgradeCost(definition.Id);
			if (Scrap < cost)
			{
				message = $"Need {cost} scrap to upgrade {definition.DisplayName}.";
				return false;
			}

			Scrap -= cost;
			_unitUpgradeLevels[definition.Id] = currentLevel + 1;
			LastResultMessage = $"{definition.DisplayName} upgraded to level {currentLevel + 1}. -{cost} scrap.";
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

			if (!IsUnitUnlocked(definition.Id))
			{
				message = $"{definition.DisplayName} unlocks at stage {definition.UnlockStage}.";
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
		Scrap = DefaultScrap;
		Fuel = DefaultFuel;
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
		_stageStars.Clear();
		_unitUpgradeLevels.Clear();
		BestEndlessWave = 0;
		BestEndlessTimeSeconds = 0f;
		EndlessRuns = 0;
	}

	private void ApplySavedData(GameSaveData saved)
	{
		Scrap = saved.Scrap;
		Fuel = saved.Fuel;
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

		NormalizeDeck();
		NormalizeStageStars();
		NormalizeUnitLevels();
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
			Scrap = Scrap,
			Fuel = Fuel,
			HighestUnlockedStage = HighestUnlockedStage,
			SelectedStage = SelectedStage,
			SelectedEndlessRouteId = SelectedEndlessRouteId,
			SelectedEndlessBoonId = SelectedEndlessBoonId,
			LastResultMessage = LastResultMessage,
			ShowDevUi = ShowDevUi,
			ShowFpsCounter = ShowFpsCounter,
			ActiveDeckUnitIds = _activeDeckUnitIds.ToArray(),
			StageStars = _stageStars.ToArray(),
			UnitLevels = new Dictionary<string, int>(_unitUpgradeLevels),
			BestEndlessWave = BestEndlessWave,
			BestEndlessTimeSeconds = BestEndlessTimeSeconds,
			EndlessRuns = EndlessRuns
		};
	}

	private void NormalizeDeck()
	{
		var validPlayerIds = new HashSet<string>(
			GameData.PlayerRosterIds.Where(IsUnitUnlocked),
			StringComparer.OrdinalIgnoreCase);
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

			if (!IsUnitUnlocked(defaultId))
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

	private IReadOnlyList<UnitDefinition> GetNewlyUnlockedPlayerUnits(int previousHighestUnlockedStage)
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
