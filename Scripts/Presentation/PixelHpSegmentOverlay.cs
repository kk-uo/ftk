using Godot;

/// <summary>
/// 绘制于敌方血条最上层的像素角标。
/// 保留像素风边缘细节，但不再绘制贯穿血条的黑色分段线，避免干扰血量读数。
/// </summary>
public partial class PixelHpSegmentOverlay : Control
{
    public int SegmentCount { get; set; } = 10;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 4f || Size.Y <= 4f)
            return;

        var accent = new Color(1f, 0.36f, 0.42f, 0.85f);
        DrawRect(new Rect2(2f, 2f, 3f, 2f), accent);
        DrawRect(new Rect2(Mathf.Max(2f, Size.X - 5f), Mathf.Max(2f, Size.Y - 4f), 3f, 2f), accent);
    }
}
