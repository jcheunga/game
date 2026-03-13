using System;
using System.Text;
using Godot;

public static class OnlineRoomResultService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomResultProvider LocalProvider = new LocalOnlineRoomResultProvider();
	private static OnlineRoomResultSubmission _lastSubmission;
	private static string _lastStatus = "Online room result not submitted yet.";

	public static bool HasJoinedRoomForChallenge(AsyncChallengeDefinition challenge)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null || challenge == null)
		{
			return false;
		}

		if (string.Equals(ticket.Status, "spectate", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ticket.Status, "waitlist", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode)
			.Equals(AsyncChallengeCatalog.NormalizeCode(challenge.Code), StringComparison.OrdinalIgnoreCase);
	}

	public static bool SubmitChallengeResult(
		AsyncChallengeDefinition challenge,
		AsyncChallengeScoreBreakdown scoreBreakdown,
		float elapsedSeconds,
		int starsEarned,
		int enemyDefeats,
		float busHullRatio,
		bool won,
		bool retreated,
		bool usedLockedDeck,
		out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (!HasJoinedRoomForChallenge(challenge) || ticket == null)
		{
			message = "No active internet room is armed for this challenge board.";
			_lastStatus = message;
			return false;
		}

		var score = Math.Max(0, scoreBreakdown?.FinalScore ?? 0);
		var hullPercent = Mathf.RoundToInt(Mathf.Clamp(busHullRatio, 0f, 1f) * 100f);
		var request = new OnlineRoomResultRequest
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			JoinToken = ticket.JoinToken,
			PlayerProfileId = GameState.Instance?.PlayerProfileId ?? "",
			PlayerCallsign = GameState.Instance?.PlayerCallsign ?? "Convoy",
			Score = score,
			StarsEarned = Mathf.Clamp(starsEarned, 0, 3),
			HullPercent = hullPercent,
			ElapsedSeconds = Math.Max(0f, elapsedSeconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			Won = won,
			Retreated = retreated,
			UsedLockedDeck = usedLockedDeck,
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastSubmission = provider.SubmitResult(ticket, request);
			_lastStatus = $"{provider.DisplayName}: {_lastSubmission.Summary}";
			OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
			OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
			message =
				$"Submitted room result for {challenge.Code} via {provider.DisplayName}.\n" +
				sessionMessage + "\n" +
				scoreboardMessage;
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room result failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return
				"Online room result:\n" +
				"No active internet room ticket. Join or host a room before posting race results.\n" +
				$"Provider status: {_lastStatus}";
		}

		if (_lastSubmission == null)
		{
			return
				"Online room result:\n" +
				$"Room {ticket.RoomTitle} is armed, but no room result has been submitted yet.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room result ({_lastSubmission.ProviderDisplayName}):");
		builder.AppendLine(_lastSubmission.Summary);
		builder.AppendLine($"Room: {_lastSubmission.RoomId}  |  Board: {_lastSubmission.BoardCode}");
		builder.AppendLine($"Score: {_lastSubmission.Score}  |  Provisional rank: {(_lastSubmission.ProvisionalRank > 0 ? $"#{_lastSubmission.ProvisionalRank}" : "pending")}");
		builder.Append($"Status: {_lastSubmission.Status}");
		return builder.ToString();
	}

	public static void ClearLastSubmission(string reason = "")
	{
		_lastSubmission = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	private static IOnlineRoomResultProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomResultProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: LocalProvider;
	}

	private static string BuildHttpEndpoint(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-result";
		}

		return normalized.TrimEnd('/') + "/challenge-room-result";
	}
}
