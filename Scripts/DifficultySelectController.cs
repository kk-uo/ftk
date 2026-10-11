//////////////////////////////////////////////////////////
// 文件：Scripts/DifficultySelectController.cs
//
// 模块：Difficulty System UI
//
// 职责：在角色确认后展示无难度与六档累加难度、永久解锁状态。
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

public partial class DifficultySelectController : Control
{
    [Signal]
    public delegate void DifficultyChosenEventHandler(int difficulty);

    [Signal]
    public delegate void ReturnRequestedEventHandler();

    private readonly Dictionary<DifficultyLevel, Button> _buttons = new();
    private DifficultyLevel _selected = DifficultyLevel.None;
    private RichTextLabel? _details;
    private Button? _confirmButton;

    public override void _Ready()
    {
        BuildLayout();
        SelectDifficulty(DifficultyLevel.None);
    }

    private void BuildLayout()
    {
        var background = new ColorRect { Color = new Color(0.012f, 0.022f, 0.035f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 72);
        margin.AddThemeConstantOverride("margin_right", 72);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        var title = new Label
        {
            Text = Localization.Get("difficulty.select.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", Localization.CurrentLanguage == "en_US" ? 48 : 54);
        title.AddThemeColorOverride("font_color", new Color("ef3b6f"));
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = Localization.Get("difficulty.select.subtitle")
                + (DeveloperModeManager.IsDeveloperMode
                    ? "\n" + Localization.Get("difficulty.select.developer_hint")
                    : string.Empty),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, DeveloperModeManager.IsDeveloperMode ? 58 : 30)
        };
        subtitle.AddThemeFontSizeOverride("font_size", Localization.CurrentLanguage == "en_US" ? 20 : 22);
        subtitle.AddThemeColorOverride("font_color", DeveloperModeManager.IsDeveloperMode
            ? new Color("58d68d")
            : new Color("58c5d8"));
        root.AddChild(subtitle);

        // 七档难度改为响应式网格。单行排列在英文界面中会把锁定说明挤出卡片和屏幕；
        // 2560/2048宽度使用4列，其它较窄窗口自动降为3列或2列。
        var cards = new GridContainer
        {
            Columns = 4,
            CustomMinimumSize = new Vector2(0, 420),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        cards.AddThemeConstantOverride("h_separation", 18);
        cards.AddThemeConstantOverride("v_separation", 18);
        root.AddChild(cards);
        cards.Resized += () =>
        {
            cards.Columns = cards.Size.X >= 1680 ? 4 : cards.Size.X >= 1120 ? 3 : 2;
        };

        for (var value = 0; value <= (int)DifficultyManager.MaximumDifficulty; value++)
        {
            var difficulty = (DifficultyLevel)value;
            var unlocked = DifficultyManager.IsUnlocked(difficulty);
            var button = new Button
            {
                Text = GetCardText(difficulty, unlocked),
                Disabled = !unlocked,
                CustomMinimumSize = new Vector2(0, 190),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                ToggleMode = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            button.AddThemeFontSizeOverride("font_size", Localization.CurrentLanguage == "en_US" ? 21 : 24);
            ApplyCardStyle(button, unlocked);
            if (unlocked)
            {
                button.Pressed += () => SelectDifficulty(difficulty);
            }
            _buttons[difficulty] = button;
            cards.AddChild(button);
        }

        var detailPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 245),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detailPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        root.AddChild(detailPanel);

        var detailMargin = new MarginContainer();
        detailMargin.AddThemeConstantOverride("margin_left", 32);
        detailMargin.AddThemeConstantOverride("margin_right", 32);
        detailMargin.AddThemeConstantOverride("margin_top", 24);
        detailMargin.AddThemeConstantOverride("margin_bottom", 24);
        detailPanel.AddChild(detailMargin);

        _details = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _details.AddThemeFontSizeOverride("normal_font_size", Localization.CurrentLanguage == "en_US" ? 20 : 22);
        detailMargin.AddChild(_details);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 24);
        root.AddChild(actions);

        var back = new Button
        {
            Text = Localization.Get("difficulty.select.back"),
            CustomMinimumSize = new Vector2(280, 64)
        };
        back.AddThemeFontSizeOverride("font_size", 22);
        back.Pressed += () => EmitSignal(SignalName.ReturnRequested);
        actions.AddChild(back);

        _confirmButton = new Button
        {
            Text = Localization.Get("difficulty.select.confirm"),
            CustomMinimumSize = new Vector2(420, 72)
        };
        _confirmButton.AddThemeFontSizeOverride("font_size", 26);
        _confirmButton.Pressed += ConfirmSelection;
        actions.AddChild(_confirmButton);
    }

    private void SelectDifficulty(DifficultyLevel difficulty)
    {
        if (!DifficultyManager.IsUnlocked(difficulty)) return;
        _selected = difficulty;
        foreach (var pair in _buttons)
        {
            pair.Value.SetPressedNoSignal(pair.Key == difficulty);
        }
        if (_details != null)
        {
            _details.Text = Localization.Get($"difficulty.{(int)difficulty}.details");
        }
    }

    private void ConfirmSelection()
    {
        if (!DifficultyManager.IsUnlocked(_selected)) return;
        EmitSignal(SignalName.DifficultyChosen, (int)_selected);
    }

    private static string GetCardText(DifficultyLevel difficulty, bool unlocked)
    {
        var title = Localization.Get($"difficulty.{(int)difficulty}.name");
        if (unlocked)
        {
            return title + "\n\n" + Localization.Get($"difficulty.{(int)difficulty}.summary");
        }

        return title + "\n\n" + Localization.Get("difficulty.locked") + "\n"
            + Localization.GetFmt("difficulty.unlock_requirement", (int)difficulty - 1);
    }

    private static void ApplyCardStyle(Button button, bool unlocked)
    {
        var normal = CreatePanelStyle();
        normal.BorderColor = unlocked ? new Color("24566c") : new Color("303843");
        button.AddThemeStyleboxOverride("normal", normal);
        var hover = CreatePanelStyle();
        hover.BorderColor = new Color("39bfd6");
        button.AddThemeStyleboxOverride("hover", hover);
        var pressed = CreatePanelStyle();
        pressed.BorderColor = new Color("ef3b6f");
        pressed.BgColor = new Color(0.10f, 0.025f, 0.055f, 0.98f);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.35f, 0.39f, 0.44f));
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.018f, 0.035f, 0.05f, 0.98f),
            BorderColor = new Color("24566c"),
            ShadowColor = new Color(0, 0, 0, 0.65f),
            ShadowSize = 10
        };
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(10);
        return style;
    }
}
