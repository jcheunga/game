using System.Collections.Generic;

public sealed class ChallengeSyncBatchEnvelope
{
	public string BatchId { get; set; } = "";
	public string ProviderId { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string ClientLabel { get; set; } = "";
	public int SaveDataVersion { get; set; }
	public long SubmittedAtUnixSeconds { get; set; }
	public int SubmissionCount { get; set; }
	public string[] BoardCodes { get; set; } = [];
	public List<ChallengeSubmissionEnvelope> Submissions { get; set; } = [];
}

public sealed class ChallengeSyncBatchResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string BatchId { get; set; } = "";
	public string RemoteStatus { get; set; } = "";
	public string ProviderSummary { get; set; } = "";
	public string[] AcceptedSubmissionIds { get; set; } = [];
	public string[] RejectedSubmissionIds { get; set; } = [];
}

public interface IChallengeSyncProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	ChallengeSyncBatchResult SubmitBatch(ChallengeSyncBatchEnvelope batch);
}
