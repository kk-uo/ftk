using Godot;

/// <summary>
/// 主菜单专用的像素科技框按钮。框线、角标与图标均为代码绘制，避免位图在不同分辨率下模糊。
/// </summary>
public partial class MainMenuActionButton : Button
{
    public enum Glyph
    {
        Start,
        Continue,
        Tutorial,
        Codex,
        Debug,
        Settings,
        Feedback,
        Exit
    }

    public Glyph ButtonGlyph { get; set; }
    public bool IsPrimary { get; set; }

    private bool _hovered;

    public override void _Ready()
    {
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        Toggled += _ => QueueRedraw();
    }

    public override void _Draw()
    {
        var accent = IsPrimary
            ? new Color(1f, 0.16f, 0.31f)
            : new Color(0.13f, 0.73f, 0.82f);
        if (_hovered || HasFocus())
        {
            accent = IsPrimary ? new Color(1f, 0.40f, 0.50f) : new Color(0.34f, 0.97f, 1f);
        }
        if (Disabled)
        {
            accent = new Color(0.30f, 0.42f, 0.46f, 0.55f);
        }

        DrawFrame(accent);
        DrawGlyph(accent);
        if (IsPrimary)
        {
            DrawChevron(accent);
        }
    }

    private void DrawFrame(Color accent)
    {
        var width = Size.X - 2f;
        var height = Size.Y - 2f;
        const float inset = 3f;
        const float corner = 12f;
        const float thickness = 2f;
        var faint = new Color(accent.R, accent.G, accent.B, accent.A * 0.5f);

        DrawLine(new Vector2(inset + corner, inset), new Vector2(width - corner, inset), accent, thickness, false);
        DrawLine(new Vector2(inset + corner, height), new Vector2(width - corner, height), faint, thickness, false);
        DrawLine(new Vector2(inset, inset + corner), new Vector2(inset, height - corner), accent, thickness, false);
        DrawLine(new Vector2(width, inset + corner), new Vector2(width, height - corner), faint, thickness, false);

        DrawLine(new Vector2(inset, inset + corner), new Vector2(inset + corner, inset), accent, thickness, false);
        DrawLine(new Vector2(width - corner, inset), new Vector2(width, inset + corner), accent, thickness, false);
        DrawLine(new Vector2(inset, height - corner), new Vector2(inset + corner, height), faint, thickness, false);
        DrawLine(new Vector2(width - corner, height), new Vector2(width, height - corner), faint, thickness, false);

        // 断裂的赛博线段，避免形成普通的完整矩形边框。
        DrawLine(new Vector2(26f, height - 7f), new Vector2(68f, height - 7f), faint, 1f, false);
        DrawLine(new Vector2(width - 76f, 7f), new Vector2(width - 34f, 7f), faint, 1f, false);
        DrawLine(new Vector2(width - 54f, height - 7f), new Vector2(width - 18f, height - 7f), accent, 1f, false);
    }

    private void DrawGlyph(Color color)
    {
        var origin = new Vector2(27f, Size.Y * 0.5f);
        var dim = new Color(color.R, color.G, color.B, color.A * 0.74f);
        const float t = 2f;

        switch (ButtonGlyph)
        {
            case Glyph.Start:
                DrawLine(origin + new Vector2(-10, -10), origin + new Vector2(10, 10), color, 3f, false);
                DrawLine(origin + new Vector2(-10, 10), origin + new Vector2(10, -10), color, 3f, false);
                DrawLine(origin + new Vector2(-12, -6), origin + new Vector2(-7, -11), dim, 2f, false);
                DrawLine(origin + new Vector2(7, 11), origin + new Vector2(12, 6), dim, 2f, false);
                break;
            case Glyph.Continue:
                DrawRect(new Rect2(origin + new Vector2(-12, -11), new Vector2(24, 22)), dim, false, t);
                DrawLine(origin + new Vector2(-7, -6), origin + new Vector2(7, -6), color, t, false);
                DrawLine(origin + new Vector2(-7, 0), origin + new Vector2(7, 0), color, t, false);
                DrawLine(origin + new Vector2(-7, 6), origin + new Vector2(3, 6), color, t, false);
                break;
            case Glyph.Tutorial:
                DrawLine(origin + new Vector2(0, -13), origin + new Vector2(13, 0), color, t, false);
                DrawLine(origin + new Vector2(13, 0), origin + new Vector2(0, 13), color, t, false);
                DrawLine(origin + new Vector2(0, 13), origin + new Vector2(-13, 0), color, t, false);
                DrawLine(origin + new Vector2(-13, 0), origin + new Vector2(0, -13), color, t, false);
                DrawLine(origin + new Vector2(-5, 0), origin + new Vector2(5, 0), dim, t, false);
                break;
            case Glyph.Codex:
                DrawRect(new Rect2(origin + new Vector2(-12, -11), new Vector2(24, 22)), color, false, t);
                DrawRect(new Rect2(origin + new Vector2(-6, -5), new Vector2(4, 4)), dim);
                DrawRect(new Rect2(origin + new Vector2(3, -5), new Vector2(4, 4)), dim);
                DrawRect(new Rect2(origin + new Vector2(-6, 4), new Vector2(4, 4)), dim);
                DrawRect(new Rect2(origin + new Vector2(3, 4), new Vector2(4, 4)), dim);
                break;
            case Glyph.Debug:
            case Glyph.Settings:
                DrawArc(origin, 10f, 0f, Mathf.Tau, 12, color, t, false);
                DrawCircle(origin, 3f, dim);
                for (var i = 0; i < 8; i++)
                {
                    var direction = Vector2.FromAngle(i * Mathf.Tau / 8f);
                    DrawLine(origin + direction * 10f, origin + direction * 14f, color, 3f, false);
                }
                break;
            case Glyph.Feedback:
                DrawRect(new Rect2(origin + new Vector2(-13, -10), new Vector2(26, 19)), color, false, t);
                DrawLine(origin + new Vector2(-5, 9), origin + new Vector2(-10, 14), color, t, false);
                DrawLine(origin + new Vector2(-7, -3), origin + new Vector2(7, -3), dim, t, false);
                DrawLine(origin + new Vector2(-7, 3), origin + new Vector2(3, 3), dim, t, false);
                break;
            case Glyph.Exit:
                DrawRect(new Rect2(origin + new Vector2(-12, -12), new Vector2(16, 24)), dim, false, t);
                DrawLine(origin + new Vector2(-2, 0), origin + new Vector2(14, 0), color, 3f, false);
                DrawLine(origin + new Vector2(9, -5), origin + new Vector2(14, 0), color, 3f, false);
                DrawLine(origin + new Vector2(9, 5), origin + new Vector2(14, 0), color, 3f, false);
                break;
        }
    }

    private void DrawChevron(Color color)
    {
        var origin = new Vector2(Size.X - 28f, Size.Y * 0.5f);
        DrawLine(origin + new Vector2(-5f, -8f), origin + new Vector2(4f, 0f), color, 2f, false);
        DrawLine(origin + new Vector2(4f, 0f), origin + new Vector2(-5f, 8f), color, 2f, false);
    }
}
