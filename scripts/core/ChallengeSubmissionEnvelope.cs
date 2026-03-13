using System.Collections.Generic;

public sealed class ChallengeSubmissionEnvelope
{
	public string SubmissionId { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string Code { get; set; } = "";
	public int Stage { get; set; } = 1;
	public string MutatorId { get; set; } = AsyncChallengeCatalog.PressureSpikeId;
	public int Score { get; set; }
	public int RawScore { get; set; }
	public float ScoreMultiplier { get; set; } = 1f;
	public bool Won { get; set; }
	public bool Retreated { get; set; }
	public float ElapsedSeconds { get; set; }
	public int EnemyDefeats { get; set; }
	public int StarsEarned { get; set; }
	public bool UsedLockedDeck { get; set; }
	public string[] DeckUnitIds { get; set; } = [];
	public int PlayerDeployments { get; set; }
	public int HullPercent { get; set; }
	public long QueuedAtUnixSeconds { get; set; }
	public int UploadAttempts { get; set; }
	public long LastUploadAttemptUnixSeconds { get; set; }
	public List<ChallengeDeploymentRecord> Deployments { get; set; } = [];
}
