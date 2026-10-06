using System.Collections.Generic;
using Godot;

/// <summary>
/// Crossfading score player on the Music bus. Scenes and home overlays choose a track; battles play their zone's
/// track and swap to the boss theme while a grave lord lives. Loops come from assets/music (art/audio/build_music.py).
/// </summary>
public partial class MusicPlayer : Node
{
	public static MusicPlayer Instance { get; private set; }

	/// <summary>The track currently playing (empty while silent or faded out).</summary>
	public string CurrentTrackId => _currentTrackId;

	private const string MusicPath = "res://assets/music/";
	private const float FadeDuration = 1.6f;
	private const float DefaultVolumeDb = -2f;

	private AudioStreamPlayer _playerA;
	private AudioStreamPlayer _playerB;
	private bool _isPlayerA = true;
	private string _currentTrackId = "";
	private string _battleTrackId = "";
	private string _homeTrackId = "";
	private bool _bossMusic;
	private float _fadeTimer;
	private float _fadeDuration = FadeDuration;
	private bool _fading;
	private float _musicVolumeScale = 1f;
	private float _duckDb, _duckTarget, _duckHold;
	private float _masterFade = 1f, _masterFadeTarget = 1f, _masterFadeRate;

	private static readonly HashSet<string> MissingTracks = new();

	private static readonly Dictionary<string, string> SceneTrackMap = new()
	{
		[SceneRouter.MainMenuScene] = "title",
		[SceneRouter.MapScene] = "campaign",
		[SceneRouter.ShopScene] = "shop",
		[SceneRouter.LoadoutScene] = "loadout",
		[SceneRouter.EndlessScene] = "endless_prep",
		[SceneRouter.MultiplayerScene] = "multiplayer",
		[SceneRouter.LanRaceScene] = "multiplayer",
		[SceneRouter.ArenaScene] = "multiplayer",
		[SceneRouter.TowerScene] = "loadout",
		[SceneRouter.RaidScene] = "loadout",
		[SceneRouter.ForgeScene] = "shop",
		[SceneRouter.SettingsScene] = "",
		[SceneRouter.CashShopScene] = "shop",
	};

	private static readonly Dictionary<string, string> RouteTrackMap = new()
	{
		["city"] = "battle_road",
		["harbor"] = "battle_harbor",
		["foundry"] = "battle_foundry",
		["quarantine"] = "battle_quarantine",
		["thornwall"] = "battle_pass",
		["basilica"] = "battle_basilica",
		["mire"] = "battle_mire",
		["steppe"] = "battle_steppe",
		["gloamwood"] = "battle_gloamwood",
		["citadel"] = "battle_citadel",
	};

	public override void _EnterTree()
	{
		Instance = this;
		// The score keeps playing under the pause menu.
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		_playerA = new AudioStreamPlayer { Bus = AudioDirector.MusicBus, VolumeDb = DefaultVolumeDb };
		_playerB = new AudioStreamPlayer { Bus = AudioDirector.MusicBus, VolumeDb = -80f };
		AddChild(_playerA);
		AddChild(_playerB);
		SetVolumeScale((GameState.Instance?.MusicVolumePercent ?? 50) / 100f);
		PlayForScene(SceneRouter.MainMenuScene);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		// Ducking under stingers: drop quickly, hold, recover gently.
		if (_duckHold > 0f) _duckHold -= dt;
		else _duckTarget = 0f;
		_duckDb = Mathf.MoveToward(_duckDb, _duckTarget, (_duckTarget < _duckDb ? 30f : 6f) * dt);
		_masterFade = Mathf.MoveToward(_masterFade, _masterFadeTarget, _masterFadeRate * dt);

		var activePlayer = _isPlayerA ? _playerA : _playerB;
		var fadingPlayer = _isPlayerA ? _playerB : _playerA;
		var t = 1f;
		if (_fading)
		{
			_fadeTimer += dt;
			t = Mathf.Clamp(_fadeTimer / _fadeDuration, 0f, 1f);
			fadingPlayer.VolumeDb = t >= .999f ? -80f : ResolveVolumeDb(1f - t);
			if (t >= 1f)
			{
				_fading = false;
				fadingPlayer.Stop();
			}
		}
		if (activePlayer.Playing) activePlayer.VolumeDb = ResolveVolumeDb(t);
		if (_masterFadeTarget <= 0f && _masterFade <= 0.001f && activePlayer.Playing) activePlayer.Stop();
	}

	public void PlayForScene(string scenePath, string routeId = "")
	{
		if (GameState.Instance != null && GameState.Instance.AudioMuted)
		{
			StopAll();
			return;
		}

		if (scenePath == SceneRouter.BattleScene)
		{
			// The battle picks its own zone track; a bare battle-scene request keeps whatever battle music is on.
			if (!string.IsNullOrWhiteSpace(routeId)) PlayBattle(routeId);
			else if (!string.IsNullOrEmpty(_battleTrackId) && _currentTrackId.Length == 0) PlayTrack(_bossMusic ? BossTrack(false) : _battleTrackId);
			return;
		}

		_battleTrackId = "";
		_bossMusic = false;
		var trackId = SceneTrackMap.TryGetValue(scenePath, out var sceneTrack) ? sceneTrack : "title";
		if (string.IsNullOrWhiteSpace(trackId)) return;
		if (scenePath == SceneRouter.MainMenuScene || scenePath == SceneRouter.MapScene) _homeTrackId = trackId;
		PlayTrack(trackId);
	}

	/// <summary>Zone battle music (the battle calls this once it knows its route).</summary>
	public void PlayBattle(string routeId)
	{
		var route = RouteCatalog.Normalize(routeId ?? "");
		_battleTrackId = RouteTrackMap.TryGetValue(route, out var track) ? track : "battle";
		_bossMusic = false;
		if (GameState.Instance != null && GameState.Instance.AudioMuted) return;
		PlayTrack(_battleTrackId);
	}

	/// <summary>Swap to the boss theme while a grave lord lives (the final theme for the citadel's sovereigns).</summary>
	public void SetBossMusic(bool active, bool final = false)
	{
		if (string.IsNullOrEmpty(_battleTrackId) || active == _bossMusic) return;
		_bossMusic = active;
		if (GameState.Instance != null && GameState.Instance.AudioMuted) return;
		PlayTrack(active ? BossTrack(final) : _battleTrackId, active ? 0.8f : 2.4f);
	}

	private static string BossTrack(bool final) => final && TryLoadTrack("battle_boss_final") != null ? "battle_boss_final" : "battle_boss";

	/// <summary>A home destination opened over the map (shop, loadout, endless...) brings its own music.</summary>
	public void PlayOverlay(string destinationPath)
	{
		if (GameState.Instance != null && GameState.Instance.AudioMuted) return;
		if (!SceneTrackMap.TryGetValue(destinationPath ?? "", out var track) || string.IsNullOrEmpty(track)) return;
		PlayTrack(track);
	}

	/// <summary>Back to the home track when the overlay closes.</summary>
	public void EndOverlay()
	{
		if (GameState.Instance != null && GameState.Instance.AudioMuted) return;
		var home = string.IsNullOrEmpty(_homeTrackId) ? "campaign" : _homeTrackId;
		PlayTrack(home);
	}

	/// <summary>Briefly lower the music under a stinger (db negative), recovering after `seconds`.</summary>
	public void Duck(float db, float seconds)
	{
		_duckTarget = Mathf.Min(_duckTarget, db);
		_duckHold = Mathf.Max(_duckHold, seconds);
	}

	/// <summary>Fade the score out (results screens); the next track request fades it back in.</summary>
	public void FadeOut(float seconds)
	{
		_masterFadeTarget = 0f;
		_masterFadeRate = 1f / Mathf.Max(0.05f, seconds);
		_currentTrackId = "";
	}

	public void StopAll()
	{
		_currentTrackId = "";
		_playerA.Stop();
		_playerB.Stop();
		_fading = false;
	}

	public void SetVolumeScale(float scale)
	{
		_musicVolumeScale = Mathf.Clamp(scale, 0f, 1f);
		var bus = AudioServer.GetBusIndex(AudioDirector.MusicBus);
		if (bus >= 0) AudioServer.SetBusVolumeDb(bus, _musicVolumeScale <= 0.01f ? -80f : Mathf.LinearToDb(_musicVolumeScale));
	}

	private void PlayTrack(string trackId, float fadeSeconds = FadeDuration)
	{
		if (trackId == _currentTrackId && (_playerA.Playing || _playerB.Playing) && _masterFadeTarget > 0f) return;
		var stream = TryLoadTrack(trackId) ?? TryLoadTrack(trackId.StartsWith("battle") ? "battle" : "campaign");
		if (stream == null) return;
		_currentTrackId = trackId;
		_masterFadeTarget = 1f;
		_masterFade = Mathf.Max(_masterFade, 0.001f);
		_masterFadeRate = 1f / FadeDuration;
		CrossfadeTo(stream, fadeSeconds);
	}

	private void CrossfadeTo(AudioStream stream, float seconds)
	{
		if (_playerA == null || _playerB == null) return;
		_isPlayerA = !_isPlayerA;
		var incoming = _isPlayerA ? _playerA : _playerB;
		incoming.Stream = stream;
		incoming.VolumeDb = -80f;
		incoming.Play();
		_fadeTimer = 0f;
		_fadeDuration = Mathf.Max(0.1f, seconds);
		_fading = true;
	}

	private float ResolveVolumeDb(float share)
	{
		if (share <= 0.001f || _masterFade <= 0.001f) return -80f;
		return DefaultVolumeDb + _duckDb + Mathf.LinearToDb(share) + Mathf.LinearToDb(_masterFade);
	}

	private static AudioStream TryLoadTrack(string trackId)
	{
		if (MissingTracks.Contains(trackId))
			return null;

		// Try OGG first (preferred for music), then MP3, then WAV
		string[] extensions = { ".ogg", ".mp3", ".wav" };
		foreach (var ext in extensions)
		{
			var path = $"{MusicPath}{trackId}{ext}";
			if (ResourceLoader.Exists(path))
			{
				var stream = ResourceLoader.Load<AudioStream>(path);
				if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
				if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				if (stream != null) return stream;
			}
		}

		MissingTracks.Add(trackId);
		return null;
	}
}
