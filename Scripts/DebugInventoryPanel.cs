//////////////////////////////////////////////////////////
// 文件：Scripts/DebugInventoryPanel.cs
//
// 模块：Inventory System
//
// 职责：
// 1. 承载背包、装备槽位与装备实例管理相关代码。
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

// 调试装备面板：仅在 Debug Mode（GameManager.DebugMapEnabled）下通过背包界面右上角的【调试】按钮打开。
// 从 EquipmentDatabase 动态读取全部装备，支持重复添加、清空背包操作，不影响已装备槽位及任何游戏数值。
/// <summary>
/// Inventory System 的公开类：DebugInventoryPanel。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class DebugInventoryPanel : Control
{
    // 每次向背包添加装备或清空背包后，调用该回调通知 InventoryController 刷新视图。
    public Action? OnInventoryChanged;

    private VBoxContainer? _listContainer;

    /// <summary>
    /// Inventory System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Localization.LanguageChanged += OnLanguageChanged;
        BuildLayout();
    }

    /// <summary>
    /// Inventory System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        BuildLayout();
    }

    private void BuildLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        // 半透明深色遮罩层
        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(680, 820)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        root.AddChild(BuildHeader());
        root.AddChild(BuildScrollArea());
        UIResourceDatabase.ApplyScrollBars(this);
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);

        var title = new Label { Text = Localization.Get("inventory.debug.title") };
        title.AddThemeFontSizeOverride("font_size", 30);
        header.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        var clearButton = new Button
        {
            Text = Localization.Get("inventory.debug.clear"),
            CustomMinimumSize = new Vector2(110, 44)
        };
        clearButton.AddThemeFontSizeOverride("font_size", 20);
        clearButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.55f, 0.45f));
        clearButton.Pressed += OnClearBackpackPressed;
        header.AddChild(clearButton);

        var closeButton = new Button
        {
            Text = Localization.Get("inventory.debug.close"),
            CustomMinimumSize = new Vector2(80, 44)
        };
        closeButton.AddThemeFontSizeOverride("font_size", 20);
        closeButton.Pressed += () => QueueFree();
        header.AddChild(closeButton);

        return header;
    }

    private Control BuildScrollArea()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        _listContainer = new VBoxContainer();
        _listContainer.AddThemeConstantOverride("separation", 6);
        _listContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_listContainer);

        PopulateList();

        return scroll;
    }

    private void PopulateList()
    {
        if (_listContainer == null)
        {
            return;
        }

        foreach (var child in _listContainer.GetChildren())
        {
            _listContainer.RemoveChild(child);
            child.QueueFree();
        }

        var sorted = GetSortedEquipments();
        var currentRarity = (EquipmentRarity)(-1);

        foreach (var definition in sorted)
        {
            // 在稀有度切换时插入分隔标题行
            if (definition.Rarity != currentRarity)
            {
                currentRarity = definition.Rarity;
                var section = new Label
                {
                    Text = $"── {GetRarityDisplayName(definition.Rarity)} ──",
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                section.AddThemeFontSizeOverride("font_size", 18);
                section.AddThemeColorOverride("font_color", GetRarityColor(definition.Rarity));
                _listContainer.AddChild(section);
            }

            _listContainer.AddChild(BuildEquipmentRow(definition));
        }
    }

    private Control BuildEquipmentRow(EquipmentDefinition definition)
    {
        var wrapper = new VBoxContainer();
        wrapper.AddThemeConstantOverride("separation", 4);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var nameLabel = new Label
        {
            Text = Localization.GetName(definition),
            CustomMinimumSize = new Vector2(110, 0)
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        nameLabel.AddThemeColorOverride("font_color", GetRarityColor(definition.Rarity));
        row.AddChild(nameLabel);

        // 稀有度
        var rarityLabel = new Label
        {
            Text = GetRarityDisplayName(definition.Rarity),
            CustomMinimumSize = new Vector2(56, 0)
        };
        rarityLabel.AddThemeFontSizeOverride("font_size", 18);
        rarityLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.74f, 0.82f));
        row.AddChild(rarityLabel);

        // 装备类型
        var typesLabel = new Label
        {
            Text = GetTypesDisplayText(definition.Types),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        typesLabel.AddThemeFontSizeOverride("font_size", 18);
        typesLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.64f, 0.72f));
        row.AddChild(typesLabel);

        // 【添加】按钮
        var addButton = new Button
        {
            Text = Localization.Get("inventory.debug.add"),
            CustomMinimumSize = new Vector2(72, 38)
        };
        addButton.AddThemeFontSizeOverride("font_size", 18);
        var capturedId = definition.Id;
        addButton.Pressed += () => OnAddPressed(capturedId);
        row.AddChild(addButton);

        wrapper.AddChild(row);

        var detailParts = new List<string>
        {
            Localization.GetFmt("inventory.debug.description_fmt", Localization.GetDescription(definition))
        };
        detailParts.Add(Localization.GetFmt("inventory.debug.acquisition_fmt", GetAcquisitionDisplayText(definition.AcquisitionMethod)));
        if (definition.UnlockConditions.Count > 0)
        {
            detailParts.Add(Localization.GetFmt("inventory.debug.source_fmt", Localization.GetEquipmentUnlockConditions(definition, " / ")));
        }

        var detailLabel = new Label
        {
            Text = string.Join("    ", detailParts),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        detailLabel.AddThemeFontSizeOverride("font_size", 16);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.70f, 0.78f));
        wrapper.AddChild(detailLabel);

        return wrapper;
    }

    private void OnAddPressed(string equipmentId)
    {
        InventoryManager.AddToInventory(equipmentId, EquipmentGainSource.Developer);
        OnInventoryChanged?.Invoke();
    }

    private void OnClearBackpackPressed()
    {
        InventoryManager.ClearBackpack();
        OnInventoryChanged?.Invoke();
    }

    // 按稀有度从高到低排列，同稀有度内按名称升序排列。
    // 新增装备无需修改此方法，动态读取 EquipmentDatabase 即可自动出现。
    private static List<EquipmentDefinition> GetSortedEquipments()
    {
        var list = new List<EquipmentDefinition>(EquipmentDatabase.GetAllEquipments());
        list.Sort((a, b) =>
        {
            var rarityCompare = ((int)b.Rarity).CompareTo((int)a.Rarity);
            return rarityCompare != 0 ? rarityCompare : string.Compare(Localization.GetName(a), Localization.GetName(b), StringComparison.Ordinal);
        });
        return list;
    }

    private static string GetRarityDisplayName(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);

    private static Color GetRarityColor(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => new Color(0.90f, 0.92f, 0.96f),
            EquipmentRarity.Rare => new Color(0.44f, 0.70f, 1.00f),
            EquipmentRarity.Epic => new Color(0.78f, 0.48f, 1.00f),
            EquipmentRarity.Legendary => new Color(1.00f, 0.84f, 0.35f),
            _ => Colors.White
        };
    }

    private static string GetTypesDisplayText(IReadOnlyList<EquipmentType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
        {
            var name = type switch
            {
                EquipmentType.Weapon => Localization.GetEquipmentTypeName(type),
                EquipmentType.Armor => Localization.GetEquipmentTypeName(type),
                EquipmentType.Vehicle => Localization.GetEquipmentTypeName(type),
                EquipmentType.Defense => Localization.GetEquipmentTypeName(type),
                EquipmentType.Attack => Localization.GetEquipmentTypeName(type),
                EquipmentType.Accessory => Localization.GetEquipmentTypeName(type),
                EquipmentType.Buff => null,
                _ => null
            };
            if (name != null)
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? string.Join(" / ", names) : Localization.Get("inventory.debug.none");
    }

    private static string GetAcquisitionDisplayText(EquipmentAcquisitionMethod method)
    {
        return method switch
        {
            EquipmentAcquisitionMethod.Reward => Localization.Get("inventory.acquisition.reward"),
            EquipmentAcquisitionMethod.Event => Localization.Get("inventory.acquisition.event"),
            EquipmentAcquisitionMethod.Shop => Localization.Get("inventory.acquisition.shop"),
            EquipmentAcquisitionMethod.Debug => Localization.Get("inventory.acquisition.debug"),
            _ => Localization.Get("inventory.acquisition.unknown")
        };
    }
}
