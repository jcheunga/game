using System;
using System.Collections.Generic;
using Godot;

public partial class AudioDirector : Node
{
	private const int SampleRate = 22050;
	private const string UiHoverCueId = "ui_hover";
	private const string UiConfirmCueId = "ui_confirm";
	private const string SceneChangeCueId = "scene_change";
	private const string DeployCueId = "deploy";
	private const string ImpactLightCueId = "impact_light";
	private const string ImpactHeavyCueId = "impact_heavy";
	private const string BusHitCueId = "bus_hit";
	private const string BarricadeHitCueId = "barricade_hit";
	private const string RepairCueId = "repair";
	private const string HazardWarningCueId = "hazard_warning";
	private const string HazardStrikeCueId = "hazard_strike";
	private const string VictoryCueId = "victory";
	private const string DefeatCueId = "defeat";
	private const string SpellCastCueId = "spell_cast";
	private const string BossSpawnCueId = "boss_spawn";
	private const string UpgradeConfirmCueId = "upgrade_confirm";
	private const string MenuAmbienceCueId = "ambience_menu";
	private const string BattleAmbienceCueId = "ambience_battle";
	private const string EndlessAmbienceCueId = "ambience_endless";
	private const string MultiplayerAmbienceCueId = "ambience_multiplayer";
	private const string ShopAmbienceCueId = "ambience_shop";
	private const string RoadAmbienceCueId = "ambience_route_road";
	private const string HarborAmbienceCueId = "ambience_route_harbor";
	private const string FoundryAmbienceCueId = "ambience_route_foundry";
	private const string QuarantineAmbienceCueId = "ambience_route_quarantine";
	private const string ThornwallAmbienceCueId = "ambience_route_thornwall";
	private const string BasilicaAmbienceCueId = "ambience_route_basilica";
	private const string MireAmbienceCueId = "ambience_route_mire";
	private const string SteppeAmbienceCueId = "ambience_route_steppe";
	private const string GloamwoodAmbienceCueId = "ambience_route_gloamwood";
	private const string CitadelAmbienceCueId = "ambience_route_citadel";

	private readonly struct ToneLayer
	{
		public ToneLayer(float startFrequency, float endFrequency, float gain, bool square = false)
		{
			StartFrequency = startFrequency;
			EndFrequency = endFrequency;
			Gain = gain;
			Square = square;
		}

		public float StartFrequency { get; }
		public float EndFrequency { get; }
		public float Gain { get; }
		public bool Square { get; }
	}

	public static AudioDirector Instance { get; private set; }

	private readonly Dictionary<string, AudioStreamWav> _cues = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, double> _lastCueTimes = new(StringComparer.OrdinalIgnoreCase);
	private readonly RandomNumberGenerator _rng = new();

	private Timer _ambienceTimer = null!;
	private string _currentScenePath = string.Empty;
	private string _currentAmbienceContextKey = string.Empty;
	private string _currentAmbienceCueId = string.Empty;
	private float _currentAmbienceVolumeDb = -24f;
	private Vector2 _currentAmbienceIntervalRange = new(5.8f, 7.4f);
	private Vector2 _currentAmbiencePitchRange = new(0.985f, 1.015f);
	private float _battlePressure;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (IsInsideTree())
		{
			GetTree().NodeAdded -= OnTreeNodeAdded;
		}

		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		_rng.Randomize();
		BuildCueLibrary();

		_ambienceTimer = new Timer
		{
			OneShot = true,
			ProcessCallback = Timer.TimerProcessCallback.Idle
		};
		_ambienceTimer.Timeout += OnAmbienceTimerTimeout;
		AddChild(_ambienceTimer);

		GetTree().NodeAdded += OnTreeNodeAdded;
		BindButtonsRecursive(GetTree().Root);
		UpdateSceneContext(true);
	}

	public override void _Process(double delta)
	{
		UpdateSceneContext();
	}

	public void PlayUiHover()
	{
		PlayCue(UiHoverCueId, -22f, 1.02f + _rng.RandfRange(-0.04f, 0.04f), 0.045f);
	}

	public void PlayUiConfirm()
	{
		PlayCue(UiConfirmCueId, -15f, 1f + _rng.RandfRange(-0.03f, 0.03f), 0.06f);
	}

	public void PlaySceneChange()
	{
		PlayCue(SceneChangeCueId, -14f, 1f + _rng.RandfRange(-0.02f, 0.02f), 0.08f);
	}

	public void PlayDeploy(UnitDefinition definition)
	{
		var pitchScale = 0.92f + Mathf.Clamp(definition.Cost / 38f, 0f, 0.16f);
		PlayCue(DeployCueId, -10f, pitchScale + _rng.RandfRange(-0.03f, 0.03f), 0.05f);
	}

	public void PlayImpact(float damage)
	{
		var heavy = damage >= 18f;
		PlayCue(
			heavy ? ImpactHeavyCueId : ImpactLightCueId,
			heavy ? -11f : -15f,
			1f + _rng.RandfRange(-0.06f, 0.06f),
			heavy ? 0.08f : 0.045f,
			heavy ? "impact_heavy" : "impact_light");
	}

	public void PlayBaseHit(bool playerBase, float damage)
	{
		var volume = damage >= 12f ? -7.5f : -10.5f;
		PlayCue(
			playerBase ? BusHitCueId : BarricadeHitCueId,
			volume,
			1f + _rng.RandfRange(-0.04f, 0.04f),
			0.09f,
			playerBase ? "bus_hit" : "barricade_hit");
	}

	public void PlayBusRepair(float amount)
	{
		var volume = amount >= 12f ? -9f : -12f;
		PlayCue(RepairCueId, volume, 1f + _rng.RandfRange(-0.03f, 0.03f), 0.08f, "bus_repair");
	}

	public void PlayHazardWarning()
	{
		PlayCue(HazardWarningCueId, -11.5f, 1f + _rng.RandfRange(-0.03f, 0.03f), 0.18f, "hazard_warning");
	}

	public void PlayHazardStrike()
	{
		PlayCue(HazardStrikeCueId, -9.5f, 1f + _rng.RandfRange(-0.04f, 0.04f), 0.08f, "hazard_strike");
	}

	public void PlayVictory()
	{
		PlayCue(VictoryCueId, -8f, 1f, 0.3f);
	}

	public void PlayDefeat()
	{
		PlayCue(DefeatCueId, -7f, 1f, 0.3f);
	}

	public void PlaySpellCast(string effectType)
	{
		var pitchScale = effectType switch
		{
			"fireball" => 0.88f,
			"heal" => 1.12f,
			"frost_burst" => 1.04f,
			"lightning_strike" => 0.94f,
			"barrier_ward" => 1.08f,
			_ => 1f
		};
		PlayCue(SpellCastCueId, -9f, pitchScale + _rng.RandfRange(-0.03f, 0.03f), 0.1f, "spell_cast");
	}

	public void PlayBossSpawn()
	{
		PlayCue(BossSpawnCueId, -6f, 1f + _rng.RandfRange(-0.02f, 0.02f), 0.4f, "boss_spawn");
	}

	public void PlayUpgradeConfirm()
	{
		PlayCue(UpgradeConfirmCueId, -10f, 1f + _rng.RandfRange(-0.03f, 0.03f), 0.08f, "upgrade_confirm");
	}

	public void RefreshMixFromState()
	{
		UpdateSceneContext(true);
	}

	public void SetBattlePressure(float pressure)
	{
		var clamped = Mathf.Clamp(pressure, 0f, 1f);
		if (Mathf.Abs(clamped - _battlePressure) < 0.06f)
		{
			return;
		}

		_battlePressure = clamped;
		UpdateSceneContext(true);
	}

	private void OnTreeNodeAdded(Node node)
	{
		if (node is Button button)
		{
			BindButton(button);
		}
	}

	private void BindButtonsRecursive(Node node)
	{
		if (node is Button button)
		{
			BindButton(button);
		}

		foreach (Node child in node.GetChildren())
		{
			BindButtonsRecursive(child);
		}
	}

	private void BindButton(Button button)
	{
		const string audioBoundMeta = "audio_bound";
		if (button.HasMeta(audioBoundMeta))
		{
			return;
		}

		button.SetMeta(audioBoundMeta, true);
		button.MouseEntered += () => PlayUiHover();
		button.FocusEntered += () => PlayUiHover();
		button.Pressed += () => PlayUiConfirm();
	}

	private void UpdateSceneContext(bool force = false)
	{
		var scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (scenePath != SceneRouter.BattleScene)
		{
			_battlePressure = 0f;
		}

		var contextKey = ResolveAmbienceContextKey(scenePath);
		if (!force && scenePath == _currentScenePath && contextKey == _currentAmbienceContextKey)
		{
			return;
		}

		_currentScenePath = scenePath;
		_currentAmbienceContextKey = contextKey;

		var nextCueId = ResolveAmbienceCueId(scenePath);
		var nextVolumeDb = ResolveAmbienceVolumeDb(scenePath);
		var nextIntervalRange = ResolveAmbienceIntervalRange(scenePath);
		var nextPitchRange = ResolveAmbiencePitchRange(scenePath);
		var ambienceChanged =
			nextCueId != _currentAmbienceCueId ||
			Mathf.Abs(nextVolumeDb - _currentAmbienceVolumeDb) > 0.05f ||
			nextIntervalRange != _currentAmbienceIntervalRange ||
			nextPitchRange != _currentAmbiencePitchRange;

		_currentAmbienceCueId = nextCueId;
		_currentAmbienceVolumeDb = nextVolumeDb;
		_currentAmbienceIntervalRange = nextIntervalRange;
		_currentAmbiencePitchRange = nextPitchRange;

		if (string.IsNullOrWhiteSpace(_currentAmbienceCueId))
		{
			_ambienceTimer.Stop();
			return;
		}

		if (ambienceChanged || !_ambienceTimer.IsStopped())
		{
			ScheduleNextAmbiencePulse(0.35f);
		}
	}

	private string ResolveAmbienceCueId(string scenePath)
	{
		var routeCueId = ResolveRouteAmbienceCueId(ResolveContextRouteId(scenePath));
		return scenePath switch
		{
			SceneRouter.MainMenuScene => MenuAmbienceCueId,
			SceneRouter.MapScene => string.IsNullOrWhiteSpace(routeCueId) ? MenuAmbienceCueId : routeCueId,
			SceneRouter.LoadoutScene => string.IsNullOrWhiteSpace(routeCueId) ? MenuAmbienceCueId : routeCueId,
			SceneRouter.ShopScene => string.IsNullOrWhiteSpace(routeCueId) ? ShopAmbienceCueId : routeCueId,
			SceneRouter.EndlessScene => string.IsNullOrWhiteSpace(routeCueId) ? EndlessAmbienceCueId : routeCueId,
			SceneRouter.MultiplayerScene => string.IsNullOrWhiteSpace(routeCueId) ? MultiplayerAmbienceCueId : routeCueId,
			SceneRouter.BattleScene => string.IsNullOrWhiteSpace(routeCueId) ? BattleAmbienceCueId : routeCueId,
			_ => string.Empty
		};
	}

	private float ResolveAmbienceVolumeDb(string scenePath)
	{
		return scenePath switch
		{
			SceneRouter.BattleScene => Mathf.Lerp(-23f, -18.5f, _battlePressure),
			SceneRouter.EndlessScene => -23f,
			SceneRouter.MultiplayerScene => -24f,
			SceneRouter.ShopScene => -24f,
			_ => -25f
		};
	}

	private Vector2 ResolveAmbienceIntervalRange(string scenePath)
	{
		return scenePath switch
		{
			SceneRouter.BattleScene => new Vector2(
				Mathf.Lerp(3.6f, 1.9f, _battlePressure),
				Mathf.Lerp(4.8f, 3.1f, _battlePressure)),
			SceneRouter.EndlessScene => new Vector2(3.1f, 4.2f),
			SceneRouter.MultiplayerScene => new Vector2(4.3f, 5.4f),
			SceneRouter.ShopScene => new Vector2(4.6f, 5.8f),
			_ => new Vector2(5.2f, 6.8f)
		};
	}

	private Vector2 ResolveAmbiencePitchRange(string scenePath)
	{
		var routeId = ResolveContextRouteId(scenePath);
		var range = routeId switch
		{
			RouteCatalog.CityId => new Vector2(0.99f, 1.02f),
			RouteCatalog.HarborId => new Vector2(0.94f, 0.98f),
			RouteCatalog.FoundryId => new Vector2(0.88f, 0.93f),
			RouteCatalog.QuarantineId => new Vector2(0.9f, 0.95f),
			RouteCatalog.ThornwallId => new Vector2(1f, 1.04f),
			RouteCatalog.BasilicaId => new Vector2(0.86f, 0.91f),
			RouteCatalog.MireId => new Vector2(0.83f, 0.89f),
			RouteCatalog.SteppeId => new Vector2(1.03f, 1.08f),
			RouteCatalog.GloamwoodId => new Vector2(0.88f, 0.94f),
			RouteCatalog.CitadelId => new Vector2(0.94f, 1f),
			_ => new Vector2(0.985f, 1.015f)
		};

		if (scenePath == SceneRouter.BattleScene)
		{
			return new Vector2(
				Mathf.Max(0.78f, range.X - (_battlePressure * 0.03f)),
				Mathf.Min(1.12f, range.Y + (_battlePressure * 0.04f)));
		}

		return range;
	}

	private string ResolveAmbienceContextKey(string scenePath)
	{
		var routeId = ResolveContextRouteId(scenePath);
		var pressureBucket = scenePath == SceneRouter.BattleScene
			? Mathf.RoundToInt(_battlePressure * 4f)
			: 0;
		return $"{scenePath}|{routeId}|{pressureBucket}";
	}

	private string ResolveContextRouteId(string scenePath)
	{
		if (GameState.Instance == null)
		{
			return "";
		}

		return scenePath switch
		{
			SceneRouter.MapScene => ResolveStageRouteId(GameState.Instance.SelectedStage),
			SceneRouter.LoadoutScene => ResolveStageRouteId(GameState.Instance.SelectedStage),
			SceneRouter.ShopScene => ResolveStageRouteId(GameState.Instance.SelectedStage),
			SceneRouter.EndlessScene => RouteCatalog.Get(GameData.GetLatestStageForMap(GameState.Instance.SelectedEndlessRouteId).MapId).Id,
			SceneRouter.MultiplayerScene => ResolveStageRouteId(GameState.Instance.GetSelectedAsyncChallenge().Stage),
			SceneRouter.BattleScene => ResolveBattleRouteId(),
			_ => ""
		};
	}

	private string ResolveBattleRouteId()
	{
		if (GameState.Instance == null)
		{
			return "";
		}

		return GameState.Instance.CurrentBattleMode switch
		{
			BattleRunMode.Endless => RouteCatalog.Get(GameData.GetLatestStageForMap(GameState.Instance.SelectedEndlessRouteId).MapId).Id,
			BattleRunMode.AsyncChallenge => ResolveStageRouteId(GameState.Instance.GetSelectedAsyncChallenge().Stage),
			_ => ResolveStageRouteId(GameState.Instance.SelectedStage)
		};
	}

	private static string ResolveStageRouteId(int stage)
	{
		if (GameState.Instance == null || GameState.Instance.MaxStage <= 0)
		{
			return "";
		}

		var clampedStage = Mathf.Clamp(stage, 1, GameState.Instance.MaxStage);
		return RouteCatalog.Get(GameData.GetStage(clampedStage).MapId).Id;
	}

	private static string ResolveRouteAmbienceCueId(string routeId)
	{
		return routeId switch
		{
			RouteCatalog.CityId => RoadAmbienceCueId,
			RouteCatalog.HarborId => HarborAmbienceCueId,
			RouteCatalog.FoundryId => FoundryAmbienceCueId,
			RouteCatalog.QuarantineId => QuarantineAmbienceCueId,
			RouteCatalog.ThornwallId => ThornwallAmbienceCueId,
			RouteCatalog.BasilicaId => BasilicaAmbienceCueId,
			RouteCatalog.MireId => MireAmbienceCueId,
			RouteCatalog.SteppeId => SteppeAmbienceCueId,
			RouteCatalog.GloamwoodId => GloamwoodAmbienceCueId,
			RouteCatalog.CitadelId => CitadelAmbienceCueId,
			_ => ""
		};
	}

	private void OnAmbienceTimerTimeout()
	{
		if (string.IsNullOrWhiteSpace(_currentAmbienceCueId))
		{
			return;
		}

		PlayCue(
			_currentAmbienceCueId,
			_currentAmbienceVolumeDb,
			_rng.RandfRange(_currentAmbiencePitchRange.X, _currentAmbiencePitchRange.Y),
			isAmbience: true);
		ScheduleNextAmbiencePulse();
	}

	private void ScheduleNextAmbiencePulse(float delaySeconds = -1f)
	{
		if (string.IsNullOrWhiteSpace(_currentAmbienceCueId))
		{
			_ambienceTimer.Stop();
			return;
		}

		var waitTime = delaySeconds >= 0f
			? delaySeconds
			: _rng.RandfRange(_currentAmbienceIntervalRange.X, _currentAmbienceIntervalRange.Y);
		_ambienceTimer.Start(waitTime);
	}

	private void PlayCue(
		string cueId,
		float volumeDb,
		float pitchScale,
		float minIntervalSeconds = 0f,
		string cooldownKey = "",
		bool isAmbience = false)
	{
		if (!_cues.TryGetValue(cueId, out var cue))
		{
			return;
		}

		if (GameState.Instance != null && GameState.Instance.AudioMuted)
		{
			return;
		}

		var throttleKey = string.IsNullOrWhiteSpace(cooldownKey) ? cueId : cooldownKey;
		var now = Time.GetTicksMsec() / 1000.0;
		if (minIntervalSeconds > 0f &&
			_lastCueTimes.TryGetValue(throttleKey, out var lastPlayedAt) &&
			now - lastPlayedAt < minIntervalSeconds)
		{
			return;
		}

		_lastCueTimes[throttleKey] = now;
		var resolvedVolumeDb = ResolveMixedVolumeDb(volumeDb, isAmbience);

		var player = new AudioStreamPlayer
		{
			Stream = cue,
			VolumeDb = resolvedVolumeDb,
			PitchScale = Mathf.Max(0.5f, pitchScale),
			Bus = "Master"
		};
		player.Finished += () => player.QueueFree();
		AddChild(player);
		player.Play();
	}

	private void BuildCueLibrary()
	{
		_cues.Clear();
		RegisterCue(UiHoverCueId, CreateCue(0.06f, 0.004f, 0.04f, 0.34f, 0.02f, 0f, 0f, 0f,
			new ToneLayer(960f, 1260f, 1f),
			new ToneLayer(1440f, 1680f, 0.18f)));
		RegisterCue(UiConfirmCueId, CreateCue(0.12f, 0.005f, 0.08f, 0.42f, 0.01f, 0f, 0f, 0f,
			new ToneLayer(520f, 760f, 1f, true),
			new ToneLayer(780f, 1040f, 0.45f)));
		RegisterCue(SceneChangeCueId, CreateCue(0.22f, 0.01f, 0.14f, 0.36f, 0.01f, 4.5f, 0.008f, 0f,
			new ToneLayer(300f, 560f, 1f),
			new ToneLayer(480f, 860f, 0.34f)));
		RegisterCue(DeployCueId, CreateCue(0.18f, 0.005f, 0.09f, 0.46f, 0.015f, 0f, 0f, 0f,
			new ToneLayer(180f, 320f, 1f, true),
			new ToneLayer(320f, 480f, 0.34f)));
		RegisterCue(ImpactLightCueId, CreateCue(0.08f, 0.002f, 0.05f, 0.42f, 0.18f, 0f, 0f, 0f,
			new ToneLayer(260f, 140f, 1f, true),
			new ToneLayer(420f, 220f, 0.2f)));
		RegisterCue(ImpactHeavyCueId, CreateCue(0.16f, 0.003f, 0.1f, 0.5f, 0.22f, 0f, 0f, 0f,
			new ToneLayer(170f, 84f, 1f, true),
			new ToneLayer(260f, 110f, 0.35f)));
		RegisterCue(BusHitCueId, CreateCue(0.19f, 0.004f, 0.12f, 0.48f, 0.16f, 0f, 0f, 0f,
			new ToneLayer(120f, 72f, 1f, true),
			new ToneLayer(220f, 98f, 0.35f)));
		RegisterCue(BarricadeHitCueId, CreateCue(0.15f, 0.004f, 0.09f, 0.42f, 0.1f, 0f, 0f, 0f,
			new ToneLayer(180f, 92f, 1f, true),
			new ToneLayer(280f, 160f, 0.24f)));
		RegisterCue(RepairCueId, CreateCue(0.2f, 0.01f, 0.12f, 0.38f, 0.02f, 2.5f, 0.01f, 0f,
			new ToneLayer(260f, 420f, 1f),
			new ToneLayer(390f, 620f, 0.32f)));
		RegisterCue(HazardWarningCueId, CreateCue(0.18f, 0.004f, 0.12f, 0.36f, 0.08f, 0f, 0f, 7.5f,
			new ToneLayer(820f, 920f, 1f, true),
			new ToneLayer(980f, 1180f, 0.18f)));
		RegisterCue(HazardStrikeCueId, CreateCue(0.24f, 0.003f, 0.18f, 0.44f, 0.12f, 0f, 0f, 0f,
			new ToneLayer(640f, 180f, 1f, true),
			new ToneLayer(300f, 90f, 0.34f)));
		RegisterCue(VictoryCueId, CreateCue(0.58f, 0.02f, 0.22f, 0.42f, 0.01f, 0f, 0f, 0f,
			new ToneLayer(260f, 392f, 1f),
			new ToneLayer(392f, 520f, 0.44f),
			new ToneLayer(520f, 660f, 0.26f)));
		RegisterCue(DefeatCueId, CreateCue(0.66f, 0.02f, 0.28f, 0.4f, 0.03f, 0f, 0f, 0f,
			new ToneLayer(260f, 160f, 1f, true),
			new ToneLayer(200f, 116f, 0.4f),
			new ToneLayer(132f, 74f, 0.24f)));
		RegisterCue(SpellCastCueId, CreateCue(0.26f, 0.008f, 0.14f, 0.38f, 0.02f, 3.2f, 0.01f, 0f,
			new ToneLayer(340f, 580f, 1f),
			new ToneLayer(540f, 860f, 0.32f),
			new ToneLayer(720f, 1120f, 0.14f)));
		RegisterCue(BossSpawnCueId, CreateCue(0.72f, 0.02f, 0.32f, 0.38f, 0.06f, 0f, 0f, 3.8f,
			new ToneLayer(110f, 86f, 1f, true),
			new ToneLayer(168f, 128f, 0.38f),
			new ToneLayer(220f, 172f, 0.18f)));
		RegisterCue(UpgradeConfirmCueId, CreateCue(0.2f, 0.008f, 0.1f, 0.4f, 0.01f, 0f, 0f, 0f,
			new ToneLayer(380f, 560f, 1f),
			new ToneLayer(560f, 760f, 0.38f)));
		RegisterCue(MenuAmbienceCueId, CreateCue(1.8f, 0.12f, 0.36f, 0.22f, 0.04f, 0.18f, 0.01f, 0.55f,
			new ToneLayer(108f, 112f, 1f),
			new ToneLayer(162f, 166f, 0.3f),
			new ToneLayer(216f, 220f, 0.16f)));
		RegisterCue(ShopAmbienceCueId, CreateCue(1.7f, 0.08f, 0.32f, 0.2f, 0.03f, 0.25f, 0.008f, 1.4f,
			new ToneLayer(124f, 132f, 1f),
			new ToneLayer(248f, 264f, 0.24f),
			new ToneLayer(372f, 396f, 0.12f)));
		RegisterCue(BattleAmbienceCueId, CreateCue(1.6f, 0.08f, 0.22f, 0.26f, 0.08f, 0.3f, 0.012f, 2.2f,
			new ToneLayer(88f, 92f, 1f, true),
			new ToneLayer(176f, 184f, 0.28f),
			new ToneLayer(264f, 276f, 0.1f)));
		RegisterCue(EndlessAmbienceCueId, CreateCue(1.7f, 0.08f, 0.24f, 0.24f, 0.1f, 0.34f, 0.015f, 2.8f,
			new ToneLayer(82f, 86f, 1f, true),
			new ToneLayer(164f, 172f, 0.26f),
			new ToneLayer(246f, 258f, 0.12f)));
		RegisterCue(MultiplayerAmbienceCueId, CreateCue(1.45f, 0.08f, 0.2f, 0.2f, 0.04f, 0f, 0f, 5.2f,
			new ToneLayer(164f, 168f, 1f, true),
			new ToneLayer(328f, 336f, 0.2f),
			new ToneLayer(492f, 504f, 0.08f)));
		RegisterCue(RoadAmbienceCueId, CreateCue(1.75f, 0.11f, 0.34f, 0.22f, 0.04f, 0.16f, 0.008f, 0.6f,
			new ToneLayer(108f, 112f, 1f),
			new ToneLayer(164f, 168f, 0.32f),
			new ToneLayer(218f, 224f, 0.14f)));
		RegisterCue(HarborAmbienceCueId, CreateCue(1.9f, 0.12f, 0.38f, 0.2f, 0.06f, 0.24f, 0.01f, 0.4f,
			new ToneLayer(96f, 100f, 1f),
			new ToneLayer(144f, 148f, 0.28f),
			new ToneLayer(286f, 292f, 0.1f)));
		RegisterCue(FoundryAmbienceCueId, CreateCue(1.58f, 0.08f, 0.28f, 0.24f, 0.08f, 0f, 0f, 1.8f,
			new ToneLayer(76f, 82f, 1f, true),
			new ToneLayer(152f, 160f, 0.24f),
			new ToneLayer(228f, 244f, 0.08f)));
		RegisterCue(QuarantineAmbienceCueId, CreateCue(1.66f, 0.08f, 0.3f, 0.22f, 0.05f, 0.28f, 0.014f, 1.2f,
			new ToneLayer(84f, 88f, 1f),
			new ToneLayer(168f, 174f, 0.24f),
			new ToneLayer(336f, 348f, 0.08f)));
		RegisterCue(ThornwallAmbienceCueId, CreateCue(1.72f, 0.1f, 0.34f, 0.22f, 0.04f, 0.18f, 0.01f, 0.75f,
			new ToneLayer(116f, 122f, 1f),
			new ToneLayer(174f, 182f, 0.26f),
			new ToneLayer(234f, 244f, 0.1f)));
		RegisterCue(BasilicaAmbienceCueId, CreateCue(1.82f, 0.14f, 0.42f, 0.2f, 0.03f, 0.12f, 0.006f, 0.3f,
			new ToneLayer(92f, 94f, 1f),
			new ToneLayer(184f, 188f, 0.22f),
			new ToneLayer(368f, 376f, 0.12f)));
		RegisterCue(MireAmbienceCueId, CreateCue(1.86f, 0.12f, 0.4f, 0.2f, 0.07f, 0.22f, 0.012f, 0.25f,
			new ToneLayer(78f, 82f, 1f),
			new ToneLayer(118f, 124f, 0.3f),
			new ToneLayer(236f, 244f, 0.08f)));
		RegisterCue(SteppeAmbienceCueId, CreateCue(1.68f, 0.08f, 0.28f, 0.22f, 0.03f, 0.3f, 0.012f, 0.9f,
			new ToneLayer(132f, 138f, 1f),
			new ToneLayer(198f, 206f, 0.24f),
			new ToneLayer(396f, 408f, 0.08f)));
		RegisterCue(GloamwoodAmbienceCueId, CreateCue(1.74f, 0.1f, 0.36f, 0.2f, 0.05f, 0.2f, 0.012f, 0.55f,
			new ToneLayer(88f, 92f, 1f),
			new ToneLayer(132f, 136f, 0.28f),
			new ToneLayer(264f, 272f, 0.1f)));
		RegisterCue(CitadelAmbienceCueId, CreateCue(1.62f, 0.08f, 0.3f, 0.24f, 0.03f, 0f, 0f, 1.4f,
			new ToneLayer(110f, 114f, 1f, true),
			new ToneLayer(220f, 228f, 0.24f),
			new ToneLayer(330f, 342f, 0.1f)));
	}

	private void RegisterCue(string id, AudioStreamWav cue)
	{
		_cues[id] = cue;
	}

	private AudioStreamWav CreateCue(
		float durationSeconds,
		float attackSeconds,
		float releaseSeconds,
		float masterGain,
		float noiseMix,
		float vibratoHz,
		float vibratoDepth,
		float tremoloHz,
		params ToneLayer[] layers)
	{
		var duration = Mathf.Max(0.04f, durationSeconds);
		var sampleCount = Math.Max(2, Mathf.RoundToInt(duration * SampleRate));
		var pcmData = new byte[sampleCount * sizeof(short)];
		var phases = new float[layers.Length];

		for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
		{
			var t = sampleIndex / (float)SampleRate;
			var blend = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
			var sampleValue = 0f;

			for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
			{
				var layer = layers[layerIndex];
				var frequency = Mathf.Lerp(layer.StartFrequency, layer.EndFrequency, blend);
				if (vibratoHz > 0f && vibratoDepth > 0f)
				{
					frequency *= 1f + (Mathf.Sin(t * Mathf.Tau * vibratoHz) * vibratoDepth);
				}

				phases[layerIndex] += Mathf.Tau * frequency / SampleRate;
				var wave = layer.Square
					? Mathf.Sign(Mathf.Sin(phases[layerIndex]))
					: Mathf.Sin(phases[layerIndex]);
				sampleValue += wave * layer.Gain;
			}

			sampleValue /= Math.Max(1, layers.Length);
			if (noiseMix > 0f)
			{
				sampleValue = Mathf.Lerp(sampleValue, _rng.RandfRange(-1f, 1f), Mathf.Clamp(noiseMix, 0f, 1f));
			}

			var attack = attackSeconds <= 0f ? 1f : Mathf.Clamp(t / attackSeconds, 0f, 1f);
			var release = releaseSeconds <= 0f
				? 1f
				: Mathf.Clamp((duration - t) / releaseSeconds, 0f, 1f);
			var tremolo = tremoloHz > 0f
				? 0.8f + (Mathf.Sin(t * Mathf.Tau * tremoloHz) * 0.2f)
				: 1f;
			var enveloped = Mathf.Clamp(sampleValue * masterGain * attack * release * tremolo, -1f, 1f);
			var sampleShort = (short)Mathf.RoundToInt(enveloped * short.MaxValue);
			var byteIndex = sampleIndex * sizeof(short);
			pcmData[byteIndex] = (byte)(sampleShort & 0xff);
			pcmData[byteIndex + 1] = (byte)((sampleShort >> 8) & 0xff);
		}

		return new AudioStreamWav
		{
			Data = pcmData,
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = SampleRate,
			Stereo = false
		};
	}

	private float ResolveMixedVolumeDb(float baseVolumeDb, bool isAmbience)
	{
		if (GameState.Instance == null)
		{
			return baseVolumeDb;
		}

		var percent = isAmbience
			? GameState.Instance.AmbienceVolumePercent
			: GameState.Instance.EffectsVolumePercent;
		return baseVolumeDb + ConvertPercentToDbOffset(percent);
	}

	private static float ConvertPercentToDbOffset(int percent)
	{
		var normalized = Mathf.Clamp(percent / 100f, 0f, 1f);
		if (normalized <= 0.001f)
		{
			return -80f;
		}

		return 20f * Mathf.Log(normalized) / Mathf.Log(10f);
	}
}
