public sealed class ChallengeSyncApiRequest
{
	public ChallengeSyncBatchEnvelope Batch { get; set; } = new();
}

public sealed class ChallengeSyncApiResponse
{
	public string BatchId { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Message { get; set; } = "";
	public string[] AcceptedSubmissionIds { get; set; } = [];
	public string[] RejectedSubmissionIds { get; set; } = [];
}
