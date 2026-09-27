//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/DiceRollOverlay.cs
//
// 模块：Battle Presentation
//
// 职责：
// 1. 提供通用的 1D6 骰子结果动画表现层组件，供任何"掷骰子判定"的技能/机制复用。
// 2. 纯表现：不知道调用方是谁、判定结果代表什么业务含义，只负责把预先算好的
//    结果和文案播放成一段"出现→摇动→定格→退场"的动画。
// 3. 通过独立 CanvasLayer 固定在视口坐标系，避免调用方局部布局影响居中位置。
// 4. 保持与 ReactionWindow 一致的"Task 返回、调用方负责挂载与释放"约定。
//
// 不负责：
// × 计算骰子结果或判定成功/失败（调用方必须已经算好 predeterminedResult/success 再传入）。
// × 决定业务场景中的挂载时机（调用方仍负责 AddChild）。
// × QueueFree 自身：与 ReactionWindow 不同，本类不在动画结束时自毁，PlayAsync 返回后
//   由调用方自行 QueueFree，方便调用方控制节点生命周期（例如立即复用同一实例）。
//
// 主要依赖：
// Godot / C# Runtime
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Battle Presentation 的公开类：DiceRollOverlay。
///
/// 通用 1D6 骰子结果动画：出现→摇动（滚动数字，纯表现）→定格（真实结果+成功/失败文案）→
/// 退场，四段式。任何持有"1D6判定"机制的技能都可以复用这个组件——本类完全不知道具体
/// 游戏含义，全部文案/结果均通过参数传入，不含任何硬编码的技能名/角色名字符串。
/// </summary>
public sealed partial class DiceRollOverlay : CanvasLayer
{
    private bool _skipRequested;
    private Control? _screenRoot;
    private Label? _numberLabel;
    private Label? _titleLabel;
    private Label? _bodyLabel;
    private Panel? _dicePanel;
    private ColorRect? _dim;
    private Tween? _activeTween;

    /// <summary>
    /// Battle Presentation 的公开入口：PlayAsync。
    ///
    /// 播放一次完整的骰子动画。<paramref name="predeterminedResult"/>/<paramref name="success"/>
    /// 是调用方已经算好的真实结果，摇动阶段滚动的数字只是视觉效果，与真实结果无关。
    /// 调用方负责在返回后 QueueFree 本节点。
    /// </summary>
    public async Task PlayAsync(
        int predeterminedResult,
        bool success,
        string successTitle,
        string successBody,
        string failTitle,
        string failBody,
        bool fastMode = false,
        double resultHoldSeconds = 0)
    {
        await PlayAsync(
            predeterminedResult.ToString(),
            success,
            successTitle,
            successBody,
            failTitle,
            failBody,
            fastMode,
            resultHoldSeconds);
    }

    /// <summary>
    /// 展示预先结算好的骰面文本。除了单骰数字，也支持“2 / 4 / 6”这类多骰结果；
    /// 周泰等单骰调用仍复用上方的整型重载。
    /// </summary>
    public async Task PlayAsync(
        string predeterminedResult,
        bool success,
        string successTitle,
        string successBody,
        string failTitle,
        string failBody,
        bool fastMode = false,
        double resultHoldSeconds = 0)
    {
        BuildVisualTree();

        var appearTime = fastMode ? 0.08 : 0.2;
        var rollTime = fastMode ? 0.12 : 0.75;
        var settleTime = fastMode ? 0.08 : 0.35;
        var exitTime = fastMode ? 0.05 : 0.2;

        await PlayAppearPhase(appearTime);
        if (!_skipRequested)
        {
            await PlayRollPhase(rollTime);
        }

        await PlaySettlePhase(predeterminedResult, success, successTitle, successBody, failTitle, failBody, settleTime);
        // 章节天命骰需要给玩家足够时间阅读奖励；战斗内的周泰判定仍采用默认的 0 秒停留。
        if (!fastMode && resultHoldSeconds > 0)
        {
            await WaitSeconds(resultHoldSeconds);
        }
        await PlayExitPhase(exitTime);
    }

    /// <summary>
    /// Battle Presentation 的公开入口：SkipNow。
    ///
    /// 开发者/测试用：立即中断任何进行中的摇动/等待。内部的等待循环每次迭代都检查
    /// _skipRequested，因此调用后 PlayAsync 会在很短时间内（≤一个轮询步长）跳过剩余等待，
    /// 依次走完定格与退场阶段并返回——与 ReactionWindow.CloseNow 的"立即解除内部等待"
    /// 语义一致，只是这里通过轮询标志位而非 TaskCompletionSource 实现。
    /// </summary>
    public void SkipNow()
    {
        _skipRequested = true;
        _activeTween?.Kill();
    }

    private void BuildVisualTree()
    {
        Name = "DiceRollOverlay";
        Layer = 100;

        // 骰子可能由地图、战斗表现区或开发者面板发起。这些调用方的 Control 尺寸并不
        // 一致，因此不能让 0.5 锚点依赖调用方矩形。CanvasLayer 下的全屏根控件始终以
        // 当前视口为坐标基准，窗口尺寸变化后也会继续覆盖并居中于整个屏幕。
        _screenRoot = new Control
        {
            Name = "ScreenRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_screenRoot);
        _screenRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _dim = new ColorRect
        {
            Name = "ScreenDim",
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _screenRoot.AddChild(_dim);
        _dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _dicePanel = new Panel
        {
            Name = "DicePanel",
            CustomMinimumSize = new Vector2(320, 280),
            PivotOffset = new Vector2(160, 140),
            Scale = new Vector2(0.3f, 0.3f),
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _dicePanel.AddThemeStyleboxOverride("panel", CreateDiceStyle());
        _screenRoot.AddChild(_dicePanel);
        _dicePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _dicePanel.OffsetLeft = -160;
        _dicePanel.OffsetRight = 160;
        _dicePanel.OffsetTop = -140;
        _dicePanel.OffsetBottom = 140;

        var root = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.Alignment = BoxContainer.AlignmentMode.Center;
        root.AddThemeConstantOverride("separation", 8);
        _dicePanel.AddChild(root);

        _numberLabel = new Label
        {
            Text = "?",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _numberLabel.AddThemeFontSizeOverride("font_size", 88);
        _numberLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.96f, 1.0f));
        root.AddChild(_numberLabel);

        _titleLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.5f));
        root.AddChild(_titleLabel);

        _bodyLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(280, 0)
        };
        _bodyLabel.AddThemeFontSizeOverride("font_size", 18);
        _bodyLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.92f));
        root.AddChild(_bodyLabel);
    }

    private static StyleBoxFlat CreateDiceStyle()
    {
        // "赛博骰子"配色：深色底 + 青色描边，定格阶段再叠加成功(绿)/失败(红)色调。
        return new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.09f, 0.94f),
            BorderColor = new Color(0.15f, 0.85f, 0.95f),
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            ShadowColor = new Color(0.9f, 0.15f, 0.25f, 0.35f),
            ShadowSize = 10
        };
    }

    private async Task PlayAppearPhase(double duration)
    {
        if (_dim == null || _dicePanel == null)
        {
            return;
        }

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_dim, "color", new Color(0, 0, 0, 0.6f), duration);
        _activeTween.TweenProperty(_dicePanel, "modulate", Colors.White, duration);
        _activeTween.TweenProperty(_dicePanel, "scale", Vector2.One, duration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await WaitSeconds(duration);
    }

    private async Task PlayRollPhase(double totalSeconds)
    {
        if (_numberLabel == null || totalSeconds <= 0)
        {
            return;
        }

        // 摇动阶段滚动的数字纯粹是视觉效果：使用与 GameManager.EventRewardRandom 无关的
        // 局部 Random，绝不能与真实结果（predeterminedResult）混淆。
        var rng = new Random();
        var interval = 0.05;
        var elapsed = 0.0;
        while (elapsed < totalSeconds && !_skipRequested)
        {
            _numberLabel.Text = rng.Next(1, 7).ToString();
            var step = Math.Min(interval, totalSeconds - elapsed);
            await WaitSeconds(step);
            elapsed += step;
            interval = Math.Min(interval * 1.3, 0.25);
        }
    }

    private async Task PlaySettlePhase(
        string result,
        bool success,
        string successTitle,
        string successBody,
        string failTitle,
        string failBody,
        double duration)
    {
        if (_numberLabel == null || _dicePanel == null || _titleLabel == null || _bodyLabel == null)
        {
            return;
        }

        _numberLabel.Text = result;
        _titleLabel.Text = success ? successTitle : failTitle;
        _bodyLabel.Text = success ? successBody : failBody;
        _titleLabel.Visible = true;
        _bodyLabel.Visible = true;

        var tint = success ? new Color(0.55f, 1.0f, 0.7f) : new Color(1.0f, 0.45f, 0.45f);
        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_dicePanel, "modulate", tint, duration);
        _activeTween.TweenProperty(_dicePanel, "scale", new Vector2(1.1f, 1.1f), duration * 0.5)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await WaitSeconds(duration);
    }

    private async Task PlayExitPhase(double duration)
    {
        if (_dim == null || _dicePanel == null)
        {
            return;
        }

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_dicePanel, "scale", new Vector2(0.3f, 0.3f), duration);
        _activeTween.TweenProperty(_dicePanel, "modulate:a", 0.0f, duration);
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
