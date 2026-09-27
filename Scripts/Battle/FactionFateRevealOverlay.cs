//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/FactionFateRevealOverlay.cs
//
// 模块：Battle Presentation
//
// 职责：
// 1. 提供"阵营命运效果抽取演出"的表现层组件：出现→滚动阵营命运名称
//    （纯视觉，与真实结果无关）→定格显示预先算好的真实结果→等待玩家
//    点击任意位置→退场。
// 2. 纯表现：不知道调用方是谁，也不负责判定"本场生效的是哪个阵营命运"——
//    真实结果必须由调用方在动画开始前就已经确定好，本类只负责展示。
//
// 不负责：
// × 计算/随机本场生效的阵营命运（调用方必须已经算好 finalName/finalDescription
//   等参数再传入；本类的滚动动画只从传入的候选名称列表里随机挑选纯视觉文本，
//   与真实结果完全无关，也绝不使用会影响真实游戏状态的随机源）。
// × 挂载到场景树（由调用方决定挂在哪个容器下，例如 MainFlow 在角色选择
//   确认后新建的全屏 presentationRoot）。
// × QueueFree 自身：与 DiceRollOverlay 一致，PlayAsync 返回后由调用方自行 QueueFree。
//
// 主要依赖：
// Godot / C# Runtime
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Battle Presentation 的公开类：FactionFateRevealOverlay。
///
/// 全屏"老虎机"式阵营命运揭示动画：出现→滚动候选名称（纯表现）→定格（真实结果，
/// 含图标/名称/详细说明）→等待玩家点击任意位置→退场。与 ReactionWindow 一致，
/// 点击继续通过 TaskCompletionSource 实现；与 DiceRollOverlay 一致，摇动阶段用
/// 局部 Random，绝不与真实判定随机源混淆。
/// </summary>
public sealed partial class FactionFateRevealOverlay : Control
{
    private bool _skipRequested;
    private TaskCompletionSource<bool>? _dismissCompletion;

    /// <summary>
    /// 定格阶段结束、开始等待玩家点击时变为 true；测试/调试代码可以据此判断
    /// "现在点击才会真正生效"，而不必猜测动画各阶段耗时。
    /// </summary>
    public bool IsAwaitingDismissClick => _dismissCompletion != null && !_dismissCompletion.Task.IsCompleted;

    private ColorRect? _dim;
    private Panel? _panel;
    private Label? _headerLabel;
    private Label? _rollingNameLabel;
    private Panel? _badge;
    private Label? _badgeLabel;
    private Label? _factionLabel;
    private Label? _descriptionLabel;
    private Label? _continueHintLabel;
    private Tween? _activeTween;

    /// <summary>
    /// Battle Presentation 的公开入口：PlayAsync。
    ///
    /// <paramref name="rollCandidateNames"/> 是滚动阶段用来做视觉滚动的候选名称池
    /// （通常是全部阵营命运的名称），<paramref name="finalName"/>/<paramref name="finalDescription"/>
    /// 等是调用方已经算好的真实结果——滚动结束后必定定格在这些参数上，不受滚动过程影响。
    /// 调用方负责在返回后 QueueFree 本节点。
    /// </summary>
    public async Task PlayAsync(
        IReadOnlyList<string> rollCandidateNames,
        string finalName,
        string finalDescription,
        string factionLabelText,
        Color factionColor,
        string factionBadgeText,
        bool fastMode = false)
    {
        BuildVisualTree();

        var appearTime = fastMode ? 0.05 : 0.25;
        var rollTime = fastMode ? 0.1 : 1.1;
        var settleTime = fastMode ? 0.05 : 0.3;
        var exitTime = fastMode ? 0.05 : 0.2;

        await PlayAppearPhase(appearTime);
        if (!_skipRequested)
        {
            await PlayRollPhase(rollTime, rollCandidateNames);
        }

        PlaySettlePhase(finalName, finalDescription, factionLabelText, factionColor, factionBadgeText);
        await PlaySettleTween(settleTime);
        await WaitForClickToContinue();
        await PlayExitPhase(exitTime);
    }

    /// <summary>
    /// Battle Presentation 的公开入口：_GuiInput。
    ///
    /// 定格阶段结束后才开始监听点击（_dismissCompletion 在那之前为 null，点击无效果），
    /// 玩家点击画面任意位置即可关闭；由于根节点 MouseFilter=Stop 覆盖全屏，点击不会
    /// 穿透到背后的战斗 UI。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (_dismissCompletion == null || _dismissCompletion.Task.IsCompleted)
        {
            return;
        }

        if ((@event is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true }))
        {
            _dismissCompletion.TrySetResult(true);
            AcceptEvent();
        }
    }

    private void BuildVisualTree()
    {
        Name = "FactionFateRevealOverlay";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 96;

        _dim = new ColorRect
        {
            Name = "ScreenDim",
            Color = new Color(0, 0, 0, 0),
            // 必须是 Ignore：Stop 会在这个全屏 ColorRect 上直接吃掉点击事件（STOP 不会
            // 向父节点冒泡，只有事件真正落在“没有子节点消费”的区域时才会回退到 overlay
            // 自身的 _GuiInput），导致玩家点击屏幕任何位置都无法关闭老虎机——之前正是这里
            // 把点击吞掉了。
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_dim);
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 用锚点居中（与 DiceRollOverlay 已验证可用的写法一致）而不是 CenterContainer：
        // 四个锚点都设为 0.5，偏移量以面板半宽/半高对称收缩，面板中心永远精确落在父级
        // 矩形的几何中心，不依赖 Container 的布局时序或子节点最小尺寸计算。
        _panel = new Panel
        {
            Name = "RevealPanel",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -260,
            OffsetRight = 260,
            OffsetTop = -220,
            OffsetBottom = 220,
            CustomMinimumSize = new Vector2(520, 440),
            PivotOffset = new Vector2(260, 220),
            Scale = new Vector2(0.3f, 0.3f),
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        AddChild(_panel);

        var root = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(root);

        _headerLabel = new Label
        {
            Text = Localization.Get("factionfate.reveal.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 22);
        _headerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.82f, 0.9f));
        root.AddChild(_headerLabel);

        _badge = new Panel
        {
            CustomMinimumSize = new Vector2(72, 72),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            // Panel 默认 MouseFilter=Stop，同样会吞掉落在图标范围内的点击，必须显式 Ignore。
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(_badge);

        _badgeLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _badgeLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _badgeLabel.AddThemeFontSizeOverride("font_size", 30);
        _badgeLabel.AddThemeColorOverride("font_color", Colors.White);
        _badge.AddChild(_badgeLabel);

        _rollingNameLabel = new Label
        {
            Text = "?",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _rollingNameLabel.AddThemeFontSizeOverride("font_size", 34);
        _rollingNameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.6f));
        root.AddChild(_rollingNameLabel);

        _factionLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _factionLabel.AddThemeFontSizeOverride("font_size", 18);
        _factionLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.85f));
        root.AddChild(_factionLabel);

        _descriptionLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 0)
        };
        _descriptionLabel.AddThemeFontSizeOverride("font_size", 18);
        _descriptionLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.94f));
        root.AddChild(_descriptionLabel);

        _continueHintLabel = new Label
        {
            Text = Localization.Get("factionfate.reveal.continue_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _continueHintLabel.AddThemeFontSizeOverride("font_size", 15);
        _continueHintLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.74f));
        root.AddChild(_continueHintLabel);
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.07f, 0.1f, 0.96f),
            BorderColor = new Color(0.85f, 0.7f, 0.25f),
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            ShadowColor = new Color(0.9f, 0.75f, 0.2f, 0.3f),
            ShadowSize = 12,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            ContentMarginLeft = 20,
            ContentMarginRight = 20
        };
    }

    private async Task PlayAppearPhase(double duration)
    {
        if (_dim == null || _panel == null)
        {
            return;
        }

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_dim, "color", new Color(0, 0, 0, 0.72f), duration);
        _activeTween.TweenProperty(_panel, "modulate", Colors.White, duration);
        _activeTween.TweenProperty(_panel, "scale", Vector2.One, duration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await WaitSeconds(duration);
    }

    private async Task PlayRollPhase(double totalSeconds, IReadOnlyList<string> candidateNames)
    {
        if (_rollingNameLabel == null || totalSeconds <= 0 || candidateNames.Count == 0)
        {
            return;
        }

        // 滚动阶段展示的名称纯粹是视觉效果：使用与 GameManager.EventRewardRandom 无关的
        // 局部 Random，绝不能与真实结果（finalName）混淆，也不会在滚动过程中真正决定结果。
        var rng = new Random();
        var interval = 0.045;
        var elapsed = 0.0;
        while (elapsed < totalSeconds && !_skipRequested)
        {
            _rollingNameLabel.Text = candidateNames[rng.Next(candidateNames.Count)];
            var step = Math.Min(interval, totalSeconds - elapsed);
            await WaitSeconds(step);
            elapsed += step;
            interval = Math.Min(interval * 1.35, 0.32);
        }
    }

    private void PlaySettlePhase(
        string finalName,
        string finalDescription,
        string factionLabelText,
        Color factionColor,
        string factionBadgeText)
    {
        if (_rollingNameLabel == null || _factionLabel == null || _descriptionLabel == null
            || _badge == null || _badgeLabel == null)
        {
            return;
        }

        _rollingNameLabel.Text = finalName;
        _factionLabel.Text = factionLabelText;
        _descriptionLabel.Text = finalDescription;
        _factionLabel.Visible = true;
        _descriptionLabel.Visible = true;

        _badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = factionColor,
            CornerRadiusBottomLeft = 36,
            CornerRadiusBottomRight = 36,
            CornerRadiusTopLeft = 36,
            CornerRadiusTopRight = 36
        });
        _badgeLabel.Text = factionBadgeText;
    }

    private async Task PlaySettleTween(double duration)
    {
        if (_panel == null)
        {
            return;
        }

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_panel, "scale", new Vector2(1.05f, 1.05f), duration * 0.5)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await WaitSeconds(duration);

        if (_continueHintLabel != null)
        {
            _continueHintLabel.Visible = true;
        }
    }

    private Task WaitForClickToContinue()
    {
        _dismissCompletion = new TaskCompletionSource<bool>();
        return _skipRequested ? Task.CompletedTask : _dismissCompletion.Task;
    }

    private async Task PlayExitPhase(double duration)
    {
        if (_dim == null || _panel == null)
        {
            return;
        }

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_panel, "scale", new Vector2(0.3f, 0.3f), duration);
        _activeTween.TweenProperty(_panel, "modulate:a", 0.0f, duration);
        _activeTween.TweenProperty(_dim, "color", new Color(0, 0, 0, 0), duration);
        await WaitSeconds(duration);
    }

    private async Task WaitSeconds(double seconds)
    {
        if (seconds <= 0 || _skipRequested)
        {
            return;
        }

        var elapsed = 0.0;
        const double step = 0.05;
        while (elapsed < seconds)
        {
            if (_skipRequested)
            {
                return;
            }

            var chunk = Math.Min(step, seconds - elapsed);
            await ToSignal(GetTree().CreateTimer(chunk), SceneTreeTimer.SignalName.Timeout);
            elapsed += chunk;
        }
    }
}
