using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Checks that the save keeps all state. (1) A save full of progress is reloaded from disk and
/// every GameState field is compared. (2) Every number and switch in GameSaveData is nudged,
/// loaded and saved again, to find values the loader drops. (3) GameState fields with no
/// counterpart in GameSaveData are listed for review.
/// </summary>
public partial class SaveRoundTripReview : Node
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private int _failures;

    // Fields that legitimately differ after a reload, and why.
    private static readonly Dictionary<string, string> Transient = new()
    {
        ["AdventureKnowledgeRevision"] = "a cache counter that bumps when the map is rebuilt",
        ["FoodRechargedAtUnixSeconds"] = "the food clock restarts at load while food is full",
    };
    // Save values the loader deliberately does not take as written, and why.
    private static readonly Dictionary<string, string> Normalized = new()
    {
        ["Version"] = "the loader stamps the current save format",
        ["FoodRechargedAtUnixSeconds"] = "the food clock restarts at load while food is full",
        ["DeveloperBattleSpeed"] = "only the listed developer speeds load",
        ["CampaignMomentumStacks"] = "momentum is capped at 3",
        ["AnalyticsConsent"] = "consent counts only for the current consent version",
        ["HasShownConsentPrompt"] = "consent counts only for the current consent version",
        ["AnalyticsConsentVersion"] = "consent counts only for the current consent version",
    };

    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok, string description) { GD.Print($"SAVE_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=save-review-"))) throw new Exception("Use an isolated save-review save.");
            await Wait();
            var state = GameState.Instance;
            state.ResetProgress();
            FillLateGame(state);
            ReloadKeepsEveryField(state);
            LoaderKeepsEveryValue(state);
            RefundsApplyOnce(state);
            ListUnsavedFields();
            var size = System.Text.Encoding.UTF8.GetByteCount(CloudSavePrivacy.Sanitize(JsonSerializer.Serialize(state.BuildSaveData())));
            Check(size < 512 * 1024 / 4, $"A late-game cloud save is {size / 1024.0:0.0} KB, well inside the server's 512 KB limit");
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"SAVE_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>Late-game progress across systems, plus every setting away from its default. Keeps the current identity.</summary>
    internal static void FillLateGame(GameState state)
    {
        state.SetDeveloperMode(true);
        state.TryDeveloperWinStages(45, out _);
        state.TryDeveloperLevelUp(30, out _);
        state.TryDeveloperOpenAllZones(out _);
        state.TryDeveloperUnlockWarband(out _);
        state.TryDeveloperTrainWarband(out _);
        state.TryAddDeveloperResources(4321, 123);
        foreach (var currency in new[] { "essence", "shards", "sigils" }) state.TryAddDeveloperCurrency(currency, 77);
        foreach (var achievement in AchievementCatalog.GetAll().Take(5)) state.TryUnlockAchievement(achievement.Id);
        state.ContributeRaidDamage(120); state.ContributeRaidDamage(80);
        // A server-recorded purchase grant, so the applied purchase IDs (exactly-once grants) are covered,
        // and a store refund of an earlier one, so the applied wallet adjustments are too.
        state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = $"PUR-roundtrip-{Guid.NewGuid():N}", ProductId = "gold_pouch", GoldCredited = 500 });
        var refunded = $"PUR-roundtrip-refunded-{Guid.NewGuid():N}";
        state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = refunded, ProductId = "food_rations", FoodCredited = 20 });
        state.TryApplyWalletAdjustment(new WalletAdjustment { AdjustmentId = $"ADJ-roundtrip-{Guid.NewGuid():N}", PurchaseId = refunded, ProductId = "food_rations", Kind = "refund", FoodDelta = -20 });
        state.SetPlayerCallsign("Roundtrip");
        state.SetDifficulty("hard");
        state.SetShowDevUi(false); state.SetShowFpsCounter(false); state.SetAudioMuted(true);
        state.SetEffectsVolumePercent(31); state.SetAmbienceVolumePercent(32); state.SetMusicVolumePercent(33);
        state.SetFontSizeOffset(2); state.SetHighContrast(true); state.SetShowHints(false);
        state.SetColorblindMode("deuteranopia"); state.SetReducedMotion(true); state.SetAutoBattle(true); state.SetLargeTextMode(true);
        state.SetCrashReportingConsent(true); state.SetAnalyticsConsent(true);
        state.SetDeveloperBattleSpeed(2f);
        state.SetSelectedStage(12);
        var code = AsyncChallengeCatalog.Create(3, AsyncChallengeCatalog.PressureSpikeId, 4242).Code;
        state.TrySetSelectedAsyncChallengeCode(code, out _);
    }

    /// <summary>
    /// Nothing may go missing or change. Loading may add entries (every roster unit at level 1,
    /// map tiles implied by explored sites); those are listed as SAVE_ADDED.
    /// </summary>
    private void ReloadKeepsEveryField(GameState state)
    {
        var before = Snapshot(state);
        SaveSystem.Instance.Save(state.BuildSaveData());
        Check(SaveSystem.Instance.LastSaveSucceeded, "The full save writes to disk");
        state.ReloadFromDisk();
        var after = Snapshot(state);
        var lost = new List<string>();
        foreach (var (field, was) in before)
        {
            if (!after.TryGetValue(field, out var now) || Transient.ContainsKey(field)) continue;
            var missing = was.Where(e => !now.TryGetValue(e.Key, out var v) || v != e.Value).Select(e => e.Key).ToList();
            var added = now.Keys.Except(was.Keys).ToList();
            if (missing.Count > 0) { lost.Add(field); GD.Print($"SAVE_LOST: {field}: {Clip(string.Join(", ", missing.Select(k => $"{k}={Value(was, k)}→{Value(now, k)}")))}"); }
            if (added.Count > 0) GD.Print($"SAVE_ADDED: {field}: {added.Count} entries, e.g. {Clip(string.Join(", ", added.Take(4).Select(k => $"{k}={now[k]}")))}");
        }
        foreach (var (field, why) in Transient) GD.Print($"SAVE_TRANSIENT: {field} — {why}");
        Check(lost.Count == 0, $"Reloading from disk loses or changes nothing ({string.Join(", ", lost)})");
        Check(state.AnalyticsConsent && state.CrashReportingConsent, "Consent given in this version survives a reload");
        var first = JsonSerializer.Serialize(state.BuildSaveData());
        SaveSystem.Instance.Save(state.BuildSaveData()); state.ReloadFromDisk();
        Check(JsonSerializer.Serialize(state.BuildSaveData()) == first, "Once loaded, saving and loading again is exact");
    }

    private static string Value(Dictionary<string, string> entries, string key) => entries.TryGetValue(key, out var value) ? value : "<missing>";

    /// <summary>
    /// A store refund the server reports is applied once per adjustment ID, however often the device
    /// hears about it and across reloads; it stops at zero when the currency was spent, records the
    /// rest, and a reversal gives back only what was taken.
    /// </summary>
    private void RefundsApplyOnce(GameState state)
    {
        WalletAdjustment Refund(string purchaseId, string product, int gold, int food) => new()
            { AdjustmentId = $"ADJ-review-{Guid.NewGuid():N}", PurchaseId = purchaseId, ProductId = product, Kind = "refund", GoldDelta = -gold, FoodDelta = -food };
        string Buy(string product, int gold, int food)
        {
            var id = $"PUR-review-{Guid.NewGuid():N}";
            state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = id, ProductId = product, GoldCredited = gold, FoodCredited = food });
            return id;
        }
        void SetBalances(int gold, int food)
        {
            var data = state.BuildSaveData();
            data.Gold = gold; data.Food = food;
            SaveSystem.Instance.Save(data);
            state.ReloadFromDisk();
        }

        var chest = Buy("gold_chest", 2200, 0);
        var refund = Refund(chest, "gold_chest", 2200, 0);
        var gold = state.Gold;
        var applied = state.TryApplyWalletAdjustment(refund);
        Check(applied is { GoldApplied: -2200, GoldUnrecovered: 0 } && state.Gold == gold - 2200 && SaveSystem.Instance.LastSaveSucceeded,
            $"A refund takes back its purchase's gold and saves ({gold} → {state.Gold})");
        Check(state.ConsumeWalletAdjustmentNotice().Contains("refunded"), "The player is told why the gold went");
        Check(state.TryApplyWalletAdjustment(refund) == null && state.Gold == gold - 2200, "Hearing about the same refund again changes nothing");
        state.ReloadFromDisk();
        Check(state.HasAppliedWalletAdjustment(refund.AdjustmentId) && state.TryApplyWalletAdjustment(refund) == null && state.Gold == gold - 2200,
            "After a reload the same refund still changes nothing");

        // The player spent most of a resupply before it was refunded.
        var resupply = Buy("campaign_resupply", 3000, 80);
        SetBalances(300, 5);
        var spent = state.TryApplyWalletAdjustment(Refund(resupply, "campaign_resupply", 3000, 80));
        Check(spent is { GoldApplied: -300, FoodApplied: -5, GoldUnrecovered: 2700, FoodUnrecovered: 75 } && state.Gold == 0 && state.Food == 0,
            $"A refund larger than the balance stops at zero and records the rest (gold {state.Gold}, food {state.Food})");
        var spentId = spent?.AdjustmentId ?? "";
        state.ReloadFromDisk();
        Check(state.Gold == 0 && state.GetAppliedWalletAdjustment(spentId) is { GoldUnrecovered: 2700, FoodUnrecovered: 75 },
            "The balance and the unrecovered rest survive a reload");

        // The store reverses that refund: only what was taken comes back.
        var reversal = state.TryApplyWalletAdjustment(new WalletAdjustment
            { AdjustmentId = $"ADJ-review-{Guid.NewGuid():N}", PurchaseId = resupply, ProductId = "campaign_resupply", Kind = "refund_reversed", GoldDelta = 3000, FoodDelta = 80, ReversesAdjustmentId = spentId });
        Check(reversal is { GoldApplied: 300, FoodApplied: 5 } && state.Gold == 300 && state.Food == 5, "A reversed refund gives back exactly what it took");

        // Relic shard packs are credited, refunded and stopped at zero like gold and food.
        var shardsBefore = state.RelicShards;
        var casket = $"PUR-review-{Guid.NewGuid():N}";
        Check(state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = casket, ProductId = "shard_casket", ShardsCredited = 55 })
            && state.RelicShards == shardsBefore + 55, "A shard pack adds its relic shards");
        var shardData = state.BuildSaveData();
        shardData.RelicShards = 20;
        SaveSystem.Instance.Save(shardData);
        state.ReloadFromDisk();
        var shardRefund = state.TryApplyWalletAdjustment(new WalletAdjustment
            { AdjustmentId = $"ADJ-review-{Guid.NewGuid():N}", PurchaseId = casket, ProductId = "shard_casket", Kind = "refund", ShardsDelta = -55 });
        Check(shardRefund is { ShardsApplied: -20, ShardsUnrecovered: 35 } && state.RelicShards == 0,
            $"A shard refund larger than the balance stops at zero and records the rest (shards {state.RelicShards})");
        Check(state.ConsumeWalletAdjustmentNotice().Contains("20 relic shards were taken back"), "The player is told how many shards went");
        state.ReloadFromDisk();
        Check(state.GetAppliedWalletAdjustment(shardRefund?.AdjustmentId ?? "") is { ShardsApplied: -20, ShardsUnrecovered: 35 },
            "The shard refund survives a reload");

        // A refund of a purchase this save never received, and a kind this build does not know.
        gold = state.Gold;
        var stranger = Refund($"PUR-review-elsewhere-{Guid.NewGuid():N}", "gold_pouch", 500, 0);
        Check(state.TryApplyWalletAdjustment(stranger) is { GoldApplied: 0, GoldUnrecovered: 0 } && state.Gold == gold,
            "A refund of a purchase this save never received takes nothing");
        var future = new WalletAdjustment { AdjustmentId = $"ADJ-review-{Guid.NewGuid():N}", PurchaseId = chest, Kind = "goodwill_grant", GoldDelta = 100 };
        Check(state.TryApplyWalletAdjustment(future) == null && !state.HasAppliedWalletAdjustment(future.AdjustmentId) && state.Gold == gold,
            "An adjustment this build does not understand is left for a newer one");
    }

    /// <summary>Nudges each scalar save value, loads it and saves again; values that do not come back are dropped or clamped.</summary>
    private void LoaderKeepsEveryValue(GameState state)
    {
        var dropped = new List<string>();
        foreach (var property in typeof(GameSaveData).GetProperties().Where(p => p.CanRead && p.CanWrite))
        {
            var data = state.BuildSaveData();
            var value = property.GetValue(data);
            object nudged = value switch
            {
                bool b => !b, int i => i + 1, long l => l + 1, float f => f + 1, double d => d + 1, _ => null
            };
            if (nudged == null) continue;
            property.SetValue(data, nudged);
            SaveSystem.Instance.Save(data);
            state.ReloadFromDisk();
            var back = property.GetValue(state.BuildSaveData());
            if (!Equals(back, nudged))
            {
                if (Normalized.TryGetValue(property.Name, out var why)) GD.Print($"SAVE_NORMALIZED: {property.Name} — {why}");
                else dropped.Add($"{property.Name} ({value} → {nudged} came back {back})");
            }
            property.SetValue(data, value);
            SaveSystem.Instance.Save(data);
            state.ReloadFromDisk();
        }
        foreach (var entry in dropped) GD.Print($"SAVE_DROPPED: {entry}");
        Check(dropped.Count == 0, $"The loader keeps every nudged number and switch ({dropped.Count} not kept)");
    }

    /// <summary>GameState fields whose name matches nothing in GameSaveData: either transient, or state that is never saved.</summary>
    private static void ListUnsavedFields()
    {
        var saved = typeof(GameSaveData).GetProperties().Select(p => Normalize(p.Name)).ToHashSet();
        foreach (var field in typeof(GameState).GetFields(Fields).Where(f => !f.IsLiteral && !typeof(Delegate).IsAssignableFrom(f.FieldType)))
        {
            var name = FieldName(field);
            if (!saved.Contains(Normalize(name))) GD.Print($"SAVE_UNMATCHED: {name} ({field.FieldType.Name})");
        }
    }

    /// <summary>Each field as entries: a dictionary by key, a set by element, a list by position, anything else as one value.</summary>
    private static Dictionary<string, Dictionary<string, string>> Snapshot(GameState state)
    {
        var snapshot = new Dictionary<string, Dictionary<string, string>>();
        foreach (var field in typeof(GameState).GetFields(Fields).Where(f => !f.IsLiteral))
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType) || typeof(GodotObject).IsAssignableFrom(field.FieldType)) continue;
            var value = field.GetValue(state);
            var entries = new Dictionary<string, string>();
            try
            {
                switch (value)
                {
                    case IDictionary map: foreach (var key in map.Keys) entries[key.ToString()!] = Text(map[key]); break;
                    case string: entries[""] = Text(value); break;
                    case IEnumerable set when value.GetType().Name.StartsWith("HashSet"): foreach (var item in set) entries[Text(item)] = ""; break;
                    case IEnumerable list: var index = 0; foreach (var item in list) entries[$"[{index++}]"] = Text(item); break;
                    default: entries[""] = Text(value); break;
                }
            }
            catch (Exception e) { entries[""] = $"<{e.GetType().Name}>"; }
            snapshot[FieldName(field)] = entries;
        }
        return snapshot;
    }

    private static string Text(object value) => JsonSerializer.Serialize(value, Json);
    private static string FieldName(FieldInfo field) => field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>')] : field.Name.TrimStart('_');
    private static string Normalize(string name) => name.TrimStart('_').ToLowerInvariant();
    private static string Clip(string text) => text.Length > 300 ? text[..300] + "…" : text;
}
