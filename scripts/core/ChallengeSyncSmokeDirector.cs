using System;
using System.Linq;
using Godot;

public partial class ChallengeSyncSmokeDirector : Node
{
	private const string ProviderArgPrefix = "--sync-smoke-provider=";
	private const string EndpointArgPrefix = "--sync-smoke-endpoint=";
	private const string TimeoutArgPrefix = "--sync-smoke-timeout=";
	private const double DefaultTimeoutSeconds = 20d;

	private enum SmokeState
	{
		Disabled,
		WaitForServices,
		QueueResult,
		Flush,
		Verify,
		Passed,
		Failed
	}

	private SmokeState _state = SmokeState.Disabled;
	private string _providerId = "";
	private string _endpoint = "";
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
		Log($"boot ready  |  provider {_providerId}  |  endpoint {(_endpoint == "" ? "<none>" : _endpoint)}");
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
			case SmokeState.QueueResult:
				ProcessQueueResult();
				break;
			case SmokeState.Flush:
				ProcessFlush();
				break;
			case SmokeState.Verify:
				ProcessVerify();
				break;
		}
	}

	private void ProcessWaitForServices()
	{
		if (GameState.Instance == null || ChallengeSyncService.Instance == null || SaveSystem.Instance == null)
		{
			return;
		}

		GameState.Instance.SetPlayerCallsign("SyncSmoke");
		GameState.Instance.SetChallengeSyncProvider(_providerId);
		GameState.Instance.SetChallengeSyncEndpoint(_endpoint);
		GameState.Instance.SetChallengeSyncAutoFlush(false);
		TransitionTo(SmokeState.QueueResult, "queue synthetic result");
	}

	private void ProcessQueueResult()
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			return;
		}

		var smokeCode = AsyncChallengeCatalog.Create(1, AsyncChallengeCatalog.PressureSpikeId, 5151).Code;
		if (!gameState.TrySetSelectedAsyncChallengeCode(smokeCode, out var message))
		{
			Fail($"failed to arm smoke challenge: {message}");
			return;
		}

		var deckIds = gameState.GetActiveDeckUnits().Select(unit => unit.Id).ToArray();
		gameState.ApplyAsyncChallengeResult(
			smokeCode,
			1320,
			27.5f,
			14,
			2,
			true,
			false,
			deckIds,
			[
				new ChallengeDeploymentRecord
				{
					UnitId = deckIds[0],
					TimeSeconds = 1.5f,
					LanePercent = 40
				}
			],
			1,
			0.78f,
			false,
			new AsyncChallengeScoreBreakdown(400, 240, 180, 120, 60, 20, 980, 1.35f, 1320));

		if (gameState.PendingChallengeSubmissionCount <= 0)
		{
			Fail("synthetic challenge result did not queue an outbox packet");
			return;
		}

		Log($"queued packet  |  pending {gameState.PendingChallengeSubmissionCount}");
		TransitionTo(SmokeState.Flush, "flush through provider");
	}

	private void ProcessFlush()
	{
		if (!ChallengeSyncService.Instance.FlushPendingSubmissions(out var message))
		{
			Fail($"flush failed: {message}");
			return;
		}

		Log($"flush ok  |  {message}");
		TransitionTo(SmokeState.Verify, "verify queue clear");
	}

	private void ProcessVerify()
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			return;
		}

		if (gameState.PendingChallengeSubmissionCount > 0)
		{
			Fail($"expected empty outbox after smoke flush, still have {gameState.PendingChallengeSubmissionCount}");
			return;
		}

		if (gameState.TotalChallengeSubmissionsSynced <= 0)
		{
			Fail("sync counter did not increment");
			return;
		}

		Log($"SYNC_SMOKE PASS  |  provider {ChallengeSyncProviderCatalog.GetDisplayName(gameState.ChallengeSyncProviderId)}  |  synced {gameState.TotalChallengeSubmissionsSynced}");
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
		Log($"SYNC_SMOKE FAIL  |  {reason}");
		_state = SmokeState.Failed;
		GetTree().Quit(1);
	}

	private static void Log(string message)
	{
		GD.Print($"[SYNC_SMOKE] {message}");
	}
}
