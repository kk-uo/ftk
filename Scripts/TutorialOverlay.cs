using Godot;
using System;

public partial class TutorialOverlay : CanvasLayer
{
    public event Action? ReturnToMenuRequested;

    /// <summary>
    /// 整合式教程专用：战斗教学全部步骤完成（即将要弹的是"教程完成"popup）
    /// 时触发，代替原本弹窗里的"返回主菜单"/"重新体验教程"——整合流程里
    /// 训练假人打赢之后应该直接进入战后奖励，而不是弹窗回菜单。
    /// </summary>
    public event Action? IntegratedFlowBattleWon;

    private Label? _stepLabel;
    private Label? _titleLabel;
    private RichTextLabel? _descLabel;
    private Control? _objSection;
    private Label? _objLabel;
    private Control? _tipsSection;
    private Label? _tipsLabel;
    private Button? _nextButton;
    private Button? _prevDevButton;
    private Button? _nextDevButton;

    private PanelContainer? _panel;
    private Button? _collapseToggleButton;
    private bool _panelCollapsed;
    private Control? _completionPopup;
    private Label? _completionTitleLabel;
    private Label? _completionDescLabel;
    private Button? _returnMenuButton;
    private Button? _retryButton;

    private TutorialArrowNode? _arrowNode;
    private TutorialController? _controller;
    private BattleManager? _battleManager;
    internal Rect2 HighlightRect { get; private set; }
    internal double PulseTime { get; private set; }

    private const float GuidePanelWidth = 560f;
    private const float GuidePanelHeight = 420f;
    private const float ScreenMargin = 32f;

    public override void _Ready()
    {
        Layer = 128;

        BuildPanel();
        BuildCompletionPopup();

        // Arrow/glow node added last so it always draws on top of the side
        // panel: highlighted targets (battle log button, player/enemy status
        // panels, etc.) can sit underneath the panel's screen region, and if
        // the arrow were added first (drawn first / behind), the panel would
        // visually swallow the highlight instead of showing it.
        _arrowNode = new TutorialArrowNode(this);
        _arrowNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _arrowNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_arrowNode);

        // The overlay is always parented directly under the real BattleManager
        // instance (see MainFlow.ShowTutorial: `battle.AddChild(overlay)`), so
        // the controller can drive that same live battle without any separate
        // simulation of its own.
        if (GetParent() is BattleManager battleManager)
        {
            _battleManager = battleManager;
            _controller = new TutorialController(battleManager);
        }

        TutorialManager.StepChanged += OnStepChanged;
        TutorialManager.TutorialCompleted += OnTutorialCompleted;

        // Tutorial may already be active if StartTutorial() was called before we entered the tree
        if (TutorialManager.IsActive && TutorialManager.CurrentStep != null)
            OnStepChanged(TutorialManager.CurrentStep);
    }

    public override void _ExitTree()
    {
        TutorialManager.StepChanged -= OnStepChanged;
        TutorialManager.TutorialCompleted -= OnTutorialCompleted;
        _controller?.Shutdown();
    }

    public override void _Process(double delta)
    {
        PulseTime += delta;
        RefreshHighlightRect();
        UpdateGuidePanelPlacement();
        _arrowNode?.QueueRedraw();
        _controller?.Tick();
    }

    // ─── Panel ────────────────────────────────────────────────────────────────

    private void BuildPanel()
    {
        // 战斗教程说明固定在右上角。高亮和箭头独立绘制，因此教学步骤切换时
        // 不会再为了避让目标而把说明卡改放到左侧。
        var panel = new PanelContainer
        {
            Name = "TutorialPanel",
            AnchorLeft = 0f, AnchorRight = 0f,
            AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -ScreenMargin - GuidePanelWidth, OffsetRight = -ScreenMargin,
            OffsetTop = ScreenMargin, OffsetBottom = ScreenMargin + GuidePanelHeight,
            ZIndex = 1
        };
        _panel = panel;
        AddChild(panel);

        _collapseToggleButton = new Button
        {
            Text = "▲",
            AnchorLeft = 0f, AnchorRight = 0f,
            AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -ScreenMargin - GuidePanelWidth / 2f - 40f,
            OffsetRight = -ScreenMargin - GuidePanelWidth / 2f + 40f,
            OffsetTop = ScreenMargin + GuidePanelHeight + 4f,
            OffsetBottom = ScreenMargin + GuidePanelHeight + 56f,
            ZIndex = 2
        };
        _collapseToggleButton.AddThemeFontSizeOverride("font_size", 18);
        _collapseToggleButton.Pressed += ToggleCollapsed;
        AddChild(_collapseToggleButton);

        // Two independent vertical sections so the action button(s) are never
        // pushed off-screen by long description text: a scrollable content
        // area on top, and a fixed (non-scrolling) button area pinned to the
        // bottom of the panel.
        var outerVbox = new VBoxContainer();
        outerVbox.AddThemeConstantOverride("separation", 0);
        panel.AddChild(outerVbox);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill
        };
        outerVbox.AddChild(scroll);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 18);
        contentMargin.AddThemeConstantOverride("margin_bottom", 12);
        scroll.AddChild(contentMargin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        contentMargin.AddChild(vbox);

        // Header
        var header = new Label
        {
            Text = Localization.Get("tutorial.header"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 27);
        header.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(header);

        _stepLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _stepLabel.AddThemeFontSizeOverride("font_size", 17);
        _stepLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        vbox.AddChild(_stepLabel);

        vbox.AddChild(new HSeparator());

        // Title
        _titleLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 25);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(_titleLabel);

        // Description (the outer ScrollContainer above already handles
        // scrolling for the whole panel, so this no longer needs its own).
        _descLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(260, 0)
        };
        _descLabel.AddThemeFontSizeOverride("normal_font_size", 21);
        vbox.AddChild(_descLabel);

        // Objective section (shown only when text is non-empty)
        _objSection = new VBoxContainer();
        vbox.AddChild(_objSection);
        _objSection.AddChild(new HSeparator());

        var objHeader = new Label { Text = Localization.Get("tutorial.objective") };
        objHeader.AddThemeFontSizeOverride("font_size", 17);
        objHeader.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.4f));
        _objSection.AddChild(objHeader);

        _objLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _objLabel.AddThemeFontSizeOverride("font_size", 21);
        _objLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.97f, 0.92f));
        _objSection.AddChild(_objLabel);

        // Tips section (shown only when text is non-empty)
        _tipsSection = new VBoxContainer();
        vbox.AddChild(_tipsSection);
        _tipsSection.AddChild(new HSeparator());

        var tipsHeader = new Label { Text = Localization.Get("tutorial.tips") };
        tipsHeader.AddThemeFontSizeOverride("font_size", 17);
        tipsHeader.AddThemeColorOverride("font_color", new Color(0.6f, 0.82f, 1f));
        _tipsSection.AddChild(tipsHeader);

        _tipsLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _tipsLabel.AddThemeFontSizeOverride("font_size", 20);
        _tipsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.97f));
        _tipsSection.AddChild(_tipsLabel);

        // ─── Fixed bottom action area (outside the scroll, always visible) ───
        var bottomMargin = new MarginContainer();
        bottomMargin.AddThemeConstantOverride("margin_left", 16);
        bottomMargin.AddThemeConstantOverride("margin_right", 16);
        bottomMargin.AddThemeConstantOverride("margin_top", 10);
        bottomMargin.AddThemeConstantOverride("margin_bottom", 16);
        outerVbox.AddChild(bottomMargin);

        var bottomVbox = new VBoxContainer();
        bottomVbox.AddThemeConstantOverride("separation", 8);
        bottomMargin.AddChild(bottomVbox);

        bottomVbox.AddChild(new HSeparator());

        // Next / Complete button (hidden in Action mode): large, gold, with
        // hover/pressed feedback so it's impossible to miss.
        _nextButton = CreateHighlightedActionButton();
        _nextButton.Pressed += () => TutorialManager.AdvanceToNext();
        bottomVbox.AddChild(_nextButton);

        // Dev-mode navigation row
        if (DeveloperModeManager.IsDeveloperMode)
        {
            var devRow = new HBoxContainer();
            bottomVbox.AddChild(devRow);

            _prevDevButton = new Button { Text = "◀", CustomMinimumSize = new Vector2(52, 38) };
            _prevDevButton.Pressed += () => TutorialManager.PreviousStep();
            devRow.AddChild(_prevDevButton);

            var restartBtn = new Button
            {
                Text = "↺",
                SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill,
                CustomMinimumSize = new Vector2(0, 38)
            };
            restartBtn.Pressed += () => TutorialManager.Restart();
            devRow.AddChild(restartBtn);

            _nextDevButton = new Button { Text = "▶", CustomMinimumSize = new Vector2(52, 38) };
            _nextDevButton.Pressed += () => TutorialManager.AdvanceToNext();
            devRow.AddChild(_nextDevButton);
        }
    }

    /// <summary>
    /// Centered modal shown only when the tutorial actually completes: a
    /// full-screen dim backdrop (blocks clicks to whatever is behind it) plus
    /// a panel in the middle of the screen with the completion message and
    /// the 返回主菜单/重新体验教程 buttons — instead of tucking them into the
    /// thin side panel, this puts the final call-to-action front and center.
    /// </summary>
    private void BuildCompletionPopup()
    {
        _completionPopup = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _completionPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_completionPopup);

        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _completionPopup.AddChild(backdrop);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _completionPopup.AddChild(centerContainer);

        var popupPanel = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
        popupPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f),
            BorderColor = new Color(1f, 0.85f, 0.3f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 28, ContentMarginRight = 28,
            ContentMarginTop = 26, ContentMarginBottom = 26
        });
        centerContainer.AddChild(popupPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        popupPanel.AddChild(vbox);

        _completionTitleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _completionTitleLabel.AddThemeFontSizeOverride("font_size", 30);
        _completionTitleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(_completionTitleLabel);

        _completionDescLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(380, 0)
        };
        _completionDescLabel.AddThemeFontSizeOverride("font_size", 20);
        _completionDescLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.92f));
        vbox.AddChild(_completionDescLabel);

        _returnMenuButton = CreateHighlightedActionButton(Localization.Get("tutorial.btn.return_menu"));
        _returnMenuButton.Pressed += () => ReturnToMenuRequested?.Invoke();
        vbox.AddChild(_returnMenuButton);

        _retryButton = CreateHighlightedActionButton(Localization.Get("tutorial.btn.retry"));
        _retryButton.Pressed += OnRetryPressed;
        vbox.AddChild(_retryButton);
    }

    /// <summary>
    /// Large gold action button used for "继续"/"返回主菜单"/"重新体验教程":
    /// distinct normal/hover/pressed/focus styleboxes give clear hover
    /// highlighting and click feedback, and a fixed height + full-width flag
    /// keeps every button in the panel the same size.
    /// </summary>
    private static Button CreateHighlightedActionButton(string? text = null)
    {
        var button = new Button
        {
            Text = text ?? string.Empty,
            SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 56)
        };
        button.AddThemeFontSizeOverride("font_size", 22);

        var normal = new StyleBoxFlat { BgColor = new Color(0.88f, 0.66f, 0.10f) };
        var hover = new StyleBoxFlat { BgColor = new Color(1f, 0.80f, 0.22f) };
        var pressed = new StyleBoxFlat { BgColor = new Color(0.66f, 0.48f, 0.05f) };
        var focus = new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.80f, 0.22f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderColor = new Color(1f, 1f, 1f, 0.85f)
        };
        foreach (var box in new[] { normal, hover, pressed, focus })
        {
            box.CornerRadiusTopLeft = 8;
            box.CornerRadiusTopRight = 8;
            box.CornerRadiusBottomLeft = 8;
            box.CornerRadiusBottomRight = 8;
            box.ContentMarginTop = 4;
            box.ContentMarginBottom = 4;
        }

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeColorOverride("font_color", new Color(0.16f, 0.09f, 0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.08f, 0.04f, 0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.96f, 0.85f));
        button.AddThemeColorOverride("font_focus_color", new Color(0.08f, 0.04f, 0f));

        return button;
    }

    // ─── Highlight ────────────────────────────────────────────────────────────

    private void RefreshHighlightRect()
    {
        var step = TutorialManager.CurrentStep;
        if (step?.Highlight == null || step.Highlight.Type == HighlightTargetType.None)
        {
            HighlightRect = default;
            return;
        }

        HighlightRect = FindHighlightRect(step.Highlight);
    }

    private void UpdateGuidePanelPlacement()
    {
        if (_panel == null)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        var width = Mathf.Min(GuidePanelWidth, viewportSize.X - ScreenMargin * 2f);
        var height = Mathf.Min(GuidePanelHeight, viewportSize.Y - ScreenMargin * 2f);
        var fixedRightPlacement = new Rect2(viewportSize.X - width - ScreenMargin, ScreenMargin, width, height);

        _panel.OffsetLeft = fixedRightPlacement.Position.X;
        _panel.OffsetRight = fixedRightPlacement.End.X;
        _panel.OffsetTop = fixedRightPlacement.Position.Y;
        _panel.OffsetBottom = fixedRightPlacement.End.Y;

        if (_collapseToggleButton != null)
        {
            var toggleTop = fixedRightPlacement.End.Y + 4f;
            _collapseToggleButton.OffsetLeft = fixedRightPlacement.GetCenter().X - 40f;
            _collapseToggleButton.OffsetRight = fixedRightPlacement.GetCenter().X + 40f;
            _collapseToggleButton.OffsetTop = toggleTop;
            _collapseToggleButton.OffsetBottom = toggleTop + 52f;
        }
    }

    private Rect2 FindHighlightRect(TutorialHighlightTarget target)
    {
        switch (target.Type)
        {
            case HighlightTargetType.CardOfType:
            {
                // "action_slots" is never actually populated anywhere in the
                // codebase, so route through the live BattleManager instead of
                // relying on that group (see BattleManager.Tutorial.cs).
                if (!Enum.TryParse<CardType>(target.TargetId, out var cardType))
                    return default;
                return _battleManager?.GetTutorialActionCardRect(cardType) ?? default;
            }
            case HighlightTargetType.Player:
            {
                foreach (var node in GetTree().GetNodesInGroup("tutorial_player_area"))
                    if (node is Control c) return c.GetGlobalRect();
                return default;
            }
            case HighlightTargetType.Enemy:
            {
                foreach (var node in GetTree().GetNodesInGroup("tutorial_enemy_area"))
                    if (node is Control c) return c.GetGlobalRect();
                return default;
            }
            case HighlightTargetType.UI:
            {
                foreach (var node in GetTree().GetNodesInGroup("tutorial_battle_log"))
                    if (node is Control c) return c.GetGlobalRect();
                return default;
            }
            default:
                return default;
        }
    }

    // ─── Step change ──────────────────────────────────────────────────────────

    private void OnStepChanged(TutorialStep? step)
    {
        // Completion is handled separately by OnTutorialCompleted (which fires
        // just before this null step-change) so it can show its own popup
        // instead of the overlay disappearing outright.
        if (step == null) return;

        Visible = true;
        if (_panel != null) _panel.Visible = !_panelCollapsed;
        if (_completionPopup != null) _completionPopup.Visible = false;

        if (_stepLabel != null)
            _stepLabel.Text = string.Format(
                Localization.Get("tutorial.step_fmt"),
                TutorialManager.CurrentStepIndex + 1,
                TutorialManager.TotalSteps);

        if (_titleLabel != null)
            _titleLabel.Text = step.DisplayTitle;

        if (_descLabel != null)
            _descLabel.Text = step.DisplayDescription;

        var obj = GetObjectiveText(step);
        if (_objSection != null) _objSection.Visible = !string.IsNullOrWhiteSpace(obj);
        if (_objLabel != null) _objLabel.Text = obj;

        var tips = step.DisplayTips;
        if (_tipsSection != null) _tipsSection.Visible = !string.IsNullOrWhiteSpace(tips);
        if (_tipsLabel != null) _tipsLabel.Text = tips;

        if (_nextButton != null)
        {
            _nextButton.Visible = step.AdvanceMode == TutorialAdvanceMode.Button;
            _nextButton.Text = step.IsLastStep
                ? Localization.Get("tutorial.btn.complete")
                : Localization.Get("tutorial.btn.continue");
        }

        if (_prevDevButton != null)
            _prevDevButton.Disabled = !TutorialManager.CanGoPrevious;
    }

    // NeedFee 是所有"费用不足"课程共用的插入步骤，静态文案只能说"请先使用费"；
    // 这里在费用不足具体是为了打出哪张牌时（TutorialManager.PendingFeeLessonTarget
    // 由 TutorialController 在跳转前写入），补一句"然后再使用XX"的具体提示，
    // 其余步骤仍然显示各自数据库里配置的目标文案，不受影响。
    private static string GetObjectiveText(TutorialStep step)
    {
        if (step.Id == TutorialDatabase.StepIds.NeedFee
            && Enum.TryParse<CardType>(TutorialManager.PendingFeeLessonTarget, out var targetCardType))
        {
            return Localization.GetFmt("tutorial.step.need_fee.obj_target_fmt", BattleRules.GetCardName(targetCardType));
        }

        return step.DisplayObjective;
    }

    private void OnTutorialCompleted()
    {
        if (IntegratedTutorialFlow.IsActive)
        {
            IntegratedFlowBattleWon?.Invoke();
            return;
        }

        Visible = true;

        // Hide the side instructional panel and show a centered popup instead
        // — the completion call-to-action should be impossible to miss, not
        // tucked into the thin 360px side panel used throughout the lesson.
        if (_panel != null) _panel.Visible = false;
        if (_completionPopup != null) _completionPopup.Visible = true;
        if (_completionTitleLabel != null) _completionTitleLabel.Text = Localization.Get("tutorial.complete.title");
        if (_completionDescLabel != null) _completionDescLabel.Text = Localization.Get("tutorial.complete.desc");
    }

    private void OnRetryPressed()
    {
        _battleManager?.RestartTutorialBattle();
        TutorialManager.ResetForNewRun();
        TutorialManager.StartTutorial();
    }

    /// <summary>
    /// 折叠/展开说明面板：玩家随时可以把挡住右侧敌方状态条/自己状态卡的
    /// 说明面板收起去看一眼，再点回来继续——不影响教程进度，也不影响
    /// 高亮框/箭头的显示（那部分是 _arrowNode 独立绘制的，不受此开关影响）。
    /// </summary>
    private void ToggleCollapsed()
    {
        _panelCollapsed = !_panelCollapsed;
        if (_panel != null) _panel.Visible = !_panelCollapsed && TutorialManager.CurrentStep != null;
        if (_collapseToggleButton != null) _collapseToggleButton.Text = _panelCollapsed ? "▼" : "▲";
    }
}

// ─── Arrow + glow drawing node ────────────────────────────────────────────────

public partial class TutorialArrowNode : Control
{
    private readonly TutorialOverlay _overlay;
    private const float PulseSpeed = 2.5f;
    private const float BounceAmp = 8f;
    private const float BounceSpeed = 2.2f;

    public TutorialArrowNode(TutorialOverlay overlay)
    {
        _overlay = overlay;
    }

    public override void _Draw()
    {
        var rect = _overlay.HighlightRect;
        if (rect.Size == Vector2.Zero) return;

        var t = (float)_overlay.PulseTime;
        var pulse = 0.5f + 0.5f * MathF.Sin(t * PulseSpeed);

        // Pulsing gold fill
        DrawRect(rect, new Color(1f, 0.87f, 0.2f, 0.1f + 0.15f * pulse));
        // Pulsing border
        DrawRect(rect, new Color(1f, 0.87f, 0.2f, 0.5f + 0.38f * pulse), false, 2f + pulse * 1.5f);

        // Bouncing downward-pointing triangle above target
        var bounce = MathF.Sin(t * BounceSpeed) * BounceAmp;
        var cx = rect.Position.X + rect.Size.X * 0.5f;
        var tipY = rect.Position.Y - 14f + bounce;

        DrawColoredPolygon(
            new[] { new Vector2(cx, tipY), new Vector2(cx - 14f, tipY - 28f), new Vector2(cx + 14f, tipY - 28f) },
            new Color(1f, 0.87f, 0.15f, 0.95f));
    }
}
