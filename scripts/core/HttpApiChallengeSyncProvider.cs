using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiChallengeSyncProvider : IChallengeSyncProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _endpointUrl;

	public HttpApiChallengeSyncProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP API";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public ChallengeSyncBatchResult SubmitBatch(ChallengeSyncBatchEnvelope batch)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP sync endpoint is not configured.");
		}

		var requestBody = new ChallengeSyncApiRequest
		{
			Batch = batch
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		request.Headers.TryAddWithoutValidation("X-Convoy-Profile", batch.PlayerProfileId);
		request.Headers.TryAddWithoutValidation("X-Convoy-Client", batch.ClientLabel);
		request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(request);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var parsed = string.IsNullOrWhiteSpace(responseBody)
			? null
			: JsonSerializer.Deserialize<ChallengeSyncApiResponse>(responseBody, JsonOptions);
		var acceptedIds = parsed?.AcceptedSubmissionIds != null && parsed.AcceptedSubmissionIds.Length > 0
			? parsed.AcceptedSubmissionIds
			: batch.Submissions.ConvertAll(entry => entry.SubmissionId).ToArray();
		var rejectedIds = parsed?.RejectedSubmissionIds ?? [];

		return new ChallengeSyncBatchResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			BatchId = string.IsNullOrWhiteSpace(parsed?.BatchId) ? batch.BatchId : parsed.BatchId,
			RemoteStatus = string.IsNullOrWhiteSpace(parsed?.Status) ? "accepted" : parsed.Status,
			ProviderSummary = string.IsNullOrWhiteSpace(parsed?.Message)
				? $"Posted batch {batch.BatchId} to {_endpointUrl}."
				: parsed.Message,
			AcceptedSubmissionIds = acceptedIds,
			RejectedSubmissionIds = rejectedIds
		};
	}
}
