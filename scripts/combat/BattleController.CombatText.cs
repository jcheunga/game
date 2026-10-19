using System.Collections.Generic;
using Godot;

// Combat text is numbers (damage, healing, repairs, courage) and a few words (Dodge, Parry, Braced); names and
// callouts stay off the field. Colour says who was hurt, never who struck, and quick hits on one target add into
// one tally so a crowded melee shows a few clear numbers instead of a heap of small ones.
public partial class BattleController
{
    private const int MaxCombatTexts = 36;
    // Stand-in tally keys for the two bases; unit keys are instance ids, which never come this low.
    private const ulong PlayerBaseTextKey = 1, EnemyBaseTextKey = 2;
    private readonly Dictionary<(ulong, CombatTextKind), BattleFloatText> _combatTallies = new();
    private readonly List<BattleFloatText> _combatTexts = new();
    private readonly RandomNumberGenerator _combatTextRng = new();
    private (ulong Key, BattleFloatText Text) _lastCombatTally;

    private void SpawnDamageFeedback(Unit target, float damage, bool critical = false)
    {
        if (damage <= 0.05f || !IsInstanceValid(target)) return;
        var kind = target.Team == Team.Player ? CombatTextKind.Hurt : CombatTextKind.Damage;
        // A big bite out of a body reads a little larger.
        SpawnCombatTally(target.GetInstanceId(), target.CombatTextPoint, damage, kind, critical, damage / Mathf.Max(1f, target.MaxHealth) * 3f);
    }

    private void SpawnHealFeedback(Unit target, float healed)
    {
        if (healed <= 0.05f || !IsInstanceValid(target)) return;
        SpawnCombatTally(target.GetInstanceId(), target.CombatTextPoint, healed, CombatTextKind.Heal);
    }

    // Damage to (or repairs on) the wagon or the enemy gate, over its core.
    private void SpawnBaseFeedback(bool playerBase, float amount, bool repair = false)
    {
        if (amount <= 0.05f) return;
        var kind = repair ? CombatTextKind.Heal : playerBase ? CombatTextKind.Hurt : CombatTextKind.Damage;
        var core = playerBase ? PlayerBaseCorePosition : EnemyBaseCorePosition;
        SpawnCombatTally(playerBase ? PlayerBaseTextKey : EnemyBaseTextKey, core + new Vector2(0f, -34f), amount, kind);
    }

    private void SpawnCourageFeedback(Vector2 at, float amount)
    {
        if (amount <= 0.05f) return;
        NewCombatText(at + new Vector2(0f, -28f)).SetupAmount(amount, CombatTextKind.Courage, drift: CombatTextDrift(), reducedMotion: IsReducedMotionEnabled());
    }

    private void SpawnCombatCallout(Unit unit, string word)
    {
        if (!IsInstanceValid(unit)) return;
        NewCombatText(unit.CombatTextPoint + new Vector2(0f, -22f)).Setup(word, drift: CombatTextDrift() * .5f, reducedMotion: IsReducedMotionEnabled());
    }

    // A critical hit rolled outside ContentDamage upgrades the number that hit just raised.
    private void MarkCriticalHit(Unit target)
    {
        if (IsInstanceValid(target) && _lastCombatTally.Key == target.GetInstanceId() && IsInstanceValid(_lastCombatTally.Text))
            _lastCombatTally.Text.MakeCritical();
    }

    private void SpawnCombatTally(ulong key, Vector2 at, float amount, CombatTextKind kind, bool critical = false, float emphasis = 0f)
    {
        if (!critical && _combatTallies.TryGetValue((key, kind), out var tally) && IsInstanceValid(tally) && !tally.IsQueuedForDeletion() && tally.CanMerge(kind))
        {
            tally.AddAmount(amount);
        }
        else
        {
            tally = NewCombatText(at);
            tally.SetupAmount(amount, kind, critical, emphasis, CombatTextDrift(), IsReducedMotionEnabled());
            _combatTallies[(key, kind)] = tally;
        }
        _lastCombatTally = (key, tally);
    }

    private BattleFloatText NewCombatText(Vector2 at)
    {
        _combatTexts.RemoveAll(x => !IsInstanceValid(x) || x.IsQueuedForDeletion());
        // A crowded field hurries its oldest text off rather than stacking more.
        for (var i = 0; i <= _combatTexts.Count - MaxCombatTexts; i++) _combatTexts[i].Retire();
        if (_combatTallies.Count > 96)
        {
            var stale = new List<(ulong, CombatTextKind)>();
            foreach (var (key, text) in _combatTallies) if (!IsInstanceValid(text)) stale.Add(key);
            foreach (var key in stale) _combatTallies.Remove(key);
        }
        var label = new BattleFloatText { Position = at };
        // Keep combat text one size on screen while the camera zooms.
        if (_battleCamera != null) label.PresentationScale = () => 1.2f / _battleCamera.Zoom.X;
        AddChild(label);
        _combatTexts.Add(label);
        return label;
    }

    // Cosmetic sideways drift, drawn from its own generator so text never shifts the seeded battle.
    private Vector2 CombatTextDrift() => new(_combatTextRng.RandfRange(-9f, 9f), 0f);
}
