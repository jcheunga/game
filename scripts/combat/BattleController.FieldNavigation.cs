using System.Linq;
using Godot;

public partial class BattleController
{
    private HBoxContainer _fieldNavigation;
    private Control _fieldOverview;
    private Button _fieldLeftThreats, _fieldRightThreats, _fieldDeployButton;
    private float _leftThreatX, _rightThreatX;

    private void BuildFieldNavigation(Control root)
    {
        // Camera panning is available directly on the field.
    }

    private void JumpToFieldPoint(float x)
    {
        if (_battlePaused || _battleEnded || _endlessCheckpointActive) return;
        if (_battleCamera != null) { SetBattleCameraX(x); return; }
        if (_mobileCamera == null) return;
        if (_mobileOverview) ToggleMobileOverview();
        _mobileFollow = false;
        _mobilePointerDown = false;
        _mobileViewButton.Text = "Follow";
        _mobileCamera.Position = new Vector2(x, _mobileCamera.Position.Y);
        ClampMobileCamera();
        _mobileCamera.ForceUpdateScroll();
    }

    private void UpdateFieldNavigation()
    {
        if (_fieldNavigation == null) return;
        var mobile = _mobileHud != null;
        _fieldNavigation.Visible = !mobile || !_mobileClearView;
        if (mobile)
        {
            var width = _mobileHud.Size.X;
            var alongside = width >= 800;
            _fieldOverview.CustomMinimumSize = new Vector2(140, 44);
            foreach (var button in new[] { _fieldLeftThreats, _fieldRightThreats, _fieldDeployButton })
            {
                button.AddThemeFontSizeOverride("font_size", 17);
                button.ClipText = true;
                button.CustomMinimumSize = new Vector2(button == _fieldDeployButton ? 132 : 84, 44);
            }
            _fieldNavigation.Position = new Vector2(alongside ? 322 : 12, _mobileFieldTop / MobilePresentation.HudScale + (alongside ? 6 : 62));
            _fieldNavigation.Size = new Vector2(width - (alongside ? 338 : 24), 44);
        }
        else
        {
            _fieldNavigation.Position = new Vector2(Mathf.Max(12, (GetViewportRect().Size.X - 660) / 2), 100);
            _fieldNavigation.Size = new Vector2(660, 36);
        }
        var left = ScreenToBattle(new Vector2(0, 300)).X;
        var right = ScreenToBattle(new Vector2(GetViewportRect().Size.X, 300)).X;
        var enemies = _units.Where(u => !u.IsDead && u.Team == Team.Enemy).ToArray();
        var leftEnemies = enemies.Where(u => u.Position.X < left).OrderByDescending(u => u.Position.X).ToArray();
        var rightEnemies = enemies.Where(u => u.Position.X > right).OrderBy(u => u.Position.X).ToArray();
        _fieldLeftThreats.Text = leftEnemies.Length == 0 ? "Wagon" : $"◀ {leftEnemies.Length}";
        _fieldRightThreats.Text = rightEnemies.Length == 0 ? "Gate" : $"{rightEnemies.Length} ▶";
        if (mobile)
        {
            if (leftEnemies.Length == 0) _fieldLeftThreats.Text = "◀";
            if (rightEnemies.Length == 0) _fieldRightThreats.Text = "▶";
        }

        _leftThreatX = leftEnemies.FirstOrDefault()?.Position.X ?? PlayerBaseX;
        _rightThreatX = rightEnemies.FirstOrDefault()?.Position.X ?? EnemyBaseX;
        _fieldDeployButton.Text = !_outpostCaptured ? "Find forward post" : _forwardDeploymentsRemaining == 0 ? "Post spent · Wagon" :
            !_forwardDeploymentArmed ? $"Wagon · Post {_forwardDeploymentsRemaining}" : OutpostBlocked ? "Post blocked · Wagon" :
            _forwardCooldownRemaining > 0 ? $"Post {_forwardCooldownRemaining:0}s · Wagon" : $"Post ready · {_forwardDeploymentsRemaining}";
        if (mobile) _fieldDeployButton.Text = !_outpostCaptured ? "Find post" : _forwardDeploymentsRemaining == 0 ? "Wagon" :
            !_forwardDeploymentArmed ? $"Wagon · {_forwardDeploymentsRemaining}" : OutpostBlocked ? "Blocked" :
            _forwardCooldownRemaining > 0 ? $"Post {_forwardCooldownRemaining:0}s" : $"Post ({_forwardDeploymentsRemaining})";
        _fieldDeployButton.TooltipText = _outpostCaptured
            ? $"{(_forwardDeploymentArmed ? "Tap to save post charges and deploy from the wagon." : "Tap to use forward deployments when the post is ready.")}\n{_stageData.Battlefield.Briefing}"
            : _stageData.Battlefield.Briefing;
        _fieldDeployButton.Disabled = _outpostCaptured && _forwardDeploymentsRemaining == 0;
        _fieldOverview.QueueRedraw();
    }

    private void DrawFieldOverview()
    {
        var size = _fieldOverview.Size;
        var ink = new Color("8ecae6");
        _fieldOverview.DrawStyleBox(_battleOverlaySurface ??= MedievalUi.Engraved("inset", 0, 0), new Rect2(Vector2.Zero, size));
        Vector2 Point(Vector2 world) => new(Mathf.Clamp(world.X / BattleWorldWidth, 0, 1) * size.X,
            6 + Mathf.Clamp((world.Y - BattlefieldTop) / (BattlefieldBottom - BattlefieldTop), 0, 1) * (size.Y - 12));
        var left = Mathf.Clamp(ScreenToBattle(new Vector2(0, 300)).X / BattleWorldWidth, 0, 1) * size.X;
        var right = Mathf.Clamp(ScreenToBattle(new Vector2(GetViewportRect().Size.X, 300)).X / BattleWorldWidth, 0, 1) * size.X;
        _fieldOverview.DrawRect(new Rect2(left, 1, right - left, size.Y - 2), new Color(ink, .4f), false, 1);
        foreach (var unit in _units)
            if (!unit.IsDead) _fieldOverview.DrawCircle(Point(unit.Position), unit.VisualClass == "boss" ? 4 : 2,
                unit.Team == Team.Player ? new Color("86d5a5") : new Color("ed7864"));
        _fieldOverview.DrawRect(new Rect2(Point(OutpostPosition) - Vector2.One * 4, Vector2.One * 8),
            _outpostCaptured ? new Color("86d5a5") : ink, false, 2);
        if (!_supplyCollected) _fieldOverview.DrawCircle(Point(SupplyPosition), 4, new Color("ffd166"));
        if (_pendingTunnelInvasion.HasValue) _fieldOverview.DrawCircle(Point(_pendingTunnelInvasion.Value), 6, new Color("ffb454"), false, 2);
        if (_spawnDirector.EncounterWarningActive)
        {
            var x = _spawnDirector.NextEncounterSpawnX / BattleWorldWidth * size.X;
            _fieldOverview.DrawLine(new Vector2(x, 2), new Vector2(x, size.Y - 2), new Color("ffb454"), 3);
        }
    }
}
