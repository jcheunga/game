using System.Collections.Generic;
using Godot;

/// <summary>
/// The caravan spell: once the mana bar is full a large slot opens on the right of the card dock, and casting it
/// spends the whole bar. After a short wind-up the war wagon's lantern looses arcane missiles for a few seconds;
/// each one homes on a living enemy (spread so every enemy is struck again and again, up to a per-enemy cap)
/// and bursts on open ground once every enemy has had its share. Tuning lives in combat_config.json
/// (CaravanSpell); the missiles' look lives in CaravanBarrageFx.
/// </summary>
public partial class BattleController
{
    private const float CaravanWindupSeconds = .45f;
    // Battle-time gaps between repeated launch and impact sounds, so thirty missiles a second stay listenable.
    private const float CaravanLaunchSoundGap = .11f, CaravanImpactSoundGap = .075f, CaravanNumberGap = .3f;
    // The lantern on the wagon roof, as a fraction of the wagon picture (the same on every skin).
    private static readonly Vector2 WagonLanternUv = new(.683f, .317f);

    private CaravanSpellButton _caravanSpellButton;
    private CaravanBarrageFx _caravanBarrageFx;
    private float _caravanSpellCooldown, _caravanWindup, _caravanBarrageRemaining, _caravanLaunchDebt;
    private float _caravanLaunchSoundAt, _caravanImpactSoundAt, _caravanNumberClock;
    private int _caravanMissilesLaunched, _caravanMissilesLanded, _caravanCasts;
    private readonly Dictionary<Unit, int> _caravanHits = new();
    private readonly Dictionary<Unit, float> _caravanDamageTally = new();

    private CaravanSpellTuning CaravanSpell => _combat.CaravanSpell ?? new CaravanSpellTuning();
    private Vector2 WagonLantern => WagonArt is { } art ? art.Point(WagonGround, WagonLanternUv) : PlayerBaseCorePosition + new Vector2(22, -60);
    private bool CaravanBarrageActive => _caravanWindup > 0f || _caravanBarrageRemaining > 0f;
    /// <summary>The slot is open whenever the bar holds the spell's cost.</summary>
    private bool CaravanSpellOpen => !_battleEnded && _mana >= CaravanSpell.ManaCost - .01f;
    private bool CaravanSpellReady => CaravanSpellOpen && !_endlessCheckpointActive && _caravanSpellCooldown <= .05f && !CaravanBarrageActive;

    private void ResetCaravanSpell()
    {
        _caravanSpellCooldown = _caravanWindup = _caravanBarrageRemaining = _caravanLaunchDebt = 0f;
        _caravanMissilesLaunched = _caravanMissilesLanded = _caravanCasts = 0;
        _caravanHits.Clear();
        _caravanDamageTally.Clear();
    }

    private void BuildCaravanSpellButton(Control root, Control dock)
    {
        var spell = CaravanSpell;
        var card = new RealmButton { Name = "CaravanSpellCard", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        var art = AttachBattleCardContent(card, RoyalArt.Item(spell.Id));
        card.Pressed += TryCastCaravanSpell;
        card.TooltipText = $"{spell.DisplayName} [Space] · {spell.Description}";
        _caravanSpellButton = new CaravanSpellButton(card, art) { Opened = () => AudioDirector.Instance?.Play("caravan_spell_ready") };
        root.AddChild(_caravanSpellButton);
        // It slides out from behind the dock, so it draws under it.
        root.MoveChild(_caravanSpellButton, dock.GetIndex());
    }

    /// <summary>Places the slot to the right of the dock, moving the dock left if the two would not fit.</summary>
    private void LayoutCaravanSpellButton(Control dock, float cardWidth, float cardHeight, float screenRight)
    {
        if (_caravanSpellButton == null) return;
        // A card the size of the troops' in a slot styled like the dock, as tall as the dock and on its base line.
        var size = new Vector2(cardWidth + 18, cardHeight + 20);
        const float gap = 10f;
        var overflow = dock.Position.X + dock.Size.X + gap + size.X - (screenRight - 8f);
        if (overflow > 0f) dock.Position -= new Vector2(Mathf.Min(overflow, dock.Position.X - 8f), 0);
        var rest = new Rect2(new Vector2(dock.Position.X + dock.Size.X + gap, dock.Position.Y + dock.Size.Y - size.Y), size);
        _caravanSpellButton.Place(rest);
    }

    /// <summary>The top of the caravan slot (for keeping other HUD pieces clear of it).</summary>
    private float CaravanSpellTop => _caravanSpellButton is { RestRect.Size.Y: > 0f } slot ? slot.RestRect.Position.Y : float.MaxValue;

    private void UpdateCaravanSpellHud()
    {
        if (_caravanSpellButton == null) return;
        var spell = CaravanSpell;
        var ready = CaravanSpellReady;
        _caravanSpellButton.SetState(CaravanSpellOpen, ready);
        var cooldown = CaravanBarrageActive ? spell.Cooldown : _caravanSpellCooldown;
        // While ready it wears the gold frame of a card waiting to be played.
        _caravanSpellButton.Art.SetState(0, 1f, cooldown, spell.Cooldown, ready, _battleEnded || _endlessCheckpointActive);
        var card = _caravanSpellButton.Card;
        card.AccessibilityName = $"{spell.DisplayName}, caravan spell, {Mathf.RoundToInt(spell.ManaCost)} mana";
        card.AccessibilityDescription = CaravanBarrageActive ? "The barrage is firing."
            : _caravanSpellCooldown > .05f ? $"Cooldown {_caravanSpellCooldown:0.0} seconds."
            : ready ? "Ready. Select to fire the war wagon's arcane barrage at every enemy." : "Opens when the mana bar is full.";
    }

    /// <summary>Space casts the caravan spell. It is caught before the GUI, so a focused HUD button cannot swallow it.</summary>
    private bool HandleCaravanSpellKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space }) return false;
        if (_battlePaused || _battleEnded || _endlessCheckpointActive || !CaravanSpellOpen) return false;
        TryCastCaravanSpell();
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void TryCastCaravanSpell()
    {
        if (_battlePaused || _restartPending || SceneRouter.Instance?.IsTransitioning == true) return;
        if (!CaravanSpellReady)
        {
            AudioDirector.Instance?.PlaySpellDenied();
            SetStatus(_mana < CaravanSpell.ManaCost ? "The caravan spell opens when the mana bar is full."
                : CaravanBarrageActive ? "The barrage is already firing." : $"The lantern is recharging ({_caravanSpellCooldown:0} s).");
            return;
        }
        var spell = CaravanSpell;
        _mana = Mathf.Max(0f, _mana - spell.ManaCost);
        _caravanSpellCooldown = spell.Cooldown;
        _caravanWindup = CaravanWindupSeconds;
        _caravanBarrageRemaining = spell.Duration;
        _caravanLaunchDebt = 1f;
        _caravanHits.Clear();
        _caravanCasts++;
        _spellsCast++;
        ApplyContentSpellEquipment();
        GameState.Instance.AddBountyProgress("spell_casts", 1);
        if (_selectionMode == BattleSelectionMode.Spell) ClearArmedSelection();
        EnsureCaravanBarrageFx().Ignite(CaravanWindupSeconds + spell.Duration);
        CaravanCastVfx(spell.Duration);
        var director = AudioDirector.Instance;
        if (director != null && !director.PlayAt("spell_caravan_barrage", WagonLantern)) director.PlayAt("ability_overcharge", WagonLantern);
        MusicPlayer.Instance?.Duck(-5f, CaravanWindupSeconds + spell.Duration);
        SetStatus($"{spell.DisplayName}! The war wagon's lantern looses its missiles.");
        UpdateHud();
    }

    private CaravanBarrageFx EnsureCaravanBarrageFx()
    {
        if (!IsInstanceValid(_caravanBarrageFx))
        {
            _caravanBarrageFx = new CaravanBarrageFx { AimOf = Chest };
            AddChild(_caravanBarrageFx);
        }
        _caravanBarrageFx.Emitter = WagonLantern;
        return _caravanBarrageFx;
    }

    /// <summary>Runs on the battle clock with the rest of the simulation.</summary>
    private void TickCaravanSpell(float delta)
    {
        _caravanSpellCooldown = Mathf.Max(0f, _caravanSpellCooldown - delta);
        if (_caravanWindup > 0f)
        {
            _caravanWindup -= delta;
            if (_caravanWindup <= 0f) CaravanVolleyStartVfx();
        }
        else if (_caravanBarrageRemaining > 0f)
        {
            var firing = Mathf.Min(delta, _caravanBarrageRemaining);
            _caravanBarrageRemaining -= delta;
            _caravanLaunchDebt += firing * CaravanSpell.MissilesPerSecond;
            while (_caravanLaunchDebt >= 1f)
            {
                _caravanLaunchDebt -= 1f;
                LaunchCaravanMissile();
            }
            if (_caravanBarrageRemaining <= 0f) CaravanVolleyEndVfx();
        }
        if (IsInstanceValid(_caravanBarrageFx)) _caravanBarrageFx.Step(delta, RetargetCaravanMissile, LandCaravanMissile);
        _caravanNumberClock += delta;
        if (_caravanNumberClock >= CaravanNumberGap) FlushCaravanNumbers();
    }

    /// <summary>After the battle ends the last missiles still fly home and burst, but strike nothing.</summary>
    private void TickCaravanSpellAfterBattle(float delta)
    {
        if (!_battleEnded || !IsInstanceValid(_caravanBarrageFx) || _caravanBarrageFx.Missiles.Count == 0) return;
        if (GetTree().Paused) return;
        _caravanBarrageFx.Step(delta, null, null);
    }

    private bool IsCaravanTarget(Unit unit) => IsInstanceValid(unit) && !unit.IsDead && unit.Team == Team.Enemy && !unit.IsUntargetable
        && unit.Position.X >= BattlefieldLeft && unit.Position.X <= BattlefieldRight;

    /// <summary>The living enemy struck least so far by this barrage (ties broken at random), below the per-enemy cap.</summary>
    private Unit PickCaravanTarget(Vector2? near = null)
    {
        var cap = CaravanSpell.MaxHitsPerEnemy;
        Unit best = null;
        var bestHits = int.MaxValue;
        var bestScore = float.MinValue;
        foreach (var unit in _units)
        {
            if (!IsCaravanTarget(unit)) continue;
            _caravanHits.TryGetValue(unit, out var hits);
            if (hits >= cap) continue;
            // A retarget prefers what is close; a fresh launch spreads at random.
            var score = near is { } point ? -point.DistanceTo(unit.Position) : _rng.Randf();
            if (hits < bestHits || hits == bestHits && score > bestScore) { best = unit; bestHits = hits; bestScore = score; }
        }
        return best;
    }

    private void LaunchCaravanMissile()
    {
        var fx = EnsureCaravanBarrageFx();
        var target = PickCaravanTarget();
        if (target != null) _caravanHits[target] = _caravanHits.GetValueOrDefault(target) + 1;
        fx.Launch(target, target != null ? Chest(target) : CaravanStrayPoint());
        _caravanMissilesLaunched++;
        if (_elapsed >= _caravanLaunchSoundAt)
        {
            _caravanLaunchSoundAt = _elapsed + CaravanLaunchSoundGap;
            AudioDirector.Instance?.PlayAt("cast_arcane", WagonLantern, -7f, _rng.RandfRange(.92f, 1.12f));
        }
    }

    /// <summary>A missile with nothing left to strike bursts on open ground: mostly in view, sometimes anywhere on the field.</summary>
    private Vector2 CaravanStrayPoint()
    {
        var left = PlayerBaseX + 80f;
        var right = EnemyBaseX - 50f;
        if (_battleCamera != null && _rng.Randf() < .65f)
        {
            var halfView = GetViewportRect().Size.X * .5f / Mathf.Max(.01f, _battleCamera.Zoom.X);
            var centre = _battleCamera.GetScreenCenterPosition().X;
            left = Mathf.Max(left, centre - halfView + 30f);
            right = Mathf.Min(right, centre + halfView - 30f);
            if (right <= left) { left = PlayerBaseX + 80f; right = EnemyBaseX - 50f; }
        }
        return new Vector2(_rng.RandfRange(left, right), _rng.RandfRange(BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
    }

    private void RetargetCaravanMissile(CaravanBarrageFx.Missile missile)
    {
        var next = PickCaravanTarget(missile.Position);
        if (next == null) return;
        _caravanHits[next] = _caravanHits.GetValueOrDefault(next) + 1;
        _caravanBarrageFx.Redirect(missile, next, Chest(next));
    }

    private void LandCaravanMissile(CaravanBarrageFx.Missile missile)
    {
        _caravanMissilesLanded++;
        if (_battleEnded) return;
        var target = missile.Target;
        var at = missile.Position;
        if (target != null && IsInstanceValid(target) && !target.IsDead)
        {
            var dealt = target.TakeDamage(CaravanSpell.MissileDamage, CaravanSpell.DisplayName, CombatDamageType.Magical);
            if (dealt > .05f) _caravanDamageTally[target] = _caravanDamageTally.GetValueOrDefault(target) + dealt;
        }
        else if (_caravanMissilesLanded % 3 == 0)
        {
            // Open-ground bursts leave a faint violet scorch.
            SpellVfx.Decal(this, new Vector2(at.X, Mathf.Clamp(at.Y, BattlefieldTop, BattlefieldBottom)), "scorch", new Color(.7f, .55f, 1f, .7f), 12f, 2.4f);
        }
        if (_elapsed >= _caravanImpactSoundAt)
        {
            _caravanImpactSoundAt = _elapsed + CaravanImpactSoundGap;
            AudioDirector.Instance?.PlayAt("impact_arcane", at, -5f, _rng.RandfRange(.9f, 1.15f));
        }
    }

    /// <summary>Damage numbers gather each enemy's hits into one number every few tenths of a second.</summary>
    private void FlushCaravanNumbers()
    {
        _caravanNumberClock = 0f;
        if (_caravanDamageTally.Count == 0) return;
        foreach (var (unit, damage) in _caravanDamageTally)
            SpawnDamageFeedback(unit, damage);
        _caravanDamageTally.Clear();
    }

    // ── The look of the cast ──

    /// <summary>The wind-up: a rune circle opens under the wagon, light gathers into the lantern and a beam climbs from it.</summary>
    private void CaravanCastVfx(float duration)
    {
        var lantern = WagonLantern;
        var ground = WagonGround;
        var hold = CaravanWindupSeconds + duration;
        SpellVfx.Sigil(this, ground, "arcane", CaravanBarrageFx.Body, 92f, hold + .6f, .5f);
        SpellVfx.Sigil(this, ground, "arcane", CaravanBarrageFx.Edge, 58f, hold + .4f, -.9f, .1f);
        SpellVfx.Flash(this, ground, CaravanBarrageFx.Deep, 120f, hold, true);
        SpellVfx.Shockwave(this, ground, CaravanBarrageFx.Body, 90f, .55f);
        SpellVfx.Flash(this, lantern, CaravanBarrageFx.Core, 70f, .5f);
        // Motes drawn up from the ground into the lantern.
        SpellVfx.Burst(this, lantern, "p_orb", 26, CaravanWindupSeconds + .1f, SpellVfx.Fade(CaravanBarrageFx.Core, CaravanBarrageFx.Body), 4, 9, 0, 12,
            groundRadius: 80f, explosiveness: .35f, pull: 520f);
        SpellVfx.Burst(this, ground, "p_rune", 10, 1.4f, SpellVfx.Fade(CaravanBarrageFx.Core, CaravanBarrageFx.Edge), 10, 16, 20, 50, spread: 25,
            gravity: -45f, groundRadius: 70f, explosiveness: .4f);
        SpellVfx.Burst(this, lantern, "p_swirl", 4, .6f, SpellVfx.Fade(CaravanBarrageFx.Core, CaravanBarrageFx.Body), 26, 40, 5, 15, spin: 360);
        SpellShake(.25f);
    }

    /// <summary>The volley opens: a beam stands over the lantern, a ring rolls out from the wagon and the field shakes.</summary>
    private void CaravanVolleyStartVfx()
    {
        var lantern = WagonLantern;
        var duration = CaravanSpell.Duration;
        SpellVfx.Pillar(this, lantern + new Vector2(0, 8), new Color(CaravanBarrageFx.Body, .9f), 46f, 260f, duration + .2f)?.Envelope(.06f, .3f);
        SpellVfx.Flash(this, lantern, CaravanBarrageFx.Edge, 90f, .4f);
        SpellVfx.Shockwave(this, WagonGround, CaravanBarrageFx.Edge, 140f, .7f);
        SoundRings(lantern, new Color(CaravanBarrageFx.Edge, .6f), 2, 130f);
        SpellVfx.Burst(this, lantern, "p_star", 20, .8f, SpellVfx.Fade(CaravanBarrageFx.Core, CaravanBarrageFx.Body), 6, 12, 120, 260, spread: 70,
            gravity: 160f, damping: 60f);
        SpellShake(.45f);
    }

    /// <summary>The last missile leaves: the lantern gutters out in a soft shimmer.</summary>
    private void CaravanVolleyEndVfx()
    {
        var lantern = WagonLantern;
        SpellVfx.Flash(this, lantern, CaravanBarrageFx.Body, 80f, .7f);
        SpellVfx.Burst(this, lantern, "p_star", 12, 1.1f, SpellVfx.Fade(CaravanBarrageFx.Core, CaravanBarrageFx.Edge), 4, 9, 20, 70, spread: 90,
            gravity: -20f, explosiveness: .5f);
    }
}
