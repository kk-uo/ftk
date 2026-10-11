using Godot;

/// <summary>背叛者独立武器层。只响应现有攻击表现，不改变任何伤害或攻击判定。</summary>
public partial class TraitorWeaponVisual : Node2D
{
    public const float RestAngle = 12f;
    // 坐标位于 256×192 的身体逻辑画布中；武器纹理的握柄位于 (50%, 65%)。
    public static readonly Vector2 GripPosition = new(205, 92);
    private Tween? _swing;
    public bool IsSwinging { get; private set; }

    public override void _Ready()
    {
        Position = GripPosition;
        RotationDegrees = RestAngle;
        ShowBehindParent = true;
        var texture = BossPortraitTextures.Load(BossPortraitTextures.Halberd);
        if (texture == null) return;
        AddChild(new Sprite2D
        {
            Texture = texture,
            Centered = false,
            Position = new Vector2(-32, -125),
            TextureFilter = TextureFilterEnum.Nearest
        });
    }

    public void PlayAttack()
    {
        _swing?.Kill();
        RotationDegrees = RestAngle;
        IsSwinging = true;
        _swing = CreateTween();
        // 先蓄力再向空出的持戟侧劈落，避免连同貂蝉/背景一起扭转。
        _swing.TweenProperty(this, "rotation_degrees", -18f, .09)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _swing.TweenProperty(this, "rotation_degrees", 105f, .12)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _swing.TweenProperty(this, "rotation_degrees", RestAngle, .22)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _swing.TweenCallback(Callable.From(() => { IsSwinging = false; _swing = null; }));
    }

    public override void _ExitTree()
    {
        _swing?.Kill();
        _swing = null;
        IsSwinging = false;
    }
}
