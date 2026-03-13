using System;
using System.Collections.Generic;
using System.Linq;

public static class LocalOnlineRoomStubState
{
	public sealed class TelemetrySnapshot
	{
		public string PlayerCallsign { get; init; } = "";
		public int ElapsedDeciseconds { get; init; }
		public int EnemyDefeats { get; init; }
		public int HullPercent { get; init; }
	}

	private sealed class RoomState
	{
		public bool RoundLaunched { get; set; }
		public bool RoundComplete { get; set; }
		public HashSet<string> SubmittedCallsigns { get; } = new(StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TelemetrySnapshot> TelemetryByCallsign { get; } = new(StringComparer.OrdinalIgnoreCase);
	}

	private static readonly Dictionary<string, RoomState> StatesByRoomId = new(StringComparer.OrdinalIgnoreCase);

	public static void MarkRoundLaunched(string roomId)
	{
		var state = GetOrCreate(roomId);
		state.RoundLaunched = true;
		state.RoundComplete = false;
		state.SubmittedCallsigns.Clear();
		state.TelemetryByCallsign.Clear();
	}

	public static void MarkRoundReset(string roomId)
	{
		if (string.IsNullOrWhiteSpace(roomId))
		{
			return;
		}

		StatesByRoomId.Remove(roomId.Trim());
	}

	public static void MarkResultSubmitted(string roomId, string playerCallsign)
	{
		var state = GetOrCreate(roomId);
		state.RoundLaunched = false;
		state.RoundComplete = true;
		if (!string.IsNullOrWhiteSpace(playerCallsign))
		{
			state.SubmittedCallsigns.Add(playerCallsign.Trim());
		}
	}

	public static void UpdateTelemetry(string roomId, string playerCallsign, float elapsedSeconds, int enemyDefeats, int hullPercent)
	{
		if (string.IsNullOrWhiteSpace(playerCallsign))
		{
			return;
		}

		var state = GetOrCreate(roomId);
		state.RoundLaunched = true;
		state.TelemetryByCallsign[playerCallsign.Trim()] = new TelemetrySnapshot
		{
			PlayerCallsign = playerCallsign.Trim(),
			ElapsedDeciseconds = Math.Max(0, (int)MathF.Round(Math.Max(0f, elapsedSeconds) * 10f)),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			HullPercent = Math.Clamp(hullPercent, 0, 100)
		};
	}

	public static bool IsRoundLaunched(string roomId)
	{
		return TryGet(roomId, out var state) && state.RoundLaunched;
	}

	public static bool IsRoundComplete(string roomId)
	{
		return TryGet(roomId, out var state) && state.RoundComplete;
	}

	public static IReadOnlyList<string> GetSubmittedCallsigns(string roomId)
	{
		return TryGet(roomId, out var state)
			? state.SubmittedCallsigns.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
			: [];
	}

	public static IReadOnlyList<TelemetrySnapshot> GetTelemetrySnapshots(string roomId)
	{
		return TryGet(roomId, out var state)
			? state.TelemetryByCallsign.Values.OrderBy(snapshot => snapshot.PlayerCallsign, StringComparer.OrdinalIgnoreCase).ToArray()
			: [];
	}

	private static RoomState GetOrCreate(string roomId)
	{
		var normalizedRoomId = NormalizeRoomId(roomId);
		if (!StatesByRoomId.TryGetValue(normalizedRoomId, out var state))
		{
			state = new RoomState();
			StatesByRoomId[normalizedRoomId] = state;
		}

		return state;
	}

	private static bool TryGet(string roomId, out RoomState state)
	{
		return StatesByRoomId.TryGetValue(NormalizeRoomId(roomId), out state);
	}

	private static string NormalizeRoomId(string roomId)
	{
		return string.IsNullOrWhiteSpace(roomId) ? "default-room" : roomId.Trim();
	}
}
