using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class BattleDeckState
{
    private readonly List<UnitDefinition> _roster = new();
    private readonly Dictionary<string, float> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<UnitDefinition> Roster => _roster;
    public UnitDefinition ArmedUnit { get; private set; } = null!;
    public bool HasArmedUnit => ArmedUnit != null;

    public void Initialize(IEnumerable<UnitDefinition> roster)
    {
        _roster.Clear();
        _roster.AddRange(roster);

        _cooldowns.Clear();
        foreach (var unit in _roster)
        {
            _cooldowns[unit.Id] = 0f;
        }

        ArmedUnit = null!;
    }

    public void TickCooldowns(float delta)
    {
        for (var i = 0; i < _roster.Count; i++)
        {
            var unit = _roster[i];
            var cooldown = GetCooldownRemaining(unit.Id);
            if (cooldown <= 0f)
            {
                continue;
            }

            _cooldowns[unit.Id] = Mathf.Max(0f, cooldown - delta);
        }
    }

    public float GetCooldownRemaining(string unitId)
    {
        return _cooldowns.TryGetValue(unitId, out var cooldown)
            ? Mathf.Max(0f, cooldown)
            : 0f;
    }

    public void Arm(UnitDefinition definition)
    {
        ArmedUnit = _roster.FirstOrDefault(unit => unit.Id == definition.Id)!;
    }

    public void Disarm()
    {
        ArmedUnit = null!;
    }

    public bool CanDeploy(UnitDefinition definition, float courage, bool battleEnded, out string reason)
    {
        reason = "";
        if (battleEnded)
        {
            reason = "Battle is already over.";
            return false;
        }

        if (!_roster.Any(unit => unit.Id == definition.Id))
        {
            reason = "This unit is not in your warband.";
            return false;
        }

        var cooldown = GetCooldownRemaining(definition.Id);
        if (cooldown > 0.05f)
        {
            reason = $"{definition.DisplayName} is still recovering ({cooldown:0.0}s).";
            return false;
        }

        if (courage < definition.Cost)
        {
            reason = $"Not enough courage for {definition.DisplayName}.";
            return false;
        }

        return true;
    }

    public void MarkDeployed(UnitDefinition definition, float cooldownDuration = -1f)
    {
        var appliedCooldown = cooldownDuration >= 0f
            ? cooldownDuration
            : definition.DeployCooldown;
        _cooldowns[definition.Id] = Mathf.Max(0f, appliedCooldown);
        Disarm();
    }

    public void ReduceCooldowns(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        for (var i = 0; i < _roster.Count; i++)
        {
            var unit = _roster[i];
            _cooldowns[unit.Id] = Mathf.Max(0f, GetCooldownRemaining(unit.Id) - amount);
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
            var unit = _roster[i];
            _cooldowns[unit.Id] = GetCooldownRemaining(unit.Id) + amount;
        }
    }

}
