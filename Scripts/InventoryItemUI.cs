//////////////////////////////////////////////////////////
// 文件：Scripts/InventoryItemUI.cs
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

// 背包/装备槽中的一个独立装备格子。相同装备不堆叠，每个实例单独显示、单独可拖拽。
/// <summary>
/// Inventory System 的公开类：InventoryItemUI。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class InventoryItemUI : PanelContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;

    public OwnedEquipment? Item { get; private set; }
    public bool ReadOnly { get; set; } = false;
    public Action<OwnedEquipment?>? OnHoverChanged;
    public Func<Guid, Guid, bool>? CanSwapDropped;
    public Action<Guid, Guid>? OnSwapDropped;

    /// <summary>
    /// Inventory System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        BuildLayout();
        MouseEntered += () =>
        {
            OnHoverChanged?.Invoke(Item);
            ShowTooltip();
        };
        MouseExited += () =>
        {
            OnHoverChanged?.Invoke(null);
            TooltipManager.Hide();
        };
    }

    /// <summary>
    /// Inventory System 的公开入口：SetItem。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetItem(OwnedEquipment item)
    {
        Item = item;
        Refresh();
    }

    /// <summary>
    /// Inventory System 的公开入口：_GetDragData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (ReadOnly || Item == null)
        {
            return default;
        }

        var preview = new Label
        {
            Text = Localization.GetName(Item.Definition)
        };
        preview.AddThemeFontSizeOverride("font_size", 18);
        SetDragPreview(preview);

        return InventoryDragPayload.Create(Item.InstanceId);
    }

    /// <summary>
    /// 允许装备格本身作为交换目标；实际合法性仍由 InventoryManager 的统一规则决定。
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (ReadOnly || Item == null || !InventoryDragPayload.TryParse(data, out var sourceId))
        {
            return false;
        }

        return CanSwapDropped?.Invoke(sourceId, Item.InstanceId) == true;
    }

    /// <summary>
    /// 将来源实例和当前格实例提交给控制器执行一次原子交换。
    /// </summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Item != null && InventoryDragPayload.TryParse(data, out var sourceId))
        {
            OnSwapDropped?.Invoke(sourceId, Item.InstanceId);
        }
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(140, 80);

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(box);

        // 默认只显示图片，不显示装备名字：名字/品质/类型/描述全部移到悬停 Tooltip 里。
        _iconRect = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddChild(_iconRect);

        // 名字标签保留字段但不再默认显示，仅用于拖拽预览等极少数场景。
        _nameLabel = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_nameLabel);

        ApplyStyle();
    }

    private void Refresh()
    {
        if (_nameLabel == null || _iconRect == null || Item == null)
        {
            return;
        }

        _nameLabel.Text = Localization.GetName(Item.Definition);
        _iconRect.Texture = IconLibrary.ResolveEquipmentIcon(Item.Definition.Id);
        ApplyStyle();
    }

    private void ShowTooltip()
    {
        if (Item == null)
        {
            return;
        }

        var definition = Item.Definition;
        var effectLines = new System.Collections.Generic.List<string>();
        for (var i = 0; i < definition.Effects.Count; i++)
        {
            var key = $"equipment.{definition.Id}.effect.{i + 1}";
            var text = Localization.Get(key);
            effectLines.Add(text.StartsWith("【Missing:", StringComparison.Ordinal)
                ? Localization.GetDescription(definition)
                : text);
        }

        var description = Localization.GetDescription(definition);
        if (effectLines.Count > 0)
        {
            description += "\n" + string.Join("\n", effectLines);
        }

        // 副标题同时包含品质文本和类型文本（都读取既有 Localization key，
        // 不新增任何本地化条目），标题颜色再额外用品质色，双重体现"品质"。
        var subtitle = $"{Localization.GetRarityName(definition.Rarity)} · {InventoryController.GetTypesDisplayText(definition.Types)}";

        var content = new TooltipContent(
            description: description,
            title: Localization.GetName(definition),
            subtitle: subtitle,
            titleColor: GetRarityColor(definition.Rarity),
            icon: IconLibrary.ResolveEquipmentIcon(definition.Id));

        TooltipManager.ShowRich(content, this);
    }

    /// <summary>
    /// 品质 → 颜色映射；背包格子边框和 Tooltip 标题共用同一份颜色，保持视觉一致。
    /// </summary>
    private static Color GetRarityColor(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => new Color(0.62f, 0.62f, 0.62f),
            EquipmentRarity.Rare => new Color(0.30f, 0.56f, 0.92f),
            EquipmentRarity.Epic => new Color(0.66f, 0.32f, 0.86f),
            EquipmentRarity.Legendary => new Color(0.92f, 0.70f, 0.18f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };
    }

    private void ApplyStyle()
    {
        var borderColor = Item != null ? GetRarityColor(Item.Definition.Rarity) : new Color(0.5f, 0.5f, 0.5f);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.16f, 0.18f),
            BorderColor = borderColor,
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 6,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6
        };
        AddThemeStyleboxOverride("panel", style);
    }
}

// 拖拽载荷的编码/解码：仅携带装备实例Id（Guid 字符串），供拖放目标解析。
/// <summary>
/// Inventory System 的公开类：InventoryDragPayload。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class InventoryDragPayload
{
    private const string Key = "instance_id";

    /// <summary>
    /// Inventory System 的公开入口：Create。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Variant Create(Guid instanceId)
    {
        var dict = new Godot.Collections.Dictionary
        {
            [Key] = instanceId.ToString()
        };
        return dict;
    }

    /// <summary>
    /// Inventory System 的公开入口：TryParse。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TryParse(Variant data, out Guid instanceId)
    {
        instanceId = Guid.Empty;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dict = data.AsGodotDictionary();
        if (!dict.ContainsKey(Key))
        {
            return false;
        }

        return Guid.TryParse(dict[Key].AsString(), out instanceId);
    }
}
