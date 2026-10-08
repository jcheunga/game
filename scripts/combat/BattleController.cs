using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController : Node2D
{
	private readonly HealthBarMotion _playerHealthBarMotion = new();
	private readonly HealthBarMotion _enemyHealthBarMotion = new();
	private const float LanRaceTelemetryIntervalSeconds = 1f;
	private const float OnlineRoomTelemetryIntervalSeconds = 1f;
	private const float OnlineRoomMonitorRefreshIntervalSeconds = 2f;
	private const float OnlineRoomEndRefreshIntervalSeconds = 2.5f;
	private const float DeployMomentumDurationSeconds = 1.2f;
	private const float DeployMomentumDefenseScale = 0.88f;
	private const float FormationLaneTolerance = 132f;
	private const float FormationBacklineCatchupThreshold = 35f;
	private const float ImpactShakeDurationSeconds = 0.09f;
	private const float MeleeImpactSlowDurationSeconds = 0.1f;
	private const float RangedImpactSlowDurationSeconds = 0.06f;
	private const float TargetFocusScoreBonus = 1800f;
	private const float TargetFinisherScoreBonus = 2600f;
	private const float CampaignBossPhaseThresholdRatio = 0.55f;

	private readonly struct TerrainPalette
	{
		public TerrainPalette(
			Color skyColor,
			Color groundColor,
			Color accentColor,
			Color playerBaseColor,
			Color enemyBaseColor,
			Color playerCoreColor,
			Color enemyCoreColor)
		{
			SkyColor = skyColor;
			GroundColor = groundColor;
			AccentColor = accentColor;
			PlayerBaseColor = playerBaseColor;
			EnemyBaseColor = enemyBaseColor;
			PlayerCoreColor = playerCoreColor;
			EnemyCoreColor = enemyCoreColor;
		}

		public Color SkyColor { get; }
		public Color GroundColor { get; }
		public Color AccentColor { get; }
		public Color PlayerBaseColor { get; }
		public Color EnemyBaseColor { get; }
		public Color PlayerCoreColor { get; }
		public Color EnemyCoreColor { get; }
	}

	private sealed class DeploySlot
	{
		public DeploySlot(UnitDefinition definition, Button button, BattleActionCard card)
		{
			Definition = definition;
			Button = button;
			Card = card;
		}

		public UnitDefinition Definition { get; }
		public Button Button { get; }
		public BattleActionCard Card { get; }
	}

	private sealed class SpellSlot
	{
		public SpellSlot(SpellDefinition definition, Button button, BattleActionCard card)
		{
			Definition = definition;
			Button = button;
			Card = card;
		}

		public SpellDefinition Definition { get; }
		public Button Button { get; }
		public BattleActionCard Card { get; }
	}

	private enum BattleSelectionMode
	{
		Unit,
		Spell
	}

	private sealed class ChallengeGhostMarker
	{
		public ChallengeGhostMarker(string unitId, Vector2 position, Color color, float triggerTime)
		{
			UnitId = unitId;
			Position = position;
			Color = color;
			TriggerTime = triggerTime;
			Remaining = 1.3f;
		}

		public string UnitId { get; }
		public Vector2 Position { get; }
		public Color Color { get; }
		public float TriggerTime { get; }
		public float Remaining { get; set; }
	}

	private readonly struct EndlessDraftOption
	{
		public EndlessDraftOption(string id, string title, string summary)
		{
			Id = id;
			Title = title;
			Summary = summary;
		}

		public string Id { get; }
		public string Title { get; }
		public string Summary { get; }
	}

	private CombatTuning _combat = new();

	private readonly List<Unit> _units = new();
	private readonly BattleDeckState _deck = new();
	private readonly BattleSpellState _spellDeck = new();
	private readonly List<ChallengeDeploymentRecord> _challengeDeploymentTape = new();
	private readonly List<ChallengeGhostMarker> _challengeGhostMarkers = new();
	private readonly RandomNumberGenerator _rng = new();
	private BattleSpawnDirector _spawnDirector = null!;
	private BattleRunMode _battleMode;
	private float _eventEnemyHealthScale = 1f;
	private float _eventEnemyDamageScale = 1f;
	private BattleHudBar _courageBar = null!;
	private BattleHudBar _manaBar = null!;
	private Label _statusLabel = null!;
	private Label _fpsLabel = null!;
	private Label _endLabel = null!;
	private PanelContainer _topHudPanel = null!;
	private PanelContainer _endPanel = null!;
	private CenterContainer _endCenter = null!;
	private Button _endPrimaryButton = null!;
	private Button _endSecondaryButton = null!;
	private CenterContainer _draftCenter = null!;
	private PanelContainer _draftPanel = null!;
	private Label _draftLabel = null!;
	private readonly List<DeploySlot> _deploySlots = new();
	private readonly List<SpellSlot> _spellSlots = new();
	private readonly List<Button> _draftButtons = new();
	private readonly HashSet<string> _endlessRunUpgrades = new(StringComparer.OrdinalIgnoreCase);

	private StageDefinition _stageData = null!;
	private AsyncChallengeDefinition _challengeDefinition = null!;
	private AsyncChallengeMutatorDefinition _challengeMutator = null!;

	private int _stage;
	private string _activeRouteId = "city";
	private string _endlessBoonId = EndlessBoonCatalog.SurplusCourageId;
	private string _endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
	private string _endlessSupportEventLabel = "No caravan support event yet.";
	private string _endlessBattlefieldEventLabel = "No battlefield event active.";

	private float _playerBaseHealth;
	private bool _playerHullTookDamage;
	private float _playerBaseMaxHealth;
	private float _enemyBaseHealth;
	private float _enemyBaseMaxHealth;
	private float _courage;
	private float _maxCourage;
	private float _mana;
	private float _maxMana;
	private float _courageGainPerSecond;
	private float _campaignScoutCourageGainScale = 1f;
	private float _campaignScoutBoostRemaining;
	private int _campaignMomentumStacks;
	private float _campaignMomentumAttackScale = 1f;
	private float _campaignMomentumSpeedScale = 1f;
	private float _campaignMomentumBoostRemaining;
	private int _campaignDoctrineThreshold;
	private int _campaignDoctrineDefeatProgress;
	private float _elapsed;
	private int _playerDeployments;
	private int _enemyDefeats;
	private readonly Dictionary<string, float> _unitDamageDealt = new();
	private readonly Dictionary<Unit, Unit> _targetLocks = new();
	private readonly HashSet<Unit> _campaignBossPhaseTriggeredUnits = new();
	private int _spellsCast;
	private int _activeAbilitiesTriggered;
	private string _lastDeadPlayerUnitId = "";
	private Vector2 _lastDeadPlayerPosition;
	private readonly List<(Unit unit, float expiresAt)> _barricades = new();
	private float _playerSignalJamSeconds;
	private float _challengeMutatorNextJamTimer;
	private float _playerBaseFlashTimer;
	private float _enemyBaseFlashTimer;
	private float _bossEntranceBannerTimer;
	private StyleBoxTexture _battleOverlaySurface;
	private float _impactShakeTimer;
	private float _impactShakeStrength;
	private float _defenseEncounterStartedAt;
	private float _enemySignalJamTimer;
	private float _enemySignalJamRecoveryUntil;
	private float _enemySignalJamCourageGainScale = 1f;
	private float _weatherSpeedScale = 1f;
	private float _weatherAggroScale = 1f;
	private float _weatherCourageScale = 1f;
	private float _weatherDamageScale = 1f;
	private bool _battleEnded;
	private bool _battlePaused;
	private string _bossEntranceBannerText = "";
	private Color _bossEntranceBannerColor = Colors.White;
	private Vector2 _restingScenePosition;
	private bool _defenseEncounterActive;
	private bool _defenseEncounterHullDamaged;
	private int _defenseEncounterPeakPressure;
	private CenterContainer _pauseOverlay;
	private bool _endlessCheckpointActive;
	private float _endlessUnitHealthScale = 1f;
	private float _endlessUnitDamageScale = 1f;
	private float _endlessGoldScale = 1f;
	private bool _endlessBerserkerBlood;
	private float _endlessBusArmorScale = 1f;
	private float _endlessDamageReflectRatio;
	private float _endlessDamageReflectExpiry;
	private float _endlessTempDamageScale = 1f;
	private float _endlessTempDamageExpiry;
	private int _endlessBossGoldBonus;
	private int _endlessBossFoodBonus;
	private int _lastEndlessBossCheckpointWave;
	private string _lastEndlessBossCheckpointTitle = "";
	private int _endlessBossCheckpointsCleared;
	private readonly HashSet<string> _triggeredComboPairIds = new(StringComparer.OrdinalIgnoreCase);
	private string[] _draftOptionIds = Array.Empty<string>();
	private bool _draftingRouteFork;
	private BattleSelectionMode _selectionMode = BattleSelectionMode.Unit;
	private ChallengeRunRecord _challengeGhostRun = null!;
	private int _challengeGhostNextIndex;
	private float _lanRaceTelemetryTimer;
	private float _onlineRoomTelemetryTimer;
	private float _onlineRoomMonitorRefreshTimer;
	private float _onlineRoomEndRefreshTimer;
	private bool _lanStartBarrierActive;
	private bool _onlineRoomStartBarrierActive;
	private float _onlineRoomStartCountdownRemaining;
	private string _lanChallengeEndBaseText = "";

	private bool IsEndlessMode => _battleMode == BattleRunMode.Endless;
	private bool IsChallengeMode => _battleMode == BattleRunMode.AsyncChallenge;
	private bool IsSeasonalEventMode => _battleMode == BattleRunMode.SeasonalEvent;
	private bool IsArenaMode => _battleMode == BattleRunMode.Arena;
	private bool IsTowerMode => _battleMode == BattleRunMode.Tower;
	private bool IsCampaignMode => _battleMode == BattleRunMode.Campaign;
	private bool IsLanRaceMode => IsChallengeMode && LanChallengeService.Instance != null && LanChallengeService.Instance.HasRoom;
	private bool IsOnlineRoomMode => IsChallengeMode &&
		!IsLanRaceMode &&
		OnlineRoomTelemetryService.HasJoinedRoomForChallenge(_challengeDefinition);

	private float PlayerBaseX => _combat.PlayerBaseX;
	private float EnemyBaseX => _combat.EnemyBaseX;
	private float PlayerSpawnX => _combat.PlayerSpawnX;
	private float EnemySpawnX => _combat.EnemySpawnX;

	private float BattlefieldLeft => _combat.BattlefieldLeft;
	private float BattlefieldRight => _combat.BattlefieldRight;
	private float BattlefieldTop => _combat.BattlefieldTop;
	private float BattlefieldBottom => _combat.BattlefieldBottom;
	private float SpawnVerticalPadding => _combat.SpawnVerticalPadding;
	private float BaseCoreRadius => _combat.BaseCoreRadius;
	private float BaseApproachDistance => _combat.BaseApproachDistance;

	private float BaseCenterY => (BattlefieldTop + BattlefieldBottom) * 0.5f;
	private Vector2 PlayerBaseCorePosition => new(PlayerBaseX, BaseCenterY);
	private Vector2 EnemyBaseCorePosition => new(EnemyBaseX, BaseCenterY);

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		YSortEnabled = true;
		_battleRewardStart = GameState.Instance.BuildSaveData();
		BattleSummaryData.Current = null;
		_restingScenePosition = Position;
		_combat = GameData.Combat;
		_battleMode = GameState.Instance.CurrentBattleMode;
		_challengeDefinition = GameState.Instance.GetSelectedAsyncChallenge();
		_challengeMutator = AsyncChallengeCatalog.GetMutator(_challengeDefinition.MutatorId);

		if (IsChallengeMode)
		{
			_rng.Seed = (ulong)_challengeDefinition.Seed;
		}
		else
		{
			_rng.Randomize();
		}

		_spawnDirector = new BattleSpawnDirector(_rng);
		_lanRaceTelemetryTimer = IsLanRaceMode ? 0.2f : 0f;
		_onlineRoomTelemetryTimer = IsOnlineRoomMode ? 0.2f : 0f;
		_onlineRoomMonitorRefreshTimer = IsOnlineRoomMode ? 1.2f : 0f;
		_onlineRoomEndRefreshTimer = IsOnlineRoomMode ? 0.75f : 0f;
		_lanStartBarrierActive = IsLanRaceMode;
		_onlineRoomStartBarrierActive = false;
		_onlineRoomStartCountdownRemaining = 0f;
		_lanChallengeEndBaseText = "";

		if (IsEndlessMode)
		{
			_activeRouteId = NormalizeRouteId(GameState.Instance.SelectedEndlessRouteId);
			_endlessBoonId = EndlessBoonCatalog.Normalize(GameState.Instance.SelectedEndlessBoonId);
			_endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
			_stageData = GameData.GetLatestStageForMap(_activeRouteId);
			_stage = _stageData.StageNumber;
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData) * 1.08f;
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}
		else if (IsChallengeMode)
		{
			_stage = Mathf.Clamp(_challengeDefinition.Stage, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth *
				StageModifiers.ResolvePlayerBaseHealthScale(_stageData) *
				_challengeMutator.PlayerBaseHealthScale;
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth *
				StageModifiers.ResolveEnemyBaseHealthScale(_stageData) *
				_challengeMutator.EnemyBaseHealthScale;
		}
		else if (IsTowerMode)
		{
			var towerFloor = ChallengeTowerCatalog.GetFloor(GameState.Instance.SelectedTowerFloor);
			var baseStage = towerFloor?.BaseStageNumber ?? 1;
			_stage = Mathf.Clamp(baseStage, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
			_eventEnemyHealthScale = towerFloor?.EnemyHealthScale ?? 1f;
			_eventEnemyDamageScale = towerFloor?.EnemyDamageScale ?? 1f;
		}
		else if (IsArenaMode)
		{
			// Arena: use a mid-campaign stage (Reliquary Steps) as the battlefield
			_stage = Mathf.Clamp(57, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}
		else if (IsSeasonalEventMode)
		{
			var eventDef = SeasonalEventCatalog.GetById(GameState.Instance.SelectedEventId);
			var eventStageIndex = GameState.Instance.SelectedEventStageIndex;
			var eventStage = eventDef?.Stages != null && eventStageIndex >= 0 && eventStageIndex < eventDef.Stages.Length
				? eventDef.Stages[eventStageIndex]
				: null;
			var baseStageNum = eventStage?.BaseStageNumber ?? 1;
			_stage = Mathf.Clamp(baseStageNum, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			var eventHealthScale = eventStage?.EnemyHealthScale ?? 1f;
			var eventDamageScale = eventStage?.EnemyDamageScale ?? 1f;
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
			_eventEnemyHealthScale = eventHealthScale;
			_eventEnemyDamageScale = eventDamageScale;
		}
		else
		{
			_stage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
			_stageData = GameData.GetStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}

		_playerBaseMaxHealth = GameState.Instance.ApplyPlayerBaseHealthUpgrade(_playerBaseMaxHealth);
		_playerBaseHealth = _playerBaseMaxHealth;
		_playerHullTookDamage = false;
		_enemyBaseHealth = _enemyBaseMaxHealth;
		Projectile.FieldTint = FieldLighting.Tint;
		InitializeBaseWeapons();
		_campaignScoutCourageGainScale = 1f;
		_campaignScoutBoostRemaining = 0f;
		_campaignMomentumStacks = 0;
		_campaignMomentumAttackScale = 1f;
		_campaignMomentumSpeedScale = 1f;
		_campaignMomentumBoostRemaining = 0f;
		_campaignDoctrineThreshold = IsCampaignMode ? GameState.Instance.GetCampaignRouteDoctrineThreshold(_stage) : 0;
		_campaignDoctrineDefeatProgress = 0;
		_campaignBossPhaseTriggeredUnits.Clear();

		var baseCourageMax = _combat.CourageMax + (IsChallengeMode ? _challengeMutator.CourageMaxBonus : 0f);
		_maxCourage = GameState.Instance.ApplyPlayerCourageMaxUpgrade(baseCourageMax);
		_courage = 0f;
		_maxMana = Mathf.Max(0f, _combat.ManaMax);
		_mana = Mathf.Clamp(_combat.ManaStart, 0f, _maxMana);
		if (IsCampaignMode && GameState.Instance.HasCampaignScoutBonus(_stage))
		{
			_campaignScoutCourageGainScale = GameState.Instance.GetCampaignScoutCourageGainScale(_stage);
			_campaignScoutBoostRemaining = GameState.Instance.GetCampaignScoutDurationSeconds(_stage);
		}
		if (IsCampaignMode && GameState.Instance.CampaignMomentumStacks > 0)
		{
			_campaignMomentumStacks = GameState.Instance.CampaignMomentumStacks;
			_campaignMomentumAttackScale = GameState.Instance.GetCampaignMomentumAttackScale();
			_campaignMomentumSpeedScale = GameState.Instance.GetCampaignMomentumSpeedScale();
			_campaignMomentumBoostRemaining = GameState.Instance.GetCampaignMomentumDurationSeconds();
		}
		var weather = WeatherCatalog.GetById(_stageData?.WeatherId);
		_weatherSpeedScale = weather.SpeedScale;
		_weatherAggroScale = weather.AggroRangeScale;
		_weatherCourageScale = weather.CourageGainScale;
		_weatherDamageScale = weather.DamageScale;

		_courageGainPerSecond = GameState.Instance.ApplyPlayerCourageGainUpgrade(
			_combat.CourageGainPerSecond *
			StageModifiers.ResolveCourageGainScale(_stageData) *
			_weatherCourageScale *
			(IsChallengeMode ? _challengeMutator.CourageGainScale : 1f) *
			(IsChallengeMode ? 1f : GameState.Instance.GetDifficulty().CourageGainScale));

		if (IsEndlessMode)
		{
			ApplyEndlessBoon();
		}

		_playerDeployments = 0;
		_enemyDefeats = 0;
		_unitDamageDealt.Clear();
		_spellsCast = 0;
		_activeAbilitiesTriggered = 0;
		_playerSignalJamSeconds = 0f;
		_endlessCheckpointActive = false;
		_endlessUnitHealthScale = 1f;
		_endlessUnitDamageScale = 1f;
		_endlessGoldScale = 1f;
		_endlessBerserkerBlood = false;
		_endlessBusArmorScale = 1f;
		_endlessDamageReflectRatio = 0f;
		_endlessDamageReflectExpiry = 0f;
		_endlessTempDamageScale = 1f;
		_endlessTempDamageExpiry = 0f;
		_endlessBossGoldBonus = 0;
		_endlessBossFoodBonus = 0;
		_lastEndlessBossCheckpointWave = 0;
		_lastEndlessBossCheckpointTitle = "";
		_endlessBossCheckpointsCleared = 0;
		_triggeredComboPairIds.Clear();
		_endlessRunUpgrades.Clear();
		_challengeDeploymentTape.Clear();
		_challengeGhostMarkers.Clear();
		_challengeGhostNextIndex = 0;
		_enemySignalJamTimer = 0f;
		_enemySignalJamCourageGainScale = 1f;
		_challengeMutatorNextJamTimer = IsChallengeMode && _challengeMutator.SignalJamIntervalSeconds > 0.05f
			? _challengeMutator.SignalJamIntervalSeconds
			: 0f;
		_draftOptionIds = Array.Empty<string>();
		_draftingRouteFork = false;
		_endlessSupportEventLabel = IsEndlessMode
			? "Opening caravan package deployed."
			: "No caravan support event yet.";
		_endlessBattlefieldEventLabel = IsEndlessMode
			? "Initial route event is arming."
			: "No battlefield event active.";
		_challengeGhostRun = IsChallengeMode
			? GameState.Instance.GetChallengeGhostRun(_challengeDefinition.Code, GameState.Instance.HasSelectedAsyncChallengeLockedDeck)
			: null;
		if (IsOnlineRoomMode)
		{
			var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
			var roomTitle = OnlineRoomJoinService.GetCachedTicket()?.RoomTitle;
			if (roomSnapshot?.HasRoom == true && roomSnapshot.RaceCountdownActive && roomSnapshot.RaceCountdownRemainingSeconds > 0.05f)
			{
				_onlineRoomStartBarrierActive = true;
				_onlineRoomStartCountdownRemaining = roomSnapshot.RaceCountdownRemainingSeconds;
			}
		}

		_deck.Initialize(GameState.Instance.GetBattleDeckUnits().Where(unit => GameState.Instance.IsUnitOwned(unit.Id)));
		_spellDeck.Initialize(GameState.Instance.GetBattleDeckSpells());
		_selectionMode = BattleSelectionMode.Unit;
		if (IsArenaMode)
		{
			// Spawn opponent units as enemies
			var opponent = GameState.Instance.SelectedArenaOpponent;
			if (opponent != null)
			{
				var xBase = 1100f;
				for (var i = 0; i < opponent.DeckUnitIds.Length; i++)
				{
					try
					{
						var unitDef = GameData.GetUnit(opponent.DeckUnitIds[i]);
						if (unitDef == null) continue;
						var level = opponent.UnitLevels.TryGetValue(unitDef.Id, out var lvl) ? lvl : 1;
						var stats = GameState.Instance.BuildPlayerUnitStatsAtLevel(unitDef, level);
						var pos = new Vector2(xBase + i * 60f, 360f + (i - 1) * 40f);
						SpawnEnemyUnit(stats, pos);
					}
					catch { }
				}
			}

			_spawnDirector.Initialize(_stage, _stageData, _combat, GameData.GetEnemyUnits());
		}
		else if (IsEndlessMode)
		{
			_spawnDirector.InitializeEndless(_activeRouteId, _stageData, _combat, GameData.GetEnemyUnits());
		}
		else
		{
			_spawnDirector.Initialize(_stage, _stageData, _combat, GameData.GetEnemyUnits());
			if (IsChallengeMode)
			{
				_spawnDirector.SetEnemyScaleModifiers(_challengeMutator.EnemyHealthScale, _challengeMutator.EnemyDamageScale);
			}

			var eliteHealthScale = StageModifiers.ResolveEnemyHealthScale(_stageData);
			var eliteDamageScale = StageModifiers.ResolveEnemyDamageScale(_stageData);
			if (eliteHealthScale > 1.001f || eliteDamageScale > 1.001f)
			{
				_spawnDirector.SetEnemyScaleModifiers(
					_spawnDirector.AdditionalEnemyHealthScale * eliteHealthScale,
					_spawnDirector.AdditionalEnemyDamageScale * eliteDamageScale);
			}
		}

		if (!IsChallengeMode)
		{
			var diffDef = GameState.Instance.GetDifficulty();
			_spawnDirector.SetEnemyScaleModifiers(
				_spawnDirector.AdditionalEnemyHealthScale * diffDef.EnemyHealthScale,
				_spawnDirector.AdditionalEnemyDamageScale * diffDef.EnemyDamageScale);
		}

		if (IsSeasonalEventMode || IsTowerMode)
		{
			_spawnDirector.SetEnemyScaleModifiers(
				_spawnDirector.AdditionalEnemyHealthScale * _eventEnemyHealthScale,
				_spawnDirector.AdditionalEnemyDamageScale * _eventEnemyDamageScale);
		}

		_spawnDirector.EnableAdvanceEncounters(HasCampaignField);

		InitializeBattlePresentation();
		BuildUi();
		SetStatus(IsEndlessMode ? "Defend your wagon."
			: IsChallengeMode ? $"Challenge {_challengeDefinition.Code}"
			: "Tap a unit card to deploy.");
		TryShowTutorialHint("first_battle");
		if (IsEndlessMode)
		{
			TryShowTutorialHint("first_endless");
		}
		UpdateHud();
		if (IsLanRaceMode)
		{
			if (LanChallengeService.Instance != null)
			{
				LanChallengeService.Instance.StateChanged += OnLanRaceStateChanged;
			}

			LanChallengeService.Instance?.ReportLocalBattleLoaded();
		}
		// The zone's own score, soundscape and positional effects; the caravan's horn opens the fight.
		AudioDirector.Instance?.EnterBattle(this, _activeRouteId);
		MusicPlayer.Instance?.PlayBattle(_activeRouteId);
		AudioDirector.Instance?.PlayBattleStart();
	}

	public override void _ExitTree()
	{
		AudioDirector.Instance?.ExitBattle(this);
		if (_hudLayout != null) GetViewport().SizeChanged -= _hudLayout;
		CleanupBattleCamera();
		CleanupMobilePresentation();
		Engine.TimeScale = 1f;
		ResetImpactShake();
		_stageArtwork = null;
		UnitPool.Clear();
		UnitSpriteLoader.ClearCache();
		BattlefieldTextureLoader.ClearCache();
		BattleStructureArt.ClearCache();
		BattleLighting.ClearCache();
		ProjectilePool.Clear();
		if (IsLanRaceMode && LanChallengeService.Instance != null)
		{
			LanChallengeService.Instance.StateChanged -= OnLanRaceStateChanged;
		}
	}

	public override void _Draw()
	{
		var palette = ResolveTerrainPalette();

		DrawPlayableTerrain(palette);
		DrawCursedGround();
		DrawChallengeGhostMarkers();
		// Enemy arrival timing stays hidden to preserve suspense.
		DrawSelectionPreview();

		// These warnings belong to the screen even while the battlefield pans or zooms.
		DrawSetTransformMatrix(GetGlobalTransformWithCanvas().AffineInverse());
		DrawCriticalHealthVignette();
		DrawBossEntranceBanner();
		DrawSetTransform(Vector2.Zero);
		DrawBossPhaseWarnings();
	}

	private void DrawBossEntranceBanner()
	{
		if (_bossEntranceBannerTimer <= 0.001f || string.IsNullOrWhiteSpace(_bossEntranceBannerText))
		{
			return;
		}

		const int titleFontSize = 33;
		const int subFontSize = 14;
		var font = RealmUi.DisplayFont(titleFontSize);
		var subFont = ThemeDB.FallbackFont;
		if (subFont == null)
		{
			return;
		}

		var appearRatio = Mathf.Clamp(_bossEntranceBannerTimer / 2.2f, 0f, 1f);
		var pulse = IsReducedMotionEnabled()
			? 0.32f
			: 0.5f + (0.5f * Mathf.Sin((_elapsed * 8f) + 0.4f));
		var width = 460f + ((1f - appearRatio) * 36f);
		var height = 74f;
		var rect = new Rect2((GetViewportRect().Size.X - width) * 0.5f,
			_mobileCamera != null ? _mobileFieldTop + 8f : 136f, width, height);
		var fill = new Color(_bossEntranceBannerColor, 0.14f + (pulse * 0.07f));
		var outline = new Color(_bossEntranceBannerColor.Lightened(0.12f), 0.72f);
		DrawStyleBox(_battleOverlaySurface ??= MedievalUi.Engraved("inset", 0, 0), rect);
		DrawRect(rect, fill, true);
		DrawRect(rect, outline, false, 3f);

		var title = _bossEntranceBannerText;
		var titleSize = font.GetStringSize(title, HorizontalAlignment.Left, -1f, titleFontSize);
		var subline = IsEndlessMode
			? "Boss wave · checkpoint threat entered the lane"
			: "Boss arrival · major route threat entered the battlefield";
		var subSize = subFont.GetStringSize(subline, HorizontalAlignment.Left, -1f, subFontSize);
		var titlePos = new Vector2(rect.Position.X + ((rect.Size.X - titleSize.X) * 0.5f), rect.Position.Y + 34f);
		var subPos = new Vector2(rect.Position.X + ((rect.Size.X - subSize.X) * 0.5f), rect.Position.Y + 58f);
		DrawString(font, titlePos, title, HorizontalAlignment.Left, -1f, titleFontSize, Colors.White);
		DrawString(subFont, subPos, subline, HorizontalAlignment.Left, -1f, subFontSize, new Color(1f, 1f, 1f, 0.82f));
	}

	private void DrawSelectionPreview()
	{
		if (_battleEnded || _battlePaused || _endlessCheckpointActive || _mobileClearView)
		{
			return;
		}

		if (_cardPointerDown)
		{
			if (!_cardDragging || !CanDropCard(_cardPointerPosition)) return;
			var target = ScreenToBattle(_cardPointerPosition);
			if (_dragSpell != null) DrawSpellPreview(GameState.Instance.BuildSpellStats(_dragSpell), target);
			return;
		}

		var mousePosition = GetLocalMousePosition();
		if (!IsInBattlefield(mousePosition))
		{
			return;
		}

		if (_selectionMode == BattleSelectionMode.Spell && _spellDeck.HasArmedSpell)
		{
			DrawSpellPreview(GameState.Instance.BuildSpellStats(_spellDeck.ArmedSpell), ClampBattlefieldPoint(mousePosition));
		}
	}

	private void DrawSpellPreview(ResolvedSpellStats spell, Vector2 requestedTargetPosition)
	{
		var previewPosition = ResolveSpellPreviewPosition(spell, requestedTargetPosition);
		var color = spell.GetTint().Lightened(0.08f);
		var radius = Mathf.Max(18f, spell.Radius);

		if (spell.EffectType == "war_cry")
		{
			radius = 60f;
		}
		else if (spell.EffectType == "resurrect" && !string.IsNullOrWhiteSpace(_lastDeadPlayerUnitId))
		{
			radius = 34f;
		}

		DrawSetTransform(previewPosition, 0, new Vector2(1f, BattleGroundPlane.DepthScale));
		DrawCircle(Vector2.Zero, radius, new Color(color, 0.08f));
		DrawArc(
			Vector2.Zero,
			radius,
			0f,
			Mathf.Tau,
			36,
			new Color(color, 0.62f),
			2.6f);
		DrawArc(
			Vector2.Zero,
			Mathf.Max(12f, radius * 0.58f),
			0f,
			Mathf.Tau,
			28,
			new Color(color.Lightened(0.18f), 0.34f),
			1.8f,
			true);

		DrawSetTransform(Vector2.Zero);

		if (spell.EffectType is "fireball" or "frost_burst" or "lightning_strike" or "earthquake" or "polymorph")
		{
			foreach (var enemy in GetLivingUnitsInRadius(previewPosition, spell.Radius, Team.Enemy))
			{
				BattleGroundPlane.Ring(this, enemy.Position, enemy.Radius + 8f, new Color(color, .45f));
			}
		}
		else if (spell.EffectType is "heal" or "barrier_ward")
		{
			foreach (var ally in GetLivingUnitsInRadius(previewPosition, spell.Radius, Team.Player))
			{
				BattleGroundPlane.Ring(this, ally.Position, ally.Radius + 8f, new Color(color, .45f));
			}
		}

		DrawPreviewLabel(previewPosition + new Vector2(26f, -26f), BuildSpellPreviewText(spell, previewPosition), color);
	}

	private void DrawPreviewLabel(Vector2 position, string text, Color color)
	{
		// Drag instructions live in the screen-space card preview. Keep the
		// world-space target rings, without duplicating text at camera zoom.
		if (_cardDragging || string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		var font = ThemeDB.FallbackFont;
		if (font == null)
		{
			return;
		}

		const int fontSize = 16;
		var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize);
		var panelPosition = new Vector2(
			Mathf.Clamp(position.X, BattlefieldLeft + 8f, BattlefieldRight - textSize.X - 20f),
			Mathf.Clamp(position.Y, BattlefieldTop + 8f, BattlefieldBottom - textSize.Y - 18f));
		var panelRect = new Rect2(panelPosition, textSize + new Vector2(14f, 10f));
		DrawStyleBox(_battleOverlaySurface ??= MedievalUi.Engraved("inset", 0, 0), panelRect);
		DrawRect(panelRect, new Color(color, 0.72f), false, 2f);
		DrawString(font, panelRect.Position + new Vector2(7f, textSize.Y + 3f), text, HorizontalAlignment.Left, -1f, fontSize, Colors.White);
	}

	private Vector2 ResolveSpellPreviewPosition(ResolvedSpellStats spell, Vector2 requestedTargetPosition)
	{
		return spell.EffectType switch
		{
			"war_cry" => PlayerBaseCorePosition,
			"resurrect" when !string.IsNullOrWhiteSpace(_lastDeadPlayerUnitId) => ClampBattlefieldPoint(_lastDeadPlayerPosition),
			_ => ClampBattlefieldPoint(requestedTargetPosition)
		};
	}

	private string BuildSpellPreviewText(ResolvedSpellStats spell, Vector2 previewPosition)
	{
		int Enemies() => GetLivingUnitsInRadius(previewPosition, spell.Radius, Team.Enemy).Length;
		int Allies() => GetLivingUnitsInRadius(previewPosition, spell.Radius, Team.Player).Length;
		var detail = spell.EffectType switch
		{
			"fireball" or "frost_burst" or "earthquake" => Plural(Enemies(), "enemy", "enemies"),
			"heal" => $"{Plural(Allies(), "ally", "allies")} in range",
			"lightning_strike" => Plural(Math.Min(3, Enemies()), "chain target", "chain targets"),
			"barrier_ward" => $"{Plural(Allies(), "ally", "allies")} warded",
			"stone_barricade" => $"holds for {spell.Duration:0.0}s",
			"war_cry" => $"rallies {Plural(CountTeamUnits(Team.Player), "ally", "allies")}",
			"polymorph" => FindToughestEnemyInRadius(previewPosition, spell.Radius)?.UnitName ?? "no enemy in range",
			"resurrect" => string.IsNullOrWhiteSpace(_lastDeadPlayerUnitId)
				? "no fallen ally"
				: $"revives {GameData.GetUnit(_lastDeadPlayerUnitId)?.DisplayName ?? _lastDeadPlayerUnitId}",
			_ => ""
		};
		return detail.Length == 0 ? spell.DisplayName : $"{spell.DisplayName} · {detail}";
	}

	private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

	private TerrainPalette ResolveTerrainPalette()
	{
		var terrainId = (_stageData?.TerrainId ?? "urban").ToLowerInvariant();
		return terrainId switch
		{
			"highway" => new TerrainPalette(
				new Color("1c2a39"),
				new Color("2a3b4f"),
				new Color(1f, 1f, 1f, 0.12f),
				new Color("2a9d8f"),
				new Color("ef476f"),
				new Color("4cc9a6"),
				new Color("ff4d6d")),
			"night" => new TerrainPalette(
				new Color("0b132b"),
				new Color("1a1e3d"),
				new Color(0.75f, 0.89f, 1f, 0.14f),
				new Color("2d6a4f"),
				new Color("9d0208"),
				new Color("52b788"),
				new Color("ef476f")),
			"industrial" => new TerrainPalette(
				new Color("273043"),
				new Color("3b4252"),
				new Color(0.95f, 0.95f, 0.95f, 0.09f),
				new Color("588157"),
				new Color("bc4749"),
				new Color("84a98c"),
				new Color("e63946")),
			"swamp" => new TerrainPalette(
				new Color("274029"),
				new Color("3a5a40"),
				new Color(0.82f, 0.93f, 0.76f, 0.08f),
				new Color("40916c"),
				new Color("bc4749"),
				new Color("74c69d"),
				new Color("ef476f")),
			"shipyard" => new TerrainPalette(
				new Color("102a43"),
				new Color("243b53"),
				new Color(0.88f, 0.95f, 1f, 0.1f),
				new Color("2f9e8f"),
				new Color("c1121f"),
				new Color("52b788"),
				new Color("ef476f")),
			"railyard" => new TerrainPalette(
				new Color("33211d"),
				new Color("1c1616"),
				new Color(1f, 0.79f, 0.56f, 0.08f),
				new Color("bc6c25"),
				new Color("c1121f"),
				new Color("f4a261"),
				new Color("ef476f")),
			"smelter" => new TerrainPalette(
				new Color("3f1d12"),
				new Color("23120f"),
				new Color(1f, 0.6f, 0.2f, 0.1f),
				new Color("e76f51"),
				new Color("c1121f"),
				new Color("f4a261"),
				new Color("ff4d6d")),
			"foundry" => new TerrainPalette(
				new Color("2a1a17"),
				new Color("161312"),
				new Color(1f, 0.75f, 0.38f, 0.08f),
				new Color("f77f00"),
				new Color("c1121f"),
				new Color("fcbf49"),
				new Color("ef476f")),
			"checkpoint" => new TerrainPalette(
				new Color("173f35"),
				new Color("102522"),
				new Color(0.88f, 1f, 0.84f, 0.08f),
				new Color("52b788"),
				new Color("c1121f"),
				new Color("d8f3dc"),
				new Color("ef476f")),
			"decon" => new TerrainPalette(
				new Color("204e4a"),
				new Color("12302d"),
				new Color(0.86f, 1f, 0.82f, 0.1f),
				new Color("95d5b2"),
				new Color("c1121f"),
				new Color("d8f3dc"),
				new Color("ef476f")),
			"lab" => new TerrainPalette(
				new Color("1c3d46"),
				new Color("11242c"),
				new Color(0.86f, 1f, 0.9f, 0.08f),
				new Color("72efdd"),
				new Color("c1121f"),
				new Color("c7f9cc"),
				new Color("ef476f")),
			"blacksite" => new TerrainPalette(
				new Color("142a29"),
				new Color("0c1717"),
				new Color(0.96f, 1f, 0.72f, 0.08f),
				new Color("52b788"),
				new Color("c1121f"),
				new Color("d9ed92"),
				new Color("ef476f")),
			"pass" => new TerrainPalette(
				new Color("45586d"),
				new Color("202f3d"),
				new Color(0.92f, 0.97f, 1f, 0.08f),
				new Color("a9d6ff"),
				new Color("c1121f"),
				new Color("edf6f9"),
				new Color("ef476f")),
			"shrine" => new TerrainPalette(
				new Color("49596a"),
				new Color("243341"),
				new Color(0.95f, 0.98f, 0.9f, 0.08f),
				new Color("d8f3dc"),
				new Color("c1121f"),
				new Color("fefae0"),
				new Color("ef476f")),
			"watchfort" => new TerrainPalette(
				new Color("3b4858"),
				new Color("1d2834"),
				new Color(0.9f, 0.95f, 1f, 0.08f),
				new Color("bde0fe"),
				new Color("c1121f"),
				new Color("f1faee"),
				new Color("ef476f")),
			"cathedral" => new TerrainPalette(
				new Color("5a4f44"),
				new Color("2a241f"),
				new Color(1f, 0.95f, 0.82f, 0.08f),
				new Color("e9c46a"),
				new Color("c1121f"),
				new Color("fefae0"),
				new Color("ef476f")),
			"ossuary" => new TerrainPalette(
				new Color("51473f"),
				new Color("241f1a"),
				new Color(0.9f, 1f, 0.88f, 0.08f),
				new Color("cdb4db"),
				new Color("c1121f"),
				new Color("f1faee"),
				new Color("ef476f")),
			"reliquary" => new TerrainPalette(
				new Color("4b4136"),
				new Color("211c18"),
				new Color(1f, 0.95f, 0.76f, 0.08f),
				new Color("ffd166"),
				new Color("c1121f"),
				new Color("fff3b0"),
				new Color("ef476f")),
			"marsh" => new TerrainPalette(
				new Color("37503b"),
				new Color("1a271c"),
				new Color(0.86f, 0.98f, 0.84f, 0.08f),
				new Color("90be6d"),
				new Color("c1121f"),
				new Color("ecf39e"),
				new Color("ef476f")),
			"chapel" => new TerrainPalette(
				new Color("4a5642"),
				new Color("232a1f"),
				new Color(0.9f, 0.96f, 0.82f, 0.08f),
				new Color("c9d6a3"),
				new Color("c1121f"),
				new Color("fefae0"),
				new Color("ef476f")),
			"ferry" => new TerrainPalette(
				new Color("36504a"),
				new Color("1a2825"),
				new Color(0.82f, 0.95f, 0.9f, 0.08f),
				new Color("95d5b2"),
				new Color("c1121f"),
				new Color("d8f3dc"),
				new Color("ef476f")),
			"grassland" => new TerrainPalette(
				new Color("755132"),
				new Color("2b1d13"),
				new Color(1f, 0.92f, 0.7f, 0.08f),
				new Color("f4a261"),
				new Color("c1121f"),
				new Color("ffd6a5"),
				new Color("ef476f")),
			"waystation" => new TerrainPalette(
				new Color("6a4930"),
				new Color("2a1b11"),
				new Color(1f, 0.9f, 0.68f, 0.08f),
				new Color("e9c46a"),
				new Color("c1121f"),
				new Color("fef3c7"),
				new Color("ef476f")),
			"siegecamp" => new TerrainPalette(
				new Color("5e4130"),
				new Color("24180f"),
				new Color(1f, 0.86f, 0.66f, 0.08f),
				new Color("ffb703"),
				new Color("c1121f"),
				new Color("ffd6a5"),
				new Color("ef476f")),
			"grove" => new TerrainPalette(
				new Color("4d3228"),
				new Color("1d1415"),
				new Color(0.96f, 0.88f, 0.74f, 0.08f),
				new Color("b08968"),
				new Color("c1121f"),
				new Color("e6ccb2"),
				new Color("ef476f")),
			"witchcircle" => new TerrainPalette(
				new Color("513139"),
				new Color("22161c"),
				new Color(0.92f, 0.86f, 0.8f, 0.08f),
				new Color("dda15e"),
				new Color("c1121f"),
				new Color("fefae0"),
				new Color("ef476f")),
			"timberroad" => new TerrainPalette(
				new Color("5b3b2d"),
				new Color("241813"),
				new Color(0.95f, 0.86f, 0.76f, 0.08f),
				new Color("bc6c25"),
				new Color("c1121f"),
				new Color("f4a261"),
				new Color("ef476f")),
			"bridgefort" => new TerrainPalette(
				new Color("4d566f"),
				new Color("1d2230"),
				new Color(0.92f, 0.9f, 1f, 0.08f),
				new Color("cdb4db"),
				new Color("c1121f"),
				new Color("f8edff"),
				new Color("ef476f")),
			"breachyard" => new TerrainPalette(
				new Color("5c4f54"),
				new Color("241e21"),
				new Color(0.95f, 0.9f, 0.96f, 0.08f),
				new Color("e0aaff"),
				new Color("c1121f"),
				new Color("f3d1ff"),
				new Color("ef476f")),
			"innerkeep" => new TerrainPalette(
				new Color("4a4e69"),
				new Color("1b1d2a"),
				new Color(0.94f, 0.9f, 1f, 0.08f),
				new Color("e0aaff"),
				new Color("c1121f"),
				new Color("f8edff"),
				new Color("ef476f")),
			_ => new TerrainPalette(
				new Color("14213d"),
				new Color("22324f"),
				new Color(1f, 1f, 1f, 0.08f),
				new Color("2a9d8f"),
				new Color("e63946"),
				new Color("52b788"),
				new Color("ef476f"))
		};
	}

	private void DrawPlayerBus(CanvasItem canvas, TerrainPalette palette, RouteDefinition route)
	{
		var healthRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);

		var skinId = GameState.Instance.SelectedWagonSkinId;
		var art = WagonArt;
		var wagonTexture = art?.Texture ?? (skinId == WagonSkinCatalog.DefaultSkinId ? null
			: BattlefieldTextureLoader.TryLoadStructure("war_wagon_" + skinId));
		wagonTexture ??= BattlefieldTextureLoader.TryLoadStructure("war_wagon");
		if (wagonTexture != null)
		{
			var drawRect = art?.At(WagonGround) ?? new Rect2(PlayerBaseX - 90f, BaseCenterY - 128f, 180f, 140f);
			var modulate = FieldLighting.Tint;
			if (_playerBaseFlashTimer > 0f)
			{
				modulate.R = Mathf.Min(1f, 1f + 0.3f);
				modulate.G = Mathf.Max(0.7f, 1f - 0.2f);
				modulate.B = Mathf.Max(0.7f, 1f - 0.2f);
			}
			canvas.DrawTextureRect(wagonTexture, drawRect, false, modulate);
			art?.DrawDoor(canvas, drawRect, WagonDoorFrame, modulate);
			DrawDamageSmoke(canvas, (art?.Point(WagonGround, art.Smoke) ?? new Vector2(PlayerBaseX - 18f, BaseCenterY - 80f)), healthRatio, palette.PlayerBaseColor);
			DrawBaseHealthMeter(canvas,
				new Vector2(PlayerBaseX, drawRect.Position.Y + drawRect.Size.Y * .11f - 5f),
				132f,
				healthRatio,
				true);
			return;
		}

		var skinTint = GameState.Instance.GetWagonSkinColor();
		var baseColor = palette.PlayerBaseColor.Lerp(skinTint, 0.4f);
		var coreColor = palette.PlayerCoreColor.Lerp(skinTint, 0.25f);
		var bodyColor = ResolveBaseBodyColor(baseColor, _playerBaseFlashTimer, healthRatio);
		var cabinColor = ResolveBaseCoreColor(coreColor, _playerBaseFlashTimer, healthRatio);
		var trimColor = route.BannerAccent;
		var trimShadow = route.BannerPanel.Lightened(0.12f);
		var bodyRect = new Rect2(PlayerBaseX - 46f, BaseCenterY - 34f, 122f, 58f);
		var cabinRect = new Rect2(PlayerBaseX + 44f, BaseCenterY - 24f, 34f, 34f);
		var bumperRect = new Rect2(PlayerBaseX - 58f, BaseCenterY + 10f, 16f, 12f);

		canvas.DrawRect(bodyRect, bodyColor, true);
		canvas.DrawRect(cabinRect, cabinColor, true);
		canvas.DrawRect(bumperRect, cabinColor.Darkened(0.2f), true);
		canvas.DrawRect(
			new Rect2(PlayerBaseX - 34f, BaseCenterY - 44f, 66f, 10f),
			bodyColor.Darkened(0.18f),
			true);
		canvas.DrawLine(
			new Vector2(PlayerBaseX - 20f, BaseCenterY - 34f),
			new Vector2(PlayerBaseX - 20f, BaseCenterY - 8f),
			trimShadow,
			3f,
			true);
		canvas.DrawLine(
			new Vector2(PlayerBaseX + 18f, BaseCenterY - 34f),
			new Vector2(PlayerBaseX + 18f, BaseCenterY - 8f),
			trimShadow,
			3f,
			true);
		canvas.DrawColoredPolygon(
			new[]
			{
				new Vector2(PlayerBaseX - 30f, BaseCenterY - 34f),
				new Vector2(PlayerBaseX - 4f, BaseCenterY - 62f),
				new Vector2(PlayerBaseX + 30f, BaseCenterY - 34f)
			},
			bodyColor.Lightened(0.18f));
		canvas.DrawLine(
			new Vector2(PlayerBaseX + 10f, BaseCenterY - 62f),
			new Vector2(PlayerBaseX + 10f, BaseCenterY - 108f),
			trimShadow,
			4f,
			true);
		canvas.DrawColoredPolygon(
			new[]
			{
				new Vector2(PlayerBaseX + 10f, BaseCenterY - 108f),
				new Vector2(PlayerBaseX + 46f, BaseCenterY - 100f),
				new Vector2(PlayerBaseX + 24f, BaseCenterY - 88f),
				new Vector2(PlayerBaseX + 46f, BaseCenterY - 74f),
				new Vector2(PlayerBaseX + 10f, BaseCenterY - 80f)
			},
			trimColor);
		canvas.DrawLine(
			new Vector2(PlayerBaseX - 42f, BaseCenterY + 10f),
			new Vector2(PlayerBaseX + 62f, BaseCenterY + 10f),
			bodyColor.Darkened(0.24f),
			4f,
			true);
		canvas.DrawRect(
			new Rect2(PlayerBaseX - 2f, BaseCenterY - 14f, 18f, 20f),
			trimColor.Darkened(0.22f),
			true);
		canvas.DrawRect(
			new Rect2(PlayerBaseX - 26f, BaseCenterY - 20f, 44f, 18f),
			new Color(1f, 1f, 1f, 0.18f + ((_playerBaseFlashTimer > 0f) ? 0.12f : 0f)),
			true);
		canvas.DrawCircle(new Vector2(PlayerBaseX - 12f, BaseCenterY + 28f), 12f, new Color("1f2933"));
		canvas.DrawCircle(new Vector2(PlayerBaseX + 42f, BaseCenterY + 28f), 12f, new Color("1f2933"));

		var warningLight = healthRatio < 0.45f && Mathf.Sin(_elapsed * 10f) > 0f
			? new Color(1f, 0.34f, 0.28f, 0.95f)
			: new Color(1f, 0.96f, 0.62f, 0.95f);
		canvas.DrawCircle(new Vector2(PlayerBaseX + 72f, BaseCenterY - 8f), 4f, warningLight);

		DrawDamageSmoke(canvas, new Vector2(PlayerBaseX - 18f, BaseCenterY - 48f), healthRatio, bodyColor);
		DrawBaseHealthMeter(canvas,
			new Vector2(PlayerBaseX + 8f, BaseCenterY - 58f),
			132f,
			healthRatio,
			true);
	}

	private void DrawEnemyBarricade(CanvasItem canvas, TerrainPalette palette, RouteDefinition route)
	{
		var healthRatio = Mathf.Clamp(_enemyBaseHealth / Mathf.Max(1f, _enemyBaseMaxHealth), 0f, 1f);

		var art = CastleArt;
		var gatehouseTexture = art?.Texture ?? BattlefieldTextureLoader.TryLoadStructure("gatehouse");
		if (gatehouseTexture != null)
		{
			var drawRect = art?.At(CastleGround) ?? new Rect2(EnemyBaseX - 90f, BaseCenterY - 148f, 180f, 160f);
			var modulate = FieldLighting.Tint;
			if (_enemyBaseFlashTimer > 0f)
			{
				modulate.R = Mathf.Min(1f, 1f + 0.3f);
				modulate.G = Mathf.Max(0.7f, 1f - 0.2f);
				modulate.B = Mathf.Max(0.7f, 1f - 0.2f);
			}
			canvas.DrawTextureRect(gatehouseTexture, drawRect, false, modulate);
			DrawDamageSmoke(canvas, (art?.Point(CastleGround, art.Smoke) ?? new Vector2(EnemyBaseX - 6f, BaseCenterY - 100f)), healthRatio, palette.EnemyBaseColor);
			DrawBaseHealthMeter(canvas,
				new Vector2(EnemyBaseX, drawRect.Position.Y + drawRect.Size.Y * .08f - 10f),
				132f,
				healthRatio,
				false);
			return;
		}

		var wallColor = ResolveBaseBodyColor(palette.EnemyBaseColor, _enemyBaseFlashTimer, healthRatio);
		var coreColor = ResolveBaseCoreColor(palette.EnemyCoreColor, _enemyBaseFlashTimer, healthRatio);
		var bannerColor = coreColor.Lerp(route.BannerAccent, 0.28f);
		var wallBaseX = EnemyBaseX - 52f;
		canvas.DrawRect(new Rect2(wallBaseX, BaseCenterY - 54f, 76f, 112f), wallColor, true);
		canvas.DrawRect(new Rect2(wallBaseX - 18f, BaseCenterY - 12f, 18f, 72f), coreColor.Darkened(0.1f), true);
		canvas.DrawRect(new Rect2(wallBaseX + 76f, BaseCenterY - 36f, 18f, 94f), coreColor.Darkened(0.15f), true);
		canvas.DrawRect(new Rect2(wallBaseX + 12f, BaseCenterY - 72f, 22f, 18f), coreColor, true);
		canvas.DrawRect(new Rect2(wallBaseX + 40f, BaseCenterY - 84f, 22f, 30f), coreColor, true);
		for (var i = 0; i < 4; i++)
		{
			canvas.DrawRect(
				new Rect2(wallBaseX + 4f + (i * 18f), BaseCenterY - 68f - ((i % 2) * 4f), 12f, 14f),
				coreColor.Darkened(0.04f),
				true);
		}

		var gateRect = new Rect2(wallBaseX + 20f, BaseCenterY - 4f, 24f, 54f);
		canvas.DrawRect(gateRect, wallColor.Darkened(0.28f), true);
		for (var i = 0; i < 4; i++)
		{
			var x = gateRect.Position.X + 4f + (i * 5f);
			canvas.DrawLine(
				new Vector2(x, gateRect.Position.Y + 4f),
				new Vector2(x, gateRect.End.Y - 2f),
				coreColor.Lightened(0.16f),
				2f,
				true);
		}

		canvas.DrawLine(
			new Vector2(wallBaseX + 52f, BaseCenterY - 84f),
			new Vector2(wallBaseX + 52f, BaseCenterY - 122f),
			coreColor.Darkened(0.08f),
			4f,
			true);
		canvas.DrawColoredPolygon(
			new[]
			{
				new Vector2(wallBaseX + 52f, BaseCenterY - 122f),
				new Vector2(wallBaseX + 16f, BaseCenterY - 114f),
				new Vector2(wallBaseX + 38f, BaseCenterY - 102f),
				new Vector2(wallBaseX + 18f, BaseCenterY - 86f),
				new Vector2(wallBaseX + 52f, BaseCenterY - 92f)
			},
			bannerColor);
		canvas.DrawCircle(new Vector2(EnemyBaseX - 10f, BaseCenterY - 26f), 7f, new Color(1f, 0.45f, 0.2f, 0.9f));

		if (healthRatio < 0.75f)
		{
			canvas.DrawLine(
				new Vector2(wallBaseX + 16f, BaseCenterY - 22f),
				new Vector2(wallBaseX + 42f, BaseCenterY + 16f),
				coreColor.Lightened(0.25f),
				3f,
				true);
		}

		if (healthRatio < 0.45f)
		{
			canvas.DrawLine(
				new Vector2(wallBaseX + 48f, BaseCenterY - 44f),
				new Vector2(wallBaseX + 18f, BaseCenterY + 6f),
				new Color(1f, 0.68f, 0.38f, 0.8f),
				4f,
				true);
		}

		DrawDamageSmoke(canvas, new Vector2(EnemyBaseX - 14f, BaseCenterY - 88f), healthRatio, wallColor);
		DrawBaseHealthMeter(canvas,
			new Vector2(EnemyBaseX - 10f, BaseCenterY - 112f),
			124f,
			healthRatio,
			false);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_battleEnded || _endlessCheckpointActive || _battlePaused)
		{
			ResetImpactShake();
			return;
		}

		var deltaF = (float)delta;
		if (HandleLanStartBarrier())
		{
			return;
		}

		if (HandleOnlineRoomStartBarrier(deltaF))
		{
			return;
		}

		_elapsed += deltaF;
		_playerBaseFlashTimer = Mathf.Max(0f, _playerBaseFlashTimer - deltaF);
		_enemyBaseFlashTimer = Mathf.Max(0f, _enemyBaseFlashTimer - deltaF);
		_bossEntranceBannerTimer = Mathf.Max(0f, _bossEntranceBannerTimer - deltaF);
		UpdateImpactShake(deltaF);
		if (IsChallengeMode && _challengeMutator.SignalJamIntervalSeconds > 0.05f)
		{
			_challengeMutatorNextJamTimer = Mathf.Max(0f, _challengeMutatorNextJamTimer - deltaF);
			if (_challengeMutatorNextJamTimer <= 0.001f)
			{
				TriggerChallengeMutatorSignalJam();
				_challengeMutatorNextJamTimer = _challengeMutator.SignalJamIntervalSeconds;
			}
		}

		if (_enemySignalJamTimer > 0f)
		{
			_playerSignalJamSeconds += Mathf.Min(_enemySignalJamTimer, deltaF);
			_enemySignalJamTimer = Mathf.Max(0f, _enemySignalJamTimer - deltaF);
			if (_enemySignalJamTimer <= 0.001f)
			{
				_enemySignalJamCourageGainScale = 1f;
				_enemySignalJamRecoveryUntil = _elapsed + 3f;
			}
		}

		_courage += (_courageGainPerSecond * _campaignScoutCourageGainScale * _enemySignalJamCourageGainScale) * deltaF;
		if (_courage > _maxCourage)
		{
			_courage = _maxCourage;
		}
		if (_campaignScoutBoostRemaining > 0f)
		{
			_campaignScoutBoostRemaining = Mathf.Max(0f, _campaignScoutBoostRemaining - deltaF);
			if (_campaignScoutBoostRemaining <= 0.001f)
			{
				_campaignScoutCourageGainScale = 1f;
				SetStatus("Scout tempo faded. Courage flow returned to standard caravan pace.");
			}
		}
		if (_campaignMomentumBoostRemaining > 0f)
		{
			_campaignMomentumBoostRemaining = Mathf.Max(0f, _campaignMomentumBoostRemaining - deltaF);
			if (_campaignMomentumBoostRemaining <= 0.001f)
			{
				_campaignMomentumAttackScale = 1f;
				_campaignMomentumSpeedScale = 1f;
				SetStatus("Caravan momentum spent. The opening march has faded.");
			}
		}

		_deck.TickCooldowns(deltaF);
		_spellDeck.TickCooldowns(deltaF);
		UpdateCampaignField();
		_spawnDirector.Tick(deltaF, _elapsed, () => CountTeamUnits(Team.Enemy), SpawnEnemyUnit, SetStatus);
		UpdateChallengeGhost(deltaF);

		SimulateUnits(deltaF);
		UpdateWagonDoor(deltaF);
		TickBaseWeapons(deltaF);
		ApplyFriendlyUnitSeparation(deltaF);
		ExpireBarricades();
		CleanupDeadUnits();
		TryTriggerCampaignBossPhase();
		UpdateDefenseMomentumRewards();
		MaybeOpenEndlessDraft();
		UpdateLanRaceTelemetry(deltaF);
		UpdateOnlineRoomTelemetry(deltaF);
		UpdateOnlineRoomMonitor(deltaF);
		AudioDirector.Instance?.SetBattlePressure(ResolveBattleAudioPressure());
		TickHordeVoices();
		UpdateHud();
		QueueRedraw();
		CheckBattleEnd();
	}

	public override void _Process(double delta)
	{
		UpdateActorLighting();
		UpdateCardDragPreview();
		UpdateBattleCamera((float)delta);
		var reducedMotion = IsReducedMotionEnabled();
		_playerHealthBarMotion.Update(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), (float)delta, reducedMotion);
		_enemyHealthBarMotion.Update(_enemyBaseHealth / Mathf.Max(1f, _enemyBaseMaxHealth), (float)delta, reducedMotion);
		QueueRedraw();
		if (!_battleEnded || !IsOnlineRoomMode)
		{
			return;
		}

		TickOnlineRoomEndPanelRefresh((float)delta);
	}

	private static bool IsReducedMotionEnabled()
	{
		return GameState.Instance != null && GameState.Instance.ReducedMotion;
	}

	private void UpdateImpactShake(float delta)
	{
		if (IsReducedMotionEnabled())
		{
			ResetImpactShake();
			return;
		}

		if (_impactShakeTimer <= 0.001f || _impactShakeStrength <= 0.01f)
		{
			ResetImpactShake();
			return;
		}

		_impactShakeTimer = Mathf.Max(0f, _impactShakeTimer - delta);
		var intensity = Mathf.Clamp(_impactShakeTimer / ImpactShakeDurationSeconds, 0f, 1f);
		var amplitude = _impactShakeStrength * intensity;
		Position = _restingScenePosition + new Vector2(
			_rng.RandfRange(-amplitude, amplitude),
			_rng.RandfRange(-amplitude, amplitude));

		if (_impactShakeTimer <= 0.001f)
		{
			ResetImpactShake();
		}
	}

	private void ResetImpactShake()
	{
		_impactShakeTimer = 0f;
		_impactShakeStrength = 0f;
		Position = _restingScenePosition;
	}

	private void UpdateDefenseMomentumRewards()
	{
		if (_battleEnded || _playerBaseHealth <= 0f || _enemyBaseHealth <= 0f)
		{
			ResetDefenseEncounterTracking();
			return;
		}

		var pressure = CountTeamUnits(Team.Enemy) + _spawnDirector.PendingSpawnCount;
		if (pressure > 0)
		{
			if (!_defenseEncounterActive)
			{
				_defenseEncounterActive = true;
				_defenseEncounterHullDamaged = false;
				_defenseEncounterPeakPressure = pressure;
				_defenseEncounterStartedAt = _elapsed;
			}
			else
			{
				_defenseEncounterPeakPressure = Math.Max(_defenseEncounterPeakPressure, pressure);
			}

			return;
		}

		if (!_defenseEncounterActive)
		{
			return;
		}

		var encounterDuration = Mathf.Max(0f, _elapsed - _defenseEncounterStartedAt);
		if (ShouldGrantDefenseMomentumReward(_defenseEncounterPeakPressure, encounterDuration))
		{
			GrantDefenseMomentumReward(_defenseEncounterPeakPressure, encounterDuration);
		}

		ResetDefenseEncounterTracking();
	}

	private bool ShouldGrantDefenseMomentumReward(int peakPressure, float encounterDuration)
	{
		return !_defenseEncounterHullDamaged &&
			peakPressure >= 3 &&
			encounterDuration >= 4.5f &&
			(!IsEndlessMode || !_spawnDirector.EndlessCheckpointPending);
	}

	private void GrantDefenseMomentumReward(int peakPressure, float encounterDuration)
	{
		var courageGain = Mathf.Clamp(3f + (peakPressure * 1.2f), 5f, IsEndlessMode ? 12f : 10f);
		var rallyDuration = Mathf.Clamp(2.8f + (peakPressure * 0.08f), 2.8f, 4f);
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			unit.ApplyTemporaryCombatBuff(1.08f, 1.1f, rallyDuration);
		}

		_courage = Mathf.Min(_maxCourage, _courage + courageGain);
		var rewardColor = RouteCatalog.Get(_activeRouteId).BannerAccent.Lightened(0.12f);
		SpawnEffect(PlayerBaseCorePosition, rewardColor, 12f, 34f + (peakPressure * 1.2f), 0.26f, false);
		SetStatus($"Clean defense: line held for {encounterDuration:0.0}s. +{Mathf.RoundToInt(courageGain)} courage and a rally burst.");
	}

	private void RegisterPlayerHullDamage(float damageAmount)
	{
		// Even a tiny hit disqualifies a no-damage clear; repairs never reset this.
		if (damageAmount > 0f) _playerHullTookDamage = true;
		if (damageAmount <= 0.05f)
		{
			return;
		}

		if (_defenseEncounterActive)
		{
			_defenseEncounterHullDamaged = true;
		}
	}

	private void ResetDefenseEncounterTracking()
	{
		_defenseEncounterActive = false;
		_defenseEncounterHullDamaged = false;
		_defenseEncounterPeakPressure = 0;
		_defenseEncounterStartedAt = 0f;
	}

	private bool HandleLanStartBarrier()
	{
		if (!_lanStartBarrierActive || !IsLanRaceMode)
		{
			return false;
		}

		var service = LanChallengeService.Instance;
		if (service == null)
		{
			_lanStartBarrierActive = false;
			return false;
		}

		if (service.RaceCombatReleased)
		{
			_lanStartBarrierActive = false;
			SetStatus("LAN countdown complete. Race live.");
			UpdateHud();
			QueueRedraw();
			return false;
		}

		if (service.RaceCountdownActive)
		{
			SetStatus($"LAN launch sync. Combat begins in {service.RaceCountdownRemainingSeconds:0.0}s.");
		}
		else
		{
			SetStatus("Waiting for all LAN runners to finish loading...");
		}

		UpdateHud();
		QueueRedraw();
		return true;
	}

	private bool HandleOnlineRoomStartBarrier(float delta)
	{
		if (!_onlineRoomStartBarrierActive || !IsOnlineRoomMode)
		{
			return false;
		}

		_onlineRoomStartCountdownRemaining = Mathf.Max(0f, _onlineRoomStartCountdownRemaining - delta);
		if (_onlineRoomStartCountdownRemaining <= 0.001f)
		{
			_onlineRoomStartBarrierActive = false;
			SetStatus("Online room countdown complete. Race live.");
			UpdateHud();
			QueueRedraw();
			return false;
		}

		SetStatus($"Online room launch sync. Combat begins in {_onlineRoomStartCountdownRemaining:0.0}s.");
		UpdateHud();
		QueueRedraw();
		return true;
	}

	private void UpdateLanRaceTelemetry(float delta)
	{
		if (!IsLanRaceMode || _battleEnded || LanChallengeService.Instance == null)
		{
			return;
		}

		_lanRaceTelemetryTimer -= delta;
		if (_lanRaceTelemetryTimer > 0f)
		{
			return;
		}

		_lanRaceTelemetryTimer = LanRaceTelemetryIntervalSeconds;
		LanChallengeService.Instance.UpdateLocalRaceTelemetry(
			_elapsed,
			_enemyDefeats,
			_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth);
	}

	private void UpdateOnlineRoomTelemetry(float delta)
	{
		if (!IsOnlineRoomMode || _battleEnded || AppLifecycleService.Instance?.ShouldPauseOnlineRoomTraffic == true)
		{
			return;
		}

		_onlineRoomTelemetryTimer -= delta;
		if (_onlineRoomTelemetryTimer > 0f)
		{
			return;
		}

		_onlineRoomTelemetryTimer = OnlineRoomTelemetryIntervalSeconds + _rng.RandfRange(-0.2f, 0.2f);
		OnlineRoomTelemetryService.UpdateLocalRaceTelemetry(
			_challengeDefinition,
			_elapsed,
			_enemyDefeats,
			_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth);
	}

	private void UpdateOnlineRoomMonitor(float delta)
	{
		if (!IsOnlineRoomMode ||
			_battleEnded ||
			_onlineRoomStartBarrierActive ||
			!OnlineRoomJoinService.HasActiveTicket() ||
			AppLifecycleService.Instance?.ShouldPauseOnlineRoomTraffic == true)
		{
			return;
		}

		_onlineRoomMonitorRefreshTimer -= delta;
		if (_onlineRoomMonitorRefreshTimer > 0f)
		{
			return;
		}

		_onlineRoomMonitorRefreshTimer = OnlineRoomMonitorRefreshIntervalSeconds + _rng.RandfRange(-0.3f, 0.3f);
		OnlineRoomSessionService.RefreshJoinedRoom(out _);
	}

	private void OnLanRaceStateChanged()
	{
		RefreshLanRaceEndPanel();
	}

	private void RefreshLanRaceEndPanel()
	{
		if (!IsLanRaceMode || string.IsNullOrWhiteSpace(_lanChallengeEndBaseText) || _endLabel == null || !_endPanel.Visible)
		{
			return;
		}

		var service = LanChallengeService.Instance;
		if (service == null)
		{
			_endLabel.Text = _lanChallengeEndBaseText;
			return;
		}

		_endLabel.Text =
			$"{_lanChallengeEndBaseText}\n\n" +
			$"{service.BuildRaceMonitorSummary()}\n\n" +
			$"{service.ScoreboardSummary}\n\n" +
			$"{service.SessionStandingsSummary}";
	}

	private void TickOnlineRoomEndPanelRefresh(float delta)
	{
		if (!IsOnlineRoomMode || _endLabel == null || _endPanel == null || !_endPanel.Visible || string.IsNullOrWhiteSpace(_lanChallengeEndBaseText))
		{
			return;
		}

		_onlineRoomEndRefreshTimer -= delta;
		if (_onlineRoomEndRefreshTimer > 0f)
		{
			return;
		}

		_onlineRoomEndRefreshTimer = OnlineRoomEndRefreshIntervalSeconds;
		RefreshOnlineRoomEndPanel(true);
	}

	private void RefreshOnlineRoomEndPanel(bool refreshProvider)
	{
		if (!IsOnlineRoomMode || string.IsNullOrWhiteSpace(_lanChallengeEndBaseText) || _endLabel == null || _endPanel == null || !_endPanel.Visible)
		{
			return;
		}

		if (refreshProvider && OnlineRoomJoinService.HasActiveTicket())
		{
			OnlineRoomSessionService.RefreshJoinedRoom(out _);
			OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out _);
		}

		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			_endLabel.Text = _lanChallengeEndBaseText;
			return;
		}

		var sections = new List<string>
		{
			_lanChallengeEndBaseText,
			$"Online room: {ticket.RoomTitle}"
		};

		var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (roomSnapshot?.HasRoom == true)
		{
			sections.Add(MultiplayerRoomFormatter.BuildRaceMonitorSummary(roomSnapshot));
		}
		else
		{
			sections.Add("Room monitor pending. Waiting for the joined-room snapshot to refresh.");
		}

		var scoreboardSnapshot = OnlineRoomScoreboardService.GetCachedSnapshot();
		sections.Add(BuildOnlineRoomScoreboardExcerpt(scoreboardSnapshot, 4));
		_endLabel.Text = string.Join("\n\n", sections.Where(section => !string.IsNullOrWhiteSpace(section)));
	}

	private static string BuildOnlineRoomScoreboardExcerpt(OnlineRoomScoreboardSnapshot snapshot, int maxEntries)
	{
		if (snapshot == null || snapshot.Entries == null || snapshot.Entries.Count == 0)
		{
			return "Shared standings: waiting for room results.";
		}

		var lines = new List<string>
		{
			$"Shared standings ({snapshot.ProviderDisplayName}):"
		};
		foreach (var entry in snapshot.Entries.Take(Math.Max(1, maxEntries)))
		{
			lines.Add(
				$"#{entry.Rank} {entry.PlayerCallsign} · {entry.Score} pts · Hull {entry.HullPercent}% · {entry.ElapsedSeconds:0.0}s · {(entry.Retreated ? "retreated" : entry.Won ? "cleared" : "failed")}");
		}

		return string.Join("\n", lines);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (HandleBattleCameraInput(@event)) return;
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			HandleKeyInput(keyEvent);
			return;
		}

		if (_battleEnded || _endlessCheckpointActive || _battlePaused)
		{
			return;
		}

		if (BeginMobileGesture(@event)) return;
		if (@event is InputEventScreenTouch touchEvent && touchEvent.Pressed)
		{
			var worldPosition=ScreenToBattle(touchEvent.Position);
			if (IsInBattlefield(worldPosition))
			{
				TryUseSelectionAt(worldPosition);
			}
			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
		{
			return;
		}

		if (!mouseButton.Pressed)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			ClearArmedSelection();
			return;
		}

		if (mouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		var mouseWorldPosition=ScreenToBattle(mouseButton.Position);
		if (IsInBattlefield(mouseWorldPosition))
		{
			TryUseSelectionAt(mouseWorldPosition);
		}
	}

	private void HandleKeyInput(InputEventKey keyEvent)
	{
		if (_battleEnded)
		{
			if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)
			{
				HandleEndPanelPrimaryAction();
				return;
			}

			if (keyEvent.Keycode == Key.Escape || keyEvent.Keycode == Key.M)
			{
				HandleEndPanelSecondaryAction();
				return;
			}
		}

		if (keyEvent.Keycode == Key.Escape)
		{
			TogglePause();
			return;
		}

		if (keyEvent.Keycode == Key.F12)
		{
			ScreenshotCapture.Capture("battle");
			return;
		}

		if (keyEvent.Keycode == Key.Backspace || keyEvent.Keycode == Key.Delete)
		{
			ClearArmedSelection();
			return;
		}

		if (_battleEnded || _endlessCheckpointActive || _battlePaused)
		{
			return;
		}

		var unitIndex = keyEvent.Keycode switch
		{
			Key.Key1 => 0,
			Key.Key2 => 1,
			Key.Key3 => 2,
			Key.Key4 => 3,
			Key.Key5 => 4,
			Key.Key6 => 5,
			_ => -1
		};
		if (unitIndex >= 0 && unitIndex < _deploySlots.Count)
		{
			DeployPlayerUnit(_deploySlots[unitIndex].Definition);
			return;
		}

		var spellIndex = keyEvent.Keycode switch
		{
			Key.Q => 0,
			Key.W => 1,
			Key.E => 2,
			Key.R => 3,
			Key.T => 4,
			_ => -1
		};
		if (spellIndex >= 0 && spellIndex < _spellSlots.Count)
		{
			ArmSpell(_spellSlots[spellIndex].Definition);
		}
	}

	private void ResetBattleSpeed() => Engine.TimeScale = 1f;

	private void TogglePause()
	{
		if (_restartPending || _battleEnded || _endlessCheckpointActive) return;
		if (_battleSettingsModal != null) { CloseBattleSettings(); return; }
		CancelCardDrag();
		_mobilePointerDown = false;
		_battlePaused = !_battlePaused;
		GetTree().Paused = _battlePaused;
		if (_pauseOverlay != null)
		{
			_pauseOverlay.Visible = _battlePaused;
			if (_battlePaused) { _restartMessage.Hide(); _resumeButton.GrabFocus(); }
			else _hudSettingsButton.GrabFocus();
		}
	}

	private int CountTeamUnits(Team team)
	{
		var count = 0;
		foreach (var unit in _units)
		{
			if (!unit.IsDead && unit.Team == team)
			{
				count++;
			}
		}

		return count;
	}


	private void ArmSpell(SpellDefinition definition)
	{
		if (_battleEnded)
		{
			return;
		}

		_selectionMode = BattleSelectionMode.Spell;
		_deck.Disarm();
		_spellDeck.Arm(definition);
		AudioDirector.Instance?.PlaySpellArm();
		SetStatus($"{definition.DisplayName} · {(_cardDragging ? "Release to cast" : "Drag to cast")}");
		TryShowTutorialHint("first_spell_unlock");
		UpdateHud();
	}

	private void ClearArmedSelection()
	{
		if (_selectionMode == BattleSelectionMode.Spell && _spellDeck.HasArmedSpell)
		{
			var spellName = _spellDeck.ArmedSpell.DisplayName;
			_spellDeck.Disarm();
			_selectionMode = BattleSelectionMode.Unit;
			SetStatus($"{spellName} cast cleared.");
			UpdateHud();
			return;
		}

	}

	private bool IsInBattlefield(Vector2 position)
	{
		return position.X >= BattlefieldLeft &&
			position.X <= BattlefieldRight &&
			position.Y >= BattlefieldTop &&
			position.Y <= BattlefieldBottom;
	}

	// Ground taps only aim magic. Units deploy from their card, never from the field.
	private void TryUseSelectionAt(Vector2 clickPosition)
	{
		if (_selectionMode == BattleSelectionMode.Spell && _spellDeck.HasArmedSpell)
		{
			TryCastSpellAt(_spellDeck.ArmedSpell, ClampBattlefieldPoint(clickPosition));
			return;
		}

		SetStatus("Tap a unit card to deploy, or drag magic onto the battlefield.");
	}

	private void TryCastSpellAt(SpellDefinition definition, Vector2 targetPosition)
	{
		var resolved = GameState.Instance.BuildSpellStats(definition);
		if (!_spellDeck.CanCast(definition, resolved.ManaCost, _mana, _battleEnded, _endlessCheckpointActive, out var reason))
		{
			AudioDirector.Instance?.PlaySpellDenied();
			SetStatus(reason);
			return;
		}

		_mana -= resolved.ManaCost;
		_spellDeck.MarkCast(definition, ResolvePlayerSpellCooldown(resolved));
		var effectSummary = ApplySpellEffect(resolved, targetPosition);
		_selectionMode = BattleSelectionMode.Unit;
		_spellsCast++;
		GameState.Instance.AddBountyProgress("spell_casts", 1);
		AudioDirector.Instance?.PlaySpellCast(resolved.EffectType, targetPosition);
		SetStatus($"Cast Lv{resolved.Level} {definition.DisplayName} at lane {Mathf.RoundToInt(targetPosition.Y)}. {effectSummary}");
		UpdateHud();
	}

	private void DeployPlayerUnit(UnitDefinition definition)
	{
		if (_battleEnded || _battlePaused || _endlessCheckpointActive)
		{
			return;
		}

		if (!_deck.CanDeploy(definition, _courage, _battleEnded, out var reason))
		{
			AudioDirector.Instance?.PlayDeployDenied();
			SetStatus(reason);
			UpdateHud();
			return;
		}

		var spawnPosition = CaravanDeployPosition;
		var stats = BuildPlayerUnitStatsForBattle(definition);
		_courage -= stats.Cost;
		_deck.MarkDeployed(definition, ResolvePlayerDeployCooldown(definition));
		_playerDeployments++;
		GameState.Instance.AddBountyProgress("unit_deploys", 1);
		GameState.Instance.AddUnitMasteryXP(definition.Id, MasteryCatalog.XPPerDeploy);
		RecordChallengeDeployment(definition.Id, spawnPosition.Y);
		var deployedUnit = SpawnUnit(Team.Player, stats, spawnPosition);
		BeginWagonExit(deployedUnit);
		ApplyDeployMomentum(deployedUnit);
		ApplyFortifiedDeployBonus(spawnPosition);
		AudioDirector.Instance?.PlayDeploy(definition, WagonDoorExit);
		BattleParticles.SpawnDeployBurst(this, WagonDoorExit, stats.Color);

		var ghostDeployFeedback = BuildChallengeGhostDeployFeedback(definition);
		SetStatus(
			$"Deployed Lv{GameState.Instance.GetUnitLevel(definition.Id)} {stats.Name} from the war wagon.{ghostDeployFeedback}");
		UpdateHud();
	}

	private void RecordChallengeDeployment(string unitId, float spawnY)
	{
		if (!IsChallengeMode || string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		var lanePercent = Mathf.RoundToInt(
			Mathf.InverseLerp(
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding,
				spawnY) * 100f);
		_challengeDeploymentTape.Add(new ChallengeDeploymentRecord
		{
			UnitId = unitId,
			TimeSeconds = _elapsed,
			LanePercent = Mathf.Clamp(lanePercent, 0, 100)
		});
	}

	private void SpawnEnemyUnit(UnitStats stats, Vector2 position)
	{
		// All reinforcements leave the stronghold, including scripted waves and boss escorts,
		// and may take any line across the band, right up to either edge.
		position = new Vector2(EnemySpawnX, Mathf.Clamp(position.Y, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
		GameState.Instance.DiscoverCodexEntry(stats.DefinitionId);
		var unit = SpawnUnit(Team.Enemy, stats, position);
		AudioDirector.Instance?.PlayEnemySpawn(unit);

		if (stats.VisualClass == "boss")
		{
			TriggerBossEntranceBanner(stats);
			BattleParticles.SpawnBossSpawnBurst(this, position, stats.Color);
			AudioDirector.Instance?.PlayBossSpawn();
			MusicPlayer.Instance?.SetBossMusic(true, AudioCatalog.IsFinalBoss(stats.DefinitionId));
			TryShowTutorialHint("first_boss");
		}
	}

	private void TriggerBossEntranceBanner(UnitStats stats)
	{
		_bossEntranceBannerTimer = 2.2f;
		_bossEntranceBannerText = stats.Name;
		_bossEntranceBannerColor = stats.Color.Lightened(0.12f);
		_enemyBaseFlashTimer = Mathf.Max(_enemyBaseFlashTimer, 0.32f);
		SetStatus($"Boss arrival: {stats.Name} entered the battlefield.");
	}

	private Unit SpawnUnit(Team team, UnitStats stats, Vector2 position)
	{
		var unit = UnitPool.Acquire();
		unit.Setup(team, stats, position);
		unit.EnvironmentTint = FieldLighting.Tint;
		unit.GroundShadowsManaged = true;
		unit.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		_campaignPeriodicSummons.Remove(unit);
		unit.ShouldPausePresentation = () => _battlePaused || _endlessCheckpointActive || _battleEnded;
		unit.Visible = true;
		unit.Modulate = Colors.White;
		if (team == Team.Player)
		{
			var ability = UnitActiveAbilityCatalog.GetForUnit(stats.DefinitionId);
			if (ability != null && GameState.Instance.GetUnitLevel(stats.DefinitionId) >= ability.UnlockLevel)
			{
				unit.SetActiveAbility(ability.Id, ability.CooldownSeconds);
			}
		}

		AddChild(unit);
		_units.Add(unit);
		return unit;
	}

	private void SpawnProjectile(Unit attacker, Unit target)
		=> SpawnProjectileDamage(attacker, target, 1f);

	private void SpawnProjectileDamage(Unit attacker, Unit target, float damageScale)
	{
		var projectile = ProjectilePool.Acquire();
		AddChild(projectile);
		projectile.GlobalPosition = attacker.WeaponContactPosition;
		projectile.ShouldPause = () => _battlePaused || _endlessCheckpointActive || _battleEnded;
		var shotStyle = ProjectileStyles.ForUnit(attacker.DefinitionId, attacker.MotionProfile);
		projectile.SetStyle(shotStyle);
		projectile.LaunchGroundY = attacker.GlobalPosition.Y;
		AudioDirector.Instance?.PlayLaunch(attacker, shotStyle.Id);

		var speed = attacker.ProjectileSpeed > 0f ? attacker.ProjectileSpeed : 210f;
		var color = attacker.Tint.Lightened(0.25f);
		var shotTeam = attacker.Team;
		var shotName = attacker.UnitName;
		var shotTint = attacker.Tint;
		var shotSplash = attacker.AttackSplashRadius;
		var shotLifetime = attacker.CombatLifetime;

		// Arrow Ward: reduce incoming projectile damage against player units
		var projectileDamage = attacker.CurrentAttackDamage * damageScale;
		if (attacker.Team == Team.Enemy && target.Team == Team.Player)
		{
			var wardLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.ProjectileWardId);
			if (wardLevel > 0)
			{
				projectileDamage *= Mathf.Max(0.2f, 1f - (wardLevel * 0.08f));
			}
		}

		if (attacker.AttackSplashRadius > 0.05f)
		{
			projectile.Setup(
				target,
				projectileDamage,
				speed,
				color,
				_ =>
				{
					ApplySplashDamage(shotTeam, target.GlobalPosition, projectileDamage, shotSplash, shotTint, shotName);
					return 0f;
				},
				() => !IsInstanceValid(target) || target.IsDead,
				(position, _, hitColor) =>
				{
					SpawnEffect(position, hitColor.Lightened(0.14f), 8f, shotSplash, 0.2f, false);
				});
		}
		else
		{
			var interceptor = FindProjectileShieldInterceptor(attacker, target);
			if (interceptor != null)
			{
				var attackerName = attacker.UnitName;
				projectile.Setup(
					interceptor,
					projectileDamage,
					speed,
					color,
					d => interceptor.TakeDamage(d, attackerName),
					() => !IsInstanceValid(interceptor) || interceptor.IsDead,
					(pos, dmg, hitColor) =>
					{
						TrackDamageDealt(attackerName, dmg);
						AudioDirector.Instance?.PlayShieldBlock(pos);
						SpawnDamageFeedback(pos, dmg, hitColor);
						if (IsInstanceValid(attacker) && attacker.CombatLifetime == shotLifetime) ApplyImpactReaction(attacker, interceptor, dmg, true);
					});
			}
			else
			{
				var attackerName2 = attacker.UnitName;
				projectile.Setup(
					target,
					projectileDamage,
					speed,
					color,
					d => target.TakeDamage(d, attackerName2),
					() => !IsInstanceValid(target) || target.IsDead,
					(pos, dmg, hitColor) =>
					{
						TrackDamageDealt(attackerName2, dmg);
						SpawnDamageFeedback(pos, dmg, hitColor);
						if (IsInstanceValid(attacker) && attacker.CombatLifetime == shotLifetime) ApplyImpactReaction(attacker, target, dmg, true);
					});
			}
		}

	}

	private void TryAttackBase(Unit attacker)
	{
		if ((IsEndlessMode && attacker.Team == Team.Player) || !attacker.DamagesStructures)
		{
			return;
		}

		var targetBase = attacker.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
		if (!attacker.TryBeginAttackPosition(targetBase, BaseCoreRadius))
		{
			return;
		}
		attacker.ScheduleAttackImpact(() => ResolveAttackBase(attacker));
	}

	private void ResolveAttackBase(Unit attacker)
	{
		if (attacker.UsesProjectile)
		{
			SpawnUnitBaseProjectile(attacker);
			return;
		}
		ApplyUnitBaseHit(attacker.Team, attacker.BaseDamage, attacker.Tint, attacker, attacker.CombatLifetime);
	}

	private void ApplyUnitBaseHit(Team team, float damage, Color tint, Unit attacker, uint attackerLifetime)
	{
		if (_battleEnded) return;
		if (team == Team.Player)
		{
			if (_enemyBaseHealth <= 0f) return;
			var gateBreakLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.GateBreakerId);
			var baseDamage = damage * (1f + (gateBreakLevel * 0.08f));
			_enemyBaseHealth -= baseDamage;
			_enemyBaseFlashTimer = 0.22f;
			AudioDirector.Instance?.PlayBaseHit(false, baseDamage);
			BattleParticles.SpawnBaseHitDebris(this, EnemyBaseCorePosition, tint);
			SpawnCombatNumber(EnemyBaseCorePosition + new Vector2(0f, -24f), $"-{Mathf.RoundToInt(baseDamage)}", tint.Lightened(0.18f), 0.44f);
		}
		else
		{
			var busArmorScale = _endlessBusArmorScale > 1.001f ? (1f / _endlessBusArmorScale) : 1f;
			var busDamage = Mathf.Max(1f, damage * busArmorScale * BaseWeaponCatalog.ArmorScale(_wagonArmorLevel));
			_playerBaseHealth -= busDamage;
			RegisterPlayerHullDamage(busDamage);
			if (_endlessDamageReflectRatio > 0.01f && IsInstanceValid(attacker) && attacker.CombatLifetime == attackerLifetime && !attacker.IsDead)
			{
				var reflected = attacker.TakeDamage(busDamage * _endlessDamageReflectRatio);
				if (reflected > 0.5f)
				{
					SpawnDamageFeedback(attacker.Position, reflected, new Color("b8c0ff"));
				}
			}
			_playerBaseFlashTimer = 0.22f;
			AudioDirector.Instance?.PlayBaseHit(true, busDamage);
			BattleParticles.SpawnBaseHitDebris(this, PlayerBaseCorePosition, tint);
			SpawnCombatNumber(PlayerBaseCorePosition + new Vector2(0f, -24f), $"-{Mathf.RoundToInt(busDamage)}", tint.Lightened(0.18f), 0.44f);
		}
		CheckBattleEnd();
	}

	private bool ShouldRepairBus(Unit unit)
	{
		return unit.Team == Team.Player &&
			unit.BusRepairAmount > 0.05f &&
			_playerBaseHealth < _playerBaseMaxHealth - 0.5f;
	}

	private void SimulatePlayerBusSupport(Unit unit, float delta)
	{
		var supportRadius = BaseCoreRadius + 9f;
		if (unit.CanAttackPosition(PlayerBaseCorePosition, supportRadius))
		{
			if (unit.TryBeginAttackPosition(PlayerBaseCorePosition, supportRadius))
			{
				unit.ScheduleAttackImpact(() =>
				{
					if (_playerBaseHealth <= 0) return;
					var repaired = Mathf.Min(unit.BusRepairAmount, _playerBaseMaxHealth - _playerBaseHealth);
					if (repaired > 0.05f)
					{
						_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + repaired);
						_playerBaseFlashTimer = 0.12f;
						AudioDirector.Instance?.PlayBusRepair(repaired);
						SpawnEffect(PlayerBaseCorePosition, unit.Tint.Lightened(0.12f), 7f, 22f, 0.18f);
						SpawnCombatNumber(PlayerBaseCorePosition + new Vector2(_rng.RandfRange(-10f, 10f), -34f), $"+{Mathf.RoundToInt(repaired)}", unit.Tint.Lightened(0.26f), 0.44f);
					}
				});
			}

			return;
		}

		unit.MoveToward(
			PlayerBaseCorePosition,
			delta,
			BattlefieldLeft,
			BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding);
	}

	private bool ShouldApproachBase(Unit unit)
	{
		if (unit.Team == Team.Player)
		{
			return unit.Position.X >= EnemyBaseX - BaseApproachDistance;
		}

		return unit.Position.X <= PlayerBaseX + BaseApproachDistance;
	}

	private void SimulateUnits(float delta)
	{
		ApplyTeamAuras();
		ApplyComboPairBonuses();
		ApplyEndlessBoonEffects(delta);
		ApplyCampaignMomentumEffects();
		ApplyCursedGroundAttrition(delta);
		ApplyWeatherUnitModifiers();

		// Reinforcements join on the next tick; summoning must not invalidate iteration.
		var actingCount = _units.Count;
		for (var unitIndex = 0; unitIndex < actingCount; unitIndex++)
		{
			var unit = _units[unitIndex];
			if (unit.IsDead)
			{
				continue;
			}

			if (TickWagonExit(unit, delta)) continue;
			if (_pendingBossPhases.ContainsKey(unit)) continue;
			unit.TickAttackTimer(delta);
			if (_battleEnded) return;
			unit.TickSpecialTimer(delta);
			unit.TickActiveAbilityTimer(delta);
			if (unit.IsDead || unit.IsAttackCommitted) continue;
			var defendingBase = unit.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
			if (!(IsEndlessMode && unit.Team == Team.Player) && unit.DamagesStructures && unit.IsAttackingPosition(defendingBase) &&
				unit.CanAttackPosition(defendingBase, BaseCoreRadius) && !ShouldRepairBus(unit))
			{
				TryAttackBase(unit);
				continue;
			}
			if (CanUseCampaignEnemySpecial(unit) && TryTriggerEnemySpecialAbility(unit))
			{
				continue;
			}
			var target = FindClosestEnemy(unit);
			if (target != null && unit.CanAttack(target))
			{
				TryTriggerPlayerActiveAbility(unit, target);
			}
			if (unit.IsAttackCommitted) continue;
			var prioritizeObjectiveRaid = ShouldPrioritizeObjectiveRaid(unit, target);

			// Point-blank, ranged units fight hand to hand instead of shooting: whoever is in their face
			// comes before the target they would rather shoot, even one out of range.
			var meleeThreat = unit.UsesProjectile && !prioritizeObjectiveRaid ? FindMeleeThreat(unit) : null;
			if (meleeThreat != null)
			{
				if (unit.TryBeginMeleeAttack(meleeThreat))
				{
					AudioDirector.Instance?.PlaySwing(unit, MeleeContactProfile(unit));
					QueueRangedMelee(unit, meleeThreat);
				}
			}
			else if (target != null && !prioritizeObjectiveRaid && unit.CanAttack(target))
			{
				if (unit.TryBeginAttack(target))
				{
					if (!unit.UsesProjectile) AudioDirector.Instance?.PlaySwing(unit);
					QueueUnitStrike(unit, target);
				}
			}
			else if (target != null && !prioritizeObjectiveRaid)
			{
				if (unit.UsesProjectile)
				{
					SimulateRangedPositioning(unit, target, delta, false);
				}
				else
				{
					unit.MoveToward(
						target.Position,
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
			}
			else
			{
				var targetBase = unit.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
				if (ShouldRepairBus(unit))
				{
					SimulatePlayerBusSupport(unit, delta);
				}
				else if (unit.CanAttackPosition(targetBase, BaseCoreRadius))
				{
					if (unit.Team == Team.Enemy && string.Equals(unit.SpecialAbilityId, "siege_deploy", StringComparison.OrdinalIgnoreCase))
					{
						TrySiegeTowerDeploy(unit);
					}
					else if (unit.DamagesStructures)
					{
						TryAttackBase(unit);
					}
					// Other ranged troops hold at firing range, covering the attackers without hurting the walls.
				}
				else if (prioritizeObjectiveRaid || ShouldApproachBase(unit))
				{
					unit.MoveToward(
						targetBase,
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
				else if (TryHoldFormation(unit, delta))
				{
				}
				else
				{
					unit.Advance(
						delta,
						BattlefieldLeft,
						BattlefieldRight,
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding);
				}
			}
		}
	}

	private void ApplyFriendlyUnitSeparation(float delta)
	{
		var smoothing = Mathf.Clamp(delta * 9f, 0f, 1f);
		if (smoothing <= 0.001f)
		{
			return;
		}

		for (var i = 0; i < _units.Count; i++)
		{
			var unitA = _units[i];
			if (unitA.IsDead || IsHoldingAttackPosition(unitA) || _wagonExits.ContainsKey(unitA))
			{
				continue;
			}

			for (var j = i + 1; j < _units.Count; j++)
			{
				var unitB = _units[j];
				if (unitB.IsDead || IsHoldingAttackPosition(unitB) || _wagonExits.ContainsKey(unitB) || unitA.Team != unitB.Team)
				{
					continue;
				}

				var deltaPos = unitB.Position - unitA.Position;
				var minDistance = (unitA.Radius + unitB.Radius) * 0.78f;
				var distanceSquared = deltaPos.LengthSquared();
				if (distanceSquared >= minDistance * minDistance)
				{
					continue;
				}

				var distance = Mathf.Sqrt(Mathf.Max(0.0001f, distanceSquared));
				var overlap = minDistance - distance;
				if (overlap <= 0.01f)
				{
					continue;
				}

				Vector2 normal;
				if (distanceSquared <= 0.0001f)
				{
					var verticalSign = ((i + j) & 1) == 0 ? -1f : 1f;
					var horizontalSign = unitA.Team == Team.Player ? -0.24f : 0.24f;
					normal = new Vector2(horizontalSign, verticalSign).Normalized();
				}
				else
				{
					normal = deltaPos / distance;
					var fallbackY = normal.Y == 0f ? (((i + j) & 1) == 0 ? -1f : 1f) : normal.Y;
					normal = new Vector2(normal.X * 0.38f, fallbackY * 1.25f).Normalized();
				}

				var separation = overlap * 0.5f * smoothing;
				var push = new Vector2(normal.X * separation * 0.65f, normal.Y * separation);
				OffsetUnitWithinBattlefield(unitA, -push);
				OffsetUnitWithinBattlefield(unitB, push);
			}
		}
	}

	private bool IsHoldingAttackPosition(Unit unit) => unit.IsAttackCommitted ||
		(_targetLocks.TryGetValue(unit, out var target) && IsValidCombatTarget(unit, target) && unit.CanAttack(target)) ||
		unit.CanAttackPosition(unit.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition, BaseCoreRadius);

	private void SimulateRangedPositioning(Unit unit, Unit target, float delta, bool targetInRange)
	{
		if (targetInRange || unit.IsDead || target.IsDead || unit.CanAttack(target)) return;
		unit.MoveToward(target.Position, delta, BattlefieldLeft, BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
	}

	private bool TryHoldFormation(Unit unit, float delta)
	{
		if (!ShouldHoldFormation(unit) || !TryGetFormationAnchor(unit, out var anchorPosition))
		{
			return false;
		}

		var direction = unit.Team == Team.Player ? 1f : -1f;
		var forwardGap = (anchorPosition.X - unit.Position.X) * direction;
		// Support troops wait for a leader ahead of them, but never fall back to one behind:
		// out in front, they keep marching toward the far end.
		if (forwardGap < -12f)
		{
			return false;
		}

		var laneGap = Mathf.Abs(anchorPosition.Y - unit.Position.Y);
		if (forwardGap <= 12f && laneGap <= 10f)
		{
			return true;
		}

		var moveDelta = delta;
		if (forwardGap > FormationBacklineCatchupThreshold)
		{
			moveDelta *= 1.14f;
		}

		unit.MoveToward(
			new Vector2(forwardGap > 0f ? anchorPosition.X : unit.Position.X, anchorPosition.Y),
			moveDelta,
			BattlefieldLeft,
			BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding);
		return true;
	}

	private bool TryGetFormationAnchor(Unit unit, out Vector2 anchorPosition)
	{
		anchorPosition = unit.Position;
		var direction = unit.Team == Team.Player ? 1f : -1f;
		var laneTolerance = Mathf.Max(FormationLaneTolerance, unit.AggroRangeY * 1.18f);
		Unit bestLeader = null;
		var bestScore = float.MinValue;

		foreach (var ally in _units)
		{
			if (ally == unit || ally.IsDead || ally.Team != unit.Team)
			{
				continue;
			}

			var laneOffset = Mathf.Abs(ally.Position.Y - unit.Position.Y);
			if (laneOffset > laneTolerance)
			{
				continue;
			}

			var forwardOffset = (ally.Position.X - unit.Position.X) * direction;
			if (forwardOffset < -66f)
			{
				continue;
			}
			if (forwardOffset < -12f && !IsFormationLeader(ally))
			{
				continue;
			}

			var score = (forwardOffset * 1.18f) - laneOffset;
			if (IsFormationLeader(ally))
			{
				score += 34f;
			}
			else if (ShouldHoldFormation(ally))
			{
				score -= 10f;
			}

			if (score <= bestScore)
			{
				continue;
			}

			bestScore = score;
			bestLeader = ally;
		}

		if (bestLeader == null)
		{
			return false;
		}

		var trailingDistance = ResolveFormationTrailingDistance(unit, bestLeader);
		var desiredX = bestLeader.Position.X - (direction * trailingDistance);
		var desiredY = Mathf.Lerp(unit.Position.Y, bestLeader.Position.Y, 0.7f);
		anchorPosition = new Vector2(
			Mathf.Clamp(desiredX, BattlefieldLeft + 24f, BattlefieldRight - 24f),
			Mathf.Clamp(desiredY, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
		return true;
	}

	private static bool ShouldHoldFormation(Unit unit)
	{
		return unit.BusRepairAmount > 0.05f ||
			unit.ProvidesAura ||
			(unit.UsesProjectile && unit.AttackRange >= 96f) ||
			unit.VisualClass is "support" or "banner" or "howler";
	}

	private static bool IsFormationLeader(Unit unit)
	{
		return !ShouldHoldFormation(unit) || unit.VisualClass is "banner" or "howler";
	}

	private static float ResolveFormationTrailingDistance(Unit unit, Unit leader)
	{
		var trailingDistance = unit.UsesProjectile
			? Mathf.Clamp(unit.AttackRange * 0.42f, 21f, 48f)
			: 24f;
		if (unit.ProvidesAura)
		{
			trailingDistance = Mathf.Min(trailingDistance, Mathf.Clamp(unit.AuraRadius * 0.34f, 11f, 31f));
		}
		if (unit.BusRepairAmount > 0.05f)
		{
			trailingDistance = Mathf.Max(trailingDistance, 29f);
		}
		if (unit.VisualClass == "banner")
		{
			trailingDistance = 18f;
		}

		return Mathf.Clamp(trailingDistance + (leader.Radius * 0.2f), 18f, 55f);
	}

	private void OffsetUnitWithinBattlefield(Unit unit, Vector2 offset)
	{
		var nextPosition = unit.Position + offset;
		unit.Position = new Vector2(
			Mathf.Clamp(nextPosition.X, BattlefieldLeft + 18f, BattlefieldRight - 18f),
			Mathf.Clamp(nextPosition.Y, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
	}

	private void ApplyImpactReaction(Unit attacker, Unit target, float appliedDamage, bool ranged)
	{
		ShowWeaponContact(attacker,target,appliedDamage,ranged);
		if (appliedDamage <= 0.05f ||
			!IsInstanceValid(attacker) ||
			!IsInstanceValid(target) ||
			attacker.IsDead ||
			target.IsDead)
		{
			return;
		}

		// Routine hits must not shove a shared target out of another swing's
		// contact range. Deliberate ability/hazard pushes remain separate.
		var resistance = ResolveImpactResistance(target);
		ApplyImpactStagger(attacker, target, appliedDamage, resistance, ranged);
		TriggerImpactShake(appliedDamage, ranged);
	}

	private static float ResolveImpactResistance(Unit target)
	{
		return target.VisualClass switch
		{
			"boss" => 0.32f,
			"crusher" => 0.48f,
			"brute" => 0.58f,
			"shield" => 0.64f,
			"siegetower" => 0.18f,
			_ => 1f
		};
	}

	private void ApplyImpactStagger(Unit attacker, Unit target, float appliedDamage, float resistance, bool ranged)
	{
		var damageRatio = Mathf.Clamp(appliedDamage / (ranged ? 16f : 18f), 0.2f, 1f);
		var intensity = resistance * damageRatio;
		var targetSlowScale = Mathf.Lerp(0.92f, ranged ? 0.74f : 0.58f, intensity);
		var targetSlowDuration = (ranged ? RangedImpactSlowDurationSeconds : MeleeImpactSlowDurationSeconds) * Mathf.Lerp(0.5f, 1f, intensity);
		target.ApplyTemporarySpeedModifier(targetSlowScale, targetSlowDuration);

		if (!ranged)
		{
			var attackerRecoveryScale = Mathf.Lerp(0.94f, 0.84f, damageRatio);
			attacker.ApplyTemporarySpeedModifier(attackerRecoveryScale, 0.045f);
		}
	}

	// The fall, landing and dissolve follow the authored death clip: the thud, dust and camera
	// accent land on the frame the body hits the ground, not the moment of death.
	private void PresentUnitDeath(Unit deadUnit)
	{
		var boss = deadUnit.VisualClass == "boss";
		var corpse = deadUnit.SpawnDeathVisual(this);
		// The boss stinger already played as the kill was counted (CleanupDeadUnits); here the body falls.
		AudioDirector.Instance?.PlayUnitDeath(deadUnit, corpse?.Style);
		if (boss && !_units.Any(u => u != deadUnit && !u.IsDead && u.VisualClass == "boss")) MusicPlayer.Instance?.SetBossMusic(false);
		if (corpse == null)
		{
			BattleParticles.SpawnDeathBurst(this, deadUnit.Position, deadUnit.Tint, boss);
			return;
		}
		BattleDeathEffects.SpawnDeathMoment(this, deadUnit.BodyContactPosition, corpse.Style);
		corpse.Impacted = body =>
		{
			AudioDirector.Instance?.PlayBodyFall(body.ImpactPosition, body.Style.Fx, body.Style.Heavy, body.Style.Boss);
			BattleDeathEffects.SpawnImpact(this, body);
			if (body.Style.Heavy && !IsReducedMotionEnabled())
			{
				_impactShakeStrength = Mathf.Max(_impactShakeStrength, body.Style.Boss ? .6f : .28f);
				_impactShakeTimer = Mathf.Max(_impactShakeTimer, ImpactShakeDurationSeconds * (body.Style.Boss ? 2.2f : 1.2f));
			}
		};
		corpse.DissolveStarted = body => BattleDeathEffects.SpawnDissolve(this, body, body.Style.Boss ? 1.3f : .7f);
	}

	private void TriggerImpactShake(float appliedDamage, bool ranged)
	{
		if (appliedDamage < 24f || IsReducedMotionEnabled())
		{
			return;
		}

		// A restrained accent for heavier blows, not a screen shake per sword tap.
		var strength = Mathf.Clamp((appliedDamage-20f) * (ranged?.01f:.018f), 0, ranged?.35f:.65f);

		_impactShakeStrength = Mathf.Max(_impactShakeStrength, strength);
		_impactShakeTimer = Mathf.Max(_impactShakeTimer, ImpactShakeDurationSeconds * (ranged ? 0.82f : 1f));
	}

	private void TryTriggerPlayerActiveAbility(Unit unit, Unit target)
	{
		if (unit.Team != Team.Player || unit.IsDead || !unit.HasActiveAbility)
		{
			return;
		}

		if (!unit.TryTriggerActiveAbility())
		{
			return;
		}

		_activeAbilitiesTriggered++;
		AudioDirector.Instance?.PlayAbility(unit.ActiveAbilityId, unit.Position);



		unit.FaceCombatTarget(target);
		var targetLife = target.CombatLifetime;
		unit.ScheduleAttackImpact(() =>
		{
			if (!IsInstanceValid(target) || target.CombatLifetime != targetLife) return;
			ResolvePlayerActiveAbility(unit,target);
		});
	}

	private void ResolvePlayerActiveAbility(Unit unit, Unit target)
	{
		switch (unit.ActiveAbilityId)
		{
			case "swordsman_cleave":
				ActiveAbilityCleave(unit);
				break;
			case "archer_volley":
				ActiveAbilityArrowVolley(unit);
				break;
			case "shield_knight_wall":
				ActiveAbilityShieldWall(unit);
				break;
			case "spearman_thrust":
				ActiveAbilityPiercingThrust(unit, target);
				break;
			case "crossbow_snipe":
				ActiveAbilitySnipe(unit);
				break;
			case "cavalry_charge":
				ActiveAbilityCharge(unit);
				break;
			case "mage_beam":
				ActiveAbilityArcaneBeam(unit);
				break;
			case "halberdier_sweep":
				ActiveAbilitySweepingStrike(unit);
				break;
			case "alchemist_bomb":
				ActiveAbilityVolatileFlask(unit, target);
				break;
			case "monk_blessing":
				ActiveAbilityBlessing(unit);
				break;
			case "hound_howl":
				ActiveAbilityPackHowl(unit);
				break;
			case "banner_inspire":
				ActiveAbilityInspire(unit);
				break;
			case "rogue_vanish":
				ActiveAbilityVanish(unit);
				break;
			case "berserker_frenzy":
				ActiveAbilityBloodFrenzy(unit);
				break;
			case "lantern_guard_bulwark":
				ActiveAbilityShieldWall(unit);
				break;
			case "ballista_anchor_shot":
				ActiveAbilitySnipe(unit);
				break;
			case "stormcaller_overcharge":
				ActiveAbilityArcaneBeam(unit);
				break;
		}
	}

	private void ActiveAbilityCleave(Unit unit)
	{
		var damage = unit.CurrentAttackDamage * 1.5f;
		var radius = Mathf.Max(48f, unit.AttackRange * 0.8f);
		ApplySplashDamage(unit.Team, unit.Position, damage, radius, unit.Tint, unit.UnitName);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.12f), 10f, radius, 0.24f, false);
	}

	private void ActiveAbilityArrowVolley(Unit unit)
	{
		var fired = 0;
		foreach (var enemy in _units)
		{
			if (fired >= 3)
			{
				break;
			}

			if (enemy.IsDead || enemy.Team == unit.Team || enemy.IsUntargetable)
			{
				continue;
			}

			if (unit.Position.DistanceTo(enemy.Position) > unit.AttackRange + 20f)
			{
				continue;
			}

			SpawnProjectile(unit, enemy);
			fired++;
		}


	}

	private void ActiveAbilityShieldWall(Unit unit)
	{
		unit.ApplyTemporaryDefenseModifier(0.4f, 4f);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.18f), 8f, 36f, 0.28f, false);
	}

	private void ActiveAbilityPiercingThrust(Unit unit, Unit target)
	{
		if (!unit.CanResolveContact(target,target.CombatLifetime,false)) return;
		// Ignore defense: scale raw damage to cancel the target's DamageTakenScale
		var rawDamage = unit.CurrentAttackDamage * 2f;
		var compensated = rawDamage / Mathf.Max(0.05f, target.DamageTakenScale);
		var applied = target.TakeDamage(compensated, unit.UnitName);
		ShowWeaponContact(unit,target,applied,false);
		SpawnDamageFeedback(target.Position, applied, unit.Tint);
	}

	private void ActiveAbilitySnipe(Unit unit)
	{
		Unit farthest = null;
		var farthestDist = 0f;
		foreach (var enemy in _units)
		{
			if (enemy.IsDead || enemy.Team == unit.Team || enemy.IsUntargetable)
			{
				continue;
			}

			var dist = unit.Position.DistanceTo(enemy.Position);
			if (dist <= unit.AttackRange + 20f && dist > farthestDist)
			{
				farthestDist = dist;
				farthest = enemy;
			}
		}

		if (farthest != null)
		{
			if (unit.MotionProfile == "ballista")
			{
				SpawnProjectileDamage(unit, farthest, 3f);
				return;
			}
			var damage = unit.CurrentAttackDamage * 3f;
			var applied = farthest.TakeDamage(damage, unit.UnitName);
			ShowReleaseTrace(unit,farthest);
			ShowWeaponContact(unit,farthest,applied,true);
			SpawnDamageFeedback(farthest.Position, applied, unit.Tint);
		}
	}

	private void ActiveAbilityCharge(Unit unit)
	{
		var chargeDistance = Mathf.Max(80f, unit.AttackRange * 1.5f);
		var direction = unit.Team == Team.Player ? Vector2.Right : Vector2.Left;
		var chargeEnd = unit.Position + direction * chargeDistance;
		var damage = unit.CurrentAttackDamage * 1.5f;
		var hitCount = 0;

		foreach (var enemy in _units)
		{
			if (enemy.IsDead || enemy.Team == unit.Team || enemy.IsUntargetable)
			{
				continue;
			}

			// Check if enemy is within a wide line along the charge path
			var toEnemy = enemy.Position - unit.Position;
			var projection = toEnemy.Dot(direction);
			if (projection < 0f || projection > chargeDistance)
			{
				continue;
			}

			var perpendicular = Mathf.Abs(toEnemy.Y - direction.Y * projection);
			if (perpendicular > 32f)
			{
				continue;
			}

			var applied = enemy.TakeDamage(damage, unit.UnitName);
			SpawnDamageFeedback(enemy.Position, applied, unit.Tint);
			hitCount++;
		}

		unit.MoveToward(
			chargeEnd,
			0.5f,
			BattlefieldLeft,
			BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.1f), 8f, 44f, 0.22f, false);
	}

	private void ActiveAbilityArcaneBeam(Unit unit)
	{
		var damagePerTarget = unit.CurrentAttackDamage * 4f / 2f;
		var hit = 0;
		foreach (var enemy in _units)
		{
			if (hit >= 2)
			{
				break;
			}

			if (enemy.IsDead || enemy.Team == unit.Team || enemy.IsUntargetable)
			{
				continue;
			}

			if (unit.Position.DistanceTo(enemy.Position) > unit.AttackRange + 20f)
			{
				continue;
			}

			var applied = enemy.TakeDamage(damagePerTarget, unit.UnitName);
			ShowReleaseTrace(unit,enemy);
			ShowWeaponContact(unit,enemy,applied,true);
			SpawnDamageFeedback(enemy.Position, applied, unit.Tint);
			hit++;
		}

		if (hit > 0)
		{
			SpawnEffect(unit.Position, unit.Tint.Lightened(0.15f), 6f, 48f, 0.26f, false);
		}
	}

	private void ActiveAbilitySweepingStrike(Unit unit)
	{
		var damage = unit.CurrentAttackDamage * 1.2f;
		var radius = Mathf.Max(56f, unit.AttackRange);
		ApplySplashDamage(unit.Team, unit.Position, damage, radius, unit.Tint, unit.UnitName);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.1f), 10f, radius, 0.22f, false);
	}

	private void ActiveAbilityVolatileFlask(Unit unit, Unit target)
	{
		if (!unit.CanResolveContact(target,target.CombatLifetime,true)) return;
		var damage = unit.CurrentAttackDamage * 2f;
		var radius = Mathf.Max(48f, unit.AttackSplashRadius > 0.05f ? unit.AttackSplashRadius * 1.5f : 48f);
		var team=unit.Team; var tint=unit.Tint; var name=unit.UnitName;
		var projectile=ProjectilePool.Acquire();
		projectile.GlobalPosition=unit.WeaponContactPosition;
		projectile.ShouldPause=()=>_battlePaused || _endlessCheckpointActive || _battleEnded;
		projectile.SetStyle(ProjectileStyles.Flask);
		projectile.LaunchGroundY=unit.GlobalPosition.Y;
		projectile.Setup(target,damage,unit.ProjectileSpeed>0?unit.ProjectileSpeed:360,tint,
			d=>{ ApplySplashDamage(team,target.Position,d,radius,tint,name); return d; },
			()=>!IsInstanceValid(target) || target.IsDead,
			(pos,_,color)=>SpawnEffect(pos,color.Lightened(.14f),10,radius,.26f,false));
		AddChild(projectile);
	}

	private void ActiveAbilityBlessing(Unit unit)
	{
		var healRadius = Mathf.Max(64f, unit.AuraRadius);
		var healed = 0;
		foreach (var ally in _units)
		{
			if (ally.IsDead || ally.Team != unit.Team)
			{
				continue;
			}

			if (ally.Position.DistanceTo(unit.Position) > healRadius)
			{
				continue;
			}

			var applied = ally.Heal(15f);
			if (applied > 0.5f)
			{
				healed++;
			}
		}

		SpawnEffect(unit.Position, unit.Tint.Lightened(0.2f), 10f, healRadius * 0.6f, 0.28f, false);
	}

	private void ActiveAbilityPackHowl(Unit unit)
	{
		// 50% attack speed simulated as 50% increased attack damage (DPS equivalent)
		unit.ApplyTemporaryCombatBuff(1.5f, 1f, 5f);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.12f), 8f, 40f, 0.24f, false);
	}

	private void ActiveAbilityInspire(Unit unit)
	{
		if (unit.ProvidesAura)
		{
			var auraRadius = Mathf.Max(48f, unit.AuraRadius);
			foreach (var ally in _units)
			{
				if (ally.IsDead || ally.Team != unit.Team)
				{
					continue;
				}

				if (ally.Position.DistanceTo(unit.Position) > auraRadius)
				{
					continue;
				}

				ally.ApplyTemporaryCombatBuff(
					unit.AuraAttackDamageScale,
					unit.AuraSpeedScale,
					6f);
			}
		}

		SpawnEffect(unit.Position, unit.Tint.Lightened(0.18f), 10f, 52f, 0.28f, false);
	}

	private void ActiveAbilityVanish(Unit unit)
	{
		unit.SetUntargetable(3f);
		unit.ApplyTemporaryCombatBuff(2.5f, 1f, 3f);
		SpawnEffect(unit.Position, unit.Tint.Darkened(0.3f), 6f, 28f, 0.2f, false);
	}

	private void ActiveAbilityBloodFrenzy(Unit unit)
	{
		// 40% attack speed simulated as 40% increased attack damage (DPS equivalent)
		// Takes 20% more damage during the frenzy
		unit.ApplyTemporaryCombatBuff(1.4f, 1f, 6f);
		unit.ApplyTemporaryDefenseModifier(1.2f, 6f);
		SpawnEffect(unit.Position, unit.Tint.Lightened(0.08f), 8f, 36f, 0.22f, false);
	}

	private bool TryTriggerEnemySpecialAbility(Unit unit)
	{
		if (unit.Team != Team.Enemy || unit.IsDead || !unit.HasSpecialAbility)
		{
			return false;
		}

		var origin = unit.Position;
		var triggered = unit.SpecialAbilityId switch
		{
			"rally_call" => TriggerBossRallyCall(unit),
			"jam_signal" => TriggerEnemySignalJam(unit),
			"raise_fallen" => TriggerEnemyRaiseFallen(unit),
			"projectile_shield" => false, // Passive ability handled in projectile logic
			"siege_deploy" => false, // Handled in movement/base-approach logic
			"burrow" => TriggerEnemyBurrow(unit),
			_ => false
		};
		if (triggered)
		{
			var sound = unit.SpecialAbilityId switch { "rally_call" => "rally", "jam_signal" => "jam", "raise_fallen" => "raise", "burrow" => "burrow", _ => "" };
			AudioDirector.Instance?.PlayEnemySpecial(sound, origin);
			if (sound == "burrow") AudioDirector.Instance?.PlayEnemySpecial("emerge", unit.Position);
		}
		return triggered;
	}

	private bool TriggerBossRallyCall(Unit boss)
	{
		if (!boss.TryTriggerSpecialAbility())
		{
			return false;
		}

		var buffedCount = 0;
		foreach (var ally in _units)
		{
			if (ally.IsDead || ally.Team != boss.Team)
			{
				continue;
			}

			if (ally.Position.DistanceTo(boss.Position) > Mathf.Max(48f, boss.SpecialBuffRadius))
			{
				continue;
			}

			ally.ApplyTemporaryCombatBuff(
				boss.SpecialBuffAttackDamageScale,
				boss.SpecialBuffSpeedScale,
				boss.SpecialBuffDuration);
			buffedCount++;
		}

		var escortsSpawned = 0;
		if (!string.IsNullOrWhiteSpace(boss.SpecialSpawnUnitId) && boss.SpecialSpawnCount > 0)
		{
			for (var i = 0; i < boss.SpecialSpawnCount; i++)
			{
				if (CountTeamUnits(Team.Enemy) >= _spawnDirector.GetMaxActiveEnemies()) break;
				if (!CanAddBossReinforcement(boss, boss.SpecialSpawnUnitId) || !CanAddCampaignPeriodicReinforcement(boss)) break;
				if (!_spawnDirector.TryBuildEnemyStats(boss.SpecialSpawnUnitId, out var escortStats))
				{
					break;
				}

				var escortPosition = new Vector2(
					Mathf.Clamp(boss.Position.X + _rng.RandfRange(-26f, 26f), BattlefieldLeft + 20f, BattlefieldRight - 20f),
					Mathf.Clamp(
						boss.Position.Y + _rng.RandfRange(-58f, 58f),
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding));
				SpawnEnemyUnit(escortStats, escortPosition);
				RecordCampaignPeriodicReinforcement(boss);
				escortsSpawned++;
			}
		}

		SpawnEffect(boss.Position, boss.Tint.Lightened(0.15f), 12f, Mathf.Max(56f, boss.SpecialBuffRadius * 0.55f), 0.28f, false);
		SetStatus(
			$"{boss.UnitName} rally call: {buffedCount} undead surged forward" +
			(escortsSpawned > 0 ? $" and {escortsSpawned} escorts joined the push." : "."));
		return true;
	}

	private bool TriggerEnemySignalJam(Unit jammer)
	{
		// Multiple hexers must not repeatedly delay every card or keep the economy locked forever.
		if (_enemySignalJamTimer > 0f || _elapsed < _enemySignalJamRecoveryUntil) return false;
		if (!jammer.TryTriggerSpecialAbility())
		{
			return false;
		}

		var jamDuration = GameState.Instance.ApplyPlayerSignalJamDurationUpgrade(
			Mathf.Max(3.5f, jammer.SpecialBuffDuration));
		var jamScale = GameState.Instance.ApplyPlayerSignalJamCourageGainScaleUpgrade(
			Mathf.Clamp(jammer.SpecialCourageGainScale, 0.25f, 1f));
		var cooldownPenalty = GameState.Instance.ApplyPlayerSignalJamCooldownPenaltyUpgrade(
			Mathf.Max(0f, jammer.SpecialDeployCooldownPenalty));
		_enemySignalJamTimer = Mathf.Max(_enemySignalJamTimer, jamDuration);
		_enemySignalJamCourageGainScale = Mathf.Min(_enemySignalJamCourageGainScale, jamScale);
		if (cooldownPenalty > 0.05f)
		{
			_deck.IncreaseCooldowns(cooldownPenalty);
			_spellDeck.IncreaseCooldowns(cooldownPenalty);
		}

		// Boss-class jammers can also spawn escorts alongside the jam effect
		var escortsSpawned = 0;
		if (!string.IsNullOrWhiteSpace(jammer.SpecialSpawnUnitId) && jammer.SpecialSpawnCount > 0)
		{
			for (var i = 0; i < jammer.SpecialSpawnCount; i++)
			{
				if (CountTeamUnits(Team.Enemy) >= _spawnDirector.GetMaxActiveEnemies()) break;
				if (!CanAddCampaignPeriodicReinforcement(jammer)) break;
				if (!_spawnDirector.TryBuildEnemyStats(jammer.SpecialSpawnUnitId, out var escortStats))
				{
					break;
				}

				var escortPosition = new Vector2(
					Mathf.Clamp(jammer.Position.X + _rng.RandfRange(-28f, 28f), BattlefieldLeft + 20f, BattlefieldRight - 20f),
					Mathf.Clamp(
						jammer.Position.Y + _rng.RandfRange(-52f, 52f),
						BattlefieldTop + SpawnVerticalPadding,
						BattlefieldBottom - SpawnVerticalPadding));
				SpawnEnemyUnit(escortStats, escortPosition);
				RecordCampaignPeriodicReinforcement(jammer);
				escortsSpawned++;
			}
		}

		SpawnEffect(jammer.Position, jammer.Tint.Lightened(0.08f), 12f, 54f, 0.26f, false);
		SpawnEffect(PlayerBaseCorePosition, jammer.Tint, 10f, 36f, 0.24f, false);

		var statusMsg = $"Enemy hexer disrupted caravan rhythm: courage gain suppressed for {jamDuration:0.0}s and card recovery delayed.";
		if (escortsSpawned > 0)
		{
			statusMsg += $" {escortsSpawned} escorts emerged from the disruption.";
		}
		SetStatus(statusMsg);
		return true;
	}

	private bool TriggerEnemyRaiseFallen(Unit lich)
	{
		if (!lich.TryTriggerSpecialAbility())
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(lich.SpecialSpawnUnitId) || lich.SpecialSpawnCount <= 0)
		{
			return false;
		}

		var spawned = 0;
		for (var i = 0; i < lich.SpecialSpawnCount; i++)
		{
			if (CountTeamUnits(Team.Enemy) >= _spawnDirector.GetMaxActiveEnemies()) break;
			if (!CanAddBossReinforcement(lich, lich.SpecialSpawnUnitId) || !CanAddCampaignPeriodicReinforcement(lich)) break;
			if (!_spawnDirector.TryBuildEnemyStats(lich.SpecialSpawnUnitId, out var raisedStats))
			{
				break;
			}

			var spawnPosition = new Vector2(
				Mathf.Clamp(lich.Position.X + _rng.RandfRange(-40f, 40f), BattlefieldLeft + 20f, BattlefieldRight - 20f),
				Mathf.Clamp(
					lich.Position.Y + _rng.RandfRange(-50f, 50f),
					BattlefieldTop + SpawnVerticalPadding,
					BattlefieldBottom - SpawnVerticalPadding));
			SpawnEnemyUnit(raisedStats, spawnPosition);
			RecordCampaignPeriodicReinforcement(lich);
			spawned++;
		}

		if (spawned > 0)
		{
			SpawnEffect(lich.Position, lich.Tint.Lightened(0.1f), 10f, 48f, 0.26f, false);
			SetStatus($"Enemy lich raised {spawned} fallen undead from the battlefield.");
		}

		return spawned > 0;
	}

	private bool TriggerEnemyBurrow(Unit tunneler)
	{
		if (!tunneler.TryTriggerSpecialAbility())
		{
			return false;
		}

		// Find the rearmost player unit to burrow behind
		Unit rearTarget = null;
		var leftmostX = float.MaxValue;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			if (unit.Position.X < leftmostX)
			{
				leftmostX = unit.Position.X;
				rearTarget = unit;
			}
		}

		if (rearTarget == null)
		{
			return false;
		}

		var burrowTarget = new Vector2(
			Mathf.Clamp(rearTarget.Position.X - 30f, BattlefieldLeft + 20f, BattlefieldRight - 20f),
			Mathf.Clamp(
				rearTarget.Position.Y + _rng.RandfRange(-40f, 40f),
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding));

		SpawnEffect(tunneler.Position, tunneler.Tint, 8f, 22f, 0.2f, false);
		tunneler.Position = burrowTarget;
		SpawnEffect(tunneler.Position, tunneler.Tint.Lightened(0.12f), 6f, 28f, 0.22f, false);
		SetStatus("Enemy tunneler burrowed behind the caravan lines.");
		return true;
	}

	private void TriggerChallengeMutatorSignalJam()
	{
		var jamDuration = GameState.Instance.ApplyPlayerSignalJamDurationUpgrade(
			Mathf.Max(2.5f, _challengeMutator.SignalJamDurationSeconds));
		var jamScale = GameState.Instance.ApplyPlayerSignalJamCourageGainScaleUpgrade(
			Mathf.Clamp(_challengeMutator.SignalJamCourageGainScale, 0.25f, 1f));
		var cooldownPenalty = GameState.Instance.ApplyPlayerSignalJamCooldownPenaltyUpgrade(
			Mathf.Max(0f, _challengeMutator.SignalJamCooldownPenalty));
		_enemySignalJamTimer = Mathf.Max(_enemySignalJamTimer, jamDuration);
		_enemySignalJamCourageGainScale = Mathf.Min(_enemySignalJamCourageGainScale, jamScale);
		if (cooldownPenalty > 0.05f)
		{
			_deck.IncreaseCooldowns(cooldownPenalty);
			_spellDeck.IncreaseCooldowns(cooldownPenalty);
		}

		var blackoutColor = new Color("93c5fd");
		SpawnEffect(EnemyBaseCorePosition, blackoutColor, 12f, 42f, 0.24f, false);
		SpawnEffect(PlayerBaseCorePosition, blackoutColor.Lightened(0.08f), 10f, 38f, 0.24f, false);


		SetStatus($"Challenge mutator blackout hit caravan signals for {jamDuration:0.0}s.");
	}

	private void ApplyTeamAuras()
	{
		foreach (var unit in _units)
		{
			if (!unit.IsDead)
			{
				unit.ResetCombatModifiers();
			}
		}

		foreach (var source in _units)
		{
			if (source.IsDead || !source.ProvidesAura)
			{
				continue;
			}

			foreach (var target in _units)
			{
				if (target == source || target.IsDead || target.Team != source.Team)
				{
					continue;
				}

				if (target.Position.DistanceTo(source.Position) > source.AuraRadius)
				{
					continue;
				}

				target.ApplyCombatAura(source.AuraAttackDamageScale, source.AuraSpeedScale);
			}
		}
	}

	private void ApplyWeatherUnitModifiers()
	{
		if (_weatherSpeedScale > 0.999f && _weatherSpeedScale < 1.001f &&
			_weatherDamageScale > 0.999f && _weatherDamageScale < 1.001f)
		{
			return;
		}

		foreach (var unit in _units)
		{
			if (!unit.IsDead)
			{
				unit.ApplyWeatherModifiers(_weatherSpeedScale, _weatherDamageScale);
			}
		}
	}

	private void ApplyCampaignMomentumEffects()
	{
		if (_campaignMomentumBoostRemaining <= 0.05f || _campaignMomentumStacks <= 0)
		{
			return;
		}

		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			unit.ApplyTemporaryCombatBuff(_campaignMomentumAttackScale, _campaignMomentumSpeedScale, 0.2f);
		}
	}

	private void TryTriggerCampaignBossPhase()
	{
		if (!IsCampaignMode || _battleEnded)
		{
			return;
		}

		// Reinforcements join on the next tick; summoning must not invalidate iteration.
		var actingCount = _units.Count;
		for (var unitIndex = 0; unitIndex < actingCount; unitIndex++)
		{
			var unit = _units[unitIndex];
			if (!IsCampaignBossPhaseUnit(unit) || _campaignBossPhaseTriggeredUnits.Contains(unit))
			{
				continue;
			}

			if (unit.HealthRatio > CampaignBossPhaseThresholdRatio)
			{
				continue;
			}

			if (!PrepareBossPhase(unit)) continue;
			_campaignBossPhaseTriggeredUnits.Add(unit);
			ApplyCampaignBossPhase(unit);
		}
	}

	private bool IsCampaignBossPhaseUnit(Unit unit)
	{
		return unit != null &&
			!unit.IsDead &&
			unit.Team == Team.Enemy &&
			unit.VisualClass == "boss" &&
			!string.IsNullOrWhiteSpace(unit.DefinitionId) &&
			unit.DefinitionId.StartsWith(GameData.EnemyBossId, StringComparison.OrdinalIgnoreCase);
	}

	private void ApplyCampaignBossPhase(Unit boss)
	{
		var color = boss.Tint.Lightened(0.08f);
		SpawnEffect(boss.Position, color, 14f, 64f, 0.3f, false);


		switch (boss.DefinitionId)
		{
			case GameData.EnemyBossDocksId:
				DamagePlayersNear(boss.Position + new Vector2(-18f, 0f), 52f, 20f, color);
				SlowPlayersNear(boss.Position, 59f, 0.72f, 4.2f, color);
				PushPlayersFromPoint(boss.Position, 54f, 11f, 0.82f, 2.8f, color);
				SetStatus($"{boss.UnitName} unleashed Undertow and shoved the caravan line back.");
				break;
			case GameData.EnemyBossForgeId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				DamagePlayersNear(boss.Position, 46f, 18f, color);
				BuffUnitsNear(Team.Enemy, boss.Position, 66f, 1.12f, 1.06f, 6.5f, color);
				SetStatus($"{boss.UnitName} entered Forge Surge" + (escorts > 0 ? $": {escorts} escorts reinforced the breach." : "."));
				break;
			}
			case GameData.EnemyBossWardId:
			{
				_enemySignalJamTimer = Mathf.Max(_enemySignalJamTimer, 5.25f);
				_enemySignalJamCourageGainScale = Mathf.Min(_enemySignalJamCourageGainScale, 0.62f);
				_deck.IncreaseCooldowns(0.8f);
				_spellDeck.IncreaseCooldowns(0.8f);
				SlowPlayersNear(boss.Position, 60f, 0.76f, 4.2f, color);
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, 1);
				SetStatus($"{boss.UnitName} blackout phase hit caravan signals" + (escorts > 0 ? " and fresh hexers joined the field." : "."));
				break;
			}
			case GameData.EnemyBossPassId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(2, boss.SpecialSpawnCount));
				BuffUnitsNear(Team.Enemy, boss.Position, 75f, 1.08f, 1.18f, 7f, color);
				PushPlayersFromPoint(boss.Position, 51f, 8f, 0.86f, 2.4f, color);
				SetStatus($"{boss.UnitName} called a stampede" + (escorts > 0 ? $": {escorts} fast escorts flooded the lane." : "."));
				break;
			}
			case GameData.EnemyBossBasilicaId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				HealUnit(boss, boss.MaxHealth * 0.12f, color);
				RepairEnemyBaseByRatio(0.04f, color);
				SetStatus($"{boss.UnitName} invoked Crypt Vow" + (escorts > 0 ? $" and {escorts} ritual escorts answered the call." : "."));
				break;
			}
			case GameData.EnemyBossMireId:
				DamagePlayersNear(boss.Position, 55f, 22f, color);
				SlowPlayersNear(boss.Position, 64f, 0.66f, 4.6f, color);
				HealUnit(boss, boss.MaxHealth * 0.1f, color);
				SetStatus($"{boss.UnitName} burst into a Rot Swell and dragged the frontline into the mire.");
				break;
			case GameData.EnemyBossSteppeId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(2, boss.SpecialSpawnCount + 1));
				BuffUnitsNear(Team.Enemy, boss.Position, 75f, 1.1f, 1.2f, 7f, color);
				SetStatus($"{boss.UnitName} launched Wolf Run" + (escorts > 0 ? $": {escorts} runners broke from the flank." : "."));
				break;
			}
			case GameData.EnemyBossVergeId:
			{
				var target = FindHighestHealthPlayer();
				if (target != null)
				{
					var appliedDamage = target.TakeDamage(30f, boss.UnitName);
					SpawnDamageFeedback(target.Position, appliedDamage, color);
					target.ApplyTemporarySpeedModifier(0.58f, 4.5f);
					DamagePlayersNear(target.Position, 36f, 14f, color);
				}

				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				SetStatus($"{boss.UnitName} marked the heaviest defender with Hex Bloom" + (escorts > 0 ? $" as {escorts} witchlight escorts closed in." : "."));
				break;
			}
			case GameData.EnemyBossCitadelId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				RepairEnemyBaseByRatio(0.05f, color);
				BuffUnitsNear(Team.Enemy, EnemyBaseCorePosition + new Vector2(-64f, 0f), 70f, 1.12f, 1.06f, 6.5f, color);
				SetStatus($"{boss.UnitName} fortified the keep" + (escorts > 0 ? $" and {escorts} elite escorts took the lane." : "."));
				break;
			}
			case GameData.EnemyBossReliquaryId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				RepairEnemyBaseByRatio(0.05f, color);
				HealUnit(boss, boss.MaxHealth * 0.1f, color);
				SetStatus($"{boss.UnitName} opened the catacombs" + (escorts > 0 ? $" and {escorts} bone artillery crews emerged." : "."));
				break;
			}
			case GameData.EnemyBossAshenRegentId:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				DamagePlayersNear(boss.Position, 58f, 24f, color);
				PushPlayersFromPoint(boss.Position, 58f, 10f, 0.82f, 3f, color);
				BuffUnitsNear(Team.Enemy, boss.Position, 70f, 1.1f, 1.06f, 6f, color);
				SetStatus($"{boss.UnitName} cast Ashfall Edict" + (escorts > 0 ? $" and {escorts} heavy escorts stepped through the smoke." : "."));
				break;
			}
			case GameData.EnemyBossTidemasterId:
				DamagePlayersNear(boss.Position + new Vector2(-20f, 0f), 59f, 26f, color);
				SlowPlayersNear(boss.Position, 66f, 0.64f, 4.8f, color);
				PushPlayersFromPoint(boss.Position, 59f, 13f, 0.8f, 3.2f, color);
				RepairEnemyBaseByRatio(0.03f, color);
				SetStatus($"{boss.UnitName} broke the floodgates and drowned the frontline in pressure.");
				break;
			case GameData.EnemyBossPlagueMonarchId:
			{
				_enemySignalJamTimer = Mathf.Max(_enemySignalJamTimer, 6.25f);
				_enemySignalJamCourageGainScale = Mathf.Min(_enemySignalJamCourageGainScale, 0.48f);
				_deck.IncreaseCooldowns(1.1f);
				_spellDeck.IncreaseCooldowns(1.1f);
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				SlowPlayersNear(boss.Position, 65f, 0.72f, 4.5f, color);
				SetStatus($"{boss.UnitName} blotted out caravan signals" + (escorts > 0 ? $" while {escorts} captains reinforced the assault." : "."));
				break;
			}
			case GameData.EnemyBossId:
			default:
			{
				var escorts = SpawnEnemyEscortsNear(boss, boss.SpecialSpawnUnitId, Mathf.Max(1, boss.SpecialSpawnCount));
				BuffUnitsNear(Team.Enemy, boss.Position, 70f, 1.1f, 1.08f, 6f, color);
				SetStatus($"{boss.UnitName} entered a last stand" + (escorts > 0 ? $" and {escorts} escorts answered the call." : "."));
				break;
			}
		}
	}

	private void DamageEnemiesNear(Vector2 center, float radius, float damage, Color color)
	{
		DamageUnitsNear(Team.Player, center, radius, damage, color);
	}

	private void DamagePlayersNear(Vector2 center, float radius, float damage, Color color)
	{
		DamageUnitsNear(Team.Enemy, center, radius, damage, color);
	}

	private void DamageUnitsNear(Team attackerTeam, Vector2 center, float radius, float damage, Color color)
	{
		ApplySplashDamage(attackerTeam, center, damage, radius, color);
		SpawnEffect(center, color, 10f, radius, 0.24f, false);

	}

	private void SlowEnemiesNear(Vector2 center, float radius, float speedScale, float duration, Color color)
	{
		ApplySpeedModifierNear(Team.Enemy, center, radius, speedScale, duration, color);
	}

	private void SlowPlayersNear(Vector2 center, float radius, float speedScale, float duration, Color color)
	{
		ApplySpeedModifierNear(Team.Player, center, radius, speedScale, duration, color);
	}

	private void ApplySpeedModifierNear(Team targetTeam, Vector2 center, float radius, float speedScale, float duration, Color color)
	{
		var affected = false;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != targetTeam || unit.Position.DistanceTo(center) > radius)
			{
				continue;
			}

			unit.ApplyTemporarySpeedModifier(speedScale, duration);
			affected = true;
		}

		if (!affected)
		{
			return;
		}

		SpawnEffect(center, color.Lightened(0.08f), 10f, radius, 0.2f, false);

	}

	private void BuffAllPlayerUnits(float attackScale, float speedScale, float duration, float defenseScale = 1f)
	{
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			unit.ApplyTemporaryCombatBuff(attackScale, speedScale, duration);
			if (defenseScale < 0.999f || defenseScale > 1.001f)
			{
				unit.ApplyTemporaryDefenseModifier(defenseScale, duration);
			}
		}
	}

	private void BuffUnitsNear(Team targetTeam, Vector2 center, float radius, float attackScale, float speedScale, float duration, Color color)
	{
		var affected = false;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != targetTeam || unit.Position.DistanceTo(center) > radius)
			{
				continue;
			}

			unit.ApplyTemporaryCombatBuff(attackScale, speedScale, duration);
			affected = true;
		}

		if (!affected)
		{
			return;
		}

		SpawnEffect(center, color.Lightened(0.08f), 10f, radius, 0.2f, false);

	}

	private void PushEnemiesFromPoint(Vector2 point, float radius, float pushDistance, float slowScale, float duration, Color color)
	{
		PushUnitsFromPoint(Team.Enemy, point, radius, pushDistance, slowScale, duration, color);
	}

	private void PushPlayersFromPoint(Vector2 point, float radius, float pushDistance, float slowScale, float duration, Color color)
	{
		PushUnitsFromPoint(Team.Player, point, radius, pushDistance, slowScale, duration, color);
	}

	private void PushUnitsFromPoint(Team targetTeam, Vector2 point, float radius, float pushDistance, float slowScale, float duration, Color color)
	{
		var affected = false;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != targetTeam || unit.Position.DistanceTo(point) > radius)
			{
				continue;
			}

			var direction = unit.Position - point;
			if (direction.LengthSquared() <= 0.001f)
			{
				direction = Vector2.Right;
			}
			else
			{
				direction = direction.Normalized();
			}

			OffsetUnitWithinBattlefield(unit, direction * pushDistance);
			unit.ApplyTemporarySpeedModifier(slowScale, duration);
			affected = true;
		}

		if (!affected)
		{
			return;
		}

		var effectOffset = targetTeam == Team.Enemy ? 60f : -60f;
		SpawnEffect(point + new Vector2(effectOffset, 0f), color.Lightened(0.08f), 12f, radius * 0.7f, 0.24f, false);

	}

	private Unit FindHighestHealthEnemy()
	{
		Unit best = null;
		var bestHealth = 0f;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Enemy)
			{
				continue;
			}

			if (unit.Health > bestHealth)
			{
				bestHealth = unit.Health;
				best = unit;
			}
		}

		return best;
	}

	private Unit FindHighestHealthPlayer()
	{
		Unit best = null;
		var bestHealth = 0f;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			if (unit.Health > bestHealth)
			{
				bestHealth = unit.Health;
				best = unit;
			}
		}

		return best;
	}

	private int SpawnEnemyEscortsNear(Unit anchor, string unitId, int count, float xSpread = 16f, float ySpread = 20f)
	{
		if (anchor == null || anchor.IsDead || string.IsNullOrWhiteSpace(unitId) || count <= 0)
		{
			return 0;
		}

		var spawned = 0;
		for (var i = 0; i < count; i++)
		{
			if (CountTeamUnits(Team.Enemy) >= _spawnDirector.GetMaxActiveEnemies()) break;
			if (!CanAddBossReinforcement(anchor, unitId)) break;
			if (!_spawnDirector.TryBuildEnemyStats(unitId, out var escortStats))
			{
				break;
			}

			var escortPosition = new Vector2(
				Mathf.Clamp(anchor.Position.X + _rng.RandfRange(-xSpread, xSpread), BattlefieldLeft + 20f, BattlefieldRight - 20f),
				Mathf.Clamp(
					anchor.Position.Y + _rng.RandfRange(-ySpread, ySpread),
					BattlefieldTop + SpawnVerticalPadding,
					BattlefieldBottom - SpawnVerticalPadding));
			SpawnEnemyUnit(escortStats, escortPosition);
			spawned++;
		}

		return spawned;
	}

	private void HealUnit(Unit unit, float amount, Color color)
	{
		if (unit == null || unit.IsDead || amount <= 0.05f)
		{
			return;
		}

		var healed = unit.Heal(amount);
		if (healed <= 0.05f)
		{
			return;
		}

		SpawnEffect(unit.Position, color.Lightened(0.08f), 10f, 36f, 0.22f, false);
		SpawnCombatNumber(unit.Position + new Vector2(_rng.RandfRange(-10f, 10f), -36f), $"+{Mathf.RoundToInt(healed)}", color.Lightened(0.24f), 0.52f);

	}

	private void ApplyEndlessBoonEffects(float delta)
	{
		// Tick expiry timers
		if (_endlessDamageReflectExpiry > 0f)
		{
			_endlessDamageReflectExpiry -= delta;
			if (_endlessDamageReflectExpiry <= 0f)
			{
				_endlessDamageReflectRatio = 0f;
			}
		}

		if (_endlessTempDamageExpiry > 0f)
		{
			_endlessTempDamageExpiry -= delta;
			if (_endlessTempDamageExpiry <= 0f)
			{
				_endlessTempDamageScale = 1f;
			}
		}

		// Berserker Blood boon: all player units gain mild berserk scaling
		if (_endlessBerserkerBlood)
		{
			foreach (var unit in _units)
			{
				if (unit.IsDead || unit.Team != Team.Player)
				{
					continue;
				}

				var missingRatio = 1f - unit.HealthRatio;
				if (missingRatio > 0.05f)
				{
					unit.ApplyTemporaryCombatBuff(Mathf.Min(1.35f, 1f + (missingRatio * 0.4f)), 1f, 0.2f);
				}
			}
		}

		// Temp damage scale from Berserk Ritual draft
		if (_endlessTempDamageScale > 1.001f)
		{
			foreach (var unit in _units)
			{
				if (unit.IsDead || unit.Team != Team.Player)
				{
					continue;
				}

				unit.ApplyTemporaryCombatBuff(_endlessTempDamageScale, 1f, 0.2f);
			}
		}

	}

	private void ApplyComboPairBonuses()
	{
		const float comboBuffDuration = 0.2f;
		var comboPairs = ComboPairCatalog.GetAll();

		foreach (var combo in comboPairs)
		{
			for (var i = 0; i < _units.Count; i++)
			{
				var unitA = _units[i];
				if (unitA.IsDead || unitA.Team != Team.Player)
				{
					continue;
				}

				if (!unitA.DefinitionId.Equals(combo.UnitIdA, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				for (var j = combo.UnitIdA == combo.UnitIdB ? i + 1 : 0; j < _units.Count; j++)
				{
					var unitB = _units[j];
					if (unitB == unitA || unitB.IsDead || unitB.Team != Team.Player)
					{
						continue;
					}

					if (!unitB.DefinitionId.Equals(combo.UnitIdB, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (unitA.Position.DistanceTo(unitB.Position) > combo.ProximityRadius)
					{
						continue;
					}

					unitA.ApplyTemporaryCombatBuff(combo.DamageScaleA, combo.SpeedScaleA, comboBuffDuration);
					if (combo.HealthScaleA > 1.001f)
					{
						unitA.ApplyTemporaryDefenseModifier(1f / combo.HealthScaleA, comboBuffDuration);
					}

					unitB.ApplyTemporaryCombatBuff(combo.DamageScaleB, combo.SpeedScaleB, comboBuffDuration);
					if (combo.HealthScaleB > 1.001f)
					{
						unitB.ApplyTemporaryDefenseModifier(1f / combo.HealthScaleB, comboBuffDuration);
					}

					if (!string.IsNullOrEmpty(combo.Id) && _triggeredComboPairIds.Add(combo.Id))
					{
						AudioDirector.Instance?.PlayCombo();
					}
				}
			}
		}
	}

	private void ApplyFortifiedDeployBonus(Vector2 spawnPosition)
	{
		if (!StageModifiers.HasFortifiedDeploy(_stageData))
		{
			return;
		}

		var defenseScale = StageModifiers.ResolveFortifiedDeployDefenseScale(_stageData);
		var duration = StageModifiers.ResolveFortifiedDeployDuration(_stageData);
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			if (unit.Position.DistanceTo(spawnPosition) > 12f)
			{
				continue;
			}

			unit.ApplyTemporaryDefenseModifier(defenseScale, duration);
			break;
		}
	}

	private void ApplyCursedGroundAttrition(float delta)
	{
		var dps = StageModifiers.ResolveCursedGroundDps(_stageData);
		if (dps <= 0.01f)
		{
			return;
		}

		var tickDamage = dps * delta;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player || !CursedGroundAreas().Any(area => area.HasPoint(unit.Position)))
			{
				continue;
			}

			unit.TakeDamage(tickDamage);
		}
	}

	private static bool ShouldPrioritizeObjectiveRaid(Unit unit, Unit directTarget)
	{
		return unit.Team == Team.Enemy &&
			unit.VisualClass == "saboteur" &&
			(directTarget == null || !unit.CanAttack(directTarget));
	}

	private Unit FindClosestEnemy(Unit source)
	{
		var isBackstab = string.Equals(source.SpecialAbilityId, "backstab", StringComparison.OrdinalIgnoreCase);
		Unit bestTarget = null;
		var bestPriority = int.MaxValue;
		var bestDistance = isBackstab ? float.MinValue : float.MaxValue;
		var hasLockedTarget = false;

		if (_targetLocks.TryGetValue(source, out var lockedTarget) && IsValidCombatTarget(source, lockedTarget))
		{
			if (source.CanAttack(lockedTarget)) return lockedTarget;
			hasLockedTarget = true;
			bestTarget = lockedTarget;
			bestPriority = ResolveTargetPriority(source, lockedTarget);
			bestDistance = isBackstab
				? lockedTarget.Position.X
				: ResolveTargetSelectionScore(source, lockedTarget);
		}
		else
		{
			_targetLocks.Remove(source);
		}

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team == source.Team || candidate.IsUntargetable)
			{
				continue;
			}

			if (!source.IsInAggroRange(candidate, _weatherAggroScale))
			{
				continue;
			}

			var priority = ResolveTargetPriority(source, candidate);
			if (isBackstab)
			{
				// Backstab: prefer the farthest enemy (rear of formation) among highest-priority targets
				var distance = candidate.Position.X;
				var replaceDistance = hasLockedTarget && priority == bestPriority
					? bestDistance + 26f
					: bestDistance;
				if (priority < bestPriority || (priority == bestPriority && distance > replaceDistance))
				{
					bestPriority = priority;
					bestDistance = distance;
					bestTarget = candidate;
				}
			}
			else
			{
				var distance = ResolveTargetSelectionScore(source, candidate);
				var replaceDistance = hasLockedTarget && priority == bestPriority
					? bestDistance * 0.82f
					: bestDistance;
				if (priority < bestPriority || (priority == bestPriority && distance < replaceDistance))
				{
					bestPriority = priority;
					bestDistance = distance;
					bestTarget = candidate;
				}
			}
		}

		if (bestTarget != null)
		{
			_targetLocks[source] = bestTarget;
		}
		else
		{
			_targetLocks.Remove(source);
		}

		return bestTarget;
	}

	private bool IsValidCombatTarget(Unit source, Unit candidate)
	{
		return IsInstanceValid(source) &&
			IsInstanceValid(candidate) &&
			!source.IsDead &&
			!candidate.IsDead &&
			candidate.Team != source.Team &&
			!candidate.IsUntargetable &&
			source.IsInAggroRange(candidate, _weatherAggroScale);
	}

	private static float ResolveTargetDistanceScore(Unit source, Unit candidate)
	{
		var delta = candidate.Position - source.Position;
		var laneWeight = source.UsesProjectile ? 2.2f : 1.45f;
		return (delta.X * delta.X) + ((delta.Y * delta.Y) * laneWeight);
	}

	private float ResolveTargetSelectionScore(Unit source, Unit candidate)
	{
		var distanceScore = ResolveTargetDistanceScore(source, candidate);
		if (!ShouldUseCoordinatedTargeting(source))
		{
			return distanceScore;
		}

		var focusCount = CountAlliedTargetPressure(source, candidate);
		var focusBias = Mathf.Min(3, focusCount) * TargetFocusScoreBonus;
		var finisherBias = (1f - candidate.HealthRatio) * TargetFinisherScoreBonus;
		if (candidate.HealthRatio <= 0.34f)
		{
			finisherBias += 520f;
		}

		var laneDelta = Mathf.Abs(candidate.Position.Y - source.Position.Y);
		var laneBias = Mathf.Max(0f, 720f - (laneDelta * 8f));
		return Mathf.Max(0f, distanceScore - focusBias - finisherBias - laneBias);
	}

	private int CountAlliedTargetPressure(Unit source, Unit candidate)
	{
		var count = 0;
		var laneTolerance = Mathf.Max(96f, source.AggroRangeY * 1.2f);
		foreach (var pair in _targetLocks)
		{
			var ally = pair.Key;
			if (ally == source ||
				!IsInstanceValid(ally) ||
				ally.IsDead ||
				ally.Team != source.Team ||
				pair.Value != candidate)
			{
				continue;
			}

			if (Mathf.Abs(ally.Position.Y - source.Position.Y) > laneTolerance)
			{
				continue;
			}

			if (ally.Position.DistanceTo(source.Position) > 120f)
			{
				continue;
			}

			count++;
			if (count >= 3)
			{
				return count;
			}
		}

		return count;
	}

	private static bool ShouldUseCoordinatedTargeting(Unit source)
	{
		return source.UsesProjectile || source.ProvidesAura || source.VisualClass is "banner" or "howler";
	}

	private static int ResolveTargetPriority(Unit source, Unit candidate)
	{
		if (source.Team != Team.Player)
		{
			return 5;
		}

		// Frontliners engage the enemy touching their line before chasing a distant support unit.
		if (!source.UsesProjectile && source.SpecialAbilityId != "backstab" && source.CanAttack(candidate))
		{
			return -1;
		}

		return candidate.VisualClass switch
		{
			"jammer" => 0,
			"howler" => 1,
			"necromancer" => 1,
			"spitter" => 2,
			"saboteur" => source.VisualClass is "fighter" or "shield" or "skirmisher" ? 1 : 3,
			"siegetower" => 2,
			"mirror" => 3,
			"boss" => 3,
			_ => 5
		};
	}

	private void ApplySplashDamage(Team attackerTeam, Vector2 center, float damage, float radius, Color color, string attackerName = null)
	{
		if (damage <= 0.05f || radius <= 0.05f)
		{
			return;
		}

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team == attackerTeam || candidate.IsUntargetable)
			{
				continue;
			}

			if (candidate.Position.DistanceTo(center) > radius)
			{
				continue;
			}

			var appliedDamage = candidate.TakeDamage(damage, attackerName);
			candidate.ReactToContact(Mathf.Sign(candidate.Position.X-center.X),appliedDamage,ResolveImpactResistance(candidate));
			if (attackerName != null)
			{
				TrackDamageDealt(attackerName, appliedDamage);
			}
			SpawnDamageFeedback(candidate.Position, appliedDamage, color);
		}
	}

	private void TrackDamageDealt(Unit unit, float appliedDamage)
	{
		if (unit.Team != Team.Player || appliedDamage <= 0.05f || string.IsNullOrWhiteSpace(unit.UnitName))
			return;
		_unitDamageDealt.TryGetValue(unit.UnitName, out var current);
		_unitDamageDealt[unit.UnitName] = current + appliedDamage;
	}

	private void TrackDamageDealt(string unitName, float appliedDamage)
	{
		if (appliedDamage <= 0.05f || string.IsNullOrWhiteSpace(unitName))
			return;
		_unitDamageDealt.TryGetValue(unitName, out var current);
		_unitDamageDealt[unitName] = current + appliedDamage;
	}

	private string BuildBattleStatsBreakdown()
	{
		var topUnits = _unitDamageDealt
			.OrderByDescending(kv => kv.Value)
			.Take(3)
			.ToList();
		if (topUnits.Count == 0)
			return "";
		var lines = new List<string> { "--- Battle Stats ---" };
		foreach (var kv in topUnits)
		{
			lines.Add($"{kv.Key}: {Mathf.RoundToInt(kv.Value)} damage");
		}
		lines.Add($"Spells cast: {_spellsCast} · Abilities triggered: {_activeAbilitiesTriggered}");
		return string.Join("\n", lines);
	}

	private Unit FindClosestEnemyToPoint(Vector2 point, float maxDistance)
	{
		Unit bestTarget = null;
		var bestDistance = maxDistance * maxDistance;

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team != Team.Enemy)
			{
				continue;
			}

			var distance = candidate.Position.DistanceSquaredTo(point);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestTarget = candidate;
			}
		}

		return bestTarget;
	}

	private void ExpireBarricades()
	{
		for (var i = _barricades.Count - 1; i >= 0; i--)
		{
			var (unit, expiresAt) = _barricades[i];
			if (!IsInstanceValid(unit) || unit.IsDead)
			{
				_barricades.RemoveAt(i);
				continue;
			}

			if (_elapsed >= expiresAt)
			{
				SpawnEffect(unit.Position, unit.Tint, 8f, 22f, 0.2f, false);
				unit.TakeDamage(unit.MaxHealth * 10f);
				_barricades.RemoveAt(i);
			}
		}
	}

	private void CleanupDeadUnits()
	{
		var bestThreatPriority = int.MaxValue;
		var threatLabel = "";
		var threatStatus = "";
		var threatColor = Colors.White;
		var threatPosition = Vector2.Zero;
		var threatCourageGain = 0f;
		var threatBuffRadius = 0f;
		var threatBuffDuration = 0f;
		var threatAttackScale = 1f;
		var threatSpeedScale = 1f;

		for (var i = _units.Count - 1; i >= 0; i--)
		{
			if (!_units[i].IsDead)
			{
				continue;
			}

			var deadUnit = _units[i];
			if (deadUnit.Team == Team.Enemy)
			{
				_enemyDefeats++;
				GainMana(_combat.ManaPerEnemyKill);
				RegisterCampaignDoctrineDefeat(deadUnit);
				GameState.Instance.RecordCodexKill(deadUnit.DefinitionId);
				GameState.Instance.AddBountyProgress("enemy_defeats", 1);
				if (deadUnit.VisualClass == "boss") GameState.Instance.AddBountyProgress("boss_kills", 1);

				if (!string.IsNullOrEmpty(deadUnit.LastDamagedBy))
				{
					foreach (var killer in _units)
					{
						if (!killer.IsDead && killer.Team == Team.Player && killer.UnitName == deadUnit.LastDamagedBy)
						{
							GameState.Instance.AddUnitMasteryXP(killer.DefinitionId, MasteryCatalog.XPPerKill);
							break;
						}
					}
				}

				if (deadUnit.VisualClass == "boss")
				{
					TryRollRelicDropFromBoss();
					AudioDirector.Instance?.PlayBossDeath();
				}

				if (!string.IsNullOrEmpty(deadUnit.LastDamagedBy) && _rng.Randf() < 0.25f)
				{
					foreach (var killer in _units)
					{
						if (!killer.IsDead &&
							killer.Team == Team.Player &&
							killer.UnitName == deadUnit.LastDamagedBy &&
							!string.IsNullOrEmpty(killer.KillQuote))
						{
							break;
						}
					}
				}

				if (TryBuildThreatNeutralizedFeedback(
					deadUnit,
					out var label,
					out var status,
					out var color,
					out var priority,
					out var courageGain,
					out var buffRadius,
					out var buffDuration,
					out var attackScale,
					out var speedScale) &&
					priority < bestThreatPriority)
				{
					bestThreatPriority = priority;
					threatLabel = label;
					threatStatus = status;
					threatColor = color;
					threatPosition = deadUnit.Position;
					threatCourageGain = courageGain;
					threatBuffRadius = buffRadius;
					threatBuffDuration = buffDuration;
					threatAttackScale = attackScale;
					threatSpeedScale = speedScale;
				}
			}
			else if (deadUnit.Team == Team.Player && !string.IsNullOrWhiteSpace(deadUnit.UnitName))
			{
				_lastDeadPlayerUnitId = deadUnit.VisualClass == "walker" ? "" : (deadUnit.UnitName ?? "");
				_lastDeadPlayerPosition = deadUnit.Position;
			}

			TriggerDeathBurst(deadUnit);
			TriggerSpawnOnDeath(deadUnit);
				TriggerDamageReflectOnDeath();
			TryLichGraveyardReanimate(deadUnit);
			PresentUnitDeath(deadUnit);
			_pendingBossPhases.Remove(deadUnit);
			_campaignBossPhaseTriggeredUnits.Remove(deadUnit);
			foreach (var attacker in _targetLocks.Where(pair => pair.Value == deadUnit).Select(pair => pair.Key).ToArray())
				_targetLocks.Remove(attacker);
			_targetLocks.Remove(deadUnit);
			_units.RemoveAt(i);
			UnitPool.Release(deadUnit);
		}

		if (bestThreatPriority < int.MaxValue)
		{
			ApplyThreatNeutralizedFeedback(
				threatPosition,
				threatStatus,
				threatColor,
				threatCourageGain,
				threatBuffRadius,
				threatBuffDuration,
				threatAttackScale,
				threatSpeedScale);
		}

		PruneTargetLocks();
	}

	private void GainMana(float amount)
	{
		if (amount > 0f) _mana = Mathf.Min(_maxMana, _mana + amount);
	}

	private bool TryBuildThreatNeutralizedFeedback(
		Unit deadUnit,
		out string label,
		out string status,
		out Color color,
		out int priority,
		out float courageGain,
		out float buffRadius,
		out float buffDuration,
		out float attackScale,
		out float speedScale)
	{
		label = "";
		status = "";
		color = deadUnit.Tint.Lightened(0.18f);
		priority = int.MaxValue;
		courageGain = 0f;
		buffRadius = 86f;
		buffDuration = 2.4f;
		attackScale = 1.06f;
		speedScale = 1.08f;

		switch (deadUnit.VisualClass)
		{
			case "jammer":
				label = "SIGNAL CUT";
				status = "Enemy jammer neutralized. Courage flow stabilized.";
				priority = 0;
				courageGain = 4f;
				buffDuration = 2.8f;
				attackScale = 1.08f;
				speedScale = 1.1f;
				break;
			case "necromancer":
				label = "NECRO DOWN";
				status = "Reanimation threat removed from the lane.";
				priority = 1;
				courageGain = 3f;
				buffDuration = 2.7f;
				attackScale = 1.08f;
				speedScale = 1.08f;
				break;
			case "siegetower":
				label = "SIEGE BROKEN";
				status = "Siege pressure collapsed and the line surged forward.";
				priority = 2;
				courageGain = 4f;
				buffRadius = 94f;
				buffDuration = 3f;
				attackScale = 1.1f;
				speedScale = 1.1f;
				break;
			case "howler":
				label = "PACK BROKEN";
				status = "Enemy howl pressure fell off and the front steadied.";
				priority = 3;
				courageGain = 2f;
				buffDuration = 2.5f;
				attackScale = 1.06f;
				speedScale = 1.09f;
				break;
			default:
				return false;
		}

		return true;
	}

	private void ApplyThreatNeutralizedFeedback(
		Vector2 position,
		string status,
		Color color,
		float courageGain,
		float buffRadius,
		float buffDuration,
		float attackScale,
		float speedScale)
	{
		var ralliedAllies = 0;
		foreach (var ally in _units)
		{
			if (ally.IsDead || ally.Team != Team.Player)
			{
				continue;
			}

			if (ally.Position.DistanceTo(position) > buffRadius)
			{
				continue;
			}

			ally.ApplyTemporaryCombatBuff(attackScale, speedScale, buffDuration);
			ralliedAllies++;
		}

		if (courageGain > 0.05f)
		{
			_courage = Mathf.Min(_maxCourage, _courage + courageGain);
		}

		SpawnEffect(position, color, 12f, 34f, 0.24f, false);



		var rallySuffix = ralliedAllies > 0
			? $" {ralliedAllies} nearby allies surged."
			: "";
		var courageSuffix = courageGain > 0.05f
			? $" +{Mathf.RoundToInt(courageGain)} courage."
			: "";
		SetStatus($"{status}{rallySuffix}{courageSuffix}");
	}

	private void RegisterCampaignDoctrineDefeat(Unit deadUnit)
	{
		if (!IsCampaignMode || deadUnit == null || deadUnit.Team != Team.Enemy || _campaignDoctrineThreshold <= 0)
		{
			return;
		}

		_campaignDoctrineDefeatProgress += deadUnit.VisualClass == "boss" ? 2 : 1;
		while (_campaignDoctrineDefeatProgress >= _campaignDoctrineThreshold)
		{
			_campaignDoctrineDefeatProgress -= _campaignDoctrineThreshold;
			ApplyCampaignRouteDoctrine(deadUnit);
		}
	}

	private void ApplyCampaignRouteDoctrine(Unit anchorUnit)
	{
		var color = RouteCatalog.Get(_activeRouteId).BannerAccent.Lightened(0.04f);
		switch (_activeRouteId)
		{
			case RouteCatalog.CityId:
				_courage = Mathf.Min(_maxCourage, _courage + 4f);
				_deck.ReduceCooldowns(0.45f);
				_spellDeck.ReduceCooldowns(0.45f);
				SetStatus("Lantern Levy cycled: +4 courage and quicker card recovery.");
				break;
			case RouteCatalog.HarborId:
			{
				var anchor = FindClosestEnemyToPoint(PlayerBaseCorePosition + new Vector2(120f, 0f), 130f)?.Position ?? anchorUnit?.Position ?? EnemyBaseCorePosition;
				DamageEnemiesNear(anchor, 38f, 14f, color);
				SlowEnemiesNear(anchor, 38f, 0.74f, 2.8f, color);
				SetStatus("Ripchain Echo snapped across the nearest push.");
				break;
			}
			case RouteCatalog.FoundryId:
			{
				var anchor = FindClosestEnemyToPoint(new Vector2(Mathf.Lerp(PlayerBaseX, EnemyBaseX, 0.64f), BaseCenterY), 210f)?.Position ?? anchorUnit?.Position ?? EnemyBaseCorePosition;
				DamageEnemiesNear(anchor, 42f, 18f, color);
				SetStatus("Smelter Volley scattered the densest enemy pack.");
				break;
			}
			case RouteCatalog.QuarantineId:
				_enemySignalJamTimer = Mathf.Max(0f, _enemySignalJamTimer - 1.8f);
				if (_enemySignalJamTimer <= 0.05f)
				{
					_enemySignalJamTimer = 0f;
					_enemySignalJamCourageGainScale = 1f;
				}
				RepairBusByRatio(0.02f);
				SetStatus("Cleanse Pulse cut through curse pressure and patched the wagon.");
				break;
			case RouteCatalog.ThornwallId:
				PushEnemiesFromPoint(PlayerBaseCorePosition, 90f, 7f, 0.72f, 2.8f, color);
				SetStatus("Stonewake rolled downhill and knocked the front back.");
				break;
			case RouteCatalog.BasilicaId:
				RepairBusByRatio(0.02f);
				BuffAllPlayerUnits(1.04f, 1.04f, 4.2f);
				SetStatus("Sanctuary Chorus steadied the wagon and blessed the line.");
				break;
			case RouteCatalog.MireId:
			{
				var anchor = FindClosestEnemyToPoint(PlayerBaseCorePosition + new Vector2(90f, 0f), 110f)?.Position ?? anchorUnit?.Position ?? EnemyBaseCorePosition;
				DamageEnemiesNear(anchor, 43f, 12f, color);
				SlowEnemiesNear(anchor, 48f, 0.7f, 3.2f, color);
				SetStatus("Bog Snare dragged the nearest push into the mire.");
				break;
			}
			case RouteCatalog.SteppeId:
				_courage = Mathf.Min(_maxCourage, _courage + 4f);
				BuffAllPlayerUnits(1.02f, 1.08f, 4.5f);
				SetStatus("Rider Tempo kicked the caravan forward with a speed surge.");
				break;
			case RouteCatalog.GloamwoodId:
			{
				var target = FindHighestHealthEnemy();
				if (target != null)
				{
					var appliedDamage = target.TakeDamage(20f, "Witchmark");
					SpawnDamageFeedback(target.Position, appliedDamage, color);
					target.ApplyTemporarySpeedModifier(0.7f, 3.2f);
				}
				SetStatus("Witchmark fell on the toughest enemy in the lane.");
				break;
			}
			case RouteCatalog.CitadelId:
				DamageEnemyBaseByRatio(0.02f, color);
				DamageEnemiesNear(EnemyBaseCorePosition + new Vector2(-84f, 0f), 42f, 16f, color);
				SetStatus("Bastion Range shelled the keep and its frontline.");
				break;
			default:
				_courage = Mathf.Min(_maxCourage, _courage + 3f);
				SetStatus("District doctrine cycled and steadied the caravan.");
				break;
		}
	}

	private void PruneTargetLocks()
	{
		if (_targetLocks.Count == 0)
		{
			return;
		}

		var staleSources = new List<Unit>();
		foreach (var pair in _targetLocks)
		{
			if (!IsInstanceValid(pair.Key) ||
				pair.Key.IsDead ||
				!IsInstanceValid(pair.Value) ||
				pair.Value.IsDead)
			{
				staleSources.Add(pair.Key);
			}
		}

		foreach (var source in staleSources)
		{
			_targetLocks.Remove(source);
		}
	}

	private void TryRollRelicDropFromBoss()
	{
		var relicVaultLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.RelicVaultId);
		var relicBonus = relicVaultLevel * 0.12f;
		var roll = _rng.Randf();
		string targetRarity;
		if (roll < 0.10f + (relicBonus * 0.4f))
			targetRarity = "epic";
		else if (roll < 0.40f + (relicBonus * 0.6f))
			targetRarity = "rare";
		else
			targetRarity = "common";

		var candidates = GameData.GetAllEquipment()
			.Where(e => string.Equals(e.Rarity, targetRarity, StringComparison.OrdinalIgnoreCase) &&
				(!IsCampaignMode || CampaignProgressionCatalog.IsCampaignRelic(e.Id)))
			.ToList();
		if (candidates.Count == 0)
			return;

		var relic = candidates[_rng.RandiRange(0, candidates.Count - 1)];
		var isNew = GameState.Instance.GrantBossRelic(relic.Id, out var duplicateShards);
		if (isNew)
		{
			AudioDirector.Instance?.PlayRelicPickup();
		}

		// Sigil drop from boss kills
		GameState.Instance.GrantSigils(1);
	}

	private void TriggerDeathBurst(Unit deadUnit)
	{
		if (deadUnit.DeathBurstDamage <= 0f || deadUnit.DeathBurstRadius <= 0f)
		{
			return;
		}

		AudioDirector.Instance?.PlayEnemySpecial("burst", deadUnit.Position);
		SpawnEffect(
			deadUnit.Position,
			deadUnit.Tint.Lightened(0.15f),
			12f,
			deadUnit.DeathBurstRadius,
			0.26f,
			false);
		BattleParticles.SpawnDeathBurstExplosion(this, deadUnit.Position, deadUnit.Tint.Lightened(0.15f), deadUnit.DeathBurstRadius);

		foreach (var candidate in _units)
		{
			if (candidate == deadUnit || candidate.IsDead || candidate.Team == deadUnit.Team)
			{
				continue;
			}

			if (candidate.Position.DistanceTo(deadUnit.Position) > deadUnit.DeathBurstRadius)
			{
				continue;
			}

			var appliedDamage = candidate.TakeDamage(deadUnit.DeathBurstDamage);
			SpawnDamageFeedback(candidate.Position, appliedDamage, deadUnit.Tint.Lightened(0.15f));
		}
	}

	private void TriggerSpawnOnDeath(Unit deadUnit)
	{
		if (deadUnit.Team != Team.Enemy ||
			string.IsNullOrWhiteSpace(deadUnit.SpawnOnDeathUnitId) ||
			deadUnit.SpawnOnDeathCount <= 0)
		{
			return;
		}

		if (!_spawnDirector.TryBuildEnemyStats(deadUnit.SpawnOnDeathUnitId, out var spawnedStats))
		{
			GD.PushWarning($"Could not resolve spawned enemy '{deadUnit.SpawnOnDeathUnitId}'.");
			return;
		}

		var count = deadUnit.SpawnOnDeathCount;
		AudioDirector.Instance?.PlayEnemySpecial("split", deadUnit.Position);
		const float spacing = 18f;
		var startOffset = -((count - 1) * spacing) * 0.5f;

		for (var i = 0; i < count; i++)
		{
			var spawnPosition = new Vector2(
				Mathf.Clamp(deadUnit.Position.X + _rng.RandfRange(-10f, 10f), BattlefieldLeft, BattlefieldRight),
				Mathf.Clamp(
					deadUnit.Position.Y + startOffset + (spacing * i),
					BattlefieldTop + SpawnVerticalPadding,
					BattlefieldBottom - SpawnVerticalPadding));
			SpawnEnemyUnit(spawnedStats, spawnPosition);
		}
	}

	private Unit FindMeleeThreat(Unit unit)
	{
		Unit closest = null;
		var best = float.MaxValue;
		foreach (var other in _units)
		{
			// CanAttack too: a threat the unit could not swing at would hold it in place.
			if (other.IsDead || other.Team == unit.Team || other.IsUntargetable || !unit.InMeleeReach(other) || !unit.CanAttack(other)) continue;
			var d = unit.Position.DistanceSquaredTo(other.Position);
			if (d < best) { best = d; closest = other; }
		}
		return closest;
	}

	private Unit FindProjectileShieldInterceptor(Unit attacker, Unit target)
	{
		if (attacker.Team != Team.Player)
		{
			return null;
		}

		Unit bestShield = null;
		var bestDistance = float.MaxValue;

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team != Team.Enemy)
			{
				continue;
			}

			if (!string.Equals(candidate.SpecialAbilityId, "projectile_shield", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (candidate.SpecialBuffRadius <= 0.05f)
			{
				continue;
			}

			// Shield wall blocks if the target is within its protection radius
			if (target.Position.DistanceTo(candidate.Position) > candidate.SpecialBuffRadius)
			{
				continue;
			}

			// And the shield wall is between the attacker and the target (X-wise)
			if (candidate.Position.X < attacker.Position.X || candidate.Position.X > target.Position.X + 40f)
			{
				continue;
			}

			var distance = attacker.Position.DistanceSquaredTo(candidate.Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestShield = candidate;
			}
		}

		return bestShield;
	}

	private void ApplyDamageReflect(Unit reflector, Unit attacker, float appliedDamage)
	{
		if (reflector.IsDead || attacker.IsDead || reflector.DamageReflectScale <= 0.01f)
		{
			return;
		}

		var reflectedDamage = appliedDamage * reflector.DamageReflectScale;
		if (reflectedDamage <= 0.5f)
		{
			return;
		}

		var actualReflected = attacker.TakeDamage(reflectedDamage);
		if (actualReflected > 0.05f)
		{
			AudioDirector.Instance?.PlayEnemySpecial("reflect", reflector.Position);
			SpawnDamageFeedback(attacker.Position, actualReflected, reflector.Tint.Lightened(0.3f));
		}
	}

	private void ApplyMirrorPressureReflect(Unit attacker, Unit target, float appliedDamage)
	{
		if (attacker.IsDead || attacker.Team != Team.Player || !StageModifiers.HasMirrorPressure(_stageData))
		{
			return;
		}

		var reflectScale = StageModifiers.ResolveMirrorPressureScale(_stageData);
		var reflectedDamage = appliedDamage * reflectScale;
		if (reflectedDamage <= 0.5f)
		{
			return;
		}

		var actualReflected = attacker.TakeDamage(reflectedDamage);
		if (actualReflected > 0.05f)
		{
			SpawnDamageFeedback(attacker.Position, actualReflected, target.Tint.Lightened(0.3f));
		}
	}

	private bool TrySiegeTowerDeploy(Unit siegeTower)
	{
		if (!string.Equals(siegeTower.SpecialAbilityId, "siege_deploy", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(siegeTower.SpecialSpawnUnitId) || siegeTower.SpecialSpawnCount <= 0)
		{
			return false;
		}

		var spawned = 0;
		for (var i = 0; i < siegeTower.SpecialSpawnCount; i++)
		{
			if (!_spawnDirector.TryBuildEnemyStats(siegeTower.SpecialSpawnUnitId, out var spawnedStats))
			{
				break;
			}

			var spawnPos = new Vector2(
				Mathf.Clamp(siegeTower.Position.X + _rng.RandfRange(-20f, 20f), BattlefieldLeft + 20f, BattlefieldRight - 20f),
				Mathf.Clamp(
					siegeTower.Position.Y + _rng.RandfRange(-40f, 40f),
					BattlefieldTop + SpawnVerticalPadding,
					BattlefieldBottom - SpawnVerticalPadding));
			SpawnEnemyUnit(spawnedStats, spawnPos);
			spawned++;
		}

		if (spawned > 0)
		{
			SpawnEffect(siegeTower.Position, siegeTower.Tint.Lightened(0.12f), 14f, 56f, 0.3f, false);
			SetStatus($"Siege Tower deployed {spawned} enemies behind the caravan lines.");
		}

		// Kill the siege tower after deploying
		AudioDirector.Instance?.PlayEnemySpecial("tower", siegeTower.Position);
		siegeTower.TakeDamage(siegeTower.MaxHealth * 10f);
		return spawned > 0;
	}

	private void TriggerDamageReflectOnDeath()
	{
		// Placeholder for any reflect-on-death cleanup. Mirror reflect is handled on damage.
	}

	private void TryLichGraveyardReanimate(Unit deadUnit)
	{
		if (deadUnit.Team != Team.Enemy || deadUnit.VisualClass == "boss")
		{
			return;
		}

		if (!StageModifiers.HasLichGraveyard(_stageData))
		{
			return;
		}

		// A casualty may be replaced by the boss before death cleanup runs. Graveyard
		// resurrection must respect the same artillery limit as its summon commands.
		foreach (var commander in _units)
			if (!commander.IsDead && commander.Team == Team.Enemy &&
				!CanAddBossReinforcement(commander, deadUnit.DefinitionId))
				return;

		var chance = StageModifiers.ResolveLichGraveyardChance(_stageData);
		if (_rng.Randf() >= chance)
		{
			return;
		}

		if (!_spawnDirector.TryBuildEnemyStats(deadUnit.DefinitionId, out var reanimatedStats))
		{
			return;
		}

		var spawnPosition = new Vector2(
			Mathf.Clamp(deadUnit.Position.X + _rng.RandfRange(-12f, 12f), BattlefieldLeft, BattlefieldRight),
			Mathf.Clamp(deadUnit.Position.Y + _rng.RandfRange(-12f, 12f),
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding));
		SpawnEnemyUnit(reanimatedStats, spawnPosition);
		SpawnEffect(spawnPosition, deadUnit.Tint.Lightened(0.15f), 8f, 32f, 0.24f, false);
		SetStatus("A fallen enemy reanimated from the lich graveyard.");
	}

	private void SpawnEffect(
		Vector2 position,
		Color color,
		float startRadius,
		float endRadius,
		float lifetime,
		bool filled = true,
		BattleEffectStyle style = BattleEffectStyle.Pulse)
	{
		if (IsReducedMotionEnabled())
		{
			endRadius = Mathf.Lerp(startRadius, endRadius, 0.35f);
			lifetime = Mathf.Min(lifetime, 0.12f);
		}

		var effect = new BattleEffect();
		effect.Position = position;
		effect.Setup(color, startRadius, endRadius, lifetime, filled, style);
		effect.GroundProjected = true;
		AddChild(effect);
	}

	private void SpawnDamageFeedback(Vector2 position, float damage, Color color)
	{
		if (damage <= 0.05f)
		{
			return;
		}

		SpawnCombatNumber(
			position + new Vector2(_rng.RandfRange(-6f, 6f), -8f),
			$"-{Mathf.RoundToInt(damage)}",
			color.Lightened(0.3f),
			0.46f);
	}

	// Now and then a living enemy groans, shrieks or rattles as the host advances.
	private void TickHordeVoices()
	{
		if (_battleEnded || _battlePaused || AudioDirector.Instance == null) return;
		var enemies = _units.Where(u => u.Team == Team.Enemy && !u.IsDead).ToList();
		if (enemies.Count > 0) AudioDirector.Instance.TickHordeVoices(enemies[_rng.RandiRange(0, enemies.Count - 1)]);
	}

	// Battle text is numbers only (damage, healing, repairs); names and callouts stay off the field.
	private void SpawnCombatNumber(Vector2 position, string text, Color color, float lifetime = 0.5f)
	{
		var floatText = new BattleFloatText();
		if (_mobileCamera != null) floatText.PresentationScale = () => MobilePresentation.HudScale / _mobileCamera.Zoom.X;
		// Keep combat text near its old on-screen size while the desktop camera is zoomed in.
		else if (_battleCamera != null) floatText.PresentationScale = () => 1.2f / _battleCamera.Zoom.X;
		floatText.Position = position;
		floatText.Setup(
			text,
			color,
			lifetime,
			new Vector2(_rng.RandfRange(-10f, 10f), _rng.RandfRange(-54f, -42f)));
		AddChild(floatText);
	}

	private Vector2 ClampBattlefieldPoint(Vector2 position)
	{
		return new Vector2(
			Mathf.Clamp(position.X, BattlefieldLeft + 18f, BattlefieldRight - 18f),
			Mathf.Clamp(position.Y, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
	}

	private Unit[] GetLivingUnitsInRadius(Vector2 center, float radius, Team team)
	{
		var radiusSquared = radius * radius;
		return _units
			.Where(unit =>
				!unit.IsDead &&
				unit.Team == team &&
				unit.Position.DistanceSquaredTo(center) <= radiusSquared)
			.ToArray();
	}

	private Unit FindToughestEnemyInRadius(Vector2 center, float radius)
	{
		Unit bestTarget = null;
		var bestHealth = 0f;
		foreach (var target in GetLivingUnitsInRadius(center, radius, Team.Enemy))
		{
			if (target.Health > bestHealth)
			{
				bestHealth = target.Health;
				bestTarget = target;
			}
		}

		return bestTarget;
	}

	private void ApplyDeployMomentum(Unit unit)
	{
		if (!IsInstanceValid(unit) || unit.IsDead || unit.Team != Team.Player)
		{
			return;
		}

		var speedScale = 1f;
		unit.ApplyTemporaryCombatBuff(1f, speedScale, DeployMomentumDurationSeconds);
		unit.ApplyTemporaryDefenseModifier(DeployMomentumDefenseScale, DeployMomentumDurationSeconds);
	}

	private string ApplySpellEffect(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		return spell.EffectType switch
		{
			"fireball" => ApplyFireballSpell(spell, targetPosition),
			"heal" => ApplyHealSpell(spell, targetPosition),
			"frost_burst" => ApplyFrostBurstSpell(spell, targetPosition),
			"lightning_strike" => ApplyLightningStrikeSpell(spell, targetPosition),
			"barrier_ward" => ApplyBarrierWardSpell(spell, targetPosition),
			"stone_barricade" => ApplyStoneBarricadeSpell(spell, targetPosition),
			"war_cry" => ApplyWarCrySpell(spell),
			"earthquake" => ApplyEarthquakeSpell(spell, targetPosition),
			"polymorph" => ApplyPolymorphSpell(spell, targetPosition),
			"resurrect" => ApplyResurrectSpell(spell, targetPosition),
			_ => "The spell fizzled without a scripted effect."
		};
	}

	private string ApplyFireballSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Enemy);
		var hits = 0;
		var totalDamage = 0f;

		FireballStreak.Spawn(this, targetPosition, color, () => _battlePaused || _endlessCheckpointActive);
		SpawnEffect(targetPosition, color, 14f, spell.Radius, 0.26f, false, BattleEffectStyle.Fireburst);
		BattleParticles.SpawnFireballParticles(this, targetPosition, spell.Radius);

		foreach (var target in targets)
		{
			var appliedDamage = target.TakeDamage(spell.Power);
			if (appliedDamage <= 0.05f)
			{
				continue;
			}

			hits++;
			totalDamage += appliedDamage;
			SpawnDamageFeedback(target.Position, appliedDamage, color);
		}

		return hits > 0
			? $"Fireball hit {hits} enemies for {Mathf.RoundToInt(totalDamage)} total damage."
			: "Fireball burst across empty ground.";
	}

	private string ApplyHealSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var allies = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Player);
		var healedUnits = 0;
		var totalHealing = 0f;

		SpawnEffect(targetPosition, color, 12f, spell.Radius, 0.28f, false, BattleEffectStyle.HealBloom);
		BattleParticles.SpawnHealSparkles(this, targetPosition, color, spell.Radius);

		foreach (var ally in allies)
		{
			var healed = ally.Heal(spell.Power);
			if (healed <= 0.05f)
			{
				continue;
			}

			healedUnits++;
			totalHealing += healed;
			SpawnEffect(ally.Position, color.Lightened(0.08f), 8f, 22f, 0.18f, false, BattleEffectStyle.HealBloom);
			SpawnCombatNumber(ally.Position + new Vector2(0f, -24f), $"+{Mathf.RoundToInt(healed)}", color.Lightened(0.24f), 0.46f);
		}

		var repaired = RepairBusByAmount(spell.SecondaryPower);
		return
			$"Heal restored {Mathf.RoundToInt(totalHealing)} across {healedUnits} allies" +
			(repaired > 0.05f ? $" and repaired {Mathf.RoundToInt(repaired)} war wagon hull." : ".");
	}

	private string ApplyFrostBurstSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Enemy);
		var slowed = 0;
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 14f, spell.Radius, 0.3f, false, BattleEffectStyle.FrostBurst);
		BattleParticles.SpawnFrostParticles(this, targetPosition, color, spell.Radius);

		foreach (var target in targets)
		{
			var appliedDamage = target.TakeDamage(spell.Power);
			target.ApplyTemporarySpeedModifier(0.62f, spell.Duration);
			if (appliedDamage > 0.05f)
			{
				totalDamage += appliedDamage;
				SpawnDamageFeedback(target.Position, appliedDamage, color);
			}

			slowed++;
		}

		return slowed > 0
			? $"Frost Burst slowed {slowed} enemies for {spell.Duration:0.0}s and dealt {Mathf.RoundToInt(totalDamage)} damage."
			: "Frost Burst failed to catch an enemy pack.";
	}

	private string ApplyLightningStrikeSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Enemy)
			.OrderBy(unit => unit.Position.DistanceSquaredTo(targetPosition))
			.Take(3)
			.ToArray();
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 10f, 24f, 0.2f, false, BattleEffectStyle.LightningStrike);
		BattleParticles.SpawnLightningParticles(this, targetPosition, color);

		for (var i = 0; i < targets.Length; i++)
		{
			var scale = i switch
			{
				0 => 1f,
				1 => 0.78f,
				_ => 0.58f
			};
			var appliedDamage = targets[i].TakeDamage(spell.Power * scale);
			if (appliedDamage <= 0.05f)
			{
				continue;
			}

			totalDamage += appliedDamage;
			SpawnEffect(targets[i].Position, color, 10f, 30f, 0.2f, false, BattleEffectStyle.LightningStrike);
			SpawnDamageFeedback(targets[i].Position, appliedDamage, color);
		}

		return targets.Length > 0
			? $"Lightning Strike chained through {targets.Length} enemies for {Mathf.RoundToInt(totalDamage)} damage."
			: "Lightning Strike had no valid target.";
	}

	private string ApplyBarrierWardSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var allies = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Player);
		var warded = 0;

		SpawnEffect(targetPosition, color, 12f, spell.Radius, 0.28f, false, BattleEffectStyle.WardSigil);
		BattleParticles.SpawnWardParticles(this, targetPosition, color, spell.Radius);

		foreach (var ally in allies)
		{
			ally.ApplyTemporaryDefenseModifier(spell.Power, spell.Duration);
			warded++;
			SpawnEffect(ally.Position, color.Lightened(0.05f), 6f, 18f, 0.18f, false, BattleEffectStyle.WardSigil);
		}

		return warded > 0
			? $"Barrier Ward covered {warded} allies for {spell.Duration:0.0}s."
			: "Barrier Ward found no allied units in the target lane.";
	}

	private string ApplyStoneBarricadeSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var clampedPosition = ClampBattlefieldPoint(targetPosition);

		// Spawn a temporary barricade unit that blocks enemy movement
		var barricadeDef = new UnitDefinition
		{
			Id = "barricade_wall",
			DisplayName = "Stone Barricade",
			Side = "Player",
			MaxHealth = spell.Power,
			Speed = 0f,
			AttackDamage = 0f,
			AttackRange = 0f,
			AttackCooldown = 999f,
			AggroRangeX = 0f,
			AggroRangeY = 0f,
			VisualClass = "shield",
			VisualScale = 1.2f,
			ColorHex = "a68a64",
			DamageTakenScale = 0.7f
		};
		var barricadeStats = new UnitStats(barricadeDef);
		SpawnUnit(Team.Player, barricadeStats, clampedPosition);
		var barricadeUnit = _units[_units.Count - 1];
		_barricades.Add((barricadeUnit, _elapsed + spell.Duration));

		SpawnEffect(clampedPosition, color, 10f, spell.Radius, 0.24f, false);
		BattleParticles.SpawnStoneBarricadeParticles(this, clampedPosition, spell.Radius);

		return $"Stone Barricade raised at the target lane with {Mathf.RoundToInt(spell.Power)} durability.";
	}

	private string ApplyWarCrySpell(ResolvedSpellStats spell)
	{
		var color = spell.GetTint();
		var buffed = 0;

		SpawnEffect(PlayerBaseCorePosition, color, 18f, 60f, 0.32f, false);
		BattleParticles.SpawnWarCryParticles(this, PlayerBaseCorePosition, 60f);

		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Player)
			{
				continue;
			}

			unit.ApplyTemporaryCombatBuff(spell.Power, spell.SecondaryPower, spell.Duration);
			buffed++;
			SpawnEffect(unit.Position, color.Lightened(0.08f), 6f, 18f, 0.16f, false);
		}

		return buffed > 0
			? $"War Cry rallied {buffed} allies with +{Mathf.RoundToInt((spell.Power - 1f) * 100f)}% attack and +{Mathf.RoundToInt((spell.SecondaryPower - 1f) * 100f)}% speed for {spell.Duration:0.0}s."
			: "War Cry echoed across an empty battlefield.";
	}

	private string ApplyEarthquakeSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Enemy);
		var hits = 0;
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 20f, spell.Radius, 0.34f, false);
		BattleParticles.SpawnEarthquakeParticles(this, targetPosition, spell.Radius);

		foreach (var target in targets)
		{
			var appliedDamage = target.TakeDamage(spell.Power);
			target.ApplyTemporarySpeedModifier(0.5f, spell.Duration);
			if (appliedDamage > 0.05f)
			{
				totalDamage += appliedDamage;
				SpawnDamageFeedback(target.Position, appliedDamage, color);
			}
			hits++;
		}

		return hits > 0
			? $"Earthquake shook {hits} enemies for {Mathf.RoundToInt(totalDamage)} damage and slowed them for {spell.Duration:0.0}s."
			: "Earthquake rumbled through empty ground.";
	}

	private string ApplyPolymorphSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, spell.Radius, Team.Enemy);

		// Find the toughest enemy in range (highest current health)
		Unit bestTarget = null;
		var bestHealth = 0f;
		foreach (var target in targets)
		{
			if (target.Health > bestHealth)
			{
				bestHealth = target.Health;
				bestTarget = target;
			}
		}

		if (bestTarget == null)
		{
			SpawnEffect(targetPosition, color, 8f, spell.Radius, 0.22f, false);
			return "Polymorph found no enemy to transform.";
		}

		// Massively debuff the target: near-zero speed, takes 2.5x damage
		// Note: ApplyTemporaryCombatBuff clamps values UP to 1.0 so cannot be used for debuffs
		bestTarget.ApplyTemporarySpeedModifier(0.08f, spell.Duration);
		bestTarget.ApplyTemporaryDefenseModifier(2.5f, spell.Duration);

		SpawnEffect(bestTarget.Position, color, 10f, 32f, 0.26f, false, BattleEffectStyle.WardSigil);
		BattleParticles.SpawnPolymorphParticles(this, bestTarget.Position);

		return $"Polymorph transformed {bestTarget.UnitName} into a harmless creature for {spell.Duration:0.0}s.";
	}

	private string ApplyResurrectSpell(ResolvedSpellStats spell, Vector2 targetPosition)
	{
		var color = spell.GetTint();

		if (string.IsNullOrWhiteSpace(_lastDeadPlayerUnitId))
		{
			SpawnEffect(targetPosition, color, 8f, 28f, 0.22f, false);
			return "Resurrect found no fallen ally to restore.";
		}

		// Find the unit definition that matches the last dead unit by display name
		UnitDefinition resurrectDef = null;
		foreach (var unitId in GameData.PlayerRosterIds)
		{
			var def = GameData.GetUnit(unitId);
			if (def != null && string.Equals(def.DisplayName, _lastDeadPlayerUnitId, StringComparison.OrdinalIgnoreCase))
			{
				resurrectDef = def;
				break;
			}
		}

		if (resurrectDef == null || !_deck.Roster.Any(unit => unit.Id == resurrectDef.Id) || !GameState.Instance.IsUnitOwned(resurrectDef.Id))
		{
			SpawnEffect(targetPosition, color, 8f, 28f, 0.22f, false);
			return "Resurrect could not restore the fallen unit.";
		}

		var stats = GameState.Instance.BuildPlayerUnitStatsForDeck(resurrectDef, Array.Empty<UnitDefinition>());
		var spawnPos = ClampBattlefieldPoint(_lastDeadPlayerPosition);
		SpawnUnit(Team.Player, stats, spawnPos);

		// Apply half-health penalty
		var spawnedUnit = _units[_units.Count - 1];
		var halfDamage = spawnedUnit.MaxHealth * (1f - spell.Power);
		spawnedUnit.TakeDamage(halfDamage);

		SpawnEffect(spawnPos, color, 12f, 38f, 0.28f, false, BattleEffectStyle.HealBloom);
		BattleParticles.SpawnResurrectParticles(this, spawnPos);
		_lastDeadPlayerUnitId = "";

		return $"Resurrect restored {resurrectDef.DisplayName} at {Mathf.RoundToInt(spell.Power * 100f)}% health.";
	}

	private Color ResolveBaseBodyColor(Color source, float flashTimer, float healthRatio)
	{
		var wornColor = source.Lerp(new Color("4a4e69"), (1f - healthRatio) * 0.28f);
		var flashStrength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f);
		return wornColor.Lerp(Colors.White, flashStrength * 0.42f);
	}

	private Color ResolveBaseCoreColor(Color source, float flashTimer, float healthRatio)
	{
		var dangerColor = source.Lerp(new Color("ff6b6b"), (1f - healthRatio) * 0.24f);
		var flashStrength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f);
		return dangerColor.Lerp(Colors.White, flashStrength * 0.5f);
	}

	private void DrawBaseHealthMeter(CanvasItem canvas, Vector2 center, float width, float healthRatio, bool friendly)
	{
		var highContrast = GameState.Instance?.HighContrast ?? false;
		// Base meters shrink with the bases they label.
		width *= _combat.StructureScale;
		var origin = center - new Vector2(width * 0.5f, 0f);
		HealthBarPainter.Draw(canvas, new Rect2(origin, new Vector2(width, (highContrast ? 18f : 16f) * Mathf.Max(.6f, _combat.StructureScale))), healthRatio,
			friendly ? _playerHealthBarMotion.TrailRatio : _enemyHealthBarMotion.TrailRatio,
			friendly, HealthBarKind.Base, highContrast);
	}

	private void DrawCriticalHealthVignette()
	{
		var healthRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);
		if (healthRatio >= 0.35f)
		{
			return;
		}

		var intensity = Mathf.Clamp((0.35f - healthRatio) / 0.35f, 0f, 1f);
		var pulse = 0.5f + (Mathf.Sin(_elapsed * 4f) * 0.5f);
		var alpha = intensity * Mathf.Lerp(0.06f, 0.18f, pulse);
		var vignetteColor = new Color(0.8f, 0.1f, 0.05f, alpha);
		const float edgeWidth = 48f;
		var viewport = GetViewportRect().Size;
		DrawRect(new Rect2(0f, 0f, edgeWidth, viewport.Y), vignetteColor, true);
		DrawRect(new Rect2(viewport.X - edgeWidth, 0f, edgeWidth, viewport.Y), vignetteColor, true);
		DrawRect(new Rect2(0f, 0f, viewport.X, edgeWidth * 0.6f), vignetteColor, true);
		DrawRect(new Rect2(0f, viewport.Y - (edgeWidth * 0.6f), viewport.X, edgeWidth * 0.6f), vignetteColor, true);
	}

	private void DrawDamageSmoke(CanvasItem canvas, Vector2 origin, float healthRatio, Color sourceColor)
	{
		if (healthRatio >= 0.7f)
		{
			return;
		}

		var smokeStrength = Mathf.Clamp((0.7f - healthRatio) / 0.7f, 0f, 1f);
		var plumeOffset = Mathf.Sin(_elapsed * 1.8f) * 6f;
		canvas.DrawCircle(origin + new Vector2(plumeOffset, -12f), 10f + (smokeStrength * 5f), new Color(sourceColor.Darkened(0.7f), 0.18f + (smokeStrength * 0.12f)));
		canvas.DrawCircle(origin + new Vector2(-4f + (plumeOffset * 0.4f), -24f), 14f + (smokeStrength * 6f), new Color(0f, 0f, 0f, 0.12f + (smokeStrength * 0.12f)));
	}

	private Color ResolveDeployButtonTint(UnitDefinition definition, bool isReady, bool hasCourage)
	{
		var tint = definition.GetTint();
		if (!isReady)
		{
			return tint.Darkened(0.45f);
		}

		if (!hasCourage)
		{
			return tint.Darkened(0.28f).Lerp(new Color("6c757d"), 0.35f);
		}

		return tint.Lerp(Colors.White, 0.25f);
	}

	private Color ResolveSpellButtonTint(SpellDefinition definition, bool isReady, bool hasMana, bool armed)
	{
		var tint = definition.GetTint();
		if (!isReady)
		{
			return tint.Darkened(0.45f);
		}

		if (!hasMana)
		{
			return tint.Darkened(0.28f).Lerp(new Color("6c757d"), 0.35f);
		}

		return armed
			? tint.Lightened(0.25f)
			: tint.Lerp(Colors.White, 0.22f);
	}

	private string BuildDeployButtonTooltip(UnitDefinition definition, int level, bool isReady, float cooldown)
	{
		var stats = BuildPlayerUnitStatsForBattle(definition);
		var effectiveDeployCooldown = ResolvePlayerDeployCooldown(definition);
		var status = isReady ? "Ready to deploy" : $"Cooldown: {cooldown:0.0}s";
		return
			$"Lv{level} {definition.DisplayName}\n" +
			$"{SquadSynergyCatalog.GetTagDisplayName(definition.SquadTag)}\n" +
			$"{status}\n" +
			$"HP {Mathf.RoundToInt(stats.MaxHealth)} · {stats.AttackDamage:0.#} damage · range {stats.AttackRange:0.#}\n" +
			$"{effectiveDeployCooldown:0.#}s recovery · {definition.Cost} courage" +
			UnitStatText.BuildInlineTraits(stats);
	}

	private float ResolvePlayerDeployCooldown(UnitDefinition definition)
	{
		var cooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(definition.DeployCooldown);
		if (IsChallengeMode)
		{
			cooldown *= _challengeMutator.DeployCooldownScale;
		}

		return Mathf.Max(1.5f, cooldown);
	}

	private float ResolvePlayerSpellCooldown(ResolvedSpellStats resolved)
	{
		var cooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(resolved.Cooldown);
		if (IsChallengeMode)
		{
			cooldown *= _challengeMutator.DeployCooldownScale;
		}

		return Mathf.Max(2f, cooldown);
	}

	private bool HasChallengeGhostRun()
	{
		return IsChallengeMode &&
			_challengeGhostRun != null &&
			_challengeGhostRun.Deployments != null &&
			_challengeGhostRun.Deployments.Count > 0;
	}

	private void UpdateChallengeGhost(float delta)
	{
		if (!HasChallengeGhostRun())
		{
			return;
		}

		while (_challengeGhostNextIndex < _challengeGhostRun.Deployments.Count &&
			_challengeGhostRun.Deployments[_challengeGhostNextIndex].TimeSeconds <= _elapsed + 0.001f)
		{
			TriggerChallengeGhostMarker(_challengeGhostRun.Deployments[_challengeGhostNextIndex]);
			_challengeGhostNextIndex++;
		}

		for (var i = _challengeGhostMarkers.Count - 1; i >= 0; i--)
		{
			_challengeGhostMarkers[i].Remaining -= delta;
			if (_challengeGhostMarkers[i].Remaining <= 0f)
			{
				_challengeGhostMarkers.RemoveAt(i);
			}
		}
	}

	private void TriggerChallengeGhostMarker(ChallengeDeploymentRecord deployment)
	{
		if (deployment == null || string.IsNullOrWhiteSpace(deployment.UnitId))
		{
			return;
		}

		var unit = GameData.GetUnit(deployment.UnitId);
		var ghostColor = unit.GetTint().Lightened(0.28f).Lerp(new Color("8ecae6"), 0.4f);
		var markerPosition = new Vector2(PlayerSpawnX + 26f, ResolveChallengeLaneY(deployment.LanePercent));
		_challengeGhostMarkers.Add(new ChallengeGhostMarker(unit.Id, markerPosition, ghostColor, deployment.TimeSeconds));
		SpawnEffect(markerPosition, ghostColor, 8f, 30f, 0.22f, false);
	}

	private float ResolveChallengeLaneY(int lanePercent)
	{
		return Mathf.Lerp(
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding,
			Mathf.Clamp(lanePercent, 0, 100) / 100f);
	}

	private void DrawChallengeGhostMarkers()
	{
		if (_challengeGhostMarkers.Count == 0)
		{
			return;
		}

		for (var i = 0; i < _challengeGhostMarkers.Count; i++)
		{
			var marker = _challengeGhostMarkers[i];
			var alpha = Mathf.Clamp(marker.Remaining / 1.3f, 0f, 1f);
			var radius = 18f + ((1f - alpha) * 14f);
			var color = new Color(marker.Color, 0.2f + (alpha * 0.55f));
			var rimColor = new Color(marker.Color.Lightened(0.12f), 0.4f + (alpha * 0.45f));
			DrawCircle(marker.Position, radius * 0.36f, color);
			DrawArc(marker.Position, radius, -Mathf.Pi * 0.5f, Mathf.Tau - (Mathf.Pi * 0.5f), 32, rimColor, 3f);
			DrawLine(marker.Position + new Vector2(-10f, 0f), marker.Position + new Vector2(10f, 0f), rimColor, 2f, true);
			DrawLine(marker.Position + new Vector2(0f, -10f), marker.Position + new Vector2(0f, 10f), rimColor, 2f, true);
		}
	}

	private void ShowEndPanelAnimated()
	{
		// Earned stars ring out one by one as the result appears (D major, in tune with the victory fanfare).
		if (_endStarRating.Visible) AudioDirector.Instance?.PlayStars(_endStarRating.Stars);
		if (IsInstanceValid(_royalResult)) { _royalResult.Visible = true; _royalResult.Appear(); return; }
		_endCenter.Visible = true;
		_endPanel.Visible = true;
		_endPanel.Modulate = new Color(1f, 1f, 1f, 0f);
		_endPanel.Scale = new Vector2(0.94f, 0.94f);
		_endPanel.PivotOffset = _endPanel.Size * 0.5f;
		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(_endPanel, "modulate:a", 1f, 0.35f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_endPanel, "scale", Vector2.One, 0.4f)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	private void SpawnBattleEndParticles(bool playerWon)
	{
		if (playerWon)
		{
			var goldColor = new Color("ffd166");
			BattleParticles.SpawnDeployBurst(this, EnemyBaseCorePosition, goldColor);
			BattleParticles.SpawnDeployBurst(this, EnemyBaseCorePosition + new Vector2(0f, -40f), goldColor.Lightened(0.15f));
			BattleParticles.SpawnDeathBurst(this, EnemyBaseCorePosition, new Color("ef476f"), true);
		}
		else
		{
			var smokeColor = new Color(0.3f, 0.28f, 0.25f);
			BattleParticles.SpawnDeathBurst(this, PlayerBaseCorePosition, smokeColor, true);
			BattleParticles.SpawnBaseHitDebris(this, PlayerBaseCorePosition, smokeColor);
		}
	}

	private string BuildChallengeGhostDeployFeedback(UnitDefinition definition)
	{
		if (!HasChallengeGhostRun())
		{
			return "";
		}

		var deploymentIndex = _playerDeployments - 1;
		if (deploymentIndex < 0)
		{
			return "";
		}

		if (deploymentIndex >= _challengeGhostRun.Deployments.Count)
		{
			return " Beyond the saved ghost tape.";
		}

		var ghostDeployment = _challengeGhostRun.Deployments[deploymentIndex];
		var ghostUnit = GameData.GetUnit(ghostDeployment.UnitId);
		var timeDelta = _elapsed - ghostDeployment.TimeSeconds;
		var sameUnit = definition.Id.Equals(ghostDeployment.UnitId, StringComparison.OrdinalIgnoreCase);
		var paceLabel = timeDelta <= -0.15f
			? "AHEAD"
			: timeDelta >= 0.15f
				? "LATE"
				: "SYNC";
		return sameUnit
			? $" Ghost split {deploymentIndex + 1}: {paceLabel.ToLowerInvariant()} {Mathf.Abs(timeDelta):0.0}s."
			: $" Ghost split {deploymentIndex + 1}: swapped {definition.DisplayName} for {ghostUnit.DisplayName}, timing {FormatSignedSeconds(timeDelta)}.";
	}

	private string BuildChallengeGhostResultSummary(int finalScore, int starsEarned)
	{
		if (_challengeGhostRun == null)
		{
			return "Ghost comparison: no saved benchmark armed for this board.";
		}

		var scoreDelta = finalScore - _challengeGhostRun.Score;
		var starDelta = starsEarned - _challengeGhostRun.StarsEarned;
		var timeDelta = _elapsed - _challengeGhostRun.ElapsedSeconds;
		var hullPercent = Mathf.RoundToInt(Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f) * 100f);
		var ghostHullPercent = Mathf.RoundToInt(Mathf.Clamp(_challengeGhostRun.BusHullRatio, 0f, 1f) * 100f);
		var hullDelta = hullPercent - ghostHullPercent;
		var deployDelta = _playerDeployments - _challengeGhostRun.PlayerDeployments;
		var timeText = timeDelta <= -0.05f
			? $"Faster by {Mathf.Abs(timeDelta):0.0}s"
			: timeDelta >= 0.05f
				? $"Slower by {Mathf.Abs(timeDelta):0.0}s"
				: "Time matched";
		return
			$"Ghost comparison: {FormatSignedInt(scoreDelta)} pts · {timeText} · Hull {FormatSignedInt(hullDelta)}% · Deploys {FormatSignedInt(deployDelta)} · Stars {FormatSignedInt(starDelta)}";
	}

	private void MaybeOpenEndlessDraft()
	{
		if (!IsEndlessMode || _endlessCheckpointActive || !_spawnDirector.EndlessCheckpointPending)
		{
			return;
		}

		if (_spawnDirector.PendingSpawnCount > 0 || CountTeamUnits(Team.Enemy) > 0)
		{
			return;
		}

		ResolveEndlessBossCheckpoint();
		_endlessCheckpointActive = true;
		_draftingRouteFork = IsRouteForkCheckpoint();
		_draftOptionIds = _draftingRouteFork ? BuildRouteForkOptions() : BuildDraftOptions();
		_draftLabel.Text = _draftingRouteFork
			? $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose the next route segment before the caravan rolls out.\n{BuildEndlessBossCheckpointCheckpointSummary()}"
			: $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose one run upgrade before the next surge.\n{BuildEndlessBossCheckpointCheckpointSummary()}";

		for (var i = 0; i < _draftButtons.Count; i++)
		{
			var option = _draftingRouteFork
				? GetRouteForkOption(_draftOptionIds[i])
				: GetDraftOption(_draftOptionIds[i]);
			_draftButtons[i].Text = $"{option.Title}\n{option.Summary}";
			_draftButtons[i].Disabled = false;
		}

		_draftCenter.Visible = true;
		_draftPanel.Visible = true;
		SetStatus(_draftingRouteFork ? "Checkpoint held. Pick the next route segment." : "Checkpoint held. Pick a caravan boon.");
		UpdateHud();
	}

	private string[] BuildDraftOptions()
	{
		var available = new List<string>
		{
			"bus_plates",
			"supply_drop",
			"courage_pump",
			"shock_drill",
			"field_tonic",
			"salvage_contract",
			"relic_spark",
			"berserk_ritual",
			"mirror_ward",
			"rally_banner"
		};

		for (var i = available.Count - 1; i >= 0; i--)
		{
			if (_endlessRunUpgrades.Contains(available[i]))
			{
				available.RemoveAt(i);
			}
		}

		while (available.Count < 3)
		{
			available.Add("supply_drop");
		}

		var picked = new List<string>();
		while (picked.Count < 3 && available.Count > 0)
		{
			var index = _rng.RandiRange(0, available.Count - 1);
			picked.Add(available[index]);
			available.RemoveAt(index);
		}

		return picked.ToArray();
	}

	private string[] BuildRouteForkOptions()
	{
		var options = EndlessRouteForkCatalog.GetAll();
		return new[]
		{
			options[0].Id,
			options[1].Id,
			options[2].Id
		};
	}

	private void ApplyEndlessDraftChoice(int draftIndex)
	{
		if (!_endlessCheckpointActive || draftIndex < 0 || draftIndex >= _draftOptionIds.Length)
		{
			return;
		}

		if (_draftingRouteFork)
		{
			var fork = GetRouteForkOption(_draftOptionIds[draftIndex]);
			ApplyEndlessRouteFork(fork.Id);
			SetStatus($"Route fork selected: {fork.Title}.");
		}
		else
		{
			var option = GetDraftOption(_draftOptionIds[draftIndex]);
			ApplyEndlessRunUpgrade(option.Id);
			SetStatus($"Checkpoint upgrade applied: {option.Title}.");
		}

		_endlessCheckpointActive = false;
		_draftCenter.Visible = false;
		_draftPanel.Visible = false;
		_draftOptionIds = Array.Empty<string>();
		_draftingRouteFork = false;
		_spawnDirector.ResumeEndlessAfterCheckpoint(_elapsed);
		UpdateHud();
	}

	private void ApplyEndlessRunUpgrade(string optionId)
	{
		switch (optionId)
		{
			case "bus_plates":
			{
				_endlessRunUpgrades.Add(optionId);
				var hullGain = _playerBaseMaxHealth * 0.15f;
				_playerBaseMaxHealth += hullGain;
				_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + hullGain);
				break;
			}
			case "supply_drop":
				_courage = Mathf.Min(_maxCourage, _courage + 25f);
				break;
			case "courage_pump":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_courageGainPerSecond *= 1.2f;
				}
				break;
			case "shock_drill":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessUnitDamageScale *= 1.12f;
				}
				break;
			case "field_tonic":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessUnitHealthScale *= 1.18f;
				}
				break;
			case "salvage_contract":
				if (_endlessRunUpgrades.Add(optionId))
				{
					_endlessGoldScale *= 1.15f;
				}
				break;
			case "relic_spark":
			{
				_endlessRunUpgrades.Add(optionId);
				var candidates = GameData.GetAllEquipment()
					.Where(e => string.Equals(e.Rarity, "common", StringComparison.OrdinalIgnoreCase))
					.ToList();
				if (candidates.Count > 0)
				{
					var relic = candidates[_rng.RandiRange(0, candidates.Count - 1)];
					GameState.Instance.TryGrantEquipment(relic.Id);
				}
				break;
			}
			case "berserk_ritual":
				_endlessTempDamageScale = 1.15f;
				_endlessTempDamageExpiry = _elapsed + 60f;
				break;
			case "mirror_ward":
				_endlessDamageReflectRatio = 0.2f;
				_endlessDamageReflectExpiry = _elapsed + 45f;
				break;
			case "rally_banner":
				RepairBusByRatio(0.25f);
				_courage = Mathf.Min(_maxCourage, _courage + 8f);
				break;
		}
	}

	private static EndlessDraftOption GetDraftOption(string optionId)
	{
		return optionId switch
		{
			"bus_plates" => new EndlessDraftOption("bus_plates", "Wagon Plates", "Add 15% max war wagon hull and repair the caravan by the same amount."),
			"supply_drop" => new EndlessDraftOption("supply_drop", "Supply Drop", "Immediately gain +25 courage for the next deployment burst."),
			"courage_pump" => new EndlessDraftOption("courage_pump", "Courage Pump", "Increase courage generation by 20% for the rest of the run."),
			"shock_drill" => new EndlessDraftOption("shock_drill", "Shock Drill", "Future deployed units deal 12% more damage for the rest of the run."),
			"field_tonic" => new EndlessDraftOption("field_tonic", "Field Tonic", "Future deployed units gain 18% more health for the rest of the run."),
				"salvage_contract" => new EndlessDraftOption("salvage_contract", "Quartermaster Ledger", "Increase final gold payout by 15% for the rest of the run."),
			"relic_spark" => new EndlessDraftOption("relic_spark", "Relic Spark", "Grant a random common relic."),
			"berserk_ritual" => new EndlessDraftOption("berserk_ritual", "Berserk Ritual", "All deployed units gain +15% attack damage for 60 seconds."),
			"mirror_ward" => new EndlessDraftOption("mirror_ward", "Mirror Ward", "War wagon reflects 20% of damage taken for the next 45 seconds."),
			"rally_banner" => new EndlessDraftOption("rally_banner", "Rally Banner", "Restore 25% war wagon hull and gain +8 courage."),
			_ => new EndlessDraftOption("supply_drop", "Supply Drop", "Immediately gain +25 courage for the next deployment burst.")
		};
	}

	private static EndlessDraftOption GetRouteForkOption(string optionId)
	{
		var fork = EndlessRouteForkCatalog.Get(optionId);
		return new EndlessDraftOption(fork.Id, fork.Title, fork.Summary);
	}

	private void ApplyEndlessRouteFork(string optionId)
	{
		_endlessRouteForkId = EndlessRouteForkCatalog.Normalize(optionId);
		_spawnDirector.SetEndlessRouteFork(_endlessRouteForkId);

		TriggerRouteForkSupportEvent(_endlessRouteForkId);
	}

	private void TriggerRouteForkSupportEvent(string routeForkId)
	{
		switch (routeForkId)
		{
			case EndlessRouteForkCatalog.MainlinePushId:
				_endlessSupportEventLabel = "Dispatch riders arrived: +20 courage and cooldown recovery across the squad.";
				_courage = Mathf.Min(_maxCourage, _courage + 20f);
				_deck.ReduceCooldowns(3f);
				_spellDeck.ReduceCooldowns(3f);
				break;
			case EndlessRouteForkCatalog.ScavengeDetourId:
					_endlessSupportEventLabel = "Supplies recovered: the war wagon was repaired.";
				RepairBusByRatio(0.1f);
				break;
			case EndlessRouteForkCatalog.FortifiedBlockId:
				_endlessSupportEventLabel = "Fortifications secured: the war wagon was repaired.";
				RepairBusByRatio(0.12f);
				break;
		}
	}

	private string BuildEndlessBossCheckpointCheckpointSummary()
	{
		if (!IsEndlessMode)
		{
			return "Boss checkpoint report: standby.";
		}

		if (!EndlessBossCheckpointCatalog.IsBossCheckpointWave(_spawnDirector.EndlessWaveNumber))
		{
			return "Boss checkpoint report: no warlord surge on this checkpoint.";
		}

		var definition = EndlessBossCheckpointCatalog.GetForWave(_spawnDirector.EndlessWaveNumber, _activeRouteId);
		return $"[OK] {definition.Title} · {definition.RewardSummary}";
	}

	private void ResolveEndlessBossCheckpoint()
	{
		if (!IsEndlessMode || !EndlessBossCheckpointCatalog.IsBossCheckpointWave(_spawnDirector.EndlessWaveNumber))
		{
			return;
		}

		var definition = EndlessBossCheckpointCatalog.GetForWave(_spawnDirector.EndlessWaveNumber, _activeRouteId);
		_lastEndlessBossCheckpointWave = _spawnDirector.EndlessWaveNumber;
		_lastEndlessBossCheckpointTitle = definition.Title;
		_endlessBossCheckpointsCleared++;
		_endlessBossGoldBonus += definition.RewardGold;
		_endlessBossFoodBonus += definition.RewardFood;

		switch (_activeRouteId)
		{
			case RouteCatalog.CityId:
				_courage = Mathf.Min(_maxCourage, _courage + 18f);
				break;
			case RouteCatalog.HarborId:
				RepairBusByRatio(0.06f);
				break;
			case RouteCatalog.FoundryId:
				_deck.ReduceCooldowns(1f);
				_spellDeck.ReduceCooldowns(1f);
				break;
			case RouteCatalog.QuarantineId:
				break;
			case RouteCatalog.ThornwallId:
				break;
			case RouteCatalog.BasilicaId:
				break;
			case RouteCatalog.MireId:
				break;
			case RouteCatalog.SteppeId:
				break;
			case RouteCatalog.GloamwoodId:
				break;
			case RouteCatalog.CitadelId:
				break;
		}

		var route = RouteCatalog.Get(_activeRouteId);
		SpawnEffect(EnemyBaseCorePosition, route.BannerAccent.Lightened(0.08f), 18f, 72f, 0.3f, false);
		SetStatus($"{definition.Title} broken on wave {_spawnDirector.EndlessWaveNumber}. {definition.ClearStatus}");
	}

	private UnitStats BuildPlayerUnitStatsForBattle(UnitDefinition definition)
	{
		if (!IsEndlessMode)
		{
			return GameState.Instance.BuildPlayerUnitStatsForDeck(definition, GameState.Instance.GetBattleDeckUnits());
		}

		return GameState.Instance.BuildPlayerUnitStatsForDeck(
			definition,
			GameState.Instance.GetBattleDeckUnits(),
			_endlessUnitHealthScale,
			_endlessUnitDamageScale,
			0f,
			0);
	}

	private void RepairBusByRatio(float ratio)
	{
		if (ratio <= 0f)
		{
			return;
		}

		var healAmount = _playerBaseMaxHealth * ratio;
		_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + healAmount);
		_playerBaseFlashTimer = 0.18f;
		AudioDirector.Instance?.PlayBusRepair(healAmount);
		SpawnEffect(PlayerBaseCorePosition, new Color("80ed99"), 10f, 28f, 0.22f);
		SpawnCombatNumber(PlayerBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(healAmount)}", new Color("b7efc5"), 0.56f);
	}

	private void DamageEnemyBaseByRatio(float ratio, Color color)
	{
		if (ratio <= 0f)
		{
			return;
		}

		var damageAmount = _enemyBaseMaxHealth * ratio;
		_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth - damageAmount);
		_enemyBaseFlashTimer = 0.22f;
		AudioDirector.Instance?.PlayBaseHit(false, damageAmount);
		SpawnEffect(EnemyBaseCorePosition, color, 10f, 30f, 0.22f, false);
		SpawnCombatNumber(EnemyBaseCorePosition + new Vector2(0f, -38f), $"-{Mathf.RoundToInt(damageAmount)}", color.Lightened(0.1f), 0.56f);

	}

	private void RepairEnemyBaseByRatio(float ratio, Color color)
	{
		if (ratio <= 0f || _enemyBaseHealth <= 0f)
		{
			return;
		}

		var repairAmount = _enemyBaseMaxHealth * ratio;
		_enemyBaseHealth = Mathf.Min(_enemyBaseMaxHealth, _enemyBaseHealth + repairAmount);
		_enemyBaseFlashTimer = 0.18f;
		SpawnEffect(EnemyBaseCorePosition, color.Lightened(0.08f), 10f, 28f, 0.22f);
		SpawnCombatNumber(EnemyBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(repairAmount)}", color.Lightened(0.18f), 0.56f);

	}

	private float RepairBusByAmount(float amount)
	{
		if (amount <= 0f || _playerBaseHealth >= _playerBaseMaxHealth)
		{
			return 0f;
		}

		var repaired = Mathf.Min(amount, _playerBaseMaxHealth - _playerBaseHealth);
		_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + repaired);
		_playerBaseFlashTimer = 0.18f;
		AudioDirector.Instance?.PlayBusRepair(repaired);
		SpawnEffect(PlayerBaseCorePosition, new Color("80ed99"), 10f, 28f, 0.22f);
		SpawnCombatNumber(PlayerBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(repaired)}", new Color("b7efc5"), 0.56f);
		return repaired;
	}

	private void DamageBusByRatio(float ratio, Color color)
	{
		if (ratio <= 0f)
		{
			return;
		}

		var damageAmount = _playerBaseMaxHealth * ratio;
		_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth - damageAmount);
		RegisterPlayerHullDamage(damageAmount);
		_playerBaseFlashTimer = 0.22f;
		AudioDirector.Instance?.PlayBaseHit(true, damageAmount);
		SpawnEffect(PlayerBaseCorePosition, color, 10f, 30f, 0.22f, false);
		SpawnCombatNumber(PlayerBaseCorePosition + new Vector2(0f, -38f), $"-{Mathf.RoundToInt(damageAmount)}", color.Lightened(0.1f), 0.56f);

	}

	private bool IsRouteForkCheckpoint()
	{
		return _spawnDirector.EndlessWaveNumber > 0 && _spawnDirector.EndlessWaveNumber % 10 == 0;
	}

	private void CheckBattleEnd()
	{
		if (IsEndlessMode)
		{
			if (_playerBaseHealth <= 0f)
			{
				EndBattle(false);
			}

			return;
		}

		if (_playerBaseHealth <= 0f)
		{
			EndBattle(false);
			return;
		}

		if (_enemyBaseHealth <= 0f)
		{
			_enemyBaseHealth = 0f;
			EndBattle(true);
		}
	}

	private void EndBattle(bool playerWon)
	{
		if (_battleEnded)
		{
			return;
		}

		// Rations recharged during play are not battle loot.
		_battleRewardStart.Food = GameState.Instance.Food;
		_battleEnded = true;
		AudioDirector.Instance?.SetBattlePressure(0.18f);
		_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth);
		_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth);
		SpawnBattleEndParticles(playerWon);
		_endStarRating.Visible = !IsEndlessMode;
		_endStarRating.Stars = StageStarScore.Evaluate(playerWon, _playerBaseHealth, _playerBaseMaxHealth, _playerHullTookDamage);

		if (IsEndlessMode)
		{
			FinalizeEndlessRun(false);
			return;
		}

		if (IsChallengeMode)
		{
			if (playerWon)
			{
				AudioDirector.Instance?.PlayVictory();
			}
			else
			{
				AudioDirector.Instance?.PlayDefeat();
			}

			var stageResult = BuildStageBattleResult();
			var evaluation = StageObjectives.EvaluateBattle(_stageData, stageResult, playerWon);
			var starsEarned = evaluation.StarsEarned;
			var scoreBreakdown = AsyncChallengeCatalog.CalculateScoreBreakdown(_challengeDefinition, stageResult, playerWon, starsEarned);
			var medalLabel = AsyncChallengeCatalog.ResolveMedalLabel(_challengeDefinition, scoreBreakdown.FinalScore);
			var challengeDeckUnitIds = GameState.Instance.GetSelectedAsyncChallengeDeckUnits()
				.Select(unit => unit.Id)
				.ToArray();
			GameState.Instance.ApplyAsyncChallengeResult(
				_challengeDefinition.Code,
				scoreBreakdown.FinalScore,
				_elapsed,
				_enemyDefeats,
				starsEarned,
				playerWon,
				false,
				challengeDeckUnitIds,
				_challengeDeploymentTape,
				_playerDeployments,
				_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
				GameState.Instance.HasSelectedAsyncChallengeLockedDeck,
				scoreBreakdown);
			LanChallengeService.Instance?.SubmitChallengeResult(
				_challengeDefinition,
				scoreBreakdown,
				_elapsed,
				starsEarned,
				_enemyDefeats,
				_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
				playerWon,
				false,
				GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
			var onlineRoomResultSubmitted = false;
			if (OnlineRoomResultService.HasJoinedRoomForChallenge(_challengeDefinition))
			{
				onlineRoomResultSubmitted = OnlineRoomResultService.SubmitChallengeResult(
					_challengeDefinition,
					scoreBreakdown,
					_elapsed,
					starsEarned,
					_enemyDefeats,
					_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
					playerWon,
					false,
					GameState.Instance.HasSelectedAsyncChallengeLockedDeck,
					out _);
			}
			var challengeStatsBreakdown = BuildBattleStatsBreakdown();
			_lanChallengeEndBaseText =
				$"Challenge {_challengeDefinition.Code}\n" +
				$"{(playerWon ? "Cleared" : "Failed")} · Score {scoreBreakdown.FinalScore} · Tier {medalLabel}\n" +
				$"{BuildStageBattleStatsText(stageResult)}\n" +
				$"{AsyncChallengeCatalog.BuildScoreSummary(scoreBreakdown)}\n" +
				$"{AsyncChallengeCatalog.BuildTargetSummary(_challengeDefinition, scoreBreakdown.FinalScore)}\n" +
				$"{StageObjectives.BuildOutcomeSummary(evaluation)}\n" +
				$"{BuildChallengeGhostResultSummary(scoreBreakdown.FinalScore, starsEarned)}\n" +
				$"Personal best: {GameState.Instance.GetAsyncChallengeBestScore(_challengeDefinition.Code)}" +
				(string.IsNullOrWhiteSpace(challengeStatsBreakdown) ? "" : $"\n{challengeStatsBreakdown}");
			if (!IsLanRaceMode)
			{
				_endLabel.Text = _lanChallengeEndBaseText;
			}
			SetStatus(playerWon
				? IsLanRaceMode
					? "LAN race result submitted. Return to the room for the shared scoreboard."
					: onlineRoomResultSubmitted
						? "Online room result submitted. Return to multiplayer for the shared room board."
					: "Challenge clear recorded. Share the code and see who posts the cleaner score."
				: IsLanRaceMode
					? "LAN race result submitted. Return to the room and queue a rematch."
					: onlineRoomResultSubmitted
						? "Online room failure recorded. Return to multiplayer for the shared room board."
					: "Challenge failed. Refit the approach and try the same code again.");
			// Solo challenges end on the royal result board; shared rooms keep their scoreboard card.
			if (!IsLanRaceMode && !IsOnlineRoomMode)
				PresentResult(playerWon, playerWon ? "Challenge cleared" : "Challenge failed", $"Score {scoreBreakdown.FinalScore:N0} · {medalLabel}");
			else
			{
				if (playerWon) PresentResult(true, "Victory");
				_endCenter.Visible = true;
				_endPanel.Visible = true;
				RefreshLanRaceEndPanel();
				RefreshOnlineRoomEndPanel(true);
			}
			UpdateHud();
			return;
		}

		if (playerWon)
		{
			AudioDirector.Instance?.PlayVictory();
			var stageResult = BuildStageBattleResult();
			var evaluation = StageObjectives.EvaluateBattle(_stageData, stageResult, true);
			var rewardGold = IsCampaignMode ? _stageData.RewardGold : IsTowerMode ? ChallengeTowerCatalog.GetFloor(GameState.Instance.SelectedTowerFloor)?.RewardGold ?? 0 : 0;
			var rewardFood = IsCampaignMode ? _stageData.RewardFood : IsTowerMode ? ChallengeTowerCatalog.GetFloor(GameState.Instance.SelectedTowerFloor)?.RewardFood ?? 0 : 0;
			if (IsCampaignMode) GameState.Instance.ApplyVictory(_stage, rewardGold, rewardFood, evaluation.StarsEarned);
			if (IsArenaMode && GameState.Instance.SelectedArenaOpponent != null)
			{
				GameState.Instance.ApplyArenaResult(true, GameState.Instance.SelectedArenaOpponent.ArenaRating);
			}
			if (IsTowerMode)
			{
				GameState.Instance.ApplyTowerVictory(GameState.Instance.SelectedTowerFloor, evaluation.StarsEarned);
			}
			// Bounty: stage cleared
			GameState.Instance.AddBountyProgress("stages_cleared", 1);
			GameState.Instance.AddSeasonXP(SeasonPassCatalog.XPPerBattleWin);
			// Mastery: battle win XP for all deployed units
			foreach (var unit in _units)
			{
				if (!unit.IsDead && unit.Team == Team.Player)
				{
					GameState.Instance.AddUnitMasteryXP(unit.DefinitionId, MasteryCatalog.XPPerBattleWin);
				}
			}
			if (IsSeasonalEventMode && !string.IsNullOrWhiteSpace(GameState.Instance.SelectedEventId))
			{
				GameState.Instance.RecordEventStageCleared(GameState.Instance.SelectedEventId);
				var eventDef = SeasonalEventCatalog.GetById(GameState.Instance.SelectedEventId);
				var eventStageIdx = GameState.Instance.SelectedEventStageIndex;
				if (eventDef?.Stages != null && eventStageIdx >= 0 && eventStageIdx < eventDef.Stages.Length)
				{
					var stageReward = eventDef.Stages[eventStageIdx].CompletionReward;
					if (stageReward != null)
					{
						GameState.Instance.ApplyEventStageReward(stageReward);
					}
				}
			}
			var busHealthRatio = _playerBaseMaxHealth > 0f ? _playerBaseHealth / _playerBaseMaxHealth : 0f;
			GameState.Instance.CheckCombatAchievements(busHealthRatio, _elapsed, _triggeredComboPairIds.Count, 0);
			GameState.Instance.ConsumeAchievementNotification();
			PresentResult(true, "Victory");
			SetStatus("Gatehouse shattered. Route secured.");
		}
		else
		{
			AudioDirector.Instance?.PlayDefeat();
			if (IsArenaMode && GameState.Instance.SelectedArenaOpponent != null)
			{
				GameState.Instance.ApplyArenaResult(false, GameState.Instance.SelectedArenaOpponent.ArenaRating);
			}
			if (IsCampaignMode) GameState.Instance.ApplyDefeat(_stage);
			PresentResult(false, "Defeat");
			SetStatus("The war wagon was overrun. Regroup.");
		}

		ShowEndPanelAnimated();
		UpdateHud();
	}

	private void RetreatToMap()
	{
		// Scene transitions must be able to animate when retreating from the pause menu.
		if (_battlePaused) TogglePause();
		if (IsEndlessMode)
		{
			if (!_battleEnded)
			{
				_battleEnded = true;
				_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth);
				_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth);
				FinalizeEndlessRun(true);
			}

			SceneRouter.Instance.GoToMap();
			return;
		}

		if (IsChallengeMode)
		{
			if (!_battleEnded)
			{
				var challengeDeckUnitIds = GameState.Instance.GetSelectedAsyncChallengeDeckUnits()
					.Select(unit => unit.Id)
					.ToArray();
				GameState.Instance.ApplyAsyncChallengeResult(
					_challengeDefinition.Code,
					0,
					_elapsed,
					_enemyDefeats,
					0,
					false,
					true,
					challengeDeckUnitIds,
					_challengeDeploymentTape,
					_playerDeployments,
					_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
					GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
				LanChallengeService.Instance?.SubmitChallengeResult(
					_challengeDefinition,
					null,
					_elapsed,
					0,
					_enemyDefeats,
					_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
					false,
					true,
					GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
				if (OnlineRoomResultService.HasJoinedRoomForChallenge(_challengeDefinition))
				{
					OnlineRoomResultService.SubmitChallengeResult(
						_challengeDefinition,
						null,
						_elapsed,
						0,
						_enemyDefeats,
						_playerBaseMaxHealth <= 0f ? 0f : _playerBaseHealth / _playerBaseMaxHealth,
						false,
						true,
						GameState.Instance.HasSelectedAsyncChallengeLockedDeck,
						out _);
				}
			}

			_battleEnded = true;
			if (IsOnlineRoomMode) OnlineRoomActionService.LeaveRoom(out _);
			SceneRouter.Instance.GoToMap();
			return;
		}

		if (!_battleEnded && IsCampaignMode)
		{
			GameState.Instance.ApplyRetreat(_stage);
		}
		_battleEnded = true;
		SceneRouter.Instance.GoToMap();
	}

	private void HandleEndPanelPrimaryAction()
	{
		if (!_battleEnded)
		{
			return;
		}

		if (IsLanRaceMode)
		{
			SceneRouter.Instance.GoToLanRace();
			return;
		}

		if (IsOnlineRoomMode)
		{
			SceneRouter.Instance.GoToMultiplayer();
			return;
		}

		TryRestartBattle();
	}

	private void HandleEndPanelSecondaryAction()
	{
		if (!_battleEnded)
		{
			return;
		}

		if (IsEndlessMode)
		{
			SceneRouter.Instance.GoToEndless();
			return;
		}

		if (IsChallengeMode)
		{
			if (IsLanRaceMode)
			{
				SceneRouter.Instance.GoToMultiplayer();
			}
			else if (IsOnlineRoomMode)
			{
				OnlineRoomActionService.LeaveRoom(out _);
				SceneRouter.Instance.GoToMultiplayer();
			}
			else
			{
				SceneRouter.Instance.GoToMultiplayer();
			}
			return;
		}

		if (IsTowerMode) SceneRouter.Instance.GoToTower();
		else if (IsArenaMode) SceneRouter.Instance.GoToArena();
		else if (IsSeasonalEventMode) SceneRouter.Instance.GoToEvent();
		else SceneRouter.Instance.GoToMap();
	}

	private StageBattleResult BuildStageBattleResult()
	{
		// Stage hazards and mission events are described in stage data but not simulated, so they report none.
		return new StageBattleResult
		{
			PlayerBaseHealth = _playerBaseHealth,
			PlayerBaseMaxHealth = _playerBaseMaxHealth,
			PlayerBaseTookDamage = _playerHullTookDamage,
			Elapsed = _elapsed,
			PlayerDeployments = _playerDeployments,
			EnemyDefeats = _enemyDefeats,
			PlayerSignalJamSeconds = _playerSignalJamSeconds
		};
	}

	private void FinalizeEndlessRun(bool retreated)
	{
		AudioDirector.Instance?.SetBattlePressure(0.14f);
		var rewardGold = CalculateEndlessGoldReward();
		var rewardFood = CalculateEndlessFoodReward();
		GameState.Instance.ApplyEndlessResult(_activeRouteId, _spawnDirector.EndlessWaveNumber, _elapsed, _enemyDefeats, rewardGold, rewardFood, retreated);
		var busHealthRatio = _playerBaseMaxHealth > 0f ? _playerBaseHealth / _playerBaseMaxHealth : 0f;
		GameState.Instance.CheckCombatAchievements(busHealthRatio, _elapsed, _triggeredComboPairIds.Count, _endlessBossCheckpointsCleared);
		GameState.Instance.ConsumeAchievementNotification();
		var time = System.TimeSpan.FromSeconds(_elapsed);
		if (!retreated) PresentResult(false, "Run over", $"Wave {_spawnDirector.EndlessWaveNumber} reached · {(int)time.TotalMinutes}:{time.Seconds:00}");
		SetStatus(retreated
			? "The caravan withdrew in good order and banked its spoils."
			: "The caravan was eventually overrun. Rear scouts recovered what they could.");
		if (!retreated)
		{
			AudioDirector.Instance?.PlayDefeat();
		}

		if (!retreated) ShowEndPanelAnimated();
		UpdateHud();
	}

	private string BuildStageBattleStatsText(StageBattleResult result)
	{
		var routeLabel = ResolveRouteLabel(_activeRouteId);
		var hullPercent = Mathf.RoundToInt(Mathf.Clamp(result.PlayerBaseHealth / Mathf.Max(1f, result.PlayerBaseMaxHealth), 0f, 1f) * 100f);
		return
			$"{routeLabel} · Stage {_stage} · {_stageData.StageName}\n" +
			$"Time {result.Elapsed:0.0}s · Hull {hullPercent}% · Enemy defeats {result.EnemyDefeats} · Deployments {result.PlayerDeployments}\n" +
			$"Hazard hits {result.PlayerHazardHits} · Signal jam {result.PlayerSignalJamSeconds:0.0}s";
	}

	private float ResolveBattleAudioPressure()
	{
		var enemyCountPressure = Mathf.Clamp(CountTeamUnits(Team.Enemy) / 12f, 0f, 1f) * 0.45f;
		var hullPressure = (1f - Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f)) * 0.3f;
		var pendingPressure = Mathf.Clamp(_spawnDirector.PendingSpawnCount / 10f, 0f, 1f) * 0.12f;
		var endlessPressure = IsEndlessMode
			? Mathf.Clamp(_spawnDirector.EndlessWaveNumber / 40f, 0f, 1f) * 0.14f
			: 0f;
		return Mathf.Clamp(enemyCountPressure + hullPressure + pendingPressure + endlessPressure, 0f, 1f);
	}

	private int CalculateEndlessGoldReward()
	{
		var timeBonus = Mathf.FloorToInt(_elapsed / 18f) * 3;
		var reward = Math.Max(0, (_spawnDirector.EndlessWaveNumber * 16) + (_enemyDefeats * 2) + timeBonus);
		reward = Mathf.RoundToInt(reward * _endlessGoldScale);
		reward = Mathf.RoundToInt(reward * ResolveRouteForkGoldScale());
		if (_endlessBoonId == EndlessBoonCatalog.SalvageCacheId)
		{
			reward = Mathf.RoundToInt(reward * 1.25f);
		}

		return Math.Max(0, reward + _endlessBossGoldBonus);
	}

	private int CalculateEndlessFoodReward()
	{
		if (_spawnDirector.EndlessWaveNumber <= 0)
		{
			return 0;
		}

		return Math.Max(0, (_spawnDirector.EndlessWaveNumber / 4) + _endlessBossFoodBonus);
	}

	private static string FormatSignedInt(int value)
	{
		return value >= 0 ? $"+{value}" : value.ToString();
	}

	private static string FormatSignedSeconds(float value)
	{
		return value >= 0f ? $"+{value:0.0}s" : $"-{Mathf.Abs(value):0.0}s";
	}

	private static string ResolveRouteLabel(string routeId)
	{
		return RouteCatalog.Get(routeId).Title;
	}

	private static string NormalizeRouteId(string routeId)
	{
		return RouteCatalog.Normalize(routeId);
	}

	private void ApplyEndlessBoon()
	{
		switch (_endlessBoonId)
		{
			case EndlessBoonCatalog.ReinforcedBusId:
				_playerBaseMaxHealth *= 1.2f;
				_playerBaseHealth = _playerBaseMaxHealth;
				break;
			case EndlessBoonCatalog.SurplusCourageId:
				_courageGainPerSecond *= 1.2f;
				break;
			case EndlessBoonCatalog.RelicForgeId:
			{
				var candidates = GameData.GetAllEquipment().ToList();
				if (candidates.Count > 0)
				{
					var relic = candidates[_rng.RandiRange(0, candidates.Count - 1)];
					GameState.Instance.TryGrantEquipment(relic.Id);
				}
				break;
			}
			case EndlessBoonCatalog.BerserkerBloodId:
				_endlessBerserkerBlood = true;
				break;
			case EndlessBoonCatalog.ShieldFormationId:
				_endlessBusArmorScale = 1.2f;
				break;
			case EndlessBoonCatalog.SplitterBaneId:
				_endlessGoldScale *= 1.12f;
				break;
		}
	}

	private float ResolveRouteForkGoldScale()
	{
		return _endlessRouteForkId switch
		{
			EndlessRouteForkCatalog.MainlinePushId => 1.1f,
			EndlessRouteForkCatalog.ScavengeDetourId => 1.2f,
			EndlessRouteForkCatalog.FortifiedBlockId => 0.9f,
			_ => 1f
		};
	}

}
