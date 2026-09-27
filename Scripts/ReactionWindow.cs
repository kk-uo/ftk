//////////////////////////////////////////////////////////
// 文件：Scripts/ReactionWindow.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using Godot;
using System.Threading.Tasks;

/// <summary>
/// Core System 的公开类：ReactionWindow。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed partial class ReactionWindow : Control
{
    private const double UrgentSeconds = 0.5;

    private TaskCompletionSource<ReactionOption?>? _completion;
    private ProgressBar? _progressBar;
    private Label? _timeLabel;
    private double _remainingSeconds;
    private double _timeoutSeconds;

    /// <summary>
    /// Core System 的公开入口：OpenAsync。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Task<ReactionOption?> OpenAsync(IReaction reaction)
    {
        _completion = new TaskCompletionSource<ReactionOption?>();
        _timeoutSeconds = GetTimeoutSeconds(reaction);
        _remainingSeconds = _timeoutSeconds;

        Name = "ReactionWindow";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        ZIndex = 80;
        Modulate = new Color(1, 1, 1, 0);

        AddChild(CreateCenterPanel(reaction));
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.White, 0.2);

        SetProcess(true);
        UpdateProgress();
        return _completion.Task;
    }

    /// <summary>
    /// Core System 的公开入口：_Process。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_completion == null || _completion.Task.IsCompleted)
        {
            return;
        }

        _remainingSeconds -= delta;
        UpdateProgress();
        if (_remainingSeconds <= 0)
        {
            Complete(null);
        }
    }

    /// <summary>
    /// Core System 的公开入口：CloseNow。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void CloseNow()
    {
        Complete(null);
    }

    private Control CreateCenterPanel(IReaction reaction)
    {
        var panel = new PanelContainer
        {
            Name = "ReactionReadBarPanel",
            CustomMinimumSize = new Vector2(760, 172),
            MouseFilter = MouseFilterEnum.Ignore
        };
        BattleReadBarLayout.PlaceAboveActionArea(panel, new Vector2(760, 172));
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.08f, 0.09f, 0.11f, 0.88f), new Color(0.95f, 0.72f, 0.26f)));

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 14);
        panel.AddChild(root);

        var titleLabel = new Label
        {
            Text = GetActivationText(reaction.Title),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 40);
        titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.36f));
        root.AddChild(titleLabel);

        var promptLabel = new Label
        {
            Text = GetPromptText(reaction),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        promptLabel.AddThemeFontSizeOverride("font_size", 26);
        promptLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.92f, 0.82f));
        root.AddChild(promptLabel);

        _progressBar = new ProgressBar
        {
            Name = "ReactionProgressBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(620, 24)
        };
        _progressBar.AddThemeStyleboxOverride("fill", CreateProgressFill(new Color(1.0f, 0.72f, 0.18f)));
        root.AddChild(_progressBar);

        _timeLabel = new Label
        {
            Text = Localization.GetFmt("reaction.countdown_seconds_fmt", _timeoutSeconds),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 20);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.88f, 0.96f));
        root.AddChild(_timeLabel);

        return panel;
    }

    private void UpdateProgress()
    {
        if (_progressBar != null)
        {
            _progressBar.Value = Mathf.Clamp(_timeoutSeconds > 0 ? _remainingSeconds / _timeoutSeconds * 100.0 : 0.0, 0.0, 100.0);
            var urgent = _remainingSeconds <= UrgentSeconds;
            var blink = urgent ? 0.68f + 0.32f * Mathf.Abs(Mathf.Sin((float)_remainingSeconds * 36.0f)) : 1.0f;
            var fillColor = urgent ? new Color(1.0f, 0.15f, 0.10f, blink) : new Color(1.0f, 0.72f, 0.18f);
            _progressBar.AddThemeStyleboxOverride("fill", CreateProgressFill(fillColor));
        }

        if (_timeLabel != null)
        {
            _timeLabel.Text = Localization.GetFmt(
                "reaction.countdown_seconds_fmt",
                Mathf.Max(0.0, _remainingSeconds));
        }
    }

    private void Complete(ReactionOption? option)
    {
        if (_completion == null || _completion.Task.IsCompleted)
        {
            return;
        }

        SetProcess(false);
        _completion.SetResult(option);
        QueueFree();
    }

    private static double GetTimeoutSeconds(IReaction reaction)
    {
        return reaction.Id == ReactionIds.Longdan ? 2.0 : 1.5;
    }

    private static string GetActivationText(string title)
    {
        var text = title.Trim().TrimEnd('?', '？');
        var activationPrefix = Localization.Get("reaction.activation_prefix");
        if (text.StartsWith(activationPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            text = text[activationPrefix.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(text)
            ? Localization.Get("reaction.activation_default")
            : Localization.GetFmt("reaction.activation_fmt", text);
    }

    private static string GetPromptText(IReaction reaction)
    {
        if (reaction.Id == ReactionIds.Longdan) return Localization.Get("reaction.longdan.prompt");
        if (reaction.Id == ReactionIds.JiGu) return Localization.Get("reaction.jigu.prompt");
        if (reaction is BreakArmyReaction bar)
        {
            return Localization.GetFmt("reaction.pojun.prompt_fmt", bar.CurrentMultiplier, bar.BaseDamage);
        }

        return Localization.Get("reaction.default.prompt");
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 16,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 16
        };
    }

    private static StyleBoxFlat CreateProgressFill(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };
    }
}
