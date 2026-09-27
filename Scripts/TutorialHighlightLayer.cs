//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialHighlightLayer.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// 整合式教程需要在角色选择/商店/背包/地图/事件这些"非战斗"真实屏幕上叠加
// 高亮+说明文字，视觉上和 TutorialOverlay.cs（战斗内教程专用）想要的效果
// 一致（半透明遮罩+脉冲高亮框+跳动箭头+居中说明面板+继续按钮），但
// TutorialOverlay.cs 本身和 BattleManager/TutorialController 强耦合，不适合
// 直接搬到别的屏幕用。这里把纯展示逻辑单独提炼成一个新的、独立的组件，
// 不改动 TutorialOverlay.cs 本身——避免影响已经工作正常的战斗内教程渲染。
//
// 使用方式：
// 宿主 Control（CharacterSelectController/ShopController/InventoryController/
// MapController等）在教学模式下自己 AddChild 一个 TutorialHighlightLayer，
// 每一步调用 ShowStep(...) 指定高亮目标 Control + 标题/说明/目标/提示文本 +
// 是否显示"继续"按钮；宿主自行监听真实交互（点击角色/购买/拖拽装备等）来
// 决定何时调用 NextRequested 之外的自定义推进逻辑。
//
// 不负责：
// × 教程步骤的宏观顺序（由 IntegratedTutorialFlow 负责）。
// × 判断"这一步是否已经完成"（由各宿主自己监听真实信号判断）。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class TutorialHighlightLayer : CanvasLayer
{
    /// <summary>点击"继续"按钮时触发（仅当 ShowStep 的 showContinueButton=true 时按钮可见）。</summary>
    public event Action? ContinueRequested;

    private PanelContainer? _panel;
    private Button? _collapseToggleButton;
    private bool _panelCollapsed;
    private Label? _titleLabel;
    private RichTextLabel? _descLabel;
    private Control? _objSection;
    private Label? _objLabel;
    private Button? _continueButton;
    private TutorialHighlightArrowNode? _arrowNode;

    private Control? _highlightTarget;
    internal Rect2 HighlightRect { get; private set; }
    internal double PulseTime { get; private set; }

    private const float GuidePanelWidth = 620f;
    private const float GuidePanelHeight = 360f;
    private const float ScreenMargin = 32f;

    public override void _Ready()
    {
        Layer = 130;
        BuildPanel();
        BuildCollapseToggle();

        _arrowNode = new TutorialHighlightArrowNode(this);
        _arrowNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _arrowNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_arrowNode);
    }

    /// <summary>
    /// 折叠/展开固定在右上角的说明面板；收起后高亮框/箭头仍然可见，
    /// 不影响继续推进教程。
    /// </summary>
    private void BuildCollapseToggle()
    {
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
        _collapseToggleButton.Pressed += () =>
        {
            _panelCollapsed = !_panelCollapsed;
            if (_panel != null) _panel.Visible = !_panelCollapsed;
            _collapseToggleButton.Text = _panelCollapsed ? "▼" : "▲";
        };
        AddChild(_collapseToggleButton);
    }

    public override void _Process(double delta)
    {
        PulseTime += delta;
        HighlightRect = _highlightTarget != null && IsInstanceValid(_highlightTarget)
            ? _highlightTarget.GetGlobalRect()
            : default;
        UpdateGuidePanelPlacement();
        _arrowNode?.QueueRedraw();
    }

    /// <summary>设置本步需要高亮的真实控件（传 null 表示本步不高亮任何控件）。</summary>
    public void SetHighlightTarget(Control? target)
    {
        _highlightTarget = target;
    }

    public void ShowStep(string title, string description, string objective, string tips, bool showContinueButton)
    {
        Visible = true;
        if (_panel != null) _panel.Visible = !_panelCollapsed;
        if (_titleLabel != null) _titleLabel.Text = title;
        if (_descLabel != null) _descLabel.Text = description;

        if (_objSection != null) _objSection.Visible = !string.IsNullOrWhiteSpace(objective);
        if (_objLabel != null) _objLabel.Text = objective;

        if (_continueButton != null)
        {
            _continueButton.Visible = showContinueButton;
        }

        _ = tips; // 目前非战斗教学步骤都不需要 Tips 段落，保留参数位以便未来复用。
    }

    public void HideAll()
    {
        Visible = false;
        SetHighlightTarget(null);
    }

    private void BuildPanel()
    {
        // 非战斗教程与战斗教程保持一致：说明卡固定在右上角，目标高亮仍由
        // 独立箭头指向，因此说明卡不会在步骤之间跳到左侧。
        var panel = new PanelContainer
        {
            Name = "TutorialHighlightPanel",
            AnchorLeft = 0f, AnchorRight = 0f,
            AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -ScreenMargin - GuidePanelWidth, OffsetRight = -ScreenMargin,
            OffsetTop = ScreenMargin, OffsetBottom = ScreenMargin + GuidePanelHeight,
            ZIndex = 1
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.10f, 0.97f)
        });
        _panel = panel;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var header = new Label
        {
            Text = Localization.Get("tutorial.header"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 25);
        header.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(header);
        vbox.AddChild(new HSeparator());

        _titleLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _titleLabel.AddThemeFontSizeOverride("font_size", 25);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(_titleLabel);

        _descLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(280, 0)
        };
        _descLabel.AddThemeFontSizeOverride("normal_font_size", 21);
        vbox.AddChild(_descLabel);

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

        vbox.AddChild(new HSeparator());
        _continueButton = new Button
        {
            Text = Localization.Get("tutorial.btn.continue"),
            CustomMinimumSize = new Vector2(0, 52)
        };
        _continueButton.AddThemeFontSizeOverride("font_size", 22);
        _continueButton.Pressed += () => ContinueRequested?.Invoke();
        vbox.AddChild(_continueButton);
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
            _collapseToggleButton.AnchorLeft = 0f;
            _collapseToggleButton.AnchorRight = 0f;
            _collapseToggleButton.AnchorTop = 0f;
            _collapseToggleButton.AnchorBottom = 0f;
            _collapseToggleButton.OffsetLeft = fixedRightPlacement.GetCenter().X - 40f;
            _collapseToggleButton.OffsetRight = fixedRightPlacement.GetCenter().X + 40f;
            _collapseToggleButton.OffsetTop = toggleTop;
            _collapseToggleButton.OffsetBottom = toggleTop + 52f;
        }
    }
}

// ─── Arrow + glow drawing node（与 TutorialArrowNode 视觉一致，独立实现避免耦合 TutorialOverlay）────
public partial class TutorialHighlightArrowNode : Control
{
    private readonly TutorialHighlightLayer _layer;
    private const float PulseSpeed = 2.5f;
    private const float BounceAmp = 8f;
    private const float BounceSpeed = 2.2f;

    public TutorialHighlightArrowNode(TutorialHighlightLayer layer)
    {
        _layer = layer;
    }

    public override void _Draw()
    {
        var rect = _layer.HighlightRect;
        if (rect.Size == Vector2.Zero) return;

        var t = (float)_layer.PulseTime;
        var pulse = 0.5f + 0.5f * MathF.Sin(t * PulseSpeed);

        DrawRect(rect, new Color(1f, 0.87f, 0.2f, 0.1f + 0.15f * pulse));
        DrawRect(rect, new Color(1f, 0.87f, 0.2f, 0.5f + 0.38f * pulse), false, 2f + pulse * 1.5f);

        var bounce = MathF.Sin(t * BounceSpeed) * BounceAmp;
        var cx = rect.Position.X + rect.Size.X * 0.5f;
        var tipY = rect.Position.Y - 14f + bounce;

        DrawColoredPolygon(
            new[] { new Vector2(cx, tipY), new Vector2(cx - 14f, tipY - 28f), new Vector2(cx + 14f, tipY - 28f) },
            new Color(1f, 0.87f, 0.15f, 0.95f));
    }
}
