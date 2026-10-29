using System.Collections.Generic;
using System.Linq;
using Godot;

// The painted caravan, alive: its flag and awning stir in the wind (a cloth shader on the plate), it rocks on its springs
// when struck and keeps the bolts that hit it, its lanterns flicker and the cupola fills with light as mana gathers,
// it shows its armour upgrades and the zone's weather, wears battle damage below half health and tips over when it
// falls. Its crew works the roof weapons. Drawn as sibling canvases at the wagon's sort position (plate, detail, glow,
// health), so troops sort against it as one object.
public partial class BattleController
{
    private const int MaxWagonBolts = 6;
    private const string ClothShader = @"
shader_type canvas_item;
// Plate UV rects: the flag (its pole stands at the right edge) and the awning.
uniform vec4 flag;
uniform vec4 awning;
uniform float time;
uniform float wind = 1.0;
// 0: the pole stands at the flag's right edge (the caravan's flag); 1: at its left (the victory standard).
uniform float pole_left = 0.0;
void fragment() {
	vec2 uv = UV;
	// The flag streams back from its pole: a travelling wave that grows toward the free end.
	float from_right = clamp((flag.z - uv.x) / max(flag.z - flag.x, 1e-4), 0.0, 1.0);
	float along = mix(from_right, 1.0 - from_right, pole_left);
	float inside = step(flag.x - 0.02, uv.x) * step(uv.x, flag.z) * step(flag.y - 0.03, uv.y) * step(uv.y, flag.w + 0.03);
	uv.y += inside * wind * along * 0.008 * sin(along * 8.0 - time * 5.2);
	uv.x += inside * wind * along * 0.003 * sin(along * 6.0 - time * 4.1 + uv.y * 30.0);
	// The awning's scalloped edge lifts and settles.
	float depth = clamp((uv.y - awning.y) / max(awning.w - awning.y, 1e-4), 0.0, 1.0);
	float under = step(awning.x, uv.x) * step(uv.x, awning.z) * step(awning.y, uv.y) * step(uv.y, awning.w + 0.01);
	uv.y += under * wind * depth * depth * 0.004 * sin(uv.x * 90.0 - time * 3.3);
	COLOR = texture(TEXTURE, uv) * COLOR;
}";

    private static Shader _clothShader;
    private ShaderMaterial _wagonCloth;
    private float _wagonSway, _wagonSwaySpeed, _wagonDamagedMix, _ballistaAim, _ballistaAimGoal, _firepotKick;
    private float _wagonFallenAt = -1f;
    private readonly List<(Vector2 Uv, float Angle, ProjectileStyle Style)> _wagonBolts = new();
    private SceneryFigure _wagonArcher;
    private Texture2D _wagonArmour, _wagonWeather;

    private bool ReducedMotion => GameState.Instance?.ReducedMotion ?? false;
    private float WagonHealthRatio => Mathf.Clamp(_playerBaseHealth / Mathf.Max(1f, _playerBaseMaxHealth), 0f, 1f);

    /// <summary>The wagon's canvases after the plate: troop door and roof crew, lamp glow, smoke and health meter.</summary>
    private void BuildWagonLayers(Node2D plate)
    {
        var art = WagonArt;
        if (art == null) return;
        _clothShader ??= new Shader { Code = ClothShader };
        _wagonCloth = new ShaderMaterial { Shader = _clothShader };
        _wagonCloth.SetShaderParameter("flag", new Vector4(art.Flag.Position.X, art.Flag.Position.Y, art.Flag.End.X, art.Flag.End.Y));
        _wagonCloth.SetShaderParameter("awning", new Vector4(art.Awning.Position.X, art.Awning.Position.Y, art.Awning.End.X, art.Awning.End.Y));
        _wagonCloth.SetShaderParameter("wind", ReducedMotion ? 0f : 1f);
        plate.Material = _wagonCloth;
        AddChild(new BattleBaseCanvas { Name = "CaravanDetail", Position = WagonSortPosition,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, Paint = DrawWagonDetail });
        AddChild(new BattleBaseCanvas { Name = "CaravanGlow", Position = WagonSortPosition,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            TextureFilter = TextureFilterEnum.Linear, Paint = DrawWagonGlow });
        AddChild(new BattleBaseCanvas { Name = "CaravanHud", Position = WagonSortPosition, Paint = DrawWagonHud });

        _wagonArmour = BattleStructureArt.Overlay("armour");
        _wagonWeather = _activeRouteId switch
        {
            RouteCatalog.ThornwallId => BattleStructureArt.Overlay("snow"),
            RouteCatalog.MireId => BattleStructureArt.Overlay("mud"),
            _ => null
        };
        if (_wagonMounts.Count > 0 && _wagonMounts[0].Weapon.Kind == BaseWeaponKind.Arrows) _wagonArcher = SceneryFigure.Load("player_shooter", .82f);
    }

    private void UpdateWagonLife(float delta)
    {
        if (WagonArt == null) return;
        _wagonCloth?.SetShaderParameter("time", _showTime);
        // A damped spring: hits kick it, and it rocks back to rest on the wagon's springs in about a second.
        var force = -110f * _wagonSway - 7f * _wagonSwaySpeed;
        _wagonSwaySpeed += force * delta;
        _wagonSway += _wagonSwaySpeed * delta;
        _wagonDamagedMix = Mathf.MoveToward(_wagonDamagedMix, WagonHealthRatio < .5f || _wagonFallenAt >= 0 ? 1f : 0f, delta / .35f);
        _firepotKick = Mathf.MoveToward(_firepotKick, 0f, delta * 2.5f);
        _ballistaAim = Mathf.Lerp(_ballistaAim, _ballistaAimGoal, 1f - Mathf.Exp(-delta * 5f));
        _wagonArcher?.Tick(delta);
        // Between shots the ballista follows the nearest enemy it could reach.
        var ballista = _wagonMounts.FindIndex(m => m.Weapon.Kind == BaseWeaponKind.Ballista);
        if (ballista >= 0)
        {
            var origin = WagonMountPosition(ballista);
            var target = FindBaseWeaponTarget(Team.Player, PlayerBaseCorePosition, _wagonMounts[ballista].Weapon);
            _ballistaAimGoal = target == null ? 0f : BallistaPitch(origin, target.Position);
        }
    }

    // In this side view a bolt only pitches a little: down at close targets, up a touch for far ones.
    private static float BallistaPitch(Vector2 origin, Vector2 target) =>
        Mathf.Clamp((target - origin + new Vector2(0, -14)).Angle(), -.22f, .12f);

    /// <summary>A blow to the wagon: it rocks back on its springs, and a siege bolt stays stuck.</summary>
    private void OnWagonStruck(float damage, Unit attacker, uint lifetime)
    {
        if (!ReducedMotion) _wagonSwaySpeed -= .08f + Mathf.Min(damage, 30f) * .004f;
        if (WagonArt == null || !IsInstanceValid(attacker) || attacker.CombatLifetime != lifetime || !attacker.UsesProjectile) return;
        var style = ProjectileStyles.ForUnit(attacker.DefinitionId, attacker.MotionProfile);
        // Bolts bite into the planks even when they would pass through a soldier; pots and flasks don't stick.
        if (!style.Sticks && !style.Sprite.Contains("bolt")) return;
        // Bolts strike the hull's side; they come in from the right, nosing down.
        var uv = new Vector2(_rng.RandfRange(.32f, .74f), _rng.RandfRange(.5f, .66f));
        if (_wagonBolts.Count == MaxWagonBolts) _wagonBolts.RemoveAt(0);
        _wagonBolts.Add((uv, Mathf.Pi + _rng.RandfRange(-.35f, -.1f), style));
    }

    /// <summary>The caravan falls: its front wheel gives way and it tips forward, smoking and burning.</summary>
    private void FellWagon()
    {
        if (WagonArt == null || _wagonFallenAt >= 0) return;
        _wagonFallenAt = _showTime;
        var art = WagonArt;
        var roof = art.Point(WagonGround, art.Smoke);
        if (ReducedMotion) return;
        SpellVfx.Burst(this, roof, "p_flame", 14, 2.4f, SpellVfx.Fade(new Color("ffd27a"), new Color("c2410c")), 5, 10, 10, 30,
            spread: 25, gravity: -40f, groundRadius: 26f, explosiveness: .2f);
        BattleParticles.SpawnBaseHitDebris(this, art.Point(WagonGround, new Vector2(.74f, .8f)), new Color("6b4a2f"));
    }

    // Tipping forward about the rear wheel as the front one collapses, over half a second; then it lies still.
    private (float Angle, Vector2 Pivot, float Drop) WagonFall()
    {
        if (_wagonFallenAt < 0 || WagonArt is not { } art) return (0f, WagonGround, 0f);
        var t = ReducedMotion ? 1f : Mathf.Clamp((_showTime - _wagonFallenAt) / .55f, 0f, 1f);
        var eased = 1f - Mathf.Pow(1f - t, 3f);
        var bounce = t >= 1f ? 0f : Mathf.Sin(t * Mathf.Pi) * .012f;
        var rear = art.Contacts.Length > 2 ? art.Contacts[2] : new Vector2(.28f, art.Anchor.Y);
        return (eased * .075f + bounce, art.Point(WagonGround, rear), eased * 2.5f);
    }

    /// <summary>World → canvas transform with the wagon's rocking and fall applied about its ground.</summary>
    private Transform2D WagonTransform(CanvasItem canvas)
    {
        var (fall, pivot, drop) = WagonFall();
        var sway = Transform2D.Identity.Translated(-WagonGround).Rotated(_wagonSway).Translated(WagonGround);
        var tip = Transform2D.Identity.Translated(-pivot).Rotated(fall).Translated(pivot + new Vector2(0, drop));
        return Transform2D.Identity.Translated(-((Node2D)canvas).Position) * tip * sway;
    }

    private Color WagonModulate()
    {
        var modulate = FieldLighting.Tint;
        if (_playerBaseFlashTimer > 0f) modulate = new Color(1f, Mathf.Max(.7f, modulate.G - .2f), Mathf.Max(.7f, modulate.B - .2f));
        if (_wagonFallenAt >= 0) modulate = modulate.Darkened(.18f);
        return modulate;
    }

    /// <summary>The plate itself (through the cloth shader): clean or battered, its armour by level and the zone's weather.</summary>
    private void DrawWagonPlate(CanvasItem canvas, BattleStructureArt art)
    {
        canvas.DrawSetTransformMatrix(WagonTransform(canvas));
        var rect = art.At(WagonGround);
        var modulate = WagonModulate();
        canvas.DrawTextureRect(art.Texture, rect, false, modulate);
        if (art.Damaged != null && _wagonDamagedMix > 0f) canvas.DrawTextureRect(art.Damaged, rect, false, new Color(modulate, _wagonDamagedMix));
        if (_wagonArmour != null && _wagonArmorLevel > 0 && art.ArmourRows.Length >= 3)
        {
            // Armour arrives by level: wheels and hubs, then the lower hull, then the whole body.
            var row = _wagonArmorLevel >= 5 ? art.ArmourRows[2] : _wagonArmorLevel >= 3 ? art.ArmourRows[1] : art.ArmourRows[0];
            var size = _wagonArmour.GetSize();
            canvas.DrawTextureRectRegion(_wagonArmour, new Rect2(rect.Position + new Vector2(0, rect.Size.Y * row), rect.Size.X, rect.Size.Y * (1 - row)),
                new Rect2(0, size.Y * row, size.X, size.Y * (1 - row)), modulate);
        }
        if (_wagonWeather != null) canvas.DrawTextureRect(_wagonWeather, rect, false, modulate);
    }

    /// <summary>Over the plate, rocking with it: the troop door, bolts stuck in the hull and the roof crew.</summary>
    private void DrawWagonDetail(CanvasItem canvas)
    {
        if (WagonArt is not { } art) return;
        canvas.DrawSetTransformMatrix(WagonTransform(canvas));
        var rect = art.At(WagonGround);
        var modulate = WagonModulate();
        art.DrawDoor(canvas, rect, WagonDoorFrame, modulate);
        foreach (var (uv, angle, style) in _wagonBolts)
        {
            var texture = ProjectileStyles.Texture(style.Sprite);
            if (texture == null) continue;
            var tip = ProjectileStyles.Meta(style.Sprite).Tip;
            var length = style.Length * 1.1f;
            var height = length * texture.GetHeight() / texture.GetWidth();
            var at = rect.Position + uv * rect.Size;
            var bolt = WagonTransform(canvas) * Transform2D.Identity.Rotated(angle).Translated(at);
            canvas.DrawSetTransformMatrix(bolt * Transform2D.FlipY);
            // The head is buried in the planks: only the shaft and fletching show.
            canvas.DrawTextureRectRegion(texture, new Rect2(-tip.X * length, -tip.Y * height, length * (tip.X - .3f), height),
                new Rect2(0, 0, texture.GetWidth() * (tip.X - .3f), texture.GetHeight()), modulate);
        }
        canvas.DrawSetTransformMatrix(WagonTransform(canvas));
        if (_wagonFallenAt < 0 && _playerBaseHealth > 0) DrawWagonArmaments(canvas);
    }

    /// <summary>Additive light: the hanging lanterns flicker, and the cupola's lamp swells as mana gathers for the
    /// caravan spell, breathing once the spell is ready.</summary>
    private void DrawWagonGlow(CanvasItem canvas)
    {
        if (WagonArt is not { } art || _wagonFallenAt >= 0) return;
        canvas.DrawSetTransformMatrix(WagonTransform(canvas));
        var glow = LanternGlowTexture();
        var strength = .55f + .45f * FieldLighting.LampStrength;
        for (var i = 0; i < art.Lamps.Length; i++)
        {
            var flicker = .78f + .14f * Mathf.Sin(_showTime * (4.7f + i * .9f) + i * 1.7f) + .08f * Mathf.Sin(_showTime * 12.3f + i * 2.9f);
            var radius = 5.5f + .8f * flicker;
            canvas.DrawTextureRect(glow, new Rect2(art.Point(WagonGround, art.Lamps[i]) - Vector2.One * radius, Vector2.One * radius * 2),
                false, new Color(1f, .78f, .42f, .32f * flicker * strength));
        }
        if (art.Lantern is not { } lantern) return;
        var fill = Mathf.Clamp(_mana / Mathf.Max(1f, CaravanSpell.ManaCost), 0f, 1f);
        var ready = CaravanSpellOpen ? .5f + .5f * Mathf.Sin(_showTime * 3.4f) : 0f;
        var size = 7f + fill * 7f + ready * 3f;
        canvas.DrawTextureRect(glow, new Rect2(art.Point(WagonGround, lantern) - Vector2.One * size, Vector2.One * size * 2), false,
            new Color(CaravanBarrageFx.Body.Lerp(new Color(1f, .82f, .5f), .55f), .16f + .3f * fill + .18f * ready));
    }

    private void DrawWagonHud(CanvasItem canvas)
    {
        if (WagonArt is not { } art) return;
        var rect = art.At(WagonGround);
        DrawDamageSmoke(canvas, art.Point(WagonGround, art.Smoke), WagonHealthRatio, ResolveTerrainPalette().PlayerBaseColor);
        DrawBaseHealthMeter(canvas, new Vector2(PlayerBaseX, rect.Position.Y + rect.Size.Y * .11f - 5f), 132f, WagonHealthRatio, true);
    }

    /// <summary>Roof crew and weapons, in the wagon's transform: an archer (the arrow mount), the ballista on its stand
    /// pitching toward its target, and the firepot arm, which kicks back as it throws.</summary>
    private void DrawWagonArmaments(CanvasItem canvas)
    {
        var transform = WagonTransform(canvas);
        for (var i = 0; i < _wagonMounts.Count; i++)
        {
            var position = WagonMountPosition(i);
            var weapon = _wagonMounts[i].Weapon;
            if (weapon.Kind == BaseWeaponKind.Arrows && _wagonArcher != null)
            {
                _wagonArcher.Draw(canvas, position + new Vector2(0, 1.5f), 1f, FieldLighting.Tint);
                continue;
            }
            if (weapon.Kind == BaseWeaponKind.Ballista && DrawBallista(canvas, transform, position)) continue;
            var kick = weapon.Kind == BaseWeaponKind.Firepot ? -_firepotKick * .18f : 0f;
            DrawBaseMountSprite(canvas, transform * Transform2D.Identity.Rotated(kick).Translated(position), weapon);
            canvas.DrawSetTransformMatrix(transform);
        }
    }

    private bool DrawBallista(CanvasItem canvas, Transform2D transform, Vector2 position)
    {
        var stand = BattlefieldTextureLoader.TryLoadStructure("mount_ballista_stand");
        var bow = BattlefieldTextureLoader.TryLoadStructure("mount_ballista_bow");
        if (stand == null || bow == null) return false;
        var scale = _combat.StructureScale;
        var rect = new Rect2(-30f, -59f, 60f, 75f);
        // The bow turns on the stand's pivot, at the top of its upright.
        var pivot = new Vector2(1.2f, -23.5f);
        canvas.DrawSetTransformMatrix(transform * Transform2D.Identity.Scaled(Vector2.One * scale).Translated(position));
        canvas.DrawTextureRect(stand, rect, false, FieldLighting.Tint);
        canvas.DrawSetTransformMatrix(transform * Transform2D.Identity.Translated(-pivot).Rotated(_ballistaAim).Translated(pivot)
            .Scaled(Vector2.One * scale).Translated(position));
        canvas.DrawTextureRect(bow, rect, false, FieldLighting.Tint);
        canvas.DrawSetTransformMatrix(transform);
        return true;
    }

    /// <summary>The crew answers a shot: the archer draws and looses, the ballista snaps to its mark, the firepot kicks.</summary>
    private void AnimateWagonShot(BaseWeaponDefinition weapon, Unit target)
    {
        switch (weapon.Kind)
        {
            case BaseWeaponKind.Arrows when _wagonArcher != null:
                // Joined at the release, so the arrow leaves the bow as the shot does.
                _wagonArcher.Play(UnitAnimState.Attack, Mathf.Max(0, _wagonArcher.ContactFrame - 1));
                break;
            case BaseWeaponKind.Ballista:
                _ballistaAim = _ballistaAimGoal = BallistaPitch(WagonMountPosition(_wagonMounts.FindIndex(m => m.Weapon == weapon)), target.Position);
                break;
            case BaseWeaponKind.Firepot:
                _firepotKick = 1f;
                break;
        }
    }

    /// <summary>A soft white radial glow for additive lights (lanterns, windows, gate light); freed with the battle.</summary>
    private static GradientTexture2D LanternGlowTexture() => _lanternGlow ??= new GradientTexture2D
    {
        Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(.5f, .5f), FillTo = new(.5f, 0),
        Gradient = new Gradient { Offsets = new[] { 0f, .3f, 1f }, Colors = new[] { Colors.White, new Color(1, 1, 1, .35f), new Color(1, 1, 1, 0) } }
    };
}
