using Godot;
using System;
using System.Linq;

public partial class BattleController
{
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

        var topVBox = RealmUi.Panel(root, new Rect2(16 + safeL, 12 + safeT, 1248 - safeL - safeR, 76), out _topHudPanel);
        var topRow = new HBoxContainer(); topVBox.AddChild(topRow);
        var titleStack = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        titleStack.AddThemeConstantOverride("separation", 2); topRow.AddChild(titleStack);
        _battleBannerLabel = RealmUi.Label("", 17); titleStack.AddChild(_battleBannerLabel);
        _baseHealthLabel = RealmUi.Label("", 13, true); titleStack.AddChild(_baseHealthLabel);
        var meters = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        meters.AddThemeConstantOverride("separation", 4); topRow.AddChild(meters);
        _courageBar = new BattleHudBar { CustomMinimumSize = new Vector2(250, 26) };
        _courageBar.Setup(RealmUi.Gold, new Color("ffffff33"), "Courage"); meters.AddChild(_courageBar);
        _waveProgressBar = new BattleHudBar { CustomMinimumSize = new Vector2(250, 26) };
        _waveProgressBar.Setup(new Color("86b4a0"), new Color("ffffff22"), "Waves"); meters.AddChild(_waveProgressBar);
        _timerLabel = RealmUi.Label("", 14); topRow.AddChild(_timerLabel);
        _speedButton = RealmUi.Button("clock", "1x", CycleBattleSpeed); topRow.AddChild(_speedButton);
        topRow.AddChild(RealmUi.IconButton("eye", "Battle intel [Tab]", ToggleCombatIntel));
        topRow.AddChild(RealmUi.IconButton("pause", "Pause [Escape]", TogglePause));
        topRow.AddChild(RealmUi.IconButton("back", "Retreat", RetreatToMap));
        _statusLabel = new Label { Position = new Vector2(28, 510), Size = new Vector2(1220, 40),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        _statusLabel.AddThemeFontSizeOverride("font_size", 18); var messageScroll = new ScrollContainer { Position = new Vector2(28, 490), Size = new Vector2(1220, 60), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(messageScroll); _statusLabel.Position = Vector2.Zero; _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; messageScroll.AddChild(_statusLabel);
        var infoVBox = RealmUi.Panel(root, new Rect2(786, 102, 478, 436), out _intelPanel);
        infoVBox.AddChild(RealmUi.Label("FIELD INTELLIGENCE", 12, true));
        var report = RealmUi.Scroll(infoVBox);
        _battleSubtitleLabel = RealmUi.Label("", 14); report.AddChild(_battleSubtitleLabel);
        _baseWeaponsIntel = RealmUi.Label("", 14); report.AddChild(_baseWeaponsIntel);
        _battleMissionLabel = RealmUi.Label("", 14); report.AddChild(_battleMissionLabel);
        _resourceLabel = RealmUi.Label("", 14); report.AddChild(_resourceLabel);
        _waveIntelLabel = RealmUi.Label("", 14); report.AddChild(_waveIntelLabel);
        _objectiveStatusLabel = RealmUi.Label("", 14); report.AddChild(_objectiveStatusLabel);
        _fpsLabel = RealmUi.Label("", 12, true); report.AddChild(_fpsLabel);
        _showDevUiToggle = new CheckBox { Text = "Battle intel", Visible = false };
        _showFpsToggle = new CheckBox { Text = "FPS", Visible = false };
        root.AddChild(_showDevUiToggle); root.AddChild(_showFpsToggle);
        _showDevUiToggle.Toggled += OnShowDevUiToggled;
        _showFpsToggle.Toggled += OnShowFpsToggled;
        if (IsCampaignMode)
        {
            var orders = new HBoxContainer { Position = new Vector2(18, 102) };
            root.AddChild(orders);
            _convoyOrderButton = RealmUi.IconButton("crown", "Caravan command [C]", () => TryActivateCampaignConvoyCommand());
            _assaultOrderButton = RealmUi.IconButton("sword", "Assault order [Z]", () => TryCommitCampaignFieldOrder(true));
            _bulwarkOrderButton = RealmUi.IconButton("shield", "Bulwark order [X]", () => TryCommitCampaignFieldOrder(false));
            _rescueOrderButton = RealmUi.IconButton("heart", "Rescue directive [V]", () => TryCommitCampaignAdaptiveWaveDirective(false));
            _breakthroughOrderButton = RealmUi.IconButton("bolt", "Breakthrough directive [B]", () => TryCommitCampaignAdaptiveWaveDirective(true));
            foreach (var button in new[] { _convoyOrderButton, _assaultOrderButton, _bulwarkOrderButton, _rescueOrderButton, _breakthroughOrderButton })
                orders.AddChild(button);
        }

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
		spawnStack.AddChild(unitRow);

		foreach (var definition in _deck.Roster)
		{
			var unit = definition;
			var button = new RealmButton
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, 124f)
			};
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			var (titleLabel, detailLabel) = AttachBattleCardContent(
				button,
				UiBadgeFactory.CreateUnitBadge(unit, new Vector2(48f, 48f)));
			button.Pressed += () => ArmPlayerUnit(unit);
			unitRow.AddChild(button);
			_deploySlots.Add(new DeploySlot(unit, button, titleLabel, detailLabel));
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
					CustomMinimumSize = new Vector2(0f, 124f)
				};
				button.AddThemeColorOverride("font_color", Colors.White);
				button.AddThemeColorOverride("font_hover_color", Colors.White);
				button.AddThemeColorOverride("font_pressed_color", Colors.White);
				button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
				var (titleLabel, detailLabel) = AttachBattleCardContent(
					button,
					UiBadgeFactory.CreateSpellBadge(spell, new Vector2(40f, 40f)));
				button.Pressed += () => ArmSpell(spell);
				spellRow.AddChild(button);
				_spellSlots.Add(new SpellSlot(spell, button, titleLabel, detailLabel));
			}
		}

		_pauseOverlay = new CenterContainer();
		_pauseOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_pauseOverlay.Visible = false;
		root.AddChild(_pauseOverlay);
		var pauseBg = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) };
		pauseBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_pauseOverlay.AddChild(pauseBg);
        var pauseCard = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        _pauseOverlay.AddChild(pauseCard);
        var pauseStack = new VBoxContainer(); pauseCard.AddChild(pauseStack);
        pauseStack.AddChild(RealmUi.Heading("A moment of respite", 28));
        pauseStack.AddChild(RealmUi.Label("1–5  Units     Q–T  Rites\nClick the field to deploy. Right-click to cancel.\nSpace  Speed     Tab  Intel     Escape  Pause", 16, true));
        pauseStack.AddChild(RealmUi.Button("arrow", "Resume battle", TogglePause, true));
        pauseStack.AddChild(RealmUi.Button("back", "Retreat", RetreatToMap));

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
		endPadding.AddThemeConstantOverride("margin_left", 20);
		endPadding.AddThemeConstantOverride("margin_right", 20);
		endPadding.AddThemeConstantOverride("margin_top", 20);
		endPadding.AddThemeConstantOverride("margin_bottom", 20);
		_endPanel.AddChild(endPadding);

		var endVBox = new VBoxContainer();
		endVBox.AddThemeConstantOverride("separation", 12);
		endVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		endPadding.AddChild(endVBox);

		var endScroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 260f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
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
						: "Back To Map",
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

	private static (Label titleLabel, Label detailLabel) AttachBattleCardContent(Button button, Control badge)
	{
		button.Text = string.Empty;

		var padding = new MarginContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		padding.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		padding.AddThemeConstantOverride("margin_left", 10);
		padding.AddThemeConstantOverride("margin_right", 10);
		padding.AddThemeConstantOverride("margin_top", 8);
		padding.AddThemeConstantOverride("margin_bottom", 8);
		button.AddChild(padding);

		var stack = UiBadgeFactory.CreateStackWithLeadingBadge(padding, badge, separation: 10, stackSpacing: 2);
		var titleLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 18);
        titleLabel.AddThemeColorOverride("font_color", Colors.White);
		stack.AddChild(titleLabel);

		var detailLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center
		};
		detailLabel.AddThemeFontSizeOverride("font_size", 18);
        detailLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.84f));
		stack.AddChild(detailLabel);

		SetMouseFilterRecursive(padding, Control.MouseFilterEnum.Ignore);
		return (titleLabel, detailLabel);
	}

	private static void SetMouseFilterRecursive(Control control, Control.MouseFilterEnum mouseFilter)
	{
		control.MouseFilter = mouseFilter;
		foreach (var child in control.GetChildren())
		{
			if (child is Control childControl)
			{
				SetMouseFilterRecursive(childControl, mouseFilter);
			}
		}
	}

	private void UpdateHud()
	{
        if (_convoyOrderButton != null)
        {
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
					? $"{_spawnDirector.NextScriptedWaveIndex}/{_spawnDirector.TotalScriptedWaves}"
					: "");
		_waveProgressBar.Visible = _spawnDirector.UsesScriptedWaves || _spawnDirector.IsEndlessMode;
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

			var stateLabel = !isReady
				? $"CD {cooldown:0.0}s"
				: hasCourage
					? "DEPLOY"
					: $"NEED {slot.Definition.Cost - Mathf.FloorToInt(_courage)} more";
			var marker = slot.Definition == _deck.ArmedUnit ? "> " : "";
			slot.TitleLabel.Text = $"{marker}{slot.Definition.DisplayName}";
			slot.DetailLabel.Text = $"{slot.Definition.Cost} courage · Lv{level}\n{stateLabel}";
			slot.Button.SelfModulate = ResolveDeployButtonTint(slot.Definition, isReady, hasCourage, slot.Definition == _deck.ArmedUnit);
			slot.Button.TooltipText = BuildDeployButtonTooltip(slot.Definition, level, isReady, cooldown);
			var totalCd = ResolvePlayerDeployCooldown(slot.Definition);
			var cdRatio = !isReady && totalCd > 0.1f ? cooldown / totalCd : 0f;
			slot.CooldownOverlay.Visible = cdRatio > 0.01f;
			if (cdRatio > 0.01f)
			{
				slot.CooldownOverlay.AnchorRight = Mathf.Clamp(cdRatio, 0f, 1f);
			}
		}

		foreach (var slot in _spellSlots)
		{
			var resolved = GameState.Instance.BuildSpellStats(slot.Definition);
			var cooldown = _spellDeck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= resolved.CourageCost;
			var armed = _selectionMode == BattleSelectionMode.Spell && slot.Definition == _spellDeck.ArmedSpell;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;

			var stateLabel = !isReady
				? $"CD {cooldown:0.0}s"
				: hasCourage
					? "CAST"
					: $"NEED {resolved.CourageCost - Mathf.FloorToInt(_courage)} more";
			var marker = armed ? "* " : "";
			slot.TitleLabel.Text = $"{marker}{slot.Definition.DisplayName}";
			slot.DetailLabel.Text = $"{resolved.CourageCost} courage · Lv{resolved.Level}\n{stateLabel}";
			slot.Button.SelfModulate = ResolveSpellButtonTint(slot.Definition, isReady, hasCourage, armed);
			slot.Button.TooltipText = SpellText.BuildTooltipSummary(slot.Definition, resolved, isReady, cooldown);
			var totalSpellCd = ResolvePlayerSpellCooldown(slot.Definition, resolved);
			var spellCdRatio = !isReady && totalSpellCd > 0.1f ? cooldown / totalSpellCd : 0f;
			slot.CooldownOverlay.Visible = spellCdRatio > 0.01f;
			if (spellCdRatio > 0.01f)
			{
				slot.CooldownOverlay.AnchorRight = Mathf.Clamp(spellCdRatio, 0f, 1f);
			}
		}
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
        _intelPanel.Visible = _combatIntelExpanded;
        _timerLabel.Visible = true;
        _statusLabel.Visible = true;
        _fpsLabel.Visible = GameState.Instance.ShowFpsCounter;
        _showDevUiToggle.SetPressedNoSignal(_combatIntelExpanded);
    }

    private void ToggleCombatIntel()
    {
        _combatIntelExpanded = !_combatIntelExpanded;
        ApplyDevUiSettings();
    }

}
