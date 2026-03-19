using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CrownroadServer;

public static class BalanceDashboard
{
    public static string Render(string dataDir)
    {
        var stagesPath = Path.Combine(dataDir, "stages.json");
        var unitsPath = Path.Combine(dataDir, "units.json");
        var spellsPath = Path.Combine(dataDir, "spells.json");

        var stages = LoadArray(stagesPath, "Stages");
        var units = LoadArray(unitsPath, "Units");
        var spells = LoadArray(spellsPath, "Spells");

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Balance Dashboard</title>");
        sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.Append("<style>");
        sb.Append("body{font-family:-apple-system,sans-serif;background:#0d1117;color:#c9d1d9;max-width:1100px;margin:0 auto;padding:20px}");
        sb.Append("h1{color:#e2b714}h2{color:#8b949e;border-bottom:1px solid #30363d;padding-bottom:8px}");
        sb.Append("table{width:100%;border-collapse:collapse;margin:12px 0;font-size:13px}");
        sb.Append("th,td{padding:6px 10px;text-align:left;border-bottom:1px solid #21262d}");
        sb.Append("th{color:#8b949e;font-weight:600;position:sticky;top:0;background:#0d1117}");
        sb.Append(".bar{height:16px;border-radius:3px;display:inline-block;vertical-align:middle}");
        sb.Append(".warn{color:#f85149}.good{color:#3fb950}.mid{color:#d29922}");
        sb.Append(".chart{display:flex;align-items:flex-end;gap:2px;height:120px;margin:12px 0;padding:4px;background:#161b22;border-radius:6px}");
        sb.Append(".chart-bar{flex:1;background:#e2b714;border-radius:2px 2px 0 0;min-width:4px;position:relative}");
        sb.Append(".chart-bar:hover{background:#ffd166}");
        sb.Append(".chart-bar .tip{display:none;position:absolute;bottom:100%;left:50%;transform:translateX(-50%);background:#30363d;color:#c9d1d9;padding:2px 6px;border-radius:3px;font-size:11px;white-space:nowrap}");
        sb.Append(".chart-bar:hover .tip{display:block}");
        sb.Append("</style></head><body>");
        sb.Append("<h1>Balance Dashboard</h1>");
        sb.Append("<p><a href='/admin' style='color:#58a6ff'>Back to Admin</a></p>");

        // Stage difficulty curve
        if (stages != null)
        {
            RenderStageCurves(sb, stages);
        }

        // Unit value table
        if (units != null)
        {
            RenderUnitTable(sb, units);
        }

        // Spell value table
        if (spells != null)
        {
            RenderSpellTable(sb, spells);
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void RenderStageCurves(StringBuilder sb, JsonElement[] stages)
    {
        sb.Append("<h2>Stage Difficulty Curve</h2>");

        var sorted = stages.OrderBy(s => GetInt(s, "StageNumber")).ToArray();
        var maxHealth = sorted.Max(s => GetFloat(s, "EnemyHealthScale"));
        var maxReward = sorted.Max(s => GetInt(s, "RewardGold"));

        // Health scale bar chart
        sb.Append("<p style='color:#8b949e;font-size:12px'>Enemy Health Scale by Stage</p>");
        sb.Append("<div class='chart'>");
        foreach (var stage in sorted)
        {
            var num = GetInt(stage, "StageNumber");
            var health = GetFloat(stage, "EnemyHealthScale");
            var pct = maxHealth > 0 ? (health / maxHealth * 100) : 0;
            sb.Append($"<div class='chart-bar' style='height:{pct:F0}%'><span class='tip'>S{num}: {health:F2}x</span></div>");
        }
        sb.Append("</div>");

        // Reward gold bar chart
        sb.Append("<p style='color:#8b949e;font-size:12px'>Reward Gold by Stage</p>");
        sb.Append("<div class='chart'>");
        foreach (var stage in sorted)
        {
            var num = GetInt(stage, "StageNumber");
            var gold = GetInt(stage, "RewardGold");
            var pct = maxReward > 0 ? ((float)gold / maxReward * 100) : 0;
            sb.Append($"<div class='chart-bar' style='height:{pct:F0}%;background:#3fb950'><span class='tip'>S{num}: {gold}g</span></div>");
        }
        sb.Append("</div>");

        // Stage detail table
        sb.Append("<h2>Stage Details</h2>");
        sb.Append("<table><tr><th>#</th><th>Name</th><th>District</th><th>Health</th><th>Damage</th><th>Gold</th><th>Food</th><th>Entry</th><th>Net Food</th><th>Ratio</th></tr>");
        foreach (var stage in sorted)
        {
            var num = GetInt(stage, "StageNumber");
            var name = GetStr(stage, "StageName");
            var map = GetStr(stage, "MapName");
            var health = GetFloat(stage, "EnemyHealthScale");
            var damage = GetFloat(stage, "EnemyDamageScale");
            var gold = GetInt(stage, "RewardGold");
            var food = GetInt(stage, "RewardFood");
            var entry = GetInt(stage, "EntryFoodCost");
            var net = food - entry;
            var ratio = entry > 0 ? (float)food / entry : 0;
            var ratioClass = ratio < 1.5 ? "warn" : ratio < 2.0 ? "mid" : "good";

            sb.Append($"<tr><td>{num}</td><td>{name}</td><td>{map}</td>");
            sb.Append($"<td>{health:F2}x</td><td>{damage:F2}x</td>");
            sb.Append($"<td>{gold}</td><td>{food}</td><td>{entry}</td>");
            sb.Append($"<td>{net}</td><td class='{ratioClass}'>{ratio:F1}x</td></tr>");
        }
        sb.Append("</table>");
    }

    private static void RenderUnitTable(StringBuilder sb, JsonElement[] units)
    {
        var playerUnits = units
            .Where(u => GetStr(u, "Side").Equals("Player", StringComparison.OrdinalIgnoreCase))
            .Where(u => GetInt(u, "Cost") > 0)
            .OrderBy(u => GetInt(u, "UnlockStage"))
            .ThenBy(u => GetInt(u, "Cost"))
            .ToArray();

        sb.Append("<h2>Player Unit Value</h2>");
        sb.Append("<table><tr><th>Unit</th><th>Cost</th><th>HP</th><th>ATK</th><th>Range</th><th>CD</th><th>DPS</th><th>DMG/Cost</th><th>HP/Cost</th><th>Unlock</th></tr>");

        foreach (var unit in playerUnits)
        {
            var name = GetStr(unit, "DisplayName");
            var cost = GetInt(unit, "Cost");
            var hp = GetFloat(unit, "MaxHealth");
            var atk = GetFloat(unit, "AttackDamage");
            var range = GetFloat(unit, "AttackRange");
            var cd = GetFloat(unit, "AttackCooldown");
            var unlock = GetInt(unit, "UnlockStage");
            var dps = cd > 0.01 ? atk / cd : 0;
            var dmgPerCost = cost > 0 ? atk / cost : 0;
            var hpPerCost = cost > 0 ? hp / cost : 0;

            var dmgClass = dmgPerCost > 0.65 ? "good" : dmgPerCost < 0.4 ? "warn" : "mid";
            var hpClass = hpPerCost > 3.0 ? "good" : hpPerCost < 1.5 ? "warn" : "mid";

            sb.Append($"<tr><td>{name}</td><td>{cost}</td><td>{hp:F0}</td><td>{atk:F0}</td>");
            sb.Append($"<td>{range:F0}</td><td>{cd:F2}s</td><td>{dps:F1}</td>");
            sb.Append($"<td class='{dmgClass}'>{dmgPerCost:F2}</td>");
            sb.Append($"<td class='{hpClass}'>{hpPerCost:F1}</td>");
            sb.Append($"<td>S{unlock}</td></tr>");
        }
        sb.Append("</table>");
    }

    private static void RenderSpellTable(StringBuilder sb, JsonElement[] spells)
    {
        var sorted = spells.OrderBy(s => GetInt(s, "UnlockStage")).ToArray();

        sb.Append("<h2>Spell Value</h2>");
        sb.Append("<table><tr><th>Spell</th><th>Courage</th><th>Power</th><th>Radius</th><th>CD</th><th>Duration</th><th>Power/Courage</th><th>Unlock</th></tr>");

        foreach (var spell in sorted)
        {
            var name = GetStr(spell, "DisplayName");
            var courage = GetInt(spell, "CourageCost");
            var power = GetFloat(spell, "Power");
            var radius = GetFloat(spell, "Radius");
            var cd = GetFloat(spell, "Cooldown");
            var duration = GetFloat(spell, "Duration");
            var unlock = GetInt(spell, "UnlockStage");
            var powerPerCourage = courage > 0 && power > 1 ? power / courage : 0;

            var valueClass = powerPerCourage > 1.2 ? "good" : powerPerCourage > 0 && powerPerCourage < 0.8 ? "warn" : "mid";

            sb.Append($"<tr><td>{name}</td><td>{courage}</td><td>{power:F1}</td><td>{radius:F0}</td>");
            sb.Append($"<td>{cd:F1}s</td><td>{(duration > 0 ? $"{duration:F1}s" : "-")}</td>");
            sb.Append($"<td class='{valueClass}'>{(powerPerCourage > 0 ? $"{powerPerCourage:F2}" : "utility")}</td>");
            sb.Append($"<td>S{unlock}</td></tr>");
        }
        sb.Append("</table>");
    }

    private static JsonElement[]? LoadArray(string path, string rootProperty)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(rootProperty, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var result = new List<JsonElement>();
        foreach (var item in arr.EnumerateArray()) result.Add(item);
        return result.ToArray();
    }

    private static string GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : 0;

    private static float GetFloat(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0f;
        if (v.TryGetDouble(out var d)) return (float)d;
        if (v.TryGetInt32(out var i)) return i;
        return 0f;
    }
}
