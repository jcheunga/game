using Godot;

/// <summary>
/// What lingering magic looks like on a body, beside its status tag: flames on the burning, thorns or chains
/// on the rooted, a shimmer around a shield or a projectile mirror, stars over the stunned and a rune over
/// the silenced. Drawn over the sprite in the unit's own space; textures come from SpellVfx.
/// </summary>
public partial class Unit
{
    private float StatusHeight => _spriteSheet == null ? Radius + 16
        : Radius * 2 * VisualScale * _spriteSheet.DrawScale * _spriteSheet.FrameHeight / _spriteSheet.FrameWidth * _spriteSheet.HealthBarY;

    private void DrawStatusVisuals()
    {
        if (IsDead || TacticalArt != null) return;
        var height = StatusHeight;
        var clock = ContentAge;
        var still = GameState.Instance?.ReducedMotion ?? false;
        if (_rootTime > 0f && !IsControlled)
        {
            if (ContentState == "anchored")
            {
                var link = SpellVfx.Tex("p_chain");
                for (var side = -1; side <= 1; side += 2)
                {
                    var from = new Vector2(side * Radius * 1.4f, 2); var to = new Vector2(side * Radius * .3f, -height * .42f);
                    var span = to - from; var steps = Mathf.Max(2, (int)(span.Length() / 10f));
                    for (var i = 0; i <= steps; i++)
                    {
                        DrawSetTransformMatrix(new Transform2D(span.Angle() + (i % 2) * .25f, from + span * i / steps));
                        DrawTextureRect(link, new Rect2(-7, -3.5f, 14, 7), false, new Color(.9f, .75f, 1f, .9f));
                    }
                }
                DrawSetTransform(Vector2.Zero);
            }
            else
            {
                var width = Radius * 3.4f;
                DrawTextureRect(SpellVfx.Tex("rise_bramble"), new Rect2(-width / 2, -width * .62f, width, width * .66f), false, new Color(1, 1, 1, .95f));
            }
        }
        if (IsBurning)
        {
            var flame = SpellVfx.Tex("p_flame");
            for (var i = 0; i < 3; i++)
            {
                var flicker = still ? 1f : .75f + .25f * Mathf.Sin(clock * 17f + i * 2.1f);
                var size = Radius * (1.1f + .25f * i) * flicker;
                var at = new Vector2((i - 1) * Radius * .55f, -height * (.18f + .2f * i));
                DrawTextureRect(flame, new Rect2(at - new Vector2(size * .5f, size), new Vector2(size, size * 1.3f)), false, new Color(1, 1, 1, .85f));
            }
        }
        if (ShieldHealth > 0 || ProjectileMirrorTime > 0)
        {
            var tint = ShieldHealth > 0 ? new Color(1f, .86f, .5f) : new Color(.85f, .95f, 1f);
            var shimmer = still ? .5f : .42f + .14f * Mathf.Sin(clock * 4f);
            var size = new Vector2(Radius * 3.4f, height * 1.15f);
            DrawTextureRect(SpellVfx.Ring, new Rect2(new Vector2(-size.X / 2, -height * 1.05f), size), false, new Color(tint, shimmer));
        }
        if (IsControlled)
        {
            var star = SpellVfx.Tex("p_star");
            for (var i = 0; i < 3; i++)
            {
                var angle = (still ? 0 : clock * 4f) + i * Mathf.Tau / 3f;
                var at = new Vector2(Mathf.Cos(angle) * Radius * .9f, -height - 4 + Mathf.Sin(angle) * Radius * .3f);
                DrawTextureRect(star, new Rect2(at - Vector2.One * 6, Vector2.One * 12), false, new Color(1f, .93f, .6f, .95f));
            }
        }
        if (IsSilenced)
        {
            var pulse = still ? .8f : .6f + .3f * Mathf.Sin(clock * 5f);
            DrawTextureRect(SpellVfx.Tex("p_rune"), new Rect2(new Vector2(-9, -height - 30), Vector2.One * 18), false, new Color(.8f, .6f, 1f, pulse));
        }
    }
}
