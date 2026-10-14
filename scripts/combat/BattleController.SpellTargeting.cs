using System.Linq;
using Godot;

public partial class BattleController
{
    private bool CanResolveSpellTarget(ResolvedSpellStats spell, Vector2 point, out string reason)
    {
        reason = "";
        var effect = spell.EffectType;
        if (effect is "aegis_bolt" or "gale_step" or "verdant_renewal")
        {
            if (ContentSpellTarget(Team.Player, point) == null) reason = "Aim at a living ally.";
        }
        else if (effect is "radiant_rebuke" or "anchor_hex" or "molten_brand" or "chain_lightning" or "banish")
        {
            if (ContentSpellTarget(Team.Enemy, point) == null) reason = "Aim at a living enemy.";
        }
        else if (effect is "fireball" or "frost_burst" or "lightning_strike" or "earthquake" or "polymorph"
            or "tidal_push" or "withering_seal" or "bramble_snare" or "royal_edict" or "tactic_war_horn")
        {
            if (!NearbyContentUnits(Team.Enemy, point, spell.Radius).Any(u => !u.IsUntargetable)) reason = "No enemy in range.";
        }
        else if (effect is "heal" or "barrier_ward" or "purifying_light" or "rallying_advance" or "mirror_ward")
        {
            var allies = NearbyContentUnits(Team.Player, point, spell.Radius).Where(u => !u.IsTacticalObject && !u.IsUntargetable);
            if (effect == "heal" ? !allies.Any(u => u.Health < u.MaxHealth) : !allies.Any())
                reason = effect == "heal" ? "Aim at an injured ally." : "No ally in range.";
        }
        else if (effect == "war_cry" && !HasLivingTroop(Team.Player)) reason = "Deploy an ally before rallying.";
        else if (effect == "soul_harvest" && !_contentCorpses.Any(c => c.Team == Team.Enemy && c.Expires > _elapsed && c.Position.DistanceTo(point) < spell.Radius))
            reason = "Aim at fallen enemies.";
        else if (effect == "resurrect")
        {
            if (string.IsNullOrEmpty(_lastDeadPlayerUnitId) || !_deck.Roster.Any(u => u.Id == _lastDeadPlayerUnitId) || !GameState.Instance.IsUnitOwned(_lastDeadPlayerUnitId))
                reason = "No fallen squad member to restore.";
            else if (CountTeamUnits(Team.Player) >= 32) reason = "The battlefield is full.";
        }
        else if (effect == "tactic_repair_cache" && point.DistanceTo(PlayerBaseCorePosition) >= 60)
            reason = "Place the repair cache beside your wagon.";

        if (reason.Length == 0 && spell.Definition.CardKind == "tactical" &&
            effect is not ("tactic_smoke_pot" or "tactic_war_horn") && CountTeamUnits(Team.Player) >= 32)
            reason = "The battlefield is full.";
        return reason.Length == 0;
    }

    private bool HasLivingTroop(Team team) => _units.Any(u => !u.IsDead && u.Team == team && !u.IsTacticalObject && !u.Definition.NonCombatant);

}
