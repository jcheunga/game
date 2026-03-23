using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SkillTreeNode
{
	public string Id { get; }
	public string UnitId { get; }
	public string Title { get; }
	public string Description { get; }
	public float HealthScale { get; }
	public float DamageScale { get; }
	public float SpeedScale { get; }
	public float CooldownReduction { get; }
	public string PrerequisiteNodeId { get; }
	public int GoldCost { get; }
	public int TomeCost { get; }

	public SkillTreeNode(string id, string unitId, string title, string description,
		float healthScale, float damageScale, float speedScale, float cooldownReduction,
		string prerequisiteNodeId, int goldCost, int tomeCost)
	{
		Id = id;
		UnitId = unitId;
		Title = title;
		Description = description;
		HealthScale = healthScale;
		DamageScale = damageScale;
		SpeedScale = speedScale;
		CooldownReduction = cooldownReduction;
		PrerequisiteNodeId = prerequisiteNodeId;
		GoldCost = goldCost;
		TomeCost = tomeCost;
	}
}

public sealed class UnitSkillTree
{
	public string UnitId { get; }
	public SkillTreeNode[] Nodes { get; }

	public UnitSkillTree(string unitId, SkillTreeNode[] nodes)
	{
		UnitId = unitId;
		Nodes = nodes;
	}
}

public readonly struct SkillTreeBonus
{
	public float HealthScale { get; }
	public float DamageScale { get; }
	public float SpeedScale { get; }
	public float CooldownReduction { get; }

	public SkillTreeBonus(float healthScale, float damageScale, float speedScale, float cooldownReduction)
	{
		HealthScale = healthScale;
		DamageScale = damageScale;
		SpeedScale = speedScale;
		CooldownReduction = cooldownReduction;
	}

	public static readonly SkillTreeBonus None = new(1f, 1f, 1f, 0f);
}

public static class UnitSkillTreeCatalog
{
	// Diamond tree: t1 (root) -> t2, t3 -> t4, t5
	// t2 requires t1, t3 requires t1, t4 requires t2, t5 requires t3

	private static UnitSkillTree MakeTree(string unitId, string theme,
		float h1, float d1, float s1, float c1,
		float h2, float d2, float s2, float c2,
		float h3, float d3, float s3, float c3,
		float h4, float d4, float s4, float c4,
		float h5, float d5, float s5, float c5)
	{
		var nodes = new[]
		{
			new SkillTreeNode($"{unitId}_t1", unitId, $"{theme} I", $"First {theme.ToLower()} talent.", h1, d1, s1, c1, null, 200, 1),
			new SkillTreeNode($"{unitId}_t2", unitId, $"{theme} II-A", $"Left branch {theme.ToLower()} talent.", h2, d2, s2, c2, $"{unitId}_t1", 400, 2),
			new SkillTreeNode($"{unitId}_t3", unitId, $"{theme} II-B", $"Right branch {theme.ToLower()} talent.", h3, d3, s3, c3, $"{unitId}_t1", 400, 2),
			new SkillTreeNode($"{unitId}_t4", unitId, $"{theme} III-A", $"Advanced left {theme.ToLower()} talent.", h4, d4, s4, c4, $"{unitId}_t2", 600, 3),
			new SkillTreeNode($"{unitId}_t5", unitId, $"{theme} III-B", $"Advanced right {theme.ToLower()} talent.", h5, d5, s5, c5, $"{unitId}_t3", 600, 3),
		};
		return new UnitSkillTree(unitId, nodes);
	}

	private static readonly UnitSkillTree[] Trees =
	{
		//                            unitId             theme         h1     d1     s1     c1     h2     d2     s2     c2     h3     d3     s3     c3     h4     d4     s4     c4     h5     d5     s5     c5
		MakeTree("player_brawler",    "Valor",          1.04f, 1.03f, 1.0f,  0f,    1.06f, 1.0f,  1.0f,  0f,    1.0f,  1.06f, 1.0f,  0f,    1.08f, 1.0f,  1.0f,  0f,    1.0f,  1.08f, 1.0f,  0f),
		MakeTree("player_shooter",    "Precision",      1.03f, 1.04f, 1.0f,  0f,    1.0f,  1.06f, 1.0f,  0f,    1.05f, 1.0f,  1.0f,  0.02f, 1.0f,  1.08f, 1.0f,  0f,    1.06f, 1.0f,  1.0f,  0.03f),
		MakeTree("player_defender",   "Fortitude",      1.06f, 1.0f,  1.0f,  0f,    1.08f, 1.0f,  1.0f,  0f,    1.04f, 1.04f, 1.0f,  0f,    1.10f, 1.0f,  1.0f,  0f,    1.06f, 1.06f, 1.0f,  0f),
		MakeTree("player_spear",      "Reach",          1.04f, 1.04f, 1.0f,  0f,    1.0f,  1.06f, 1.0f,  0f,    1.06f, 1.0f,  1.0f,  0f,    1.0f,  1.10f, 1.0f,  0f,    1.08f, 1.0f,  1.0f,  0f),
		MakeTree("player_ranger",     "Marksmanship",   1.03f, 1.05f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0f,    1.05f, 1.0f,  1.0f,  0.02f, 1.0f,  1.10f, 1.0f,  0f,    1.06f, 1.0f,  1.0f,  0.04f),
		MakeTree("player_raider",     "Charge",         1.04f, 1.03f, 1.02f, 0f,    1.0f,  1.06f, 1.04f, 0f,    1.06f, 1.0f,  1.02f, 0f,    1.0f,  1.08f, 1.06f, 0f,    1.08f, 1.0f,  1.04f, 0f),
		MakeTree("player_breacher",   "Impact",         1.04f, 1.05f, 1.0f,  0f,    1.06f, 1.04f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0f,    1.08f, 1.06f, 1.0f,  0f,    1.0f,  1.10f, 1.0f,  0f),
		MakeTree("player_coordinator","Devotion",       1.06f, 1.02f, 1.0f,  0f,    1.08f, 1.0f,  1.0f,  0.02f, 1.04f, 1.04f, 1.0f,  0f,    1.10f, 1.0f,  1.0f,  0.03f, 1.06f, 1.06f, 1.0f,  0f),
		MakeTree("player_marksman",   "Arcana",         1.03f, 1.06f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0.02f, 1.06f, 1.0f,  1.0f,  0f,    1.0f,  1.12f, 1.0f,  0.03f, 1.08f, 1.0f,  1.0f,  0f),
		MakeTree("player_mechanic",   "Engineering",    1.05f, 1.04f, 1.0f,  0f,    1.06f, 1.04f, 1.0f,  0f,    1.0f,  1.06f, 1.0f,  0.02f, 1.08f, 1.06f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0.03f),
		MakeTree("player_grenadier",  "Alchemy",        1.03f, 1.06f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0f,    1.05f, 1.04f, 1.0f,  0f,    1.0f,  1.12f, 1.0f,  0f,    1.06f, 1.06f, 1.0f,  0f),
		MakeTree("player_hound",      "Instinct",       1.05f, 1.03f, 1.03f, 0f,    1.06f, 1.0f,  1.04f, 0f,    1.0f,  1.06f, 1.04f, 0f,    1.08f, 1.0f,  1.06f, 0f,    1.0f,  1.08f, 1.06f, 0f),
		MakeTree("player_banner",     "Command",        1.06f, 1.03f, 1.0f,  0f,    1.08f, 1.0f,  1.0f,  0f,    1.04f, 1.06f, 1.0f,  0f,    1.10f, 1.0f,  1.0f,  0f,    1.06f, 1.08f, 1.0f,  0f),
		MakeTree("player_necromancer","Dark Arts",       1.04f, 1.06f, 1.0f,  0f,    1.0f,  1.08f, 1.0f,  0.02f, 1.06f, 1.04f, 1.0f,  0f,    1.0f,  1.12f, 1.0f,  0.03f, 1.08f, 1.06f, 1.0f,  0f),
		MakeTree("player_rogue",      "Shadow",         1.03f, 1.06f, 1.02f, 0f,    1.0f,  1.08f, 1.04f, 0f,    1.05f, 1.04f, 1.02f, 0f,    1.0f,  1.12f, 1.06f, 0f,    1.06f, 1.06f, 1.04f, 0f),
		MakeTree("player_berserker",  "Fury",           1.05f, 1.06f, 1.0f,  0f,    1.0f,  1.10f, 1.0f,  0f,    1.08f, 1.04f, 1.0f,  0f,    1.0f,  1.14f, 1.0f,  0f,    1.10f, 1.06f, 1.0f,  0f),
	};

	private static readonly Dictionary<string, UnitSkillTree> ByUnitId;

	static UnitSkillTreeCatalog()
	{
		ByUnitId = new Dictionary<string, UnitSkillTree>(StringComparer.OrdinalIgnoreCase);
		foreach (var tree in Trees)
		{
			ByUnitId[tree.UnitId] = tree;
		}
	}

	public static IReadOnlyList<UnitSkillTree> GetAll() => Trees;

	public static UnitSkillTree GetTree(string unitId)
	{
		return ByUnitId.TryGetValue(unitId, out var tree) ? tree : null;
	}

	public static SkillTreeNode GetNode(string unitId, string nodeId)
	{
		var tree = GetTree(unitId);
		return tree?.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
	}

	public static SkillTreeBonus Resolve(string unitId, IReadOnlyCollection<string> unlockedNodeIds)
	{
		var tree = GetTree(unitId);
		if (tree == null || unlockedNodeIds == null || unlockedNodeIds.Count == 0)
		{
			return SkillTreeBonus.None;
		}

		var h = 1f;
		var d = 1f;
		var s = 1f;
		var c = 0f;
		foreach (var node in tree.Nodes)
		{
			if (unlockedNodeIds.Contains(node.Id))
			{
				h *= node.HealthScale;
				d *= node.DamageScale;
				s *= node.SpeedScale;
				c += node.CooldownReduction;
			}
		}

		return new SkillTreeBonus(h, d, s, c);
	}
}
