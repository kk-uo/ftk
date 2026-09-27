//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/SkillTriggerToastQueue.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 顺序播放我方角色技能触发大字。
// 2. 合并同一结算链中同一技能的重复表现请求。
// 3. 保持浮层不参与战斗 HUD 与卡牌 Container 布局。
//
// 不负责：
// × 判断技能是否真正触发。
// × 修改战斗状态。
// × 从日志文本识别技能。
//
// 主要依赖：
// SkillTriggerPresentationRequest
// Godot Tween
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

/// <summary>
/// 我方角色技能大字表现的唯一播放队列。
///
/// 控件复用同一个面板，避免连续触发时创建多个重叠 Tween 或全屏节点。
/// </summary>
public partial class SkillTriggerToastQueue : Control
{
    private const float FullWidth = 560.0f;
    private const float SimplifiedWidth = 420.0f;
    private const float ToastHeight = 132.0f;
    private const float RightSafeMargin = 52.0f;
    private const float EnterOffset = 150.0f;

    private readonly Queue<SkillTriggerPresentationRequest> _pending = new();
    private readonly HashSet<string> _acceptedInChain = new();
    private readonly List<ISkillTriggerVisualEffect> _visualEffects = new();
    private PanelContainer? _toast;
    private Control? _effectHost;
    private Label? _protocolLabel;
    private Label? _skillName;
    private Label? _cyanEcho;
    private Label? _magentaEcho;
    private ColorRect? _energyLine;
    private Tween? _activeTween;
    private bool _playing;

    /// <summary>
    /// 当前表现等级。默认使用完整动画；关闭时请求会被忽略。
    /// </summary>
    public SkillTriggerPresentationMode Mode { get; set; } = SkillTriggerPresentationMode.Full;

    /// <summary>
    /// 自动化验证使用的已接收请求数量。
    /// </summary>
    public int AcceptedRequestCount { get; private set; }

    /// <summary>
    /// 最近一次真正开始播放的本地化技能名。
    /// </summary>
    public string LastPlayedSkillName { get; private set; } = string.Empty;

    /// <summary>
    /// 当前是否仍在播放或等待技能提示。
    /// </summary>
    public bool IsBusy => _playing || _pending.Count > 0;

    /// <summary>
    /// 当前已经挂载的附加视觉特效数量。
    /// </summary>
    public int VisualEffectCount => _visualEffects.Count;

    /// <summary>
    /// 建立唯一的可复用技能大字控件。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildToast();
        foreach (var effect in _visualEffects)
        {
            effect.Attach(_effectHost!);
        }

        RegisterVisualEffect(new CyberSkillTriggerVisualEffect());
        Resized += PlaceToastAtRest;
        PlaceToastAtRest();
    }

    /// <summary>
    /// 注册一个跟随技能大字生命周期播放的附加表现。
    ///
    /// 后续角色纹章、专属粒子或音效同步应通过该接口接入，
    /// 不应在技能效果代码内直接创建表现节点。
    /// </summary>
    public void RegisterVisualEffect(ISkillTriggerVisualEffect effect)
    {
        if (_visualEffects.Contains(effect))
        {
            return;
        }

        _visualEffects.Add(effect);
        if (_effectHost != null)
        {
            effect.Attach(_effectHost);
        }
    }

    /// <summary>
    /// 移除一个附加表现并停止其当前动画。
    /// </summary>
    public void UnregisterVisualEffect(ISkillTriggerVisualEffect effect)
    {
        if (!_visualEffects.Remove(effect))
        {
            return;
        }

        effect.Detach();
    }

    /// <summary>
    /// 将已经确认生效的技能加入顺序播放队列。
    ///
    /// 同一结算链中的同一技能只显示一次；不同结算链仍会再次显示。
    /// </summary>
    public void Enqueue(SkillTriggerPresentationRequest request)
    {
        if (Mode == SkillTriggerPresentationMode.Off || request.Side != BattleTeam.Player)
        {
            return;
        }

        var dedupKey = $"{request.ResolutionChainId}:{request.ActorId}:{request.SkillId}";
        if (!_acceptedInChain.Add(dedupKey))
        {
            return;
        }

        AcceptedRequestCount++;
        _pending.Enqueue(request);
        if (!_playing)
        {
            PlayNext();
        }
    }

    /// <summary>
    /// 停止当前动画并清理等待队列。
    ///
    /// 战斗重开、结束或场景退出时调用，防止上一场技能名残留。
    /// </summary>
    public void Clear()
    {
        _activeTween?.Kill();
        _activeTween = null;
        _pending.Clear();
        _acceptedInChain.Clear();
        _playing = false;
        foreach (var effect in _visualEffects)
        {
            effect.Stop();
        }

        if (_toast != null)
        {
            _toast.Visible = false;
            _toast.Modulate = Colors.Transparent;
        }
    }

    /// <summary>
    /// 场景销毁时释放 Tween 与队列状态。
    /// </summary>
    public override void _ExitTree()
    {
        Clear();
    }

    private void BuildToast()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.012f, 0.026f, 0.034f, 0.94f),
            BorderColor = new Color(0.16f, 0.66f, 0.69f, 0.84f),
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ContentMarginLeft = 32,
            ContentMarginRight = 28,
            ContentMarginTop = 13,
            ContentMarginBottom = 13,
            ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.55f),
            ShadowSize = 10,
            ShadowOffset = new Vector2(-5, 5)
        };

        _toast = new PanelContainer
        {
            Name = "SkillTriggerToast",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(FullWidth, ToastHeight),
            PivotOffset = new Vector2(FullWidth, ToastHeight * 0.5f),
            Visible = false,
            ZIndex = 1
        };
        _toast.AddThemeStyleboxOverride("panel", style);
        AddChild(_toast);

        _effectHost = new Control
        {
            Name = "VisualEffectOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 0
        };
        _effectHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _toast.AddChild(_effectHost);

        var content = new VBoxContainer
        {
            Name = "Content",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        content.AddThemeConstantOverride("separation", 3);
        _toast.AddChild(content);

        _protocolLabel = new Label
        {
            Name = "ProtocolLabel",
            Text = Localization.Get("presentation.skill_trigger_protocol"),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _protocolLabel.AddThemeFontSizeOverride("font_size", 14);
        _protocolLabel.AddThemeColorOverride("font_color", new Color(0.30f, 0.74f, 0.76f, 0.78f));
        content.AddChild(_protocolLabel);

        var nameLayer = new Control
        {
            Name = "SkillNameLayer",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 66)
        };
        content.AddChild(nameLayer);

        _cyanEcho = CreateNameLabel("CyanEcho", new Color(0.12f, 0.76f, 0.80f, 0.46f), 1);
        _magentaEcho = CreateNameLabel("MagentaEcho", new Color(0.74f, 0.16f, 0.42f, 0.42f), 2);
        _skillName = CreateNameLabel("SkillName", new Color(0.82f, 0.96f, 0.96f), 3);
        _skillName.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.05f, 0.065f, 1.0f));
        _skillName.AddThemeConstantOverride("outline_size", 7);
        nameLayer.AddChild(_cyanEcho);
        nameLayer.AddChild(_magentaEcho);
        nameLayer.AddChild(_skillName);

        _energyLine = new ColorRect
        {
            Name = "EnergyLine",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 3),
            Color = new Color(0.66f, 0.18f, 0.40f, 0.72f)
        };
        content.AddChild(_energyLine);
    }

    private static Label CreateNameLabel(string name, Color color, int zIndex)
    {
        var label = new Label
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ZIndex = zIndex
        };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 56);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void PlayNext()
    {
        if (_toast == null || _skillName == null || _pending.Count == 0)
        {
            _playing = false;
            return;
        }

        _playing = true;
        var request = _pending.Dequeue();
        LastPlayedSkillName = request.LocalizedSkillName;
        _skillName.Text = request.LocalizedSkillName;
        if (_cyanEcho != null)
        {
            _cyanEcho.Text = request.LocalizedSkillName;
        }

        if (_magentaEcho != null)
        {
            _magentaEcho.Text = request.LocalizedSkillName;
        }

        var simplified = Mode == SkillTriggerPresentationMode.Simplified;
        var width = simplified ? SimplifiedWidth : FullWidth;
        var enterDuration = simplified ? 0.10f : 0.16f;
        var holdDuration = simplified ? 0.28f : 0.42f;
        var exitDuration = simplified ? 0.22f : 0.32f;
        _toast.CustomMinimumSize = new Vector2(width, ToastHeight);
        _toast.Size = new Vector2(width, ToastHeight);
        _toast.PivotOffset = new Vector2(width, ToastHeight * 0.5f);
        PlaceToastAtRest();
        var restPosition = _toast.Position;

        _toast.Visible = true;
        _toast.Modulate = new Color(1, 1, 1, 0);
        _toast.Scale = simplified ? Vector2.One : new Vector2(0.85f, 0.85f);
        _toast.Position = restPosition + new Vector2(EnterOffset, 0);
        PrepareEchoes(simplified);

        var effectContext = new SkillTriggerVisualEffectContext(
            request,
            new Vector2(width, ToastHeight),
            simplified,
            enterDuration,
            holdDuration,
            exitDuration);
        foreach (var effect in _visualEffects)
        {
            effect.Play(effectContext);
        }

        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_toast, "position", restPosition, enterDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _activeTween.TweenProperty(_toast, "modulate:a", 1.0f, enterDuration)
            .SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(_toast, "scale", simplified ? Vector2.One : new Vector2(1.05f, 1.05f), enterDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        if (_cyanEcho != null && _magentaEcho != null)
        {
            _activeTween.TweenProperty(_cyanEcho, "position:x", 0.0f, enterDuration * 1.4f)
                .SetEase(Tween.EaseType.Out);
            _activeTween.TweenProperty(_magentaEcho, "position:x", 0.0f, enterDuration * 1.65f)
                .SetEase(Tween.EaseType.Out);
            _activeTween.TweenProperty(_cyanEcho, "modulate:a", 0.08f, enterDuration + holdDuration * 0.55f);
            _activeTween.TweenProperty(_magentaEcho, "modulate:a", 0.06f, enterDuration + holdDuration * 0.62f);
        }

        _activeTween.SetParallel(false);
        _activeTween.TweenProperty(_toast, "scale", Vector2.One, simplified ? 0.04f : 0.08f);
        _activeTween.TweenInterval(holdDuration);
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_toast, "position", restPosition + new Vector2(52, -14), exitDuration)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        _activeTween.TweenProperty(_toast, "modulate:a", 0.0f, exitDuration)
            .SetEase(Tween.EaseType.In);
        _activeTween.SetParallel(false);
        _activeTween.Finished += OnToastFinished;
    }

    private void PrepareEchoes(bool simplified)
    {
        if (_cyanEcho == null || _magentaEcho == null)
        {
            return;
        }

        _cyanEcho.Position = new Vector2(simplified ? -2 : -7, 0);
        _magentaEcho.Position = new Vector2(simplified ? 2 : 8, 0);
        _cyanEcho.Modulate = new Color(1, 1, 1, simplified ? 0.24f : 0.70f);
        _magentaEcho.Modulate = new Color(1, 1, 1, simplified ? 0.18f : 0.62f);
    }

    private void OnToastFinished()
    {
        if (_toast != null)
        {
            _toast.Visible = false;
        }

        _activeTween = null;
        PlayNext();
    }

    private void PlaceToastAtRest()
    {
        if (_toast == null)
        {
            return;
        }

        var width = _toast.CustomMinimumSize.X;
        _toast.Position = CalculateRestPosition(Size, width);
    }

    /// <summary>
    /// 根据当前视口计算右侧安全区位置。
    ///
    /// 该函数不读取固定分辨率，窗口模式、全屏和不同宽高比共用同一套计算。
    /// </summary>
    internal static Vector2 CalculateRestPosition(Vector2 viewportSize, float width)
    {
        var x = Mathf.Max(24.0f, viewportSize.X - width - RightSafeMargin);
        // 右侧中部略偏上，避开右下玩家状态和底部卡牌，也不占用顶部导航。
        var maximumY = Mathf.Max(120.0f, viewportSize.Y - ToastHeight - 260.0f);
        var y = Mathf.Clamp(viewportSize.Y * 0.48f - ToastHeight * 0.5f, 120.0f, maximumY);
        return new Vector2(x, y);
    }
}
