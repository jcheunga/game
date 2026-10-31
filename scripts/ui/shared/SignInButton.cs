using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A sign-in button on the companies' own terms, not in the game's royal style. art/signin/README.md lists Google's
/// and Apple's rules and how each is met. Every sign-in button is the same: Google's dark theme (near-black face,
/// a 1 pt grey rim, 4 pt corners), the company's logo on a white tile at the leading edge, and a Google Sans Medium
/// title at one size, starting at one column after the tile, so a stack of them lines up and none outranks
/// another. The face is drawn here; the title stays the button's <see cref="Button.Text"/> for tests and screen
/// readers. Presses come from <see cref="ButtonFeel"/> like any button: the whole face sinks and darkens. Apple's
/// logo and the Game Center icon come only from Apple's own files; until those are added their white tile stays
/// empty (never a stand-in logo) and the button keeps its size, title column and action.
/// </summary>
public partial class SignInButton : Button
{
    public enum Brand { Google, Apple, PlayGames, GameCenter }

    /// <summary>Canvas pixels: 44 pt on a phone (a 390 pt tall phone shows 0.54 pt per canvas pixel).</summary>
    public const float Height = 80;
    /// <summary>Apple's smallest Sign in with Apple button is 140 pt wide.</summary>
    public const float MinWidth = 260;
    /// <summary>The white logo tile, square, centred on the button's height.</summary>
    public const float TileSize = 60;

    // Google's spec in points of its own button: iOS is 44 tall with 12 after the G and 16 after the title; Android
    // and the web are 40 tall with 10 and 12. The G is 20, the title Google Sans Medium 14 and the corners 4 on both.
    private static readonly bool Ios = OS.GetName() == "iOS";
    public static float Unit => Height / (Ios ? 44 : 40);
    public static float LogoSize => 20 * Unit;
    public static float TitleSize => 14 * Unit;
    private static float Inset => (Height - TileSize) / 2;
    private static float Gap => (Ios ? 12 : 10) * Unit;
    private static float Trail => (Ios ? 16 : 12) * Unit;
    private static float Corner => 4 * Unit;

    // Google's dark theme: #131314 fill, a 1 pt #8E918F rim inside it, #E3E3E3 text. The G keeps the white
    // background Google asks for, as a tile; the other logos share the tile so every button matches.
    private static readonly Color Fill = new("131314"), Rim = new("8e918f"), Ink = new("e3e3e3"), Tile = Colors.White;
    // A disabled button is dimmed under one veil, so the G's own white edge stays seamless with its tile.
    private static readonly Color DisabledVeil = new(.07f, .07f, .08f, .62f);

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Font _font;

    public Brand Kind { get; private set; }

    public SignInButton()
    {
        // The button's own text is never drawn and never sizes it (see RoyalButton for why that matters).
        ClipText = true;
        AddThemeFontSizeOverride("font_size", 1);
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            AddThemeColorOverride(key, Colors.Transparent);
        var empty = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) AddThemeStyleboxOverride(state, empty);
        CustomMinimumSize = new Vector2(MinWidth, Height);
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public static SignInButton Create(Brand brand, string title, Action action)
    {
        var button = new SignInButton { Kind = brand, Text = title, AccessibilityName = title, TooltipText = title };
        button.Pressed += () => action?.Invoke();
        return button;
    }

    /// <summary>
    /// A sign-in title. Google and Apple allow "Continue with Google" and "Continue with Apple"; Play Games lets a
    /// game name it "Play Games" (the full "Google Play Games" leaves no margin at this size); Game Center keeps
    /// Apple's spelling.
    /// </summary>
    public static string Title(Brand brand) => "Continue with " + brand switch
    {
        Brand.Google => "Google", Brand.Apple => "Apple", Brand.PlayGames => "Play Games", _ => "Game Center"
    };

    public static Font TitleFont => _font ??= ResourceLoader.Load<FontFile>("res://assets/fonts/GoogleSans-Medium.ttf");

    /// <summary>The size every title is drawn at: Google's 14 pt, smaller only if a title would not fit its margins.</summary>
    public float TitleFontSize
    {
        get
        {
            var size = Mathf.Round(TitleSize);
            var room = Size.X - Trail - TitleStart;
            var width = TitleFont.GetStringSize(Text, HorizontalAlignment.Left, -1, (int)size).X;
            return width <= room || width <= 0 ? size : Mathf.Floor(size * room / width);
        }
    }

    /// <summary>The white tile behind the logo, in the button's own space.</summary>
    public static Rect2 TileRect => new(Inset, Inset, TileSize, TileSize);

    // Every title starts at Google's gap after the tile, so titles line up from button to button.
    private static float TitleStart => Mathf.Round(TileRect.End.X + Gap);

    /// <summary>
    /// Where the logo art is drawn, centred on the tile: the G at Google's 20 pt (its cut keeps 1 pt of white round
    /// it), the controller badge with its own margin, the Game Center icon at 20 pt, and Apple's square logo-only file
    /// whole, as tall as the tile. Empty while a brand's art is missing.
    /// </summary>
    public Rect2 LogoRect
    {
        get
        {
            if (LogoArt(Kind) == null) return new Rect2(TileRect.GetCenter(), Vector2.Zero);
            var side = Kind switch { Brand.Apple => TileSize, Brand.GameCenter => LogoSize, _ => LogoSize * 22f / 20 };
            return new Rect2((TileRect.GetCenter() - Vector2.One * side / 2).Round(), Vector2.One * side);
        }
    }

    /// <summary>Whether a brand's official logo file is in the game.</summary>
    public static bool HasLogo(Brand brand) => LogoArt(brand) != null;

    /// <summary>Where the title is drawn: from the shared title column, centred on the button's height.</summary>
    public Rect2 TitleRect
    {
        get
        {
            var font = TitleFont; var size = TitleFontSize;
            var width = font.GetStringSize(Text, HorizontalAlignment.Left, -1, (int)size).X;
            var height = font.GetHeight((int)size);
            return new Rect2(TitleStart, Mathf.Round((Size.Y - height) / 2), width, height);
        }
    }

    public override void _Draw()
    {
        var rim = Mathf.Max(1, Mathf.Round(Unit));
        var face = new StyleBoxFlat { BgColor = Fill, BorderColor = Rim, AntiAliasing = true };
        face.SetBorderWidthAll((int)rim);
        face.SetCornerRadiusAll((int)Mathf.Round(Corner));
        DrawStyleBox(face, new Rect2(Vector2.Zero, Size));
        // Every button has its white tile, so the column matches; a brand whose official art is not in yet keeps it empty.
        var tile = new StyleBoxFlat { BgColor = Tile, AntiAliasing = true };
        tile.SetCornerRadiusAll((int)Mathf.Round(Corner * .75f));
        DrawStyleBox(tile, TileRect);
        if (LogoArt(Kind) is { } art) DrawTextureRect(art, LogoRect, false);
        var title = TitleRect; var font = TitleFont; var size = (int)TitleFontSize;
        DrawString(font, new Vector2(title.Position.X, title.Position.Y + font.GetAscent(size)), Text, HorizontalAlignment.Left, -1, size, Ink);
        if (!Disabled) return;
        var veil = new StyleBoxFlat { BgColor = DisabledVeil, AntiAliasing = true };
        veil.SetCornerRadiusAll((int)Mathf.Round(Corner));
        DrawStyleBox(veil, new Rect2(Vector2.Zero, Size));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    internal static Texture2D LogoArt(Brand brand)
    {
        var path = "res://assets/ui/signin/" + brand switch
        {
            Brand.Google => "google-g.png", Brand.Apple => "apple-logo.svg", Brand.PlayGames => "play-games-controller.png", _ => "game-center.png"
        };
        if (Textures.TryGetValue(path, out var texture)) return texture;
        return Textures[path] = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}
