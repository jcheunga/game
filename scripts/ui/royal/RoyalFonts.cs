using System.Collections.Generic;
using Godot;

/// <summary>
/// The concept typography: Cinzel for engraved capitals (titles, captions, buttons),
/// Crimson Pro for readable book text and Grenze Gotisch for the gothic hand of battle text.
/// All are variable fonts; weights are cached variations.
/// </summary>
public static class RoyalFonts
{
    private const string DisplayPath = "res://assets/fonts/Cinzel-Variable.ttf";
    private const string BodyPath = "res://assets/fonts/CrimsonPro-Variable.ttf";
    private const string GothicPath = "res://assets/fonts/GrenzeGotisch-Variable.ttf";
    private static readonly Dictionary<(string, int), Font> Cache = new();

    /// <summary>Engraved Roman capitals. Mixed-case text renders as small caps with tall initials.</summary>
    public static Font Display(int weight = 700) => Variation(DisplayPath, weight);

    /// <summary>Whether a font is the engraved display face (button captions in it read as full capitals).</summary>
    public static bool IsDisplay(Font font) => font is FontVariation { BaseFont: FontFile file } && file.ResourcePath == DisplayPath;

    /// <summary>Book serif for names, descriptions and numbers.</summary>
    public static Font Body(int weight = 500) => Variation(BodyPath, weight);

    /// <summary>A readable gothic blackletter, for the numbers and words that rise over the battlefield.</summary>
    public static Font Gothic(int weight = 700) => Variation(GothicPath, weight);

    private static Font Variation(string path, int weight)
    {
        if (Cache.TryGetValue((path, weight), out var cached)) return cached;
        var file = ResourceLoader.Load<FontFile>(path);
        var font = new FontVariation { BaseFont = file };
        font.VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), weight } };
        return Cache[(path, weight)] = font;
    }
}
