using System.Collections.Generic;
using Godot;

// Outworks flanking the stronghold: walls, a beacon tower, braziers and barricades. Pure scenery: they sort
// with troops for depth but are never targets, never take hits and never block movement.
public partial class BattleController
{
    // Ground positions relative to the stronghold's strike point (EnemyBaseX, band centre). The approach to its
    // left stays clear; dressing sits beyond the band's edges or behind the gate, where troops never walk.
    private static readonly (string Id, Vector2 Offset)[] OutworkLayout =
    {
        ("fort_tower", new Vector2(30, -24)),
        ("fort_wall", new Vector2(-50, -18)),
        ("fort_brazier", new Vector2(-84, -16)),
        ("fort_palisade", new Vector2(-50, 44)),
        ("fort_totem", new Vector2(30, 44)),
    };
    private readonly List<(BattleStructureArt Art, Vector2 Ground)> _outworks = new();

    private void BuildOutworks()
    {
        foreach (var (id, offset) in OutworkLayout)
        {
            var art = BattleStructureArt.Load(id);
            if (art == null) continue;
            var ground = EnemyBaseCorePosition + offset;
            _outworks.Add((art, ground));
            AddChild(new BattleBaseCanvas { Name = "Outwork " + id, Position = ground,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                Paint = canvas => canvas.DrawTextureRect(art.Texture, art.At(ground), false, FieldLighting.Tint) });
        }
    }

    private void DrawOutworkGround(CanvasItem canvas)
    {
        var light = FieldLighting;
        foreach (var (art, ground) in _outworks)
        {
            light.DrawShadow(canvas, art.Texture, new Rect2(Vector2.Zero, art.Texture.GetSize()), art.At(Vector2.Zero), ground, 1, 2.4f);
            foreach (var lamp in art.Lights) light.DrawLampGlow(canvas, art.Point(ground, lamp), true);
        }
    }
}
