using System.Collections.Generic;
using Godot;

public partial class MusicPlayer : Node
{
	public static MusicPlayer Instance { get; private set; }

	private const string MusicPath = "res://assets/music/";
	private const float FadeDuration = 1.2f;
	private const float DefaultVolumeDb = -12f;

	private AudioStreamPlayer _playerA;
	private AudioStreamPlayer _playerB;
	private bool _isPlayerA = true;
	private string _currentTrackId = "";
	private float _fadeTimer;
	private bool _fading;
	private float _musicVolumeScale = 1f;

	private static readonly HashSet<string> MissingTracks = new();

	private static readonly Dictionary<string, string> SceneTrackMap = new()
	{
		[SceneRouter.MainMenuScene] = "title",
		[SceneRouter.MapScene] = "campaign",
		[SceneRouter.ShopScene] = "shop",
		[SceneRouter.LoadoutScene] = "loadout",
		[SceneRouter.EndlessScene] = "endless_prep",
		[SceneRouter.MultiplayerScene] = "multiplayer",
		[SceneRouter.SettingsScene] = "",
		[SceneRouter.CashShopScene] = "shop",
	};

	private static readonly Dictionary<string, string> RouteTrackMap = new()
	{
		["city"] = "battle_road",
		["harbor"] = "battle_harbor",
		["foundry"] = "battle_foundry",
		["quarantine"] = "battle_quarantine",
		["pass"] = "battle_pass",
		["basilica"] = "battle_basilica",
		["mire"] = "battle_mire",
		["steppe"] = "battle_steppe",
		["gloamwood"] = "battle_gloamwood",
		["citadel"] = "battle_citadel",
	};

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		_playerA = new AudioStreamPlayer { Bus = "Master", VolumeDb = DefaultVolumeDb };
		_playerB = new AudioStreamPlayer { Bus = "Master", VolumeDb = -80f };
		AddChild(_playerA);
		AddChild(_playerB);
	}

	public override void _Process(double delta)
	{
		if (!_fading) return;

		_fadeTimer += (float)delta;
		var t = Mathf.Clamp(_fadeTimer / FadeDuration, 0f, 1f);

		var activePlayer = _isPlayerA ? _playerA : _playerB;
		var fadingPlayer = _isPlayerA ? _playerB : _playerA;

		activePlayer.VolumeDb = Mathf.Lerp(-80f, ResolveVolumeDb(), t);
		fadingPlayer.VolumeDb = Mathf.Lerp(ResolveVolumeDb(), -80f, t);

		if (t >= 1f)
		{
			_fading = false;
			fadingPlayer.Stop();
		}
	}

	public void PlayForScene(string scenePath, string routeId = "")
	{
		if (GameState.Instance != null && GameState.Instance.AudioMuted)
		{
			StopAll();
			return;
		}

		var trackId = ResolveTrackId(scenePath, routeId);
		if (string.IsNullOrWhiteSpace(trackId))
		{
			return;
		}

		if (trackId == _currentTrackId)
		{
			return;
		}

		var stream = TryLoadTrack(trackId);
		if (stream == null)
		{
			return;
		}

		_currentTrackId = trackId;
		CrossfadeTo(stream);
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
		if (!_fading)
		{
			var active = _isPlayerA ? _playerA : _playerB;
			if (active.Playing)
			{
				active.VolumeDb = ResolveVolumeDb();
			}
		}
	}

	private void CrossfadeTo(AudioStream stream)
	{
		_isPlayerA = !_isPlayerA;
		var incoming = _isPlayerA ? _playerA : _playerB;
		incoming.Stream = stream;
		incoming.VolumeDb = -80f;
		incoming.Play();

		_fadeTimer = 0f;
		_fading = true;
	}

	private float ResolveVolumeDb()
	{
		if (_musicVolumeScale <= 0.01f) return -80f;
		return DefaultVolumeDb + Mathf.LinearToDb(_musicVolumeScale);
	}

	private static string ResolveTrackId(string scenePath, string routeId)
	{
		if (scenePath == SceneRouter.BattleScene)
		{
			if (!string.IsNullOrWhiteSpace(routeId) && RouteTrackMap.TryGetValue(routeId.ToLowerInvariant(), out var routeTrack))
			{
				return routeTrack;
			}

			return "battle";
		}

		if (SceneTrackMap.TryGetValue(scenePath, out var sceneTrack))
		{
			return sceneTrack;
		}

		return "title";
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
				if (stream != null) return stream;
			}
		}

		MissingTracks.Add(trackId);
		return null;
	}
}
