using Godot;

public partial class BattleController
{
    private StageStarRating _endStarRating;
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
        var cardScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 164) };
        spawnStack.AddChild(cardScroll); cardScroll.AddChild(unitRow);

		foreach (var definition in _deck.Roster)
		{
			var unit = definition;
			var button = new RealmButton
			{
				SizeFlagsHorizontal = Control.SizeFlags.Fill,
				CustomMinimumSize = new Vector2(140f, 160f)
			};
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			var card = AttachBattleCardContent(button, UiArtLoader.TryLoadUnitIcon(unit));
			button.Pressed += () => DeployPlayerUnit(unit);
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
					CustomMinimumSize = new Vector2(140f, 160f)
				};
				button.AddThemeColorOverride("font_color", Colors.White);
				button.AddThemeColorOverride("font_hover_color", Colors.White);
				button.AddThemeColorOverride("font_pressed_color", Colors.White);
				button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
				var card = AttachBattleCardContent(button, UiArtLoader.TryLoadSpellIcon(spell));
				card.UseManaCost();
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
				? "Restart run"
					: IsLanRaceMode
						? "Rematch"
						: IsOnlineRoomMode
							? "Back to room"
						: IsChallengeMode
							? "Retry challenge"
						: "Retry stage",
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
				? "Back to endless"
					: IsLanRaceMode
						? "Back to challenges"
						: IsOnlineRoomMode
							? "Leave room"
						: IsChallengeMode
							? "Back to challenges"
						: IsTowerMode ? "Back to tower" : IsArenaMode ? "Back to arena" : IsSeasonalEventMode ? "Back to event" : "Back to map",
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

			SetStatus($"{hint.Title}: {hint.Body}");
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
        // The concept card: a thin bronze frame around full-bleed art, lifted slightly on hover.
        var frame = RoyalKit.Slice("hud-card", 8);
        frame.ContentMarginLeft = frame.ContentMarginRight = frame.ContentMarginTop = frame.ContentMarginBottom = 0;
        var hover = new StyleBoxFlat { BgColor = new Color(1f, .9f, .66f, .10f), BorderColor = new Color(1f, .86f, .55f, .7f), AntiAliasing = true };
        hover.SetBorderWidthAll(1); hover.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", frame);
        button.AddThemeStyleboxOverride("hover", new StackedStyle(frame, hover));
        button.AddThemeStyleboxOverride("pressed", new StackedStyle(frame, new StyleBoxFlat { BgColor = new Color(0, 0, 0, .2f) }));
        button.AddThemeStyleboxOverride("hover_pressed", new StackedStyle(frame, hover));
        button.AddThemeStyleboxOverride("disabled", frame);
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color(1f, .87f, .56f, .9f), AntiAliasing = true };
        focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("focus", focus);
		var card = new BattleActionCard();
		button.AddChild(card);
		card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		card.SetIcon(icon);
		return card;
	}

	private void UpdateHud()
	{
		_fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        _goldAmount.Text = GameState.Instance.Gold.ToString("N0");
        _courageBar.AccessibilityName = $"Courage, {Mathf.FloorToInt(_courage)} of {Mathf.FloorToInt(_maxCourage)}";
		_courageBar.SetValue(
			_maxCourage > 0.01f ? _courage / _maxCourage : 0f,
			$"{Mathf.FloorToInt(_courage)}/{Mathf.FloorToInt(_maxCourage)}");
		_manaBar.AccessibilityName = $"Mana, {Mathf.FloorToInt(_mana)} of {Mathf.FloorToInt(_maxMana)}";
		_manaBar.SetValue(
			_maxMana > 0.01f ? _mana / _maxMana : 0f,
			$"{Mathf.FloorToInt(_mana)}/{Mathf.FloorToInt(_maxMana)}");
		foreach (var slot in _deploySlots)
		{
			var cooldown = _deck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasCourage = _courage >= slot.Definition.Cost;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasCourage;
			var level = GameState.Instance.GetUnitLevel(slot.Definition.Id);

			slot.Button.SelfModulate = ResolveDeployButtonTint(slot.Definition, isReady, hasCourage);
			slot.Button.TooltipText = BuildDeployButtonTooltip(slot.Definition, level, isReady, cooldown);
			var totalCd = ResolvePlayerDeployCooldown(slot.Definition);
			slot.Card.SetState(slot.Definition.Cost, _courage, cooldown, totalCd, false, _battleEnded || _endlessCheckpointActive);
			slot.Button.AccessibilityName = $"{slot.Definition.DisplayName}, {slot.Definition.Cost} courage";
			slot.Button.AccessibilityDescription = $"Level {level}. " + (!isReady ? $"Cooldown {cooldown:0.0} seconds." : !hasCourage ? "Not enough courage." : "Ready. Select to deploy from the war wagon.");
		}

		foreach (var slot in _spellSlots)
		{
			var resolved = GameState.Instance.BuildSpellStats(slot.Definition);
			var cooldown = _spellDeck.GetCooldownRemaining(slot.Definition.Id);
			var isReady = cooldown <= 0.05f;
			var hasMana = _mana >= resolved.ManaCost;
			var armed = _selectionMode == BattleSelectionMode.Spell && slot.Definition == _spellDeck.ArmedSpell;
			slot.Button.Disabled = _battleEnded || _endlessCheckpointActive || !isReady || !hasMana;

			slot.Button.SelfModulate = ResolveSpellButtonTint(slot.Definition, isReady, hasMana, armed);
			slot.Button.TooltipText = SpellText.BuildTooltipSummary(slot.Definition, resolved, isReady, cooldown);
			var totalSpellCd = ResolvePlayerSpellCooldown(resolved);
			slot.Card.SetState(resolved.ManaCost, _mana, cooldown, totalSpellCd, armed, _battleEnded || _endlessCheckpointActive);
			slot.Button.AccessibilityName = $"{slot.Definition.DisplayName}, {resolved.ManaCost} mana";
			slot.Button.AccessibilityDescription = $"Level {resolved.Level}. " + (!isReady ? $"Cooldown {cooldown:0.0} seconds." : !hasMana ? "Not enough mana." : armed ? "Selected. Choose a target on the battlefield." : "Ready. Select to cast.");
		}
		_hudLayout?.Invoke();
        RefreshMobileHud();
	}
    private void ApplyDevUiSettings()
    {
        _topHudPanel.Visible = true;
        _statusLabel.Visible = false;
        _fpsLabel.Visible = GameState.Instance.ShowFpsCounter;
    }

}
