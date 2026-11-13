using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Telegraphed ground strikes: a ring is drawn on the ground, and when its warning runs out everything standing in it is
/// hit. Stage hazards (StageDefinition.Hazards) and the commanders' signature moves (UnitDefinition.SignatureAbilityId)
/// are both built on them, so the player reads every one the same way: see the ring, step out or brace.
/// The timing runs on the battle clock, not on effect nodes, so headless simulations play the same battle.
/// </summary>
public partial class BattleController
{
    private sealed class GroundStrike
    {
        public Vector2 At;
        public float Radius, Time;
        public Color Tint;
        public Team? Hits;
        public Action<Unit> OnHit;
    }

    private readonly List<GroundStrike> _groundStrikes = new();
    private readonly Dictionary<Unit, int> _commanderSpecials = new();
    private float[] _hazardNext = Array.Empty<float>();
    private int[] _hazardCount = Array.Empty<int>();
    private int _playerHazardHits;

    private void ScheduleGroundStrike(Vector2 at, float radius, float warning, Color tint, Team? hits, Action<Unit> onHit)
    {
        _groundStrikes.Add(new GroundStrike { At = at, Radius = radius, Time = warning, Tint = tint, Hits = hits, OnHit = onHit });
        SpellVfx.Sigil(this, at, "war", tint, radius, warning + .35f, .3f, flareAt: warning);
    }

    private void UpdateGroundStrikes(float delta)
    {
        UpdateStageHazards();
        for (var i = _groundStrikes.Count - 1; i >= 0; i--)
        {
            var strike = _groundStrikes[i];
            strike.Time -= delta;
            if (strike.Time > 0f) continue;
            _groundStrikes.RemoveAt(i);
            SpellVfx.Shockwave(this, strike.At, strike.Tint, strike.Radius, .4f);
            SpellVfx.Flash(this, strike.At, strike.Tint, strike.Radius * .8f, .3f, ground: true);
            foreach (var unit in _units.ToArray())
            {
                if (unit.IsDead || (strike.Hits.HasValue && unit.Team != strike.Hits.Value)) continue;
                if (unit.Position.DistanceTo(strike.At) <= strike.Radius + unit.Radius * .5f) strike.OnHit(unit);
            }
        }
    }

    // ---- Stage hazards ----

    /// <summary>
    /// A hazard strikes its spot every Interval seconds from StartTime, after WarningDuration of warning. It takes
    /// Damage percent of maximum health from every troop and foe in the ring (a quarter of that from commanders), so
    /// it can be used as well as avoided.
    /// </summary>
    private void UpdateStageHazards()
    {
        var hazards = IsCampaignMode || IsTowerMode ? _stageData?.Hazards : null;
        if (hazards == null || hazards.Length == 0 || _battleEnded) return;
        if (_hazardNext.Length != hazards.Length)
        {
            _hazardNext = hazards.Select(h => Mathf.Max(1f, h.StartTime)).ToArray();
            _hazardCount = new int[hazards.Length];
        }
        for (var i = 0; i < hazards.Length; i++)
        {
            var hazard = hazards[i];
            if (hazard == null || _elapsed < _hazardNext[i]) continue;
            _hazardNext[i] += Mathf.Max(3f, hazard.Interval);
            // The spot drifts along the road from strike to strike, so no one position is always safe or always hit.
            var drift = .09f * Mathf.Sin(_hazardCount[i]++ * 2.4f + i);
            var at = FieldPoint(Mathf.Clamp(hazard.XRatio + drift, .12f, .88f), Mathf.Clamp(hazard.YRatio, 0f, 1f));
            var share = Mathf.Max(0f, hazard.Damage) / 100f;
            ScheduleGroundStrike(at, Mathf.Max(24f, hazard.Radius), Mathf.Max(.8f, hazard.WarningDuration), hazard.GetTint(), null, unit =>
            {
                unit.TakeDamage(unit.MaxHealth * share * (unit.Definition?.IsBoss == true ? .25f : 1f), hazard.Label, CombatDamageType.True);
                if (unit.Team == Team.Player) _playerHazardHits++;
            });
        }
    }

    // ---- Commander signatures ----

    private static readonly Dictionary<string, string> SignatureNames = new()
    {
        ["undertow"] = "Undertow", ["barrage"] = "Tide barrage", ["iron_skin"] = "Iron skin", ["plague_breath"] = "Plague breath",
        ["pestilence"] = "Pestilence", ["ground_slam"] = "Ground slam", ["dark_mass"] = "Dark mass", ["bone_ward"] = "Bone ward",
        ["quake"] = "Quake", ["blood_frenzy"] = "Blood frenzy", ["slumber"] = "Slumber", ["dread"] = "Dread", ["ashfall"] = "Ashfall",
    };

    /// <summary>The display name of a commander's signature move, or "" when it has none.</summary>
    public static string SignatureName(string id) => SignatureNames.GetValueOrDefault(id ?? "", "");

    /// <summary>
    /// A commander's own move, used with every other one of its rally, jam or raise calls, starting with the first.
    /// Damage is a share of the commander's attack, so it follows the stage's strength and level.
    /// </summary>
    private void TriggerCommanderSignature(Unit boss)
    {
        var id = boss.Definition?.SignatureAbilityId ?? "";
        if (id.Length == 0 || boss.IsDead) return;
        var count = _commanderSpecials[boss] = _commanderSpecials.GetValueOrDefault(boss) + 1;
        if (count % 2 == 0) return;

        var troops = _units.Where(u => !u.IsDead && u.Team == Team.Player).ToList();
        var allies = _units.Where(u => !u.IsDead && u.Team == Team.Enemy && u != boss && u.Position.DistanceTo(boss.Position) <= 115f).ToList();
        var front = troops.OrderByDescending(u => u.Position.X).ToList();
        var power = boss.AttackDamage;
        var name = boss.UnitName;
        const float Warning = 1f;

        switch (id)
        {
            case "undertow":
                foreach (var target in front.Take(1))
                    ScheduleGroundStrike(target.Position, 58f, Warning, new Color("4aa8d8"), Team.Player, unit =>
                    {
                        unit.TakeDamage(power * .5f, name, CombatDamageType.Magical);
                        unit.Root(1.6f);
                    });
                break;
            case "barrage":
                foreach (var target in front.Take(3))
                    ScheduleGroundStrike(target.Position, 30f, Warning, new Color("5ac8e8"), Team.Player,
                        unit => unit.TakeDamage(power * .9f, name, CombatDamageType.Physical));
                break;
            case "iron_skin":
                boss.GiveShield(boss.MaxHealth * .15f, 6f);
                SpellVfx.Shockwave(this, boss.Position, new Color("c8d0d8"), 44f, .5f);
                break;
            case "plague_breath":
                ScheduleGroundStrike(boss.Position + new Vector2(-46f, 0f), 78f, Warning, new Color("8ac84a"), Team.Player,
                    unit => unit.ApplyBurn(power * .18f, 5f, name));
                break;
            case "pestilence":
                foreach (var target in front.Take(1).Concat(front.Skip(front.Count / 2).Take(1)).Distinct())
                    ScheduleGroundStrike(target.Position, 46f, Warning, new Color("a0d040"), Team.Player, unit =>
                    {
                        unit.ApplyBurn(power * .15f, 5f, name);
                        unit.ApplyHealingPenalty(.5f, 6f);
                    });
                break;
            case "ground_slam":
                ScheduleGroundStrike(boss.Position, 74f, Warning, new Color("d8a060"), Team.Player, unit =>
                {
                    unit.TakeDamage(power, name, CombatDamageType.Physical);
                    unit.Root(1.2f);
                });
                break;
            case "dark_mass":
                foreach (var ally in allies)
                {
                    ally.Heal(ally.MaxHealth * .15f);
                    SpellVfx.Flash(this, ally.BodyContactPosition, new Color("b890ff"), 12f, .4f);
                }
                break;
            case "bone_ward":
                foreach (var ally in allies)
                {
                    ally.GiveShield(ally.MaxHealth * .2f, 6f);
                    SpellVfx.Flash(this, ally.BodyContactPosition, new Color("e8e0c8"), 12f, .4f);
                }
                break;
            case "quake":
                ScheduleGroundStrike(boss.Position, 104f, Warning + .2f, new Color("7a9a50"), Team.Player, unit =>
                {
                    unit.TakeDamage(power * .7f, name, CombatDamageType.Physical);
                    unit.ReactToContact(-1f, power * .7f, 2f);
                });
                break;
            case "blood_frenzy":
                boss.ApplyTemporaryCombatBuff(1.2f, 1.25f, 5f);
                boss.ApplyTemporaryDefenseModifier(1.2f, 5f);
                SpellVfx.Shockwave(this, boss.Position, new Color("e84a3a"), 48f, .5f);
                break;
            case "slumber":
                foreach (var target in troops.OrderByDescending(u => u.AttackDamage).Take(2))
                    ScheduleGroundStrike(target.Position, 26f, Warning, new Color("9a7ae8"), Team.Player, unit => unit.Sleep(2.5f));
                break;
            case "dread":
                foreach (var target in troops.Where(u => u.Position.DistanceTo(boss.Position) <= 125f))
                {
                    target.ApplyTemporaryCombatBuff(.7f, .85f, 5f);
                    SpellVfx.Flash(this, target.BodyContactPosition, new Color("6a4a8a"), 12f, .4f);
                }
                SpellVfx.Shockwave(this, boss.Position, new Color("6a4a8a"), 125f, .6f);
                break;
            case "ashfall":
                foreach (var target in front.Where((_, i) => i % 2 == 0).Take(4))
                    ScheduleGroundStrike(target.Position, 34f, Warning, new Color("ff6a2a"), Team.Player, unit =>
                    {
                        unit.TakeDamage(power * .6f, name, CombatDamageType.Magical);
                        unit.ApplyBurn(power * .12f, 4f, name);
                    });
                break;
            default:
                return;
        }
        SetStatus($"{name}: {SignatureName(id)}!");
    }
}
