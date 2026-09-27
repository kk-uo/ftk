//////////////////////////////////////////////////////////
// 文件：Scripts/MainMenuController.cs
//
// 模块：Application Flow
//
// 职责：
// 1. 承载主流程切换、场景编排与全局入口相关代码。
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

/// <summary>
/// Application Flow 的公开类：MainMenuController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class MainMenuController : Control
{

    [Signal]
    public delegate void StartGameRequestedEventHandler();

    [Signal]
    public delegate void ContinueGameRequestedEventHandler();

    [Signal]
    public delegate void TutorialRequestedEventHandler();

    [Signal]
    public delegate void CodexRequestedEventHandler();

    [Signal]
    public delegate void DebugModeRequestedEventHandler();

    [Signal]
    public delegate void ExitGameRequestedEventHandler();

    private Label? _titleLabel;
    private Label? _subtitleLabel;
    private Button? _startButton;
    private Button? _continueButton;
    private bool _continueAvailable;
    private Button? _tutorialButton;
    private Label? _tutorialRecommendationLabel;
    private Button? _codexButton;
    private Button? _debugButton;
    private Button? _settingsButton;
    private Button? _feedbackButton;
    private Button? _exitButton;
    private SettingsPanelController? _settingsPanel;
    private Control? _feedbackPanel;
    private TextEdit? _feedbackInput;
    private Label? _feedbackStatusLabel;
    private Label? _feedbackTitleLabel;
    private Label? _feedbackDescriptionLabel;
    private Button? _feedbackCancelButton;
    private Button? _feedbackSubmitButton;
    private Label? _versionLabel;

    /// <summary>
    /// Application Flow 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        BuildLayout();
        RefreshText();
        Localization.LanguageChanged += RefreshText;
    }

    /// <summary>
    /// Application Flow 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= RefreshText;
    }

    private void BuildLayout()
    {
        var background = new MainMenuLivingBackground { Name = "ForgottenThreeKingdomsBackground" };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        AddChild(new MainMenuAtmosphere { Name = "RegionalAtmosphere", Background = background });
        AddChild(new MainMenuCrtFilter { Name = "BackgroundCrtFilter" });

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AnchorLeft = 0f,
            AnchorTop = 0f,
            OffsetLeft = 104f,
            OffsetTop = 150f,
            OffsetRight = 760f,
            OffsetBottom = 260f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 76);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.93f, 0.90f));
        _titleLabel.AddThemeColorOverride("font_shadow_color", new Color(0.78f, 0.05f, 0.17f, 0.9f));
        _titleLabel.AddThemeConstantOverride("shadow_offset_x", 4);
        _titleLabel.AddThemeConstantOverride("shadow_offset_y", 5);
        AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AnchorLeft = 0f,
            AnchorTop = 0f,
            OffsetLeft = 110f,
            OffsetTop = 260f,
            OffsetRight = 760f,
            OffsetBottom = 306f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 25);
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.56f, 0.86f, 0.91f));
        _subtitleLabel.AddThemeConstantOverride("outline_size", 4);
        _subtitleLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        AddChild(_subtitleLabel);

        var menuRoot = new VBoxContainer
        {
            AnchorLeft = 0f,
            AnchorTop = 0f,
            OffsetLeft = 110f,
            OffsetTop = 365f,
            OffsetRight = 540f,
            OffsetBottom = 1010f
        };
        menuRoot.AddThemeConstantOverride("separation", 12);
        AddChild(menuRoot);

        _startButton = CreateMenuButton(true);
        _startButton.Pressed += () => EmitSignal(SignalName.StartGameRequested);
        menuRoot.AddChild(_startButton);

        _continueButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Continue);
        _continueButton.Disabled = !_continueAvailable;
        _continueButton.Pressed += () => EmitSignal(SignalName.ContinueGameRequested);
        menuRoot.AddChild(_continueButton);

        var tutorialEntry = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        tutorialEntry.AddThemeConstantOverride("separation", 4);
        menuRoot.AddChild(tutorialEntry);

        _tutorialRecommendationLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _tutorialRecommendationLabel.AddThemeFontSizeOverride("font_size", 17);
        _tutorialRecommendationLabel.AddThemeColorOverride("font_color", new Color(1f, 0.84f, 0.28f));
        tutorialEntry.AddChild(_tutorialRecommendationLabel);

        _tutorialButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Tutorial);
        _tutorialButton.Pressed += () => EmitSignal(SignalName.TutorialRequested);
        tutorialEntry.AddChild(_tutorialButton);

        _codexButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Codex);
        _codexButton.Pressed += () => EmitSignal(SignalName.CodexRequested);
        menuRoot.AddChild(_codexButton);

        _debugButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Debug);
        _debugButton.Pressed += () => EmitSignal(SignalName.DebugModeRequested);
        menuRoot.AddChild(_debugButton);

        _settingsButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Settings);
        _settingsButton.Pressed += OpenSettings;
        menuRoot.AddChild(_settingsButton);

        _feedbackButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Feedback);
        _feedbackButton.Pressed += OpenFeedback;
        menuRoot.AddChild(_feedbackButton);

        _exitButton = CreateMenuButton(icon: MainMenuActionButton.Glyph.Exit);
        _exitButton.Pressed += () => EmitSignal(SignalName.ExitGameRequested);
        menuRoot.AddChild(_exitButton);

        _versionLabel = new Label
        {
            Text = "v0.4.0",
            HorizontalAlignment = HorizontalAlignment.Right,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -250f,
            OffsetTop = 42f,
            OffsetRight = -76f,
            OffsetBottom = 82f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _versionLabel.AddThemeFontSizeOverride("font_size", 22);
        _versionLabel.AddThemeColorOverride("font_color", new Color(0.24f, 0.90f, 0.90f));
        AddChild(_versionLabel);

        // Settings panel — added as a top-level sibling so it covers the whole viewport
        _settingsPanel = new SettingsPanelController { Visible = false };
        AddChild(_settingsPanel);

        _feedbackPanel = CreateFeedbackPanel();
        AddChild(_feedbackPanel);
    }

    private static StyleBoxFlat CreateRecommendationButtonStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthBottom = 2, BorderWidthLeft = 2,
            BorderWidthRight = 2, BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8
        };
    }

    private void RefreshText()
    {
        if (_titleLabel != null) _titleLabel.Text = Localization.Get("menu.title");
        if (_subtitleLabel != null) _subtitleLabel.Text = Localization.Get("menu.subtitle");
        if (_startButton != null) _startButton.Text = Localization.Get("menu.start");
        if (_continueButton != null) _continueButton.Text = Localization.Get("menu.continue");
        if (_tutorialButton != null) _tutorialButton.Text = Localization.Get("menu.tutorial");
        if (_tutorialRecommendationLabel != null) _tutorialRecommendationLabel.Text = Localization.Get("menu.tutorial.recommended");
        if (_codexButton != null) _codexButton.Text = Localization.Get("menu.codex");
        if (_debugButton != null) _debugButton.Text = Localization.Get("menu.debug");
        if (_settingsButton != null) _settingsButton.Text = Localization.Get("menu.settings");
        if (_feedbackButton != null) _feedbackButton.Text = Localization.Get("menu.feedback");
        if (_exitButton != null) _exitButton.Text = Localization.Get("menu.return_desktop");

        if (_feedbackPanel?.Visible == true)
        {
            RefreshFeedbackText();
        }
    }

    private void OpenSettings()
    {
        if (_settingsPanel != null)
            _settingsPanel.Visible = true;
    }

    /// <summary>
    /// Shows a first-launch cue beside the Tutorial entry. It is non-modal, so
    /// starting a run and every other menu option remain fully available.
    /// </summary>
    public void ShowTutorialRecommendation()
    {
        if (_tutorialButton == null || _tutorialRecommendationLabel == null)
        {
            return;
        }

        _tutorialRecommendationLabel.Visible = true;
        _tutorialButton.AddThemeStyleboxOverride("normal", CreateRecommendationButtonStyle(new Color(0.16f, 0.12f, 0.04f), new Color(1f, 0.78f, 0.20f)));
        _tutorialButton.AddThemeStyleboxOverride("hover", CreateRecommendationButtonStyle(new Color(0.23f, 0.17f, 0.05f), new Color(1f, 0.91f, 0.32f)));

        var pulse = CreateTween().SetLoops();
        pulse.TweenProperty(_tutorialButton, "modulate:a", 0.65f, 0.7f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(_tutorialButton, "modulate:a", 1f, 0.7f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    public void SetContinueAvailable(bool available)
    {
        // MainFlow 会在 AddChild / _Ready 之前配置菜单，必须保留状态供按钮创建时使用。
        _continueAvailable = available;
        if (_continueButton != null)
        {
            _continueButton.Disabled = !available;
        }
    }

    private Control CreateFeedbackPanel()
    {
        var overlay = new Control
        {
            Name = "FeedbackPanel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.72f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsPreset(LayoutPreset.FullRect);
        shade.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                overlay.Visible = false;
            }
        };
        overlay.AddChild(shade);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(760, 560),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", CreateFeedbackPanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 26);
        panel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        margin.AddChild(content);

        _feedbackTitleLabel = new Label { Name = "FeedbackTitle", HorizontalAlignment = HorizontalAlignment.Center };
        _feedbackTitleLabel.AddThemeFontSizeOverride("font_size", 32);
        content.AddChild(_feedbackTitleLabel);

        _feedbackDescriptionLabel = new Label { Name = "FeedbackDescription", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _feedbackDescriptionLabel.AddThemeFontSizeOverride("font_size", 18);
        _feedbackDescriptionLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.84f, 0.90f));
        content.AddChild(_feedbackDescriptionLabel);

        _feedbackInput = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 260),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PlaceholderText = Localization.Get("feedback.placeholder")
        };
        _feedbackInput.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(_feedbackInput);

        _feedbackStatusLabel = new Label
        {
            Name = "FeedbackStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _feedbackStatusLabel.AddThemeFontSizeOverride("font_size", 16);
        _feedbackStatusLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.80f, 0.46f));
        content.AddChild(_feedbackStatusLabel);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 12);
        content.AddChild(actions);

        _feedbackCancelButton = new Button { Name = "FeedbackCancel", CustomMinimumSize = new Vector2(160, 48) };
        _feedbackCancelButton.AddThemeFontSizeOverride("font_size", 20);
        _feedbackCancelButton.Pressed += () => overlay.Visible = false;
        actions.AddChild(_feedbackCancelButton);

        _feedbackSubmitButton = new Button { Name = "FeedbackSubmit", CustomMinimumSize = new Vector2(210, 48) };
        _feedbackSubmitButton.AddThemeFontSizeOverride("font_size", 20);
        _feedbackSubmitButton.Pressed += SubmitFeedback;
        actions.AddChild(_feedbackSubmitButton);

        return overlay;
    }

    private void OpenFeedback()
    {
        if (_feedbackPanel == null)
        {
            return;
        }

        _feedbackPanel.Visible = true;
        _feedbackInput?.Clear();
        RefreshFeedbackText();
        _feedbackInput?.GrabFocus();
    }

    private void RefreshFeedbackText()
    {
        if (_feedbackPanel == null)
        {
            return;
        }

        if (_feedbackTitleLabel != null) _feedbackTitleLabel.Text = Localization.Get("feedback.title");
        if (_feedbackDescriptionLabel != null) _feedbackDescriptionLabel.Text = Localization.Get("feedback.description");
        if (_feedbackCancelButton != null) _feedbackCancelButton.Text = Localization.Get("feedback.cancel");
        if (_feedbackSubmitButton != null) _feedbackSubmitButton.Text = Localization.Get("feedback.submit");
        if (_feedbackInput != null) _feedbackInput.PlaceholderText = Localization.Get("feedback.placeholder");
        if (_feedbackStatusLabel != null) _feedbackStatusLabel.Text = Localization.Get("feedback.local_only");
    }

    private void SubmitFeedback()
    {
        if (_feedbackInput == null || _feedbackStatusLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_feedbackInput.Text))
        {
            _feedbackStatusLabel.Text = Localization.Get("feedback.required");
            return;
        }

        if (!FeedbackReportService.TryCreate(_feedbackInput.Text, out var path, out var error))
        {
            _feedbackStatusLabel.Text = Localization.GetFmt("feedback.error_fmt", error);
            return;
        }

        DisplayServer.ClipboardSet(path);
        _feedbackStatusLabel.Text = Localization.GetFmt("feedback.saved_fmt", path);
    }

    private static StyleBoxFlat CreateFeedbackPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.065f, 0.09f, 0.98f),
            BorderColor = new Color(0.22f, 0.80f, 0.84f, 0.9f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
    }

    private static Button CreateMenuButton(bool primary = false, MainMenuActionButton.Glyph icon = MainMenuActionButton.Glyph.Start)
    {
        var button = new MainMenuActionButton
        {
            CustomMinimumSize = new Vector2(420, 58),
            Alignment = HorizontalAlignment.Left,
            IsPrimary = primary,
            ButtonGlyph = icon
        };
        button.AddThemeFontSizeOverride("font_size", 25);
        button.AddThemeConstantOverride("outline_size", 3);
        button.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        var normalFill = primary ? new Color(0.28f, 0.025f, 0.07f, 0.94f) : new Color(0.025f, 0.075f, 0.10f, 0.92f);
        var hoverFill = primary ? new Color(0.48f, 0.045f, 0.11f, 0.98f) : new Color(0.04f, 0.16f, 0.20f, 0.96f);
        button.AddThemeStyleboxOverride("normal", CreateMenuButtonStyle(normalFill));
        button.AddThemeStyleboxOverride("hover", CreateMenuButtonStyle(hoverFill));
        button.AddThemeStyleboxOverride("pressed", CreateMenuButtonStyle(new Color(0.01f, 0.02f, 0.03f, 0.98f)));
        button.AddThemeStyleboxOverride("disabled", CreateMenuButtonStyle(new Color(0.015f, 0.035f, 0.05f, 0.72f)));
        return button;
    }

    private static StyleBoxFlat CreateMenuButtonStyle(Color background)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            ContentMarginLeft = 72,
            ContentMarginRight = 22,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };
    }
}
