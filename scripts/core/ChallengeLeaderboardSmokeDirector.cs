using System;
using Godot;

public partial class ChallengeLeaderboardSmokeDirector : Node
{
	private const string ProviderArgPrefix = "--leaderboard-smoke-provider=";
	private const string EndpointArgPrefix = "--leaderboard-smoke-endpoint=";
	private const string CodeArgPrefix = "--leaderboard-smoke-code=";
	private const string TimeoutArgPrefix = "--leaderboard-smoke-timeout=";
	private const double DefaultTimeoutSeconds = 15d;

	private enum SmokeState
	{
		Disabled,
		WaitForServices,
		Fetch,
		Verify,
		Passed,
		Failed
	}

	private SmokeState _state = SmokeState.Disabled;
	private string _providerId = "";
	private string _endpoint = "";
	private string _code = "CH-01-PRS-5151";
	private double _timeoutSeconds = DefaultTimeoutSeconds;
	private double _elapsedSeconds;

	public override void _Ready()
	{
		ParseArguments();
		if (string.IsNullOrWhiteSpace(_providerId))
		{
			return;
		}

		ProcessMode = ProcessModeEnum.Always;
		TransitionTo(SmokeState.WaitForServices, "boot");
		Log($"boot ready  |  provider {_providerId}  |  endpoint {(_endpoint == "" ? "<none>" : _endpoint)}  |  code {_code}");
	}

	public override void _Process(double delta)
	{
		if (_state == SmokeState.Disabled || _state == SmokeState.Passed || _state == SmokeState.Failed)
		{
			return;
		}

		_elapsedSeconds += delta;
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
			case SmokeState.Fetch:
				ProcessFetch();
				break;
			case SmokeState.Verify:
				ProcessVerify();
				break;
		}
	}

	private void ProcessWaitForServices()
	{
		if (GameState.Instance == null || ChallengeLeaderboardService.Instance == null)
		{
			return;
		}

		GameState.Instance.SetChallengeSyncProvider(_providerId);
		GameState.Instance.SetChallengeSyncEndpoint(_endpoint);
		TransitionTo(SmokeState.Fetch, "fetch board");
	}

	private void ProcessFetch()
	{
		if (!ChallengeLeaderboardService.Instance.RefreshBoard(_code, 5, out var message))
		{
			Fail($"fetch failed: {message}");
			return;
		}

		Log($"fetch ok  |  {message}");
		TransitionTo(SmokeState.Verify, "verify snapshot");
	}

	private void ProcessVerify()
	{
		var snapshot = ChallengeLeaderboardService.Instance.GetCachedSnapshot(_code);
		if (snapshot == null)
		{
			Fail("no cached snapshot after fetch");
			return;
		}

		if (snapshot.Entries.Count < 2)
		{
			Fail($"expected at least 2 leaderboard entries, got {snapshot.Entries.Count}");
			return;
		}

		if (snapshot.Entries[0].Score < snapshot.Entries[1].Score)
		{
			Fail("leaderboard sort order is incorrect");
			return;
		}

		Log($"LEADERBOARD_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  entries {snapshot.Entries.Count}  |  top {snapshot.Entries[0].PlayerCallsign}");
		TransitionTo(SmokeState.Passed, "done");
		GetTree().Quit(0);
	}

	private void ParseArguments()
	{
		foreach (var argument in GetCommandLineArguments())
		{
			if (argument.StartsWith(ProviderArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				_providerId = ChallengeSyncProviderCatalog.NormalizeId(argument[ProviderArgPrefix.Length..]);
				continue;
			}

			if (argument.StartsWith(EndpointArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				_endpoint = argument[EndpointArgPrefix.Length..].Trim();
				continue;
			}

			if (argument.StartsWith(CodeArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				_code = AsyncChallengeCatalog.NormalizeCode(argument[CodeArgPrefix.Length..]);
				continue;
			}

			if (argument.StartsWith(TimeoutArgPrefix, StringComparison.OrdinalIgnoreCase) &&
				double.TryParse(argument[TimeoutArgPrefix.Length..], out var parsedTimeout))
			{
				_timeoutSeconds = Math.Max(5d, parsedTimeout);
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
		Log($"state -> {nextState}  |  {reason}");
	}

	private void Fail(string reason)
	{
		Log($"LEADERBOARD_SMOKE FAIL  |  {reason}");
		_state = SmokeState.Failed;
		GetTree().Quit(1);
	}

	private static void Log(string message)
	{
		GD.Print($"[LEADERBOARD_SMOKE] {message}");
	}
}
