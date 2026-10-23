using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A sign-in button drawn to the published rules of the company it signs in with, not in the game's royal style.
/// art/signin/README.md lists Google's and Apple's rules and how each is met. Every sign-in button shares one size
/// and face so none is more prominent than another: white, a 1 pt grey rim, 4 pt corners, the company's own logo
/// at the leading edge and a Google Sans Medium title centred in the space after it. The face is drawn here; the
/// title stays the button's <see cref="Button.Text"/> for tests and screen readers. Presses come from
/// <see cref="ButtonFeel"/> like any button: the whole face sinks and darkens. Apple's logo and the Game Center
/// icon come only from Apple's own files; until those are added the logo box stays empty (never a stand-in logo)
/// and the button keeps its size, title and action.
/// </summary>
public partial class SignInButton : Button
{
    public enum Brand { Google, Apple, PlayGames, GameCenter }

    /// <summary>Canvas pixels: 44 pt on a phone (a 390 pt tall phone shows 0.54 pt per canvas pixel).</summary>
    public const float Height = 80;
    /// <summary>Apple's smallest Sign in with Apple button is 140 pt wide.</summary>
    public const float MinWidth = 260;
    /// <summary>Apple's title is 43% of the button's height in any font.</summary>
    public const float AppleTitleShare = .43f;
    /// <summary>Apple keeps at least 8% of the button's width between the title and the trailing edge.</summary>
    public const float AppleTrailShare = .08f;

    // Google's spec in points of its own button: iOS is 44 tall with 16 before the G and 12 after it; Android and
    // the web are 40 tall with 12 and 10. The G is 20, the title Google Sans Medium 14 and the corners 4 on both.
    private static readonly bool Ios = OS.GetName() == "iOS";
    public static float Unit => Height / (Ios ? 44 : 40);
    public static float LogoSize => 20 * Unit;
    public static float TitleSize => 14 * Unit;
    private static float Lead => (Ios ? 16 : 12) * Unit;
    private static float Gap => (Ios ? 12 : 10) * Unit;
    private static float Trail => (Ios ? 16 : 12) * Unit;
    private static float Corner => 4 * Unit;

    // Google's light theme: white fill, a 1 pt #747775 rim inside it, #1F1F1F text. Apple's white style is a white
    // fill with a black logo and title; Apple lets a custom button add a bezel stroke, so it shares Google's rim.
    private static readonly Color Fill = Colors.White, Rim = new("747775"), GoogleInk = new("1f1f1f"), AppleInk = Colors.Black;
    private const float DisabledFade = .45f;

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Font _font;
    private static (Texture2D, float, float)? _appleArt;
    private static bool _appleMeasured;

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

    /// <summary>The title's size by its company's spec: Apple's share of the height, Google's 14 pt for the rest.</summary>
    public float SpecTitleSize => Kind == Brand.Apple ? Mathf.Round(Height * AppleTitleShare) : Mathf.Round(TitleSize);

    /// <summary>The size the title is drawn at: the spec size, or smaller only when a title would not fit its margins.</summary>
    public float TitleFontSize
    {
        get
        {
            var size = SpecTitleSize;
            var (start, end) = TitleSpan;
            var width = TitleFont.GetStringSize(Text, HorizontalAlignment.Left, -1, (int)size).X;
            return width <= end - start || width <= 0 ? size : Mathf.Floor(size * (end - start) / width);
        }
    }

    // Every title is centred in the same span, so titles line up from button to button. It starts after the 20 pt
    // logo box and Google's gap, or after Apple's file (whose own padding is the least gap Apple allows), and ends
    // at Google's trailing padding or Apple's 8% of the width, whichever is wider.
    private (float Start, float End) TitleSpan =>
        (Mathf.Max(Lead + LogoSize + Gap, AppleArtRect(Size.Y).End.X), Size.X - Mathf.Max(Trail, Size.X * AppleTrailShare));

    /// <summary>Where the logo art is drawn, in the button's own space. Empty while a brand's art is missing.</summary>
    public Rect2 LogoRect => Kind == Brand.Apple ? AppleArtRect(Size.Y) : LogoArt(Kind) == null ? new Rect2(Lead, Size.Y / 2, 0, 0) : BadgeRect(Kind, Size.Y);

    // The G art keeps 1 pt of white round its 20 pt box and the controller badge a margin of its own; the Game Center
    // icon fills the box.
    private static Rect2 BadgeRect(Brand brand, float height)
    {
        var side = LogoSize * (brand == Brand.GameCenter ? 1 : 22f / 20);
        return new Rect2(Mathf.Round(Lead + (LogoSize - side) / 2), Mathf.Round((height - side) / 2), side, side);
    }

    // Apple's logo-and-text file is drawn as tall as the button, uncropped and with its own padding. It slides only
    // to put its logo in line with the G (Apple lets a custom button inset the logo from the leading edge).
    private static Rect2 AppleArtRect(float height)
    {
        if (AppleArt is not { } art) return new Rect2(Lead, 0, 0, height);
        var width = art.Texture.GetWidth() * height / art.Texture.GetHeight();
        return new Rect2(Mathf.Round(Lead + LogoSize / 2 - width * art.LogoCentre), 0, width, height);
    }

    /// <summary>
    /// Apple's file, with where its logo sits across the file and how much of its height the logo fills (measured
    /// once from the art). Null until the official file is added (art/signin/README.md).
    /// </summary>
    public static (Texture2D Texture, float LogoCentre, float LogoShare)? AppleArt
    {
        get
        {
            if (_appleMeasured) return _appleArt;
            _appleMeasured = true;
            if (LogoArt(Brand.Apple) is not { } texture || texture.GetImage() is not { } image) return null;
            int left = image.GetWidth(), right = -1, top = image.GetHeight(), bottom = -1;
            for (var y = 0; y < image.GetHeight(); y++)
            for (var x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A < .5f) continue;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
            if (right < 0) return null;
            return _appleArt = (texture, (left + right + 1) / 2f / image.GetWidth(), (bottom - top + 1f) / image.GetHeight());
        }
    }

    /// <summary>Whether a brand's official logo file is in the game.</summary>
    public static bool HasLogo(Brand brand) => LogoArt(brand) != null;

    /// <summary>Where the title is drawn: centred between the logo and the trailing margin.</summary>
    public Rect2 TitleRect
    {
        get
        {
            var font = TitleFont; var size = TitleFontSize;
            var (start, end) = TitleSpan;
            var width = font.GetStringSize(Text, HorizontalAlignment.Left, -1, (int)size).X;
            var x = start + Mathf.Max(0, end - start - width) / 2;
            var height = font.GetHeight((int)size);
            return new Rect2(Mathf.Round(x), Mathf.Round((Size.Y - height) / 2), width, height);
        }
    }

    public override void _Draw()
    {
        var fade = Disabled ? DisabledFade : 1;
        var rim = Mathf.Max(1, Mathf.Round(Unit));
        var face = new StyleBoxFlat { BgColor = Fade(Fill, fade), BorderColor = Fade(Rim, fade), AntiAliasing = true };
        face.SetBorderWidthAll((int)rim);
        face.SetCornerRadiusAll((int)Mathf.Round(Corner));
        DrawStyleBox(face, new Rect2(Vector2.Zero, Size));
        if (LogoArt(Kind) is { } art) DrawTextureRect(art, LogoRect, false, Fade(Colors.White, fade));
        var title = TitleRect; var font = TitleFont; var size = (int)TitleFontSize;
        var ink = Fade(Kind == Brand.Apple ? AppleInk : GoogleInk, fade);
        DrawString(font, new Vector2(title.Position.X, title.Position.Y + font.GetAscent(size)), Text, HorizontalAlignment.Left, -1, size, ink);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    private static Color Fade(Color color, float fade) => new(color, color.A * fade);

    private static Texture2D LogoArt(Brand brand)
    {
        var path = "res://assets/ui/signin/" + brand switch
        {
            Brand.Google => "google-g.png", Brand.Apple => "apple-logo.svg", Brand.PlayGames => "play-games-controller.png", _ => "game-center.png"
        };
        if (Textures.TryGetValue(path, out var texture)) return texture;
        return Textures[path] = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}
