using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomSessionProvider : IOnlineRoomSessionProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(6)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _endpointUrl;

	public HttpApiOnlineRoomSessionProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Session";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomSessionSnapshot FetchRoomSession(OnlineRoomJoinTicket ticket)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-session endpoint is not configured.");
		}

		var requestBody = new
		{
			session = new
			{
				roomId = ticket.RoomId,
				boardCode = ticket.BoardCode,
				ticketId = ticket.TicketId,
				joinToken = ticket.JoinToken,
				playerCallsign = GameState.Instance?.PlayerCallsign ?? "Lantern",
				playerProfileId = GameState.Instance?.PlayerProfileId ?? ""
			}
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", GameState.Instance?.PlayerProfileId ?? "");
		message.Headers.TryAddWithoutValidation("X-Join-Ticket", ticket.TicketId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			throw new InvalidOperationException("HTTP session response was empty.");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		var peerSnapshots = ParsePeers(root);
		var roomId = GetString(root, "roomId", ticket.RoomId);
		var roomTitle = GetString(root, "roomTitle", ticket.RoomTitle);
		var boardCode = GetString(root, "boardCode", ticket.BoardCode);
		var boardTitle = GetString(root, "boardTitle", "");
		if (string.IsNullOrWhiteSpace(boardTitle))
		{
			boardTitle = !string.IsNullOrWhiteSpace(ticket.RoomTitle)
				? ticket.RoomTitle
				: boardCode;
		}
		if (string.IsNullOrWhiteSpace(roomTitle))
		{
			roomTitle = boardTitle;
		}
		return new OnlineRoomSessionSnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = GetString(root, "status", "ok"),
			Summary = GetString(root, "message", $"Fetched session snapshot for {ticket.RoomTitle}."),
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			RoomSnapshot = new MultiplayerRoomSnapshot
			{
				HasRoom = true,
				RoomId = roomId,
				RoomTitle = roomTitle,
				TransportLabel = GetString(root, "transportLabel", "Internet Relay"),
				RoleLabel = GetString(root, "roleLabel", "Online contender"),
				PeerCount = peerSnapshots.Count,
				SharedChallengeCode = boardCode,
				SharedChallengeTitle = boardTitle,
				LocalCallsign = GameState.Instance?.PlayerCallsign ?? "Lantern",
				DeckModeSummary = GetString(root, "deckModeSummary", ticket.UsesLockedDeck ? "Deck mode: locked shared squad." : "Deck mode: player squads."),
				JoinAddressSummary = GetString(root, "relayEndpoint", ticket.RelayEndpoint),
				UsesLockedDeck = GetBool(root, "usesLockedDeck", ticket.UsesLockedDeck),
				RoundLocked = GetBool(root, "roundLocked", false),
				RoundComplete = GetBool(root, "roundComplete", false),
				RaceCountdownActive = GetBool(root, "raceCountdownActive", false),
				RaceCountdownRemainingSeconds = GetFloat(root, "raceCountdownRemainingSeconds", 0f),
				SelectedBoardCode = boardCode,
				SelectedBoardDeckMode = GetString(root, "selectedBoardDeckMode", ticket.UsesLockedDeck ? "locked shared squad" : "player squad"),
				Peers = peerSnapshots
			}
		};
	}

	private static List<MultiplayerRoomPeerSnapshot> ParsePeers(JsonElement root)
	{
		var peers = new List<MultiplayerRoomPeerSnapshot>();
		if (!TryGetProperty(root, "peers", out var peersElement) || peersElement.ValueKind != JsonValueKind.Array)
		{
			return peers;
		}

		foreach (var peer in peersElement.EnumerateArray())
		{
			peers.Add(new MultiplayerRoomPeerSnapshot
			{
				PeerId = GetInt(peer, "peerId", peers.Count + 1),
				Label = GetString(peer, "label", $"Peer {peers.Count + 1}"),
				IsLocalPlayer = GetBool(peer, "isLocalPlayer", false),
				Phase = GetString(peer, "phase", "prep"),
				IsReady = GetBool(peer, "isReady", false),
				IsLoaded = GetBool(peer, "isLoaded", false),
				IsLaunchEligible = GetBool(peer, "isLaunchEligible", true),
				HasFullDeck = GetBool(peer, "hasFullDeck", true),
				MonitorRank = GetInt(peer, "monitorRank", peers.Count + 1),
				RaceElapsedSeconds = GetFloat(peer, "raceElapsedSeconds", -1f),
				HullPercent = GetInt(peer, "hullPercent", -1),
				EnemyDefeats = GetInt(peer, "enemyDefeats", -1),
				PostedScore = GetInt(peer, "postedScore", -1),
				PostedRank = GetInt(peer, "postedRank", -1),
				PresenceText = GetString(peer, "presenceText", "joined"),
				MonitorText = GetString(peer, "monitorText", "joined"),
				DeckText = GetString(peer, "deckText", "")
			});
		}

		return peers;
	}

	private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
	{
		foreach (var property in element.EnumerateObject())
		{
			if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static string GetString(JsonElement element, string propertyName, string fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static int GetInt(JsonElement element, string propertyName, int fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var parsed)
			? parsed
			: fallback;
	}

	private static float GetFloat(JsonElement element, string propertyName, float fallback)
	{
		if (!TryGetProperty(element, propertyName, out var value))
		{
			return fallback;
		}

		if (value.TryGetSingle(out var parsedSingle))
		{
			return parsedSingle;
		}

		return value.TryGetDouble(out var parsedDouble)
			? (float)parsedDouble
			: fallback;
	}

	private static bool GetBool(JsonElement element, string propertyName, bool fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}
}
