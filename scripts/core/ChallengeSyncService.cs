using System;
using System.Linq;
using Godot;

public partial class ChallengeSyncService : Node
{
	public static ChallengeSyncService Instance { get; private set; }

	public string SyncModeLabel => ResolveProvider().DisplayName;
	public string SyncStatus { get; private set; } = "Challenge sync idle. No packets flushed yet.";

	private readonly IChallengeSyncProvider _localProvider = new LocalJournalChallengeSyncProvider();
	private string _lastBatchSummary = "No sync batch flushed yet.";

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		RefreshStatusFromState();
	}

	public void RefreshStatusFromState()
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			SyncStatus = "Challenge sync unavailable. Game state autoload is missing.";
			return;
		}

		var lastSync = gameState.LastChallengeSyncAtUnixSeconds > 0
			? DateTimeOffset.FromUnixTimeSeconds(gameState.LastChallengeSyncAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm")
			: "never";
		var provider = ResolveProvider();
		SyncStatus =
			$"Idle  |  {provider.DisplayName}  |  Auto {(gameState.ChallengeSyncAutoFlush ? "on" : "off")}  |  Pending {gameState.PendingChallengeSubmissionCount}  |  Synced {gameState.TotalChallengeSubmissionsSynced}  |  Last sync {lastSync}";
	}

	public string BuildStatusSummary()
	{
		var provider = ResolveProvider();
		return
			$"Sync service: {SyncModeLabel}\n" +
			$"Status: {SyncStatus}\n" +
			$"Last batch: {_lastBatchSummary}\n" +
			$"{provider.BuildLocationSummary()}";
	}

	public bool FlushPendingSubmissions(out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			SyncStatus = "Challenge sync unavailable. Game state autoload is missing.";
			message = SyncStatus;
			return false;
		}

		if (gameState.PendingChallengeSubmissionCount <= 0)
		{
			RefreshStatusFromState();
			message = "Challenge outbox is empty.";
			return false;
		}

		var pending = gameState.GetPendingChallengeSubmissions(gameState.PendingChallengeSubmissionCount).ToArray();
		if (pending.Length == 0)
		{
			RefreshStatusFromState();
			message = "Challenge outbox is empty.";
			return false;
		}

		var attemptedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var submissionIds = pending.Select(entry => entry.SubmissionId).ToArray();
		gameState.RecordChallengeSubmissionAttempt(submissionIds, attemptedAtUnixSeconds);
		var provider = ResolveProvider();

		try
		{
			var batch = BuildBatchEnvelope(gameState, pending, attemptedAtUnixSeconds);
			var result = provider.SubmitBatch(batch);
			var acceptedIds = result.AcceptedSubmissionIds ?? [];
			var rejectedIds = result.RejectedSubmissionIds ?? [];
			var flushed = gameState.CompleteChallengeSubmissions(acceptedIds, attemptedAtUnixSeconds);
			RefreshStatusFromState();
			_lastBatchSummary =
				$"{result.BatchId}  |  {result.RemoteStatus}  |  accepted {flushed}" +
				(rejectedIds.Length > 0 ? $"  |  rejected {rejectedIds.Length}" : "");
			message = $"Flushed {flushed} challenge packet{(flushed == 1 ? "" : "s")} via {provider.DisplayName}." +
				(rejectedIds.Length > 0 ? $" {rejectedIds.Length} packet{(rejectedIds.Length == 1 ? "" : "s")} stayed queued." : "");
			return true;
		}
		catch (Exception ex)
		{
			SyncStatus = $"Flush failed  |  {ex.Message}";
			_lastBatchSummary = "Last batch failed before provider acceptance.";
			message = $"Challenge outbox flush failed: {ex.Message}";
			return false;
		}
	}

	public bool TryAutoFlushPending()
	{
		var gameState = GameState.Instance;
		if (gameState == null || !gameState.ChallengeSyncAutoFlush || gameState.PendingChallengeSubmissionCount <= 0)
		{
			return false;
		}

		return FlushPendingSubmissions(out _);
	}

	private ChallengeSyncBatchEnvelope BuildBatchEnvelope(GameState gameState, ChallengeSubmissionEnvelope[] pending, long submittedAtUnixSeconds)
	{
		var provider = ResolveProvider();
		var clientLabel = ProjectSettings.GetSetting("application/config/name").AsString();
		if (string.IsNullOrWhiteSpace(clientLabel))
		{
			clientLabel = "Game";
		}

		return new ChallengeSyncBatchEnvelope
		{
			BatchId = $"SYNC-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
			ProviderId = provider.Id,
			PlayerProfileId = gameState.PlayerProfileId,
			PlayerCallsign = gameState.PlayerCallsign,
			ClientLabel = clientLabel,
			SaveDataVersion = new GameSaveData().Version,
			SubmittedAtUnixSeconds = submittedAtUnixSeconds,
			SubmissionCount = pending.Length,
			BoardCodes = pending
				.Select(entry => entry.Code)
				.Where(code => !string.IsNullOrWhiteSpace(code))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray(),
			Submissions = pending
				.Select(CloneSubmission)
				.ToList()
		};
	}

	private IChallengeSyncProvider ResolveProvider()
	{
		var selectedProviderId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return selectedProviderId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiChallengeSyncProvider(GameState.Instance?.ChallengeSyncEndpoint ?? "")
			: _localProvider;
	}

	private static ChallengeSubmissionEnvelope CloneSubmission(ChallengeSubmissionEnvelope entry)
	{
		return new ChallengeSubmissionEnvelope
		{
			SubmissionId = entry.SubmissionId,
			PlayerProfileId = entry.PlayerProfileId,
			PlayerCallsign = entry.PlayerCallsign,
			Code = entry.Code,
			Stage = entry.Stage,
			MutatorId = entry.MutatorId,
			Score = entry.Score,
			RawScore = entry.RawScore,
			ScoreMultiplier = entry.ScoreMultiplier,
			Won = entry.Won,
			Retreated = entry.Retreated,
			ElapsedSeconds = entry.ElapsedSeconds,
			EnemyDefeats = entry.EnemyDefeats,
			StarsEarned = entry.StarsEarned,
			UsedLockedDeck = entry.UsedLockedDeck,
			DeckUnitIds = entry.DeckUnitIds?.ToArray() ?? [],
			PlayerDeployments = entry.PlayerDeployments,
			HullPercent = entry.HullPercent,
			QueuedAtUnixSeconds = entry.QueuedAtUnixSeconds,
			UploadAttempts = entry.UploadAttempts,
			LastUploadAttemptUnixSeconds = entry.LastUploadAttemptUnixSeconds,
			Deployments = entry.Deployments?
				.Select(deployment => new ChallengeDeploymentRecord
				{
					UnitId = deployment.UnitId,
					TimeSeconds = deployment.TimeSeconds,
					LanePercent = deployment.LanePercent
				})
				.ToList() ?? []
		};
	}
}
