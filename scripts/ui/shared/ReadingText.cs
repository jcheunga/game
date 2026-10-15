using System;

/// <summary>Reading-size preferences apply to wrapping text and scrolling descriptions.</summary>
public static class ReadingText
{
    // The game draws one 1280x720 canvas scaled to fit the screen, so text is sized for the smallest it will be
    // seen at: a landscape phone shows the canvas about 390 pt tall (0.54 pt per canvas px). Mixed-case text needs
    // 18 px there (about 9.7 pt); engraved capitals and figures read at 16.
    public const int PhoneBody = 18, PhoneCaps = 16;

    public static int Size(int authoredSize) => Math.Max(PhoneBody, authoredSize + (GameState.Instance?.FontSizeOffset ?? 0));
}
