//////////////////////////////////////////////////////////
// 文件：Scripts/EquipmentDefinition.cs
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

using System.Collections.Generic;

/// <summary>
/// Core System 的公开类：EquipmentDefinition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentDefinition : IAssetDefinition
{
    /// <summary>
    /// Core System 的公开入口：EquipmentDefinition。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EquipmentDefinition(
        string id,
        string name,
        string description,
        IReadOnlyList<EquipmentType> types,
        EquipmentRarity rarity,
        IReadOnlyList<EquipmentEffect> effects,
        EquipmentAcquisitionMethod acquisitionMethod,
        IReadOnlyList<string> unlockConditions,
        IReadOnlyList<string> tags,
        int effectPriority,
        string nameKey = "",
        string descriptionKey = "",
        string assetCode = "",
        bool canAppearInRandomPool = true,
        EquipmentActivationType activationType = EquipmentActivationType.EquippedOnly)
    {
        Id = id;
        Name = name;
        Description = description;
        Types = types;
        Rarity = rarity;
        Effects = effects;
        AcquisitionMethod = acquisitionMethod;
        UnlockConditions = unlockConditions;
        Tags = tags;
        EffectPriority = effectPriority;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
        AssetCode = assetCode;
        CanAppearInRandomPool = canAppearInRandomPool;
        ActivationType = activationType;
    }

    public string Id { get; }
    // Permanent unique identifier in EQ-TNNNN format. Never modify after creation.
    public string AssetCode { get; }
    public string Name { get; }
    public string NameKey { get; }
    public string Description { get; }
    public string DescriptionKey { get; }
    /// <summary>
    /// 装备外观与使用方式的专属本地化键。
    ///
    /// 该字段按装备 Id 约定生成，不承载数值效果；没有专属文本时由
    /// <see cref="Localization.GetEquipmentFlavorDescription"/> 按主槽位提供可读回退。
    /// </summary>
    public string FlavorDescriptionKey => $"equipment.{Id}.flavor";
    public string DisplayName => Localization.GetOrFallback(NameKey, Name);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public string DisplayFlavorDescription => Localization.GetEquipmentFlavorDescription(this);
    public IReadOnlyList<EquipmentType> Types { get; }
    public EquipmentRarity Rarity { get; }
    public IReadOnlyList<EquipmentEffect> Effects { get; }
    public EquipmentAcquisitionMethod AcquisitionMethod { get; }
    public IReadOnlyList<string> UnlockConditions { get; }
    public IReadOnlyList<string> Tags { get; }
    public int EffectPriority { get; }
    public bool CanAppearInRandomPool { get; }

    /// <summary>
    /// 生效范围：默认 <see cref="EquipmentActivationType.EquippedOnly"/>（只有装备到装备槽
    /// 才生效），和这次新增之前所有装备的实际行为完全一致——只有明确写
    /// <see cref="EquipmentActivationType.Inventory"/> 的装备（例如黄金雕像）才会
    /// "只要在背包里就生效"。是否生效应统一通过 <see cref="InventoryManager.IsActive"/>
    /// 判断，不要在具体装备效果里各自重复这个逻辑。
    /// </summary>
    public EquipmentActivationType ActivationType { get; }
}
