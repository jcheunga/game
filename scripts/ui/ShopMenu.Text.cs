using System.Linq;
using Godot;

/// <summary>Plain-language upgrade effects for the war wagon workshop.</summary>
public partial class ShopMenu
{
    private string BuildBaseUpgradeEffectText(BaseUpgradeDefinition upgrade, int level)
    {
        var armamentEffect = BaseWeaponCatalog.UpgradeEffect(upgrade.Id, level);
        if (armamentEffect != null)
            return armamentEffect;
        return upgrade.Id switch
        {
            BaseUpgradeCatalog.HullPlatingId => $"+{Mathf.RoundToInt((GameState.Instance.GetPlayerBaseHealthScaleAtLevel(level) - 1f) * 100f)}% war wagon hull",
            BaseUpgradeCatalog.PantryId => $"+{GameState.Instance.GetPlayerCourageMaxBonusAtLevel(level):0} max courage · " + $"+{Mathf.RoundToInt((GameState.Instance.GetPlayerCourageGainScaleAtLevel(level) - 1f) * 100f)}% gain",
            BaseUpgradeCatalog.DispatchConsoleId => $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerDeployCooldownScaleAtLevel(level)) * 100f)}% card recovery",
            BaseUpgradeCatalog.SignalRelayId => $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerSignalJamDurationScaleAtLevel(level)) * 100f)}% jam time · " + $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerSignalJamCooldownPenaltyScaleAtLevel(level)) * 100f)}% jam cooldown hit · " + $"+{Mathf.RoundToInt(GameState.Instance.GetPlayerSignalJamSuppressionMitigationAtLevel(level) * 100f)}% jam resist",
            _ => upgrade.Summary
        };
    }

}
