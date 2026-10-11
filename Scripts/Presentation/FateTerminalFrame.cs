using Godot;

/// <summary>初始事件专用切角终端边框。装饰与选项规则分离，不依赖事件图标。</summary>
public partial class FateTerminalFrame : PanelContainer
{
    public Color Accent { get; set; } = new Color("ee285b");
    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        Resized += QueueRedraw;
    }

    public override void _Draw() => DrawFrame(this, Size, Accent, false);

    public static void DrawFrame(Control host, Vector2 size, Color accent, bool hot)
    {
        // 只压低边框亮度，标题和说明保持可读；悬停时略微提亮以提示可点击。
        accent = accent.Darkened(hot ? .25f : .45f);
        var w = size.X; var h = size.Y;
        var points = new[] { new Vector2(20,4), new Vector2(w-26,4), new Vector2(w-5,22),
            new Vector2(w-5,h-24), new Vector2(w-23,h-5), new Vector2(22,h-5),
            new Vector2(5,h-22), new Vector2(5,23), new Vector2(20,4) };
        host.DrawColoredPolygon(points, new Color(.008f,.019f,.028f,.96f));
        host.DrawPolyline(points,new Color(accent,hot ? .16f : .05f),hot ? 12 : 5);
        host.DrawPolyline(points,accent,hot ? 5 : 3);
        host.DrawLine(new Vector2(24,11),new Vector2(w*.72f,11),new Color(accent,.6f),2);
        host.DrawLine(new Vector2(w-12,30),new Vector2(w-12,h*.48f),new Color(accent,.4f),1);
        for(var y=28; y<h-20; y+=6)
            host.DrawLine(new Vector2(14,y),new Vector2(w-14,y),new Color(accent,.025f));
        foreach(var p in new[]{new Vector2(7,22),new Vector2(w-27,4),new Vector2(16,h-13),new Vector2(w-17,h-24)})
            host.DrawRect(new Rect2(p,new Vector2(16,7)),accent);
        // 固定像素缺口和加厚短轨，模拟参考图的破损终端边框，而不是普通细线面板。
        host.DrawRect(new Rect2(24,2,w*.23f,5),accent);
        host.DrawRect(new Rect2(w*.63f,h-8,w*.20f,5),accent);
        for (var i=0; i<4; i++)
        {
            host.DrawRect(new Rect2(3,36+i*13,5,7),new Color(accent,.8f));
            host.DrawRect(new Rect2(w-8,h-48-i*17,5,9),new Color(accent,.7f));
        }
        for(var i=0;i<3;i++) host.DrawRect(new Rect2(w-64+i*12,25,4,5),accent);
    }
}

public partial class FateSelectButton : Button
{
    public Color Accent { get; set; }
    public override void _Ready()
    {
        foreach(var state in new[]{"normal","hover","pressed","focus","disabled"})
            AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        FateTerminalFrame.DrawFrame(this,Size,Accent,IsHovered()||HasFocus());
        var font=GetThemeFont("font");
        DrawString(font,new Vector2(0,Size.Y/2+11),Localization.Get("initial_event.choose_button"),
            HorizontalAlignment.Center,Size.X,30,Accent);
    }
}
