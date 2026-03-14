using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class OnlineRoomRecoveryService
{
	public static bool IsAvailable => true;

	private static OnlineRoomJoinTicket _lastRecoveredTicket;
	private static string _lastRecoveryMode = "";
	private static long _lastRecoveredAtUnixSeconds;
	private static string _lastStatus = "Online room recovery not attempted yet.";

	public static bool CanRecoverExpiredSeat()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		return ticket != null && OnlineRoomJoinService.IsTicketExpired(ticket);
	}

	public static bool TryRecoverExpiredSeat(out string message)
	{
		var expiredTicket = OnlineRoomJoinService.GetCachedTicket();
		if (expiredTicket == null)
		{
			message = "No cached online room seat is available to recover.";
			_lastStatus = message;
			return false;
		}

		if (!OnlineRoomJoinService.IsTicketExpired(expiredTicket))
		{
			message = $"Room seat for {expiredTicket.RoomTitle} is still active.";
			_lastStatus = message;
			return false;
		}

		if (IsHostSeat(expiredTicket))
		{
			message =
				$"Host seat for {expiredTicket.RoomTitle} expired while offline.\n" +
				"Publish the room again or negotiate a fresh host seat manually.";
			_lastStatus = message;
			return false;
		}

		var statusParts = new List<string>();
		var boardMessage = SyncSelectedBoardFromTicket(expiredTicket);
		if (!string.IsNullOrWhiteSpace(boardMessage))
		{
			statusParts.Add(boardMessage);
		}

		var room = FindRecoverableRoom(expiredTicket, statusParts);
		if (room != null)
		{
			if (OnlineRoomJoinService.RequestJoin(room, out var joinMessage))
			{
				statusParts.Add($"Recovered expired room seat by rejoining {room.Title}.");
				statusParts.Add(joinMessage);
				RefreshJoinedRoomState(statusParts);
				FinalizeRecovery("rejoin", statusParts, out message);
				return true;
			}

			statusParts.Add($"Direct room recovery for {room.Title} failed.");
			statusParts.Add(joinMessage);
		}
		else
		{
			statusParts.Add($"Original room {expiredTicket.RoomTitle} is unavailable. Trying quick match on the same board.");
		}

		if (OnlineRoomMatchmakeService.QuickMatchBoard(expiredTicket.BoardCode, expiredTicket.UsesLockedDeck, out var matchMessage))
		{
			statusParts.Add($"Recovered stale seat through quick match for {expiredTicket.BoardCode}.");
			statusParts.Add(matchMessage);
			FinalizeRecovery("quick_match", statusParts, out message);
			return true;
		}

		statusParts.Add(matchMessage);
		message = string.Join("\n", statusParts.Where(part => !string.IsNullOrWhiteSpace(part)));
		_lastStatus = message;
		_lastRecoveredTicket = null;
		_lastRecoveryMode = "";
		_lastRecoveredAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return false;
	}

	public static string BuildStatusSummary()
	{
		var currentTicket = OnlineRoomJoinService.GetCachedTicket();
		if (!MatchesTicket(_lastRecoveredTicket, currentTicket))
		{
			return
				"Online room recovery:\n" +
				"No current stale-room recovery cached.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine("Online room recovery:");
		builder.AppendLine($"Mode: {_lastRecoveryMode}  |  Room: {_lastRecoveredTicket.RoomTitle}");
		builder.AppendLine($"Board: {_lastRecoveredTicket.BoardCode}  |  Seat: {_lastRecoveredTicket.SeatLabel}");
		builder.AppendLine($"Status: {_lastRecoveredTicket.Status}");
		builder.AppendLine($"Recovered at: {FormatUnixTime(_lastRecoveredAtUnixSeconds)}");
		builder.Append($"Provider status: {_lastStatus}");
		return builder.ToString();
	}

	public static void ClearLastRecovery(string reason = "")
	{
		_lastRecoveredTicket = null;
		_lastRecoveryMode = "";
		_lastRecoveredAtUnixSeconds = 0;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	private static OnlineRoomDirectoryEntry FindRecoverableRoom(OnlineRoomJoinTicket expiredTicket, List<string> statusParts)
	{
		var cachedRoom = LookupRoom(expiredTicket);
		if (cachedRoom != null)
		{
			statusParts.Add($"Found cached room listing for {cachedRoom.Title}.");
			return cachedRoom;
		}

		var gameState = GameState.Instance;
		if (gameState == null)
		{
			statusParts.Add("Game state is unavailable, so the room directory could not be refreshed.");
			return null;
		}

		OnlineRoomDirectoryService.RefreshRooms(gameState.HighestUnlockedStage, gameState.MaxStage, 8, out var directoryMessage);
		if (!string.IsNullOrWhiteSpace(directoryMessage))
		{
			statusParts.Add(directoryMessage);
		}

		var refreshedRoom = LookupRoom(expiredTicket);
		if (refreshedRoom != null)
		{
			statusParts.Add($"Found live room listing for {refreshedRoom.Title}.");
			return refreshedRoom;
		}

		statusParts.Add($"Original room {expiredTicket.RoomTitle} is not listed for {expiredTicket.BoardCode}.");
		return null;
	}

	private static OnlineRoomDirectoryEntry LookupRoom(OnlineRoomJoinTicket ticket)
	{
		var targetBoardCode = AsyncChallengeCatalog.NormalizeCode(ticket?.BoardCode ?? "");
		return OnlineRoomDirectoryService.GetCachedRooms()
			.FirstOrDefault(entry =>
				!string.IsNullOrWhiteSpace(entry?.RoomId) &&
				!string.IsNullOrWhiteSpace(ticket?.RoomId) &&
				entry.RoomId.Equals(ticket.RoomId, StringComparison.OrdinalIgnoreCase) &&
				AsyncChallengeCatalog.NormalizeCode(entry.BoardCode)
					.Equals(targetBoardCode, StringComparison.OrdinalIgnoreCase));
	}

	private static string SyncSelectedBoardFromTicket(OnlineRoomJoinTicket ticket)
	{
		var gameState = GameState.Instance;
		if (gameState == null || ticket == null || string.IsNullOrWhiteSpace(ticket.BoardCode))
		{
			return "";
		}

		return gameState.TrySetSelectedAsyncChallengeBoard(
			ticket.BoardCode,
			ticket.UsesLockedDeck ? ticket.LockedDeckUnitIds : Array.Empty<string>(),
			out var message)
			? message
			: $"Room recovery could not re-arm the local board automatically: {message}";
	}

	private static void RefreshJoinedRoomState(List<string> statusParts)
	{
		if (GameState.Instance == null)
		{
			statusParts.Add("Skipped live room refresh because the full game state is unavailable.");
			return;
		}

		OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
		statusParts.Add(sessionMessage);
		OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
		statusParts.Add(scoreboardMessage);
	}

	private static void FinalizeRecovery(string recoveryMode, List<string> statusParts, out string message)
	{
		_lastRecoveredTicket = OnlineRoomJoinService.GetCachedTicket();
		_lastRecoveryMode = recoveryMode == "quick_match" ? "quick match fallback" : "direct room rejoin";
		_lastRecoveredAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		message = string.Join("\n", statusParts.Where(part => !string.IsNullOrWhiteSpace(part)));
		_lastStatus = message;
	}

	private static bool IsHostSeat(OnlineRoomJoinTicket ticket)
	{
		return ticket != null &&
			(string.Equals(ticket.Status, "hosted", StringComparison.OrdinalIgnoreCase) ||
			 (ticket.SeatLabel?.IndexOf("host", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
	}

	private static bool MatchesTicket(OnlineRoomJoinTicket left, OnlineRoomJoinTicket right)
	{
		if (left == null || right == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(left.TicketId) &&
			!string.IsNullOrWhiteSpace(right.TicketId) &&
			!left.TicketId.Equals(right.TicketId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(left.RoomId) &&
			!string.IsNullOrWhiteSpace(right.RoomId) &&
			!left.RoomId.Equals(right.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(left.BoardCode) ||
			string.IsNullOrWhiteSpace(right.BoardCode) ||
			AsyncChallengeCatalog.NormalizeCode(left.BoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(right.BoardCode), StringComparison.OrdinalIgnoreCase);
	}

	private static string FormatUnixTime(long unixSeconds)
	{
		return unixSeconds <= 0
			? "never"
			: DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss");
	}
}
