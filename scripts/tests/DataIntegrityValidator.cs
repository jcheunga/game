#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    private static readonly Regex IndexedFormatPlaceholderPattern = new(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);

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
        var localeDir = Path.Combine(dataDir, "locale");
        var assetsDir = Path.GetFullPath(Path.Combine(dataDir, "..", "assets"));

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

        // The shallow battlefield only works if every fighter's vertical aggro reaches either edge from the
        // wagon's centre line, yet cannot span the whole band from one edge to the other.
        var laneHalfHeight = LoadLaneHalfHeight(Path.Combine(dataDir, "combat_config.json"));
        Check(laneHalfHeight > 0, "combat_config.json must define a positive battlefield lane height");
        foreach (var unit in units.Where(unit => GetFloat(unit, "AttackRange") > 0))
        {
            var aggroY = unit.TryGetProperty("AggroRangeY", out _) ? GetFloat(unit, "AggroRangeY") : 96f;
            Check(aggroY >= laneHalfHeight && aggroY < laneHalfHeight * 2,
                $"Unit {GetStr(unit, "Id")} AggroRangeY {aggroY} must lie in [{laneHalfHeight}, {laneHalfHeight * 2}) for the battlefield band");
        }

        Check(playerUnitIds.Count >= 16, $"Expected at least 16 player units, found {playerUnitIds.Count}");
        Check(enemyUnitIds.Count >= 15, $"Expected at least 15 enemy units, found {enemyUnitIds.Count}");

        // ── Spell validation ──
        Console.WriteLine("--- Spells ---");
        var (manaMax, manaPerKill) = LoadMana(Path.Combine(dataDir, "combat_config.json"));
        Check(manaMax > 0 && manaPerKill > 0, "combat_config.json must define a positive ManaMax and ManaPerEnemyKill");
        foreach (var spell in spells)
        {
            var id = GetStr(spell, "Id");
            var name = GetStr(spell, "DisplayName");

            Check(!string.IsNullOrWhiteSpace(id), $"Spell has empty Id (name={name})");
            Check(!spellIds.Contains(id), $"Duplicate spell Id: {id}");
            spellIds.Add(id);

            Check(!string.IsNullOrWhiteSpace(name), $"Spell {id} has empty DisplayName");
            Check(GetInt(spell, "ManaCost") > 0, $"Spell {id} has non-positive ManaCost");
            Check(GetInt(spell, "ManaCost") <= manaMax, $"Spell {id} costs {GetInt(spell, "ManaCost")} mana, more than the {manaMax} the bar holds");
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
                var validRarities = new[] { "common", "rare", "epic", "hardened" };
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
        var validMissionEventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ritual_site", "relic_escort", "gate_breach", "rescue_hold", "mainline_push"
        };
        // Scripted remix stages (the former postgame 51-60), now inside their zones.
        var remixStages = new HashSet<int> { 8, 20, 28, 40, 48, 60, 68, 78, 88, 100 };
        var postgameBossStages = new Dictionary<int, string>
        {
            [60] = "enemy_boss_reliquary",
            [100] = "enemy_boss_ashen_regent",
            [20] = "enemy_boss_tidemaster",
            [40] = "enemy_boss_plague_monarch"
        };
        // Remix stages keep their signature modifiers once the campaign has introduced them (fortified deploys
        // arrive at stage 51, so the earlier Whiteout Bastion and Pale Crown no longer carry them).
        var postgameModifierExpectations = new Dictionary<int, string[]>
        {
            [60] = new[] { "lich_graveyard" },
            [68] = new[] { "lich_graveyard" },
            [88] = new[] { "mirror_pressure", "tunnel_invasion" },
            [100] = new[] { "reinforced_barricade" }
        };
        var zoneStages = new Dictionary<string, List<(int Number, bool Boss)>>(StringComparer.OrdinalIgnoreCase);

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
            Check(rewardFood >= 0, $"Stage {num} has negative RewardFood");
            Check(entryFood > 0, $"Stage {num} has non-positive EntryFoodCost");
            Check(healthScale > 0, $"Stage {num} has non-positive EnemyHealthScale");
            Check(damageScale > 0, $"Stage {num} has non-positive EnemyDamageScale");
            Check(entryFood == 4, $"Stage {num} must cost 4 food, including bosses");

            Check(GetFloat(stage, "PlayerBaseHealth") > 0, $"Stage {num} has non-positive PlayerBaseHealth");
            Check(GetFloat(stage, "EnemyBaseHealth") > 0, $"Stage {num} has non-positive EnemyBaseHealth");

            Check(stage.TryGetProperty("Battlefield", out var battlefield), $"Stage {num} is missing its expanded battlefield plan");
            if (battlefield.ValueKind == JsonValueKind.Object)
            {
                Check(GetFloat(battlefield, "OutpostXRatio") is >= .3f and <= .5f, $"Stage {num} forward post lies outside the middle approach");
                Check(GetFloat(battlefield, "SupplyXRatio") > GetFloat(battlefield, "OutpostXRatio") && GetFloat(battlefield, "SupplyXRatio") < .9f,
                    $"Stage {num} supplies must lie beyond the forward post and before the gate");
                Check(GetFloat(battlefield, "OutpostYRatio") is >= .15f and <= .85f && GetFloat(battlefield, "SupplyYRatio") is >= .15f and <= .85f,
                    $"Stage {num} field objectives must leave room around their capture rings");
                Check(GetInt(battlefield, "ForwardDeployments") is >= 1 and <= 6 && GetFloat(battlefield, "ForwardCooldown") >= 5 && GetFloat(battlefield, "CaptureSeconds") > 0,
                    $"Stage {num} forward deployments must have bounded charges and recovery");
                Check(GetStr(battlefield, "SupplyReward") is "courage" or "repair" or "siege", $"Stage {num} has an unsupported supply reward");
                Check(GetStr(battlefield, "OutpostTitle").Length > 0 && GetStr(battlefield, "SupplyTitle").Length > 0, $"Stage {num} field objectives need names");
                if (battlefield.TryGetProperty("CursePatches", out var patches))
                    foreach (var patch in patches.EnumerateArray())
                        Check(GetFloat(patch, "XRatio") is >= .2f and <= .85f && GetFloat(patch, "Width") is > 0 and <= 300 &&
                            GetFloat(patch, "Height") > 0 && GetFloat(patch, "Height") <= laneHalfHeight && GetFloat(patch, "YRatio") is >= .25f and <= .75f,
                            $"Stage {num} cursed pockets must leave clear ground above and below");
            }

            // Check wave unit references
            var hasScriptedWaves = false;
            var containsExpectedBossWave = !postgameBossStages.ContainsKey(num);
            var hasBossWave = GetFloat(stage, "BossSpawnStartTime") > 0f;
            if (stage.TryGetProperty("Waves", out var waves) && waves.ValueKind == JsonValueKind.Array)
            {
                hasScriptedWaves = true;
                var previousTriggerTime = -1f;
                foreach (var wave in waves.EnumerateArray())
                {
                    var triggerTime = GetFloat(wave, "TriggerTime");
                    var spawnInterval = GetFloat(wave, "SpawnInterval");
                    Check(triggerTime >= 0f, $"Stage {num} has wave with negative TriggerTime");
                    Check(spawnInterval > 0f, $"Stage {num} has wave with non-positive SpawnInterval");
                    Check(triggerTime + 0.001f >= previousTriggerTime, $"Stage {num} has scripted waves out of order");
                    previousTriggerTime = triggerTime;
                    Check(GetStr(wave, "Area") is "Approach" or "Crossroads" or "Gate", $"Stage {num} wave needs a named encounter area");
                    Check(GetFloat(wave, "SpawnXRatio") is >= .2f and <= 1 && GetFloat(wave, "AdvanceTriggerXRatio") < GetFloat(wave, "SpawnXRatio"),
                        $"Stage {num} encounter entry must be ahead of its advance trigger");

                    if (wave.TryGetProperty("Entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
                    {
                        var entryCount = 0;
                        foreach (var entry in entries.EnumerateArray())
                        {
                            var waveUnitId = GetStr(entry, "UnitId");
                            var count = GetInt(entry, "Count");
                            Check(!string.IsNullOrWhiteSpace(waveUnitId), $"Stage {num} has scripted wave entry with empty UnitId");
                            Check(unitIds.Contains(waveUnitId), $"Stage {num} wave entry references unknown unit: {waveUnitId}");
                            Check(count > 0, $"Stage {num} wave entry '{waveUnitId}' has non-positive Count");
                            if (waveUnitId.StartsWith("enemy_boss", StringComparison.OrdinalIgnoreCase)) hasBossWave = true;
                            if (postgameBossStages.TryGetValue(num, out var expectedBossId) &&
                                waveUnitId.Equals(expectedBossId, StringComparison.OrdinalIgnoreCase))
                            {
                                containsExpectedBossWave = true;
                            }

                            entryCount++;
                        }

                        Check(entryCount > 0, $"Stage {num} has scripted wave with empty Entries");
                    }

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

            if (!zoneStages.TryGetValue(mapId, out var zoneList)) zoneStages[mapId] = zoneList = new();
            zoneList.Add((num, hasBossWave));

            if (stage.TryGetProperty("MissionEvents", out var missionEvents) && missionEvents.ValueKind == JsonValueKind.Array)
            {
                foreach (var mission in missionEvents.EnumerateArray())
                {
                    var missionType = GetStr(mission, "Type");
                    Check(validMissionEventTypes.Contains(missionType), $"Stage {num} has invalid mission event type: {missionType}");
                    Check(GetFloat(mission, "Radius") > 0f, $"Stage {num} mission event '{missionType}' has non-positive Radius");
                    Check(GetFloat(mission, "TargetSeconds") > 0f, $"Stage {num} mission event '{missionType}' has non-positive TargetSeconds");
                    Check(GetFloat(mission, "StartTime") >= 0f, $"Stage {num} mission event '{missionType}' has negative StartTime");
                }
            }

            if (remixStages.Contains(num))
            {
                Check(hasScriptedWaves, $"Postgame stage {num} is missing scripted waves");
                Check(stage.TryGetProperty("MissionEvents", out var postgameMissionEvents) &&
                    postgameMissionEvents.ValueKind == JsonValueKind.Array &&
                    postgameMissionEvents.GetArrayLength() > 0,
                    $"Postgame stage {num} is missing an authored mission event");

                if (postgameBossStages.TryGetValue(num, out var expectedBossWaveId))
                {
                    Check(containsExpectedBossWave, $"Postgame boss stage {num} is missing boss wave '{expectedBossWaveId}'");
                }

                if (postgameModifierExpectations.TryGetValue(num, out var expectedModifierTypes))
                {
                    foreach (var modifierType in expectedModifierTypes)
                    {
                        Check(HasStageModifier(stage, modifierType), $"Postgame stage {num} is missing expected modifier '{modifierType}'");
                    }
                }
            }

            prevHealthScale = healthScale;
            prevRewardGold = rewardGold;
        }

        Check(stageNumbers.Count == 100, $"Expected 100 stages, found {stageNumbers.Count}");
        foreach (var (zone, list) in zoneStages)
        {
            var ordered = list.OrderBy(x => x.Number).ToArray();
            var first = ordered[0].Number;
            Check(ordered.Length == 10 && ordered[^1].Number == first + 9 && (first - 1) % 10 == 0,
                $"Zone {zone} must hold ten contiguous stages starting at 10k+1 (found {string.Join(",", ordered.Select(x => x.Number))})");
            Check(ordered[^1].Boss, $"Zone {zone} must end on a boss stage");
        }

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
            var sortOrders = new HashSet<int>();
            var appleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var googleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stripeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gold", "food", "mixed" };
            foreach (var product in shopProducts)
            {
                var id = GetStr(product, "id");
                var category = GetStr(product, "category");
                var currencyType = GetStr(product, "currencyType");
                var displayName = GetStr(product, "displayName");
                var description = GetStr(product, "description");
                var appleProductId = GetStr(product, "appleProductId");
                var googleProductId = GetStr(product, "googleProductId");
                var stripePriceId = GetStr(product, "stripePriceId");
                var sortOrder = GetInt(product, "sortOrder");
                var currencyAmount = GetInt(product, "currencyAmount");
                var bonusAmount = GetInt(product, "bonusAmount");
                var goldAmount = GetInt(product, "goldAmount");
                var foodAmount = GetInt(product, "foodAmount");
                var grantsUnitUnlock = GetBool(product, "grantsUnitUnlock");

                Check(!string.IsNullOrWhiteSpace(id), "Shop product has empty id");
                Check(!productIds.Contains(id), $"Duplicate shop product id: {id}");
                productIds.Add(id);

                var price = GetFloat(product, "priceUsd");
                Check(validCategories.Contains(category), $"Shop product {id} has invalid category: {category}");
                Check(!string.IsNullOrWhiteSpace(displayName), $"Shop product {id} has empty displayName");
                Check(!string.IsNullOrWhiteSpace(description), $"Shop product {id} has empty description");
                Check(price > 0, $"Shop product {id} has non-positive priceUsd");
                Check(sortOrder > 0, $"Shop product {id} has non-positive sortOrder");
                Check(!sortOrders.Contains(sortOrder), $"Duplicate shop product sortOrder: {sortOrder}");
                sortOrders.Add(sortOrder);

                Check(!string.IsNullOrWhiteSpace(appleProductId), $"Shop product {id} is missing appleProductId");
                Check(!string.IsNullOrWhiteSpace(googleProductId), $"Shop product {id} is missing googleProductId");
                Check(!string.IsNullOrWhiteSpace(stripePriceId), $"Shop product {id} is missing stripePriceId");
                Check(HasValidStoreProductId(appleProductId, requireDot: true), $"Shop product {id} has invalid appleProductId: {appleProductId}");
                Check(HasValidStoreProductId(googleProductId, requireDot: false), $"Shop product {id} has invalid googleProductId: {googleProductId}");
                Check(HasValidStripePriceId(stripePriceId), $"Shop product {id} has invalid stripePriceId: {stripePriceId}");
                Check(!appleIds.Contains(appleProductId), $"Duplicate appleProductId: {appleProductId}");
                Check(!googleIds.Contains(googleProductId), $"Duplicate googleProductId: {googleProductId}");
                Check(!stripeIds.Contains(stripePriceId), $"Duplicate stripePriceId: {stripePriceId}");
                appleIds.Add(appleProductId);
                googleIds.Add(googleProductId);
                stripeIds.Add(stripePriceId);

                Check(bonusAmount >= 0, $"Shop product {id} has negative bonusAmount");

                if (category.Equals("gold", StringComparison.OrdinalIgnoreCase) ||
                    category.Equals("food", StringComparison.OrdinalIgnoreCase))
                {
                    Check(currencyType.Equals(category, StringComparison.OrdinalIgnoreCase),
                        $"Shop product {id} currencyType '{currencyType}' does not match category '{category}'");
                    Check(currencyAmount > 0, $"Shop product {id} has non-positive currencyAmount");
                    Check(goldAmount == 0, $"Shop product {id} should not use goldAmount");
                    Check(foodAmount == 0, $"Shop product {id} should not use foodAmount");
                    Check(!grantsUnitUnlock, $"Shop product {id} should not grant a unit unlock");
                }
                else if (category.Equals("mixed", StringComparison.OrdinalIgnoreCase))
                {
                    Check(currencyType.Equals("mixed", StringComparison.OrdinalIgnoreCase),
                        $"Shop product {id} should use currencyType 'mixed'");
                    Check(currencyAmount == 0, $"Shop product {id} should keep currencyAmount at 0");
                    Check(bonusAmount == 0, $"Shop product {id} should keep bonusAmount at 0");
                    Check(goldAmount > 0 || foodAmount > 0 || grantsUnitUnlock,
                        $"Mixed shop product {id} has no meaningful reward configured");
                }
            }

            Check(productIds.Count >= 10, $"Expected at least 10 shop products, found {productIds.Count}");
        }

        // ── Locale validation ──
        Console.WriteLine("--- Locales ---");
        Check(Directory.Exists(localeDir), $"Locale directory not found: {localeDir}");
        if (Directory.Exists(localeDir))
        {
            var localeFiles = Directory.GetFiles(localeDir, "*.json");
            Check(localeFiles.Length > 0, $"No locale json files found in {localeDir}");

            var englishPath = Path.Combine(localeDir, "en.json");
            var loadedEnglish = TryLoadStringMap(englishPath, out var englishStrings);
            Check(loadedEnglish, $"Could not load fallback locale: {englishPath}");

            if (loadedEnglish)
            {
                Check(englishStrings.Count >= 60, $"Expected at least 60 English locale keys, found {englishStrings.Count}");
                foreach (var pair in englishStrings)
                {
                    Check(!string.IsNullOrWhiteSpace(pair.Value), $"Locale en key '{pair.Key}' is empty");
                }

                foreach (var localePath in localeFiles.OrderBy(Path.GetFileName))
                {
                    var language = Path.GetFileNameWithoutExtension(localePath);
                    var loadedLocale = TryLoadStringMap(localePath, out var localizedStrings);
                    Check(loadedLocale, $"Could not load locale file: {localePath}");
                    if (!loadedLocale)
                    {
                        continue;
                    }

                    foreach (var pair in localizedStrings)
                    {
                        Check(!string.IsNullOrWhiteSpace(pair.Value), $"Locale {language} key '{pair.Key}' is empty");
                    }

                    if (language.Equals("en", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var pair in englishStrings)
                    {
                        Check(localizedStrings.ContainsKey(pair.Key), $"Locale {language} is missing key '{pair.Key}'");
                        if (!localizedStrings.TryGetValue(pair.Key, out var localizedValue))
                        {
                            continue;
                        }

                        Check(
                            HaveMatchingFormatPlaceholders(pair.Value, localizedValue),
                            $"Locale {language} key '{pair.Key}' has mismatched format placeholders");
                    }

                    foreach (var extraKey in localizedStrings.Keys.Where(key => !englishStrings.ContainsKey(key)))
                    {
                        Check(false, $"Locale {language} has extra key '{extraKey}' not present in en.json");
                    }
                }
            }
        }

        // ── Asset coverage report ──
        PrintAssetCoverageReport(assetsDir, units, spells, equipment ?? Array.Empty<JsonElement>(), stages);

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

    private static float LoadLaneHalfHeight(string path)
    {
        if (!File.Exists(path)) return 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("Combat", out var combat)) return 0;
        return (GetFloat(combat, "BattlefieldBottom") - GetFloat(combat, "BattlefieldTop")) * 0.5f - GetFloat(combat, "SpawnVerticalPadding");
    }

    private static (float Max, float PerKill) LoadMana(string path)
    {
        if (!File.Exists(path)) return (0, 0);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("Combat", out var combat)) return (0, 0);
        return (GetFloat(combat, "ManaMax"), GetFloat(combat, "ManaPerEnemyKill"));
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

    private static bool TryLoadStringMap(string path, out Dictionary<string, string> result)
    {
        result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return false;
        }

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            Console.Error.WriteLine($"Expected root object in {path}");
            return false;
        }

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                Console.Error.WriteLine($"Expected string value for locale key '{prop.Name}' in {path}");
                result.Clear();
                return false;
            }

            result[prop.Name] = prop.Value.GetString() ?? "";
        }

        return true;
    }

    private static void PrintAssetCoverageReport(string assetsDir, JsonElement[] units, JsonElement[] spells, JsonElement[] equipment, JsonElement[] stages)
    {
        Console.WriteLine("--- Asset Coverage ---");
        if (!Directory.Exists(assetsDir))
        {
            Console.WriteLine($"Assets directory not found: {assetsDir}");
            return;
        }

        var unitSpriteDir = Path.Combine(assetsDir, "units");
        var royalBackdropDir = Path.Combine(assetsDir, "world", "royal");
        var paintedMapDir = Path.Combine(assetsDir, "world", "royal", "maps");
        var royalItemDir = Path.Combine(assetsDir, "ui", "royal", "items");
        var structureDir = Path.Combine(assetsDir, "structures");
        var particleDir = Path.Combine(assetsDir, "particles");
        var musicDir = Path.Combine(assetsDir, "music");
        var sfxDir = Path.Combine(assetsDir, "sfx");

        var unitIconDir = Path.Combine(assetsDir, "ui", "icons", "units");
        var rewardIconDir = Path.Combine(assetsDir, "ui", "icons", "rewards");
        var metaIconDir = Path.Combine(assetsDir, "ui", "icons", "meta");
        var codexPortraitDir = Path.Combine(assetsDir, "ui", "portraits", "codex");

        var visualClasses = units
            .Select(unit => AssetCoverageCatalog.NormalizeId(GetStr(unit, "VisualClass")))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Console.WriteLine(BuildCoverageLine("Unit sprites", visualClasses, id => HasAnyFile(unitSpriteDir, id, ".png"), "assets/units/{visual_class}.png"));

        Console.WriteLine(BuildCoverageLine("Zone battle backdrops", AssetCoverageCatalog.RouteIds, id => HasAnyFile(royalBackdropDir, id, ".json"), "assets/world/royal/{zone_id}.json"));
        Console.WriteLine(BuildCoverageLine("Painted maps", AssetCoverageCatalog.RouteIds, id => HasAnyFile(paintedMapDir, id, ".jpg"), "assets/world/royal/maps/{zone_id}.jpg"));
        Console.WriteLine(BuildCoverageLine("Structures", AssetCoverageCatalog.StructureIds, id => HasAnyFile(structureDir, id, ".png"), "assets/structures/{structure_id}.png"));
        Console.WriteLine(BuildCoverageLine("Particle textures", AssetCoverageCatalog.ParticleTextureIds, id => HasAnyFile(particleDir, id, ".png"), "assets/particles/{particle_id}.png"));

        Console.WriteLine(BuildCoverageLine(
            "Unit icons",
            units.Select(unit => GetStr(unit, "Id")).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            unitId => HasUnitIconCoverage(unitIconDir, unitId, units),
            "assets/ui/icons/units/{unit_id}.png (or {visual_class}.png)"));
        Console.WriteLine(BuildCoverageLine(
            "Spell pictures",
            spells.Select(spell => GetStr(spell, "Id")).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            spellId => HasAnyFile(royalItemDir, spellId, ".png"),
            "assets/ui/royal/items/{spell_id}.png"));
        Console.WriteLine(BuildCoverageLine(
            "Relic pictures",
            equipment.Select(item => GetStr(item, "Id")).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            relicId => HasAnyFile(royalItemDir, relicId, ".png"),
            "assets/ui/royal/items/{relic_id}.png"));
        Console.WriteLine(BuildCoverageLine(
            "Reward icons",
            AssetCoverageCatalog.RewardIconIds,
            rewardType => HasAnyFile(rewardIconDir, rewardType, ".png"),
            "assets/ui/icons/rewards/{reward_type}.png"));
        Console.WriteLine(BuildCoverageLine(
            "Meta icons",
            AssetCoverageCatalog.MetaIconIds,
            metaId => HasAnyFile(metaIconDir, metaId, ".png"),
            "assets/ui/icons/meta/{meta_id}.png"));
        // Units, spells and relics show their battle figure or painted picture; other codex foes need a portrait.
        var named = units.Select(unit => GetStr(unit, "Id")).Concat(spells.Select(spell => GetStr(spell, "Id"))).Concat(equipment.Select(item => GetStr(item, "Id")))
            .Concat(spells.Select(spell => GetStr(spell, "DisplayName"))).Concat(equipment.Select(item => GetStr(item, "DisplayName")))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Console.WriteLine(BuildCoverageLine(
            "Codex portraits",
            CodexCatalog.GetAll().Where(entry => !named.Contains(entry.Id) && !named.Contains(entry.Title)).Select(entry => entry.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            entryId => HasAnyFile(codexPortraitDir, entryId, ".png"),
            "assets/ui/portraits/codex/{entry_id}.png"));
        Console.WriteLine(BuildCoverageLine("Music tracks", AssetCoverageCatalog.MusicTrackIds, id => HasAnyFile(musicDir, id, ".ogg", ".mp3", ".wav"), "assets/music/{track_id}.(ogg|mp3|wav)"));
        Console.WriteLine(BuildCoverageLine("SFX overrides", AssetCoverageCatalog.SfxCueIds, id => HasAnyFile(sfxDir, id, ".ogg", ".mp3", ".wav"), "assets/sfx/{cue_id}.(ogg|mp3|wav)"));

    }

    private static string BuildCoverageLine(string label, IReadOnlyList<string> expectedIds, Func<string, bool> exists, string pattern)
    {
        if (expectedIds.Count == 0)
        {
            return $"{label}: 0/0";
        }

        var foundCount = expectedIds.Count(exists);
        var missingIds = expectedIds.Where(id => !exists(id)).Take(5).ToArray();
        var missingSuffix = missingIds.Length == 0
            ? string.Empty
            : $" | missing: {string.Join(", ", missingIds)}";
        return $"{label}: {foundCount}/{expectedIds.Count} | drop at {pattern}{missingSuffix}";
    }

    private static bool HasAnyFile(string directoryPath, string id, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(id) || extensions.Length == 0)
        {
            return false;
        }

        return extensions.Any(extension => File.Exists(Path.Combine(directoryPath, $"{id}{extension}")));
    }

    private static bool HasUnitIconCoverage(string unitIconDir, string unitId, JsonElement[] units)
    {
        if (HasAnyFile(unitIconDir, unitId, ".png"))
        {
            return true;
        }

        foreach (var unit in units)
        {
            if (!GetStr(unit, "Id").Equals(unitId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var visualClassId = AssetCoverageCatalog.NormalizeId(GetStr(unit, "VisualClass"));
            return HasAnyFile(unitIconDir, visualClassId, ".png");
        }

        return false;
    }

    private static bool HasStageModifier(JsonElement stage, string modifierType)
    {
        if (!stage.TryGetProperty("Modifiers", out var modifiers) || modifiers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var modifier in modifiers.EnumerateArray())
        {
            var type = GetStr(modifier, "Type");
            if (type.Equals(modifierType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static bool GetBool(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;
    }

    private static bool HasValidStoreProductId(string value, bool requireDot)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (requireDot && !value.Contains('.'))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(char.IsLower(c) || char.IsDigit(c) || c == '.' || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidStripePriceId(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.StartsWith("price_", StringComparison.OrdinalIgnoreCase)
            && !value.Any(char.IsWhiteSpace);
    }

    private static bool HaveMatchingFormatPlaceholders(string expected, string actual)
    {
        var expectedPlaceholders = new HashSet<string>(StringComparer.Ordinal);
        var actualPlaceholders = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in IndexedFormatPlaceholderPattern.Matches(expected))
        {
            if (match.Groups.Count > 1)
            {
                expectedPlaceholders.Add(match.Groups[1].Value);
            }
        }

        foreach (Match match in IndexedFormatPlaceholderPattern.Matches(actual))
        {
            if (match.Groups.Count > 1)
            {
                actualPlaceholders.Add(match.Groups[1].Value);
            }
        }

        return expectedPlaceholders.SetEquals(actualPlaceholders);
    }
}
