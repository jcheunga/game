using Godot;

/// <summary>One sun direction and environmental light shared by every battle actor.</summary>
public readonly record struct BattleLighting(Color Tint, float ShadowOpacity, Vector2 ShadowCast)
{
    private static GradientTexture2D _glowTexture;
    public float LampStrength => Tint.R < .96f ? 1f : .4f;
    public static BattleLighting ForZone(string zone) => zone switch
    {
        "harbor" => new(new Color(1.01f, 1.06f, 1.10f), .25f, new(.64f, .27f)),
        "foundry" => new(new Color(1.13f, .98f, .86f), .30f, new(.80f, .31f)),
        "quarantine" => new(new Color(.90f, 1.00f, .91f), .25f, new(.66f, .27f)),
        "thornwall" => new(new Color(.99f, 1.05f, .94f), .27f, new(.72f, .28f)),
        "basilica" => new(new Color(.96f, 1.02f, 1.09f), .24f, new(.62f, .26f)),
        "mire" => new(new Color(.85f, .98f, .90f), .24f, new(.64f, .26f)),
        "steppe" => new(new Color(1.11f, 1.07f, .95f), .30f, new(.80f, .30f)),
        "gloamwood" => new(new Color(.80f, .92f, 1.07f), .22f, new(.65f, .27f)),
        "citadel" => new(new Color(.95f, 1.00f, 1.08f), .28f, new(.72f, .28f)),
        _ => new(new Color(1.10f, 1.05f, .96f), .29f, new(.76f, .29f))
    };

    public void DrawContact(CanvasItem canvas, Vector2 point, Vector2 radius, float opacity = 1f)
    {
        // Layered ellipses soften into the ground, with a denser foot/wheel contact.
        for (var layer = 3; layer >= 0; layer--)
        {
            var spread = 1 + layer * .22f;
            canvas.DrawSetTransform(point, 0, radius * spread);
            canvas.DrawCircle(Vector2.Zero, 1, new Color(.025f, .035f, .05f, opacity * (.045f + (3 - layer) * .017f)));
        }
        canvas.DrawSetTransform(Vector2.Zero);
    }

    public void DrawLampGlow(CanvasItem canvas, Vector2 point, bool castle)
    {
        _glowTexture ??= new GradientTexture2D { Width = 128, Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(.5f, .5f), FillTo = new(.5f, 0),
            Gradient = new Gradient { Offsets = new[] { 0f, .35f, 1f },
                Colors = new[] { Colors.White, new Color(1, 1, 1, .32f), new Color(1, 1, 1, 0) } } };
        var color = castle ? new Color(.42f, 1.22f, 1.06f) : new Color(1.42f, .92f, .38f);
        var radius = new Vector2(36, 15);
        canvas.DrawTextureRect(_glowTexture, new Rect2(point - radius, radius * 2), false, new Color(color, .13f * LampStrength));
    }

    public static void ClearCache()
    {
        if (_glowTexture == null) return;
        _glowTexture.Gradient.Dispose(); _glowTexture.Dispose(); _glowTexture = null;
    }

    public void DrawShadow(CanvasItem canvas, Texture2D texture, Rect2 frame, Rect2 sprite, Vector2 feet, float facing = 1, float softness = 1, float opacity = 1)
    {
        // Project the current alpha silhouette onto the same ground plane. Mirroring
        // changes the actor's facing, while sunlight continues to fall from the left.
        var horizontal = new Vector2(facing < 0 ? -1 : 1, 0);
        var vertical = -ShadowCast;
        var ink = new Color(.02f, .028f, .045f, ShadowOpacity * opacity);
        for (var sample = 0; sample < 5; sample++)
        {
            var offset = sample switch { 1 => Vector2.Left, 2 => Vector2.Right, 3 => Vector2.Up, 4 => Vector2.Down, _ => Vector2.Zero };
            canvas.DrawSetTransformMatrix(new Transform2D(horizontal, vertical, feet + offset * softness));
            canvas.DrawTextureRectRegion(texture, sprite, frame, new Color(ink, ink.A * (sample == 0 ? .4f : .15f)));
        }
        canvas.DrawSetTransform(Vector2.Zero);
    }
}
