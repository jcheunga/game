using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController : Node2D
{
	private const string DefaultEndlessContactTradeoffLabel = "No contact tradeoff active.";
	private const float LanRaceTelemetryIntervalSeconds = 1f;
	private const float OnlineRoomTelemetryIntervalSeconds = 1f;
	private const float OnlineRoomMonitorRefreshIntervalSeconds = 2f;
	private const float OnlineRoomEndRefreshIntervalSeconds = 2.5f;

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
		public DeploySlot(UnitDefinition definition, Button button)
		{
			Definition = definition;
			Button = button;
		}

		public UnitDefinition Definition { get; }
		public Button Button { get; }
	}

	private sealed class SpellSlot
	{
		public SpellSlot(SpellDefinition definition, Button button)
		{
			Definition = definition;
			Button = button;
		}

		public SpellDefinition Definition { get; }
		public Button Button { get; }
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

	private sealed class EndlessFieldEvent
	{
		public EndlessFieldEvent(
			string type,
			string label,
			Vector2[] anchors,
			float duration,
			float interval,
			float radius,
			Color color)
		{
			Type = type;
			Label = label;
			Anchors = anchors;
			Remaining = duration;
			PulseTimer = interval;
			Interval = interval;
			Radius = radius;
			Color = color;
		}

		public string Type { get; }
		public string Label { get; }
		public Vector2[] Anchors { get; }
		public float Remaining { get; set; }
		public float PulseTimer { get; set; }
		public float Interval { get; }
		public float Radius { get; }
		public Color Color { get; }
	}

	private sealed class StageHazardState
	{
		public StageHazardState(
			StageHazardDefinition definition,
			Vector2 anchor,
			float nextTriggerTime,
			Color color)
		{
			Definition = definition;
			Anchor = anchor;
			NextTriggerTime = nextTriggerTime;
			Color = color;
		}

		public StageHazardDefinition Definition { get; }
		public Vector2 Anchor { get; }
		public float NextTriggerTime { get; set; }
		public bool WarningIssued { get; set; }
		public Color Color { get; }
	}

	private sealed class StageMissionState
	{
		public StageMissionState(
			StageMissionEventDefinition definition,
			Vector2 anchor,
			Color color)
		{
			Definition = definition;
			Anchor = anchor;
			Color = color;
		}

		public StageMissionEventDefinition Definition { get; }
		public Vector2 Anchor { get; }
		public Color Color { get; }
		public EndlessContactActor Actor { get; set; } = null!;
		public float Progress { get; set; }
		public bool Started { get; set; }
		public bool SupportMomentTriggered { get; set; }
		public bool PlayerInside { get; set; }
		public bool EnemyInside { get; set; }
		public bool Completed { get; set; }
		public bool Failed { get; set; }
	}

	private sealed class EndlessDirectiveState
	{
		public EndlessDirectiveState(
			EndlessDirectiveDefinition definition,
			int startWave,
			int checkpointWave,
			int targetCount,
			float targetRatio,
			int startEnemyDefeats,
			int startDeployments,
			float startBusHullRatio)
		{
			Definition = definition;
			StartWave = startWave;
			CheckpointWave = checkpointWave;
			TargetCount = targetCount;
			TargetRatio = targetRatio;
			StartEnemyDefeats = startEnemyDefeats;
			StartDeployments = startDeployments;
			LowestBusHullRatio = startBusHullRatio;
		}

		public EndlessDirectiveDefinition Definition { get; }
		public int StartWave { get; }
		public int CheckpointWave { get; }
		public int TargetCount { get; }
		public float TargetRatio { get; }
		public int StartEnemyDefeats { get; }
		public int StartDeployments { get; }
		public float LowestBusHullRatio { get; set; }
		public bool Completed { get; set; }
		public bool Failed { get; set; }
		public bool RewardGranted { get; set; }
	}

	private sealed class EndlessContactState
	{
		public EndlessContactState(
			EndlessContactDefinition definition,
			Vector2 anchor,
			Color color)
		{
			Definition = definition;
			Anchor = anchor;
			Color = color;
		}

		public EndlessContactDefinition Definition { get; }
		public Vector2 Anchor { get; }
		public Color Color { get; }
		public float Progress { get; set; }
		public bool PlayerInside { get; set; }
		public bool EnemyInside { get; set; }
		public int PlayerSupportActions { get; set; }
		public int EnemyPressureActions { get; set; }
		public float PlayerSupportRepairTotal { get; set; }
		public float EnemyPressureDamageTotal { get; set; }
		public float PlayerSupportProgressTotal { get; set; }
		public float ResponseTimer { get; set; }
		public int ResponseWavesTriggered { get; set; }
		public int ResponseWaveLimit { get; set; }
		public bool SupportMomentTriggered { get; set; }
		public bool Completed { get; set; }
		public bool Failed { get; set; }
		public bool RewardGranted { get; set; }
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
	private readonly List<StageHazardState> _stageHazards = new();
	private readonly List<StageMissionState> _stageMissions = new();
	private readonly BattleDeckState _deck = new();
	private readonly BattleSpellState _spellDeck = new();
	private readonly List<ChallengeDeploymentRecord> _challengeDeploymentTape = new();
	private readonly List<ChallengeGhostMarker> _challengeGhostMarkers = new();
	private readonly RandomNumberGenerator _rng = new();
	private BattleSpawnDirector _spawnDirector = null!;
	private BattleRunMode _battleMode;

	private Label _baseHealthLabel = null!;
	private Label _resourceLabel = null!;
	private Label _timerLabel = null!;
	private Label _statusLabel = null!;
	private Label _battleBannerLabel = null!;
	private Label _battleSubtitleLabel = null!;
	private Label _battleMissionLabel = null!;
	private Label _waveIntelLabel = null!;
	private Label _objectiveStatusLabel = null!;
	private Label _fpsLabel = null!;
	private Label _endLabel = null!;
	private PanelContainer _endPanel = null!;
	private CenterContainer _endCenter = null!;
	private CenterContainer _draftCenter = null!;
	private PanelContainer _draftPanel = null!;
	private Label _draftLabel = null!;
	private CheckBox _showDevUiToggle = null!;
	private CheckBox _showFpsToggle = null!;
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
	private string _endlessContactTradeoffLabel = DefaultEndlessContactTradeoffLabel;

	private float _playerBaseHealth;
	private float _playerBaseMaxHealth;
	private float _enemyBaseHealth;
	private float _enemyBaseMaxHealth;
	private float _courage;
	private float _maxCourage;
	private float _courageGainPerSecond;
	private float _elapsed;
	private int _playerDeployments;
	private int _enemyDefeats;
	private int _playerHazardHits;
	private float _playerSignalJamSeconds;
	private float _challengeMutatorNextJamTimer;
	private float _playerBaseFlashTimer;
	private float _enemyBaseFlashTimer;
	private float _enemySignalJamTimer;
	private float _enemySignalJamCourageGainScale = 1f;
	private bool _battleEnded;
	private bool _endlessCheckpointActive;
	private float _endlessContactCourageGainScale = 1f;
	private float _endlessUnitHealthScale = 1f;
	private float _endlessUnitDamageScale = 1f;
	private float _endlessScrapScale = 1f;
	private int _endlessDirectiveScrapBonus;
	private int _endlessDirectiveFuelBonus;
	private int _endlessContactScrapBonus;
	private int _endlessContactFuelBonus;
	private int _endlessBossScrapBonus;
	private int _endlessBossFuelBonus;
	private int _lastEndlessBossCheckpointWave;
	private string _lastEndlessBossCheckpointTitle = "";
	private string[] _draftOptionIds = Array.Empty<string>();
	private bool _draftingRouteFork;
	private BattleSelectionMode _selectionMode = BattleSelectionMode.Unit;
	private EndlessFieldEvent _activeEndlessFieldEvent = null!;
	private EndlessDirectiveState _activeEndlessDirective = null!;
	private EndlessContactState _activeEndlessContact = null!;
	private EndlessContactActor _activeEndlessContactActor = null!;
	private ChallengeRunRecord _challengeGhostRun = null!;
	private int _challengeGhostNextIndex;
	private float _lanRaceTelemetryTimer;
	private float _onlineRoomTelemetryTimer;
	private float _onlineRoomMonitorRefreshTimer;
	private float _onlineRoomEndRefreshTimer;
	private bool _lanStartBarrierActive;
	private bool _onlineRoomStartBarrierActive;
	private float _onlineRoomStartCountdownRemaining;
	private string _onlineRoomRaceSummary = "";
	private string _lanChallengeEndBaseText = "";

	private bool IsEndlessMode => _battleMode == BattleRunMode.Endless;
	private bool IsChallengeMode => _battleMode == BattleRunMode.AsyncChallenge;
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
		_onlineRoomRaceSummary = "";
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
		else
		{
			_stage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
			_stageData = GameState.Instance.BuildConfiguredCampaignStage(_stage);
			_activeRouteId = NormalizeRouteId(_stageData.MapId);
			_playerBaseMaxHealth = _stageData.PlayerBaseHealth * StageModifiers.ResolvePlayerBaseHealthScale(_stageData);
			_enemyBaseMaxHealth = _stageData.EnemyBaseHealth * StageModifiers.ResolveEnemyBaseHealthScale(_stageData);
		}

		_playerBaseMaxHealth = GameState.Instance.ApplyPlayerBaseHealthUpgrade(_playerBaseMaxHealth);
		_playerBaseHealth = _playerBaseMaxHealth;
		_enemyBaseHealth = _enemyBaseMaxHealth;

		var baseCourageMax = _combat.CourageMax + (IsChallengeMode ? _challengeMutator.CourageMaxBonus : 0f);
		_maxCourage = GameState.Instance.ApplyPlayerCourageMaxUpgrade(baseCourageMax);
		_courage = Mathf.Min(_maxCourage, Mathf.Max(0f, _combat.CourageStart + (IsChallengeMode ? _challengeMutator.CourageMaxBonus * 0.35f : 0f)));
		_courageGainPerSecond = GameState.Instance.ApplyPlayerCourageGainUpgrade(
			_combat.CourageGainPerSecond *
			StageModifiers.ResolveCourageGainScale(_stageData) *
			(IsChallengeMode ? _challengeMutator.CourageGainScale : 1f));

		if (IsEndlessMode)
		{
			ApplyEndlessBoon();
		}

		_playerDeployments = 0;
		_enemyDefeats = 0;
		_playerHazardHits = 0;
		_playerSignalJamSeconds = 0f;
		_endlessCheckpointActive = false;
		_endlessContactTradeoffLabel = DefaultEndlessContactTradeoffLabel;
		_endlessContactCourageGainScale = 1f;
		_endlessUnitHealthScale = 1f;
		_endlessUnitDamageScale = 1f;
		_endlessScrapScale = 1f;
		_endlessDirectiveScrapBonus = 0;
		_endlessDirectiveFuelBonus = 0;
		_endlessContactScrapBonus = 0;
		_endlessContactFuelBonus = 0;
		_endlessBossScrapBonus = 0;
		_endlessBossFuelBonus = 0;
		_lastEndlessBossCheckpointWave = 0;
		_lastEndlessBossCheckpointTitle = "";
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
		_activeEndlessFieldEvent = null;
		_activeEndlessDirective = null;
		_activeEndlessContact = null;
		_activeEndlessContactActor = null;
		_challengeGhostRun = IsChallengeMode
			? GameState.Instance.GetChallengeGhostRun(_challengeDefinition.Code, GameState.Instance.HasSelectedAsyncChallengeLockedDeck)
			: null;
		if (IsOnlineRoomMode)
		{
			var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
			var roomTitle = OnlineRoomJoinService.GetCachedTicket()?.RoomTitle;
			_onlineRoomRaceSummary = string.IsNullOrWhiteSpace(roomTitle)
				? $"Online room board {_challengeDefinition.Code}"
				: $"Online room: {roomTitle}";
			if (roomSnapshot?.HasRoom == true && roomSnapshot.RaceCountdownActive && roomSnapshot.RaceCountdownRemainingSeconds > 0.05f)
			{
				_onlineRoomStartBarrierActive = true;
				_onlineRoomStartCountdownRemaining = roomSnapshot.RaceCountdownRemainingSeconds;
			}
		}

		_deck.Initialize(GameState.Instance.GetBattleDeckUnits());
		_spellDeck.Initialize(GameState.Instance.GetBattleDeckSpells());
		_selectionMode = BattleSelectionMode.Unit;
		if (IsEndlessMode)
		{
			_spawnDirector.InitializeEndless(_activeRouteId, _stageData, _combat, GameData.GetEnemyUnits());
			StartRouteForkFieldEvent(_endlessRouteForkId);
			StartEndlessDirectiveSegment();
			StartEndlessContactEvent();
		}
		else
		{
			_spawnDirector.Initialize(_stage, _stageData, _combat, GameData.GetEnemyUnits());
			if (IsChallengeMode)
			{
				_spawnDirector.SetEnemyScaleModifiers(_challengeMutator.EnemyHealthScale, _challengeMutator.EnemyDamageScale);
			}
		}

		InitializeStageHazards();
		InitializeStageMissions();

		BuildUi();
		SetStatus(
			IsEndlessMode
				? $"Select a squad or spell card, click the battlefield, and hold against escalating waves. {GameState.Instance.BuildBattleDeckSynergyInlineSummary()} {GameState.Instance.BuildSpellSummary(GameState.Instance.GetBattleDeckSpells())}"
				: IsChallengeMode
					? $"Challenge {_challengeDefinition.Code}: deploy from the war wagon, cast support cards when needed, and post the best score you can. {GameState.Instance.BuildBattleDeckSynergyInlineSummary()} {GameState.Instance.BuildSpellSummary(GameState.Instance.GetBattleDeckSpells())} {(HasChallengeGhostRun() ? "Local ghost benchmark armed." : "No local ghost benchmark saved yet.")}"
				: $"Select a squad or spell card, then click the battlefield. {GameState.Instance.BuildBattleDeckSynergyInlineSummary()} {GameState.Instance.BuildSpellSummary(GameState.Instance.GetBattleDeckSpells())}");
		UpdateHud();
		if (IsLanRaceMode)
		{
			if (LanChallengeService.Instance != null)
			{
				LanChallengeService.Instance.StateChanged += OnLanRaceStateChanged;
			}

			LanChallengeService.Instance?.ReportLocalBattleLoaded();
		}
	}

	public override void _ExitTree()
	{
		if (IsLanRaceMode && LanChallengeService.Instance != null)
		{
			LanChallengeService.Instance.StateChanged -= OnLanRaceStateChanged;
		}
	}

	public override void _Draw()
	{
		var palette = ResolveTerrainPalette();
		var route = RouteCatalog.Get(_activeRouteId);

		DrawRect(new Rect2(0f, 0f, 1280f, 720f), palette.SkyColor, true);

		DrawRect(
			new Rect2(
				BattlefieldLeft,
				BattlefieldTop,
				BattlefieldRight - BattlefieldLeft,
				BattlefieldBottom - BattlefieldTop),
			palette.GroundColor,
			true);

		const int stripeCount = 6;
		for (var i = 0; i <= stripeCount; i++)
		{
			var t = i / (float)stripeCount;
			var y = Mathf.Lerp(BattlefieldTop, BattlefieldBottom, t);
			DrawLine(
				new Vector2(BattlefieldLeft, y),
				new Vector2(BattlefieldRight, y),
				palette.AccentColor,
				1.2f,
				true);
		}

		DrawTerrainDecoration();
		DrawStageHazards();
		DrawEndlessFieldEvent();
		DrawEndlessContactEvent();
		DrawChallengeGhostMarkers();

		DrawPlayerBus(palette, route);
		DrawEnemyBarricade(palette, route);
	}

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

	private void DrawTerrainDecoration()
	{
		var terrainId = (_stageData?.TerrainId ?? "urban").ToLowerInvariant();
		switch (terrainId)
		{
			case "highway":
				for (var i = 0; i < 14; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 42f, BattlefieldRight - 42f, i / 13f);
					DrawRect(new Rect2(x - 8f, BaseCenterY - 3f, 16f, 6f), new Color(1f, 0.92f, 0.52f, 0.4f), true);
				}
				break;
			case "night":
				DrawCircle(new Vector2(BattlefieldLeft + 180f, BattlefieldTop + 54f), 38f, new Color(0.4f, 0.7f, 1f, 0.09f));
				DrawCircle(new Vector2(BattlefieldRight - 220f, BattlefieldBottom - 62f), 46f, new Color(1f, 0.4f, 0.7f, 0.08f));
				break;
			case "industrial":
				DrawRect(new Rect2(BattlefieldLeft + 170f, BattlefieldTop + 30f, 84f, 24f), new Color(0f, 0f, 0f, 0.25f), true);
				DrawRect(new Rect2(BattlefieldRight - 294f, BattlefieldBottom - 60f, 84f, 24f), new Color(0f, 0f, 0f, 0.25f), true);
				DrawRect(new Rect2(BattlefieldRight - 198f, BattlefieldBottom - 60f, 84f, 24f), new Color(0f, 0f, 0f, 0.2f), true);
				break;
			case "swamp":
				DrawCircle(new Vector2(BattlefieldLeft + 270f, BattlefieldBottom - 58f), 30f, new Color(0.13f, 0.25f, 0.17f, 0.45f));
				DrawCircle(new Vector2(BattlefieldRight - 290f, BattlefieldTop + 62f), 24f, new Color(0.13f, 0.25f, 0.17f, 0.45f));
				break;
			case "shipyard":
				DrawRect(new Rect2(BattlefieldLeft, BattlefieldBottom - 46f, BattlefieldRight - BattlefieldLeft, 22f), new Color(0.2f, 0.35f, 0.5f, 0.45f), true);
				for (var i = 0; i < 7; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 110f, BattlefieldRight - 110f, i / 6f);
					DrawLine(
						new Vector2(x - 20f, BattlefieldTop + 36f),
						new Vector2(x + 20f, BattlefieldTop + 72f),
						new Color(1f, 1f, 1f, 0.12f),
						2f,
						true);
				}
				break;
			case "railyard":
				for (var i = 0; i < 3; i++)
				{
					var y = BaseCenterY - 56f + (i * 42f);
					DrawLine(
						new Vector2(BattlefieldLeft + 48f, y),
						new Vector2(BattlefieldRight - 48f, y),
						new Color(0.95f, 0.72f, 0.42f, 0.16f),
						2f,
						true);

					for (var j = 0; j < 11; j++)
					{
						var x = Mathf.Lerp(BattlefieldLeft + 86f, BattlefieldRight - 86f, j / 10f);
						DrawRect(new Rect2(x - 6f, y - 7f, 12f, 14f), new Color(0f, 0f, 0f, 0.24f), true);
					}
				}
				break;
			case "smelter":
				DrawRect(new Rect2(BattlefieldLeft + 120f, BattlefieldBottom - 58f, 180f, 18f), new Color(1f, 0.42f, 0.1f, 0.34f), true);
				DrawRect(new Rect2(BattlefieldRight - 310f, BattlefieldTop + 42f, 168f, 16f), new Color(1f, 0.54f, 0.18f, 0.28f), true);
				DrawCircle(new Vector2(BattlefieldLeft + 210f, BattlefieldTop + 62f), 26f, new Color(1f, 0.4f, 0.15f, 0.1f));
				DrawCircle(new Vector2(BattlefieldRight - 196f, BattlefieldBottom - 68f), 34f, new Color(1f, 0.55f, 0.2f, 0.1f));
				break;
			case "foundry":
				for (var i = 0; i < 5; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 104f, BattlefieldRight - 104f, i / 4f);
					DrawRect(new Rect2(x - 12f, BattlefieldTop + 26f, 24f, 92f), new Color(0f, 0f, 0f, 0.18f), true);
					DrawRect(new Rect2(x - 26f, BattlefieldTop + 112f, 52f, 12f), new Color(0.95f, 0.58f, 0.2f, 0.16f), true);
				}

				DrawRect(new Rect2(BattlefieldLeft + 160f, BattlefieldBottom - 52f, BattlefieldRight - BattlefieldLeft - 320f, 20f), new Color(1f, 0.48f, 0.14f, 0.18f), true);
				break;
			case "checkpoint":
				for (var i = 0; i < 7; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 84f, BattlefieldRight - 84f, i / 6f);
					DrawRect(new Rect2(x - 42f, BattlefieldBottom - 52f, 84f, 14f), new Color(1f, 0.95f, 0.58f, 0.16f), true);
					DrawRect(new Rect2(x - 12f, BattlefieldTop + 42f + ((i % 2) * 18f), 24f, 52f), new Color(0.85f, 1f, 0.85f, 0.08f), true);
				}
				break;
			case "decon":
				for (var i = 0; i < 3; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 180f, BattlefieldRight - 180f, i / 2f);
					DrawArc(
						new Vector2(x, BaseCenterY - 8f),
						54f,
						Mathf.Pi,
						Mathf.Tau,
						20,
						new Color(0.76f, 1f, 0.84f, 0.18f),
						4f);
					DrawLine(
						new Vector2(x - 54f, BaseCenterY - 8f),
						new Vector2(x - 54f, BattlefieldBottom - 42f),
						new Color(0.82f, 1f, 0.86f, 0.12f),
						4f,
						true);
					DrawLine(
						new Vector2(x + 54f, BaseCenterY - 8f),
						new Vector2(x + 54f, BattlefieldBottom - 42f),
						new Color(0.82f, 1f, 0.86f, 0.12f),
						4f,
						true);
				}
				break;
			case "lab":
				for (var i = 0; i < 5; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 110f, BattlefieldRight - 110f, i / 4f);
					DrawRect(new Rect2(x - 28f, BattlefieldTop + 28f, 56f, 28f), new Color(0.8f, 1f, 0.94f, 0.1f), true);
					DrawRect(new Rect2(x - 18f, BattlefieldTop + 62f, 36f, 8f), new Color(0.9f, 1f, 0.72f, 0.12f), true);
				}
				DrawLine(
					new Vector2(BattlefieldLeft + 82f, BattlefieldBottom - 52f),
					new Vector2(BattlefieldRight - 82f, BattlefieldBottom - 52f),
					new Color(0.84f, 1f, 0.74f, 0.16f),
					3f,
					true);
				break;
			case "blacksite":
				for (var i = 0; i < 6; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 92f, BattlefieldRight - 92f, i / 5f);
					DrawRect(new Rect2(x - 10f, BattlefieldTop + 18f, 20f, 118f), new Color(0f, 0f, 0f, 0.2f), true);
					DrawRect(new Rect2(x - 34f, BattlefieldTop + 132f, 68f, 10f), new Color(0.94f, 0.98f, 0.62f, 0.12f), true);
				}

				for (var i = 0; i < 9; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 54f, BattlefieldRight - 54f, i / 8f);
					DrawLine(
						new Vector2(x - 18f, BattlefieldBottom - 64f),
						new Vector2(x + 6f, BattlefieldBottom - 44f),
						new Color(1f, 0.94f, 0.58f, 0.22f),
						4f,
						true);
				}
				break;
			case "pass":
					for (var i = 0; i < 4; i++)
					{
						var left = Mathf.Lerp(BattlefieldLeft - 22f, BattlefieldRight - 220f, i / 3f);
						var peak = left + 96f + ((i % 2) * 18f);
						var right = left + 198f;
						DrawColoredPolygon(
							new[]
							{
								new Vector2(left, BattlefieldBottom - 36f),
								new Vector2(peak, BattlefieldTop + 56f + ((i % 2) * 18f)),
								new Vector2(right, BattlefieldBottom - 36f)
							},
							new Color(0.92f, 0.96f, 1f, 0.08f));
					}

					for (var i = 0; i < 9; i++)
					{
						var x = Mathf.Lerp(BattlefieldLeft + 72f, BattlefieldRight - 72f, i / 8f);
						DrawLine(
							new Vector2(x - 12f, BattlefieldTop + 36f + ((i % 3) * 12f)),
							new Vector2(x + 8f, BattlefieldTop + 60f + ((i % 3) * 12f)),
							new Color(1f, 1f, 1f, 0.16f),
							2f,
							true);
					}
					break;
				case "shrine":
					for (var i = 0; i < 3; i++)
					{
						var x = Mathf.Lerp(BattlefieldLeft + 160f, BattlefieldRight - 160f, i / 2f);
						DrawCircle(new Vector2(x, BaseCenterY - 12f), 34f, new Color(0.94f, 0.97f, 0.86f, 0.08f));
						DrawRect(new Rect2(x - 8f, BaseCenterY - 52f, 16f, 80f), new Color(1f, 1f, 1f, 0.08f), true);
						DrawRect(new Rect2(x - 28f, BaseCenterY + 22f, 56f, 10f), new Color(0.92f, 0.95f, 0.82f, 0.1f), true);
					}
					break;
				case "watchfort":
					for (var i = 0; i < 4; i++)
					{
						var x = Mathf.Lerp(BattlefieldLeft + 110f, BattlefieldRight - 110f, i / 3f);
						DrawRect(new Rect2(x - 30f, BattlefieldTop + 28f, 60f, 92f), new Color(0f, 0f, 0f, 0.16f), true);
						for (var j = 0; j < 3; j++)
						{
							DrawRect(new Rect2(x - 30f + (j * 20f), BattlefieldTop + 22f, 14f, 10f), new Color(0.94f, 0.98f, 1f, 0.12f), true);
						}
					}

					DrawLine(
						new Vector2(BattlefieldLeft + 62f, BattlefieldBottom - 58f),
						new Vector2(BattlefieldRight - 62f, BattlefieldBottom - 58f),
						new Color(0.88f, 0.93f, 1f, 0.18f),
						4f,
						true);
					break;
			case "cathedral":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 120f, BattlefieldRight - 120f, i / 3f);
					DrawArc(
						new Vector2(x, BattlefieldTop + 132f + ((i % 2) * 10f)),
						44f,
						Mathf.Pi,
						Mathf.Tau,
						20,
						new Color(1f, 0.96f, 0.82f, 0.12f),
						4f);
					DrawLine(
						new Vector2(x - 44f, BattlefieldTop + 132f + ((i % 2) * 10f)),
						new Vector2(x - 44f, BattlefieldBottom - 46f),
						new Color(1f, 0.96f, 0.86f, 0.08f),
						4f,
						true);
					DrawLine(
						new Vector2(x + 44f, BattlefieldTop + 132f + ((i % 2) * 10f)),
						new Vector2(x + 44f, BattlefieldBottom - 46f),
						new Color(1f, 0.96f, 0.86f, 0.08f),
						4f,
						true);
				}

				for (var i = 0; i < 8; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 72f, BattlefieldRight - 72f, i / 7f);
					DrawCircle(new Vector2(x, BattlefieldBottom - 54f - ((i % 2) * 6f)), 3f, new Color(1f, 0.84f, 0.46f, 0.65f));
				}
				break;
			case "ossuary":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 132f, BattlefieldRight - 132f, i / 3f);
					DrawCircle(new Vector2(x - 18f, BattlefieldBottom - 54f), 18f, new Color(1f, 0.98f, 0.94f, 0.08f));
					DrawCircle(new Vector2(x + 14f, BattlefieldBottom - 48f), 22f, new Color(0.88f, 1f, 0.9f, 0.08f));
					DrawRect(new Rect2(x - 8f, BattlefieldTop + 34f, 16f, 86f), new Color(0f, 0f, 0f, 0.14f), true);
				}
				break;
			case "reliquary":
				for (var i = 0; i < 3; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 180f, BattlefieldRight - 180f, i / 2f);
					DrawRect(new Rect2(x - 30f, BattlefieldTop + 32f, 60f, 90f), new Color(1f, 0.95f, 0.74f, 0.08f), true);
					DrawRect(new Rect2(x - 10f, BattlefieldTop + 18f, 20f, 118f), new Color(0f, 0f, 0f, 0.16f), true);
					DrawLine(
						new Vector2(x, BattlefieldTop + 18f),
						new Vector2(x, BattlefieldBottom - 42f),
						new Color(1f, 0.9f, 0.56f, 0.12f),
						3f,
						true);
				}
				break;
			case "marsh":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 120f, BattlefieldRight - 120f, i / 3f);
					DrawCircle(new Vector2(x, BattlefieldBottom - 52f + ((i % 2) * 10f)), 34f, new Color(0.56f, 0.75f, 0.43f, 0.14f));
					DrawCircle(new Vector2(x - 18f, BattlefieldBottom - 44f + ((i % 2) * 10f)), 18f, new Color(0.3f, 0.44f, 0.2f, 0.18f));
				}

				for (var i = 0; i < 7; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 84f, BattlefieldRight - 84f, i / 6f);
					DrawRect(new Rect2(x - 4f, BattlefieldTop + 34f + ((i % 2) * 14f), 8f, 76f), new Color(0.88f, 0.96f, 0.82f, 0.08f), true);
				}
				break;
			case "chapel":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 128f, BattlefieldRight - 128f, i / 3f);
					DrawArc(
						new Vector2(x, BattlefieldTop + 128f),
						38f,
						Mathf.Pi,
						Mathf.Tau,
						18,
						new Color(1f, 0.96f, 0.82f, 0.12f),
						4f);
					DrawRect(new Rect2(x - 10f, BattlefieldTop + 34f, 20f, 94f), new Color(0f, 0f, 0f, 0.14f), true);
				}
				break;
			case "ferry":
				DrawRect(new Rect2(BattlefieldLeft, BattlefieldBottom - 54f, BattlefieldRight - BattlefieldLeft, 26f), new Color(0.22f, 0.4f, 0.36f, 0.32f), true);
				for (var i = 0; i < 6; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 96f, BattlefieldRight - 96f, i / 5f);
					DrawRect(new Rect2(x - 34f, BattlefieldTop + 30f + ((i % 2) * 12f), 68f, 12f), new Color(0f, 0f, 0f, 0.16f), true);
					DrawLine(
						new Vector2(x, BattlefieldTop + 42f + ((i % 2) * 12f)),
						new Vector2(x, BattlefieldBottom - 58f),
						new Color(0.86f, 0.96f, 0.9f, 0.1f),
						3f,
						true);
				}
				break;
			case "grassland":
				for (var i = 0; i < 8; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 64f, BattlefieldRight - 64f, i / 7f);
					DrawLine(
						new Vector2(x - 16f, BattlefieldBottom - 46f),
						new Vector2(x + 12f, BattlefieldBottom - 64f),
						new Color(1f, 0.86f, 0.48f, 0.16f),
						3f,
						true);
				}
				break;
			case "waystation":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 140f, BattlefieldRight - 140f, i / 3f);
					DrawRect(new Rect2(x - 24f, BattlefieldTop + 38f, 48f, 68f), new Color(0f, 0f, 0f, 0.16f), true);
					DrawLine(
						new Vector2(x - 26f, BattlefieldTop + 38f),
						new Vector2(x, BattlefieldTop + 18f),
						new Color(1f, 0.92f, 0.74f, 0.1f),
						4f,
						true);
					DrawLine(
						new Vector2(x + 26f, BattlefieldTop + 38f),
						new Vector2(x, BattlefieldTop + 18f),
						new Color(1f, 0.92f, 0.74f, 0.1f),
						4f,
						true);
				}
				break;
			case "siegecamp":
				for (var i = 0; i < 5; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 110f, BattlefieldRight - 110f, i / 4f);
					DrawRect(new Rect2(x - 18f, BattlefieldTop + 54f + ((i % 2) * 10f), 36f, 56f), new Color(0f, 0f, 0f, 0.16f), true);
					DrawLine(
						new Vector2(x - 22f, BattlefieldTop + 54f + ((i % 2) * 10f)),
						new Vector2(x, BattlefieldTop + 30f + ((i % 2) * 10f)),
						new Color(1f, 0.82f, 0.54f, 0.12f),
						4f,
						true);
					DrawLine(
						new Vector2(x + 22f, BattlefieldTop + 54f + ((i % 2) * 10f)),
						new Vector2(x, BattlefieldTop + 30f + ((i % 2) * 10f)),
						new Color(1f, 0.82f, 0.54f, 0.12f),
						4f,
						true);
				}
				break;
			case "grove":
				for (var i = 0; i < 6; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 88f, BattlefieldRight - 88f, i / 5f);
					DrawLine(
						new Vector2(x, BattlefieldBottom - 42f),
						new Vector2(x - 12f, BattlefieldTop + 78f + ((i % 2) * 14f)),
						new Color(0.72f, 0.54f, 0.4f, 0.2f),
						8f,
						true);
					DrawCircle(new Vector2(x - 18f, BattlefieldTop + 68f + ((i % 2) * 14f)), 20f, new Color(0.9f, 0.76f, 0.62f, 0.08f));
				}
				break;
			case "witchcircle":
				for (var i = 0; i < 3; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 180f, BattlefieldRight - 180f, i / 2f);
					DrawCircle(new Vector2(x, BaseCenterY - 14f), 38f, new Color(1f, 0.92f, 0.78f, 0.08f));
					for (var j = 0; j < 6; j++)
					{
						var angle = (Mathf.Tau / 6f) * j;
						DrawCircle(
							new Vector2(x, BaseCenterY - 14f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 32f,
							4f,
							new Color(0.96f, 0.84f, 0.62f, 0.5f));
					}
				}
				break;
			case "timberroad":
				for (var i = 0; i < 5; i++)
				{
					var y = BaseCenterY - 62f + (i * 30f);
					DrawLine(
						new Vector2(BattlefieldLeft + 52f, y),
						new Vector2(BattlefieldRight - 52f, y),
						new Color(0.76f, 0.46f, 0.2f, 0.14f),
						4f,
						true);
				}
				break;
			case "bridgefort":
				for (var i = 0; i < 5; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 94f, BattlefieldRight - 94f, i / 4f);
					DrawRect(new Rect2(x - 22f, BattlefieldTop + 34f, 44f, 82f), new Color(0f, 0f, 0f, 0.18f), true);
					for (var j = 0; j < 2; j++)
					{
						DrawRect(new Rect2(x - 22f + (j * 22f), BattlefieldTop + 28f, 14f, 10f), new Color(0.98f, 0.94f, 1f, 0.14f), true);
					}
				}

				DrawLine(
					new Vector2(BattlefieldLeft + 52f, BattlefieldBottom - 62f),
					new Vector2(BattlefieldRight - 52f, BattlefieldBottom - 62f),
					new Color(0.9f, 0.84f, 1f, 0.18f),
					6f,
					true);
				break;
			case "breachyard":
				for (var i = 0; i < 4; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 130f, BattlefieldRight - 130f, i / 3f);
					DrawRect(new Rect2(x - 32f, BattlefieldBottom - 66f - ((i % 2) * 8f), 64f, 18f), new Color(0.8f, 0.72f, 0.9f, 0.12f), true);
					DrawLine(
						new Vector2(x - 34f, BattlefieldBottom - 50f - ((i % 2) * 8f)),
						new Vector2(x + 18f, BattlefieldBottom - 86f - ((i % 2) * 8f)),
						new Color(0f, 0f, 0f, 0.16f),
						5f,
						true);
				}
				break;
			case "innerkeep":
				for (var i = 0; i < 3; i++)
				{
					var x = Mathf.Lerp(BattlefieldLeft + 180f, BattlefieldRight - 180f, i / 2f);
					DrawArc(
						new Vector2(x, BattlefieldTop + 130f),
						42f,
						Mathf.Pi,
						Mathf.Tau,
						20,
						new Color(0.98f, 0.94f, 1f, 0.12f),
						4f);
					DrawRect(new Rect2(x - 8f, BattlefieldTop + 34f, 16f, 96f), new Color(0f, 0f, 0f, 0.16f), true);
				}
				break;
		}
	}

	private void DrawPlayerBus(TerrainPalette palette, RouteDefinition route)
	{
		var healthRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);
		var offset = GetBaseShakeOffset(true);
		var bodyColor = ResolveBaseBodyColor(palette.PlayerBaseColor, _playerBaseFlashTimer, healthRatio);
		var cabinColor = ResolveBaseCoreColor(palette.PlayerCoreColor, _playerBaseFlashTimer, healthRatio);
		var trimColor = route.BannerAccent;
		var trimShadow = route.BannerPanel.Lightened(0.12f);
		var bodyRect = new Rect2(PlayerBaseX - 46f, BaseCenterY - 34f, 122f, 58f);
		var cabinRect = new Rect2(PlayerBaseX + 44f, BaseCenterY - 24f, 34f, 34f);
		var bumperRect = new Rect2(PlayerBaseX - 58f, BaseCenterY + 10f, 16f, 12f);

		DrawRect(OffsetRect(bodyRect, offset), bodyColor, true);
		DrawRect(OffsetRect(cabinRect, offset), cabinColor, true);
		DrawRect(OffsetRect(bumperRect, offset), cabinColor.Darkened(0.2f), true);
		DrawRect(
			OffsetRect(new Rect2(PlayerBaseX - 34f, BaseCenterY - 44f, 66f, 10f), offset),
			bodyColor.Darkened(0.18f),
			true);
		DrawLine(
			new Vector2(PlayerBaseX - 20f, BaseCenterY - 34f) + offset,
			new Vector2(PlayerBaseX - 20f, BaseCenterY - 8f) + offset,
			trimShadow,
			3f,
			true);
		DrawLine(
			new Vector2(PlayerBaseX + 18f, BaseCenterY - 34f) + offset,
			new Vector2(PlayerBaseX + 18f, BaseCenterY - 8f) + offset,
			trimShadow,
			3f,
			true);
		DrawColoredPolygon(
			new[]
			{
				new Vector2(PlayerBaseX - 30f, BaseCenterY - 34f) + offset,
				new Vector2(PlayerBaseX - 4f, BaseCenterY - 62f) + offset,
				new Vector2(PlayerBaseX + 30f, BaseCenterY - 34f) + offset
			},
			bodyColor.Lightened(0.18f));
		DrawLine(
			new Vector2(PlayerBaseX + 10f, BaseCenterY - 62f) + offset,
			new Vector2(PlayerBaseX + 10f, BaseCenterY - 108f) + offset,
			trimShadow,
			4f,
			true);
		DrawColoredPolygon(
			new[]
			{
				new Vector2(PlayerBaseX + 10f, BaseCenterY - 108f) + offset,
				new Vector2(PlayerBaseX + 46f, BaseCenterY - 100f) + offset,
				new Vector2(PlayerBaseX + 24f, BaseCenterY - 88f) + offset,
				new Vector2(PlayerBaseX + 46f, BaseCenterY - 74f) + offset,
				new Vector2(PlayerBaseX + 10f, BaseCenterY - 80f) + offset
			},
			trimColor);
		DrawLine(
			new Vector2(PlayerBaseX - 42f, BaseCenterY + 10f) + offset,
			new Vector2(PlayerBaseX + 62f, BaseCenterY + 10f) + offset,
			bodyColor.Darkened(0.24f),
			4f,
			true);
		DrawRect(
			OffsetRect(new Rect2(PlayerBaseX - 2f, BaseCenterY - 14f, 18f, 20f), offset),
			trimColor.Darkened(0.22f),
			true);
		DrawRect(
			OffsetRect(new Rect2(PlayerBaseX - 26f, BaseCenterY - 20f, 44f, 18f), offset),
			new Color(1f, 1f, 1f, 0.18f + ((_playerBaseFlashTimer > 0f) ? 0.12f : 0f)),
			true);
		DrawCircle(new Vector2(PlayerBaseX - 12f, BaseCenterY + 28f) + offset, 12f, new Color("1f2933"));
		DrawCircle(new Vector2(PlayerBaseX + 42f, BaseCenterY + 28f) + offset, 12f, new Color("1f2933"));

		var warningLight = healthRatio < 0.45f && Mathf.Sin(_elapsed * 10f) > 0f
			? new Color(1f, 0.34f, 0.28f, 0.95f)
			: new Color(1f, 0.96f, 0.62f, 0.95f);
		DrawCircle(new Vector2(PlayerBaseX + 72f, BaseCenterY - 8f) + offset, 4f, warningLight);

		DrawDamageSmoke(new Vector2(PlayerBaseX - 18f, BaseCenterY - 48f) + offset, healthRatio, bodyColor);
		DrawBaseHealthMeter(
			new Vector2(PlayerBaseX + 8f, BaseCenterY - 58f) + offset,
			132f,
			healthRatio,
			bodyColor.Lightened(0.35f),
			new Color("80ed99"));
	}

	private void DrawEnemyBarricade(TerrainPalette palette, RouteDefinition route)
	{
		var healthRatio = Mathf.Clamp(_enemyBaseHealth / Mathf.Max(1f, _enemyBaseMaxHealth), 0f, 1f);
		var offset = GetBaseShakeOffset(false);
		var wallColor = ResolveBaseBodyColor(palette.EnemyBaseColor, _enemyBaseFlashTimer, healthRatio);
		var coreColor = ResolveBaseCoreColor(palette.EnemyCoreColor, _enemyBaseFlashTimer, healthRatio);
		var bannerColor = coreColor.Lerp(route.BannerAccent, 0.28f);
		var wallBaseX = EnemyBaseX - 52f;
		DrawRect(OffsetRect(new Rect2(wallBaseX, BaseCenterY - 54f, 76f, 112f), offset), wallColor, true);
		DrawRect(OffsetRect(new Rect2(wallBaseX - 18f, BaseCenterY - 12f, 18f, 72f), offset), coreColor.Darkened(0.1f), true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 76f, BaseCenterY - 36f, 18f, 94f), offset), coreColor.Darkened(0.15f), true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 12f, BaseCenterY - 72f, 22f, 18f), offset), coreColor, true);
		DrawRect(OffsetRect(new Rect2(wallBaseX + 40f, BaseCenterY - 84f, 22f, 30f), offset), coreColor, true);
		for (var i = 0; i < 4; i++)
		{
			DrawRect(
				OffsetRect(new Rect2(wallBaseX + 4f + (i * 18f), BaseCenterY - 68f - ((i % 2) * 4f), 12f, 14f), offset),
				coreColor.Darkened(0.04f),
				true);
		}

		var gateRect = new Rect2(wallBaseX + 20f, BaseCenterY - 4f, 24f, 54f);
		DrawRect(OffsetRect(gateRect, offset), wallColor.Darkened(0.28f), true);
		for (var i = 0; i < 4; i++)
		{
			var x = gateRect.Position.X + 4f + (i * 5f);
			DrawLine(
				new Vector2(x, gateRect.Position.Y + 4f) + offset,
				new Vector2(x, gateRect.End.Y - 2f) + offset,
				coreColor.Lightened(0.16f),
				2f,
				true);
		}

		DrawLine(
			new Vector2(wallBaseX + 52f, BaseCenterY - 84f) + offset,
			new Vector2(wallBaseX + 52f, BaseCenterY - 122f) + offset,
			coreColor.Darkened(0.08f),
			4f,
			true);
		DrawColoredPolygon(
			new[]
			{
				new Vector2(wallBaseX + 52f, BaseCenterY - 122f) + offset,
				new Vector2(wallBaseX + 16f, BaseCenterY - 114f) + offset,
				new Vector2(wallBaseX + 38f, BaseCenterY - 102f) + offset,
				new Vector2(wallBaseX + 18f, BaseCenterY - 86f) + offset,
				new Vector2(wallBaseX + 52f, BaseCenterY - 92f) + offset
			},
			bannerColor);
		DrawCircle(new Vector2(EnemyBaseX - 10f, BaseCenterY - 26f) + offset, 7f, new Color(1f, 0.45f, 0.2f, 0.9f));

		if (healthRatio < 0.75f)
		{
			DrawLine(
				new Vector2(wallBaseX + 16f, BaseCenterY - 22f) + offset,
				new Vector2(wallBaseX + 42f, BaseCenterY + 16f) + offset,
				coreColor.Lightened(0.25f),
				3f,
				true);
		}

		if (healthRatio < 0.45f)
		{
			DrawLine(
				new Vector2(wallBaseX + 48f, BaseCenterY - 44f) + offset,
				new Vector2(wallBaseX + 18f, BaseCenterY + 6f) + offset,
				new Color(1f, 0.68f, 0.38f, 0.8f),
				4f,
				true);
		}

		DrawDamageSmoke(new Vector2(EnemyBaseX - 14f, BaseCenterY - 88f) + offset, healthRatio, wallColor);
		DrawBaseHealthMeter(
			new Vector2(EnemyBaseX - 10f, BaseCenterY - 112f) + offset,
			124f,
			healthRatio,
			wallColor.Lightened(0.25f),
			new Color("ff8fab"));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_battleEnded || _endlessCheckpointActive)
		{
			return;
		}

		var deltaF = (float)delta;
		if (HandleLanStartBarrier(deltaF))
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
			}
		}

		_courage += (_courageGainPerSecond * _endlessContactCourageGainScale * _enemySignalJamCourageGainScale) * deltaF;
		if (_courage > _maxCourage)
		{
			_courage = _maxCourage;
		}

		_deck.TickCooldowns(deltaF);
		_spellDeck.TickCooldowns(deltaF);
		_spawnDirector.Tick(deltaF, _elapsed, () => CountTeamUnits(Team.Enemy), SpawnEnemyUnit, SetStatus);
		UpdateEndlessFieldEvent(deltaF);
		UpdateStageHazards();
		UpdateChallengeGhost(deltaF);

		SimulateUnits(deltaF);
		CleanupDeadUnits();
		UpdateStageMissions(deltaF);
		UpdateEndlessDirectiveState();
		UpdateEndlessContactEvent(deltaF);
		MaybeOpenEndlessDraft();
		UpdateLanRaceTelemetry(deltaF);
		UpdateOnlineRoomTelemetry(deltaF);
		UpdateOnlineRoomMonitor(deltaF);
		AudioDirector.Instance?.SetBattlePressure(ResolveBattleAudioPressure());
		UpdateHud();
		QueueRedraw();
		CheckBattleEnd();
	}

	public override void _Process(double delta)
	{
		if (!_battleEnded || !IsOnlineRoomMode)
		{
			return;
		}

		TickOnlineRoomEndPanelRefresh((float)delta);
	}

	private bool HandleLanStartBarrier(float delta)
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

		_onlineRoomTelemetryTimer = OnlineRoomTelemetryIntervalSeconds;
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

		_onlineRoomMonitorRefreshTimer = OnlineRoomMonitorRefreshIntervalSeconds;
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
				$"#{entry.Rank} {entry.PlayerCallsign}  |  {entry.Score} pts  |  Hull {entry.HullPercent}%  |  {entry.ElapsedSeconds:0.0}s  |  {(entry.Retreated ? "retreated" : entry.Won ? "cleared" : "failed")}");
		}

		return string.Join("\n", lines);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_battleEnded || _endlessCheckpointActive)
		{
			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
		{
			return;
		}

		if (!mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		if (IsInBattlefield(mouseButton.Position))
		{
			TryUseSelectionAt(mouseButton.Position);
		}
	}

	private void BuildUi()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		var canvasLayer = new CanvasLayer();
		AddChild(canvasLayer);

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvasLayer.AddChild(root);

		var topHudPanel = new PanelContainer
		{
			Position = new Vector2(16f, 16f),
			Size = new Vector2(540f, 248f)
		};
		topHudPanel.SelfModulate = route.BannerPanel.Lightened(0.08f);
		root.AddChild(topHudPanel);

		var topVBox = new VBoxContainer();
		topVBox.AddThemeConstantOverride("separation", 5);
		topHudPanel.AddChild(topVBox);

		_battleBannerLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_battleBannerLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		topVBox.AddChild(_battleBannerLabel);

		_battleSubtitleLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		topVBox.AddChild(_battleSubtitleLabel);

		_battleMissionLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_battleMissionLabel.AddThemeColorOverride("font_color", route.BannerAccent.Lightened(0.08f));
		topVBox.AddChild(_battleMissionLabel);

		_baseHealthLabel = new Label();
		topVBox.AddChild(_baseHealthLabel);

		_resourceLabel = new Label();
		topVBox.AddChild(_resourceLabel);

		_timerLabel = new Label();
		topVBox.AddChild(_timerLabel);

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		topVBox.AddChild(_statusLabel);

		_fpsLabel = new Label();
		topVBox.AddChild(_fpsLabel);

		var intelPanel = new PanelContainer
		{
			Position = new Vector2(572f, 16f),
			Size = new Vector2(380f, 336f)
		};
		intelPanel.SelfModulate = route.BannerPanel;
		root.AddChild(intelPanel);

		var infoVBox = new VBoxContainer();
		infoVBox.AddThemeConstantOverride("separation", 8);
		intelPanel.AddChild(infoVBox);

		var infoHeaderLabel = new Label
		{
			Text = IsEndlessMode ? "War Wagon Orders" : IsChallengeMode ? "Challenge Orders" : "Siege Orders"
		};
		infoHeaderLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		infoVBox.AddChild(infoHeaderLabel);

		infoVBox.AddChild(new Label
		{
			Text = IsEndlessMode
				? "1) Pick a squad or spell card below.\n2) Click the battlefield.\n3) Survive escalating waves or retreat to bank recovered rewards."
				: "1) Pick a squad or spell card below.\n2) Click the battlefield.\nCards spend courage and go on cooldown after use.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		_waveIntelLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		infoVBox.AddChild(_waveIntelLabel);

		_objectiveStatusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		infoVBox.AddChild(_objectiveStatusLabel);

		var retreatButton = new Button
		{
			Text = IsEndlessMode ? "Retreat From Run" : "Retreat To Map",
			Position = new Vector2(970f, 22f),
			Size = new Vector2(292f, 44f)
		};
		ApplyBattleButtonTheme(retreatButton, route);
		retreatButton.Pressed += RetreatToMap;
		root.AddChild(retreatButton);

		var settingsPanel = new PanelContainer
		{
			Position = new Vector2(970f, 76f),
			Size = new Vector2(292f, 92f)
		};
		settingsPanel.SelfModulate = route.BannerPanel.Darkened(0.03f);
		root.AddChild(settingsPanel);

		var settingsVBox = new VBoxContainer();
		settingsVBox.AddThemeConstantOverride("separation", 3);
		settingsPanel.AddChild(settingsVBox);

		var settingsLabel = new Label { Text = "UI Settings" };
		settingsLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		settingsVBox.AddChild(settingsLabel);

		_showDevUiToggle = new CheckBox
		{
			Text = "Show dev UI",
			ButtonPressed = GameState.Instance.ShowDevUi
		};
		_showDevUiToggle.Toggled += OnShowDevUiToggled;
		settingsVBox.AddChild(_showDevUiToggle);

		_showFpsToggle = new CheckBox
		{
			Text = "Show FPS counter",
			ButtonPressed = GameState.Instance.ShowFpsCounter
		};
		_showFpsToggle.Toggled += OnShowFpsToggled;
		settingsVBox.AddChild(_showFpsToggle);

		var spawnPanel = new PanelContainer
		{
			Position = new Vector2(16f, 586f),
			Size = new Vector2(1246f, _spellDeck.Roster.Count > 0 ? 188f : 114f)
		};
		spawnPanel.SelfModulate = route.BannerPanel.Darkened(0.02f);
		root.AddChild(spawnPanel);

		var spawnStack = new VBoxContainer();
		spawnStack.AddThemeConstantOverride("separation", 8);
		spawnPanel.AddChild(spawnStack);

		var unitRow = new HBoxContainer();
		unitRow.AddThemeConstantOverride("separation", 10);
		spawnStack.AddChild(unitRow);

		foreach (var definition in _deck.Roster)
		{
			var unit = definition;
			var button = new Button
			{
				Text = $"Deploy {unit.DisplayName} ({unit.Cost})",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, 82f)
			};
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			button.Pressed += () => ArmPlayerUnit(unit);
			unitRow.AddChild(button);
			_deploySlots.Add(new DeploySlot(unit, button));
		}

		if (_spellDeck.Roster.Count > 0)
		{
			var spellRow = new HBoxContainer();
			spellRow.AddThemeConstantOverride("separation", 10);
			spawnStack.AddChild(spellRow);

			foreach (var definition in _spellDeck.Roster)
			{
				var spell = definition;
				var button = new Button
				{
					Text = spell.DisplayName,
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
					CustomMinimumSize = new Vector2(0f, 64f)
				};
				button.AddThemeColorOverride("font_color", Colors.White);
				button.AddThemeColorOverride("font_hover_color", Colors.White);
				button.AddThemeColorOverride("font_pressed_color", Colors.White);
				button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
				button.Pressed += () => ArmSpell(spell);
				spellRow.AddChild(button);
				_spellSlots.Add(new SpellSlot(spell, button));
			}
		}

		_endCenter = new CenterContainer();
		_endCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_endCenter.Visible = false;
		root.AddChild(_endCenter);

		_endPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(480f, 280f),
			Visible = false
		};
		_endPanel.SelfModulate = route.BannerPanel.Lightened(0.04f);
		_endCenter.AddChild(_endPanel);

		var endPadding = new MarginContainer();
		endPadding.AddThemeConstantOverride("margin_left", 20);
		endPadding.AddThemeConstantOverride("margin_right", 20);
		endPadding.AddThemeConstantOverride("margin_top", 20);
		endPadding.AddThemeConstantOverride("margin_bottom", 20);
		_endPanel.AddChild(endPadding);

		var endVBox = new VBoxContainer();
		endVBox.AddThemeConstantOverride("separation", 12);
		endPadding.AddChild(endVBox);

		_endLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_endLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		endVBox.AddChild(_endLabel);

		var retryButton = new Button
		{
			Text = IsEndlessMode
				? "Restart Run"
					: IsLanRaceMode
						? "Room Rematch"
						: IsOnlineRoomMode
							? "Back To Online Room"
						: IsChallengeMode
							? "Retry Challenge"
							: "Retry Stage",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		ApplyBattleButtonTheme(retryButton, route);
		retryButton.Pressed += () =>
		{
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

			SceneRouter.Instance.RetryBattle();
		};
		endVBox.AddChild(retryButton);

		var mapButton = new Button
		{
			Text = IsEndlessMode
				? "Back To Endless Prep"
					: IsLanRaceMode
						? "Back To Multiplayer"
						: IsOnlineRoomMode
							? "Leave Online Room"
						: IsChallengeMode
							? "Back To Multiplayer"
							: "Back To Map",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		ApplyBattleButtonTheme(mapButton, route);
		mapButton.Pressed += () =>
		{
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

			SceneRouter.Instance.GoToMap();
		};
		endVBox.AddChild(mapButton);

		_draftCenter = new CenterContainer();
		_draftCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_draftCenter.Visible = false;
		root.AddChild(_draftCenter);

		_draftPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(560f, 320f),
			Visible = false
		};
		_draftPanel.SelfModulate = route.BannerPanel;
		_draftCenter.AddChild(_draftPanel);

		var draftPadding = new MarginContainer();
		draftPadding.AddThemeConstantOverride("margin_left", 20);
		draftPadding.AddThemeConstantOverride("margin_right", 20);
		draftPadding.AddThemeConstantOverride("margin_top", 20);
		draftPadding.AddThemeConstantOverride("margin_bottom", 20);
		_draftPanel.AddChild(draftPadding);

		var draftVBox = new VBoxContainer();
		draftVBox.AddThemeConstantOverride("separation", 12);
		draftPadding.AddChild(draftVBox);

		_draftLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_draftLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		draftVBox.AddChild(_draftLabel);

		for (var i = 0; i < 3; i++)
		{
			var draftIndex = i;
			var draftButton = new Button
			{
				CustomMinimumSize = new Vector2(0f, 56f)
			};
			ApplyBattleButtonTheme(draftButton, route);
			draftButton.Pressed += () => ApplyEndlessDraftChoice(draftIndex);
			draftVBox.AddChild(draftButton);
			_draftButtons.Add(draftButton);
		}

		ApplyDevUiSettings();
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private static void ApplyBattleButtonTheme(Button button, RouteDefinition route)
	{
		button.SelfModulate = route.BannerPanel.Lightened(0.04f);
		button.AddThemeColorOverride("font_color", route.BannerAccent);
		button.AddThemeColorOverride("font_hover_color", route.BannerAccent.Lightened(0.08f));
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.45f));
	}

	private void UpdateHud()
	{
		_battleBannerLabel.Text = BuildBattleBannerTitle();
		_battleSubtitleLabel.Text = BuildBattleBannerSubtitle();
		_battleMissionLabel.Text = BuildBattleBannerStatusText();
		_baseHealthLabel.Text = IsEndlessMode
			? $"War wagon hull: {Mathf.CeilToInt(_playerBaseHealth)}/{Mathf.CeilToInt(_playerBaseMaxHealth)}   |   Route: {ResolveRouteLabel(_activeRouteId)} endless hold"
			: IsChallengeMode
				? $"War wagon hull: {Mathf.CeilToInt(_playerBaseHealth)}/{Mathf.CeilToInt(_playerBaseMaxHealth)}   |   Gatehouse: {Mathf.CeilToInt(_enemyBaseHealth)}/{Mathf.CeilToInt(_enemyBaseMaxHealth)}   |   Challenge {_challengeDefinition.Code}"
				: $"War wagon hull: {Mathf.CeilToInt(_playerBaseHealth)}/{Mathf.CeilToInt(_playerBaseMaxHealth)}   |   Gatehouse: {Mathf.CeilToInt(_enemyBaseHealth)}/{Mathf.CeilToInt(_enemyBaseMaxHealth)}";
		_resourceLabel.Text = IsEndlessMode
			? $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Endless wave {_spawnDirector.EndlessWaveNumber}   |   Best {GameState.Instance.BestEndlessWave}"
			: IsChallengeMode
				? $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Challenge Stage {_stage}   |   Best {GameState.Instance.GetAsyncChallengeBestScore(_challengeDefinition.Code)}"
				: $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Stage {_stage}";
		if (IsChallengeMode && _challengeMutator.SignalJamIntervalSeconds > 0.05f && _enemySignalJamTimer <= 0.05f)
		{
			_resourceLabel.Text += $"   |   Next blackout {_challengeMutatorNextJamTimer:0.0}s";
		}
		if (_enemySignalJamTimer > 0.05f)
		{
			_resourceLabel.Text += $"   |   Signal jam {_enemySignalJamTimer:0.0}s";
		}
		var waveStatus = _spawnDirector.IsEndlessMode
			? $"   |   Pending surge: {Mathf.Max(0f, _spawnDirector.NextEndlessWaveTime - _elapsed):0.0}s   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
			: _spawnDirector.UsesScriptedWaves
				? $"   |   Waves: {_spawnDirector.NextScriptedWaveIndex}/{_spawnDirector.TotalScriptedWaves}   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
				: "";
		_timerLabel.Text =
			$"Time: {_elapsed:0.0}s   |   Active enemies: {CountTeamUnits(Team.Enemy)}   |   Active allies: {CountTeamUnits(Team.Player)}{waveStatus}";
		_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
		_waveIntelLabel.Text = BuildWaveIntelText();
		if (IsEndlessMode)
		{
			_objectiveStatusLabel.Text = BuildEndlessStatusText();
		}
		else
		{
			var objectiveText = StageObjectives.BuildLiveSummary(_stageData, BuildStageBattleResult());
			var missionText = BuildStageMissionEventText();
			_objectiveStatusLabel.Text = string.IsNullOrWhiteSpace(missionText)
				? objectiveText
				: $"{objectiveText}\n{missionText}";
		}

		foreach (var slot in _deploySlots)
		{
			var cooldown = _deck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= slot.Definition.Cost;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;
			var level = GameState.Instance.GetUnitLevel(slot.Definition.Id);

			var stateLabel = !isReady
				? $"RECOVER {cooldown:0.0}s"
				: hasCourage
					? "READY"
					: $"NEED {slot.Definition.Cost}";
			var marker = slot.Definition == _deck.ArmedUnit ? "> " : "";
			slot.Button.Text =
				$"{marker}Lv{level} {slot.Definition.DisplayName}\n{stateLabel}  |  {slot.Definition.Cost} courage";
			slot.Button.SelfModulate = ResolveDeployButtonTint(slot.Definition, isReady, hasCourage, slot.Definition == _deck.ArmedUnit);
			slot.Button.TooltipText = BuildDeployButtonTooltip(slot.Definition, level, isReady, cooldown);
		}

		foreach (var slot in _spellSlots)
		{
			var cooldown = _spellDeck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= slot.Definition.CourageCost;
			var armed = _selectionMode == BattleSelectionMode.Spell && slot.Definition == _spellDeck.ArmedSpell;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;

			var stateLabel = !isReady
				? $"RECOVER {cooldown:0.0}s"
				: hasCourage
					? "READY"
					: $"NEED {slot.Definition.CourageCost}";
			var marker = armed ? "* " : "";
			slot.Button.Text =
				$"{marker}{slot.Definition.DisplayName}\n{stateLabel}  |  {slot.Definition.CourageCost} courage";
			slot.Button.SelfModulate = ResolveSpellButtonTint(slot.Definition, isReady, hasCourage, armed);
			slot.Button.TooltipText = SpellText.BuildTooltipSummary(slot.Definition, isReady, cooldown);
		}
	}

	private string BuildBattleBannerTitle()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		return IsEndlessMode
			? $"Endless Hold  |  {route.Title}"
			: IsChallengeMode
				? $"Challenge {_challengeDefinition.Code}  |  {route.Title}"
				: $"Stage {_stage}  |  {route.Title}";
	}

	private string BuildBattleBannerSubtitle()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		return IsEndlessMode
			? $"Frontline: {_stageData.StageName}\nPath: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}  |  Pressure: {route.PressureSummary}"
			: $"{_stageData.StageName}\nPressure: {route.PressureSummary}";
	}

	private string BuildBattleBannerStatusText()
	{
		if (IsEndlessMode)
		{
			return $"Battlefield event: {_endlessBattlefieldEventLabel}\nCaravan support: {_endlessSupportEventLabel}";
		}

		var missionSummary = BuildStageMissionIntelText().Trim();
		if (string.IsNullOrWhiteSpace(missionSummary))
		{
			missionSummary = $"Battlefield pressure: {StageEncounterIntel.BuildSupportPressureSummary(_stageData)}";
		}

		if (!IsChallengeMode)
		{
			return missionSummary;
		}

		var mutatorText = BuildChallengeMutatorText();
		return string.IsNullOrWhiteSpace(mutatorText)
			? missionSummary
			: $"{mutatorText}\n{missionSummary}";
	}

	private void OnShowDevUiToggled(bool enabled)
	{
		GameState.Instance.SetShowDevUi(enabled);
		ApplyDevUiSettings();
	}

	private void OnShowFpsToggled(bool enabled)
	{
		GameState.Instance.SetShowFpsCounter(enabled);
		ApplyDevUiSettings();
	}

	private void ApplyDevUiSettings()
	{
		var showDevUi = GameState.Instance.ShowDevUi;
		_timerLabel.Visible = showDevUi;
		_statusLabel.Visible = showDevUi;
		_showFpsToggle.Disabled = !showDevUi;
		_fpsLabel.Visible = showDevUi && GameState.Instance.ShowFpsCounter;
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

	private string BuildActiveEnemyPressureText()
	{
		var howlers = 0;
		var jammers = 0;
		var saboteurs = 0;
		var spitters = 0;
		var bosses = 0;

		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != Team.Enemy)
			{
				continue;
			}

			switch (unit.VisualClass)
			{
				case "howler":
					howlers++;
					break;
				case "jammer":
					jammers++;
					break;
				case "saboteur":
					saboteurs++;
					break;
				case "spitter":
					spitters++;
					break;
				case "boss":
					bosses++;
					break;
			}
		}

		var parts = new List<string>();
		if (howlers > 0)
		{
			parts.Add($"Dread Herald x{howlers}");
		}

		if (jammers > 0)
		{
			parts.Add($"Hexer x{jammers}");
		}

		if (saboteurs > 0)
		{
			parts.Add($"Sapper x{saboteurs}");
		}

		if (spitters > 0)
		{
			parts.Add($"Blight Caster x{spitters}");
		}

		if (bosses > 0)
		{
			parts.Add($"Grave Lord x{bosses}");
		}

		if (_enemySignalJamTimer > 0.05f)
		{
			parts.Add($"Signal jam {_enemySignalJamTimer:0.0}s");
		}

		return parts.Count == 0
			? "Active pressure: standard front."
			: $"Active pressure: {string.Join(", ", parts)}";
	}

	private bool HasTeamUnitInRadius(Team team, Vector2 anchor, float radius)
	{
		var radiusSquared = radius * radius;
		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Team != team)
			{
				continue;
			}

			if (unit.Position.DistanceSquaredTo(anchor) <= radiusSquared)
			{
				return true;
			}
		}

		return false;
	}

	private void ArmPlayerUnit(UnitDefinition definition)
	{
		if (_battleEnded)
		{
			return;
		}

		_selectionMode = BattleSelectionMode.Unit;
		_deck.Arm(definition);
		var doctrine = GameState.Instance.GetUnitDoctrineDefinition(definition.Id);
		var doctrineSuffix = doctrine == null ? "" : $" [{doctrine.Title}]";
		SetStatus(
			$"Selected Lv{GameState.Instance.GetUnitLevel(definition.Id)} {definition.DisplayName}{doctrineSuffix}. Click the battlefield to deploy from the war wagon.");
		UpdateHud();
	}

	private void ArmSpell(SpellDefinition definition)
	{
		if (_battleEnded)
		{
			return;
		}

		_selectionMode = BattleSelectionMode.Spell;
		_spellDeck.Arm(definition);
		SetStatus($"Selected {definition.DisplayName}. Click the battlefield to cast it.");
		UpdateHud();
	}

	private bool IsInBattlefield(Vector2 position)
	{
		return position.X >= BattlefieldLeft &&
			position.X <= BattlefieldRight &&
			position.Y >= BattlefieldTop &&
			position.Y <= BattlefieldBottom;
	}

	private void TryUseSelectionAt(Vector2 clickPosition)
	{
		if (_selectionMode == BattleSelectionMode.Spell && _spellDeck.HasArmedSpell)
		{
			TryCastSpellAt(_spellDeck.ArmedSpell, ClampBattlefieldPoint(clickPosition));
			return;
		}

		if (!_deck.HasArmedUnit)
		{
			SetStatus("Pick a squad or spell card first, then click the battlefield.");
			return;
		}

		TryDeployAtY(clickPosition.Y);
	}

	private void TryDeployAtY(float clickY)
	{
		if (!_deck.HasArmedUnit)
		{
			SetStatus("Pick a squad card first, then click the battlefield.");
			return;
		}

		var spawnY = Mathf.Clamp(clickY, BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding);
		TrySpawnPlayer(_deck.ArmedUnit, new Vector2(PlayerSpawnX, spawnY));
	}

	private void TryCastSpellAt(SpellDefinition definition, Vector2 targetPosition)
	{
		if (!_spellDeck.CanCast(definition, _courage, _battleEnded, _endlessCheckpointActive, out var reason))
		{
			SetStatus(reason);
			return;
		}

		_courage -= definition.CourageCost;
		_spellDeck.MarkCast(definition, ResolvePlayerSpellCooldown(definition));
		var effectSummary = ApplySpellEffect(definition, targetPosition);
		_selectionMode = BattleSelectionMode.Unit;
		AudioDirector.Instance?.PlayUiConfirm();
		SetStatus($"Cast {definition.DisplayName} at lane {Mathf.RoundToInt(targetPosition.Y)}. {effectSummary}");
		UpdateHud();
	}

	private void TrySpawnPlayer(UnitDefinition definition, Vector2 spawnPosition)
	{
		if (_battleEnded)
		{
			return;
		}

		if (!_deck.CanDeploy(definition, _courage, _battleEnded, out var reason))
		{
			SetStatus(reason);
			return;
		}

		var stats = BuildPlayerUnitStatsForBattle(definition);
		_courage -= stats.Cost;
		_deck.MarkDeployed(definition, ResolvePlayerDeployCooldown(definition));
		_playerDeployments++;
		RecordChallengeDeployment(definition.Id, spawnPosition.Y);
		SpawnUnit(Team.Player, stats, spawnPosition);
		AudioDirector.Instance?.PlayDeploy(definition);
		SpawnEffect(spawnPosition, stats.Color, 12f, 42f, 0.28f);
		var ghostDeployFeedback = BuildChallengeGhostDeployFeedback(definition, spawnPosition);
		var doctrine = GameState.Instance.GetUnitDoctrineDefinition(definition.Id);
		var doctrineSuffix = doctrine == null ? "" : $" [{doctrine.Title}]";
		SetStatus(
			$"Deployed Lv{GameState.Instance.GetUnitLevel(definition.Id)} {stats.Name}{doctrineSuffix} from the war wagon at lane height {Mathf.RoundToInt(spawnPosition.Y)}.{ghostDeployFeedback}");
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
		SpawnUnit(Team.Enemy, stats, position);
		SpawnEffect(position, stats.Color.Darkened(0.15f), 10f, 26f, 0.22f, false);
	}

	private void SpawnUnit(Team team, UnitStats stats, Vector2 position)
	{
		var unit = new Unit();
		unit.Setup(team, stats, position);
		AddChild(unit);
		_units.Add(unit);
	}

	private void SpawnProjectile(Unit attacker, Unit target)
	{
		var projectile = new Projectile();
		projectile.GlobalPosition = attacker.GlobalPosition;

		var speed = attacker.ProjectileSpeed > 0f ? attacker.ProjectileSpeed : 420f;
		var color = attacker.Tint.Lightened(0.25f);
		if (attacker.AttackSplashRadius > 0.05f)
		{
			projectile.Setup(
				target,
				attacker.CurrentAttackDamage,
				speed,
				color,
				_ =>
				{
					ApplySplashDamage(attacker.Team, target.GlobalPosition, attacker.CurrentAttackDamage, attacker.AttackSplashRadius, attacker.Tint);
					return 0f;
				},
				() => !IsInstanceValid(target) || target.IsDead,
				(position, _, hitColor) =>
				{
					SpawnEffect(position, hitColor.Lightened(0.14f), 8f, attacker.AttackSplashRadius, 0.2f, false);
					SpawnFloatText(position + new Vector2(0f, -16f), "BLAST", hitColor.Lightened(0.25f), 0.48f);
				});
		}
		else
		{
			projectile.Setup(target, attacker.CurrentAttackDamage, speed, color, SpawnDamageFeedback);
		}

		AddChild(projectile);
	}

	private void SpawnContactPressureProjectile(Unit attacker)
	{
		if (!IsInstanceValid(_activeEndlessContactActor) || _activeEndlessContact == null)
		{
			return;
		}

		var targetActor = _activeEndlessContactActor;
		var projectile = new Projectile();
		projectile.GlobalPosition = attacker.GlobalPosition;
		var speed = attacker.ProjectileSpeed > 0f ? attacker.ProjectileSpeed : 400f;
		var color = attacker.Tint.Lightened(0.18f);
		projectile.Setup(
			targetActor,
			ResolveEnemyContactAttackDamage(attacker),
			speed,
			color,
			damage =>
			{
				if (!CanInteractWithEndlessContactActor(targetActor))
				{
					return 0f;
				}

				var appliedDamage = targetActor.ApplyPressureDamage(damage);
				RegisterEndlessContactPressure(appliedDamage);
				return appliedDamage;
			},
			() => !CanInteractWithEndlessContactActor(targetActor),
			(position, appliedDamage, hitColor) =>
			{
				if (appliedDamage > 0.05f)
				{
					SpawnEffect(position, hitColor, 6f, 18f + (appliedDamage * 0.2f), 0.16f, false);
					SpawnFloatText(position + new Vector2(_rng.RandfRange(-8f, 8f), -10f), $"-{Mathf.RoundToInt(appliedDamage)}", hitColor.Lightened(0.22f), 0.46f);
				}
			});
		AddChild(projectile);
	}

	private void SpawnContactSupportProjectile(Unit unit, float repairAmount, float progressBoost, string supportLabel)
	{
		if (!IsInstanceValid(_activeEndlessContactActor) || _activeEndlessContact == null)
		{
			return;
		}

		var targetActor = _activeEndlessContactActor;
		var projectile = new Projectile();
		projectile.GlobalPosition = unit.GlobalPosition;
		var speed = unit.ProjectileSpeed > 0f ? unit.ProjectileSpeed : 420f;
		var color = unit.Tint.Lightened(0.24f);
		projectile.Setup(
			targetActor,
			repairAmount,
			speed,
			color,
			_ =>
			{
				if (!CanInteractWithEndlessContactActor(targetActor))
				{
					return 0f;
				}

				var repaired = targetActor.Repair(repairAmount);
				_activeEndlessContact.Progress = Mathf.Min(
					_activeEndlessContact.Definition.TargetSeconds,
					_activeEndlessContact.Progress + progressBoost);
				RegisterEndlessContactSupport(repaired, progressBoost);
				SpawnFloatText(
					targetActor.Position + new Vector2(_rng.RandfRange(-10f, 10f), -18f),
					supportLabel,
					color.Lightened(0.2f),
					0.48f);
				return repaired;
			},
			() => !CanInteractWithEndlessContactActor(targetActor),
			(position, appliedRepair, hitColor) =>
			{
				if (appliedRepair > 0.05f)
				{
					SpawnEffect(position, hitColor, 6f, 18f + (appliedRepair * 0.2f), 0.16f, false);
				}
			});
		AddChild(projectile);
	}

	private bool CanInteractWithEndlessContactActor(EndlessContactActor actor)
	{
		return IsInstanceValid(actor) &&
			IsInstanceValid(_activeEndlessContactActor) &&
			ReferenceEquals(_activeEndlessContactActor, actor) &&
			_activeEndlessContact != null &&
			!_activeEndlessContact.Completed &&
			!_activeEndlessContact.Failed;
	}

	private void RegisterEndlessContactSupport(float repaired, float progressBoost)
	{
		if (_activeEndlessContact == null)
		{
			return;
		}

		_activeEndlessContact.PlayerSupportActions++;
		_activeEndlessContact.PlayerSupportRepairTotal += Mathf.Max(0f, repaired);
		_activeEndlessContact.PlayerSupportProgressTotal += Mathf.Max(0f, progressBoost);
	}

	private void RegisterEndlessContactPressure(float appliedDamage)
	{
		if (_activeEndlessContact == null)
		{
			return;
		}

		_activeEndlessContact.EnemyPressureActions++;
		_activeEndlessContact.EnemyPressureDamageTotal += Mathf.Max(0f, appliedDamage);
	}

	private void TryAttackBase(Unit attacker)
	{
		if (IsEndlessMode && attacker.Team == Team.Player)
		{
			return;
		}

		var targetBase = attacker.Team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition;
		if (!attacker.TryBeginAttackPosition(targetBase, BaseCoreRadius))
		{
			return;
		}

		if (attacker.Team == Team.Player)
		{
			_enemyBaseHealth -= attacker.BaseDamage;
			_enemyBaseFlashTimer = 0.22f;
			AudioDirector.Instance?.PlayBaseHit(false, attacker.BaseDamage);
			SpawnEffect(EnemyBaseCorePosition, attacker.Tint, 8f, 26f, 0.18f);
			SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -24f), $"-{attacker.BaseDamage}", attacker.Tint.Lightened(0.18f), 0.44f);
		}
		else
		{
			_playerBaseHealth -= attacker.BaseDamage;
			_playerBaseFlashTimer = 0.22f;
			AudioDirector.Instance?.PlayBaseHit(true, attacker.BaseDamage);
			SpawnEffect(PlayerBaseCorePosition, attacker.Tint, 8f, 26f, 0.18f);
			SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -24f), $"-{attacker.BaseDamage}", attacker.Tint.Lightened(0.18f), 0.44f);
		}
	}

	private bool ShouldRepairBus(Unit unit)
	{
		return unit.Team == Team.Player &&
			unit.BusRepairAmount > 0.05f &&
			_playerBaseHealth < _playerBaseMaxHealth - 0.5f;
	}

	private void SimulatePlayerBusSupport(Unit unit, float delta)
	{
		var supportRadius = BaseCoreRadius + 18f;
		if (unit.CanAttackPosition(PlayerBaseCorePosition, supportRadius))
		{
			if (unit.TryBeginAttackPosition(PlayerBaseCorePosition, supportRadius))
			{
				var repaired = Mathf.Min(unit.BusRepairAmount, _playerBaseMaxHealth - _playerBaseHealth);
				if (repaired > 0.05f)
				{
					_playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + repaired);
					_playerBaseFlashTimer = 0.12f;
					AudioDirector.Instance?.PlayBusRepair(repaired);
					SpawnEffect(PlayerBaseCorePosition, unit.Tint.Lightened(0.12f), 7f, 22f, 0.18f);
					SpawnFloatText(PlayerBaseCorePosition + new Vector2(_rng.RandfRange(-10f, 10f), -34f), $"+{Mathf.RoundToInt(repaired)}", unit.Tint.Lightened(0.26f), 0.44f);
					SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -54f), "REPAIR", unit.Tint.Lightened(0.18f), 0.42f);
				}
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

		foreach (var unit in _units)
		{
			if (unit.IsDead)
			{
				continue;
			}

			unit.TickAttackTimer(delta);
			unit.TickSpecialTimer(delta);
			if (TryTriggerEnemySpecialAbility(unit))
			{
				continue;
			}
			var target = FindClosestEnemy(unit);
			var prioritizeContact = ShouldPrioritizeEndlessContact(unit, target);
			var supportContact = ShouldSupportEndlessContact(unit, target);
			var prioritizeObjectiveRaid = ShouldPrioritizeObjectiveRaid(unit, target);

			if (target != null && !prioritizeObjectiveRaid && unit.CanAttack(target))
			{
				if (unit.UsesProjectile)
				{
					if (unit.TryBeginAttack(target))
					{
						SpawnProjectile(unit, target);
					}
				}
				else
				{
					if (unit.TryBeginAttack(target))
					{
						if (unit.AttackSplashRadius > 0.05f)
						{
							ApplySplashDamage(unit.Team, target.Position, unit.CurrentAttackDamage, unit.AttackSplashRadius, unit.Tint);
							SpawnFloatText(target.Position + new Vector2(0f, -16f), "BLAST", unit.Tint.Lightened(0.22f), 0.46f);
						}
						else
						{
							var appliedDamage = target.TakeDamage(unit.CurrentAttackDamage);
							SpawnDamageFeedback(target.Position, appliedDamage, unit.Tint);
						}
					}
				}
			}
			else if (prioritizeContact)
			{
				SimulateEnemyContactPressure(unit, delta);
			}
			else if (supportContact)
			{
				SimulatePlayerContactSupport(unit, delta);
			}
			else if (target != null && !prioritizeObjectiveRaid)
			{
				if (unit.UsesProjectile)
				{
					// Ranged units anchor their line when an enemy is nearby.
					// They do not chase targets that move out of firing range.
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
					TryAttackBase(unit);
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

	private bool TryTriggerEnemySpecialAbility(Unit unit)
	{
		if (unit.Team != Team.Enemy || unit.IsDead || !unit.HasSpecialAbility)
		{
			return false;
		}

		return unit.SpecialAbilityId switch
		{
			"rally_call" => TriggerBossRallyCall(unit),
			"jam_signal" => TriggerEnemySignalJam(unit),
			_ => false
		};
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
				escortsSpawned++;
			}
		}

		SpawnEffect(boss.Position, boss.Tint.Lightened(0.15f), 12f, Mathf.Max(56f, boss.SpecialBuffRadius * 0.55f), 0.28f, false);
		SpawnFloatText(boss.Position + new Vector2(0f, -48f), "RALLY", boss.Tint.Lightened(0.26f), 0.6f);
		SetStatus(
			$"Grave Lord rally call: {buffedCount} undead surged forward" +
			(escortsSpawned > 0 ? $" and {escortsSpawned} escorts joined the push." : "."));
		return true;
	}

	private bool TriggerEnemySignalJam(Unit jammer)
	{
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

		SpawnEffect(jammer.Position, jammer.Tint.Lightened(0.08f), 12f, 54f, 0.26f, false);
		SpawnFloatText(jammer.Position + new Vector2(0f, -42f), "JAM", jammer.Tint.Lightened(0.22f), 0.6f);
		SpawnEffect(PlayerBaseCorePosition, jammer.Tint, 10f, 36f, 0.24f, false);
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -56f), "SIGNAL JAM", jammer.Tint.Lightened(0.22f), 0.62f);
		if (GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId) > 0)
		{
			SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -86f), "RELAY HARDENED", new Color("d9f0ff"), 0.56f);
		}
		SetStatus($"Enemy hexer disrupted caravan rhythm: courage gain suppressed for {jamDuration:0.0}s and card recovery delayed.");
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
		SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -56f), "BLACKOUT", blackoutColor.Lightened(0.2f), 0.62f);
		SpawnEffect(PlayerBaseCorePosition, blackoutColor.Lightened(0.08f), 10f, 38f, 0.24f, false);
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -56f), "BOARD JAM", blackoutColor.Lightened(0.2f), 0.62f);
		if (GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId) > 0)
		{
			SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -86f), "RELAY HARDENED", new Color("d9f0ff"), 0.56f);
		}

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

	private bool ShouldSupportEndlessContact(Unit unit, Unit directTarget)
	{
		if (!IsEndlessMode ||
			unit.Team != Team.Player ||
			_activeEndlessContact == null ||
			!IsInstanceValid(_activeEndlessContactActor) ||
			_activeEndlessContact.Completed ||
			_activeEndlessContact.Failed)
		{
			return false;
		}

		var contactPosition = _activeEndlessContactActor.Position;
		var distanceToContact = unit.Position.DistanceTo(contactPosition);
		if (distanceToContact > Mathf.Max(210f, unit.AggroRangeX * 1.3f))
		{
			return false;
		}

		if (directTarget == null)
		{
			return true;
		}

		if (unit.CanAttack(directTarget))
		{
			return false;
		}

		var targetDistance = unit.Position.DistanceTo(directTarget.Position);
		return distanceToContact + 14f <= targetDistance;
	}

	private void SimulatePlayerContactSupport(Unit unit, float delta)
	{
		if (!IsInstanceValid(_activeEndlessContactActor) || _activeEndlessContact == null)
		{
			return;
		}

		var contactPosition = _activeEndlessContactActor.Position;
		var supportRadius = ResolveEndlessContactSupportRadius();
		if (unit.CanAttackPosition(contactPosition, supportRadius))
		{
			if (unit.TryBeginAttackPosition(contactPosition, supportRadius))
			{
				var repairAmount = ResolvePlayerContactSupportRepair(unit);
				var progressBoost = ResolvePlayerContactSupportProgress(unit);
				var supportLabel = ResolvePlayerContactSupportLabel(unit);
				if (unit.UsesProjectile)
				{
					SpawnContactSupportProjectile(unit, repairAmount, progressBoost, supportLabel);
				}
				else
				{
					var repaired = _activeEndlessContactActor.Repair(repairAmount);
					_activeEndlessContact.Progress = Mathf.Min(
						_activeEndlessContact.Definition.TargetSeconds,
						_activeEndlessContact.Progress + progressBoost);
					RegisterEndlessContactSupport(repaired, progressBoost);

					if (repaired > 0.05f)
					{
						SpawnEffect(contactPosition, unit.Tint.Lightened(0.15f), 6f, 18f + (repaired * 0.2f), 0.16f, false);
					}

					SpawnFloatText(
						contactPosition + new Vector2(_rng.RandfRange(-10f, 10f), -18f),
						supportLabel,
						unit.Tint.Lightened(0.28f),
						0.48f);
				}
			}

			return;
		}

		unit.MoveToward(
			contactPosition,
			delta,
			BattlefieldLeft,
			BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding);
	}

	private bool ShouldPrioritizeEndlessContact(Unit unit, Unit directTarget)
	{
		if (!IsEndlessMode ||
			unit.Team != Team.Enemy ||
			!CanEnemyUnitPressureContact(unit) ||
			_activeEndlessContact == null ||
			!IsInstanceValid(_activeEndlessContactActor) ||
			_activeEndlessContact.Completed ||
			_activeEndlessContact.Failed)
		{
			return false;
		}

		var contactPosition = _activeEndlessContactActor.Position;
		var distanceToContact = unit.Position.DistanceTo(contactPosition);
		if (distanceToContact > Mathf.Max(220f, unit.AggroRangeX * 1.35f))
		{
			return false;
		}

		if (directTarget == null)
		{
			return true;
		}

		if (unit.CanAttack(directTarget))
		{
			return false;
		}

		var targetDistance = unit.Position.DistanceTo(directTarget.Position);
		return distanceToContact + 18f < targetDistance || unit.Position.X <= contactPosition.X + 96f;
	}

	private bool CanEnemyUnitPressureContact(Unit unit)
	{
		return unit.VisualClass switch
		{
			"spitter" => true,
			"walker" => true,
			"runner" => true,
			"saboteur" => true,
			"brute" => true,
			"crusher" => true,
			"splitter" => true,
			"boss" => true,
			_ => false
		};
	}

	private static bool ShouldPrioritizeObjectiveRaid(Unit unit, Unit directTarget)
	{
		return unit.Team == Team.Enemy &&
			unit.VisualClass == "saboteur" &&
			(directTarget == null || !unit.CanAttack(directTarget));
	}

	private void SimulateEnemyContactPressure(Unit unit, float delta)
	{
		if (!IsInstanceValid(_activeEndlessContactActor) || _activeEndlessContact == null)
		{
			return;
		}

		var contactPosition = _activeEndlessContactActor.Position;
		var contactRadius = ResolveEndlessContactAttackRadius();
		if (unit.CanAttackPosition(contactPosition, contactRadius))
		{
			if (unit.TryBeginAttackPosition(contactPosition, contactRadius))
			{
				if (unit.UsesProjectile)
				{
					SpawnContactPressureProjectile(unit);
				}
				else
				{
					var appliedDamage = _activeEndlessContactActor.ApplyPressureDamage(ResolveEnemyContactAttackDamage(unit));
					RegisterEndlessContactPressure(appliedDamage);
					if (appliedDamage > 0.05f)
					{
						SpawnEffect(contactPosition, unit.Tint, 6f, 18f + (appliedDamage * 0.2f), 0.16f, false);
						SpawnFloatText(contactPosition + new Vector2(_rng.RandfRange(-8f, 8f), -10f), $"-{Mathf.RoundToInt(appliedDamage)}", unit.Tint.Lightened(0.22f), 0.46f);
					}
				}
			}

			return;
		}

		unit.MoveToward(
			contactPosition,
			delta,
			BattlefieldLeft,
			BattlefieldRight,
			BattlefieldTop + SpawnVerticalPadding,
			BattlefieldBottom - SpawnVerticalPadding);
	}

	private Unit FindClosestEnemy(Unit source)
	{
		Unit bestTarget = null;
		var bestPriority = int.MaxValue;
		var bestDistance = float.MaxValue;

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team == source.Team)
			{
				continue;
			}

			if (!source.IsInAggroRange(candidate))
			{
				continue;
			}

			var priority = ResolveTargetPriority(source, candidate);
			var distance = source.Position.DistanceSquaredTo(candidate.Position);
			if (priority < bestPriority || (priority == bestPriority && distance < bestDistance))
			{
				bestPriority = priority;
				bestDistance = distance;
				bestTarget = candidate;
			}
		}

		return bestTarget;
	}

	private static int ResolveTargetPriority(Unit source, Unit candidate)
	{
		if (source.Team != Team.Player)
		{
			return 5;
		}

		return candidate.VisualClass switch
		{
			"jammer" => 0,
			"howler" => 1,
			"spitter" => 2,
			"saboteur" => source.VisualClass is "fighter" or "shield" or "skirmisher" ? 1 : 3,
			"boss" => 3,
			_ => 5
		};
	}

	private void ApplySplashDamage(Team attackerTeam, Vector2 center, float damage, float radius, Color color)
	{
		if (damage <= 0.05f || radius <= 0.05f)
		{
			return;
		}

		foreach (var candidate in _units)
		{
			if (candidate.IsDead || candidate.Team == attackerTeam)
			{
				continue;
			}

			if (candidate.Position.DistanceTo(center) > radius)
			{
				continue;
			}

			var appliedDamage = candidate.TakeDamage(damage);
			SpawnDamageFeedback(candidate.Position, appliedDamage, color);
		}
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

	private void CleanupDeadUnits()
	{
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
			}

			TriggerDeathBurst(deadUnit);
			TriggerSpawnOnDeath(deadUnit);
			SpawnEffect(deadUnit.Position, deadUnit.Tint, 8f, 24f, 0.22f);
			_units.RemoveAt(i);
			deadUnit.QueueFree();
		}
	}

	private void TriggerDeathBurst(Unit deadUnit)
	{
		if (deadUnit.DeathBurstDamage <= 0f || deadUnit.DeathBurstRadius <= 0f)
		{
			return;
		}

		SpawnEffect(
			deadUnit.Position,
			deadUnit.Tint.Lightened(0.15f),
			12f,
			deadUnit.DeathBurstRadius,
			0.26f,
			false);

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

	private void SpawnEffect(
		Vector2 position,
		Color color,
		float startRadius,
		float endRadius,
		float lifetime,
		bool filled = true,
		BattleEffectStyle style = BattleEffectStyle.Pulse)
	{
		var effect = new BattleEffect();
		effect.Position = position;
		effect.Setup(color, startRadius, endRadius, lifetime, filled, style);
		AddChild(effect);
	}

	private void SpawnDamageFeedback(Vector2 position, float damage, Color color)
	{
		if (damage <= 0.05f)
		{
			return;
		}

		SpawnEffect(position, color.Lightened(0.12f), 4f, 18f + (damage * 0.18f), 0.14f, false);
		SpawnFloatText(
			position + new Vector2(_rng.RandfRange(-6f, 6f), -8f),
			$"-{Mathf.RoundToInt(damage)}",
			color.Lightened(0.3f),
			0.46f);
		AudioDirector.Instance?.PlayImpact(damage);
	}

	private void SpawnFloatText(Vector2 position, string text, Color color, float lifetime = 0.5f)
	{
		var floatText = new BattleFloatText();
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

	private string ApplySpellEffect(SpellDefinition definition, Vector2 targetPosition)
	{
		return definition.EffectType switch
		{
			"fireball" => ApplyFireballSpell(definition, targetPosition),
			"heal" => ApplyHealSpell(definition, targetPosition),
			"frost_burst" => ApplyFrostBurstSpell(definition, targetPosition),
			"lightning_strike" => ApplyLightningStrikeSpell(definition, targetPosition),
			"barrier_ward" => ApplyBarrierWardSpell(definition, targetPosition),
			_ => "The spell fizzled without a scripted effect."
		};
	}

	private string ApplyFireballSpell(SpellDefinition definition, Vector2 targetPosition)
	{
		var color = definition.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, definition.Radius, Team.Enemy);
		var hits = 0;
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 14f, definition.Radius, 0.26f, false, BattleEffectStyle.Fireburst);
		SpawnFloatText(targetPosition + new Vector2(0f, -18f), "FIREBALL", color.Lightened(0.22f), 0.56f);

		foreach (var target in targets)
		{
			var appliedDamage = target.TakeDamage(definition.Power);
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

	private string ApplyHealSpell(SpellDefinition definition, Vector2 targetPosition)
	{
		var color = definition.GetTint();
		var allies = GetLivingUnitsInRadius(targetPosition, definition.Radius, Team.Player);
		var healedUnits = 0;
		var totalHealing = 0f;

		SpawnEffect(targetPosition, color, 12f, definition.Radius, 0.28f, false, BattleEffectStyle.HealBloom);
		SpawnFloatText(targetPosition + new Vector2(0f, -18f), "HEAL", color.Lightened(0.18f), 0.56f);

		foreach (var ally in allies)
		{
			var healed = ally.Heal(definition.Power);
			if (healed <= 0.05f)
			{
				continue;
			}

			healedUnits++;
			totalHealing += healed;
			SpawnEffect(ally.Position, color.Lightened(0.08f), 8f, 22f, 0.18f, false, BattleEffectStyle.HealBloom);
			SpawnFloatText(ally.Position + new Vector2(0f, -24f), $"+{Mathf.RoundToInt(healed)}", color.Lightened(0.24f), 0.46f);
		}

		var repaired = RepairBusByAmount(definition.SecondaryPower);
		return
			$"Heal restored {Mathf.RoundToInt(totalHealing)} across {healedUnits} allies" +
			(repaired > 0.05f ? $" and repaired {Mathf.RoundToInt(repaired)} war wagon hull." : ".");
	}

	private string ApplyFrostBurstSpell(SpellDefinition definition, Vector2 targetPosition)
	{
		var color = definition.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, definition.Radius, Team.Enemy);
		var slowed = 0;
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 14f, definition.Radius, 0.3f, false, BattleEffectStyle.FrostBurst);
		SpawnFloatText(targetPosition + new Vector2(0f, -18f), "FROST", color.Lightened(0.24f), 0.58f);

		foreach (var target in targets)
		{
			var appliedDamage = target.TakeDamage(definition.Power);
			target.ApplyTemporarySpeedModifier(0.62f, definition.Duration);
			if (appliedDamage > 0.05f)
			{
				totalDamage += appliedDamage;
				SpawnDamageFeedback(target.Position, appliedDamage, color);
			}

			slowed++;
		}

		return slowed > 0
			? $"Frost Burst slowed {slowed} enemies for {definition.Duration:0.0}s and dealt {Mathf.RoundToInt(totalDamage)} damage."
			: "Frost Burst failed to catch an enemy pack.";
	}

	private string ApplyLightningStrikeSpell(SpellDefinition definition, Vector2 targetPosition)
	{
		var color = definition.GetTint();
		var targets = GetLivingUnitsInRadius(targetPosition, definition.Radius, Team.Enemy)
			.OrderBy(unit => unit.Position.DistanceSquaredTo(targetPosition))
			.Take(3)
			.ToArray();
		var totalDamage = 0f;

		SpawnEffect(targetPosition, color, 10f, 24f, 0.2f, false, BattleEffectStyle.LightningStrike);
		SpawnFloatText(targetPosition + new Vector2(0f, -18f), "LIGHTNING", color.Lightened(0.18f), 0.56f);

		for (var i = 0; i < targets.Length; i++)
		{
			var scale = i switch
			{
				0 => 1f,
				1 => 0.78f,
				_ => 0.58f
			};
			var appliedDamage = targets[i].TakeDamage(definition.Power * scale);
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

	private string ApplyBarrierWardSpell(SpellDefinition definition, Vector2 targetPosition)
	{
		var color = definition.GetTint();
		var allies = GetLivingUnitsInRadius(targetPosition, definition.Radius, Team.Player);
		var warded = 0;

		SpawnEffect(targetPosition, color, 12f, definition.Radius, 0.28f, false, BattleEffectStyle.WardSigil);
		SpawnFloatText(targetPosition + new Vector2(0f, -18f), "WARD", color.Lightened(0.18f), 0.56f);

		foreach (var ally in allies)
		{
			ally.ApplyTemporaryDefenseModifier(definition.Power, definition.Duration);
			warded++;
			SpawnEffect(ally.Position, color.Lightened(0.05f), 6f, 18f, 0.18f, false, BattleEffectStyle.WardSigil);
		}

		return warded > 0
			? $"Barrier Ward covered {warded} allies for {definition.Duration:0.0}s."
			: "Barrier Ward found no allied units in the target lane.";
	}

	private Rect2 OffsetRect(Rect2 rect, Vector2 offset)
	{
		return new Rect2(rect.Position + offset, rect.Size);
	}

	private Vector2 GetBaseShakeOffset(bool playerBase)
	{
		var flashTimer = playerBase ? _playerBaseFlashTimer : _enemyBaseFlashTimer;
		if (flashTimer <= 0f)
		{
			return Vector2.Zero;
		}

		var strength = Mathf.Clamp(flashTimer / 0.22f, 0f, 1f) * 4f;
		var phase = (_elapsed * 56f) + (playerBase ? 0f : 1.8f);
		return new Vector2(
			Mathf.Sin(phase) * strength,
			Mathf.Cos(phase * 1.37f) * strength * 0.38f);
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

	private void DrawBaseHealthMeter(Vector2 center, float width, float healthRatio, Color frameColor, Color fillColor)
	{
		var origin = center - new Vector2(width * 0.5f, 0f);
		DrawRect(new Rect2(origin, new Vector2(width, 8f)), new Color(0f, 0f, 0f, 0.55f), true);
		DrawRect(new Rect2(origin + new Vector2(1f, 1f), new Vector2((width - 2f) * healthRatio, 6f)), fillColor, true);
		DrawRect(new Rect2(origin, new Vector2(width, 8f)), frameColor, false, 2f);
	}

	private void DrawDamageSmoke(Vector2 origin, float healthRatio, Color sourceColor)
	{
		if (healthRatio >= 0.7f)
		{
			return;
		}

		var smokeStrength = Mathf.Clamp((0.7f - healthRatio) / 0.7f, 0f, 1f);
		var plumeOffset = Mathf.Sin(_elapsed * 1.8f) * 6f;
		DrawCircle(origin + new Vector2(plumeOffset, -12f), 10f + (smokeStrength * 5f), new Color(sourceColor.Darkened(0.7f), 0.18f + (smokeStrength * 0.12f)));
		DrawCircle(origin + new Vector2(-4f + (plumeOffset * 0.4f), -24f), 14f + (smokeStrength * 6f), new Color(0f, 0f, 0f, 0.12f + (smokeStrength * 0.12f)));
	}

	private void DrawEndlessFieldEvent()
	{
		if (!IsEndlessMode || _activeEndlessFieldEvent == null)
		{
			return;
		}

		var alpha = Mathf.Clamp(_activeEndlessFieldEvent.Remaining / 4f, 0.18f, 0.65f);
		for (var i = 0; i < _activeEndlessFieldEvent.Anchors.Length; i++)
		{
			var anchor = _activeEndlessFieldEvent.Anchors[i];
			DrawArc(
				anchor,
				_activeEndlessFieldEvent.Radius,
				0f,
				Mathf.Tau,
				24,
				new Color(_activeEndlessFieldEvent.Color, alpha),
				3f);
			DrawCircle(anchor, 7f, new Color(_activeEndlessFieldEvent.Color, alpha + 0.1f));
		}
	}

	private void DrawEndlessContactEvent()
	{
		if (!IsEndlessMode || _activeEndlessContact == null)
		{
			return;
		}

		var definition = _activeEndlessContact.Definition;
		var progressRatio = Mathf.Clamp(_activeEndlessContact.Progress / Mathf.Max(0.01f, definition.TargetSeconds), 0f, 1f);
		var baseAlpha = _activeEndlessContact.Completed
			? 0.72f
			: _activeEndlessContact.Failed
				? 0.2f
				: 0.38f + (Mathf.Sin(_elapsed * 4.4f) * 0.08f);
		var drawColor = _activeEndlessContact.Color
			.Lightened(_activeEndlessContact.PlayerInside ? 0.08f : 0f)
			.Darkened(_activeEndlessContact.EnemyInside ? 0.12f : 0f);
		var color = new Color(drawColor, Mathf.Clamp(baseAlpha, 0.16f, 0.78f));
		var anchor = _activeEndlessContact.Anchor;
		var fillAlpha = _activeEndlessContact.Completed
			? 0.16f
			: _activeEndlessContact.Failed
				? 0.05f
				: _activeEndlessContact.EnemyInside
					? 0.08f
					: 0.12f;

		DrawCircle(anchor, definition.Radius, new Color(drawColor, fillAlpha));

		DrawArc(anchor, definition.Radius, 0f, Mathf.Tau, 32, color, 3f);
		DrawCircle(anchor, 8f, color.Lightened(0.1f));

		if (!_activeEndlessContact.Failed)
		{
			var progressRadius = Mathf.Lerp(18f, definition.Radius - 8f, progressRatio);
			DrawArc(anchor, progressRadius, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + (Mathf.Tau * progressRatio), 32, color.Lightened(0.18f), 5f);
		}

		DrawLine(anchor + new Vector2(-10f, 0f), anchor + new Vector2(10f, 0f), color, 2f, true);
		DrawLine(anchor + new Vector2(0f, -10f), anchor + new Vector2(0f, 10f), color, 2f, true);
	}

	private Color ResolveDeployButtonTint(UnitDefinition definition, bool isReady, bool hasCourage, bool armed)
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

		return armed
			? tint.Lightened(0.25f)
			: tint.Lerp(Colors.White, 0.25f);
	}

	private Color ResolveSpellButtonTint(SpellDefinition definition, bool isReady, bool hasCourage, bool armed)
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
			$"HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Range {stats.AttackRange:0.#}\n" +
			$"Deploy CD {effectiveDeployCooldown:0.#}s" +
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

	private float ResolvePlayerSpellCooldown(SpellDefinition definition)
	{
		var cooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(definition.Cooldown);
		if (IsChallengeMode)
		{
			cooldown *= _challengeMutator.DeployCooldownScale;
		}

		return Mathf.Max(2f, cooldown);
	}

	private string BuildStageMissionIntelText()
	{
		var directiveText = BuildCampaignDirectiveBattleText();
		if (_stageMissions.Count == 0)
		{
			return string.IsNullOrWhiteSpace(directiveText) ? "" : $"{directiveText}\n";
		}

		var mission = _stageMissions.FirstOrDefault(candidate => !candidate.Completed && !candidate.Failed);
		if (mission == null)
		{
			return
				(string.IsNullOrWhiteSpace(directiveText) ? "" : $"{directiveText}\n") +
				"Mission event: all authored battlefield objectives are resolved.\n";
		}

		var title = StageMissionEvents.ResolveTitle(mission.Definition);
		if (!mission.Started)
		{
			return
				(string.IsNullOrWhiteSpace(directiveText) ? "" : $"{directiveText}\n") +
				$"Mission event standby: {title} arms in {Mathf.Max(0f, mission.Definition.StartTime - _elapsed):0.0}s.\n";
		}

		return
			(string.IsNullOrWhiteSpace(directiveText) ? "" : $"{directiveText}\n") +
			$"Mission event active: {title}  |  {BuildStageMissionProgressText(mission)}\n";
	}

	private string BuildStageMissionEventText()
	{
		if (_stageMissions.Count == 0)
		{
			return "";
		}

		var lines = new List<string>
		{
			"Battlefield events:"
		};

		foreach (var mission in _stageMissions)
		{
			var prefix = mission.Completed
				? "[OK]"
				: mission.Failed
					? "[X]"
					: mission.Started
						? "[..]"
						: "[--]";
			lines.Add($"{prefix} {StageMissionEvents.ResolveTitle(mission.Definition)}  |  {BuildStageMissionProgressText(mission)}");
		}

		return string.Join("\n", lines);
	}

	private string BuildStageMissionProgressText(StageMissionState mission)
	{
		if (mission == null)
		{
			return "No mission event.";
		}

		if (!mission.Started)
		{
			return $"Arms at {mission.Definition.StartTime:0.0}s";
		}

		var progress = mission.Progress;
		var target = Mathf.Max(1f, mission.Definition.TargetSeconds);
		return mission.Definition.NormalizedType switch
		{
			"ritual_site" => $"Cleanse {progress:0.0}/{target:0.0}s at the shrine circle",
			"relic_escort" => $"Escort window {progress:0.0}/{target:0.0}s around the relic route",
			"gate_breach" => $"Breach timer {progress:0.0}/{target:0.0}s on the wall charge",
			_ => $"{progress:0.0}/{target:0.0}s secured"
		};
	}

	private string BuildWaveIntelText()
	{
		if (IsEndlessMode)
		{
			if (_spawnDirector.EndlessCheckpointPending)
			{
				var remainingEnemies = CountTeamUnits(Team.Enemy) + _spawnDirector.PendingSpawnCount;
				var checkpointLabel = IsRouteForkCheckpoint()
					? "route fork"
					: "upgrade";
				if (_spawnDirector.EndlessBossCheckpointPending)
				{
					var bossCheckpoint = EndlessBossCheckpointCatalog.GetForRoute(_activeRouteId);
					return remainingEnemies > 0
						? $"Boss checkpoint active: {bossCheckpoint.Title}. Clear {remainingEnemies} remaining enemies to open the {checkpointLabel} draft.\n{BuildEndlessBossCheckpointText()}"
						: $"Boss checkpoint secured: {bossCheckpoint.Title} broken. Choose a {checkpointLabel} to resume the caravan.\n{BuildEndlessBossCheckpointCheckpointSummary()}";
				}

				return remainingEnemies > 0
					? $"Checkpoint wave active: clear {remainingEnemies} remaining enemies to open the {checkpointLabel} draft."
					: $"Checkpoint ready: choose a {checkpointLabel} to resume the caravan.";
			}

			var endlessCountdown = Mathf.Max(0f, _spawnDirector.NextEndlessWaveTime - _elapsed);
			return
				$"Endless intel: {ResolveRouteLabel(_activeRouteId)} surge route  |  Path: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}\n" +
				$"Current wave: {_spawnDirector.EndlessWaveNumber}  |  Next surge in {endlessCountdown:0.0}s  |  Queued: {_spawnDirector.PendingSpawnCount}\n" +
				$"{BuildActiveEnemyPressureText()}\n" +
				$"Pressure profile: {BuildEndlessPressureText()}\n" +
				$"Segment event: {_spawnDirector.EndlessSegmentEventLabel}\n" +
				$"{BuildEndlessBossCheckpointText()}\n" +
			$"{BuildEndlessDirectiveText()}\n" +
			$"{BuildEndlessContactText()}\n" +
			$"Contact tradeoff: {_endlessContactTradeoffLabel}\n" +
			$"Contact telemetry: {BuildEndlessContactTelemetryText()}\n" +
			$"Battlefield event: {_endlessBattlefieldEventLabel}\n" +
			$"Caravan support: {_endlessSupportEventLabel}";
		}

		var modifierSummary = $"Modifiers: {StageModifiers.BuildInlineSummary(_stageData)}";
		var hazardSummary = BuildStageHazardIntelText();
		var missionSummary = BuildStageMissionIntelText();
		var challengeHeaderText = IsChallengeMode
			? $"{BuildChallengeMutatorText()}\n{BuildOnlineRoomRaceText()}{BuildChallengeGhostText()}\n"
			: "";

		if (!_spawnDirector.UsesScriptedWaves)
		{
			return $"{modifierSummary}\n{hazardSummary}\n{missionSummary}{challengeHeaderText}Encounter intel: dynamic pressure spawns are active on this route.";
		}

		if (!_spawnDirector.TryGetNextScriptedWave(out var nextWave))
		{
			var suffix = _spawnDirector.PendingSpawnCount > 0
				? $"Encounter intel: {_spawnDirector.PendingSpawnCount} enemies still queued from the active scripted wave."
				: "Encounter intel: all scripted waves have deployed. Finish the route.";
			return $"{modifierSummary}\n{hazardSummary}\n{missionSummary}{challengeHeaderText}{suffix}";
		}

		var countdown = Mathf.Max(0f, nextWave.TriggerTime - _elapsed);
		var label = string.IsNullOrWhiteSpace(nextWave.Label)
			? $"Wave {_spawnDirector.NextScriptedWaveIndex + 1}"
			: nextWave.Label;
			return
				$"{modifierSummary}\n" +
				$"{hazardSummary}\n" +
				missionSummary +
				challengeHeaderText +
				$"{BuildActiveEnemyPressureText()}\n" +
				$"Next wave in {countdown:0.0}s: {label}\n" +
				$"{BuildWaveEntrySummary(nextWave)}\n" +
				$"{StageEncounterIntel.BuildWavePressureSummary(nextWave)}";
	}

	private string BuildChallengeMutatorText()
	{
		if (!IsChallengeMode)
		{
			return "";
		}

		if (_challengeMutator.SignalJamIntervalSeconds <= 0.05f)
		{
			return $"Mutator: {_challengeMutator.Title}";
		}

		var status = _enemySignalJamTimer > 0.05f
			? $"Blackout active ({_enemySignalJamTimer:0.0}s jam)"
			: $"Next blackout {_challengeMutatorNextJamTimer:0.0}s";
		return
			$"Mutator: {_challengeMutator.Title}  |  {status}  |  Cadence {_challengeMutator.SignalJamIntervalSeconds:0.0}s";
	}

	private string BuildOnlineRoomRaceText()
	{
		if (!IsOnlineRoomMode)
		{
			return "";
		}

		if (AppLifecycleService.Instance?.ShouldPauseOnlineRoomTraffic == true)
		{
			return $"{_onlineRoomRaceSummary}  |  room sync paused\nResume the app to refresh room telemetry.\n";
		}

		if (_onlineRoomStartBarrierActive)
		{
			return $"{_onlineRoomRaceSummary}  |  Launch sync {_onlineRoomStartCountdownRemaining:0.0}s\n";
		}

		return $"{_onlineRoomRaceSummary}  |  Room race live\n{BuildOnlineRoomMonitorText()}\n";
	}

	private string BuildOnlineRoomMonitorText()
	{
		var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (roomSnapshot == null || !roomSnapshot.HasRoom)
		{
			return "Room monitor: waiting for joined-room telemetry.";
		}

		var lines = new List<string>
		{
			MultiplayerRoomFormatter.BuildCompactRacePaceSummary(roomSnapshot)
		};
		var localPeer = roomSnapshot.Peers.FirstOrDefault(peer => peer.IsLocalPlayer) ??
			roomSnapshot.Peers.FirstOrDefault(peer =>
				!string.IsNullOrWhiteSpace(roomSnapshot.LocalCallsign) &&
				peer.Label.Equals(roomSnapshot.LocalCallsign, StringComparison.OrdinalIgnoreCase));
		if (localPeer != null && !string.IsNullOrWhiteSpace(localPeer.MonitorText))
		{
			lines.Add(localPeer.MonitorText);
		}

		foreach (var peer in roomSnapshot.Peers
			.Where(peer => localPeer == null || !peer.Label.Equals(localPeer.Label, StringComparison.OrdinalIgnoreCase))
			.OrderBy(peer => peer.MonitorRank)
			.Take(2))
		{
			if (!string.IsNullOrWhiteSpace(peer.MonitorText))
			{
				lines.Add(peer.MonitorText);
			}
		}

		if (lines.Count == 0)
		{
			return "Room monitor: waiting for peer activity.";
		}

		return string.Join("\n", lines);
	}

	private bool HasChallengeGhostRun()
	{
		return IsChallengeMode &&
			_challengeGhostRun != null &&
			_challengeGhostRun.Deployments != null &&
			_challengeGhostRun.Deployments.Count > 0;
	}

	private string BuildChallengeGhostText()
	{
		var summary = GameState.Instance.BuildChallengeGhostSummary(_challengeGhostRun);
		if (!HasChallengeGhostRun())
		{
			return summary;
		}

		var ghostDeploysElapsed = CountChallengeGhostDeploymentsElapsed(_elapsed);
		var deployDelta = _playerDeployments - ghostDeploysElapsed;
		var paceLabel = deployDelta switch
		{
			> 0 => $"Deploy pace: {FormatSignedInt(deployDelta)} ahead of the ghost timeline",
			< 0 => $"Deploy pace: {FormatSignedInt(deployDelta)} behind the ghost timeline",
			_ => "Deploy pace: matched to the ghost timeline"
		};

		if (_challengeGhostNextIndex >= _challengeGhostRun.Deployments.Count)
		{
			return $"{summary}\n{paceLabel}\nGhost timeline: benchmark run has finished all recorded drops.";
		}

		var nextDeployment = _challengeGhostRun.Deployments[_challengeGhostNextIndex];
		var unit = GameData.GetUnit(nextDeployment.UnitId);
		var remaining = Mathf.Max(0f, nextDeployment.TimeSeconds - _elapsed);
		return $"{summary}\n{paceLabel}\nNext ghost deploy in {remaining:0.0}s: {unit.DisplayName}@{nextDeployment.LanePercent}%";
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
		SpawnFloatText(markerPosition + new Vector2(0f, -22f), $"GHOST {unit.DisplayName.ToUpperInvariant()}", ghostColor.Lightened(0.12f), 0.48f);
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

	private void InitializeStageHazards()
	{
		_stageHazards.Clear();
		if (_stageData?.Hazards == null)
		{
			return;
		}

		foreach (var hazard in _stageData.Hazards)
		{
			if (hazard == null)
			{
				continue;
			}

			var anchor = new Vector2(
				Mathf.Lerp(BattlefieldLeft + 48f, BattlefieldRight - 48f, Mathf.Clamp(hazard.XRatio, 0f, 1f)),
				Mathf.Lerp(BattlefieldTop + 32f, BattlefieldBottom - 32f, Mathf.Clamp(hazard.YRatio, 0f, 1f)));
			_stageHazards.Add(new StageHazardState(
				hazard,
				anchor,
				Mathf.Max(1.5f, hazard.StartTime),
				hazard.GetTint()));
		}
	}

	private void InitializeStageMissions()
	{
		_stageMissions.Clear();
		if (IsEndlessMode || _stageData?.MissionEvents == null)
		{
			return;
		}

		foreach (var mission in _stageData.MissionEvents)
		{
			if (mission == null || string.IsNullOrWhiteSpace(mission.Type))
			{
				continue;
			}

			var anchor = new Vector2(
				Mathf.Lerp(BattlefieldLeft + 64f, BattlefieldRight - 64f, Mathf.Clamp(mission.XRatio, 0f, 1f)),
				Mathf.Lerp(BattlefieldTop + 48f, BattlefieldBottom - 48f, Mathf.Clamp(mission.YRatio, 0f, 1f)));
			_stageMissions.Add(new StageMissionState(mission, anchor, mission.GetTint()));
		}
	}

	private void UpdateStageMissions(float delta)
	{
		if (IsEndlessMode || _stageMissions.Count == 0 || _battleEnded)
		{
			return;
		}

		foreach (var mission in _stageMissions)
		{
			if (!mission.Started)
			{
				if (_elapsed + 0.001f >= mission.Definition.StartTime)
				{
					StartStageMission(mission);
				}
				else
				{
					continue;
				}
			}

			if (!CanInteractWithStageMission(mission))
			{
				continue;
			}

			var playerInside = HasTeamUnitInRadius(Team.Player, mission.Anchor, mission.Definition.Radius);
			var enemyInside = HasTeamUnitInRadius(Team.Enemy, mission.Anchor, mission.Definition.Radius);
			mission.PlayerInside = playerInside;
			mission.EnemyInside = enemyInside;

			if (playerInside && !enemyInside)
			{
				mission.Actor.Repair(ResolveStageMissionPresenceRepairRate(mission.Definition) * delta);
			}
			else if (enemyInside)
			{
				mission.Actor.ApplyPressureDamage(ResolveStageMissionPressureRate(mission.Definition) * delta);
			}

			switch (mission.Definition.NormalizedType)
			{
				case "ritual_site":
					if (playerInside && !enemyInside)
					{
						mission.Progress += delta * 1.18f;
					}
					else if (playerInside)
					{
						mission.Progress += delta * 0.44f;
					}
					else if (enemyInside)
					{
						mission.Progress -= delta * 0.72f;
					}
					else
					{
						mission.Progress -= delta * 0.22f;
					}
					break;
				case "relic_escort":
					if (!enemyInside)
					{
						mission.Progress += playerInside ? delta * 1.14f : delta * 0.68f;
					}
					else
					{
						mission.Progress -= delta * 0.94f;
					}
					break;
				case "gate_breach":
					if (playerInside && !enemyInside)
					{
						mission.Progress += delta * 1.28f;
					}
					else if (playerInside)
					{
						mission.Progress += delta * 0.52f;
					}
					else if (enemyInside)
					{
						mission.Progress -= delta * 0.86f;
					}
					else
					{
						mission.Progress -= delta * 0.34f;
					}
					break;
			}

			mission.Progress = Mathf.Clamp(
				mission.Progress,
				0f,
				Mathf.Max(1f, mission.Definition.TargetSeconds));
			mission.Actor.UpdateState(
				mission.Progress / Mathf.Max(1f, mission.Definition.TargetSeconds),
				playerInside,
				enemyInside,
				false,
				false);

			if (mission.Actor.Health <= 0.01f)
			{
				FailStageMission(mission, $"{StageMissionEvents.ResolveTitle(mission.Definition)} collapsed before the caravan secured it.");
				continue;
			}

			TryTriggerStageMissionSupportMoment(mission);

			if (mission.Progress + 0.001f >= mission.Definition.TargetSeconds)
			{
				CompleteStageMission(mission);
			}
		}
	}

	private void StartStageMission(StageMissionState mission)
	{
		if (mission.Started)
		{
			return;
		}

		mission.Started = true;
		mission.Actor = new EndlessContactActor();
		mission.Actor.Position = mission.Anchor;
		mission.Actor.Setup(
			mission.Definition.NormalizedType,
			mission.Color,
			mission.Definition.Radius,
			ResolveStageMissionMaxHealth(mission.Definition));
		mission.Actor.UpdateState(0f, false, false, false, false);
		AddChild(mission.Actor);
		SetStatus($"{StageMissionEvents.ResolveTitle(mission.Definition)} active. {StageMissionEvents.ResolveSummary(mission.Definition)}");
	}

	private bool CanInteractWithStageMission(StageMissionState mission)
	{
		return mission != null &&
			mission.Started &&
			!mission.Completed &&
			!mission.Failed &&
			IsInstanceValid(mission.Actor);
	}

	private void TryTriggerStageMissionSupportMoment(StageMissionState mission)
	{
		if (mission.SupportMomentTriggered ||
			mission.Progress + 0.001f < (Mathf.Max(1f, mission.Definition.TargetSeconds) * 0.5f))
		{
			return;
		}

		mission.SupportMomentTriggered = true;
		var title = StageMissionEvents.ResolveTitle(mission.Definition);
		switch (mission.Definition.NormalizedType)
		{
			case "ritual_site":
				_courage = Mathf.Min(_maxCourage, _courage + 6f);
				SpawnFloatText(mission.Anchor + new Vector2(0f, -56f), "WARD FLARE", mission.Color.Lightened(0.2f), 0.58f);
				SetStatus($"{title} flared and steadied the caravan. Courage surged.");
				break;
			case "relic_escort":
				RepairBusByRatio(0.03f);
				SpawnFloatText(mission.Anchor + new Vector2(0f, -56f), "RELIC PASS", mission.Color.Lightened(0.2f), 0.58f);
				SetStatus($"{title} reached cover and bought the war wagon time to patch the line.");
				break;
			case "gate_breach":
				DamageEnemyBaseByRatio(0.05f, mission.Color, "CRACKED");
				SpawnFloatText(mission.Anchor + new Vector2(0f, -56f), "WALL CRACK", mission.Color.Lightened(0.2f), 0.58f);
				SetStatus($"{title} opened the first cracks in the gatehouse.");
				break;
		}

		SpawnEffect(mission.Anchor, mission.Color.Lightened(0.08f), 10f, mission.Definition.Radius * 0.58f, 0.22f, false);
	}

	private void CompleteStageMission(StageMissionState mission)
	{
		if (mission.Completed || mission.Failed)
		{
			return;
		}

		mission.Completed = true;
		if (IsInstanceValid(mission.Actor))
		{
			mission.Actor.Repair(mission.Actor.MaxHealth);
			mission.Actor.UpdateState(1f, true, false, true, false);
		}

		SpawnEffect(mission.Anchor, mission.Color, 12f, mission.Definition.Radius * 0.72f, 0.28f, false);
		switch (mission.Definition.NormalizedType)
		{
			case "ritual_site":
				_courage = Mathf.Min(_maxCourage, _courage + 12f);
				_deck.ReduceCooldowns(0.8f);
				_spellDeck.ReduceCooldowns(0.8f);
				SpawnFloatText(mission.Anchor + new Vector2(0f, -28f), "RITE SECURED", mission.Color.Lightened(0.18f), 0.64f);
				break;
			case "relic_escort":
				RepairBusByRatio(0.06f);
				SpawnSupportUnit(ResolveStageMissionSupportUnitId());
				SpawnFloatText(mission.Anchor + new Vector2(0f, -28f), "RELICS THROUGH", mission.Color.Lightened(0.18f), 0.64f);
				break;
			case "gate_breach":
				DamageEnemyBaseByRatio(0.18f, mission.Color, "GATE BREACHED");
				SpawnFloatText(mission.Anchor + new Vector2(0f, -28f), "BREACH LANDED", mission.Color.Lightened(0.18f), 0.64f);
				break;
		}

		SetStatus($"{StageMissionEvents.ResolveTitle(mission.Definition)} secured. {StageMissionEvents.ResolveRewardSummary(mission.Definition)}");
	}

	private void FailStageMission(StageMissionState mission, string statusText = null)
	{
		if (mission.Completed || mission.Failed)
		{
			return;
		}

		mission.Failed = true;
		if (IsInstanceValid(mission.Actor))
		{
			mission.Actor.UpdateState(
				mission.Progress / Mathf.Max(1f, mission.Definition.TargetSeconds),
				mission.PlayerInside,
				mission.EnemyInside,
				false,
				true);
		}

		switch (mission.Definition.NormalizedType)
		{
			case "ritual_site":
				_courage = Mathf.Max(0f, _courage - 12f);
				_deck.IncreaseCooldowns(0.8f);
				_spellDeck.IncreaseCooldowns(0.8f);
				SpawnFloatText(mission.Anchor + new Vector2(0f, -28f), "RITE LOST", new Color("ffb4a2"), 0.62f);
				break;
			case "relic_escort":
				DamageBusByRatio(0.08f, mission.Color, "ESCORT LOST");
				break;
			case "gate_breach":
				RepairEnemyBaseByRatio(0.08f, mission.Color, "GATE RESET");
				break;
		}

		var baseStatus = statusText ?? $"{StageMissionEvents.ResolveTitle(mission.Definition)} was lost before the route was secure.";
		SetStatus($"{baseStatus} {StageMissionEvents.ResolvePenaltySummary(mission.Definition)}");
	}

	private float ResolveStageMissionMaxHealth(StageMissionEventDefinition mission)
	{
		return mission.NormalizedType switch
		{
			"ritual_site" => 84f,
			"relic_escort" => 104f,
			"gate_breach" => 116f,
			_ => 88f
		};
	}

	private static float ResolveStageMissionPresenceRepairRate(StageMissionEventDefinition mission)
	{
		return mission.NormalizedType switch
		{
			"ritual_site" => 2.1f,
			"relic_escort" => 1.9f,
			"gate_breach" => 1.7f,
			_ => 1.8f
		};
	}

	private static float ResolveStageMissionPressureRate(StageMissionEventDefinition mission)
	{
		return mission.NormalizedType switch
		{
			"ritual_site" => 7.8f,
			"relic_escort" => 8.6f,
			"gate_breach" => 9.4f,
			_ => 8f
		};
	}

	private string ResolveStageMissionSupportUnitId()
	{
		if (GameState.Instance.IsUnitUnlocked(GameData.PlayerCoordinatorId))
		{
			return GameData.PlayerCoordinatorId;
		}

		if (GameState.Instance.IsUnitUnlocked(GameData.PlayerDefenderId))
		{
			return GameData.PlayerDefenderId;
		}

		return GameData.PlayerBrawlerId;
	}

	private void UpdateStageHazards()
	{
		foreach (var hazard in _stageHazards)
		{
			var warningStart = hazard.NextTriggerTime - Mathf.Max(0.35f, hazard.Definition.WarningDuration);
			if (!hazard.WarningIssued &&
				_elapsed + 0.001f >= warningStart &&
				_elapsed + 0.001f < hazard.NextTriggerTime)
			{
				hazard.WarningIssued = true;
				AudioDirector.Instance?.PlayHazardWarning();
				SetStatus($"Hazard priming: {ResolveStageHazardLabel(hazard.Definition)}.");
			}

			if (_elapsed + 0.001f < hazard.NextTriggerTime)
			{
				continue;
			}

			TriggerStageHazard(hazard);
			hazard.NextTriggerTime += Mathf.Max(2.5f, hazard.Definition.Interval);
			hazard.WarningIssued = false;
		}
	}

	private void TriggerStageHazard(StageHazardState hazard)
	{
		AudioDirector.Instance?.PlayHazardStrike();
		SpawnEffect(
			hazard.Anchor,
			hazard.Color.Lightened(0.08f),
			12f,
			Mathf.Max(26f, hazard.Definition.Radius),
			0.24f,
			false);
		SpawnFloatText(
			hazard.Anchor + new Vector2(0f, -Mathf.Min(72f, hazard.Definition.Radius + 10f)),
			ResolveStageHazardLabel(hazard.Definition).ToUpperInvariant(),
			hazard.Color.Lightened(0.18f),
			0.54f);

		foreach (var unit in _units)
		{
			if (unit.IsDead || unit.Position.DistanceTo(hazard.Anchor) > hazard.Definition.Radius)
			{
				continue;
			}

			var appliedDamage = unit.TakeDamage(hazard.Definition.Damage);
			SpawnDamageFeedback(unit.Position, appliedDamage, hazard.Color);
			if (unit.Team == Team.Player && appliedDamage > 0.05f)
			{
				_playerHazardHits++;
			}
		}

		if (CanInteractWithEndlessContactActor(_activeEndlessContactActor) &&
			_activeEndlessContactActor.Position.DistanceTo(hazard.Anchor) <= hazard.Definition.Radius)
		{
			var appliedDamage = _activeEndlessContactActor.ApplyPressureDamage(hazard.Definition.Damage * 0.85f);
			RegisterEndlessContactPressure(appliedDamage);
		}

		foreach (var mission in _stageMissions)
		{
			if (!CanInteractWithStageMission(mission) ||
				mission.Anchor.DistanceTo(hazard.Anchor) > hazard.Definition.Radius)
			{
				continue;
			}

			mission.Actor.ApplyPressureDamage(hazard.Definition.Damage * 0.55f);
			if (mission.Actor.Health <= 0.01f)
			{
				FailStageMission(
					mission,
					$"{StageMissionEvents.ResolveTitle(mission.Definition)} was shattered by {ResolveStageHazardLabel(hazard.Definition).ToLowerInvariant()}.");
			}
		}
	}

	private void DrawStageHazards()
	{
		foreach (var hazard in _stageHazards)
		{
			var radius = Mathf.Max(20f, hazard.Definition.Radius);
			var warningDuration = Mathf.Max(0.35f, hazard.Definition.WarningDuration);
			var timeUntil = Mathf.Max(0f, hazard.NextTriggerTime - _elapsed);
			var warningRatio = timeUntil <= warningDuration
				? 1f - Mathf.Clamp(timeUntil / warningDuration, 0f, 1f)
				: 0f;
			var pulse = 0.5f + (0.5f * Mathf.Sin((_elapsed + hazard.Anchor.X * 0.002f) * 5.5f));

			DrawCircle(hazard.Anchor, radius, new Color(hazard.Color, 0.04f + (warningRatio * 0.12f)));
			DrawArc(
				hazard.Anchor,
				radius + 4f + (warningRatio * 4f),
				0f,
				Mathf.Tau,
				36,
				new Color(hazard.Color, 0.16f + (warningRatio * 0.5f)),
				2f + (warningRatio * 2f));

			if (warningRatio > 0.01f && pulse > 0.45f)
			{
				DrawArc(
					hazard.Anchor,
					radius * (0.64f + (warningRatio * 0.18f)),
					0f,
					Mathf.Tau,
					28,
					new Color(hazard.Color.Lightened(0.2f), 0.24f + (warningRatio * 0.38f)),
					2f,
					true);
			}
		}
	}

	private string BuildChallengeGhostDeployFeedback(UnitDefinition definition, Vector2 spawnPosition)
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
			var beyondColor = new Color("bde0fe");
			SpawnFloatText(spawnPosition + new Vector2(0f, -44f), "BEYOND GHOST", beyondColor, 0.52f);
			return " Beyond the saved ghost tape.";
		}

		var ghostDeployment = _challengeGhostRun.Deployments[deploymentIndex];
		var ghostUnit = GameData.GetUnit(ghostDeployment.UnitId);
		var currentLanePercent = Mathf.RoundToInt(
			Mathf.InverseLerp(
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding,
				spawnPosition.Y) * 100f);
		var laneDelta = currentLanePercent - ghostDeployment.LanePercent;
		var timeDelta = _elapsed - ghostDeployment.TimeSeconds;
		var sameUnit = definition.Id.Equals(ghostDeployment.UnitId, StringComparison.OrdinalIgnoreCase);
		var paceLabel = timeDelta <= -0.15f
			? "AHEAD"
			: timeDelta >= 0.15f
				? "LATE"
				: "SYNC";
		var feedbackColor = sameUnit
			? paceLabel switch
			{
				"AHEAD" => new Color("95d5b2"),
				"LATE" => new Color("f4a261"),
				_ => new Color("8ecae6")
			}
			: new Color("ffafcc");
		var primaryLabel = sameUnit
			? $"{paceLabel} {Mathf.Abs(timeDelta):0.0}s"
			: $"SWAP {ghostUnit.DisplayName.ToUpperInvariant()}";
		SpawnFloatText(spawnPosition + new Vector2(0f, -44f), primaryLabel, feedbackColor, 0.56f);

		if (Mathf.Abs(laneDelta) >= 8)
		{
			SpawnFloatText(
				spawnPosition + new Vector2(0f, -66f),
				$"LANE {FormatSignedInt(laneDelta)}%",
				feedbackColor.Lightened(0.12f),
				0.48f);
		}

		return sameUnit
			? $" Ghost split {deploymentIndex + 1}: {paceLabel.ToLowerInvariant()} {Mathf.Abs(timeDelta):0.0}s, lane {FormatSignedInt(laneDelta)}%."
			: $" Ghost split {deploymentIndex + 1}: swapped {definition.DisplayName} for {ghostUnit.DisplayName}, timing {FormatSignedSeconds(timeDelta)}, lane {FormatSignedInt(laneDelta)}%.";
	}

	private int CountChallengeGhostDeploymentsElapsed(float elapsed)
	{
		if (!HasChallengeGhostRun())
		{
			return 0;
		}

		var count = 0;
		foreach (var deployment in _challengeGhostRun.Deployments)
		{
			if (deployment.TimeSeconds > elapsed + 0.001f)
			{
				break;
			}

			count++;
		}

		return count;
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
			$"Ghost comparison: {FormatSignedInt(scoreDelta)} pts  |  {timeText}  |  Hull {FormatSignedInt(hullDelta)}%  |  Deploys {FormatSignedInt(deployDelta)}  |  Stars {FormatSignedInt(starDelta)}";
	}

	private string BuildStageHazardIntelText()
	{
		if (_stageHazards.Count == 0)
		{
			return "Hazards: none";
		}

		var parts = new List<string>();
		for (var i = 0; i < _stageHazards.Count && i < 2; i++)
		{
			var hazard = _stageHazards[i];
			parts.Add($"{ResolveStageHazardLabel(hazard.Definition)} {Mathf.Max(0f, hazard.NextTriggerTime - _elapsed):0.0}s");
		}

		if (_stageHazards.Count > 2)
		{
			parts.Add($"+{_stageHazards.Count - 2} more");
		}

		return "Hazards: " + string.Join("  |  ", parts);
	}

	private static string ResolveStageHazardLabel(StageHazardDefinition hazard)
	{
		return string.IsNullOrWhiteSpace(hazard?.Label) ? "Hazard pulse" : hazard.Label;
	}

	private string BuildWaveEntrySummary(StageWaveDefinition wave)
	{
		var parts = new List<string>();
		for (var i = 0; i < wave.Entries.Length; i++)
		{
			var entry = wave.Entries[i];
			if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
			{
				continue;
			}

			var displayName = GameData.GetUnit(entry.UnitId).DisplayName;
			parts.Add($"{displayName} x{Mathf.Max(1, entry.Count)}");
		}

		return parts.Count > 0
			? string.Join(", ", parts)
			: "No enemy composition data.";
	}

	private string BuildEndlessStatusText()
	{
		var projectedScrap = CalculateEndlessScrapReward();
		var projectedFuel = CalculateEndlessFuelReward();
		return
			"Endless run:\n" +
			$"Wave reached: {_spawnDirector.EndlessWaveNumber}  |  Enemy defeats: {_enemyDefeats}  |  Survival: {_elapsed:0.0}s\n" +
			$"Projected payout: +{projectedScrap} gold, +{projectedFuel} food  |  Boon: {EndlessBoonCatalog.Get(_endlessBoonId).Title}\n" +
			$"Bonus bank: directives {FormatSignedInt(_endlessDirectiveScrapBonus)} gold / {FormatSignedInt(_endlessDirectiveFuelBonus)} food  |  contacts {FormatSignedInt(_endlessContactScrapBonus)} gold / {FormatSignedInt(_endlessContactFuelBonus)} food  |  bosses {FormatSignedInt(_endlessBossScrapBonus)} gold / {FormatSignedInt(_endlessBossFuelBonus)} food\n" +
			$"Path: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}  |  Run upgrades: {_endlessRunUpgrades.Count}\n" +
			$"Segment event: {_spawnDirector.EndlessSegmentEventLabel}\n" +
			$"{BuildEndlessBossCheckpointText()}\n" +
			$"{BuildEndlessDirectiveText()}\n" +
			$"{BuildEndlessContactText()}\n" +
			$"Contact tradeoff: {_endlessContactTradeoffLabel}\n" +
			$"Contact telemetry: {BuildEndlessContactTelemetryText()}\n" +
			$"Battlefield event: {_endlessBattlefieldEventLabel}\n" +
			$"Caravan support: {_endlessSupportEventLabel}\n" +
			$"Record: wave {GameState.Instance.BestEndlessWave}  |  {GameState.Instance.BestEndlessTimeSeconds:0.0}s";
	}

	private string BuildEndlessContactTelemetryText()
	{
		if (_activeEndlessContact == null)
		{
			return "Telemetry standby.";
		}

		var healthPercent = IsInstanceValid(_activeEndlessContactActor)
			? Mathf.RoundToInt(_activeEndlessContactActor.HealthRatio * 100f)
			: 0;
		var state = _activeEndlessContact.Completed
			? "Secured"
			: _activeEndlessContact.Failed
				? "Lost"
				: _activeEndlessContact.EnemyInside
					? "Contested"
					: _activeEndlessContact.PlayerInside
					? "Supported"
						: "Open";
		var supportMomentState = _activeEndlessContact.SupportMomentTriggered
			? "Used"
			: "Standby";
		return
			$"State {state}  |  Actor hull {healthPercent}%  |  Support {_activeEndlessContact.PlayerSupportActions} ({Mathf.RoundToInt(_activeEndlessContact.PlayerSupportRepairTotal)} hull / +{_activeEndlessContact.PlayerSupportProgressTotal:0.0}s)  |  Pressure {_activeEndlessContact.EnemyPressureActions} ({Mathf.RoundToInt(_activeEndlessContact.EnemyPressureDamageTotal)} damage)  |  Responses {_activeEndlessContact.ResponseWavesTriggered}/{_activeEndlessContact.ResponseWaveLimit}  |  Assist {supportMomentState}";
	}

	private string BuildEndlessPressureText()
	{
		var routeText = RouteCatalog.Get(_activeRouteId).PressureSummary;

		var forkText = _endlessRouteForkId switch
		{
			EndlessRouteForkCatalog.MainlinePushId => "Current fork speeds up the line and raises ranged pressure.",
			EndlessRouteForkCatalog.ScavengeDetourId => "Current fork slows the surge slightly but adds heavier supply lanes.",
			EndlessRouteForkCatalog.FortifiedBlockId => "Current fork softens pressure at the cost of lower gold efficiency.",
			_ => ""
		};

		return $"{routeText} {forkText}".Trim();
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
		ResolveEndlessDirectiveCheckpoint();
		ResolveEndlessContactCheckpoint();
		_endlessCheckpointActive = true;
		_draftingRouteFork = IsRouteForkCheckpoint();
		_draftOptionIds = _draftingRouteFork ? BuildRouteForkOptions() : BuildDraftOptions();
		_draftLabel.Text = _draftingRouteFork
			? $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose the next route segment before the caravan rolls out.\n{BuildEndlessBossCheckpointCheckpointSummary()}\n{BuildEndlessDirectiveCheckpointSummary()}\n{BuildEndlessContactCheckpointSummary()}\nTradeoff report: {_endlessContactTradeoffLabel}\n{BuildEndlessContactTelemetryText()}"
			: $"Checkpoint secure on wave {_spawnDirector.EndlessWaveNumber}.\nChoose one run upgrade before the next surge.\n{BuildEndlessBossCheckpointCheckpointSummary()}\n{BuildEndlessDirectiveCheckpointSummary()}\n{BuildEndlessContactCheckpointSummary()}\nTradeoff report: {_endlessContactTradeoffLabel}\n{BuildEndlessContactTelemetryText()}";

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
			"salvage_contract"
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
		StartRouteForkFieldEvent(_endlessRouteForkId);
		StartEndlessDirectiveSegment();
		StartEndlessContactEvent();
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
					_endlessScrapScale *= 1.15f;
				}
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
		StartRouteForkFieldEvent(_endlessRouteForkId);

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
					_endlessSupportEventLabel = "Forager escort arrived: cavalry reinforcement deployed and the war wagon was patched.";
				RepairBusByRatio(0.1f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerRaiderId)
					? GameData.PlayerRaiderId
					: GameData.PlayerBrawlerId);
				break;
			case EndlessRouteForkCatalog.FortifiedBlockId:
				_endlessSupportEventLabel = "Safehouse militia joined: shield knight reinforcement and war wagon repairs secured the block.";
				RepairBusByRatio(0.12f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerDefenderId)
					? GameData.PlayerDefenderId
					: GameData.PlayerBrawlerId);
				break;
		}
	}

	private void StartRouteForkFieldEvent(string routeForkId)
	{
		var normalizedForkId = EndlessRouteForkCatalog.Normalize(routeForkId);
		_activeEndlessFieldEvent = normalizedForkId switch
		{
			EndlessRouteForkCatalog.MainlinePushId => new EndlessFieldEvent(
				"mainline_push",
				"Rapid flares sweep the forward lanes and detonate around the rush line.",
				new[]
				{
					new Vector2(EnemySpawnX - 150f, BattlefieldTop + 96f),
					new Vector2(EnemySpawnX - 210f, BaseCenterY),
					new Vector2(EnemySpawnX - 150f, BattlefieldBottom - 96f)
				},
				18f,
				2.8f,
				54f,
				new Color("ffd166")),
			EndlessRouteForkCatalog.ScavengeDetourId => new EndlessFieldEvent(
				"scavenge_detour",
					"Supply caches pulse courage and quick repairs from the caravan sidelines.",
				new[]
				{
					new Vector2(PlayerSpawnX + 40f, BattlefieldTop + 118f),
					new Vector2(PlayerSpawnX + 40f, BattlefieldBottom - 118f)
				},
				20f,
				4.2f,
				44f,
				new Color("80ed99")),
			_ => new EndlessFieldEvent(
				"fortified_block",
					"A ward turret scans the block and suppresses enemies near the caravan.",
				new[]
				{
					new Vector2(PlayerBaseX + 124f, BaseCenterY - 34f)
				},
				22f,
				2.7f,
				320f,
				new Color("9bf6ff"))
		};

		_endlessBattlefieldEventLabel = _activeEndlessFieldEvent.Label;
	}

	private void StartEndlessDirectiveSegment()
	{
		if (!IsEndlessMode)
		{
			return;
		}

		var definition = EndlessDirectiveCatalog.GetForRouteFork(_endlessRouteForkId);
		var targetCount = ResolveDirectiveTargetCount(definition);
		var targetRatio = ResolveDirectiveTargetRatio(definition);
		var currentBusRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);
		_activeEndlessDirective = new EndlessDirectiveState(
			definition,
			Math.Max(1, _spawnDirector.EndlessWaveNumber + 1),
			_spawnDirector.EndlessWaveNumber + 5,
			targetCount,
			targetRatio,
			_enemyDefeats,
			_playerDeployments,
			currentBusRatio);
		SetStatus($"Caravan directive issued: {definition.Title}. {definition.Summary}");
	}

	private void UpdateEndlessDirectiveState()
	{
		if (!IsEndlessMode || _activeEndlessDirective == null || _battleEnded)
		{
			return;
		}

		var currentBusRatio = Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);
		_activeEndlessDirective.LowestBusHullRatio = Mathf.Min(_activeEndlessDirective.LowestBusHullRatio, currentBusRatio);

		if (_activeEndlessDirective.Completed || _activeEndlessDirective.Failed)
		{
			return;
		}

		switch (_activeEndlessDirective.Definition.Type)
		{
			case "enemy_defeats":
				if (GetDirectiveEnemyDefeatProgress() >= _activeEndlessDirective.TargetCount)
				{
					CompleteEndlessDirective("Threat lane cleared ahead of schedule.");
				}
				break;
			case "deploy_limit":
				if (GetDirectiveDeploymentCount() > _activeEndlessDirective.TargetCount)
				{
						FailEndlessDirective("The sweep went loud. The supply window collapsed.");
				}
				break;
			case "bus_hull_ratio":
				if (_activeEndlessDirective.LowestBusHullRatio < _activeEndlessDirective.TargetRatio)
				{
					FailEndlessDirective("The ward line cracked below the safehouse threshold.");
				}
				break;
		}
	}

	private void ResolveEndlessDirectiveCheckpoint()
	{
		if (!IsEndlessMode || _activeEndlessDirective == null || _activeEndlessDirective.RewardGranted)
		{
			return;
		}

		if (_activeEndlessDirective.Completed || _activeEndlessDirective.Failed)
		{
			return;
		}

		switch (_activeEndlessDirective.Definition.Type)
		{
			case "enemy_defeats":
				if (GetDirectiveEnemyDefeatProgress() >= _activeEndlessDirective.TargetCount)
				{
					CompleteEndlessDirective("Breakthrough window secured at the checkpoint.");
				}
				else
				{
					FailEndlessDirective("The caravan missed the breakthrough quota before the checkpoint.");
				}
				break;
			case "deploy_limit":
				if (GetDirectiveDeploymentCount() <= _activeEndlessDirective.TargetCount)
				{
						CompleteEndlessDirective("The caravan reached the checkpoint with the supply sweep intact.");
				}
				else
				{
						FailEndlessDirective("Too many deployments burned the supply sweep before the checkpoint.");
				}
				break;
			case "bus_hull_ratio":
				if (_activeEndlessDirective.LowestBusHullRatio >= _activeEndlessDirective.TargetRatio)
				{
					CompleteEndlessDirective("The war wagon held the fortified block all the way to the checkpoint.");
				}
				else
				{
					FailEndlessDirective("The fortified hold broke before the caravan reached the checkpoint.");
				}
				break;
		}
	}

	private int GetDirectiveEnemyDefeatProgress()
	{
		return _activeEndlessDirective == null
			? 0
			: Math.Max(0, _enemyDefeats - _activeEndlessDirective.StartEnemyDefeats);
	}

	private int GetDirectiveDeploymentCount()
	{
		return _activeEndlessDirective == null
			? 0
			: Math.Max(0, _playerDeployments - _activeEndlessDirective.StartDeployments);
	}

	private int ResolveDirectiveTargetCount(EndlessDirectiveDefinition definition)
	{
		if (definition == null)
		{
			return 0;
		}

		return definition.Type switch
		{
			"enemy_defeats" => definition.TargetCount + Math.Min(6, _spawnDirector.EndlessWaveNumber / 5),
			_ => definition.TargetCount
		};
	}

	private static float ResolveDirectiveTargetRatio(EndlessDirectiveDefinition definition)
	{
		return definition == null ? 0f : definition.TargetRatio;
	}

	private void CompleteEndlessDirective(string statusText)
	{
		if (_activeEndlessDirective == null || _activeEndlessDirective.RewardGranted)
		{
			return;
		}

		_activeEndlessDirective.Completed = true;
		_activeEndlessDirective.RewardGranted = true;
		ApplyEndlessDirectiveReward(_activeEndlessDirective);
		SetStatus($"{_activeEndlessDirective.Definition.Title} complete. {statusText}");
	}

	private void FailEndlessDirective(string statusText)
	{
		if (_activeEndlessDirective == null || _activeEndlessDirective.Completed || _activeEndlessDirective.Failed)
		{
			return;
		}

		_activeEndlessDirective.Failed = true;
		SetStatus($"{_activeEndlessDirective.Definition.Title} failed. {statusText}");
	}

	private void ApplyEndlessDirectiveReward(EndlessDirectiveState directive)
	{
		switch (directive.Definition.Id)
		{
			case EndlessDirectiveCatalog.BreakthroughDirectiveId:
				_courage = Mathf.Min(_maxCourage, _courage + 16f);
				_deck.ReduceCooldowns(1.5f);
				_spellDeck.ReduceCooldowns(1.5f);
				SpawnEffect(PlayerBaseCorePosition, new Color("ffe066"), 12f, 30f, 0.24f);
				SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -54f), "BREAKTHROUGH", new Color("fff3b0"), 0.62f);
				break;
			case EndlessDirectiveCatalog.SalvageSweepDirectiveId:
				_endlessDirectiveScrapBonus += 28;
				_endlessDirectiveFuelBonus += 1;
				RepairBusByRatio(0.04f);
				SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -54f), "SALVAGE SECURED", new Color("f4a261"), 0.66f);
				break;
			case EndlessDirectiveCatalog.HoldLineDirectiveId:
				RepairBusByRatio(0.08f);
				_deck.ReduceCooldowns(1f);
				_spellDeck.ReduceCooldowns(1f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerDefenderId)
					? GameData.PlayerDefenderId
					: GameData.PlayerBrawlerId);
				SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -54f), "SAFEHOUSE AID", new Color("ade8f4"), 0.66f);
				break;
		}
	}

	private string BuildEndlessDirectiveText()
	{
		if (!IsEndlessMode || _activeEndlessDirective == null)
		{
			return "Directive: standby.";
		}

		var prefix = _activeEndlessDirective.Completed
			? "[OK]"
			: _activeEndlessDirective.Failed
				? "[X]"
				: "[..]";
		return $"{prefix} Directive: {_activeEndlessDirective.Definition.Title}  |  {BuildEndlessDirectiveProgressText()}  |  {_activeEndlessDirective.Definition.RewardSummary}";
	}

	private string BuildEndlessBossCheckpointText()
	{
		if (!IsEndlessMode)
		{
			return "Boss checkpoint: standby.";
		}

		if (_spawnDirector.EndlessBossCheckpointPending)
		{
			var definition = EndlessBossCheckpointCatalog.GetForRoute(_activeRouteId);
			return $"[BOSS] {definition.Title}  |  Wave {_spawnDirector.EndlessWaveNumber}  |  {definition.Summary}  |  {definition.RewardSummary}";
		}

		var nextBossWave = EndlessBossCheckpointCatalog.GetNextBossCheckpointWave(_spawnDirector.EndlessWaveNumber);
		if (_lastEndlessBossCheckpointWave > 0)
		{
			return $"[OK] {_lastEndlessBossCheckpointTitle} broken on wave {_lastEndlessBossCheckpointWave}  |  Next boss checkpoint at wave {nextBossWave}";
		}

		return $"Boss checkpoint: first warlord surge expected at wave {nextBossWave}.";
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

		var definition = EndlessBossCheckpointCatalog.GetForRoute(_activeRouteId);
		return $"[OK] {definition.Title}  |  {definition.RewardSummary}";
	}

	private string BuildEndlessDirectiveCheckpointSummary()
	{
		if (_activeEndlessDirective == null)
		{
			return "Directive report: standby.";
		}

		var prefix = _activeEndlessDirective.Completed
			? "[OK]"
			: _activeEndlessDirective.Failed
				? "[X]"
				: "[..]";
		return $"{prefix} {_activeEndlessDirective.Definition.Title}  |  {BuildEndlessDirectiveProgressText()}";
	}

	private string BuildEndlessDirectiveProgressText()
	{
		if (_activeEndlessDirective == null)
		{
			return "No directive";
		}

		return _activeEndlessDirective.Definition.Type switch
		{
			"enemy_defeats" => $"Defeats {GetDirectiveEnemyDefeatProgress()}/{_activeEndlessDirective.TargetCount} before checkpoint wave {_activeEndlessDirective.CheckpointWave}",
			"deploy_limit" => $"Deployments {GetDirectiveDeploymentCount()}/{_activeEndlessDirective.TargetCount} before checkpoint wave {_activeEndlessDirective.CheckpointWave}",
			"bus_hull_ratio" => $"War wagon hull low {Mathf.RoundToInt(_activeEndlessDirective.LowestBusHullRatio * 100f)}% / keep above {Mathf.RoundToInt(_activeEndlessDirective.TargetRatio * 100f)}% through wave {_activeEndlessDirective.CheckpointWave}",
			_ => _activeEndlessDirective.Definition.Summary
		};
	}

	private void ResolveEndlessBossCheckpoint()
	{
		if (!IsEndlessMode || !EndlessBossCheckpointCatalog.IsBossCheckpointWave(_spawnDirector.EndlessWaveNumber))
		{
			return;
		}

		var definition = EndlessBossCheckpointCatalog.GetForRoute(_activeRouteId);
		_lastEndlessBossCheckpointWave = _spawnDirector.EndlessWaveNumber;
		_lastEndlessBossCheckpointTitle = definition.Title;
		_endlessBossScrapBonus += definition.RewardGold;
		_endlessBossFuelBonus += definition.RewardFood;

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
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerCoordinatorId)
					? GameData.PlayerCoordinatorId
					: GameData.PlayerDefenderId);
				break;
			case RouteCatalog.ThornwallId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerRangerId)
					? GameData.PlayerRangerId
					: GameData.PlayerShooterId);
				break;
			case RouteCatalog.BasilicaId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerCoordinatorId)
					? GameData.PlayerCoordinatorId
					: GameData.PlayerMarksmanId);
				break;
			case RouteCatalog.MireId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerMechanicId)
					? GameData.PlayerMechanicId
					: GameData.PlayerBrawlerId);
				break;
			case RouteCatalog.SteppeId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerRaiderId)
					? GameData.PlayerRaiderId
					: GameData.PlayerBrawlerId);
				break;
			case RouteCatalog.GloamwoodId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerMarksmanId)
					? GameData.PlayerMarksmanId
					: GameData.PlayerShooterId);
				break;
			case RouteCatalog.CitadelId:
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerBreacherId)
					? GameData.PlayerBreacherId
					: GameData.PlayerDefenderId);
				break;
		}

		var route = RouteCatalog.Get(_activeRouteId);
		SpawnEffect(EnemyBaseCorePosition, route.BannerAccent.Lightened(0.08f), 18f, 72f, 0.3f, false);
		SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -56f), "BOSS CHECKPOINT BROKEN", route.BannerAccent.Lightened(0.16f), 0.72f);
		SetStatus($"{definition.Title} broken on wave {_spawnDirector.EndlessWaveNumber}. {definition.ClearStatus}");
	}

	private void ResetEndlessContactTradeoffs()
	{
		_endlessContactTradeoffLabel = DefaultEndlessContactTradeoffLabel;
		_endlessContactCourageGainScale = 1f;
		_spawnDirector.ResetEndlessSegmentTradeoffs();
	}

	private void StartEndlessContactEvent()
	{
		if (!IsEndlessMode)
		{
			return;
		}

		ResetEndlessContactTradeoffs();

		var definition = EndlessContactCatalog.GetForRouteFork(_endlessRouteForkId);
		if (IsInstanceValid(_activeEndlessContactActor))
		{
			_activeEndlessContactActor.QueueFree();
		}

		_activeEndlessContact = new EndlessContactState(
			definition,
			ResolveEndlessContactAnchor(definition.Id),
			ResolveEndlessContactColor(definition.Id));
		_activeEndlessContact.ResponseTimer = ResolveEndlessContactResponseCadence(definition.Id);
		_activeEndlessContact.ResponseWaveLimit = ResolveEndlessContactResponseLimit(definition.Id);
		_activeEndlessContactActor = new EndlessContactActor();
		_activeEndlessContactActor.Position = _activeEndlessContact.Anchor;
		_activeEndlessContactActor.Setup(
			definition.Id,
			_activeEndlessContact.Color,
			definition.Radius,
			ResolveEndlessContactMaxHealth(definition.Id));
		AddChild(_activeEndlessContactActor);
	}

	private Vector2 ResolveEndlessContactAnchor(string contactId)
	{
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => new Vector2(Mathf.Lerp(PlayerBaseX, EnemyBaseX, 0.64f), BaseCenterY),
			EndlessContactCatalog.SalvageCacheId => new Vector2(PlayerSpawnX + 132f, BattlefieldBottom - 128f),
			EndlessContactCatalog.SafehouseRescueId => new Vector2(PlayerBaseX + 164f, BattlefieldTop + 126f),
			_ => new Vector2(Mathf.Lerp(PlayerBaseX, EnemyBaseX, 0.5f), BaseCenterY)
		};
	}

	private static Color ResolveEndlessContactColor(string contactId)
	{
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => new Color("ffb703"),
			EndlessContactCatalog.SalvageCacheId => new Color("84cc16"),
			EndlessContactCatalog.SafehouseRescueId => new Color("90e0ef"),
			_ => Colors.White
		};
	}

	private float ResolveEndlessContactResponseCadence(string contactId)
	{
		var wavePressure = Mathf.Max(0, _spawnDirector.EndlessWaveNumber);
		var baseCadence = contactId switch
		{
			EndlessContactCatalog.RelaySignalId => 6.8f,
			EndlessContactCatalog.SalvageCacheId => 7.4f,
			EndlessContactCatalog.SafehouseRescueId => 7.9f,
			_ => 7.2f
		};

		return Mathf.Max(3.6f, baseCadence - (wavePressure * 0.08f));
	}

	private int ResolveEndlessContactResponseLimit(string contactId)
	{
		var extra = Mathf.Clamp(_spawnDirector.EndlessWaveNumber / 6, 0, 2);
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => 2 + extra,
			EndlessContactCatalog.SalvageCacheId => 2 + extra,
			EndlessContactCatalog.SafehouseRescueId => 1 + extra,
			_ => 2
		};
	}

	private void UpdateEndlessContactEvent(float delta)
	{
		if (!IsEndlessMode || _activeEndlessContact == null || _battleEnded)
		{
			return;
		}

		if (_activeEndlessContact.Completed || _activeEndlessContact.Failed)
		{
			return;
		}

		var anchor = _activeEndlessContact.Anchor;
		var radius = _activeEndlessContact.Definition.Radius;
		var playerInside = HasTeamUnitInRadius(Team.Player, anchor, radius);
		var enemyInside = HasTeamUnitInRadius(Team.Enemy, anchor, radius);
		_activeEndlessContact.PlayerInside = playerInside;
		_activeEndlessContact.EnemyInside = enemyInside;

		if (IsInstanceValid(_activeEndlessContactActor))
		{
			if (playerInside && !enemyInside)
			{
				_activeEndlessContactActor.Repair(ResolveEndlessContactPresenceRepairRate(_activeEndlessContact.Definition.Id) * delta);
			}
		}

		switch (_activeEndlessContact.Definition.Type)
		{
			case "forward_presence":
				if (playerInside && !enemyInside)
				{
					_activeEndlessContact.Progress += delta * 1.2f;
				}
				else if (playerInside)
				{
					_activeEndlessContact.Progress += delta * 0.4f;
				}
				else
				{
					_activeEndlessContact.Progress -= delta * 0.3f;
				}
				break;
			case "secure_cache":
				if (playerInside && !enemyInside)
				{
					_activeEndlessContact.Progress += delta * 1.25f;
				}
				else if (enemyInside)
				{
					_activeEndlessContact.Progress -= delta * 0.9f;
				}
				else
				{
					_activeEndlessContact.Progress -= delta * 0.18f;
				}
				break;
			case "rescue_hold":
				if (!enemyInside)
				{
					_activeEndlessContact.Progress += playerInside ? delta * 1.18f : delta * 0.85f;
				}
				else
				{
					_activeEndlessContact.Progress -= delta * 1.05f;
				}
				break;
		}

		_activeEndlessContact.Progress = Mathf.Clamp(
			_activeEndlessContact.Progress,
			0f,
			_activeEndlessContact.Definition.TargetSeconds);

		if (IsInstanceValid(_activeEndlessContactActor))
		{
			_activeEndlessContactActor.UpdateState(
				_activeEndlessContact.Progress / Mathf.Max(0.01f, _activeEndlessContact.Definition.TargetSeconds),
				playerInside,
				enemyInside,
				_activeEndlessContact.Completed,
				_activeEndlessContact.Failed);

			if (_activeEndlessContactActor.Health <= 0.01f)
			{
				FailEndlessContactEvent("The battlefield contact was destroyed before the caravan secured it.");
				return;
			}
		}

		TryTriggerEndlessContactSupportMoment();

		if (_activeEndlessContact.Progress + 0.001f >= _activeEndlessContact.Definition.TargetSeconds)
		{
			CompleteEndlessContactEvent();
			return;
		}

		UpdateEndlessContactResponses(delta);
	}

	private void UpdateEndlessContactResponses(float delta)
	{
		if (_activeEndlessContact == null || _activeEndlessContact.Completed || _activeEndlessContact.Failed)
		{
			return;
		}

		if (_activeEndlessContact.ResponseWavesTriggered >= _activeEndlessContact.ResponseWaveLimit)
		{
			return;
		}

		_activeEndlessContact.ResponseTimer -= delta;
		if (_activeEndlessContact.ResponseTimer > 0f)
		{
			return;
		}

		if (TriggerEndlessContactResponse())
		{
			_activeEndlessContact.ResponseWavesTriggered++;
			_activeEndlessContact.ResponseTimer = ResolveEndlessContactResponseCadence(_activeEndlessContact.Definition.Id) * 0.9f;
			return;
		}

		_activeEndlessContact.ResponseTimer = 1.6f;
	}

	private void TryTriggerEndlessContactSupportMoment()
	{
		if (_activeEndlessContact == null || _activeEndlessContact.SupportMomentTriggered)
		{
			return;
		}

		if (_activeEndlessContact.Progress + 0.001f < (_activeEndlessContact.Definition.TargetSeconds * 0.5f))
		{
			return;
		}

		_activeEndlessContact.SupportMomentTriggered = true;
		var contact = _activeEndlessContact;
		var statusText = "";
		switch (contact.Definition.Id)
		{
			case EndlessContactCatalog.RelaySignalId:
				_courage = Mathf.Min(_maxCourage, _courage + 8f);
				_deck.ReduceCooldowns(0.7f);
				_spellDeck.ReduceCooldowns(0.7f);
				statusText = "Relay uplink pulse refreshed courage and squad recovery.";
				_endlessSupportEventLabel = "Relay uplink pulse boosted caravan deployment tempo.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -56f), "UPLINK BURST", contact.Color.Lightened(0.28f), 0.6f);
				break;
			case EndlessContactCatalog.SalvageCacheId:
				_endlessContactScrapBonus += 8;
				RepairBusByRatio(0.02f);
				statusText = "Salvage crew hauled reserve parts aboard the caravan.";
					_endlessSupportEventLabel = "Reserve stores loaded: light repairs and extra payout banked.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -56f), "SUPPLY WINCH", contact.Color.Lightened(0.28f), 0.6f);
				break;
			case EndlessContactCatalog.SafehouseRescueId:
				RepairBusByRatio(0.03f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerShooterId)
					? GameData.PlayerShooterId
					: GameData.PlayerBrawlerId);
				statusText = "Safehouse volunteers joined the firing line around the caravan.";
				_endlessSupportEventLabel = "Militia volunteers deployed from the safehouse block.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -56f), "MILITIA JOIN", contact.Color.Lightened(0.28f), 0.6f);
				break;
			default:
				return;
		}

		SpawnEffect(contact.Anchor, contact.Color.Lightened(0.08f), 10f, contact.Definition.Radius * 0.58f, 0.22f, false);
		SetStatus($"{contact.Definition.Title} triggered caravan assistance. {statusText}");
	}

	private bool TriggerEndlessContactResponse()
	{
		if (_activeEndlessContact == null)
		{
			return false;
		}

		var contact = _activeEndlessContact;
		var spawned = 0;
		var label = "";
		switch (contact.Definition.Id)
		{
			case EndlessContactCatalog.RelaySignalId:
				label = "Intercept Pack";
				spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyRunnerId, 78f, 122f) ? 1 : 0;
				spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyRunnerId, 110f, 154f) ? 1 : 0;
				if (_spawnDirector.EndlessWaveNumber >= 3 || contact.PlayerInside)
				{
					spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemySpitterId, 148f, 196f, 28f) ? 1 : 0;
				}
				break;
			case EndlessContactCatalog.SalvageCacheId:
				label = "Scavenge Swarm";
				spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyWalkerId, 70f, 112f, 34f) ? 1 : 0;
				spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyBloaterId, 118f, 162f, 26f) ? 1 : 0;
				if (_spawnDirector.EndlessWaveNumber >= 6)
				{
					spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyBruteId, 154f, 198f, 22f) ? 1 : 0;
				}
				break;
			case EndlessContactCatalog.SafehouseRescueId:
				label = "Blockade Push";
				spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemyWalkerId, 66f, 104f, 30f) ? 1 : 0;
				spawned += TrySpawnEndlessContactResponseEnemy(
					_spawnDirector.EndlessWaveNumber >= 6 ? GameData.EnemyCrusherId : GameData.EnemyBruteId,
					120f,
					168f,
					20f) ? 1 : 0;
				if (_spawnDirector.EndlessWaveNumber >= 9)
				{
					spawned += TrySpawnEndlessContactResponseEnemy(GameData.EnemySpitterId, 154f, 196f, 18f) ? 1 : 0;
				}
				break;
		}

		if (spawned <= 0)
		{
			return false;
		}

		SpawnEffect(contact.Anchor, contact.Color.Darkened(0.08f), 10f, contact.Definition.Radius * 0.62f, 0.24f, false);
		SpawnFloatText(contact.Anchor + new Vector2(0f, -72f), label.ToUpperInvariant(), contact.Color.Lightened(0.1f), 0.58f);
		_endlessBattlefieldEventLabel = $"Contact response active: {label}.";
		SetStatus($"{contact.Definition.Title} triggered a hostile response: {label}.");
		return true;
	}

	private bool TrySpawnEndlessContactResponseEnemy(string unitId, float minOffsetX, float maxOffsetX, float verticalRange = 38f)
	{
		if (_activeEndlessContact == null)
		{
			return false;
		}

		if (CountTeamUnits(Team.Enemy) >= _spawnDirector.GetMaxActiveEnemies() + 2)
		{
			return false;
		}

		if (!_spawnDirector.TryBuildEnemyStats(unitId, out var stats))
		{
			return false;
		}

		var anchor = _activeEndlessContact.Anchor;
		var position = new Vector2(
			Mathf.Clamp(anchor.X + _rng.RandfRange(minOffsetX, maxOffsetX), BattlefieldLeft + 48f, BattlefieldRight - 48f),
			Mathf.Clamp(anchor.Y + _rng.RandfRange(-verticalRange, verticalRange), BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
		SpawnEnemyUnit(stats, position);
		return true;
	}

	private void ResolveEndlessContactCheckpoint()
	{
		if (!IsEndlessMode || _activeEndlessContact == null || _activeEndlessContact.Completed || _activeEndlessContact.Failed)
		{
			return;
		}

		FailEndlessContactEvent();
	}

	private void CompleteEndlessContactEvent()
	{
		if (_activeEndlessContact == null || _activeEndlessContact.RewardGranted)
		{
			return;
		}

		_activeEndlessContact.Completed = true;
		_activeEndlessContact.RewardGranted = true;
		ApplyEndlessContactReward(_activeEndlessContact);
		SetStatus($"{_activeEndlessContact.Definition.Title} secured. {ResolveEndlessContactCompleteStatus(_activeEndlessContact.Definition.Id)} {_endlessContactTradeoffLabel}");
	}

	private void FailEndlessContactEvent(string statusText = null)
	{
		if (_activeEndlessContact == null || _activeEndlessContact.Completed || _activeEndlessContact.Failed)
		{
			return;
		}

		_activeEndlessContact.Failed = true;
		ApplyEndlessContactFailurePenalty(_activeEndlessContact);
		if (IsInstanceValid(_activeEndlessContactActor))
		{
			_activeEndlessContactActor.UpdateState(
				_activeEndlessContact.Progress / Mathf.Max(0.01f, _activeEndlessContact.Definition.TargetSeconds),
				_activeEndlessContact.PlayerInside,
				_activeEndlessContact.EnemyInside,
				false,
				true);
		}

		var penaltyText = ResolveEndlessContactFailurePenaltyText(_activeEndlessContact.Definition.Id);
		var baseStatus = statusText ?? $"{_activeEndlessContact.Definition.Title} lost before the caravan cleared the segment.";
		SetStatus($"{baseStatus} {penaltyText}");
	}

	private void ApplyEndlessContactReward(EndlessContactState contact)
	{
		SpawnEffect(contact.Anchor, contact.Color, 12f, contact.Definition.Radius * 0.72f, 0.28f, false);
		if (IsInstanceValid(_activeEndlessContactActor))
		{
			_activeEndlessContactActor.Repair(_activeEndlessContactActor.MaxHealth);
			_activeEndlessContactActor.UpdateState(1f, true, false, true, false);
		}

		switch (contact.Definition.Id)
		{
			case EndlessContactCatalog.RelaySignalId:
				_courage = Mathf.Min(_maxCourage, _courage + 14f);
				_deck.ReduceCooldowns(1.2f);
				_spellDeck.ReduceCooldowns(1.2f);
				ApplyEndlessContactSuccessTradeoff(contact);
				SpawnFloatText(contact.Anchor + new Vector2(0f, -24f), "RELAY ONLINE", contact.Color.Lightened(0.2f), 0.66f);
				break;
			case EndlessContactCatalog.SalvageCacheId:
				_endlessContactScrapBonus += 22;
				_endlessContactFuelBonus += 1;
				RepairBusByRatio(0.03f);
				ApplyEndlessContactSuccessTradeoff(contact);
				SpawnFloatText(contact.Anchor + new Vector2(0f, -24f), "CACHE SECURED", contact.Color.Lightened(0.2f), 0.66f);
				break;
			case EndlessContactCatalog.SafehouseRescueId:
				RepairBusByRatio(0.06f);
				_deck.ReduceCooldowns(0.8f);
				_spellDeck.ReduceCooldowns(0.8f);
				SpawnSupportUnit(GameState.Instance.IsUnitUnlocked(GameData.PlayerDefenderId)
					? GameData.PlayerDefenderId
					: GameData.PlayerBrawlerId);
				ApplyEndlessContactSuccessTradeoff(contact);
				SpawnFloatText(contact.Anchor + new Vector2(0f, -24f), "SURVIVORS OUT", contact.Color.Lightened(0.2f), 0.66f);
				break;
		}
	}

	private void ApplyEndlessContactSuccessTradeoff(EndlessContactState contact)
	{
		switch (contact.Definition.Id)
		{
			case EndlessContactCatalog.RelaySignalId:
				_spawnDirector.AdvanceNextEndlessWave(_elapsed, 1.4f);
				_endlessContactTradeoffLabel = "Relay sprint active: the next surge arrives 1.4s sooner.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -46f), "SURGE PULLED FORWARD", new Color("ffd166"), 0.68f);
				break;
			case EndlessContactCatalog.SalvageCacheId:
				_spawnDirector.SetEndlessTradeoffEnemyCapModifier(1);
				_endlessContactTradeoffLabel = "Cargo drag active: enemy cap is +1 until the next checkpoint.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -46f), "LANE STRETCHED", new Color("caffbf"), 0.68f);
				break;
			case EndlessContactCatalog.SafehouseRescueId:
				_endlessContactCourageGainScale = 0.85f;
				_endlessContactTradeoffLabel = "Evac load active: courage gain is reduced by 15% until checkpoint.";
				SpawnFloatText(contact.Anchor + new Vector2(0f, -46f), "EVAC LOAD", new Color("bde0fe"), 0.68f);
				break;
		}
	}

	private static string ResolveEndlessContactCompleteStatus(string contactId)
	{
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => "Forward scouts refreshed the route intel and opened the line.",
				EndlessContactCatalog.SalvageCacheId => "The crew hauled the supply cache aboard before the lane collapsed.",
			EndlessContactCatalog.SafehouseRescueId => "Safehouse survivors joined the caravan before the block fell.",
			_ => "The caravan secured the battlefield contact."
		};
	}

	private void ApplyEndlessContactFailurePenalty(EndlessContactState contact)
	{
		switch (contact.Definition.Id)
		{
			case EndlessContactCatalog.RelaySignalId:
				_courage = Mathf.Max(0f, _courage - 14f);
				_deck.IncreaseCooldowns(1.8f);
				_spellDeck.IncreaseCooldowns(1.8f);
				SpawnFloatText(contact.Anchor + new Vector2(0f, -24f), "SIGNAL LOST", new Color("ffb4a2"), 0.62f);
				break;
			case EndlessContactCatalog.SalvageCacheId:
				_endlessContactScrapBonus -= 16;
				_endlessContactFuelBonus -= 1;
				DamageBusByRatio(0.04f, new Color("f28482"), "CACHE LOST");
				break;
			case EndlessContactCatalog.SafehouseRescueId:
				DamageBusByRatio(0.08f, new Color("ef476f"), "BLOCK LOST");
				_deck.IncreaseCooldowns(1f);
				_spellDeck.IncreaseCooldowns(1f);
				break;
		}
	}

	private static string ResolveEndlessContactFailurePenaltyText(string contactId)
	{
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => "The caravan loses courage and squad cards recover slower.",
				EndlessContactCatalog.SalvageCacheId => "Projected spoils drop and the war wagon takes collision damage.",
			EndlessContactCatalog.SafehouseRescueId => "The caravan takes a hard hull hit and squad recovery slows.",
			_ => "The caravan loses ground on the route."
		};
	}

	private float ResolveEndlessContactMaxHealth(string contactId)
	{
		var wavePressure = Mathf.Max(0, _spawnDirector.EndlessWaveNumber);
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => 72f + (wavePressure * 2.8f),
			EndlessContactCatalog.SalvageCacheId => 88f + (wavePressure * 3.1f),
			EndlessContactCatalog.SafehouseRescueId => 104f + (wavePressure * 3.5f),
			_ => 80f
		};
	}

	private float ResolveEndlessContactPresenceRepairRate(string contactId)
	{
		return contactId switch
		{
			EndlessContactCatalog.RelaySignalId => 1.6f,
			EndlessContactCatalog.SalvageCacheId => 1.3f,
			EndlessContactCatalog.SafehouseRescueId => 1.8f,
			_ => 1.2f
		};
	}

	private float ResolveEndlessContactAttackRadius()
	{
		if (_activeEndlessContact == null)
		{
			return 22f;
		}

		return _activeEndlessContact.Definition.Id switch
		{
			EndlessContactCatalog.RelaySignalId => 26f,
			EndlessContactCatalog.SalvageCacheId => 32f,
			EndlessContactCatalog.SafehouseRescueId => 38f,
			_ => 28f
		};
	}

	private float ResolveEndlessContactSupportRadius()
	{
		if (_activeEndlessContact == null)
		{
			return 34f;
		}

		return _activeEndlessContact.Definition.Id switch
		{
			EndlessContactCatalog.RelaySignalId => 34f,
			EndlessContactCatalog.SalvageCacheId => 40f,
			EndlessContactCatalog.SafehouseRescueId => 44f,
			_ => 36f
		};
	}

	private float ResolvePlayerContactSupportRepair(Unit unit)
	{
		if (_activeEndlessContact == null)
		{
			return 0f;
		}

		return _activeEndlessContact.Definition.Id switch
		{
			EndlessContactCatalog.RelaySignalId => unit.VisualClass switch
			{
				"gunner" => 5.2f,
				"sniper" => 5.4f,
				"shield" => 4.2f,
				_ => 3.6f
			},
			EndlessContactCatalog.SalvageCacheId => unit.VisualClass switch
			{
				"skirmisher" => 5f,
				"fighter" => 4.8f,
				"shield" => 4.3f,
				_ => 3.5f
			},
			EndlessContactCatalog.SafehouseRescueId => unit.VisualClass switch
			{
				"shield" => 6.2f,
				"fighter" => 5.3f,
				"skirmisher" => 4.6f,
				_ => 3.8f
			},
			_ => 3.4f
		};
	}

	private float ResolvePlayerContactSupportProgress(Unit unit)
	{
		if (_activeEndlessContact == null)
		{
			return 0f;
		}

		return _activeEndlessContact.Definition.Id switch
		{
			EndlessContactCatalog.RelaySignalId => unit.VisualClass switch
			{
				"gunner" => 0.95f,
				"sniper" => 1.05f,
				"shield" => 0.5f,
				_ => 0.62f
			},
			EndlessContactCatalog.SalvageCacheId => unit.VisualClass switch
			{
				"skirmisher" => 1.05f,
				"fighter" => 0.92f,
				"shield" => 0.68f,
				_ => 0.56f
			},
			EndlessContactCatalog.SafehouseRescueId => unit.VisualClass switch
			{
				"shield" => 1.05f,
				"fighter" => 0.94f,
				"skirmisher" => 0.82f,
				_ => 0.58f
			},
			_ => 0.5f
		};
	}

	private string ResolvePlayerContactSupportLabel(Unit unit)
	{
		if (_activeEndlessContact == null)
		{
			return "SUPPORT";
		}

		return _activeEndlessContact.Definition.Id switch
		{
			EndlessContactCatalog.RelaySignalId => unit.VisualClass switch
			{
				"gunner" => "LINK",
				"sniper" => "UPLINK",
				_ => "BOOST"
			},
			EndlessContactCatalog.SalvageCacheId => unit.VisualClass switch
			{
				"skirmisher" => "LOAD",
				"fighter" => "HAUL",
				_ => "COVER"
			},
			EndlessContactCatalog.SafehouseRescueId => unit.VisualClass switch
			{
				"shield" => "ESCORT",
				"fighter" => "GUARD",
				_ => "STABILIZE"
			},
			_ => "SUPPORT"
		};
	}

	private float ResolveEnemyContactAttackDamage(Unit unit)
	{
		var wavePressure = Mathf.Max(0, _spawnDirector.EndlessWaveNumber);
		var baseDamage = unit.VisualClass switch
		{
			"spitter" => 13f,
			"runner" => 8f,
			"walker" => 11f,
			"splitter" => 12f,
			"brute" => 15f,
			"crusher" => 18f,
			"boss" => 24f,
			_ => 10f
		};
		var attackScale = unit.AttackDamage <= 0.05f
			? 1f
			: unit.CurrentAttackDamage / unit.AttackDamage;
		return (baseDamage * attackScale) + (wavePressure * 0.38f);
	}

	private string BuildEndlessContactText()
	{
		if (!IsEndlessMode || _activeEndlessContact == null)
		{
			return "Contact event: standby.";
		}

		var prefix = _activeEndlessContact.Completed
			? "[OK]"
			: _activeEndlessContact.Failed
				? "[X]"
				: "[..]";
		return $"{prefix} Contact: {_activeEndlessContact.Definition.Title}  |  {BuildEndlessContactProgressText()}  |  {_activeEndlessContact.Definition.RewardSummary}  |  {_activeEndlessContact.Definition.TradeoffSummary}  |  {_activeEndlessContact.Definition.PenaltySummary}";
	}

	private string BuildEndlessContactCheckpointSummary()
	{
		if (_activeEndlessContact == null)
		{
			return "Contact report: standby.";
		}

		var prefix = _activeEndlessContact.Completed
			? "[OK]"
			: _activeEndlessContact.Failed
				? "[X]"
				: "[..]";
		return $"{prefix} {_activeEndlessContact.Definition.Title}  |  {BuildEndlessContactProgressText()}";
	}

	private string BuildEndlessContactProgressText()
	{
		if (_activeEndlessContact == null)
		{
			return "No contact";
		}

		var progress = _activeEndlessContact.Progress;
		var target = _activeEndlessContact.Definition.TargetSeconds;
		return _activeEndlessContact.Definition.Type switch
		{
			"forward_presence" => $"Presence {progress:0.0}/{target:0.0}s inside the relay zone",
			"secure_cache" => $"Secure window {progress:0.0}/{target:0.0}s at the cache radius",
			"rescue_hold" => $"Hold timer {progress:0.0}/{target:0.0}s around the rescue block",
			_ => _activeEndlessContact.Definition.Summary
		};
	}

	private void UpdateEndlessFieldEvent(float delta)
	{
		if (!IsEndlessMode || _activeEndlessFieldEvent == null || _battleEnded)
		{
			return;
		}

		_activeEndlessFieldEvent.Remaining = Mathf.Max(0f, _activeEndlessFieldEvent.Remaining - delta);
		_activeEndlessFieldEvent.PulseTimer -= delta;

		while (_activeEndlessFieldEvent != null && _activeEndlessFieldEvent.PulseTimer <= 0f && _activeEndlessFieldEvent.Remaining > 0f)
		{
			TriggerEndlessFieldEventPulse(_activeEndlessFieldEvent);
			_activeEndlessFieldEvent.PulseTimer += _activeEndlessFieldEvent.Interval;
		}

		if (_activeEndlessFieldEvent != null && _activeEndlessFieldEvent.Remaining <= 0f)
		{
			_endlessBattlefieldEventLabel = "Segment event spent. Await the next route checkpoint.";
			_activeEndlessFieldEvent = null;
		}
	}

	private void TriggerEndlessFieldEventPulse(EndlessFieldEvent fieldEvent)
	{
		switch (fieldEvent.Type)
		{
			case "mainline_push":
				TriggerMainlinePushPulse(fieldEvent);
				break;
			case "scavenge_detour":
				TriggerScavengeDetourPulse(fieldEvent);
				break;
			case "fortified_block":
				TriggerFortifiedBlockPulse(fieldEvent);
				break;
		}
	}

	private void TriggerMainlinePushPulse(EndlessFieldEvent fieldEvent)
	{
		for (var i = 0; i < fieldEvent.Anchors.Length; i++)
		{
			var anchor = fieldEvent.Anchors[i];
			SpawnEffect(anchor, fieldEvent.Color, 10f, fieldEvent.Radius, 0.22f, false);

			foreach (var unit in _units)
			{
				if (unit.IsDead || unit.Team != Team.Enemy)
				{
					continue;
				}

				if (unit.Position.DistanceTo(anchor) > fieldEvent.Radius)
				{
					continue;
				}

				var appliedDamage = unit.TakeDamage(18f + (_spawnDirector.EndlessWaveNumber * 0.8f));
				SpawnDamageFeedback(unit.Position, appliedDamage, fieldEvent.Color);
			}
		}

		_endlessBattlefieldEventLabel = "Rapid flares detonated across the forward lanes.";
	}

	private void TriggerScavengeDetourPulse(EndlessFieldEvent fieldEvent)
	{
		for (var i = 0; i < fieldEvent.Anchors.Length; i++)
		{
			var anchor = fieldEvent.Anchors[i];
			SpawnEffect(anchor, fieldEvent.Color, 12f, 26f, 0.24f);
			SpawnFloatText(anchor + new Vector2(0f, -18f), "CACHE", fieldEvent.Color.Lightened(0.3f), 0.5f);
		}

		_courage = Mathf.Min(_maxCourage, _courage + 8f);
		_deck.ReduceCooldowns(1.2f);
		_spellDeck.ReduceCooldowns(1.2f);
		RepairBusByRatio(0.03f);
		_endlessBattlefieldEventLabel = "Scavenge caches yielded courage and quick repairs.";
	}

	private void TriggerFortifiedBlockPulse(EndlessFieldEvent fieldEvent)
	{
		var anchor = fieldEvent.Anchors[0];
		SpawnEffect(anchor, fieldEvent.Color, 10f, 34f, 0.22f, false);

		var target = FindClosestEnemyToPoint(anchor, fieldEvent.Radius);
		if (target != null)
		{
			var appliedDamage = target.TakeDamage(26f + (_spawnDirector.EndlessWaveNumber * 0.7f));
			SpawnDamageFeedback(target.Position, appliedDamage, fieldEvent.Color);
			SpawnFloatText(target.Position + new Vector2(0f, -20f), "TURRET", fieldEvent.Color.Lightened(0.25f), 0.46f);
		}
		else
		{
			RepairBusByRatio(0.02f);
		}

		_endlessBattlefieldEventLabel = target != null
			? "Safehouse turret suppressed the closest target."
			: "Safehouse crew redirected the pulse into caravan repairs.";
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

	private void SpawnSupportUnit(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		var definition = GameData.GetUnit(unitId);
		if (!definition.IsPlayerSide)
		{
			return;
		}

		var spawnPosition = new Vector2(
			PlayerSpawnX + _rng.RandfRange(-10f, 10f),
			Mathf.Clamp(
				BaseCenterY + _rng.RandfRange(-72f, 72f),
				BattlefieldTop + SpawnVerticalPadding,
				BattlefieldBottom - SpawnVerticalPadding));

		var stats = BuildPlayerUnitStatsForBattle(definition);
		SpawnUnit(Team.Player, stats, spawnPosition);
		SpawnEffect(spawnPosition, stats.Color.Lightened(0.15f), 12f, 32f, 0.24f);
		SpawnFloatText(spawnPosition + new Vector2(0f, -22f), $"+ {stats.Name}", stats.Color.Lightened(0.35f), 0.54f);
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
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(healAmount)}", new Color("b7efc5"), 0.56f);
	}

	private void DamageEnemyBaseByRatio(float ratio, Color color, string label = "")
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
		SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -38f), $"-{Mathf.RoundToInt(damageAmount)}", color.Lightened(0.1f), 0.56f);
		if (!string.IsNullOrWhiteSpace(label))
		{
			SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -60f), label, color.Lightened(0.18f), 0.62f);
		}
	}

	private void RepairEnemyBaseByRatio(float ratio, Color color, string label = "")
	{
		if (ratio <= 0f)
		{
			return;
		}

		var repairAmount = _enemyBaseMaxHealth * ratio;
		_enemyBaseHealth = Mathf.Min(_enemyBaseMaxHealth, _enemyBaseHealth + repairAmount);
		_enemyBaseFlashTimer = 0.18f;
		SpawnEffect(EnemyBaseCorePosition, color.Lightened(0.08f), 10f, 28f, 0.22f);
		SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(repairAmount)}", color.Lightened(0.18f), 0.56f);
		if (!string.IsNullOrWhiteSpace(label))
		{
			SpawnFloatText(EnemyBaseCorePosition + new Vector2(0f, -60f), label, color.Lightened(0.24f), 0.62f);
		}
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
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -38f), $"+{Mathf.RoundToInt(repaired)}", new Color("b7efc5"), 0.56f);
		return repaired;
	}

	private void DamageBusByRatio(float ratio, Color color, string label = "")
	{
		if (ratio <= 0f)
		{
			return;
		}

		var damageAmount = _playerBaseMaxHealth * ratio;
		_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth - damageAmount);
		_playerBaseFlashTimer = 0.22f;
		AudioDirector.Instance?.PlayBaseHit(true, damageAmount);
		SpawnEffect(PlayerBaseCorePosition, color, 10f, 30f, 0.22f, false);
		SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -38f), $"-{Mathf.RoundToInt(damageAmount)}", color.Lightened(0.1f), 0.56f);
		if (!string.IsNullOrWhiteSpace(label))
		{
			SpawnFloatText(PlayerBaseCorePosition + new Vector2(0f, -60f), label, color.Lightened(0.18f), 0.62f);
		}
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

		if (_enemyBaseHealth <= 0f)
		{
			EndBattle(true);
			return;
		}

		if (_playerBaseHealth <= 0f)
		{
			EndBattle(false);
		}
	}

	private void EndBattle(bool playerWon)
	{
		if (_battleEnded)
		{
			return;
		}

		_battleEnded = true;
		AudioDirector.Instance?.SetBattlePressure(0.18f);
		_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth);
		_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth);

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
			_lanChallengeEndBaseText =
				$"Challenge {_challengeDefinition.Code}\n" +
				$"{(playerWon ? "Cleared" : "Failed")}  |  Score {scoreBreakdown.FinalScore}  |  Tier {medalLabel}\n" +
				$"{BuildStageBattleStatsText(stageResult)}\n" +
				$"{AsyncChallengeCatalog.BuildScoreSummary(scoreBreakdown)}\n" +
				$"{AsyncChallengeCatalog.BuildTargetSummary(_challengeDefinition, scoreBreakdown.FinalScore)}\n" +
				$"{StageObjectives.BuildOutcomeSummary(evaluation)}\n" +
				$"{BuildStageMissionDebriefText()}\n" +
				$"{BuildChallengeGhostResultSummary(scoreBreakdown.FinalScore, starsEarned)}\n" +
				$"Personal best: {GameState.Instance.GetAsyncChallengeBestScore(_challengeDefinition.Code)}";
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
			_endCenter.Visible = true;
			_endPanel.Visible = true;
			RefreshLanRaceEndPanel();
			RefreshOnlineRoomEndPanel(true);
			UpdateHud();
			return;
		}

		if (playerWon)
		{
			AudioDirector.Instance?.PlayVictory();
			var stageResult = BuildStageBattleResult();
			var evaluation = StageObjectives.EvaluateBattle(_stageData, stageResult, true);
			var bestStars = Mathf.Max(GameState.Instance.GetStageStars(_stage), evaluation.StarsEarned);
			var districtRewardSummary = GameState.Instance.ApplyVictory(_stage, _stageData.RewardGold, _stageData.RewardFood, evaluation.StarsEarned);
			_endLabel.Text =
				$"Victory on stage {_stage}: {_stageData.StageName}.\n" +
				$"{BuildStageBattleStatsText(stageResult)}\n" +
				$"{StageObjectives.BuildResultSummary(_stageData, evaluation, _stageData.RewardGold, _stageData.RewardFood, bestStars)}\n" +
				$"{BuildStageMissionDebriefText()}" +
				(string.IsNullOrWhiteSpace(districtRewardSummary) ? "" : $"\n{districtRewardSummary}");
			SetStatus("Gatehouse shattered. Route secured.");
		}
		else
		{
			AudioDirector.Instance?.PlayDefeat();
			var stageResult = BuildStageBattleResult();
			var evaluation = StageObjectives.EvaluateBattle(_stageData, stageResult, false);
			var bestStars = GameState.Instance.GetStageStars(_stage);
			GameState.Instance.ApplyDefeat(_stage);
			_endLabel.Text =
				$"Defeat on stage {_stage}: {_stageData.StageName}.\n" +
				$"{BuildStageBattleStatsText(stageResult)}\n" +
				$"Clear reward on success: +{_stageData.RewardGold} gold, +{_stageData.RewardFood} food   |   Best: {bestStars}/3\n" +
				$"{StageObjectives.BuildOutcomeSummary(evaluation)}\n" +
				$"{BuildStageMissionDebriefText()}";
			SetStatus("The war wagon was overrun. Regroup.");
		}

		_endCenter.Visible = true;
		_endPanel.Visible = true;
		UpdateHud();
	}

	private void RetreatToMap()
	{
		if (IsEndlessMode)
		{
			if (!_battleEnded)
			{
				_battleEnded = true;
				_playerBaseHealth = Mathf.Max(0f, _playerBaseHealth);
				_enemyBaseHealth = Mathf.Max(0f, _enemyBaseHealth);
				FinalizeEndlessRun(true);
				return;
			}

			SceneRouter.Instance.GoToEndless();
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

			if (IsLanRaceMode)
			{
				SceneRouter.Instance.GoToLanRace();
			}
			else
			{
				SceneRouter.Instance.GoToMultiplayer();
			}
			return;
		}

		if (!_battleEnded)
		{
			GameState.Instance.ApplyRetreat(_stage);
		}

		SceneRouter.Instance.GoToMap();
	}

	private StageBattleResult BuildStageBattleResult()
	{
		var completedMissionEvents = _stageMissions.Count(mission => mission.Completed);
		var failedMissionEvents = _stageMissions.Count(mission => mission.Failed);
		return new StageBattleResult
		{
			PlayerBaseHealth = _playerBaseHealth,
			PlayerBaseMaxHealth = _playerBaseMaxHealth,
			Elapsed = _elapsed,
			PlayerDeployments = _playerDeployments,
			EnemyDefeats = _enemyDefeats,
			PlayerHazardHits = _playerHazardHits,
			PlayerSignalJamSeconds = _playerSignalJamSeconds,
			CompletedMissionEvents = completedMissionEvents,
			FailedMissionEvents = failedMissionEvents,
			TotalMissionEvents = _stageMissions.Count
		};
	}

	private void FinalizeEndlessRun(bool retreated)
	{
		AudioDirector.Instance?.SetBattlePressure(0.14f);
		var rewardGold = CalculateEndlessScrapReward();
		var rewardFood = CalculateEndlessFuelReward();
		GameState.Instance.ApplyEndlessResult(_activeRouteId, _spawnDirector.EndlessWaveNumber, _elapsed, _enemyDefeats, rewardGold, rewardFood, retreated);
		_endLabel.Text = BuildEndlessRunDebriefText(rewardGold, rewardFood, retreated);
		SetStatus(retreated
			? "The caravan withdrew in good order and banked its spoils."
			: "The caravan was eventually overrun. Rear scouts recovered what they could.");
		if (!retreated)
		{
			AudioDirector.Instance?.PlayDefeat();
		}

		_endCenter.Visible = true;
		_endPanel.Visible = true;
		UpdateHud();
	}

	private string BuildStageBattleStatsText(StageBattleResult result)
	{
		var routeLabel = ResolveRouteLabel(_activeRouteId);
		var hullPercent = Mathf.RoundToInt(Mathf.Clamp(result.PlayerBaseHealth / Mathf.Max(1f, result.PlayerBaseMaxHealth), 0f, 1f) * 100f);
		return
			$"{routeLabel}  |  Stage {_stage}  |  {_stageData.StageName}\n" +
			$"Time {result.Elapsed:0.0}s  |  Hull {hullPercent}%  |  Enemy defeats {result.EnemyDefeats}  |  Deployments {result.PlayerDeployments}\n" +
			$"Hazard hits {result.PlayerHazardHits}  |  Signal jam {result.PlayerSignalJamSeconds:0.0}s";
	}

	private float ResolveBattleAudioPressure()
	{
		var enemyCountPressure = Mathf.Clamp(CountTeamUnits(Team.Enemy) / 12f, 0f, 1f) * 0.45f;
		var hullPressure = (1f - Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f)) * 0.3f;
		var pendingPressure = Mathf.Clamp(_spawnDirector.PendingSpawnCount / 10f, 0f, 1f) * 0.12f;
		var hazardPressure = _stageHazards.Any(hazard => hazard.WarningIssued || hazard.NextTriggerTime <= _elapsed + 2f) ? 0.08f : 0f;
		var missionPressure = _stageMissions.Any(mission => mission.Started && !mission.Completed && !mission.Failed) ? 0.08f : 0f;
		var endlessPressure = IsEndlessMode
			? Mathf.Clamp(_spawnDirector.EndlessWaveNumber / 40f, 0f, 1f) * 0.14f
			: 0f;
		return Mathf.Clamp(enemyCountPressure + hullPressure + pendingPressure + hazardPressure + missionPressure + endlessPressure, 0f, 1f);
	}

	private string BuildStageMissionDebriefText()
	{
		var directiveText = BuildCampaignDirectiveBattleText();
		if (_stageMissions.Count == 0)
		{
			return string.IsNullOrWhiteSpace(directiveText)
				? "Battlefield events: none"
				: $"{directiveText}\nBattlefield events: none";
		}

		var lines = new List<string>();
		if (!string.IsNullOrWhiteSpace(directiveText))
		{
			lines.Add(directiveText);
		}

		lines.Add("Battlefield events:");

		foreach (var mission in _stageMissions)
		{
			var prefix = mission.Completed
				? "[OK]"
				: mission.Failed
					? "[X]"
					: "[--]";
			lines.Add($"{prefix} {StageMissionEvents.ResolveTitle(mission.Definition)}  |  {BuildStageMissionDebriefDetail(mission)}");
		}

		return string.Join("\n", lines);
	}

	private string BuildCampaignDirectiveBattleText()
	{
		if (IsEndlessMode || IsChallengeMode || !GameState.Instance.IsCampaignDirectiveArmed(_stage))
		{
			return "";
		}

		var directive = GameState.Instance.GetCampaignDirective(_stage);
		if (directive == null)
		{
			return "";
		}

		var bountyStatus = GameState.Instance.HasClaimedCampaignDirective(directive.Id)
			? "bounty already claimed"
			: CampaignDirectiveCatalog.BuildRewardSummary(directive);
		return $"Heroic directive: {directive.Title}  |  {directive.Summary}  |  {bountyStatus}";
	}

	private string BuildStageMissionDebriefDetail(StageMissionState mission)
	{
		if (mission.Completed)
		{
			return StageMissionEvents.ResolveRewardSummary(mission.Definition);
		}

		if (mission.Failed)
		{
			return StageMissionEvents.ResolvePenaltySummary(mission.Definition);
		}

		if (!mission.Started)
		{
			return $"Not reached before route end (arms at {mission.Definition.StartTime:0.0}s)";
		}

		var target = Mathf.Max(1f, mission.Definition.TargetSeconds);
		return $"{mission.Progress:0.0}/{target:0.0}s secured when the route ended";
	}

	private string BuildEndlessRunDebriefText(int rewardGold, int rewardFood, bool retreated)
	{
		var routeLabel = ResolveRouteLabel(_activeRouteId);
		var outcomeLine = retreated
			? $"Endless retreat banked on {routeLabel}."
			: $"Endless run ended on {routeLabel}.";
		return
			$"{outcomeLine}\n" +
			$"Wave reached: {_spawnDirector.EndlessWaveNumber}  |  Survival: {_elapsed:0.0}s  |  Enemy defeats: {_enemyDefeats}\n" +
			$"Banked payout: +{rewardGold} gold, +{rewardFood} food  |  Boon: {EndlessBoonCatalog.Get(_endlessBoonId).Title}  |  Path: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}\n" +
			$"Bonus bank: directives {FormatSignedInt(_endlessDirectiveScrapBonus)} gold / {FormatSignedInt(_endlessDirectiveFuelBonus)} food  |  contacts {FormatSignedInt(_endlessContactScrapBonus)} gold / {FormatSignedInt(_endlessContactFuelBonus)} food  |  bosses {FormatSignedInt(_endlessBossScrapBonus)} gold / {FormatSignedInt(_endlessBossFuelBonus)} food\n" +
			$"{BuildEndlessRunUpgradeSummary()}\n" +
			$"{BuildEndlessBossCheckpointText()}\n" +
			$"{BuildEndlessDirectiveCheckpointSummary()}\n" +
			$"{BuildEndlessContactCheckpointSummary()}\n" +
			$"Tradeoff report: {_endlessContactTradeoffLabel}\n" +
			$"Contact telemetry: {BuildEndlessContactTelemetryText()}\n" +
			$"Battlefield event: {_endlessBattlefieldEventLabel}\n" +
			$"Caravan support: {_endlessSupportEventLabel}\n" +
			$"Record: wave {GameState.Instance.BestEndlessWave}  |  {GameState.Instance.BestEndlessTimeSeconds:0.0}s";
	}

	private string BuildEndlessRunUpgradeSummary()
	{
		if (_endlessRunUpgrades.Count == 0)
		{
			return "Run upgrades: none";
		}

		var labels = _endlessRunUpgrades
			.Select(id => GetDraftOption(id).Title)
			.OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		return "Run upgrades: " + string.Join(", ", labels);
	}

	private int CalculateEndlessScrapReward()
	{
		var timeBonus = Mathf.FloorToInt(_elapsed / 18f) * 3;
		var reward = Math.Max(0, (_spawnDirector.EndlessWaveNumber * 16) + (_enemyDefeats * 2) + timeBonus);
		reward = Mathf.RoundToInt(reward * _endlessScrapScale);
		reward = Mathf.RoundToInt(reward * ResolveRouteForkScrapScale());
		if (_endlessBoonId == EndlessBoonCatalog.SalvageCacheId)
		{
			reward = Mathf.RoundToInt(reward * 1.25f);
		}

		return Math.Max(0, reward + _endlessDirectiveScrapBonus + _endlessContactScrapBonus + _endlessBossScrapBonus);
	}

	private int CalculateEndlessFuelReward()
	{
		if (_spawnDirector.EndlessWaveNumber <= 0)
		{
			return 0;
		}

		return Math.Max(0, (_spawnDirector.EndlessWaveNumber / 4) + _endlessDirectiveFuelBonus + _endlessContactFuelBonus + _endlessBossFuelBonus);
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
				_courage = Mathf.Min(_maxCourage, _courage + 25f);
				break;
		}
	}

	private float ResolveRouteForkScrapScale()
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
