//////////////////////////////////////////////////////////
// 文件：Scripts/SettingsPanelController.cs
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
using System;

// Settings overlay panel. Added as a child of MainMenuController and shown/hidden on demand.
// Refreshes all label text whenever Localization.LanguageChanged fires.
/// <summary>
/// Core System 的公开类：SettingsPanelController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class SettingsPanelController : Control
{
    private Label? _titleLabel;
    private Label? _languageLabel;
    private OptionButton? _languageOption;
    private Label? _musicLabel;
    private Label? _sfxLabel;
    private Label? _developerModeLabel;
    private CheckButton? _developerModeCheckBox;
    private Label? _damagePreviewLabel;
    private CheckButton? _damagePreviewCheckBox;
    private Button? _backButton;

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        BuildLayout();
        RefreshText();
        Localization.LanguageChanged += RefreshText;
        DeveloperModeManager.ModeChanged += RefreshDevModeCheckbox;
    }

    /// <summary>
    /// Core System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= RefreshText;
        DeveloperModeManager.ModeChanged -= RefreshDevModeCheckbox;
    }

    private void RefreshDevModeCheckbox()
    {
        if (_developerModeCheckBox == null) return;
        _developerModeCheckBox.SetPressedNoSignal(DeveloperModeManager.IsDeveloperMode);
    }

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = new Theme
        {
            DefaultFont = GD.Load<Font>("res://Assets/Fonts/NotoSerifCJKsc-Black.otf")
        };

        // Semi-transparent dark backdrop
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.68f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        // 必须挂到父节点之后再设置布局，否则中心容器可能只占左上角最小尺寸。
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer
        {
            Name = "SettingsContent",
            CustomMinimumSize = new Vector2(620, 480)
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.025f, .045f, .055f, 1f),
            BorderColor = new Color(.16f, .55f, .61f, 1f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        margin.AddChild(vbox);
        panel.AddChild(margin);

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 30);
        vbox.AddChild(_titleLabel);

        // Language row
        var langRow = new HBoxContainer();
        langRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(langRow);

        _languageLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _languageLabel.AddThemeFontSizeOverride("font_size", 20);
        langRow.AddChild(_languageLabel);

        _languageOption = new OptionButton
        {
            CustomMinimumSize = new Vector2(200, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        _languageOption.AddThemeFontSizeOverride("font_size", 18);
        foreach (var code in Localization.SupportedLanguages)
            _languageOption.AddItem(Localization.Get($"settings.lang.{code}"));
        SetLanguageOptionIndex();
        _languageOption.ItemSelected += OnLanguageSelected;
        langRow.AddChild(_languageOption);

        // Music volume row (placeholder — no audio system yet)
        var musicRow = new HBoxContainer();
        musicRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(musicRow);

        _musicLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _musicLabel.AddThemeFontSizeOverride("font_size", 20);
        musicRow.AddChild(_musicLabel);

        var musicSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 80,
            CustomMinimumSize = new Vector2(200, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        musicRow.AddChild(musicSlider);

        // SFX volume row (placeholder)
        var sfxRow = new HBoxContainer();
        sfxRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(sfxRow);

        _sfxLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _sfxLabel.AddThemeFontSizeOverride("font_size", 20);
        sfxRow.AddChild(_sfxLabel);

        var sfxSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 80,
            CustomMinimumSize = new Vector2(200, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        sfxRow.AddChild(sfxSlider);

        // Developer mode row
        var devRow = new HBoxContainer();
        devRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(devRow);

        _developerModeLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _developerModeLabel.AddThemeFontSizeOverride("font_size", 20);
        devRow.AddChild(_developerModeLabel);

        _developerModeCheckBox = new CheckButton
        {
            ButtonPressed = DeveloperModeManager.IsDeveloperMode,
            CustomMinimumSize = new Vector2(80, 36),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        _developerModeCheckBox.AddThemeFontSizeOverride("font_size", 18);
        _developerModeCheckBox.Toggled += on => DeveloperModeManager.SetDeveloperMode(on);
        devRow.AddChild(_developerModeCheckBox);

        // Damage preview row
        var damagePreviewRow = new HBoxContainer();
        damagePreviewRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(damagePreviewRow);

        _damagePreviewLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _damagePreviewLabel.AddThemeFontSizeOverride("font_size", 20);
        damagePreviewRow.AddChild(_damagePreviewLabel);

        _damagePreviewCheckBox = new CheckButton
        {
            ButtonPressed = DamagePreviewSettings.IsEnabled,
            CustomMinimumSize = new Vector2(80, 36),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        _damagePreviewCheckBox.AddThemeFontSizeOverride("font_size", 18);
        _damagePreviewCheckBox.Toggled += on => DamagePreviewSettings.SetEnabled(on);
        damagePreviewRow.AddChild(_damagePreviewCheckBox);

        // Back button
        _backButton = new Button
        {
            CustomMinimumSize = new Vector2(200, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _backButton.AddThemeFontSizeOverride("font_size", 20);
        _backButton.Pressed += () => Visible = false;
        vbox.AddChild(_backButton);
    }

    private void RefreshText()
    {
        if (_titleLabel != null) _titleLabel.Text = Localization.Get("settings.title");
        if (_languageLabel != null) _languageLabel.Text = Localization.Get("settings.language");
        if (_musicLabel != null) _musicLabel.Text = Localization.Get("settings.music_volume");
        if (_sfxLabel != null) _sfxLabel.Text = Localization.Get("settings.sfx_volume");
        if (_backButton != null) _backButton.Text = Localization.Get("settings.back");
        if (_developerModeLabel != null) _developerModeLabel.Text = Localization.Get("settings.developer_mode");
        if (_damagePreviewLabel != null) _damagePreviewLabel.Text = Localization.Get("settings.damage_preview");

        // Refresh option button item labels to reflect new language
        if (_languageOption != null)
        {
            for (var i = 0; i < Localization.SupportedLanguages.Length; i++)
                _languageOption.SetItemText(i, Localization.Get($"settings.lang.{Localization.SupportedLanguages[i]}"));
            SetLanguageOptionIndex();
        }
    }

    private void SetLanguageOptionIndex()
    {
        if (_languageOption == null) return;
        for (var i = 0; i < Localization.SupportedLanguages.Length; i++)
        {
            if (Localization.SupportedLanguages[i] == Localization.CurrentLanguage)
            {
                _languageOption.Selected = i;
                return;
            }
        }
    }

    private void OnLanguageSelected(long index)
    {
        if (index >= 0 && index < Localization.SupportedLanguages.Length)
            Localization.SetLanguage(Localization.SupportedLanguages[(int)index]);
    }
}
