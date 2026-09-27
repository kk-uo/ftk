//////////////////////////////////////////////////////////
// 文件：Scripts/ChoicePanel.cs
//
// 模块：Choice System
//
// 职责：
// 1. 承载统一选择界面与候选项生成相关代码。
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
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Choice System 的公开类：ChoicePanel。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class ChoicePanel : Control
{
    public event Action<ChoiceResult>? ChoiceCompleted;

    private bool _completed;
    private ChoiceRequest _request = new();
    // 阵营命运·改命 需要"替换其中一个选项、其余不变"，所以维护一份可变副本，
    // 而不是直接改 _request.Options（它的声明类型是 IReadOnlyList<ChoiceOption>）。
    private List<ChoiceOption> _options = new();

    /// <summary>
    /// Choice System 的公开入口：Configure。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Configure(ChoiceRequest request)
    {
        _request = request;
        _options = new List<ChoiceOption>(request.Options);
        _completed = false;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = new Color(0.07f, 0.08f, 0.10f),
            MouseFilter = MouseFilterEnum.Stop
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = GetPanelSize()
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        var panelMargin = IsCompactEquipmentChoice() ? 22 : 30;
        var verticalPanelMargin = IsCompactEquipmentChoice() ? 22 : 28;
        margin.AddThemeConstantOverride("margin_left", panelMargin);
        margin.AddThemeConstantOverride("margin_top", verticalPanelMargin);
        margin.AddThemeConstantOverride("margin_right", panelMargin);
        margin.AddThemeConstantOverride("margin_bottom", verticalPanelMargin);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", IsCompactEquipmentChoice() ? 12 : 18);
        margin.AddChild(root);

        var title = new Label
        {
            Text = _request.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        title.AddThemeColorOverride("font_color", new Color(0.98f, 0.91f, 0.72f));
        root.AddChild(title);

        if (!string.IsNullOrWhiteSpace(_request.Description))
        {
            var description = new Label
            {
                Text = _request.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Word
            };
            description.AddThemeFontSizeOverride("font_size", 22);
            description.AddThemeColorOverride("font_color", new Color(0.80f, 0.84f, 0.90f));
            root.AddChild(description);
        }

        if (_options.Count == 0)
        {
            AddEmptyState(root);
        }
        else
        {
            AddOptionGrid(root);
        }

        if (!string.IsNullOrWhiteSpace(_request.Footer))
        {
            var footer = new Label
            {
                Text = _request.Footer,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Word
            };
            footer.AddThemeFontSizeOverride("font_size", 18);
            footer.AddThemeColorOverride("font_color", new Color(0.66f, 0.70f, 0.78f));
            root.AddChild(footer);
        }

        if (_request.AllowCancel)
        {
            var cancel = new Button
            {
                Text = _request.CancelText,
                CustomMinimumSize = new Vector2(0, 54)
            };
            cancel.AddThemeFontSizeOverride("font_size", 22);
            cancel.Pressed += () => Complete(new ChoiceResult(null, true));
            root.AddChild(cancel);
        }
    }

    private void AddEmptyState(VBoxContainer root)
    {
        var empty = new Label
        {
            Text = _request.EmptyText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        empty.AddThemeFontSizeOverride("font_size", 24);
        empty.AddThemeColorOverride("font_color", new Color(0.78f, 0.80f, 0.84f));
        root.AddChild(empty);
    }

    private void AddOptionGrid(VBoxContainer root)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, GetGridMinHeight())
        };
        root.AddChild(scroll);

        var grid = new GridContainer
        {
            Columns = GetColumnCount(_options.Count),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        scroll.AddChild(grid);

        foreach (var option in _options)
        {
            grid.AddChild(CreateOptionCard(option));
        }
    }

    private Control CreateOptionCard(ChoiceOption option)
    {
        var isEquipment = option.Kind is ChoiceKind.Equipment or ChoiceKind.InventoryEquipment;
        var button = new Button
        {
            Text = isEquipment ? string.Empty : BuildOptionText(option),
            CustomMinimumSize = GetCardSize(option.Kind),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        button.AddThemeFontSizeOverride("font_size", option.Kind == ChoiceKind.Element ? 28 : 20);
        button.AddThemeColorOverride("font_color", GetKindColor(option.Kind));
        if (isEquipment)
        {
            AddEquipmentCardContent(button, option);
        }
        button.MouseEntered += () => ShowOptionTooltip(option, button);
        button.MouseExited += TooltipManager.Hide;
        // 刷新按钮点击不应该触发这里的"选择该选项"，两者是独立按钮，互不影响点击行为。
        button.Pressed += () => Complete(new ChoiceResult(option, false));

        if (_request.RerollOption == null)
        {
            return button;
        }

        // 阵营命运·改命：每个选项卡片旁追加一个独立的【刷新】按钮，点击不会触发上面的选择按钮。
        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        wrapper.AddThemeConstantOverride("separation", 4);
        wrapper.AddChild(button);

        var rerollButton = new Button
        {
            Text = Localization.Get("factionfate.reforge_refresh"),
            CustomMinimumSize = new Vector2(0, 32),
            // 阵营命运次数用完时，所有刷新按钮统一禁用；每次渲染都重新查询最新剩余次数。
            Disabled = FactionFateManager.RerollRemaining <= 0
        };
        rerollButton.AddThemeFontSizeOverride("font_size", 16);
        rerollButton.Pressed += () => OnRerollOptionPressed(option);
        wrapper.AddChild(rerollButton);

        return wrapper;
    }

    /// <summary>
    /// 装备选择卡保持原有卡片尺寸，只将原先的文字占位符替换为真实图标和两行文本。
    /// 图标由 IconLibrary 统一解析，避免选择界面直接拼接资源路径。
    /// </summary>
    private static void AddEquipmentCardContent(Button button, ChoiceOption option)
    {
        var content = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            OffsetLeft = 10,
            OffsetTop = 8,
            OffsetRight = -10,
            OffsetBottom = -8
        };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 8);
        button.AddChild(content);

        var icon = new TextureRect
        {
            Texture = IconLibrary.ResolveEquipmentIcon(option.Id),
            CustomMinimumSize = new Vector2(56, 56),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(icon);

        var texts = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        texts.AddThemeConstantOverride("separation", 2);
        content.AddChild(texts);

        var title = new Label
        {
            Text = option.Title,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", GetKindColor(option.Kind));
        texts.AddChild(title);

        var subtitle = new Label
        {
            Text = option.Subtitle,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Top,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        subtitle.AddThemeFontSizeOverride("font_size", 15);
        subtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.80f));
        texts.AddChild(subtitle);
    }

    /// <summary>
    /// 点击某个选项旁的【刷新】按钮：把 _options 里对应位置替换为新选项后整体 Rebuild()。
    /// 阵营命运的次数消耗已经在 FactionFateManager.TryRerollChoiceOption 内部处理，
    /// 这里不重复扣次数，只负责 UI 层的替换与失败提示。
    /// </summary>
    private void OnRerollOptionPressed(ChoiceOption option)
    {
        if (_request.RerollOption == null)
        {
            return;
        }

        var allIds = _options.Select(o => o.Id).ToList();
        var replacement = _request.RerollOption(option, allIds);
        if (replacement == null)
        {
            ShowRerollFailedHint();
            return;
        }

        var index = _options.FindIndex(o => o.Id == option.Id);
        if (index < 0)
        {
            return;
        }

        _options[index] = replacement;
        Rebuild();
    }

    /// <summary>
    /// 没有可替换的选项时的简单提示：不需要自动恢复，保持实现简单。
    /// </summary>
    private void ShowRerollFailedHint()
    {
        var hint = new Label
        {
            Text = Localization.Get("factionfate.no_reforge_candidate"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.SetAnchorsPreset(LayoutPreset.CenterTop);
        hint.OffsetTop = 20;
        hint.GrowHorizontal = GrowDirection.Both;
        hint.AddThemeFontSizeOverride("font_size", 18);
        hint.AddThemeColorOverride("font_color", new Color(0.95f, 0.45f, 0.40f));
        AddChild(hint);
    }

    private static void ShowOptionTooltip(ChoiceOption option, Control source)
    {
        if (option.Kind is ChoiceKind.Equipment or ChoiceKind.InventoryEquipment)
        {
            TooltipManager.ShowRich(
                new TooltipContent(
                    option.Description,
                    option.Title,
                    option.Subtitle,
                    GetEquipmentTitleColor(option.Payload),
                    IconLibrary.ResolveEquipmentIcon(option.Id),
                    Localization.Get("mainflow.choice_footer")),
                source);
            return;
        }

        TooltipManager.ShowRich(
            new TooltipContent(
                option.Description,
                option.Title,
                option.Subtitle,
                GetKindColor(option.Kind),
                null,
                string.Empty),
            source);
    }

    private static Color? GetEquipmentTitleColor(object? payload)
    {
        var rarity = payload switch
        {
            EquipmentDefinition definition => definition.Rarity,
            OwnedEquipment owned => owned.Definition.Rarity,
            _ => (EquipmentRarity?)null
        };

        return rarity switch
        {
            EquipmentRarity.Common => new Color(0.86f, 0.88f, 0.90f),
            EquipmentRarity.Rare => new Color(0.32f, 0.58f, 1.00f),
            EquipmentRarity.Epic => new Color(0.74f, 0.40f, 1.00f),
            EquipmentRarity.Legendary => new Color(1.00f, 0.78f, 0.22f),
            _ => null
        };
    }

    private static string BuildOptionText(ChoiceOption option)
    {
        var icon = string.IsNullOrWhiteSpace(option.IconText) ? string.Empty : $"【{option.IconText}】\n";
        var subtitle = string.IsNullOrWhiteSpace(option.Subtitle) ? string.Empty : $"\n{option.Subtitle}";
        if (option.Kind is ChoiceKind.Equipment or ChoiceKind.InventoryEquipment)
        {
            return $"{icon}{option.Title}{subtitle}";
        }

        var description = string.IsNullOrWhiteSpace(option.Description) ? string.Empty : $"\n\n{option.Description}";
        return $"{icon}{option.Title}{subtitle}{description}";
    }

    private float GetGridMinHeight()
    {
        if (IsCompactEquipmentChoice())
        {
            return _options.Count <= 3 ? 124 : 260;
        }

        return 260;
    }

    private bool IsCompactEquipmentChoice()
    {
        if (_options.Count == 0)
        {
            return false;
        }

        foreach (var option in _options)
        {
            if (option.Kind is not (ChoiceKind.Equipment or ChoiceKind.InventoryEquipment))
            {
                return false;
            }
        }

        return true;
    }

    private void Complete(ChoiceResult result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        ChoiceCompleted?.Invoke(result);
    }

    private static int GetColumnCount(int count)
        => count switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            _ => 3
        };

    private Vector2 GetPanelSize()
    {
        if (IsCompactEquipmentChoice())
        {
            return _options.Count switch
            {
                <= 1 => new Vector2(460, 360),
                2 => new Vector2(650, 360),
                3 => new Vector2(880, 370),
                4 => new Vector2(980, 500),
                _ => new Vector2(1020, 560)
            };
        }

        return _options.Count switch
        {
            <= 1 => new Vector2(720, 390),
            2 => new Vector2(840, 390),
            3 => new Vector2(1040, 410),
            4 => new Vector2(1220, 520),
            _ => new Vector2(1220, 660)
        };
    }

    private static Vector2 GetCardSize(ChoiceKind kind)
        => kind switch
        {
            ChoiceKind.Element => new Vector2(230, 170),
            ChoiceKind.Chip => new Vector2(260, 180),
            ChoiceKind.Equipment => new Vector2(236, 112),
            ChoiceKind.InventoryEquipment => new Vector2(236, 112),
            _ => new Vector2(260, 210)
        };

    private static Color GetKindColor(ChoiceKind kind)
        => kind switch
        {
            ChoiceKind.Skill => new Color(0.84f, 0.90f, 1.0f),
            ChoiceKind.Equipment => new Color(1.0f, 0.88f, 0.56f),
            ChoiceKind.InventoryEquipment => new Color(1.0f, 0.88f, 0.56f),
            ChoiceKind.Chip => new Color(0.80f, 0.94f, 1.0f),
            ChoiceKind.Element => new Color(1.0f, 0.92f, 0.78f),
            ChoiceKind.Buff => new Color(0.74f, 0.95f, 0.72f),
            ChoiceKind.Card => new Color(0.94f, 0.90f, 0.82f),
            _ => Colors.White
        };

    private static StyleBoxFlat CreatePanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.12f, 0.15f),
            BorderColor = new Color(0.32f, 0.38f, 0.50f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        style.SetBorderWidthAll(2);
        return style;
    }
}
