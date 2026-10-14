using System;

/// <summary>Reading-size preferences apply to wrapping text and scrolling descriptions.</summary>
public static class ReadingText
{
    public static int Size(int authoredSize) => Math.Max(14, authoredSize + (GameState.Instance?.FontSizeOffset ?? 0));
}
