using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public sealed class LocalJournalChallengeSyncProvider : IChallengeSyncProvider
{
	public const string BatchJournalPath = "user://challenge_sync_batches.jsonl";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false
	};

	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Journal Stub";

	public string BuildLocationSummary()
	{
		var localizedJournalPath = ProjectSettings.LocalizePath(ProjectSettings.GlobalizePath(BatchJournalPath));
		return $"Journal: {localizedJournalPath}";
	}

	public ChallengeSyncBatchResult SubmitBatch(ChallengeSyncBatchEnvelope batch)
	{
		var globalPath = ProjectSettings.GlobalizePath(BatchJournalPath);
		var directoryPath = Path.GetDirectoryName(globalPath);
		if (!string.IsNullOrWhiteSpace(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}

		using (var stream = new StreamWriter(globalPath, append: true))
		{
			stream.WriteLine(JsonSerializer.Serialize(batch, JsonOptions));
		}

		return new ChallengeSyncBatchResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			BatchId = batch.BatchId,
			RemoteStatus = "buffered",
			ProviderSummary = $"Buffered batch {batch.BatchId} to the local journal.",
			AcceptedSubmissionIds = batch.Submissions.Select(entry => entry.SubmissionId).ToArray(),
			RejectedSubmissionIds = []
		};
	}
}
