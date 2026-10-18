using Godot;

// The stage's painted stronghold (EnemyBaseArt): drawn as painted, behind the road, with its witch-fire lanterns
// breathing over it and damage smoke rising from its tallest roofs.
public partial class BattleController
{
    // The health meter's world row: above the gate, low enough to stay in shot on phones, whose view starts higher.
    private const float PaintedBaseBarY = 270f;
    private static GradientTexture2D _lanternGlow;

    private void DrawPaintedBase(CanvasItem canvas, EnemyBaseArt art, float healthRatio, TerrainPalette palette)
    {
        // It was painted in the zone's own light, so it takes no field tint; a hit flushes it red as before.
        var modulate = _enemyBaseFlashTimer > 0f ? new Color(1.3f, .8f, .8f) : Colors.White;
        canvas.DrawTextureRect(art.Texture, art.Rect, false, modulate);
        // Each roof starts smoking a quarter of the stronghold's health after the one before.
        for (var i = 0; i < art.Smoke.Length; i++)
            DrawDamageSmoke(canvas, art.Smoke[i], healthRatio + i * .25f, palette.EnemyBaseColor);
        DrawBaseHealthMeter(canvas, new Vector2(art.GateX, PaintedBaseBarY), 132f, healthRatio, false);
    }

    // Added just after the stronghold, so it shares its depth: an additive glow over each painted lantern.
    private void BuildPaintedBaseGlow()
    {
        if (PaintedBase.Lights.Length == 0) return;
        _lanternGlow ??= new GradientTexture2D { Width = 64, Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(.5f, .5f), FillTo = new(.5f, 0),
            Gradient = new Gradient { Offsets = new[] { 0f, .3f, 1f },
                Colors = new[] { Colors.White, new Color(1, 1, 1, .35f), new Color(1, 1, 1, 0) } } };
        AddChild(new BattleBaseCanvas { Name = "CastleGlow", Position = CastleSortPosition,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            TextureFilter = TextureFilterEnum.Linear, Paint = DrawPaintedBaseGlow });
    }

    private static void ClearLanternGlow()
    {
        if (_lanternGlow == null) return;
        _lanternGlow.Gradient.Dispose(); _lanternGlow.Dispose(); _lanternGlow = null;
    }

    private void DrawPaintedBaseGlow(CanvasItem canvas)
    {
        var lights = PaintedBase.Lights;
        for (var i = 0; i < lights.Length; i++)
        {
            var (point, colour) = lights[i];
            // Two detuned sines per lantern, so no two flicker together.
            var flicker = .72f + .18f * Mathf.Sin(_elapsed * (5.3f + i * .7f) + i * 2.1f) + .1f * Mathf.Sin(_elapsed * 13.7f + i);
            var radius = 7f + 1.2f * flicker;
            canvas.DrawTextureRect(_lanternGlow, new Rect2(point - Vector2.One * radius, Vector2.One * radius * 2), false,
                new Color(colour, .30f * flicker));
        }
    }
}
