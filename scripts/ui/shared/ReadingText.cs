using System;

/// <summary>Reading-size preferences apply to wrapping text and scrolling descriptions.</summary>
public static class ReadingText
{
    // The game draws one 1280x720 canvas scaled to fit the screen, so text is sized for the smallest it will be
    // seen at: a landscape phone shows the canvas about 390 pt tall (0.54 pt per canvas px). Phones are the target,
    // so the floors meet Apple's 11 pt minimum there: mixed-case text 22 px (about 12 pt), engraved capitals and
    // figures 20 px (Cinzel's capitals stand as tall as an 11 pt book face's).
    public const int PhoneBody = 22, PhoneCaps = 20;

    public static int Size(int authoredSize) => Math.Max(PhoneBody, authoredSize + (GameState.Instance?.FontSizeOffset ?? 0));
}
