using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Validates game data JSON files for internal consistency.
/// Run from the project root: dotnet script scripts/tests/DataIntegrityValidator.cs
/// Or call DataIntegrityValidator.RunAll(projectRoot) from a test harness.
/// </summary>
public static class DataIntegrityValidator
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> Errors = new();

    public static int RunAll(string dataDir)
    {
        _passed = 0;
        _failed = 0;
        Errors.Clear();

        Console.WriteLine("=== Game Data Integrity Validation ===\n");

        var unitsPath = Path.Combine(dataDir, "units.json");
        var stagesPath = Path.Combine(dataDir, "stages.json");
        var spellsPath = Path.Combine(dataDir, "spells.json");
        var equipmentPath = Path.Combine(dataDir, "equipment.json");
        var shopProductsPath = Path.Combine(dataDir, "shop_products.json");

        // Load all data
        var units = LoadArray(unitsPath, "Units");
        var stages = LoadArray(stagesPath, "Stages");
        var spells = LoadArray(spellsPath, "Spells");
        var equipment = LoadArray(equipmentPath, "Equipment");
        var shopProducts = LoadArrayRoot(shopProductsPath);

        if (units == null || stages == null || spells == null)
        {
            Console.WriteLine("FATAL: Could not load core data files.");
            return 1;
        }

        // Build lookups
        var unitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var playerUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enemyUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spellIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var equipmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Unit validation ──
        Console.WriteLine("--- Units ---");
        foreach (var unit in units)
        {
            var id = GetStr(unit, "Id");
            var name = GetStr(unit, "DisplayName");
            var side = GetStr(unit, "Side");

            Check(!string.IsNullOrWhiteSpace(id), $"Unit has empty Id (name={name})");
            Check(!unitIds.Contains(id), $"Duplicate unit Id: {id}");
            unitIds.Add(id);

            if (side.Equals("Player", StringComparison.OrdinalIgnoreCase))
                playerUnitIds.Add(id);
            else
                enemyUnitIds.Add(id);

            Check(!string.IsNullOrWhiteSpace(name), $"Unit {id} has empty DisplayName");
            Check(GetFloat(unit, "MaxHealth") > 0, $"Unit {id} has non-positive MaxHealth");
            Check(GetFloat(unit, "Speed") > 0, $"Unit {id} has non-positive Speed");
            // Siege Tower is a structure — 0 attack damage is intentional
            Check(GetFloat(unit, "AttackDamage") > 0 || id.Contains("siegetower"), $"Unit {id} has non-positive AttackDamage");
            Check(GetFloat(unit, "AttackCooldown") > 0, $"Unit {id} has non-positive AttackCooldown");

            var spawnOnDeath = GetStr(unit, "SpawnOnDeathUnitId");
            if (!string.IsNullOrWhiteSpace(spawnOnDeath))
            {
                // Deferred check — validate after all units loaded
            }

            var specialSpawn = GetStr(unit, "SpecialSpawnUnitId");
            if (!string.IsNullOrWhiteSpace(specialSpawn))
            {
                // Deferred check
            }

            if (side.Equals("Player", StringComparison.OrdinalIgnoreCase))
            {
                Check(GetInt(unit, "Cost") > 0 || id.Contains("skeleton"), $"Player unit {id} has non-positive Cost");
            }
        }

        // Deferred cross-reference checks for units
        foreach (var unit in units)
        {
            var id = GetStr(unit, "Id");
            var spawnOnDeath = GetStr(unit, "SpawnOnDeathUnitId");
            if (!string.IsNullOrWhiteSpace(spawnOnDeath))
            {
                Check(unitIds.Contains(spawnOnDeath), $"Unit {id} SpawnOnDeathUnitId '{spawnOnDeath}' not found in unit roster");
            }

            var specialSpawn = GetStr(unit, "SpecialSpawnUnitId");
            if (!string.IsNullOrWhiteSpace(specialSpawn))
            {
                Check(unitIds.Contains(specialSpawn), $"Unit {id} SpecialSpawnUnitId '{specialSpawn}' not found in unit roster");
            }
        }

        Check(playerUnitIds.Count >= 16, $"Expected at least 16 player units, found {playerUnitIds.Count}");
        Check(enemyUnitIds.Count >= 15, $"Expected at least 15 enemy units, found {enemyUnitIds.Count}");

        // ── Spell validation ──
        Console.WriteLine("--- Spells ---");
        foreach (var spell in spells)
        {
            var id = GetStr(spell, "Id");
            var name = GetStr(spell, "DisplayName");

            Check(!string.IsNullOrWhiteSpace(id), $"Spell has empty Id (name={name})");
            Check(!spellIds.Contains(id), $"Duplicate spell Id: {id}");
            spellIds.Add(id);

            Check(!string.IsNullOrWhiteSpace(name), $"Spell {id} has empty DisplayName");
            Check(GetInt(spell, "CourageCost") > 0, $"Spell {id} has non-positive CourageCost");
            Check(GetFloat(spell, "Cooldown") > 0, $"Spell {id} has non-positive Cooldown");
            Check(!string.IsNullOrWhiteSpace(GetStr(spell, "EffectType")), $"Spell {id} has empty EffectType");
        }

        Check(spellIds.Count >= 10, $"Expected at least 10 spells, found {spellIds.Count}");

        // ── Equipment validation ──
        Console.WriteLine("--- Equipment ---");
        if (equipment != null)
        {
            foreach (var equip in equipment)
            {
                var id = GetStr(equip, "Id");
                var name = GetStr(equip, "DisplayName");
                var rarity = GetStr(equip, "Rarity");

                Check(!string.IsNullOrWhiteSpace(id), $"Equipment has empty Id (name={name})");
                Check(!equipmentIds.Contains(id), $"Duplicate equipment Id: {id}");
                equipmentIds.Add(id);

                Check(!string.IsNullOrWhiteSpace(name), $"Equipment {id} has empty DisplayName");
                var validRarities = new[] { "common", "rare", "epic" };
                Check(validRarities.Contains(rarity.ToLowerInvariant()), $"Equipment {id} has invalid rarity: {rarity}");
            }

            Check(equipmentIds.Count >= 12, $"Expected at least 12 equipment items, found {equipmentIds.Count}");
        }

        // ── Stage validation ──
        Console.WriteLine("--- Stages ---");
        var stageNumbers = new HashSet<int>();
        var validMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "city", "harbor", "foundry", "quarantine", "thornwall", "basilica", "mire", "steppe", "gloamwood", "citadel"
        };

        float prevHealthScale = 0f;
        int prevRewardGold = 0;

        foreach (var stage in stages)
        {
            var num = GetInt(stage, "StageNumber");
            var name = GetStr(stage, "StageName");
            var mapId = GetStr(stage, "MapId");

            Check(num > 0, $"Stage has non-positive StageNumber (name={name})");
            Check(!stageNumbers.Contains(num), $"Duplicate StageNumber: {num}");
            stageNumbers.Add(num);

            Check(!string.IsNullOrWhiteSpace(name), $"Stage {num} has empty StageName");
            Check(validMapIds.Contains(mapId), $"Stage {num} has unknown MapId: {mapId}");

            var rewardGold = GetInt(stage, "RewardGold");
            var rewardFood = GetInt(stage, "RewardFood");
            var entryFood = GetInt(stage, "EntryFoodCost");
            var healthScale = GetFloat(stage, "EnemyHealthScale");
            var damageScale = GetFloat(stage, "EnemyDamageScale");

            Check(rewardGold > 0, $"Stage {num} has non-positive RewardGold");
            Check(rewardFood > 0, $"Stage {num} has non-positive RewardFood");
            Check(entryFood > 0, $"Stage {num} has non-positive EntryFoodCost");
            Check(healthScale > 0, $"Stage {num} has non-positive EnemyHealthScale");
            Check(damageScale > 0, $"Stage {num} has non-positive EnemyDamageScale");
            Check(rewardFood > entryFood, $"Stage {num} RewardFood ({rewardFood}) <= EntryFoodCost ({entryFood})");

            Check(GetFloat(stage, "PlayerBaseHealth") > 0, $"Stage {num} has non-positive PlayerBaseHealth");
            Check(GetFloat(stage, "EnemyBaseHealth") > 0, $"Stage {num} has non-positive EnemyBaseHealth");

            // Check wave unit references
            if (stage.TryGetProperty("Waves", out var waves) && waves.ValueKind == JsonValueKind.Array)
            {
                foreach (var wave in waves.EnumerateArray())
                {
                    if (wave.TryGetProperty("UnitIds", out var waveUnitIds) && waveUnitIds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var uid in waveUnitIds.EnumerateArray())
                        {
                            var waveUnitId = uid.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(waveUnitId))
                            {
                                Check(unitIds.Contains(waveUnitId), $"Stage {num} wave references unknown unit: {waveUnitId}");
                            }
                        }
                    }
                }
            }

            prevHealthScale = healthScale;
            prevRewardGold = rewardGold;
        }

        Check(stageNumbers.Count >= 50, $"Expected at least 50 stages, found {stageNumbers.Count}");

        // Check sequential stage numbering
        for (var i = 1; i <= stageNumbers.Count; i++)
        {
            Check(stageNumbers.Contains(i), $"Missing stage number: {i}");
        }

        // ── Shop products validation ──
        Console.WriteLine("--- Shop Products ---");
        if (shopProducts != null)
        {
            var productIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in shopProducts)
            {
                var id = GetStr(product, "id");
                Check(!string.IsNullOrWhiteSpace(id), "Shop product has empty id");
                Check(!productIds.Contains(id), $"Duplicate shop product id: {id}");
                productIds.Add(id);

                var price = GetFloat(product, "priceUsd");
                Check(price > 0, $"Shop product {id} has non-positive priceUsd");
            }

            Check(productIds.Count >= 10, $"Expected at least 10 shop products, found {productIds.Count}");
        }

        // ── Summary ──
        Console.WriteLine();
        if (Errors.Count > 0)
        {
            Console.WriteLine("ERRORS:");
            foreach (var err in Errors)
            {
                Console.Error.WriteLine($"  - {err}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed > 0 ? 1 : 0;
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            _passed++;
        }
        else
        {
            _failed++;
            Errors.Add(message);
            Console.Error.WriteLine($"  FAIL  {message}");
        }
    }

    private static JsonElement[]? LoadArray(string path, string rootProperty)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return null;
        }

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(rootProperty, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            Console.Error.WriteLine($"Missing or invalid '{rootProperty}' array in {path}");
            return null;
        }

        var result = new List<JsonElement>();
        foreach (var item in arr.EnumerateArray()) result.Add(item);
        return result.ToArray();
    }

    private static JsonElement[]? LoadArrayRoot(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return null;
        }

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            Console.Error.WriteLine($"Expected root array in {path}");
            return null;
        }

        var result = new List<JsonElement>();
        foreach (var item in doc.RootElement.EnumerateArray()) result.Add(item);
        return result.ToArray();
    }

    private static string GetStr(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }

    private static int GetInt(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : 0;
    }

    private static float GetFloat(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0f;
        if (v.TryGetDouble(out var d)) return (float)d;
        if (v.TryGetInt32(out var i)) return i;
        return 0f;
    }
}
