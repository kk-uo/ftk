//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/CyberSkillTriggerVisualEffect.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 播放技能大字的默认赛博扫描与边缘脉冲。
// 2. 复用单一节点和 Tween，避免连续触发时堆叠表现对象。
// 3. 为完整与简化表现模式提供不同强度。
//
// 不负责：
// × 决定技能触发条件。
// × 修改技能提示文字。
// × 操作战斗 HUD 布局。
//
// 主要依赖：
// ISkillTriggerVisualEffect
// Godot Tween
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

/// <summary>
/// 默认技能触发赛博特效。
///
/// 使用低饱和扫描线、短线段和双颜色边缘脉冲表达科技感，
/// 不依赖外部贴图，后续可通过注册其它接口实现替换或叠加。
/// </summary>
public partial class CyberSkillTriggerVisualEffect : Control, ISkillTriggerVisualEffect
{
    private static readonly Color Cyan = new(0.20f, 0.78f, 0.80f, 0.82f);
    private static readonly Color Magenta = new(0.72f, 0.20f, 0.43f, 0.72f);

    private readonly List<ColorRect> _edgeDashes = new();
    private ColorRect? _scanner;
    private ColorRect? _cyanPulse;
    private ColorRect? _magentaPulse;
    private Tween? _tween;

    /// <summary>
    /// 将默认赛博特效挂到技能提示 Overlay。
    /// </summary>
    public void Attach(Control host)
    {
        if (GetParent() != null)
        {
            return;
        }

        Name = "CyberSkillTriggerVisualEffect";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 0;
        Modulate = Colors.Transparent;
        host.AddChild(this);
        BuildVisuals();
    }

    /// <summary>
    /// 根据提示尺寸播放一次扫描与边缘脉冲。
    /// </summary>
    public void Play(SkillTriggerVisualEffectContext context)
    {
        if (_scanner == null || _cyanPulse == null || _magentaPulse == null)
        {
            return;
        }

        Stop();
        Visible = true;
        Modulate = Colors.White;

        var width = context.DisplaySize.X;
        var height = context.DisplaySize.Y;
        _scanner.Position = new Vector2(18, 8);
        _scanner.Size = new Vector2(context.Simplified ? 72 : 118, 2);
        _cyanPulse.Position = new Vector2(0, 18);
        _cyanPulse.Size = new Vector2(5, Mathf.Max(24, height - 36));
        _magentaPulse.Position = new Vector2(width - 3, height * 0.58f);
        _magentaPulse.Size = new Vector2(3, Mathf.Max(18, height * 0.24f));

        PositionDashes(width, height);

        var totalDuration = context.EnterDuration + context.HoldDuration + context.ExitDuration;
        _tween = CreateTween();
        _tween.SetParallel(true);
        _tween.TweenProperty(_scanner, "position:x", width - _scanner.Size.X - 20, totalDuration * 0.72f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _tween.TweenProperty(_scanner, "modulate:a", 0.0f, totalDuration * 0.82f)
            .SetDelay(context.EnterDuration * 0.45f)
            .SetEase(Tween.EaseType.In);
        _tween.TweenProperty(_cyanPulse, "modulate:a", context.Simplified ? 0.45f : 0.92f, context.EnterDuration);
        _tween.TweenProperty(_cyanPulse, "size:y", height * 0.42f, totalDuration * 0.55f)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(_magentaPulse, "modulate:a", context.Simplified ? 0.30f : 0.72f, context.EnterDuration * 1.2f);

        foreach (var dash in _edgeDashes)
        {
            dash.Modulate = new Color(1, 1, 1, 0);
            _tween.TweenProperty(dash, "modulate:a", context.Simplified ? 0.30f : 0.72f, context.EnterDuration)
                .SetDelay((dash.GetIndex() % 3) * 0.025f);
            _tween.TweenProperty(dash, "position:x", dash.Position.X + 12, totalDuration * 0.65f)
                .SetEase(Tween.EaseType.Out);
        }

        _tween.SetParallel(false);
        _tween.TweenProperty(this, "modulate:a", 0.0f, context.ExitDuration)
            .SetDelay(context.EnterDuration + context.HoldDuration)
            .SetEase(Tween.EaseType.In);
    }

    /// <summary>
    /// 停止当前赛博特效并隐藏复用节点。
    /// </summary>
    public void Stop()
    {
        _tween?.Kill();
        _tween = null;
        Modulate = Colors.Transparent;
        Visible = false;
    }

    /// <summary>
    /// 从技能提示 Overlay 移除默认赛博特效节点。
    /// </summary>
    public void Detach()
    {
        Stop();
        GetParent()?.RemoveChild(this);
    }

    private void BuildVisuals()
    {
        _scanner = CreateRect("Scanner", Cyan);
        _cyanPulse = CreateRect("CyanPulse", Cyan);
        _magentaPulse = CreateRect("MagentaPulse", Magenta);

        for (var i = 0; i < 8; i++)
        {
            var color = i % 2 == 0 ? Cyan : Magenta;
            var dash = CreateRect($"EdgeDash{i + 1}", color);
            dash.Size = new Vector2(i % 3 == 0 ? 34 : 18, 2);
            _edgeDashes.Add(dash);
        }
    }

    private ColorRect CreateRect(string name, Color color)
    {
        var rect = new ColorRect
        {
            Name = name,
            Color = color,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(rect);
        return rect;
    }

    private void PositionDashes(float width, float height)
    {
        for (var i = 0; i < _edgeDashes.Count; i++)
        {
            var top = i < _edgeDashes.Count / 2;
            var localIndex = i % (_edgeDashes.Count / 2);
            var x = 34 + localIndex * ((width - 104) / 3.0f);
            _edgeDashes[i].Position = new Vector2(x, top ? 5 : height - 7);
        }
    }
}
