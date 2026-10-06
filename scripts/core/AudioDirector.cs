using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Plays every sound effect and the ambience. Cues come from assets/sfx/sfx.json (rendered by
/// art/audio/build_sfx.py): each cue has round-robin variants, a mix bus, a cooldown, a voice limit and pitch drift.
/// Battle sounds are positional on the battlefield; ambience is a looping bed per place plus scattered details.
/// </summary>
public partial class AudioDirector : Node
{
	public const string MusicBus = "Music";
	public const string EffectsBus = "Effects";
	public const string InterfaceBus = "Interface";
	public const string AmbienceBus = "Ambience";
	private const string ManifestPath = "res://assets/sfx/sfx.json";
	private const string AmbiencePath = "res://assets/sfx/ambience.json";
	private const int MaxVoices = 32;
	private const float BedFadeSeconds = 2.5f;
	private const float HordeMinGap = 2.4f, HordeMaxGap = 5.5f;

	private sealed class CueSet
	{
		public string Id = "";
		public AudioStream[] Streams = Array.Empty<AudioStream>();
		public StringName Bus = EffectsBus;
		public bool Positional, Loop;
		public float Cooldown, Pitch, VolumeDb;
		public int Voices = 4, Playing, Last = -1;
	}

	private sealed class AmbienceProfile
	{
		public string Bed = "";
		public string[] Details = Array.Empty<string>();
		public Vector2 Gap = new(8, 14);
	}

	public static AudioDirector Instance { get; private set; }

	/// <summary>Raised whenever a cue actually plays (review drivers count these).</summary>
	public event Action<string> CuePlayed;

	/// <summary>The ambience bed currently looping and the place it belongs to.</summary>
	public string AmbienceBed => _bedCue;
	public string AmbienceContext => _ambienceContext;

	private readonly Dictionary<string, CueSet> _cues = new(StringComparer.Ordinal);
	private readonly Dictionary<string, double> _lastPlayed = new(StringComparer.Ordinal);
	private readonly Dictionary<string, AmbienceProfile> _ambience = new(StringComparer.Ordinal);
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Control> _touchControls = new();
	private int _playing;
	private Node2D _world;
	private string _battleRoute = "";
	private AudioStreamPlayer _bedA, _bedB, _din;
	private bool _bedOnA = true;
	private string _bedCue = "", _ambienceContext = "", _currentScenePath = "";
	private Timer _detailTimer;
	private Tween _bedTween;
	private float _battlePressure;
	private double _nextHordeVoice;

	public override void _EnterTree()
	{
		Instance = this;
		// Menus and the pause screen still click while the tree is paused, and the ambience keeps breathing.
		ProcessMode = ProcessModeEnum.Always;
		EnsureBuses();
	}

	public override void _ExitTree()
	{
		if (IsInsideTree()) GetTree().NodeAdded -= OnTreeNodeAdded;
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		_rng.Randomize();
		LoadManifest();
		LoadAmbienceProfiles();
		_bedA = MakeLoopPlayer("BedA");
		_bedB = MakeLoopPlayer("BedB");
		_din = MakeLoopPlayer("BattleDin");
		_detailTimer = new Timer { OneShot = true, ProcessCallback = Timer.TimerProcessCallback.Idle };
		_detailTimer.Timeout += OnDetailTimer;
		AddChild(_detailTimer);
		GetTree().NodeAdded += OnTreeNodeAdded;
		BindControlsRecursive(GetTree().Root);
		RefreshMixFromState();
	}

	public override void _Process(double delta)
	{
		// Touch UI uses visible labels and accessible names instead of hover tooltips.
		for (var i = _touchControls.Count - 1; i >= 0; i--)
		{
			var control = _touchControls[i];
			if (!IsInstanceValid(control)) { _touchControls.RemoveAt(i); continue; }
			if (control.TooltipText.Length == 0) continue;
			if (control.AccessibilityName.Length == 0) control.AccessibilityName = control.TooltipText;
			control.TooltipText = "";
		}
		UpdateSceneContext();
	}

	// ------------------------------------------------------------------ core playback

	/// <summary>Plays a cue (non-positional). Returns false when it was throttled, missing or muted.</summary>
	public bool Play(string cueId, float volumeDb = 0f, float pitch = 1f) => PlayInternal(cueId, volumeDb, pitch, null);

	/// <summary>Plays a cue at a battlefield position (panned and attenuated by the battle camera).</summary>
	public bool PlayAt(string cueId, Vector2 worldPosition, float volumeDb = 0f, float pitch = 1f) =>
		PlayInternal(cueId, volumeDb, pitch, worldPosition);

	public bool HasCue(string cueId) => _cues.TryGetValue(cueId ?? "", out var cue) && cue.Streams.Length > 0;

	public int CueCount => _cues.Count(entry => entry.Value.Streams.Length > 0);

	private bool PlayInternal(string cueId, float volumeDb, float pitch, Vector2? position)
	{
		if (string.IsNullOrEmpty(cueId) || !_cues.TryGetValue(cueId, out var cue) || cue.Streams.Length == 0) return false;
		if (GameState.Instance != null && GameState.Instance.AudioMuted) return false;
		var now = Time.GetTicksMsec() / 1000.0;
		if (cue.Cooldown > 0f && _lastPlayed.TryGetValue(cueId, out var last) && now - last < cue.Cooldown) return false;
		if (cue.Playing >= cue.Voices) return false;
		// Interface and big moments always get through; battle chatter yields when the mix is crowded.
		if (_playing >= MaxVoices && cue.Bus == EffectsBus && cue.Positional) return false;
		_lastPlayed[cueId] = now;

		var index = cue.Streams.Length == 1 ? 0 : _rng.RandiRange(0, cue.Streams.Length - 1);
		if (index == cue.Last && cue.Streams.Length > 1) index = (index + 1) % cue.Streams.Length;
		cue.Last = index;
		var pitchScale = Mathf.Max(0.5f, pitch * (1f + _rng.RandfRange(-cue.Pitch, cue.Pitch)));

		Node player;
		if (position is { } at && cue.Positional && IsInstanceValid(_world) && _world.IsInsideTree())
		{
			var spatial = new AudioStreamPlayer2D
			{
				Stream = cue.Streams[index], VolumeDb = cue.VolumeDb + volumeDb, PitchScale = pitchScale, Bus = cue.Bus,
				MaxDistance = 2600f, Attenuation = 0.85f, PanningStrength = 1.4f,
			};
			_world.AddChild(spatial);
			spatial.GlobalPosition = at;
			spatial.Play();
			player = spatial;
		}
		else
		{
			var flat = new AudioStreamPlayer
			{
				Stream = cue.Streams[index], VolumeDb = cue.VolumeDb + volumeDb, PitchScale = pitchScale, Bus = cue.Bus,
			};
			AddChild(flat);
			flat.Play();
			player = flat;
		}
		cue.Playing++;
		_playing++;
		CuePlayed?.Invoke(cueId);
		// Release the voice once - when it finishes, or when its scene is torn down first.
		var released = false;
		void Release()
		{
			if (released) return;
			released = true;
			cue.Playing = Math.Max(0, cue.Playing - 1);
			_playing = Math.Max(0, _playing - 1);
		}
		player.TreeExiting += Release;
		player.Connect(AudioStreamPlayer.SignalName.Finished, Callable.From(() => { Release(); player.QueueFree(); }));
		return true;
	}

	// ------------------------------------------------------------------ interface

	public void PlayUiHover() => Play("ui_hover");
	public void PlayUiConfirm() => Play("ui_confirm");
	public void PlayUiTap() => Play("ui_tap");
	public void PlayUiBack() => Play("ui_back");
	public void PlayUiError() => Play("ui_error");
	public void PlaySceneChange() => Play("scene_change");
	public void PlayModalOpen() => Play("modal_open");
	public void PlayModalClose() => Play("modal_close", -2f);
	public void PlayPurchase() => Play("purchase");
	public void PlayCoin() => Play("coin");
	public void PlayRewardClaim() => Play("reward_claim");
	public void PlayUpgradeConfirm() => Play("upgrade_confirm");
	public void PlayLevelUp() => Play("level_up");
	public void PlayMapTravel() => Play("map_travel");
	public void PlayMapSelect() => Play("map_select");
	public void PlayCardPickup() => Play("card_pickup");
	public void PlayCardDrop() => Play("card_drop");
	public void PlayCardCancel() => Play("card_cancel");
	public void PlaySpellArm() => Play("spell_arm");
	public void PlaySpellDenied() => Play("spell_denied");

	/// <summary>Reward claim result: a satisfying bag-and-sparkle, or a dull knock when refused. Returns ok.</summary>
	public static bool Claimed(bool ok)
	{
		if (ok) Instance?.PlayRewardClaim();
		else Instance?.PlayUiError();
		return ok;
	}

	/// <summary>Purchase result: coins into the purse, or a dull knock when refused. Returns ok.</summary>
	public static bool Purchased(bool ok)
	{
		if (ok) Instance?.PlayPurchase();
		else Instance?.PlayUiError();
		return ok;
	}

	public void PlayAchievementUnlock()
	{
		if (Play("achievement_unlock")) MusicPlayer.Instance?.Duck(-6f, 1.6f);
	}

	public void PlayRelicPickup() => Play("relic_pickup");

	/// <summary>Results stars, one after another.</summary>
	public void PlayStars(int stars)
	{
		for (var i = 1; i <= Mathf.Clamp(stars, 0, 3); i++)
		{
			var id = $"star_{i}";
			GetTree().CreateTimer(0.35 + 0.32 * i, true).Timeout += () => Play(id);
		}
	}

	// ------------------------------------------------------------------ battle

	/// <summary>Called by the battle when it starts: positional sounds attach to its world, the zone's ambience plays.</summary>
	public void EnterBattle(Node2D world, string routeId)
	{
		_world = world;
		_battleRoute = RouteCatalog.Normalize(routeId ?? "");
		_battlePressure = 0f;
		_nextHordeVoice = Time.GetTicksMsec() / 1000.0 + 3.0;
		UpdateSceneContext(true);
	}

	public void ExitBattle(Node2D world)
	{
		if (_world != world) return;
		_world = null;
		_battleRoute = "";
		SetDinLevel(0f, 1.5f);
	}

	public void SetBattlePressure(float pressure)
	{
		_battlePressure = Mathf.Clamp(pressure, 0f, 1f);
		if (IsInstanceValid(_world)) SetDinLevel(_battlePressure, 2f);
	}

	public void PlayBattleStart() => Play("battle_start");

	public void PlayWaveHorn()
	{
		if (Play("wave_horn")) MusicPlayer.Instance?.Duck(-5f, 2.5f);
	}

	/// <summary>The swing's whoosh as an attack begins (profile overrides the sprite's, e.g. a ranged unit's melee).</summary>
	public void PlaySwing(Unit attacker, string profile = null)
	{
		if (attacker == null) return;
		var cue = AudioCatalog.Swing(profile ?? attacker.MotionProfile);
		if (cue.Length > 0) PlayAt(cue, attacker.Position, -2f);
	}

	public void PlayMeleeHit(Unit attacker, Unit target, float damage, string profile = null)
	{
		if (attacker == null || target == null) return;
		var at = target.Position;
		var heavy = damage >= 24f || attacker.Radius >= 20f;
		var cue = target.VisualClass == "shield" ? "hit_shield" : AudioCatalog.Hit(profile ?? attacker.MotionProfile);
		PlayAt(cue, at, heavy ? 1.5f : 0f, heavy ? 0.94f : 1f);
		// Bone rattles under some blows on the undead (not every one, so a melee doesn't turn to clatter).
		if (AudioCatalog.IsSkeletal(target.Team == Team.Enemy, target.VisualClass) && (heavy || _rng.Randf() < 0.5f)) PlayAt("hit_bone", at, -5f);
	}

	public void PlayLaunch(Unit attacker, string styleId)
	{
		if (attacker == null) return;
		PlayAt(AudioCatalog.Launch(styleId), attacker.Position);
	}

	public void PlayProjectileImpact(string styleId, Vector2 at, float damage)
	{
		PlayAt(AudioCatalog.Impact(styleId), at, damage >= 30f ? 1.5f : 0f);
	}

	public void PlayShieldBlock(Vector2 at) => PlayAt("shield_block", at);

	public void PlayDeploy(UnitDefinition definition, Vector2 at)
	{
		if (definition == null) return;
		foreach (var cue in AudioCatalog.Deploy(definition.Id, definition.VisualClass)) PlayAt(cue, at);
	}

	public void PlayDeploy(UnitDefinition definition) => PlayDeploy(definition, IsInstanceValid(_world) ? WorldCentre() : Vector2.Zero);

	public void PlayDeployDenied() => Play("ui_error");

	/// <summary>Enemy arrival: a voice for the unit (sparingly), or the grave lord's roar.</summary>
	public void PlayEnemySpawn(Unit unit)
	{
		if (unit == null) return;
		if (unit.VisualClass == "boss")
		{
			PlayAt(AudioCatalog.BossRoarFor(unit.DefinitionId), unit.Position, 2f);
			return;
		}
		var cue = AudioCatalog.EnemyVoiceFor(unit.DefinitionId);
		if (cue.Length > 0 && _rng.Randf() < 0.45f) PlayAt(cue, unit.Position, -3f);
	}

	/// <summary>Called each battle tick with a living enemy: the host groans and shrieks now and then as it advances.</summary>
	public void TickHordeVoices(Unit sample)
	{
		var now = Time.GetTicksMsec() / 1000.0;
		if (sample == null || now < _nextHordeVoice) return;
		_nextHordeVoice = now + _rng.RandfRange(HordeMinGap, HordeMaxGap) * Mathf.Lerp(1.3f, 0.7f, _battlePressure);
		var cue = sample.VisualClass == "boss" ? "" : AudioCatalog.EnemyVoiceFor(sample.DefinitionId);
		if (cue.Length > 0) PlayAt(cue, sample.Position, -6f);
	}

	public void PlayUnitDeath(Unit unit, DeathStyle style)
	{
		if (unit == null) return;
		var fx = style?.Fx ?? "dust";
		foreach (var cue in AudioCatalog.Death(fx, unit.Team == Team.Player, unit.DefinitionId, unit.VisualClass))
			PlayAt(cue, unit.Position, unit.VisualClass == "boss" ? 2f : 0f, unit.VisualClass == "boss" ? 0.85f : 1f);
	}

	public void PlayBodyFall(Vector2 at, string fx, bool heavy, bool boss) =>
		PlayAt(AudioCatalog.BodyFall(fx, heavy), at, boss ? 3f : 0f, boss ? 0.8f : 1f);

	public void PlayAbility(string abilityId, Vector2 at)
	{
		foreach (var cue in AudioCatalog.Ability(abilityId)) PlayAt(cue, at);
	}

	/// <summary>The host's special moves: rally, jam, raise, burrow, emerge, tower, reflect, split, burst.</summary>
	public void PlayEnemySpecial(string kind, Vector2 at)
	{
		var cue = kind switch
		{
			"rally" => "herald_howl", "jam" => "hex_jam", "raise" => "necro_raise", "burrow" => "dig_burrow",
			"emerge" => "dig_emerge", "tower" => "tower_open", "reflect" => "reflect", "split" => "bone_nest_crack",
			"burst" => "explosion", _ => "",
		};
		PlayAt(cue, at);
		if (kind == "burst") PlayAt("death_gas", at, -2f);
	}

	public void PlayBaseWeapon(string styleId, Vector2 at)
	{
		var cue = styleId switch
		{
			"arrow" => "base_arrows", "ballista_bolt" => "ballista_release", "firepot" => "base_firepot",
			"harpoon" => "harpoon_release", "frost_shard" => "cast_frost", "hex" => "cast_hex", _ => AudioCatalog.Launch(styleId),
		};
		PlayAt(cue, at);
	}

	public void PlayWagonDoor(bool open, Vector2 at) => PlayAt(open ? "wagon_door_open" : "wagon_door_close", at, -2f);

	public void PlayImpact(float damage, string visualClass = "") =>
		Play(damage >= 18f ? "hit_heavy" : "hit_blunt", damage >= 18f ? -3f : -5f);

	public void PlayBaseHit(bool playerBase, float damage)
	{
		var cue = playerBase ? "bus_hit" : "barricade_hit";
		var volume = damage >= 12f ? 0f : -3f;
		if (IsInstanceValid(_world)) PlayAt(cue, BaseAnchor(playerBase), volume);
		else Play(cue, volume);
	}

	public void PlayBusRepair(float amount) => Play("repair", amount >= 12f ? 0f : -3f);

	public void PlayHazardWarning() => Play("hazard_warning");

	public void PlayHazardStrike() => Play("hazard_strike");

	public void PlayCombo() => Play("combo");

	public void PlayBossPhase()
	{
		if (Play("boss_phase")) MusicPlayer.Instance?.Duck(-6f, 2.5f);
	}

	public void PlayVictory()
	{
		Play("victory");
		MusicPlayer.Instance?.FadeOut(1.2f);
	}

	public void PlayDefeat()
	{
		Play("defeat");
		MusicPlayer.Instance?.FadeOut(1.0f);
	}

	public void PlaySpellCast(string effectType, Vector2 at)
	{
		var cue = $"spell_{effectType}";
		if (!HasCue(cue)) cue = "spell_fireball";
		if (PlayAt(cue, at) && effectType is "war_cry" or "earthquake" or "lightning_strike" or "resurrect")
			MusicPlayer.Instance?.Duck(-4f, 1.5f);
	}

	public void PlaySpellCast(string effectType) => PlaySpellCast(effectType, IsInstanceValid(_world) ? WorldCentre() : Vector2.Zero);

	public void PlayBossSpawn()
	{
		if (Play("boss_spawn")) MusicPlayer.Instance?.Duck(-8f, 2.5f);
	}

	public void PlayBossDeath()
	{
		if (Play("boss_death")) MusicPlayer.Instance?.Duck(-8f, 2.5f);
	}

	private Vector2 WorldCentre()
	{
		var camera = _world?.GetViewport()?.GetCamera2D();
		return camera != null ? camera.GetScreenCenterPosition() : Vector2.Zero;
	}

	private Vector2 BaseAnchor(bool playerBase)
	{
		var centre = WorldCentre();
		var half = (_world?.GetViewportRect().Size.X ?? 1280f) * 0.5f;
		return centre + new Vector2(playerBase ? -half : half, 0f);
	}

	// ------------------------------------------------------------------ mix

	public void RefreshMixFromState()
	{
		EnsureBuses();
		var state = GameState.Instance;
		var muted = state?.AudioMuted ?? false;
		AudioServer.SetBusMute(0, muted);
		SetBusVolume(EffectsBus, PercentToDb(state?.EffectsVolumePercent ?? 85));
		SetBusVolume(InterfaceBus, PercentToDb(state?.EffectsVolumePercent ?? 85));
		SetBusVolume(AmbienceBus, PercentToDb(state?.AmbienceVolumePercent ?? 65));
		SetBusVolume(MusicBus, PercentToDb(state?.MusicVolumePercent ?? 50));
		UpdateSceneContext(true);
	}

	private static void EnsureBuses()
	{
		foreach (var name in new[] { MusicBus, EffectsBus, InterfaceBus, AmbienceBus })
		{
			if (AudioServer.GetBusIndex(name) >= 0) continue;
			AudioServer.AddBus();
			var index = AudioServer.BusCount - 1;
			AudioServer.SetBusName(index, name);
			AudioServer.SetBusSend(index, "Master");
		}
		// A gentle safety limiter so a pile-up of hits never clips.
		if (AudioServer.GetBusEffectCount(0) == 0)
			AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = -0.6f, PreGainDb = 0f, Release = 0.1f });
	}

	private static void SetBusVolume(string bus, float db)
	{
		var index = AudioServer.GetBusIndex(bus);
		if (index >= 0) AudioServer.SetBusVolumeDb(index, db);
	}

	public static float PercentToDb(int percent)
	{
		var normalized = Mathf.Clamp(percent / 100f, 0f, 1f);
		return normalized <= 0.001f ? -80f : 20f * Mathf.Log(normalized) / Mathf.Log(10f);
	}

	// ------------------------------------------------------------------ ambience

	private void UpdateSceneContext(bool force = false)
	{
		var scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (!force && scenePath == _currentScenePath) return;
		_currentScenePath = scenePath;
		var route = scenePath == SceneRouter.BattleScene ? _battleRoute : scenePath == SceneRouter.LoadoutScene ? SelectedRoute() : "";
		var context = AudioCatalog.AmbienceContext(scenePath, route);
		if (!_ambience.ContainsKey(context)) context = "home";
		if (scenePath == SceneRouter.SettingsScene && _ambienceContext.Length > 0) context = _ambienceContext;
		if (!force && context == _ambienceContext) return;
		var changed = context != _ambienceContext;
		_ambienceContext = context;
		if (scenePath != SceneRouter.BattleScene) SetDinLevel(0f, 1.5f);
		if (changed || !_bedA.Playing && !_bedB.Playing) StartBed(_ambience.TryGetValue(context, out var p) ? p.Bed : "");
		ScheduleDetail(_rng.RandfRange(2f, 5f));
	}

	private static string SelectedRoute()
	{
		var state = GameState.Instance;
		if (state == null || state.MaxStage <= 0) return "";
		return RouteCatalog.Get(GameData.GetStage(Mathf.Clamp(state.SelectedStage, 1, state.MaxStage)).MapId).Id;
	}

	private void StartBed(string cueId)
	{
		if (cueId == _bedCue && (_bedA.Playing || _bedB.Playing)) return;
		_bedCue = cueId;
		_bedTween?.Kill();
		var outgoing = _bedOnA ? _bedA : _bedB;
		var incoming = _bedOnA ? _bedB : _bedA;
		_bedOnA = !_bedOnA;
		_bedTween = CreateTween().SetParallel(true);
		if (outgoing.Playing) _bedTween.TweenProperty(outgoing, "volume_db", -60f, BedFadeSeconds);
		if (_cues.TryGetValue(cueId, out var cue) && cue.Streams.Length > 0 && !(GameState.Instance?.AudioMuted ?? false))
		{
			incoming.Stream = cue.Streams[0];
			incoming.VolumeDb = -60f;
			incoming.Play(_rng.RandfRange(0f, 30f));
			_bedTween.TweenProperty(incoming, "volume_db", cue.VolumeDb, BedFadeSeconds);
		}
		_bedTween.Chain().TweenCallback(Callable.From(() => { if (outgoing.VolumeDb <= -59f) outgoing.Stop(); }));
	}

	private void SetDinLevel(float pressure, float seconds)
	{
		if (_din == null) return;
		var target = pressure <= 0.02f ? -60f : Mathf.Lerp(-22f, -3f, pressure);
		if (target > -59f && !_din.Playing && _cues.TryGetValue("battle_din", out var din) && din.Streams.Length > 0)
		{
			_din.Stream = din.Streams[0];
			_din.VolumeDb = -60f;
			_din.Play(_rng.RandfRange(0f, 30f));
		}
		var tween = CreateTween();
		tween.TweenProperty(_din, "volume_db", target, seconds);
		if (target <= -59f) tween.TweenCallback(Callable.From(() => _din.Stop()));
	}

	private void ScheduleDetail(float seconds)
	{
		if (_detailTimer == null) return;
		_detailTimer.Start(Mathf.Max(0.5f, seconds));
	}

	private void OnDetailTimer()
	{
		if (!_ambience.TryGetValue(_ambienceContext, out var profile)) return;
		if (profile.Details.Length > 0 && !(GameState.Instance?.AudioMuted ?? false))
		{
			var cueId = profile.Details[_rng.RandiRange(0, profile.Details.Length - 1)];
			PlayDetail(cueId);
		}
		ScheduleDetail(_rng.RandfRange(profile.Gap.X, profile.Gap.Y));
	}

	/// <summary>A detail somewhere in the stereo field: left, right or behind the scene.</summary>
	private void PlayDetail(string cueId)
	{
		if (!_cues.TryGetValue(cueId, out var cue) || cue.Streams.Length == 0) return;
		var size = GetViewport().GetVisibleRect().Size;
		var camera = GetViewport().GetCamera2D();
		var centre = camera != null ? camera.GetScreenCenterPosition() : size * 0.5f;
		var zoom = camera?.Zoom.X ?? 1f;
		var player = new AudioStreamPlayer2D
		{
			Stream = cue.Streams[_rng.RandiRange(0, cue.Streams.Length - 1)], Bus = cue.Bus, VolumeDb = cue.VolumeDb + _rng.RandfRange(-4f, 0f),
			PitchScale = 1f + _rng.RandfRange(-cue.Pitch, cue.Pitch), MaxDistance = 100000f, Attenuation = 0f, PanningStrength = 1.2f,
		};
		var parent = IsInstanceValid(_world) ? (Node)_world : this;
		parent.AddChild(player);
		player.GlobalPosition = centre + new Vector2(_rng.RandfRange(-0.5f, 0.5f) * size.X / zoom, 0f);
		player.Finished += () => player.QueueFree();
		player.Play();
	}

	private AudioStreamPlayer MakeLoopPlayer(string name)
	{
		var player = new AudioStreamPlayer { Name = name, Bus = AmbienceBus, VolumeDb = -60f };
		AddChild(player);
		return player;
	}

	// ------------------------------------------------------------------ data

	private void LoadManifest()
	{
		_cues.Clear();
		var data = ReadJson(ManifestPath);
		if (data.VariantType != Variant.Type.Dictionary) return;
		foreach (var (key, value) in data.AsGodotDictionary())
		{
			var entry = value.AsGodotDictionary();
			var streams = new List<AudioStream>();
			foreach (var file in entry["files"].AsGodotArray())
			{
				var path = file.AsString();
				if (!ResourceLoader.Exists(path)) continue;
				var stream = ResourceLoader.Load<AudioStream>(path);
				if (stream == null) continue;
				var loop = entry.ContainsKey("loop") && entry["loop"].AsBool();
				if (stream is AudioStreamOggVorbis ogg) ogg.Loop = loop;
				streams.Add(stream);
			}
			var id = key.AsString();
			_cues[id] = new CueSet
			{
				Id = id, Streams = streams.ToArray(), Bus = entry["bus"].AsString(), Positional = entry["positional"].AsBool(),
				Cooldown = (float)entry["cooldown"].AsDouble(), Voices = Math.Max(1, entry["voices"].AsInt32()),
				Pitch = (float)entry["pitch"].AsDouble(), VolumeDb = (float)entry["volume_db"].AsDouble(),
				Loop = entry.ContainsKey("loop") && entry["loop"].AsBool(),
			};
		}
	}

	private void LoadAmbienceProfiles()
	{
		_ambience.Clear();
		var data = ReadJson(AmbiencePath);
		if (data.VariantType != Variant.Type.Dictionary) return;
		foreach (var (key, value) in data.AsGodotDictionary())
		{
			var entry = value.AsGodotDictionary();
			var gap = entry["gap"].AsGodotArray();
			_ambience[key.AsString()] = new AmbienceProfile
			{
				Bed = entry["bed"].AsString(),
				Details = entry["details"].AsGodotArray().Select(v => v.AsString()).ToArray(),
				Gap = new Vector2((float)gap[0].AsDouble(), (float)gap[1].AsDouble()),
			};
		}
	}

	private static Variant ReadJson(string path)
	{
		if (!FileAccess.FileExists(path)) return new Variant();
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return file == null ? new Variant() : Json.ParseString(file.GetAsText());
	}

	// ------------------------------------------------------------------ automatic control sounds

	private void OnTreeNodeAdded(Node node) => BindControl(node);

	private void BindControlsRecursive(Node node)
	{
		BindControl(node);
		foreach (Node child in node.GetChildren()) BindControlsRecursive(child);
	}

	private void BindControl(Node node)
	{
		if (node is Control control && !_touchControls.Contains(control)) _touchControls.Add(control);
		const string boundMeta = "audio_bound";
		if (node is not Control target || target.HasMeta(boundMeta)) return;
		switch (node)
		{
			case OptionButton option:
				target.SetMeta(boundMeta, true);
				option.ItemSelected += _ => PlayUiTap();
				break;
			case BaseButton button:
				target.SetMeta(boundMeta, true);
				button.Pressed += () => PlayButtonCue(button);
				break;
			case Godot.Range range when range is Slider:
				target.SetMeta(boundMeta, true);
				range.ValueChanged += _ => { if (range.HasFocus() || Input.IsMouseButtonPressed(MouseButton.Left)) Play("ui_slider"); };
				break;
		}
	}

	/// <summary>Each button sounds like what it does: tabs turn a page, toggles latch, back steps away, primaries ring.</summary>
	private void PlayButtonCue(BaseButton button)
	{
		if (button.HasMeta("audio_silent")) return;
		if (button.HasMeta("audio_cue")) { Play(button.GetMeta("audio_cue").AsString()); return; }
		var typeName = button.GetType().Name;
		if (typeName.StartsWith("Adventure", StringComparison.Ordinal)) { PlayMapSelect(); return; }
		if (button.ButtonGroup != null || button.GetParent()?.HasMeta("realm_tabs") == true) { Play("ui_tab"); return; }
		if (button.ToggleMode) { Play(button.ButtonPressed ? "ui_toggle_on" : "ui_toggle_off"); return; }
		if (IsBackButton(button)) { PlayUiBack(); return; }
		if (button.HasMeta("realm_primary")) { PlayUiConfirm(); return; }
		PlayUiTap();
	}

	private static bool IsBackButton(BaseButton button)
	{
		var icon = (button as Button)?.Icon?.ResourcePath ?? "";
		if (icon.EndsWith("/back.svg", StringComparison.Ordinal) || icon.EndsWith("/close.svg", StringComparison.Ordinal)) return true;
		var text = ((button as Button)?.Text ?? "").Trim();
		var name = button.AccessibilityName.Length > 0 ? button.AccessibilityName : button.TooltipText;
		return text.StartsWith("Back", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Close", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Back", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Close", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Return", StringComparison.OrdinalIgnoreCase);
	}
}
