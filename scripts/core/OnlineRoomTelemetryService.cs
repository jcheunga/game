using System;
using Godot;

public static class OnlineRoomTelemetryService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomTelemetryProvider LocalProvider = new LocalOnlineRoomTelemetryProvider();
	private static OnlineRoomTelemetrySubmission _lastSubmission;
	private static string _lastStatus = "Online room telemetry not sent yet.";

	public static bool HasJoinedRoomForChallenge(AsyncChallengeDefinition challenge)
	{
		return OnlineRoomResultService.HasJoinedRoomForChallenge(challenge);
	}

	public static bool UpdateLocalRaceTelemetry(
		AsyncChallengeDefinition challenge,
		float elapsedSeconds,
		int enemyDefeats,
		float busHullRatio)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (!HasJoinedRoomForChallenge(challenge) || ticket == null)
		{
			return false;
		}

		var request = new OnlineRoomTelemetryRequest
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			JoinToken = ticket.JoinToken,
			PlayerProfileId = GameState.Instance?.PlayerProfileId ?? "",
			PlayerCallsign = GameState.Instance?.PlayerCallsign ?? "Convoy",
			ElapsedSeconds = Math.Max(0f, elapsedSeconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			HullPercent = Mathf.RoundToInt(Mathf.Clamp(busHullRatio, 0f, 1f) * 100f),
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastSubmission = provider.SubmitTelemetry(ticket, request);
			_lastStatus = $"{provider.DisplayName}: {_lastSubmission.Summary}";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room telemetry failed: {ex.Message}";
			return false;
		}
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		var currentSubmission = GetScopedSubmission(ticket);
		if (ticket == null)
		{
			return
				"Online room telemetry:\n" +
				"No active internet room ticket. Join or host a room first.\n" +
				$"Provider status: {_lastStatus}";
		}

		if (currentSubmission == null)
		{
			return
				"Online room telemetry:\n" +
				$"Room {ticket.RoomTitle} is armed, but no live telemetry has been sent yet.\n" +
				$"Provider status: {_lastStatus}";
		}

		return
			$"Online room telemetry ({currentSubmission.ProviderDisplayName}):\n" +
			$"{currentSubmission.Summary}\n" +
			$"Room: {currentSubmission.RoomId}  |  Board: {currentSubmission.BoardCode}\n" +
				$"Status: {currentSubmission.Status}";
	}

	public static void ClearLastSubmission(string reason = "")
	{
		_lastSubmission = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	private static IOnlineRoomTelemetryProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomTelemetryProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-telemetry";
		}

		return normalized.TrimEnd('/') + "/challenge-room-telemetry";
	}

	private static OnlineRoomTelemetrySubmission GetScopedSubmission(OnlineRoomJoinTicket ticket)
	{
		return MatchesRoom(_lastSubmission, ticket) ? _lastSubmission : null;
	}

	private static bool MatchesRoom(OnlineRoomTelemetrySubmission submission, OnlineRoomJoinTicket ticket)
	{
		if (submission == null || ticket == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(submission.RoomId) &&
			!string.IsNullOrWhiteSpace(ticket.RoomId) &&
			!submission.RoomId.Equals(ticket.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(submission.BoardCode) ||
			string.IsNullOrWhiteSpace(ticket.BoardCode) ||
			AsyncChallengeCatalog.NormalizeCode(submission.BoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode), StringComparison.OrdinalIgnoreCase);
	}
}
