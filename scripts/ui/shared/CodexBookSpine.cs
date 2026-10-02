using Godot;

/// <summary>A narrow shaded binding between the codex's parchment pages.</summary>
public partial class CodexBookSpine : Control
{
    public CodexBookSpine() { CustomMinimumSize = new Vector2(20, 0); MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        for (int x = 0; x < 20; x++)
        {
            var distance = Mathf.Abs(x - 9.5f) / 9.5f;
            var color = new Color("8b6b3c").Lerp(new Color("dcc491"), distance);
            DrawRect(new Rect2(x, 0, 1, Size.Y), color);
        }
        DrawLine(new Vector2(9, 8), new Vector2(9, Size.Y - 8), new Color("4a382b66"), 1);
    }
}
