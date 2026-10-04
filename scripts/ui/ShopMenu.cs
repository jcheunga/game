using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ShopMenu : Control
{
    private PanelContainer _summaryPanel = null !;
    private PanelContainer _unitsPanel = null !;
    private PanelContainer _basePanel = null !;
    private PanelContainer _relicsPanel = null !;
    private Container _resourcesRow = null !;
    private GameState _resourceState;
    private Label _statusLabel = null !;
    private VBoxContainer _recommendationStack = null !;
    private VBoxContainer _baseStack = null !;
    private VBoxContainer _relicsStack = null !;
    public override void _Ready()
    {
        BuildModalUi();
        _resourceState = GameState.Instance;
        _resourceState.FoodChanged += RebuildResourcesRow;
        RefreshUi();
        SelectArmoryTab(SceneRouter.Instance.ConsumeInitialShopTab());
        TryShowMenuHint();
    }

    public override void _ExitTree()
    {
        if (_resourceState != null)
            _resourceState.FoodChanged -= RebuildResourcesRow;
    }

    private void TryShowMenuHint()
    {
        if (!GameState.Instance.ShowHints)
        {
            return;
        }

        var hints = TutorialHintCatalog.GetByContext("first_shop");
        foreach (var hint in hints)
        {
            if (GameState.Instance.HasSeenHint(hint.Id))
            {
                continue;
            }

            _statusLabel.Text = $"{hint.Title}: {hint.Body}";
            GameState.Instance.MarkHintSeen(hint.Id);
        }
    }

    private VBoxContainer _unitDetail;
    private string _selectedRosterId = "";
    private bool _showSpells;
    private HBoxContainer _armoryTabs;
    private void SelectArmoryTab(int index)
    {
        _unitsPanel.Visible = index < 2;
        _basePanel.Visible = index == 2;
        _relicsPanel.Visible = index == 3;
        _summaryPanel.Visible = index == 4;
        _armoryTabs.GetChild<Button>(index).ButtonPressed = true;
        if (index < 2)
        {
            _showSpells = index == 1;
            _selectedRosterId = "";
            _profileExpanded = false;
            RebuildUnitPanels();
        }

        {
            RealmModal.UpdateHeading(this, index == 0 ? "Warband" : index == 1 ? "Spells" : index == 2 ? "War wagon" : index == 3 ? "Relics" : "Caravan adviser");
            _statusLabel.Text = "";
        }
    }

    private void RefreshUi()
    {
        {
            RefreshModalUi();
            return;
        }
    }

    private void RebuildResourcesRow()
    {
        RealmUi.Clear(_resourcesRow);
        var state = GameState.Instance;
        {
            foreach (var(icon, name, amount)in new[]
            {
                ("gold", "Gold", state.Gold),
                ("food", "Rations", state.Food),
                ("sigils", "Sigils", state.Sigils),
                ("tomes", "Tomes", state.Tomes),
                ("shards", "Shards", state.RelicShards),
                ("essence", "Essence", state.Essence)
            }

            )
            {
                var balance = HomeResourceUi.Amount(icon, amount.ToString("N0"), $"{name}: {amount:N0}", 28);
                balance.GetChild<Label>(1).AddThemeFontSizeOverride("font_size", 20);
                balance.Name = "Balance" + icon;
                balance.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                _resourcesRow.AddChild(balance);
            }

            return;
        }
    }

    private string BuildRouteIntelText()
    {
        var selectedStage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var route = RouteCatalog.Get(selectedStage.MapId);
        var upcomingStages = GameData.GetStagesForMap(selectedStage.MapId).Where(stage => stage.StageNumber >= selectedStage.StageNumber).Take(3).ToArray();
        var intel = $"Selected route: {route.Title}\n" + $"{route.CampaignSubtitle}\n" + $"Pressure profile: {route.PressureSummary}\n" + $"Current target: Stage {selectedStage.StageNumber} - {selectedStage.StageName}\n" + $"Deploy cost: {GameState.Instance.GetStageEntryFoodCost(selectedStage.StageNumber)} food · Clear reward: +{selectedStage.RewardGold} gold, +{selectedStage.RewardFood} food\n" + $"{GameState.Instance.BuildCampaignDirectiveStatusText(selectedStage.StageNumber)}\n" + $"{GameState.Instance.BuildCampaignReadinessDetailedSummary(selectedStage.StageNumber)}\n" + $"{StageMissionEvents.BuildSummaryText(selectedStage)}";
        if (TryGetNextStageForMap(selectedStage.MapId, out var nextRouteStage))
        {
            intel += $"\nNext route exploration: Stage {nextRouteStage.StageNumber}";
        }
        else
        {
            intel += "\nNext route exploration: Route fully explored";
        }

        if (upcomingStages.Length > 0)
        {
            intel += "\n\nUpcoming route stops:";
            foreach (var stage in upcomingStages)
            {
                var unlocked = stage.StageNumber <= GameState.Instance.HighestUnlockedStage ? "Ready" : "Locked";
                intel += $"\nS{stage.StageNumber} {stage.StageName} · {unlocked}" + $"\n  Entry {GameState.Instance.GetStageEntryFoodCost(stage.StageNumber)} food · Reward +{stage.RewardGold}g / +{stage.RewardFood}f";
            }
        }

        var pendingUnits = GameData.GetPlayerUnits().Where(unit => !GameState.Instance.IsUnitOwned(unit.Id)).OrderBy(unit => unit.UnlockStage).Take(2).ToArray();
        if (pendingUnits.Length > 0)
        {
            intel += "\n\nNext unit unlocks:";
            foreach (var unit in pendingUnits)
            {
                var unlockStage = GameData.GetStage(Mathf.Clamp(unit.UnlockStage, 1, GameState.Instance.MaxStage));
                var unlockState = GameState.Instance.IsUnitAvailableForPurchase(unit.Id) ? $"Shop unlocked · {GameState.Instance.GetUnitPurchaseCost(unit.Id)} gold" : $"Win stage {unit.UnlockStage - 1}+";
                intel += $"\n{unit.DisplayName} - {unlockStage.MapName} S{unit.UnlockStage} · {unlockState}";
            }
        }

        var pendingSpells = GameData.GetPlayerSpells().Where(spell => !GameState.Instance.IsSpellOwned(spell.Id)).OrderBy(spell => spell.UnlockStage).Take(2).ToArray();
        if (pendingSpells.Length > 0)
        {
            intel += "\n\nNext spell unlocks:";
            foreach (var spell in pendingSpells)
            {
                var unlockStage = GameData.GetStage(Mathf.Clamp(spell.UnlockStage, 1, GameState.Instance.MaxStage));
                var unlockState = GameState.Instance.IsSpellAvailableForPurchase(spell.Id) ? $"Archive open · {GameState.Instance.GetSpellPurchaseCost(spell.Id)} gold" : $"Win stage {spell.UnlockStage - 1}+";
                intel += $"\n{spell.DisplayName} - {unlockStage.MapName} S{spell.UnlockStage} · {unlockState}";
            }
        }

        return intel;
    }

    private bool TryGetNextStageForMap(string mapId, out StageDefinition stage)
    {
        foreach (var routeStage in GameData.GetStagesForMap(mapId))
        {
            if (routeStage.StageNumber <= GameState.Instance.HighestUnlockedStage)
            {
                continue;
            }

            stage = routeStage;
            return true;
        }

        stage = GameData.GetLatestStageForMap(mapId);
        return false;
    }

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

    private void RebuildUnitPanels()
    {
        {
            RebuildModalRoster();
            return;
        }
    }

    private Control BuildUnitPanel(UnitDefinition unit)
    {
        var owned = GameState.Instance.IsUnitOwned(unit.Id);
        var available = GameState.Instance.IsUnitAvailableForPurchase(unit.Id);
        var inDeck = owned && GameState.Instance.IsUnitInActiveDeck(unit.Id);
        var level = GameState.Instance.GetUnitLevel(unit.Id);
        var purchaseCost = GameState.Instance.GetUnitPurchaseCost(unit.Id);
        var upgradeCost = GameState.Instance.GetUnitUpgradeCost(unit.Id);
        var isMaxLevel = level >= GameState.Instance.MaxUnitLevel;
        var doctrineOptions = GameState.Instance.GetUnitDoctrineOptions(unit.Id);
        var currentDoctrineId = GameState.Instance.GetUnitDoctrineId(unit.Id);
        var doctrineUnlocked = owned && GameState.Instance.IsUnitDoctrineUnlocked(unit.Id);
        var doctrineRetrainCost = GameState.Instance.GetUnitDoctrineRetrainCost(unit.Id);
        var role = SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag);
        var statusLine = owned ? $"Lv {level} · {role} · {(inDeck ? "Equipped" : "Reserve")}" : $"{role} · {(available ? "Recruit" : $"Stage {unit.UnlockStage:00}")}";
        var panel = DetailShell(unit.DisplayName, statusLine, unit.Id, false, out var stack);
        stack.AddChild(ArmoryDetailUi.Stats(ArmoryDetailUi.UnitStats(unit)));
        var extra = UnitExtraDetails(stack, unit, owned, level, isMaxLevel);
        var row = new HBoxContainer
        {
            Name = "RosterActions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);
        var deckButton = new RealmButton
        {
            Text = inDeck ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Disabled = !owned,
            Visible = owned
        };
        deckButton.Pressed += () =>
        {
            GameState.Instance.ToggleDeckUnit(unit.Id, out var message);
            GameState.Instance.SetSelectedStage(GameState.Instance.SelectedStage);
            _statusLabel.Text = $"{message}";
            RefreshUi();
        };
        row.AddChild(deckButton);
        var actionButton = new RealmButton
        {
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        if (!available)
        {
            actionButton.Text = $"Unlocks at stage {unit.UnlockStage}";
            actionButton.Disabled = true;
        }
        else if (!owned)
        {
            ArmoryDetailUi.GoldAction(actionButton, "Buy", purchaseCost);
            actionButton.Disabled = GameState.Instance.Gold < purchaseCost;
            actionButton.Pressed += () =>
            {
                GameState.Instance.TryPurchaseUnit(unit.Id, out var message);
                _statusLabel.Text = $"{message}";
                RefreshUi();
            };
        }
        else if (isMaxLevel)
        {
            var promo = UnitPromotionCatalog.TryGet(unit.Id);
            var promoted = GameState.Instance.IsUnitPromoted(unit.Id);
            if (promo != null && !promoted)
            {
                actionButton.Text = $"Promote: {promo.GoldCost}g + {promo.SigilCost} sigil";
                actionButton.Disabled = !GameState.Instance.CanPromoteUnit(unit.Id);
                actionButton.Pressed += () =>
                {
                    if (GameState.Instance.TryPromoteUnit(unit.Id, out var message))
                    {
                        AudioDirector.Instance?.PlayUpgradeConfirm();
                    }

                    _statusLabel.Text = $"{message}";
                    RefreshUi();
                };
            }
            else
            {
                actionButton.Text = promoted ? $"{promo?.PromotedTitle ?? "Promoted"}" : "Max level";
                actionButton.Disabled = true;
            }

            // Skill tree button for promoted or max-level units
            var tree = UnitSkillTreeCatalog.GetTree(unit.Id);
            if (tree != null && owned)
            {
                var unlockedCount = GameState.Instance.GetUnlockedSkillNodes(unit.Id).Count;
                var talentBtn = new RealmButton
                {
                    Text = $"Talents ({unlockedCount}/{tree.Nodes.Length})",
                    CustomMinimumSize = new Vector2(130f, 0f)
                };
                talentBtn.Pressed += () => SceneRouter.Instance.GoToSkillTree();
                extra.AddChild(talentBtn);
            }
        }
        else
        {
            ArmoryDetailUi.GoldAction(actionButton, "Upgrade", upgradeCost);
            actionButton.Disabled = GameState.Instance.Gold < upgradeCost;
            actionButton.Pressed += () =>
            {
                if (GameState.Instance.TryUpgradeUnit(unit.Id, out var message))
                {
                    AudioDirector.Instance?.PlayUpgradeConfirm();
                }

                _statusLabel.Text = $"{message}";
                RefreshUi();
            };
        }

        row.AddChild(actionButton);
        if (owned)
        {
            var prestigeVariants = PrestigeColorCatalog.GetUnlockedVariants(unit.Id);
            if (prestigeVariants.Count > 0)
            {
                var currentPrestige = GameState.Instance.GetUnitPrestigeIndex(unit.Id);
                var currentVariant = currentPrestige > 0 ? PrestigeColorCatalog.GetVariant(unit.Id, currentPrestige) : null;
                var colorLabel = currentVariant != null ? currentVariant.Title : "Default";
                var colorButton = new RealmButton
                {
                    Text = $"Color: {colorLabel}",
                    CustomMinimumSize = new Vector2(130f, 0f)
                };
                colorButton.Pressed += () =>
                {
                    var unlocked = PrestigeColorCatalog.GetUnlockedVariants(unit.Id);
                    var current = GameState.Instance.GetUnitPrestigeIndex(unit.Id);
                    var next = 0;
                    var foundCurrent = current == 0;
                    foreach (var v in unlocked)
                    {
                        if (foundCurrent)
                        {
                            next = v.PrestigeIndex;
                            break;
                        }

                        if (v.PrestigeIndex == current)
                        {
                            foundCurrent = true;
                        }
                    }

                    GameState.Instance.SetUnitPrestigeIndex(unit.Id, next);
                    RefreshUi();
                };
                extra.AddChild(colorButton);
            }
        }

        if (doctrineUnlocked && doctrineOptions.Count > 0)
        {
            var doctrineRow = new HBoxContainer();
            doctrineRow.AddThemeConstantOverride("separation", 8);
            extra.AddChild(RealmUi.Label("Doctrine", 18, true));
            extra.AddChild(doctrineRow);
            foreach (var doctrine in doctrineOptions)
            {
                var isSelected = currentDoctrineId.Equals(doctrine.Id, StringComparison.OrdinalIgnoreCase);
                var doctrineButton = new RealmButton
                {
                    Text = isSelected ? $"{doctrine.Title} ✓" : string.IsNullOrWhiteSpace(currentDoctrineId) ? $"Choose {doctrine.Title}" : $"{doctrine.Title} · {doctrineRetrainCost} gold",
                    Disabled = isSelected || (doctrineRetrainCost > 0 && GameState.Instance.Gold < doctrineRetrainCost),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                doctrineButton.Pressed += () =>
                {
                    GameState.Instance.TrySelectUnitDoctrine(unit.Id, doctrine.Id, out var message);
                    _statusLabel.Text = $"{message}";
                    RefreshUi();
                };
                doctrineRow.AddChild(doctrineButton);
            }
        }

        return panel;
    }

    private Control BuildSpellPanel(SpellDefinition spell)
    {
        var owned = GameState.Instance.IsSpellOwned(spell.Id);
        var available = GameState.Instance.IsSpellAvailableForPurchase(spell.Id);
        var equipped = owned && GameState.Instance.IsSpellInActiveDeck(spell.Id);
        var purchaseCost = GameState.Instance.GetSpellPurchaseCost(spell.Id);
        var resolved = GameState.Instance.BuildSpellStats(spell);
        var role = ArmoryDetailUi.SpellRole(spell.EffectType);
        // One status line: the action buttons already show ownership and equip state.
        var purpose = ArmoryDetailUi.SpellPurpose(spell.EffectType);
        var statusLine = owned ? $"Lv {resolved.Level} · {(purpose.Length > 0 ? purpose : role)}" : purpose.Length > 0 ? purpose : role;
        var panel = DetailShell(spell.DisplayName, statusLine, spell.Id, true, out var stack);
        var metrics = ArmoryDetailUi.SpellStats(resolved);
        stack.AddChild(ArmoryDetailUi.Stats(metrics, metrics.Count <= 4 ? 2 : 3));
        var extra = ArmoryDetailUi.Disclosure(stack, "Effects & training", _profileExpanded, value => _profileExpanded = value);
        extra.AddChild(RealmUi.Label(spell.Description, 18));
        if (owned && resolved.Level < GameState.Instance.MaxSpellLevel)
        {
            extra.AddChild(RealmUi.Label($"Next level · {resolved.Level + 1}", 18, true));
            var next = ArmoryDetailUi.SpellStats(new ResolvedSpellStats(spell, resolved.Level + 1));
            extra.AddChild(ArmoryDetailUi.Stats(next, next.Count <= 4 ? 2 : 3));
        }

        var row = new HBoxContainer
        {
            Name = "RosterActions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);
        var deckButton = new RealmButton
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Disabled = !owned,
            Visible = owned
        };
        deckButton.Pressed += () =>
        {
            GameState.Instance.ToggleDeckSpell(spell.Id, out var message);
            _statusLabel.Text = $"{message}";
            RefreshUi();
        };
        row.AddChild(deckButton);
        var actionButton = new RealmButton
        {
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        if (!available)
        {
            actionButton.Text = $"Unlocks at stage {spell.UnlockStage}";
            actionButton.Disabled = true;
        }
        else if (!owned)
        {
            if (purchaseCost > 0)
                ArmoryDetailUi.GoldAction(actionButton, "Scribe", purchaseCost);
            else
                actionButton.Text = "Prepare";
            actionButton.Disabled = GameState.Instance.Gold < purchaseCost;
            actionButton.Pressed += () =>
            {
                GameState.Instance.TryPurchaseSpell(spell.Id, out var message);
                _statusLabel.Text = $"{message}";
                RefreshUi();
            };
        }
        else
        {
            var spellLevel = GameState.Instance.GetSpellLevel(spell.Id);
            if (spellLevel < GameState.Instance.MaxSpellLevel)
            {
                var upgradeCost = GameState.Instance.GetSpellUpgradeCost(spell.Id);
                ArmoryDetailUi.GoldAction(actionButton, $"Upgrade Lv{spellLevel + 1}", upgradeCost);
                actionButton.Disabled = GameState.Instance.Gold < upgradeCost;
                actionButton.Pressed += () =>
                {
                    if (GameState.Instance.TryUpgradeSpell(spell.Id, out var message))
                    {
                        AudioDirector.Instance?.PlayUpgradeConfirm();
                    }

                    _statusLabel.Text = $"{message}";
                    RefreshUi();
                };
            }
            else
            {
                actionButton.Text = "Max level";
                actionButton.Disabled = true;
            }
        }

        row.AddChild(actionButton);
        return panel;
    }

    private void RebuildBaseUpgradePanels()
    {
        RealmUi.Clear(_baseStack);
        var upgradesHost = (Control)_baseStack;
        {
            var grid = new GridContainer
            {
                Columns = MobilePresentation.Enabled ? 1 : 2,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            grid.AddThemeConstantOverride("h_separation", 14);
            grid.AddThemeConstantOverride("v_separation", 14);
            _baseStack.AddChild(grid);
            upgradesHost = grid;
        }

        foreach (var upgrade in BaseUpgradeCatalog.GetAll())
            upgradesHost.AddChild(BuildBaseUpgradePanel(upgrade));
    }

    private Control BuildBaseUpgradePanel(BaseUpgradeDefinition upgrade)
    {
        return BuildModalUpgrade(upgrade);
    }

    private void RebuildRelicPanels()
    {
        RealmUi.Clear(_relicsStack);
        var ownedRelicIds = GameState.Instance.GetOwnedEquipment();
        var allEquipment = GameData.GetAllEquipment();
        if (ownedRelicIds.Count == 0)
        {
            _relicsStack.AddChild(RealmUi.EmptyState("star", "No relics yet", "Relics drop from bosses, expeditions and the forge."));
            return;
        }

        var ownedRelics = allEquipment.Where(eq => ownedRelicIds.Contains(eq.Id)).ToArray();
        foreach (var relic in ownedRelics)
        {
            _relicsStack.AddChild(BuildRelicPanel(relic));
        }
    }

    private Control BuildRelicPanel(EquipmentDefinition relic)
    {
        var ownedUnits = GameState.Instance.GetOwnedPlayerUnits();
        var equippedByUnitId = "";
        var equippedByUnitName = "";
        foreach (var unit in ownedUnits)
        {
            var unitEquip = GameState.Instance.GetUnitEquipment(unit.Id);
            if (unitEquip != null && unitEquip.Id == relic.Id)
            {
                equippedByUnitId = unit.Id;
                equippedByUnitName = unit.DisplayName;
                break;
            }
        }

        var rarityColor = relic.Rarity.ToLowerInvariant() switch
        {
            "legendary" => new Color("ffd166"),
            "epic" => new Color("bb86fc"),
            "rare" => new Color("64b5f6"),
            _ => new Color("90a4ae")};
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 170f),
            SelfModulate = rarityColor.Darkened(0.65f)
        };
        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);
        var stack = UiBadgeFactory.CreateStackWithLeadingBadge(padding, UiBadgeFactory.CreateRelicBadge(relic, new Vector2(68f, 68f)), stackSpacing: 6);
        stack.AddChild(new Label { Text = $"{relic.DisplayName} · {relic.Rarity}" });
        var statParts = new List<string>();
        if (Mathf.Abs(relic.HealthScale - 1f) > 0.001f)
            statParts.Add($"HP x{relic.HealthScale:0.##}");
        if (Mathf.Abs(relic.DamageScale - 1f) > 0.001f)
            statParts.Add($"DMG x{relic.DamageScale:0.##}");
        if (Mathf.Abs(relic.CooldownReduction) > 0.001f)
            statParts.Add($"CD -{Mathf.RoundToInt(relic.CooldownReduction * 100f)}%");
        if (Mathf.Abs(relic.SpeedScale - 1f) > 0.001f)
            statParts.Add($"SPD x{relic.SpeedScale:0.##}");
        if (relic.BaseDamageBonus != 0)
            statParts.Add($"Base +{relic.BaseDamageBonus}");
        var statLine = statParts.Count > 0 ? string.Join(" · ", statParts) : "No stat bonuses";
        stack.AddChild(new Label { Text = statLine, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        stack.AddChild(new Label { Text = relic.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var statusText = string.IsNullOrEmpty(equippedByUnitId) ? "Unequipped" : $"Equipped on {equippedByUnitName}";
        stack.AddChild(new Label { Text = statusText, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        if (!string.IsNullOrEmpty(equippedByUnitId))
        {
            var unequipButton = new RealmButton
            {
                Text = $"Unequip from {equippedByUnitName}",
                CustomMinimumSize = new Vector2(0f, 32f)
            };
            var capturedUnitId = equippedByUnitId;
            unequipButton.Pressed += () =>
            {
                GameState.Instance.UnequipItem(capturedUnitId);
                _statusLabel.Text = $"Unequipped {relic.DisplayName} from {equippedByUnitName}.";
                RefreshUi();
            };
            stack.AddChild(unequipButton);
        }

        var equipRow = new HBoxContainer();
        equipRow.AddThemeConstantOverride("separation", 6);
        stack.AddChild(equipRow);
        foreach (var unit in ownedUnits)
        {
            var currentEquip = GameState.Instance.GetUnitEquipment(unit.Id);
            if (currentEquip != null && currentEquip.Id == relic.Id)
                continue;
            var equipButton = new RealmButton
            {
                Text = $"Equip {unit.DisplayName}",
                CustomMinimumSize = new Vector2(0f, 30f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            var capturedUnitId = unit.Id;
            var capturedUnitName = unit.DisplayName;
            equipButton.Pressed += () =>
            {
                GameState.Instance.TryEquipItem(capturedUnitId, relic.Id);
                _statusLabel.Text = $"Equipped {relic.DisplayName} on {capturedUnitName}.";
                RefreshUi();
            };
            equipRow.AddChild(equipButton);
        }

        return panel;
    }
}
