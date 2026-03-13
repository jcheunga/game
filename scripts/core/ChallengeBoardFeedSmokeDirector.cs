using System;
using Godot;

public partial class ChallengeBoardFeedSmokeDirector : Node
{
	private const string ProviderArgPrefix = "--feed-smoke-provider=";
	private const string EndpointArgPrefix = "--feed-smoke-endpoint=";
	private const string TimeoutArgPrefix = "--feed-smoke-timeout=";
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
		if (GameState.Instance == null || ChallengeBoardFeedService.Instance == null)
		{
			return;
		}

		GameState.Instance.SetChallengeSyncProvider(_providerId);
		GameState.Instance.SetChallengeSyncEndpoint(_endpoint);
		TransitionTo(SmokeState.Fetch, "fetch feed");
	}

	private void ProcessFetch()
	{
		if (!ChallengeBoardFeedService.Instance.RefreshFeed(GameState.Instance.HighestUnlockedStage, GameState.Instance.MaxStage, 3, out var message))
		{
			Fail($"fetch failed: {message}");
			return;
		}

		Log($"fetch ok  |  {message}");
		TransitionTo(SmokeState.Verify, "verify feed");
	}

	private void ProcessVerify()
	{
		var snapshot = ChallengeBoardFeedService.Instance.GetCachedSnapshot();
		if (snapshot == null)
		{
			Fail("no cached feed snapshot after fetch");
			return;
		}

		if (snapshot.Items.Count < 2)
		{
			Fail($"expected at least 2 feed items, got {snapshot.Items.Count}");
			return;
		}

		var featured = ChallengeBoardFeedService.Instance.GetCachedFeaturedChallenges();
		if (featured.Count < 2)
		{
			Fail($"expected at least 2 normalized featured boards, got {featured.Count}");
			return;
		}

		Log($"FEED_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  items {snapshot.Items.Count}  |  first {featured[0].Challenge.Code}");
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
		Log($"FEED_SMOKE FAIL  |  {reason}");
		_state = SmokeState.Failed;
		GetTree().Quit(1);
	}

	private void Log(string message)
	{
		GD.Print($"[FEED_SMOKE] {message}");
	}
}
