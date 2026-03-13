using System;
using Godot;

public partial class LanSmokeDirector : Node
{
	private const string RoleArgPrefix = "--lan-smoke-role=";
	private const string AddressArgPrefix = "--lan-smoke-address=";
	private const string TimeoutArgPrefix = "--lan-smoke-timeout=";
	private const double DefaultTimeoutSeconds = 45d;
	private const double HostSubmitDelaySeconds = 1.0d;
	private const double ClientSubmitDelaySeconds = 1.4d;
	private const string HostCallsign = "SmokeHost";
	private const string ClientCallsign = "SmokeClient";

	private enum SmokeRole
	{
		None,
		Host,
		Client
	}

	private enum SmokeState
	{
		Disabled,
		WaitForServices,
		HostRoom,
		JoinRoom,
		WaitForBoardSync,
		ArmReady,
		WaitForLaunch,
		WaitForCountdown,
		SubmitResult,
		WaitForScoreboard,
		Passed,
		Failed
	}

	private SmokeRole _role = SmokeRole.None;
	private SmokeState _state = SmokeState.Disabled;
	private string _address = "127.0.0.1";
	private double _timeoutSeconds = DefaultTimeoutSeconds;
	private double _elapsedSeconds;
	private double _stateElapsedSeconds;
	private bool _hostAttempted;
	private bool _joinAttempted;
	private bool _launchAttempted;
	private bool _submissionSent;

	public override void _Ready()
	{
		ParseArguments();
		if (_role == SmokeRole.None)
		{
			return;
		}

		ProcessMode = ProcessModeEnum.Always;
		TransitionTo(SmokeState.WaitForServices, "boot");
		Log($"boot args ready  |  timeout {_timeoutSeconds:0.#}s  |  save {SaveSystem.Instance?.ActiveSaveFilePath ?? "pending"}");
	}

	public override void _Process(double delta)
	{
		if (_state == SmokeState.Disabled || _state == SmokeState.Passed || _state == SmokeState.Failed)
		{
			return;
		}

		_elapsedSeconds += delta;
		_stateElapsedSeconds += delta;
		if (_elapsedSeconds >= _timeoutSeconds)
		{
			Fail($"timeout after {_timeoutSeconds:0.#}s in state {_state}");
			return;
		}

		switch (_state)
		{
			case SmokeState.WaitForServices:
				ProcessWaitForServices();
				break;
			case SmokeState.HostRoom:
				ProcessHostRoom();
				break;
			case SmokeState.JoinRoom:
				ProcessJoinRoom();
				break;
			case SmokeState.WaitForBoardSync:
				ProcessWaitForBoardSync();
				break;
			case SmokeState.ArmReady:
				ProcessArmReady();
				break;
			case SmokeState.WaitForLaunch:
				ProcessWaitForLaunch();
				break;
			case SmokeState.WaitForCountdown:
				ProcessWaitForCountdown();
				break;
			case SmokeState.SubmitResult:
				ProcessSubmitResult();
				break;
			case SmokeState.WaitForScoreboard:
				ProcessWaitForScoreboard();
				break;
		}
	}

	private void ProcessWaitForServices()
	{
		if (GameState.Instance == null || LanChallengeService.Instance == null || SceneRouter.Instance == null || SaveSystem.Instance == null)
		{
			return;
		}

		GameState.Instance.SetPlayerCallsign(_role == SmokeRole.Host ? HostCallsign : ClientCallsign);
		if (_role == SmokeRole.Host)
		{
			var smokeCode = AsyncChallengeCatalog.Create(1, AsyncChallengeCatalog.PressureSpikeId, 4242).Code;
			if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(smokeCode, out var message))
			{
				Fail($"could not arm smoke board: {message}");
				return;
			}

			Log($"services ready  |  armed {smokeCode}");
			TransitionTo(SmokeState.HostRoom, "host current board");
			return;
		}

		Log($"services ready  |  joining {_address}");
		TransitionTo(SmokeState.JoinRoom, "join host");
	}

	private void ProcessHostRoom()
	{
		if (_hostAttempted)
		{
			return;
		}

		_hostAttempted = true;
		if (!LanChallengeService.Instance.HostSelectedBoard(out var message))
		{
			Fail($"host failed: {message}");
			return;
		}

		Log($"hosted board  |  {message}");
		TransitionTo(SmokeState.WaitForBoardSync, "wait for client");
	}

	private void ProcessJoinRoom()
	{
		if (_joinAttempted)
		{
			return;
		}

		_joinAttempted = true;
		if (!LanChallengeService.Instance.JoinRoom(_address, out var message))
		{
			Fail($"join failed: {message}");
			return;
		}

		Log($"join requested  |  {message}");
		TransitionTo(SmokeState.WaitForBoardSync, "wait for board");
	}

	private void ProcessWaitForBoardSync()
	{
		var service = LanChallengeService.Instance;
		if (service == null || !service.HasRoom || string.IsNullOrWhiteSpace(service.SharedChallengeCode))
		{
			return;
		}

		if (_role == SmokeRole.Host && Multiplayer.GetPeers().Length < 1)
		{
			return;
		}

		Log(
			$"board synced  |  code {service.SharedChallengeCode}  |  peers {Multiplayer.GetPeers().Length + 1}  |  scene {GetTree().CurrentScene?.SceneFilePath ?? "<none>"}");
		TransitionTo(SmokeState.ArmReady, "ready local runner");
	}

	private void ProcessArmReady()
	{
		var service = LanChallengeService.Instance;
		if (service == null)
		{
			return;
		}

		if (service.LocalReady)
		{
			Log("local runner armed");
			TransitionTo(SmokeState.WaitForLaunch, "wait for launch");
			return;
		}

		if (!service.ToggleLocalReady(out var message))
		{
			Fail($"ready failed: {message}");
			return;
		}

		Log($"ready toggled  |  {message}");
	}

	private void ProcessWaitForLaunch()
	{
		var service = LanChallengeService.Instance;
		if (service == null)
		{
			return;
		}

		if (_role == SmokeRole.Host && !_launchAttempted)
		{
			if (Multiplayer.GetPeers().Length < 1 || !service.AllPeersReady || !service.AllPeersDecksReady)
			{
				return;
			}

			_launchAttempted = true;
			if (!service.LaunchRace(out var message))
			{
				Fail($"launch failed: {message}");
				return;
			}

			Log($"launch sent  |  {message}");
		}

		if (!service.RoundLocked && GameState.Instance.CurrentBattleMode != BattleRunMode.AsyncChallenge)
		{
			return;
		}

		Log($"round locked  |  mode {GameState.Instance.CurrentBattleMode}  |  scene {GetTree().CurrentScene?.SceneFilePath ?? "<none>"}");
		TransitionTo(SmokeState.WaitForCountdown, "wait for race release");
	}

	private void ProcessWaitForCountdown()
	{
		var service = LanChallengeService.Instance;
		if (service == null || !service.RaceCombatReleased || GameState.Instance.CurrentBattleMode != BattleRunMode.AsyncChallenge)
		{
			return;
		}

		Log($"combat released  |  scene {GetTree().CurrentScene?.SceneFilePath ?? "<none>"}");
		TransitionTo(SmokeState.SubmitResult, "submit smoke result");
	}

	private void ProcessSubmitResult()
	{
		if (_submissionSent)
		{
			return;
		}

		var requiredDelay = _role == SmokeRole.Host ? HostSubmitDelaySeconds : ClientSubmitDelaySeconds;
		if (_stateElapsedSeconds < requiredDelay)
		{
			return;
		}

		var service = LanChallengeService.Instance;
		if (service == null)
		{
			return;
		}

		var challenge = GameState.Instance.GetSelectedAsyncChallenge();
		var breakdown = BuildSmokeBreakdown();
		var elapsedSeconds = _role == SmokeRole.Host ? 18.4f : 21.7f;
		var enemyDefeats = _role == SmokeRole.Host ? 18 : 14;
		var hullRatio = _role == SmokeRole.Host ? 0.78f : 0.64f;
		service.UpdateLocalRaceTelemetry(elapsedSeconds, enemyDefeats, hullRatio);
		service.SubmitChallengeResult(
			challenge,
			breakdown,
			elapsedSeconds,
			3,
			enemyDefeats,
			hullRatio,
			true,
			false,
			GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
		_submissionSent = true;
		Log($"submitted result  |  score {breakdown.FinalScore}  |  defeats {enemyDefeats}  |  hull {hullRatio * 100f:0}%");
		TransitionTo(SmokeState.WaitForScoreboard, "wait for room scoreboard");
	}

	private void ProcessWaitForScoreboard()
	{
		var service = LanChallengeService.Instance;
		if (service == null)
		{
			return;
		}

		var scoreboardReady =
			service.ScoreboardSummary.Contains(HostCallsign, StringComparison.OrdinalIgnoreCase) &&
			service.ScoreboardSummary.Contains(ClientCallsign, StringComparison.OrdinalIgnoreCase);
		var standingsReady =
			service.SessionStandingsSummary.Contains(HostCallsign, StringComparison.OrdinalIgnoreCase) &&
			service.SessionStandingsSummary.Contains(ClientCallsign, StringComparison.OrdinalIgnoreCase);
		if (!scoreboardReady || !standingsReady)
		{
			return;
		}

		Log("LAN_SMOKE PASS  |  scoreboard and standings synced");
		_state = SmokeState.Passed;
		GetTree().Quit(0);
	}

	private AsyncChallengeScoreBreakdown BuildSmokeBreakdown()
	{
		var isHost = _role == SmokeRole.Host;
		var completionBonus = isHost ? 1100 : 950;
		var starBonus = 300;
		var killBonus = isHost ? 240 : 180;
		var hullBonus = isHost ? 190 : 140;
		var timeBonus = isHost ? 180 : 120;
		var deployPenalty = 0;
		var rawScore = completionBonus + starBonus + killBonus + hullBonus + timeBonus - deployPenalty;
		var multiplier = isHost ? 1.18f : 1.11f;
		var finalScore = Mathf.RoundToInt(rawScore * multiplier);
		return new AsyncChallengeScoreBreakdown(
			completionBonus,
			starBonus,
			killBonus,
			hullBonus,
			timeBonus,
			deployPenalty,
			rawScore,
			multiplier,
			finalScore);
	}

	private void ParseArguments()
	{
		foreach (var arg in GetCommandLineArguments())
		{
			if (arg.StartsWith(RoleArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				var roleValue = arg[RoleArgPrefix.Length..].Trim();
				_role = roleValue.Equals("host", StringComparison.OrdinalIgnoreCase)
					? SmokeRole.Host
					: roleValue.Equals("client", StringComparison.OrdinalIgnoreCase)
						? SmokeRole.Client
						: SmokeRole.None;
			}
			else if (arg.StartsWith(AddressArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				var addressValue = arg[AddressArgPrefix.Length..].Trim();
				if (!string.IsNullOrWhiteSpace(addressValue))
				{
					_address = addressValue;
				}
			}
			else if (arg.StartsWith(TimeoutArgPrefix, StringComparison.OrdinalIgnoreCase) &&
				double.TryParse(arg[TimeoutArgPrefix.Length..], out var timeoutSeconds) &&
				timeoutSeconds > 5d)
			{
				_timeoutSeconds = timeoutSeconds;
			}
		}
	}

	private static string[] GetCommandLineArguments()
	{
		var userArgs = OS.GetCmdlineUserArgs();
		return userArgs.Length > 0 ? userArgs : OS.GetCmdlineArgs();
	}

	private void TransitionTo(SmokeState nextState, string reason)
	{
		_state = nextState;
		_stateElapsedSeconds = 0d;
		Log($"state -> {nextState}  |  {reason}");
	}

	private void Fail(string message)
	{
		Log($"LAN_SMOKE FAIL  |  {message}");
		_state = SmokeState.Failed;
		GetTree().Quit(1);
	}

	private void Log(string message)
	{
		GD.Print($"[LAN_SMOKE:{_role}] {message}");
	}
}
