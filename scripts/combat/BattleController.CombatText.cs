using System.Collections.Generic;
using Godot;

// Combat text is numbers (damage, healing, repairs, courage) and a few words (Dodge, Parry, Braced); names and
// callouts stay off the field. Damage is red and critical hits yellow on either side, never the striker's tint, and
// quick hits on one target add into one tally so a crowded melee shows a few clear numbers instead of a heap.
public partial class BattleController
{
    private const int MaxCombatTexts = 36;
    // Stand-in tally keys for the two bases; unit keys are instance ids, which never come this low.
    private const ulong PlayerBaseTextKey = 1, EnemyBaseTextKey = 2;
    private readonly Dictionary<(ulong, CombatTextKind), BattleFloatText> _combatTallies = new();
    private readonly List<BattleFloatText> _combatTexts = new();
    private (ulong Key, BattleFloatText Text) _lastCombatTally;

    private void SpawnDamageFeedback(Unit target, float damage, bool critical = false)
    {
        if (damage <= 0.05f || !IsInstanceValid(target)) return;
        // A big bite out of a body reads a little larger.
        SpawnCombatTally(target.GetInstanceId(), target.CombatTextPoint, damage, CombatTextKind.Damage, critical,
            damage / Mathf.Max(1f, target.MaxHealth) * 3f);
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
        var kind = repair ? CombatTextKind.Heal : CombatTextKind.Damage;
        var core = playerBase ? PlayerBaseCorePosition : EnemyBaseCorePosition;
        SpawnCombatTally(playerBase ? PlayerBaseTextKey : EnemyBaseTextKey, core + new Vector2(0f, -34f), amount, kind);
    }

    private void SpawnCourageFeedback(Vector2 at, float amount)
    {
        if (amount <= 0.05f) return;
        var label = NewCombatText(at + new Vector2(0f, -28f));
        label.SetupAmount(amount, CombatTextKind.Courage, reducedMotion: IsReducedMotionEnabled());
        PlaceCombatText(label);
    }

    private void SpawnCombatCallout(Unit unit, string word)
    {
        if (!IsInstanceValid(unit)) return;
        var label = NewCombatText(unit.CombatTextPoint + new Vector2(0f, -22f));
        label.Setup(word, reducedMotion: IsReducedMotionEnabled());
        PlaceCombatText(label);
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
            tally.SetupAmount(amount, kind, critical, emphasis, IsReducedMotionEnabled());
            PlaceCombatText(tally);
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

    // A new label that would print over a fresh one steps up just above it (a few times at most), so a crowded
    // melee shows a neat column instead of a heap; fading labels it would cover clear away at once. Labels rise
    // at one pace, so ones placed apart stay apart.
    private void PlaceCombatText(BattleFloatText label)
    {
        var scale = _battleCamera != null ? 1.2f / _battleCamera.Zoom.X : 1f;
        bool Overlaps(BattleFloatText other, Vector2 at) => other != label && IsInstanceValid(other) && !other.IsQueuedForDeletion()
            && Mathf.Abs(other.Position.X - at.X) < (other.HalfExtent.X + label.HalfExtent.X) * scale
            && Mathf.Abs(other.Position.Y - at.Y) < (other.HalfExtent.Y + label.HalfExtent.Y) * scale;
        var spot = label.Position;
        for (var step = 0; step < 4; step++)
        {
            var blocker = _combatTexts.Find(other => other.Fresh && Overlaps(other, spot));
            if (blocker == null) break;
            spot.Y = blocker.Position.Y - (blocker.HalfExtent.Y + label.HalfExtent.Y + 1f) * scale;
        }
        foreach (var other in _combatTexts)
            if (!other.Fresh && Overlaps(other, spot)) other.Retire();
        label.MoveOrigin(spot);
    }
}
