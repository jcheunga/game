using Godot;
using System;
using System.Linq;

public partial class BattleController
{
    private StageStarRating _endStarRating;
    private Button _convoyOrderButton, _assaultOrderButton, _bulwarkOrderButton, _rescueOrderButton, _breakthroughOrderButton;
	private void BuildUi()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		var canvasLayer = new CanvasLayer();
		AddChild(canvasLayer);

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		MedievalUi.Apply(root);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvasLayer.AddChild(root);

		var safeL = SafeAreaService.Instance?.MarginLeft ?? 0;
		var safeT = SafeAreaService.Instance?.MarginTop ?? 0;
		var safeR = SafeAreaService.Instance?.MarginRight ?? 0;
		var safeB = SafeAreaService.Instance?.MarginBottom ?? 0;

        BuildCompactHud(root);

		var spawnPanel = new PanelContainer
		{
			Position = new Vector2(16f + safeL, 554f - safeB),
			Size = new Vector2(1246f - safeL - safeR, 150f)
		};
		spawnPanel.SelfModulate = Colors.White;
		root.AddChild(spawnPanel);

		var spawnStack = new VBoxContainer();
		spawnStack.AddThemeConstantOverride("separation", 8);
		spawnPanel.AddChild(spawnStack);

		var unitRow = new HBoxContainer();
		unitRow.AddThemeConstantOverride("separation", 10);
		var cardScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 130) };
        spawnStack.AddChild(cardScroll); cardScroll.AddChild(unitRow);

		foreach (var definition in _deck.Roster)
		{
			var unit = definition;
			var button = new RealmButton
			{
				SizeFlagsHorizontal = Control.SizeFlags.Fill,
				CustomMinimumSize = new Vector2(112f, 124f)
			};
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			var card = AttachBattleCardContent(button, UiArtLoader.TryLoadUnitIcon(unit));
			button.Pressed += () => ArmPlayerUnit(unit);
			unitRow.AddChild(button);
			_deploySlots.Add(new DeploySlot(unit, button, card));
		}

		if (_spellDeck.Roster.Count > 0)
		{
			var spellRow = unitRow;

			foreach (var definition in _spellDeck.Roster)
			{
				var spell = definition;
				var button = new RealmButton
				{
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
					CustomMinimumSize = new Vector2(112f, 124f)
				};
				button.AddThemeColorOverride("font_color", Colors.White);
				button.AddThemeColorOverride("font_hover_color", Colors.White);
				button.AddThemeColorOverride("font_pressed_color", Colors.White);
				button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
				var card = AttachBattleCardContent(button, UiArtLoader.TryLoadSpellIcon(spell));
				button.Pressed += () => ArmSpell(spell);
				spellRow.AddChild(button);
				_spellSlots.Add(new SpellSlot(spell, button, card));
			}
		}

        BuildBattleMenu(root);

		_endCenter = new CenterContainer();
		_endCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_endCenter.Visible = false;
		root.AddChild(_endCenter);

		_endPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(760f, 540f),
			Visible = false
		};
		_endPanel.SelfModulate = Colors.White;
		_endCenter.AddChild(_endPanel);

		var endPadding = new MarginContainer();
		_endPadding = endPadding;
		endPadding.AddThemeConstantOverride("margin_left", 20);
		endPadding.AddThemeConstantOverride("margin_right", 20);
		endPadding.AddThemeConstantOverride("margin_top", 20);
		endPadding.AddThemeConstantOverride("margin_bottom", 20);
		_endPanel.AddChild(endPadding);

		var endVBox = new VBoxContainer();
		_endContent = endVBox;
		endVBox.AddThemeConstantOverride("separation", 12);
		endVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		endPadding.AddChild(endVBox);
		_endStarRating = new StageStarRating { Visible = false };
		endVBox.AddChild(_endStarRating);

		var endScroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 260f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_endReportScroll = endScroll;
		endVBox.AddChild(endScroll);

		_endLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_endLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		_endLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		endScroll.AddChild(_endLabel);


		_endPrimaryButton = new RealmButton
		{
			Text = IsEndlessMode
				? "Restart Run"
					: IsLanRaceMode
						? "Room Rematch"
						: IsOnlineRoomMode
							? "Back To Online Room"
						: IsChallengeMode
							? "Retry Challenge"
						: "Retry Stage",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		ApplyBattleButtonTheme(_endPrimaryButton, route);
		_endPrimaryButton.Pressed += HandleEndPanelPrimaryAction;
        if (!IsLanRaceMode && !IsOnlineRoomMode) StyleRestartButton(_endPrimaryButton);
        _endRetryMessage = RealmUi.Label("", 18, true); _endRetryMessage.Visible = false; endVBox.AddChild(_endRetryMessage);
		endVBox.AddChild(_endPrimaryButton);

		_endSecondaryButton = new RealmButton
		{
			Text = IsEndlessMode
				? "Back To Endless Prep"
					: IsLanRaceMode
						? "Back To Multiplayer"
						: IsOnlineRoomMode
							? "Leave Online Room"
						: IsChallengeMode
							? "Back To Multiplayer"
						: IsTowerMode ? "Back To Tower" : IsArenaMode ? "Back To Arena" : IsSeasonalEventMode ? "Back To Event" : "Back To Map",
			CustomMinimumSize = new Vector2(0f, 48f)
		};
		ApplyBattleButtonTheme(_endSecondaryButton, route);
		_endSecondaryButton.Pressed += HandleEndPanelSecondaryAction;
		endVBox.AddChild(_endSecondaryButton);

		_draftCenter = new CenterContainer();
		_draftCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_draftCenter.Visible = false;
		root.AddChild(_draftCenter);

		_draftPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(560f, 320f),
			Visible = false
		};
		_draftPanel.SelfModulate = Colors.White;
		_draftCenter.AddChild(_draftPanel);

		var draftPadding = new MarginContainer();
		draftPadding.AddThemeConstantOverride("margin_left", 20);
		draftPadding.AddThemeConstantOverride("margin_right", 20);
		draftPadding.AddThemeConstantOverride("margin_top", 20);
		draftPadding.AddThemeConstantOverride("margin_bottom", 20);
		_draftPanel.AddChild(draftPadding);

		var draftVBox = new VBoxContainer();
		draftVBox.AddThemeConstantOverride("separation", 12);
		draftPadding.AddChild(draftVBox);

		_draftLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_draftLabel.AddThemeColorOverride("font_color", route.BannerAccent);
		draftVBox.AddChild(_draftLabel);

		for (var i = 0; i < 3; i++)
		{
			var draftIndex = i;
			var draftButton = new RealmButton
			{
				CustomMinimumSize = new Vector2(0f, 56f)
			};
			ApplyBattleButtonTheme(draftButton, route);
			draftButton.Pressed += () => ApplyEndlessDraftChoice(draftIndex);
			draftVBox.AddChild(draftButton);
			_draftButtons.Add(draftButton);
		}


		ConfigureBattleCamera(root);
		ConfigureMobileBattleUi(root,spawnPanel,unitRow);
        ConfigureCompactHudLayout(spawnPanel,unitRow);
		BuildFieldNavigation(root);
		BuildCardDragPreview(root);
		ApplyDevUiSettings();
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private void TryShowTutorialHint(string context)
	{
		if (!GameState.Instance.ShowHints)
		{
			return;
		}

		var hints = TutorialHintCatalog.GetByContext(context);
		foreach (var hint in hints)
		{
			if (GameState.Instance.HasSeenHint(hint.Id))
			{
				continue;
			}

			SetStatus($"[{hint.Title}] {hint.Body}");
			GameState.Instance.MarkHintSeen(hint.Id);
		}
	}

	private static void ApplyBattleButtonTheme(Button button, RouteDefinition route)
	{
		button.SelfModulate = Colors.White;
		button.AddThemeColorOverride("font_color", route.BannerAccent);
		button.AddThemeColorOverride("font_hover_color", route.BannerAccent.Lightened(0.08f));
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.45f));
	}

	private static BattleActionCard AttachBattleCardContent(Button button, Texture2D icon)
	{
		button.Text = string.Empty;
		var card = new BattleActionCard();
		button.AddChild(card);
		card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		card.SetIcon(icon);
		return card;
	}

	private void UpdateHud()
	{
        if (_convoyOrderButton != null)
        {
            _convoyOrderButton.Visible = _campaignConvoyCommandReady && !_campaignConvoyCommandTriggered;
            _convoyOrderButton.Disabled = !_campaignConvoyCommandReady || _campaignConvoyCommandTriggered;
            _convoyOrderButton.TooltipText = _campaignConvoyCommandTriggered ? "Caravan command spent" :
                _campaignConvoyCommandReady ? "Rally your troops [C]" : $"Caravan command ready in {_campaignConvoyCommandChargeRemaining:0}s";
            _assaultOrderButton.Visible = _bulwarkOrderButton.Visible = _campaignFieldOrderReady && !_campaignFieldOrderCommitted;
            _rescueOrderButton.Visible = _breakthroughOrderButton.Visible = _campaignAdaptiveWaveChoiceReady && !_campaignAdaptiveWaveChoiceUsed;
        }
		_baseWeaponsIntel.Text = BuildBaseWeaponsIntel();
		_battleBannerLabel.Text = IsEndlessMode ? $"Endless · Wave {_spawnDirector.EndlessWaveNumber}" : $"{_stage:00} · {_stageData.StageName}";
		_battleSubtitleLabel.Text = BuildBattleBannerSubtitle();
		_battleMissionLabel.Text = BuildBattleBannerStatusText();
        _baseHealthLabel.Text = $"Wagon  {Mathf.CeilToInt(_playerBaseHealth)} / {Mathf.CeilToInt(_playerBaseMaxHealth)}" +
            (IsEndlessMode ? "" : $"     Gate  {(_enemyBaseHealth <= 0 ? "Breached" : $"{Mathf.CeilToInt(_enemyBaseHealth)} / {Mathf.CeilToInt(_enemyBaseMaxHealth)}")}");
		_resourceLabel.Text = IsEndlessMode
			? $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Endless wave {_spawnDirector.EndlessWaveNumber}   |   Best {GameState.Instance.BestEndlessWave}"
			: IsChallengeMode
				? $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Challenge Stage {_stage}   |   Best {GameState.Instance.GetAsyncChallengeBestScore(_challengeDefinition.Code)}"
				: $"Courage: {Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}   |   Stage {_stage}";
		if (IsCampaignMode && _campaignMomentumBoostRemaining > 0.05f)
		{
			_resourceLabel.Text += $"   |   Momentum x{_campaignMomentumStacks} ({Mathf.CeilToInt(_campaignMomentumBoostRemaining)}s)";
		}
		if (IsCampaignMode)
		{
			_resourceLabel.Text += _campaignReserveReady
				? "   |   Reserve ready"
				: _campaignReserveTriggered
					? "   |   Reserve spent"
					: "";
			_resourceLabel.Text += _campaignConvoyCommandReady
				? "   |   Command [C] ready"
				: _campaignConvoyCommandTriggered
					? "   |   Command spent"
					: $"   |   Command {_campaignConvoyCommandChargeRemaining:0.0}s";
			_resourceLabel.Text += _campaignFieldOrderReady
				? "   |   Order [Z/X] ready"
				: _campaignFieldOrderCommitted
					? "   |   Order spent"
					: "";
			if (_campaignDoctrineThreshold > 0)
			{
				_resourceLabel.Text += $"   |   Doctrine {_campaignDoctrineDefeatProgress}/{_campaignDoctrineThreshold}";
			}
		}
		if (IsCampaignMode && _campaignScoutBoostRemaining > 0.05f)
		{
			_resourceLabel.Text += $"   |   Scout +{Mathf.RoundToInt((_campaignScoutCourageGainScale - 1f) * 100f)}% ({Mathf.CeilToInt(_campaignScoutBoostRemaining)}s)";
		}
		if (IsChallengeMode && _challengeMutator.SignalJamIntervalSeconds > 0.05f && _enemySignalJamTimer <= 0.05f)
		{
			_resourceLabel.Text += $"   |   Next blackout {_challengeMutatorNextJamTimer:0.0}s";
		}
		if (_enemySignalJamTimer > 0.05f)
		{
			_resourceLabel.Text += $"   |   Signal jam {_enemySignalJamTimer:0.0}s";
		}
		var waveStatus = _spawnDirector.IsEndlessMode
			? $"   |   Pending surge: {Mathf.Max(0f, _spawnDirector.NextEndlessWaveTime - _elapsed):0.0}s   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
			: _spawnDirector.UsesScriptedWaves
				? $"   |   Waves: {_spawnDirector.NextScriptedWaveIndex}/{_spawnDirector.TotalScriptedWaves}   |   Queued spawns: {_spawnDirector.PendingSpawnCount}"
				: "";
		_timerLabel.Text =
			$"{(int)_elapsed / 60:00}:{(int)_elapsed % 60:00}";
		_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        _healthBar.SetValue(_playerBaseMaxHealth > 0 ? _playerBaseHealth / _playerBaseMaxHealth : 0,
            $"{Mathf.Max(0, Mathf.CeilToInt(_playerBaseHealth))} / {Mathf.CeilToInt(_playerBaseMaxHealth)}");
        _goldAmount.Text = GameState.Instance.Gold.ToString("N0");
        _healthBar.AccessibilityName = $"War wagon health, {Mathf.Max(0, Mathf.CeilToInt(_playerBaseHealth))} of {Mathf.CeilToInt(_playerBaseMaxHealth)}";
        _courageBar.AccessibilityName = $"Courage, {Mathf.FloorToInt(_courage)} of {Mathf.FloorToInt(_maxCourage)}";
		_courageBar.SetValue(
			_maxCourage > 0.01f ? _courage / _maxCourage : 0f,
			$"{Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}");
		var waveRatio = _spawnDirector.IsEndlessMode
			? 0f
			: _spawnDirector.UsesScriptedWaves && _spawnDirector.TotalScriptedWaves > 0
				? (float)_spawnDirector.NextScriptedWaveIndex / _spawnDirector.TotalScriptedWaves
				: 0f;
		_waveProgressBar.SetValue(
			waveRatio,
			_spawnDirector.IsEndlessMode
				? $"Endless wave {_spawnDirector.EndlessWaveNumber}"
				: _spawnDirector.UsesScriptedWaves
					? $"{(HasCampaignField && !MobilePresentation.Enabled ? _spawnDirector.CurrentEncounterArea + " · " : "")}{_spawnDirector.NextScriptedWaveIndex}/{_spawnDirector.TotalScriptedWaves}"
					: "");
		_waveProgressBar.Visible = false;
		_waveIntelLabel.Text = BuildWaveIntelText();
		if (IsEndlessMode)
		{
			_objectiveStatusLabel.Text = BuildEndlessStatusText();
		}
		else
		{
			var objectiveText = StageObjectives.BuildLiveSummary(_stageData, BuildStageBattleResult());
			var missionText = BuildStageMissionEventText();
			_objectiveStatusLabel.Text = string.IsNullOrWhiteSpace(missionText)
				? objectiveText
				: $"{objectiveText}\n{missionText}";
		}

		foreach (var slot in _deploySlots)
		{
			var cooldown = _deck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= slot.Definition.Cost;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;
			var level = GameState.Instance.GetUnitLevel(slot.Definition.Id);

			var armed = _selectionMode == BattleSelectionMode.Unit && slot.Definition == _deck.ArmedUnit;
			slot.Button.SelfModulate = ResolveDeployButtonTint(slot.Definition, isReady, hasCourage, armed);
			slot.Button.TooltipText = BuildDeployButtonTooltip(slot.Definition, level, isReady, cooldown);
			var totalCd = ResolvePlayerDeployCooldown(slot.Definition);
			slot.Card.SetState(slot.Definition.Cost, _courage, cooldown, totalCd, armed, _battleEnded || _endlessCheckpointActive);
			slot.Button.AccessibilityName = $"{slot.Definition.DisplayName}, {slot.Definition.Cost} courage";
			slot.Button.AccessibilityDescription = $"Level {level}. " + (!isReady ? $"Cooldown {cooldown:0.0} seconds." : !hasCourage ? "Not enough courage." : armed ? "Selected. Choose a position on the battlefield." : "Ready. Select to place on the battlefield.");
		}

		foreach (var slot in _spellSlots)
		{
			var resolved = GameState.Instance.BuildSpellStats(slot.Definition);
			var cooldown = _spellDeck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= resolved.CourageCost;
			var armed = _selectionMode == BattleSelectionMode.Spell && slot.Definition == _spellDeck.ArmedSpell;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;

			slot.Button.SelfModulate = ResolveSpellButtonTint(slot.Definition, isReady, hasCourage, armed);
			slot.Button.TooltipText = SpellText.BuildTooltipSummary(slot.Definition, resolved, isReady, cooldown);
			var totalSpellCd = ResolvePlayerSpellCooldown(slot.Definition, resolved);
			slot.Card.SetState(resolved.CourageCost, _courage, cooldown, totalSpellCd, armed, _battleEnded || _endlessCheckpointActive);
			slot.Button.AccessibilityName = $"{slot.Definition.DisplayName}, {resolved.CourageCost} courage";
			slot.Button.AccessibilityDescription = $"Level {resolved.Level}. " + (!isReady ? $"Cooldown {cooldown:0.0} seconds." : !hasCourage ? "Not enough courage." : armed ? "Selected. Choose a target on the battlefield." : "Ready. Select to cast.");
		}
		_hudLayout?.Invoke();
        RefreshMobileHud();
	}

	private string BuildBattleBannerTitle()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		return IsEndlessMode
			? $"Endless Hold  |  {route.Title}"
			: IsChallengeMode
				? $"Challenge {_challengeDefinition.Code}  |  {route.Title}"
				: $"Stage {_stage}  |  {route.Title}";
	}

	private string BuildBattleBannerSubtitle()
	{
		var route = RouteCatalog.Get(_activeRouteId);
		return IsEndlessMode
			? $"Frontline: {_stageData.StageName}\nPath: {EndlessRouteForkCatalog.Get(_endlessRouteForkId).Title}  |  Pressure: {route.PressureSummary}"
			: $"{_stageData.StageName}\nPressure: {route.PressureSummary}";
	}

	private string BuildBattleBannerStatusText()
	{
		if (IsEndlessMode)
		{
			return $"Battlefield event: {_endlessBattlefieldEventLabel}\nCaravan support: {_endlessSupportEventLabel}";
		}

		var missionSummary = BuildStageMissionIntelText().Trim();
		if (string.IsNullOrWhiteSpace(missionSummary))
		{
			missionSummary = $"Battlefield pressure: {StageEncounterIntel.BuildSupportPressureSummary(_stageData)}";
		}

		if (!IsChallengeMode)
		{
			return missionSummary;
		}

		var mutatorText = BuildChallengeMutatorText();
		return string.IsNullOrWhiteSpace(mutatorText)
			? missionSummary
			: $"{mutatorText}\n{missionSummary}";
	}

	private void OnShowDevUiToggled(bool enabled)
	{
		GameState.Instance.SetShowDevUi(enabled);
        _combatIntelExpanded = enabled;
		ApplyDevUiSettings();
	}

	private void OnShowFpsToggled(bool enabled)
	{
		GameState.Instance.SetShowFpsCounter(enabled);
		ApplyDevUiSettings();
	}

    private bool _combatIntelExpanded;
    private void ApplyDevUiSettings()
    {
        _topHudPanel.Visible = true;
        _intelPanel.Visible = false;
        _timerLabel.Visible = false;
        _statusLabel.Visible = false;
        _waveProgressBar.Visible = false;
        _fpsLabel.Visible = GameState.Instance.ShowFpsCounter;
        _showDevUiToggle.SetPressedNoSignal(_combatIntelExpanded);
    }

    private void ToggleCombatIntel()
    {
        _combatIntelExpanded = false;
        ApplyDevUiSettings();
    }

}
