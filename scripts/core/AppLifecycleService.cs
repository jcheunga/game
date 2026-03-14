using System;
using System.Collections.Generic;
using Godot;

public partial class AppLifecycleService : Node
{
	public static AppLifecycleService Instance { get; private set; }

	public event Action StateChanged;

	public bool IsApplicationPaused { get; private set; }
	public bool HasApplicationFocus { get; private set; } = true;
	public bool IsInteractive => !IsApplicationPaused && HasApplicationFocus;
	public bool ShouldPauseOnlineRoomTraffic => !IsInteractive;
	public long LastBackgroundedAtUnixSeconds { get; private set; }
	public long LastResumedAtUnixSeconds { get; private set; }
	public string LastLifecycleStatus { get; private set; } = "Application is in the foreground.";
	public string LastResumeRecoverySummary { get; private set; } = "No resume recovery run yet.";

	private bool _resumeRecoveryPending;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Notification(int what)
	{
		switch (what)
		{
			case (int)NotificationApplicationPaused:
				HandleBackgroundSignal("Application paused by the OS.", setPaused: true, focusState: null);
				break;
			case (int)NotificationApplicationResumed:
				HandleForegroundSignal("Application resumed.", clearPaused: true, focusState: null);
				break;
			case (int)NotificationApplicationFocusOut:
			case (int)NotificationWMWindowFocusOut:
				HandleBackgroundSignal("Application focus lost.", setPaused: false, focusState: false);
				break;
			case (int)NotificationApplicationFocusIn:
			case (int)NotificationWMWindowFocusIn:
				HandleForegroundSignal("Application focus restored.", clearPaused: false, focusState: true);
				break;
		}
	}

	public string BuildStatusSummary()
	{
		return
			"App lifecycle:\n" +
			$"State: {(IsInteractive ? "foreground" : IsApplicationPaused ? "backgrounded" : "focus lost")}\n" +
			$"Last backgrounded: {FormatUnixTime(LastBackgroundedAtUnixSeconds)}\n" +
			$"Last resumed: {FormatUnixTime(LastResumedAtUnixSeconds)}\n" +
			$"Last event: {LastLifecycleStatus}\n" +
			$"Resume recovery: {LastResumeRecoverySummary}";
	}

	private void HandleBackgroundSignal(string reason, bool setPaused, bool? focusState)
	{
		var changed = false;
		if (setPaused && !IsApplicationPaused)
		{
			IsApplicationPaused = true;
			changed = true;
		}

		if (focusState.HasValue && HasApplicationFocus != focusState.Value)
		{
			HasApplicationFocus = focusState.Value;
			changed = true;
		}

		if (!ShouldPauseOnlineRoomTraffic)
		{
			return;
		}

		_resumeRecoveryPending = true;
		LastBackgroundedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		LastLifecycleStatus = $"{reason} Room polling is paused.";
		if (changed)
		{
			StateChanged?.Invoke();
		}
	}

	private void HandleForegroundSignal(string reason, bool clearPaused, bool? focusState)
	{
		var changed = false;
		if (clearPaused && IsApplicationPaused)
		{
			IsApplicationPaused = false;
			changed = true;
		}

		if (focusState.HasValue && HasApplicationFocus != focusState.Value)
		{
			HasApplicationFocus = focusState.Value;
			changed = true;
		}

		LastResumedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		LastLifecycleStatus = IsInteractive
			? $"{reason} Foreground network activity restored."
			: $"{reason} Waiting for full foreground state before resuming room traffic.";
		if (IsInteractive && _resumeRecoveryPending)
		{
			RunResumeRecovery(reason);
			_resumeRecoveryPending = false;
			changed = true;
		}

		if (changed)
		{
			StateChanged?.Invoke();
		}
	}

	private void RunResumeRecovery(string reason)
	{
		var lines = new List<string>();
		if (PlayerProfileSyncService.RefreshProfile(out var profileMessage))
		{
			lines.Add(profileMessage);
		}
		else
		{
			lines.Add(profileMessage);
		}

		if (OnlineRoomJoinService.GetCachedTicket() == null)
		{
			lines.Add("No joined online room seat was armed.");
		}
		else if (!OnlineRoomJoinService.HasActiveTicket())
		{
			if (OnlineRoomRecoveryService.TryRecoverExpiredSeat(out var recoveryMessage))
			{
				lines.Add(recoveryMessage);
			}
			else
			{
				lines.Add(recoveryMessage);
			}
		}
		else
		{
			if (OnlineRoomSeatLeaseService.TryAutoRenewIfNeeded(out var leaseMessage))
			{
				lines.Add(leaseMessage);
			}
			OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
			lines.Add(sessionMessage);
			OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
			lines.Add(scoreboardMessage);
		}

		var gameState = GameState.Instance;
		if (ChallengeSyncService.Instance != null && gameState != null)
		{
			if (gameState.ChallengeSyncAutoFlush && gameState.PendingChallengeSubmissionCount > 0)
			{
				if (ChallengeSyncService.Instance.TryAutoFlushPending())
				{
					lines.Add("Pending async challenge submissions were auto-flushed on resume.");
				}
				else
				{
					lines.Add("Resume auto-flush attempted, but some challenge submissions remain queued.");
				}
			}
			else
			{
				ChallengeSyncService.Instance.RefreshStatusFromState();
			}
		}

		LastResumeRecoverySummary = $"{reason} {FormatUnixTime(LastResumedAtUnixSeconds)}\n{string.Join("\n", lines)}";
	}

	private static string FormatUnixTime(long unixSeconds)
	{
		return unixSeconds <= 0
			? "never"
			: DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss");
	}
}
