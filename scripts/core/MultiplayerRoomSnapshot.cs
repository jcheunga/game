using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MultiplayerRoomPeerSnapshot
{
	public int PeerId { get; init; }
	public string Label { get; init; } = "";
	public string Phase { get; init; } = "";
	public bool IsReady { get; init; }
	public bool IsLoaded { get; init; }
	public bool IsLaunchEligible { get; init; }
	public bool HasFullDeck { get; init; }
	public int MonitorRank { get; init; }
	public string PresenceText { get; init; } = "";
	public string MonitorText { get; init; } = "";
	public string DeckText { get; init; } = "";
}

public sealed class MultiplayerRoomSnapshot
{
	public bool HasRoom { get; init; }
	public string TransportLabel { get; init; } = "";
	public string RoleLabel { get; init; } = "";
	public int PeerCount { get; init; }
	public string SharedChallengeCode { get; init; } = "";
	public string SharedChallengeTitle { get; init; } = "";
	public string LocalCallsign { get; init; } = "";
	public string DeckModeSummary { get; init; } = "";
	public string JoinAddressSummary { get; init; } = "";
	public bool UsesLockedDeck { get; init; }
	public bool RoundLocked { get; init; }
	public bool RoundComplete { get; init; }
	public bool RaceCountdownActive { get; init; }
	public float RaceCountdownRemainingSeconds { get; init; }
	public string SelectedBoardCode { get; init; } = "";
	public string SelectedBoardDeckMode { get; init; } = "";
	public IReadOnlyList<MultiplayerRoomPeerSnapshot> Peers { get; init; } = Array.Empty<MultiplayerRoomPeerSnapshot>();
}

public static class MultiplayerRoomFormatter
{
	public static string BuildRoomSummary(MultiplayerRoomSnapshot snapshot)
	{
		if (snapshot == null)
		{
			return "No room snapshot available.";
		}

		if (!snapshot.HasRoom)
		{
			return
				$"No {snapshot.TransportLabel} room active.\n" +
				$"Selected board: {snapshot.SelectedBoardCode}\n" +
				$"Board deck mode: {snapshot.SelectedBoardDeckMode}\n" +
				$"Transport: {snapshot.TransportLabel}\n" +
				"Host a room to broadcast the current board, or join a host IP to sync it.";
		}

		var launchEligiblePeers = snapshot.Peers.Where(peer => peer.IsLaunchEligible).ToArray();
		var spectatorCount = snapshot.Peers.Count - launchEligiblePeers.Length;
		var submittedCount = launchEligiblePeers.Count(peer => MatchesPhase(peer.Phase, "submitted"));
		var loadingCount = launchEligiblePeers.Count(peer => MatchesPhase(peer.Phase, "loading"));
		var racingCount = launchEligiblePeers.Count(peer => MatchesPhase(peer.Phase, "racing"));
		var readyCount = launchEligiblePeers.Count(peer => peer.IsReady);
		var hasSubmittedRound = submittedCount > 0;
		var hasActiveRace = hasSubmittedRound || racingCount > 0;
		var hasLoadingRace = loadingCount > 0;
		var roomStatusSummary = hasLoadingRace
			? snapshot.RaceCountdownActive
				? $"Launch sync: all runners loaded, start in {snapshot.RaceCountdownRemainingSeconds:0.0}s"
				: $"Launch sync: {launchEligiblePeers.Count(peer => peer.IsLoaded)}/{Math.Max(1, launchEligiblePeers.Length)} loaded"
			: snapshot.RoundComplete
			? $"Round complete: {submittedCount}/{Math.Max(1, launchEligiblePeers.Length)} results posted. Ready for rematch or board refresh."
			: hasActiveRace
			? $"Race progress: {submittedCount}/{Math.Max(1, launchEligiblePeers.Length)} submitted, {racingCount} in battle"
			: launchEligiblePeers.Length == 0
				? "Ready: 0/0"
				: $"Ready: {readyCount}/{launchEligiblePeers.Length}";
		if (spectatorCount > 0)
		{
			roomStatusSummary += $"  |  Spectators {spectatorCount}";
		}

		var readyLines = snapshot.Peers.Count == 0
			? "No room peers synced yet."
			: string.Join("\n", snapshot.Peers.Select(peer => $"{peer.Label}: {peer.PresenceText}"));
		var addressSummary = string.IsNullOrWhiteSpace(snapshot.JoinAddressSummary)
			? ""
			: $"\nShare IP: {snapshot.JoinAddressSummary}";
		var peerDeckSummary = snapshot.UsesLockedDeck
			? ""
			: $"\nDeck sync: {launchEligiblePeers.Count(peer => peer.HasFullDeck)}/{launchEligiblePeers.Length} full active convoys\nPeer convoys:\n{BuildPeerDeckSummaryText(snapshot.Peers)}";
		return
			$"{snapshot.RoleLabel} room active  |  Peers: {snapshot.PeerCount}\n" +
			$"Board: {snapshot.SharedChallengeCode}\n" +
			$"{snapshot.SharedChallengeTitle}\n" +
			$"Transport: {snapshot.TransportLabel}\n" +
			$"Local callsign: {snapshot.LocalCallsign}\n" +
			$"{snapshot.DeckModeSummary}\n" +
			$"{roomStatusSummary}\n" +
			$"{readyLines}{peerDeckSummary}{addressSummary}";
	}

	public static string BuildLaunchReadinessSummary(MultiplayerRoomSnapshot snapshot)
	{
		if (snapshot == null || !snapshot.HasRoom)
		{
			return "Launch readiness: no active room.";
		}

		var launchEligiblePeers = snapshot.Peers.Where(peer => peer.IsLaunchEligible).ToArray();
		var spectatorPeers = snapshot.Peers.Where(peer => !peer.IsLaunchEligible).ToArray();
		var readyPeers = launchEligiblePeers.Where(peer => peer.IsReady).ToArray();
		var unreadyPeers = launchEligiblePeers.Where(peer => !peer.IsReady).ToArray();
		var incompleteDeckPeers = snapshot.UsesLockedDeck
			? Array.Empty<MultiplayerRoomPeerSnapshot>()
			: launchEligiblePeers.Where(peer => !peer.HasFullDeck).ToArray();

		var lines = new List<string>
		{
			$"Launch readiness for {snapshot.SharedChallengeCode}:"
		};
		if (snapshot.RoundLocked && !snapshot.RoundComplete)
		{
			lines.Add(snapshot.RaceCountdownActive
				? $"Round status: countdown live, combat starts in {snapshot.RaceCountdownRemainingSeconds:0.0}s."
				: "Round status: current room race is still in flight.");
		}
		else if (snapshot.RoundComplete)
		{
			lines.Add("Round status: complete. Ready runners can launch the rematch.");
		}
		else
		{
			lines.Add("Round status: lobby open for launch.");
		}

		lines.Add($"Runner pool: {launchEligiblePeers.Length}  |  Ready {readyPeers.Length}/{launchEligiblePeers.Length}");
		lines.Add(readyPeers.Length > 0
			? $"Ready now: {BuildPeerListText(readyPeers)}"
			: "Ready now: none yet");
		if (unreadyPeers.Length > 0)
		{
			lines.Add($"Waiting on ready: {BuildPeerListText(unreadyPeers)}");
		}

		if (snapshot.UsesLockedDeck)
		{
			lines.Add("Deck sync: locked shared squad, no personal deck blockers.");
		}
		else
		{
			lines.Add(incompleteDeckPeers.Length > 0
				? $"Deck blockers: {BuildPeerListText(incompleteDeckPeers)}"
				: "Deck sync: all active runners have full convoys.");
		}

		if (spectatorPeers.Length > 0)
		{
			lines.Add($"Spectators: {BuildPeerListText(spectatorPeers)}");
		}

		lines.Add(launchEligiblePeers.Length > 0 &&
			readyPeers.Length == launchEligiblePeers.Length &&
			incompleteDeckPeers.Length == 0 &&
			!snapshot.RoundLocked
			? "Launch state: room is green to launch."
			: "Launch state: waiting on the blockers above.");
		return string.Join("\n", lines);
	}

	public static string BuildRaceMonitorSummary(MultiplayerRoomSnapshot snapshot)
	{
		if (snapshot == null || !snapshot.HasRoom)
		{
			return "Room race monitor: no active room.";
		}

		if (snapshot.Peers.Count == 0)
		{
			return "Room race monitor: waiting for peers.";
		}

		var lines = new List<string>
		{
			$"Room race monitor for {snapshot.SharedChallengeCode}:"
		};
		foreach (var peer in snapshot.Peers
			.OrderBy(entry => entry.MonitorRank)
			.ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase))
		{
			lines.Add(peer.MonitorText);
		}

		return string.Join("\n", lines);
	}

	private static string BuildPeerDeckSummaryText(IEnumerable<MultiplayerRoomPeerSnapshot> peers)
	{
		var lines = peers
			.Select(peer => peer.DeckText)
			.Where(line => !string.IsNullOrWhiteSpace(line))
			.ToArray();
		return lines.Length == 0
			? "Deck sync pending."
			: string.Join("\n", lines);
	}

	private static string BuildPeerListText(IEnumerable<MultiplayerRoomPeerSnapshot> peers)
	{
		var labels = peers
			.Select(peer => peer.Label)
			.Where(label => !string.IsNullOrWhiteSpace(label))
			.ToArray();
		return labels.Length == 0 ? "none" : string.Join(", ", labels);
	}

	private static bool MatchesPhase(string phase, string expected)
	{
		return string.Equals(phase, expected, StringComparison.OrdinalIgnoreCase);
	}
}
