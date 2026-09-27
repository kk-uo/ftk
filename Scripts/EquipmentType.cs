//////////////////////////////////////////////////////////
// 文件：Scripts/EquipmentType.cs
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

public enum EquipmentType
{
    Buff,
    Weapon,
    Armor,
    Vehicle,
    Mount,
    Defense,
    Attack,
    Accessory
}

/// <summary>
/// Core System 的公开枚举：EquipmentRarity。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EquipmentRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Core System 的公开枚举：EquipmentAcquisitionMethod。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EquipmentAcquisitionMethod
{
    Reward,
    Event,
    Shop,
    Debug,
    Unknown
}

/// <summary>
/// 装备进入玩家背包时的运行时来源。
///
/// 数据定义中的 <see cref="EquipmentAcquisitionMethod"/> 描述“这件装备通常从哪里产出”；
/// 本枚举描述“这一次获得发生在什么流程”。两者不能混用，否则角色初始化、调试恢复等流程
/// 会被误判成正式奖励，并提前消费只监听正式奖励的 Run 效果。
/// </summary>
public enum EquipmentGainSource
{
    GameplayReward,
    ShopPurchase,
    EventReward,
    BattleReward,
    BossReward,
    ChoiceReward,
    EnemyDrop,
    CharacterStartingEquipment,
    RunInitialization,
    InitialEvent,
    Developer,
    SaveRestore,
    Transformation,
    ReforgeReplacement,
    FactionFateReplacement,
    QualityUpgradeReplacement
}

/// <summary>
/// 装备生效范围：决定一件装备的效果什么时候算"激活"。
///
/// 绝大多数装备只有放进装备槽才会生效（<see cref="EquippedOnly"/>，也是默认值，
/// 保证这次新增不影响任何已有装备的行为）；少数装备（例如黄金雕像）只要存在于
/// 背包里就会生效，不需要占用装备槽，用 <see cref="Inventory"/> 表示。
///
/// 判断某件已拥有的装备是否处于激活状态，应该统一调用
/// <see cref="InventoryManager.IsActive"/>，不要在具体业务代码里各自判断
/// "是不是背包生效装备"，也不要针对某一件具体装备写特殊分支。
/// </summary>
public enum EquipmentActivationType
{
    /// <summary>只有装备到装备槽才会生效（绝大多数装备，也是默认值）。</summary>
    EquippedOnly,

    /// <summary>只要存在于背包（无论是否装备）就会生效。</summary>
    Inventory
}
