using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class BattleSpellState
{
    private readonly List<SpellDefinition> _roster = new();
    private readonly Dictionary<string, float> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SpellDefinition> Roster => _roster;
    public SpellDefinition ArmedSpell { get; private set; } = null!;
    public bool HasArmedSpell => ArmedSpell != null;

    public void Initialize(IEnumerable<SpellDefinition> roster)
    {
        _roster.Clear();
        _roster.AddRange(roster ?? Array.Empty<SpellDefinition>());

        _cooldowns.Clear();
        foreach (var spell in _roster)
        {
            _cooldowns[spell.Id] = 0f;
        }

        ArmedSpell = _roster.FirstOrDefault()!;
    }

    public void TickCooldowns(float delta)
    {
        for (var i = 0; i < _roster.Count; i++)
        {
            var spell = _roster[i];
            var cooldown = GetCooldownRemaining(spell.Id);
            if (cooldown <= 0f)
            {
                continue;
            }

            _cooldowns[spell.Id] = Mathf.Max(0f, cooldown - delta);
        }
    }

    public float GetCooldownRemaining(string spellId)
    {
        return _cooldowns.TryGetValue(spellId, out var cooldown)
            ? Mathf.Max(0f, cooldown)
            : 0f;
    }

    public void Arm(SpellDefinition definition)
    {
        ArmedSpell = definition;
    }

    public bool CanCast(SpellDefinition definition, float courage, bool battleEnded, bool checkpointActive, out string reason)
    {
        reason = "";
        if (battleEnded)
        {
            reason = "Battle is already over.";
            return false;
        }

        if (checkpointActive)
        {
            reason = "Checkpoint draft is active.";
            return false;
        }

        var cooldown = GetCooldownRemaining(definition.Id);
        if (cooldown > 0.05f)
        {
            reason = $"{definition.DisplayName} is still recovering ({cooldown:0.0}s).";
            return false;
        }

        if (courage < definition.CourageCost)
        {
            reason = $"Not enough courage for {definition.DisplayName}.";
            return false;
        }

        return true;
    }

    public void MarkCast(SpellDefinition definition, float cooldownDuration = -1f)
    {
        var appliedCooldown = cooldownDuration >= 0f
            ? cooldownDuration
            : definition.Cooldown;
        _cooldowns[definition.Id] = Mathf.Max(0f, appliedCooldown);
        AutoArmNextReadySpell(definition);
    }

    public void ReduceCooldowns(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        for (var i = 0; i < _roster.Count; i++)
        {
            var spell = _roster[i];
            _cooldowns[spell.Id] = Mathf.Max(0f, GetCooldownRemaining(spell.Id) - amount);
        }
    }

    public void IncreaseCooldowns(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        for (var i = 0; i < _roster.Count; i++)
        {
            var spell = _roster[i];
            _cooldowns[spell.Id] = GetCooldownRemaining(spell.Id) + amount;
        }
    }

    private void AutoArmNextReadySpell(SpellDefinition castSpell)
    {
        if (ArmedSpell != castSpell)
        {
            return;
        }

        for (var i = 0; i < _roster.Count; i++)
        {
            var spell = _roster[i];
            if (GetCooldownRemaining(spell.Id) > 0.05f)
            {
                continue;
            }

            ArmedSpell = spell;
            return;
        }
    }
}
