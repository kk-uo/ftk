using Godot;

/// <summary>芯片外壳由界面绘制，头像复用角色资源；不改变解锁或选人规则。</summary>
public partial class HeroChipButton : Button
{
    public const int ChipWidth = 210;
    public const int ChipHeight = 280;
    public CharacterData Hero { get; set; } = null!;
    public bool Unlocked { get; set; }
    public int Serial { get; set; }
    private Texture2D? _portrait;
    private bool _selected;
    private float _scan;
    private Tween? _selectionTween;
    public static Color FactionColor(Faction faction) => faction switch
    {
        Faction.Wu => new Color("f02a63"), Faction.Shu => new Color("35d5a0"),
        Faction.Wei => new Color("37b9e8"), _ => new Color("b18be8")
    };

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(ChipWidth, ChipHeight);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        TextureFilter = TextureFilterEnum.Nearest;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _portrait = Unlocked ? CharacterVisualDatabase.TryGetPortraitTexture(Hero.Id) : null;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        Resized += QueueRedraw;
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        _selectionTween?.Kill();
        Scale = Vector2.One;
        _scan = 0;
        if (selected)
        {
            PivotOffset = Size / 2;
            _selectionTween = CreateTween();
            _selectionTween.TweenProperty(this, "scale", new Vector2(1.045f, 1.045f), .12);
            _selectionTween.TweenProperty(this, "scale", Vector2.One, .22).SetTrans(Tween.TransitionType.Back);
            _selectionTween.Parallel().TweenMethod(Callable.From<float>(v => { _scan = v; QueueRedraw(); }), 0f, 1f, .45);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Hero == null) return;
        var accent = Unlocked ? FactionColor(Hero.Faction) : new Color("344653");
        var border = _selected ? new Color("ffda45") : accent;
        var w = Size.X; var h = Size.Y;
        var outline = new[] { new Vector2(12, 3), new Vector2(w-16, 3), new Vector2(w-3, 16),
            new Vector2(w-3, h-20), new Vector2(w-16,h-7), new Vector2(12,h-7), new Vector2(3,h-20), new Vector2(3,16), new Vector2(12,3) };
        DrawColoredPolygon(outline, new Color("08121b"));
        if (_selected) DrawPolyline(outline, new Color(border, .2f), 12);
        DrawPolyline(outline, border, _selected ? 4 : 2);
        DrawRect(new Rect2(12, 14, w-24, h-36), new Color(accent, .35f), false, 1);
        if (_portrait != null)
        {
            var target = new Rect2(20, 30, w-40, h-94);
            var sourceSize = _portrait.GetSize();
            var cropSize = target.Size / Mathf.Max(target.Size.X/sourceSize.X, target.Size.Y/sourceSize.Y);
            DrawTextureRectRegion(_portrait, target, new Rect2((sourceSize-cropSize)/2, cropSize), new Color(.86f,.9f,.95f));
        }
        for (var y=36; y<h-65; y+=5)
            DrawLine(new Vector2(18,y), new Vector2(w-18,y), new Color(0,0,0,.2f));
        for (var x=23; x<w-22; x+=9)
            DrawRect(new Rect2(x,h-15,5,12), Unlocked ? new Color("baa672") : new Color("444537"));
        for (var y=50; y<h-52; y+=18)
            DrawRect(new Rect2(7,y,5,9), new Color(accent,.7f));
        var font = GetThemeFont("font");
        DrawString(font,new Vector2(22,29),Serial.ToString("D2"),HorizontalAlignment.Left,-1,16,border);
        if (!Unlocked) DrawString(font,new Vector2(0,h*.58f),"?",HorizontalAlignment.Center,w,64,new Color("53606b"));
        DrawRect(new Rect2(17,h-66,w-34,43),new Color(.015f,.025f,.04f,.94f));
        DrawString(font,new Vector2(24,h-40),Unlocked ? Hero.Name : "未解锁",HorizontalAlignment.Left,w-45,24,Unlocked ? Colors.White : new Color("63717c"));
        if (Unlocked) DrawString(font,new Vector2(w-44,h-22),Hero.Faction switch { Faction.Wu=>"吴",Faction.Shu=>"蜀",Faction.Wei=>"魏",_=>"群" },HorizontalAlignment.Left,-1,16,accent);
        if (_selected && _scan > 0 && _scan < 1)
            DrawRect(new Rect2(18,32+(h-98)*_scan,w-36,5),new Color(1,.9f,.4f,.7f));
        if (IsHovered() || HasFocus()) DrawRect(new Rect2(13,15,w-26,h-39),new Color(border,.09f));
    }
}
