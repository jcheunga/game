using System.Collections.Generic;

public sealed class BattleSummaryData
{
	public bool Won { get; set; }
	public int StarsEarned { get; set; }
	public float ElapsedSeconds { get; set; }
	public int EnemiesDefeated { get; set; }
	public int BossesKilled { get; set; }
	public int UnitsDeployed { get; set; }
	public int UnitsLost { get; set; }
	public int SpellsCast { get; set; }
	public float TotalDamageDealt { get; set; }
	public float TotalDamageTaken { get; set; }
	public int GoldEarned { get; set; }
	public int FoodEarned { get; set; }
	public int SeasonXPEarned { get; set; }
	public Dictionary<string, int> MasteryXPPerUnit { get; set; } = new();
	public string BattleMode { get; set; } = "";
	public int Stage { get; set; }
	public float MutatorGoldMultiplier { get; set; } = 1f;

	public static BattleSummaryData Current { get; set; }
}
