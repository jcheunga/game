using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomReportProvider : IOnlineRoomReportProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _endpointUrl;

	public HttpApiOnlineRoomReportProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Report";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomReportResult SubmitReport(OnlineRoomJoinTicket ticket, OnlineRoomReportRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-report endpoint is not configured.");
		}

		var requestBody = new OnlineRoomReportApiRequest
		{
			Report = request
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", request.PlayerProfileId);
		message.Headers.TryAddWithoutValidation("X-Join-Ticket", request.TicketId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var parsed = string.IsNullOrWhiteSpace(responseBody)
			? null
			: JsonSerializer.Deserialize<OnlineRoomReportApiResponse>(responseBody, JsonOptions);
		return new OnlineRoomReportResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = string.IsNullOrWhiteSpace(parsed?.RoomId) ? ticket.RoomId : parsed.RoomId,
			BoardCode = string.IsNullOrWhiteSpace(parsed?.BoardCode) ? ticket.BoardCode : parsed.BoardCode,
			ReportId = parsed?.ReportId ?? "",
			SubjectType = string.IsNullOrWhiteSpace(parsed?.SubjectType) ? request.SubjectType : parsed.SubjectType,
			SubjectLabel = string.IsNullOrWhiteSpace(parsed?.SubjectLabel) ? request.SubjectLabel : parsed.SubjectLabel,
			ReasonId = string.IsNullOrWhiteSpace(parsed?.ReasonId) ? request.ReasonId : parsed.ReasonId,
			Status = string.IsNullOrWhiteSpace(parsed?.Status) ? "accepted" : parsed.Status,
			Summary = string.IsNullOrWhiteSpace(parsed?.Message)
				? $"Submitted moderation report for {request.SubjectLabel}."
				: parsed.Message,
			SubmittedAtUnixSeconds = parsed?.SubmittedAtUnixSeconds ?? request.RequestedAtUnixSeconds
		};
	}
}
