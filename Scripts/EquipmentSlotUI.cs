//////////////////////////////////////////////////////////
// 文件：Scripts/EquipmentSlotUI.cs
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

// 单个装备槽：Weapon / Armor / Vehicle / Accessory1-3。
// 拖拽兼容的装备放入即视为装备；若槽位已占用，由 InventoryManager 自动把旧装备放回背包，不弹确认框。
/// <summary>
/// Core System 的公开类：EquipmentSlotUI。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class EquipmentSlotUI : PanelContainer
{
    // The equipped-item row is deliberately larger than backpack cells: it is
    // the primary comparison area of the inventory screen.
    private static readonly Vector2 EquippedSlotSize = new(200, 180);
    private static readonly Vector2 EquippedItemSize = new(176, 124);

    private Label? _slotNameLabel;
    private Control? _contentArea;
    private InventoryItemUI? _itemUi;
    private Label? _emptyLabel;

    public EquipmentSlot Slot { get; private set; }
    public bool ReadOnly { get; set; } = false;
    public Action<Guid, EquipmentSlot>? OnEquipDropped;
    public Action<OwnedEquipment?>? OnHoverChanged;
    public Func<Guid, Guid, bool>? CanSwapDropped;
    public Action<Guid, Guid>? OnSwapDropped;

    /// <summary>
    /// Core System 的公开入口：Initialize。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Initialize(EquipmentSlot slot)
    {
        Slot = slot;
        BuildLayout();
    }

    /// <summary>
    /// Core System 的公开入口：SetEquippedItem。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetEquippedItem(OwnedEquipment? item)
    {
        if (_contentArea == null)
        {
            return;
        }

        foreach (var child in _contentArea.GetChildren())
        {
            _contentArea.RemoveChild(child);
            child.QueueFree();
        }

        _itemUi = null;
        _emptyLabel = null;

        if (item == null)
        {
            var placeholder = new Label
            {
                Text = Localization.Get("inventory.empty"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            placeholder.AddThemeFontSizeOverride("font_size", 20);
            placeholder.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.58f));
            _contentArea.AddChild(placeholder);
            _emptyLabel = placeholder;
            return;
        }

        _itemUi = new InventoryItemUI
        {
            OnHoverChanged = OnHoverChanged,
            CanSwapDropped = CanSwapDropped,
            OnSwapDropped = OnSwapDropped,
            ReadOnly = ReadOnly
        };
        _itemUi.CustomMinimumSize = EquippedItemSize;
        _contentArea.AddChild(_itemUi);
        _itemUi.SetItem(item);
    }

    /// <summary>
    /// 返回当前槽内物品控件，供控制器在刷新后播放不参与布局的交换反馈。
    /// </summary>
    public InventoryItemUI? GetItemControl()
    {
        return _itemUi;
    }

    /// <summary>
    /// Core System 的公开入口：_CanDropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (ReadOnly)
        {
            return false;
        }

        if (!InventoryDragPayload.TryParse(data, out var instanceId))
        {
            return false;
        }

        var item = InventoryManager.FindOwned(instanceId);
        return item != null && InventoryManager.CanEquipToSlot(item.Definition, Slot);
    }

    /// <summary>
    /// Core System 的公开入口：_DropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InventoryDragPayload.TryParse(data, out var instanceId))
        {
            OnEquipDropped?.Invoke(instanceId, Slot);
        }
    }

    private void BuildLayout()
    {
        CustomMinimumSize = EquippedSlotSize;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.15f),
            BorderColor = new Color(0.40f, 0.40f, 0.44f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 10,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 10
        };
        AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);

        _slotNameLabel = new Label
        {
            Text = GetSlotDisplayName(Slot),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _slotNameLabel.AddThemeFontSizeOverride("font_size", 20);
        _slotNameLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.80f, 0.86f));
        box.AddChild(_slotNameLabel);

        _contentArea = new CenterContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddChild(_contentArea);

        SetEquippedItem(null);
    }

    /// <summary>
    /// Core System 的公开入口：RefreshLocalization。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RefreshLocalization()
    {
        if (_slotNameLabel != null)
        {
            _slotNameLabel.Text = GetSlotDisplayName(Slot);
        }

        if (_emptyLabel != null)
        {
            _emptyLabel.Text = Localization.Get("inventory.empty");
        }

        if (_itemUi?.Item != null)
        {
            _itemUi.SetItem(_itemUi.Item);
        }
    }

    private static string GetSlotDisplayName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => Localization.Get("equip.slot.weapon"),
            EquipmentSlot.Armor => Localization.Get("equip.slot.armor"),
            EquipmentSlot.Vehicle => Localization.Get("equip.slot.vehicle"),
            EquipmentSlot.Accessory1 => Localization.Get("equip.slot.accessory1"),
            EquipmentSlot.Accessory2 => Localization.Get("equip.slot.accessory2"),
            EquipmentSlot.Accessory3 => Localization.Get("equip.slot.accessory3"),
            EquipmentSlot.Accessory4 => Localization.Get("equip.slot.accessory4"),
            EquipmentSlot.Accessory5 => Localization.Get("equip.slot.accessory5"),
            EquipmentSlot.Universal => Localization.Get("equip.slot.universal"),
            _ => slot.ToString()
        };
    }
}
