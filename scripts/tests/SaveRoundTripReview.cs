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
        // A server-recorded purchase grant, so the applied purchase IDs (exactly-once grants) are covered.
        state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = $"PUR-roundtrip-{Guid.NewGuid():N}", ProductId = "gold_pouch", GoldCredited = 500 });
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
