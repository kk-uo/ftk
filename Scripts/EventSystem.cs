//////////////////////////////////////////////////////////
// 文件：Scripts/EventSystem.cs
//
// 模块：Event System
//
// 职责：
// 1. 承载地图事件、事件选项与事件奖励相关代码。
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

using System;
using System.Collections.Generic;

/// <summary>
/// Event System 的公开枚举：MapNodeType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum MapNodeType
{
    Battle,
    Event,
    Elite,
    Shop,
    Boss,
    Treasure
}

/// <summary>
/// Event System 的公开类：MapNode。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class MapNode
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public MapNodeType Type;
    public int StageIndex;
    public string? FixedEventId;
    public List<string> NextNodeIds = new();

    // ── 电量（Power）系统 ────────────────────────────────────────────────
    // 进入该节点需要消耗的电量；0 表示不消耗（既有的所有固定节点默认都是0，
    // 不影响原有任何章节的行为，只有显式赋值的节点才会真正扣电量）。
    public int PowerCost;
    // 是否为继续探索阶段动态生成的节点：true 时该节点完成后会连同同批未选节点
    // 一起被移除、重新刷新一批新的可选节点（见 GameManager 的继续探索相关方法）。
    public bool IsContinueExploring;
    // 继续探索战斗节点专用：覆盖 StageDatabase 原本按节点Id查表的遭遇池，
    // 让动态生成、Id 各不相同的节点也能复用既有章节的战斗遭遇池。
    // 为空时完全走原有的 Id→StageId 查表逻辑，不影响任何既有节点。
    public string? StageIdOverride;

    // ── 固定电量消耗（第一章前五关等"不受新电量规则影响"的节点专用）────────────
    // true 时 GameManager.GetNodeEnergyCost 直接返回 FixedEnergyCost，不再按节点
    // 类型/事件品质动态计算——用于保护第一章前五关等历史固定消耗节点，以及各章节
    // 常规Boss（消耗恒为0）。false（默认）时完全不影响任何既有行为。
    public bool UseFixedEnergyCost;
    public int FixedEnergyCost;

    // ── 固定敌人编队（第四章 4-1/4-3 等"进入后编队必须锁定，不再重新随机"的节点专用）──
    // 建图时立刻调用一次 StageDatabase.RollEncounter(stageId) 并把结果缓存到这里；
    // 之后每次进入战斗都直接读取这个缓存列表，不再重新随机。为空（默认）时完全不影响
    // 任何既有节点——原有节点继续走 StageDatabase.RollEncounterForNode 现场随机的逻辑。
    public List<string>? FixedEncounterEnemyIds;
}

/// <summary>
/// Event System 的公开枚举：EventCategory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventCategory
{
    Common,
    CharacterExclusive,
    Conditional,
    Shop,
    Story,
    Special
}

/// <summary>
/// Event System 的公开枚举：EventRepeatType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventRepeatType
{
    Repeatable,
    RunOnce,
    PermanentOnce,
    // 每章最多出现一次（跨章节可重复；同章内再次随机到时会被过滤）。
    OncePerChapter
}

/// <summary>
/// Event System 的公开枚举：CharacterEventTriggerType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum CharacterEventTriggerType
{
    Random,
    Condition
}

/// <summary>
/// Event System 的公开枚举：EventStageTag。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventStageTag
{
    Any,
    EarlyGame,
    MidGame,
    LateGame,
    BossPreparation
}

/// <summary>
/// Event System 的公开枚举：EventRarity。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Event System 的公开类：EventStageRequirement。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EventStageRequirement
{
    public int MinStage = 1;
    public int MaxStage = 999;
    public EventStageTag StageTag = EventStageTag.Any;
}

/// <summary>
/// Event System 的公开枚举：EventConditionType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventConditionType
{
    CurrentStage,
    Gold,
    Forage,
    CurrentHP,
    MaxHP,
    CurrentCharacter,
    HasSkill,
    HasEquipment,
    OwnsEquipment,
    DefeatedEnemies,
    ClearedStages,
    CurrentChapter,
    CurrentChapterRoute,
    CurrentChapterVariant,
    // 持有 StringValue（逗号分隔的装备 Id 列表）中任意一件装备时满足条件。
    HasAnyEquipmentInPool,
    // 拥有（背包或已装备）StringValue（逗号分隔）中任意一件装备时满足条件。
    OwnsAnyEquipmentInPool,
    // 当前角色阵营匹配 StringValue（与 Faction 枚举名称比较，不区分大小写）。
    CurrentCharacterFaction,
    // 玩家可用牌集包含指定牌型（StringValue = CardType 枚举名称）。
    HasPlayerCardType,
    // 玩家背包或装备槽中当前至少有一件装备。
    HasAnyOwnedEquipment,
    // 当前角色匹配 StringValue，或玩家当前桃基础回复量至少为 IntValue。
    CurrentCharacterOrPeachBaseHealAtLeast,
    // 玩家持有 StringValue 指定 Id 的局内 Buff（RunBuff）。
    HasRunBuff,
    // 玩家本局是否曾经触发过（进入过）StringValue 指定 Id 的事件，不区分当时选择了哪个选项。
    HasSeenEvent,
    // 整合式教程当前是否处于激活状态（IntegratedTutorialFlow.IsActive）。专门用来把
    // 教学专属事件（如【路边补给】）锁死在教学固定节点上，防止它通过
    // EventManager.GetEventForNode 的通用候选池泄漏进正式章节的随机事件池。
    TutorialIntegratedActive
}

/// <summary>
/// Event System 的公开枚举：EventComparison。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventComparison
{
    Equal,
    GreaterOrEqual,
    LessOrEqual
}

/// <summary>
/// Event System 的公开类：EventCondition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EventCondition
{
    public EventConditionType Type;
    public EventComparison Comparison = EventComparison.Equal;
    public int IntValue;
    public string StringValue = string.Empty;
    // 为 true 时反转条件结果（"不满足原条件"才视为满足）。
    public bool Negate;
}

/// <summary>
/// Event System 的公开枚举：EventRewardType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventRewardType
{
    Gold,
    // 在 [Amount, MaxAmount] 闭区间内随机获得金币。
    RandomGold,
    Forage,
    Heal,
    MaxHp,
    // 临时生命值：叠加在当前生命上，可超过最大生命上限，不修改最大生命。战斗结束后自动清除。
    TempHp,
    Skill,
    Equipment,

    // 以下为预留类型：数据结构已定义，结算逻辑尚未接入 EventManager.ApplyReward。
    // RandomEquipmentFromPool：StringValue 为逗号分隔的装备Id候选池，等权重随机获得其中一件。
    RandomEquipmentFromPool,
    // RandomEquipmentByRarity：StringValue 为 EquipmentRarity 名称，随机获得一件可正常获得的对应品质装备。
    RandomEquipmentByRarity,
    // DeferredEquipmentAfterNextBattle：记录“下一场战斗结束后随机获得一件装备”。StringValue 记录装备稀有度池（如 "common,rare"），允许重复获得。
    DeferredEquipmentAfterNextBattle,
    // DeferredEquipmentAfterChapterBoss：记录“击败指定章节Boss后获得装备”。StringValue 为目标装备Id，Amount 记录目标章节号。
    DeferredEquipmentAfterChapterBoss,
    // OpenShopUI：记录”直接打开商店UI”动作，结算逻辑尚未接入（暂无商店UI实现）。
    OpenShopUI,

    // ————————————————————————————————————————
    // 石碑 / 芯片系统新增奖励类型
    // ————————————————————————————————————————
    // 攻击性锦囊（万箭齐发/南蛮入侵）伤害永久 +Amount（与芯片叠加，不替换）。
    AttackTrickDamageBonus,
    // 所有杀系（普通杀/火杀/雷杀/必中杀）伤害永久 +Amount（与芯片叠加，不替换）。
    RunKillDamageBonus,
    // 石碑血祭随机：50% 最大生命+20/当前生命+20，50% 杀系伤害+5；在 ApplyReward 内掷骰。
    SteleBloodPriceRandom,
    // 防御芯片计数 +1；HP 变化由 MaxHp / Heal 奖励单独处理。
    DefenseChipCount,
    // 攻击芯片计数 +1；杀系加伤 = count×3，由 GameManager.RunKillDamageBonus 计算。
    AttackChipInstall,
    // 知识芯片计数 +1；锦囊加伤 = count×5，由 GameManager.AttackTrickDamageBonus 计算。
    KnowledgeChipInstall,
    // 预留：锈类武器升级接口（FutureRustWeaponUpgrade）——当前无实现，仅返回 null 使 option.ResultText 生效。
    FutureRustWeaponUpgrade,
    // 设置章节路线；Amount = (int)ChapterRoute 枚举值。
    SetChapterRoute,
    // 从玩家可用牌集中移除指定牌型（StringValue = CardType 枚举名称）。
    RemovePlayerCardType,
    // 向玩家可用牌集中添加指定牌型（StringValue = CardType 枚举名称）。
    AddPlayerCardType,
    // 原子化"替换卡牌"：一步完成移除旧牌型+加入新牌型，而不是靠两条独立的
    // RemovePlayerCardType/AddPlayerCardType 拼接（StringValue=旧CardType枚举名称，
    // SecondaryStringValue=新CardType枚举名称）。语义上就是 RemovePlayerCardType+
    // AddPlayerCardType 的组合，但作为事件数据里的单一条目，避免被拆散/漏配对。
    ReplaceCardType,
    // 藏宝阁：进入事件专属特殊战斗。
    StartTreasurePavilionBattle,
    // 藏宝阁：提供随机史诗装备三选一。Amount 可用于附加金币奖励。
    ChooseOneEpicEquipment,
    // 添加局外 Buff。StringValue = BuffId，Amount 可覆盖默认持续战斗数。
    RunBuff,
    // 最大电量永久+Amount（本局永久生效，不随章节切换重置；进入新章节按新上限回满）。
    MaxPowerBonus,
    // 按当前最大生命百分比提升最大生命，并同步增加等量当前生命。Amount = 百分比整数。
    MaxHpPercentWithCurrentSync,
    // 进入指定事件：StringValue 为目标事件Id，EventController 接收后重新渲染新事件界面（不关闭当前节点）。
    TriggerEvent,
    // 随机触发指定章节的非商店事件。Amount = 章节号。
    TriggerRandomChapterNonShopEvent,
    // 打开载具商店UI。EventController 收到后发出 VehicleShopRequested 信号，由 MainFlow 统一切换界面。
    OpenVehicleShop,
    // 打开巫婆的传奇商店。
    OpenWitchShop,
    // 回复至满血（当前生命恢复到最大生命值）。
    HealFull,
    // 南蛮入侵本局永久伤害加成；Amount = 增加的百分比（如 50 表示 ×1.5）。
    NanmanDamageMultiply,
    // 蛮族专属进化：移除【蛮族】技能，获得【蛮族之王】技能。
    EvolveManzuToManzuWang,
    // 蛮族营寨决斗：开始流亡蛮族×2的特殊战斗，胜利获得蛮族的牙齿。
    StartManZuCampBattle,
    // 蛮族营寨偷窃：50%成功获得200金币，50%失败进入决斗。
    ManZuCampTheft,
    // 古蜀铸炉：销毁全部装备并清空金币。
    DestroyAllEquipmentAndGold,
    // 古蜀铸炉：按当前生命值百分比扣除HP；Amount = 百分比整数（如50表示失去50%当前HP，最低保留1）。
    LoseCurrentHpPercent,
    // 古蜀铸炉：当前生命固定为1，并按当前最大生命值降低百分比；Amount = 百分比整数。
    // 用原子奖励保证生命上限变化不会把“当前生命为1”的结算顺序打断。
    SetCurrentHpToOneAndLoseMaxHpPercent,
    // 古蜀铸炉：传奇装备三选一（EventController 收到信号后展示选择UI）。
    ChooseOneLegendaryEquipment,
    // 禁书库暗门：传奇武器三选一（严格筛选武器栏位，不混入护甲、载具或饰品）。
    ChooseOneLegendaryWeaponEquipment,
    // 古蜀铸炉：稀有/史诗装备三选一（EventController 收到信号后展示选择UI）。
    ChooseOneRareOrEpicEquipment,
    // 将 StringValue 指定装备转化为 SecondaryStringValue 指定装备；未持有原装备时不执行。
    TransformEquipment,
    // 移除玩家拥有的指定装备（不返还金币）；StringValue = 装备Id。
    RemoveSpecificEquipment,
    // 禁书库：回收全部芯片（攻击/防御/知识）计数归零，不退还HP。
    RemoveAllChips,
    // 禁书库：随机获得一个稀有技能（非Boss技能、玩家未持有）。
    RandomRareSkill,
    // 禁书库：连续两次芯片三选一（EventController 收到信号后展示选择UI）。
    ChooseOneChipTwice,
    // 单次芯片三选一。
    ChooseOneChipOnce,
    // 禁书库：吕蒙专属，将克己升级为克己·无限制协议。
    EvolveKejiToKejiUnlimited,
    // 七星坛：进入作法招魂特殊战斗（第一章Boss强化版）。
    StartQiXingTanBattle,
    // 七星坛：诸葛亮专属，将观星升级为天机。
    EvolveGuanxingToTianJi,
    // 古蜀铸炉·回炉重铸：选择一件已持有装备摧毁，获得高一品质的随机装备（EventController 收到信号后展示选择UI）。
    SacrificeOneEquipmentForUpgrade,
    // 鼠王事件②：随机销毁一件稀有及以上装备，获得高一品质的随机装备。
    RatKingOfferGift,
    // 鼠王事件③：进入鼠王特殊战斗（鼠王+机械巨鼠×2）。
    StartRatKingEventBattle,
    // 汉室宗祠：随机获得一件史诗饰品（Accessory类型）装备。
    RandomEpicAccessory,
    // 汉室宗祠：酒的增伤倍率永久+0.5。
    WineDamageMultiplierBonus,
    // 汉室宗祠：夺舍灵魂，清理技能/装备并获得四个带SoulPossessionSkill标记的技能。
    SoulPossession,
    // 汉室宗祠：获得【先祖的赐福】3层。
    AncestorBlessing,
    // 囚禁的精灵：弹出四选一元素强化界面。
    ChooseElfElementBlessing,
    // 囚禁的精灵：选择一件装备，重铸为同品质的另一件装备。
    ElfEquipmentReforge,
    // 改造铺：从玩家当前拥有的武器（包含已装备）中随机展示至多三件，选择一件重铸。
    ModificationShopWeaponReforge,
    // 囚禁的精灵：本局桃基础回复量+Amount。
    PeachBaseHealBonus,
    // 追兵①"杀出去"：进入追兵特殊战斗（追捕者×3）。
    StartChasingPursuersBattle,
    // 追兵③"声东击西"：50%成功获得50金币并保留全部遗物；
    // 50%失败失去【遗忘之石】/【雕像核心】（若拥有）并损失15点生命。
    ChasingPursuersDiversion,
    // 恶臭蘑菇：Amount 为洗牌后的颜色槽位索引（0/1/2），实际装备Id由
    // GameManager.GetStinkyMushroomColorForOption(Amount) 在本局内一次性随机决定并缓存。
    StinkyMushroomEquipmentForOption,

    // 电量永久获得（非本局永久上限，只是一次性发放）；Amount = 获得数量。
    // 复用 GameManager.AddPower，与地图消耗电量走同一个入口。
    PowerGain,
    // 爆炸果实①"碰一下"：扣除 Amount 点当前生命值；不受 EventCostType.CurrentHP 的
    // CanAffordCost 门槛限制（该选项必须始终可选），因此扣血导致的死亡由
    // GameManager.ApplyExplosiveFruitTouchDamage 内部按既有"战败消耗粮草复活/
    // 粮草耗尽游戏结束"流程处理，而不是把选项禁用掉。
    ExplosiveFruitTouchDamage,
    // 爆炸果实①：永久获得第4个"普通饰品槽"（仅允许普通品质饰品装备，与
    // 吴·多宝架的 Accessory4 相互独立，互不影响）。
    ExplosiveFruitAccessorySlot,
    // 爆炸果实②："远处射箭激发果实"：本局内永久让万箭齐发的伤害属性变为火属性，
    // 只影响 DamageEvent.DamageType（享受所有按 DamageType 判定的火属性加成），
    // 不修改万箭齐发的费用/目标数量/基础伤害/卡牌类型/是否属于杀。
    ExplosiveFruitArrowBarrageFireUpgrade,
    // 酒馆·点杯酒：本局永久生效，濒死自动饮酒救援不再消耗费用（原本需要1费）。
    FreeWineRevive,
    // 打开酒馆的黑市：装备品质额外获得10%传奇装备概率，所有商品价格×1.5。
    OpenBlackMarketShop
}

/// <summary>
/// Event System 的公开类：EventReward。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EventReward
{
    public EventRewardType Type;
    public int Amount;
    // RandomGold 等区间奖励的上限；普通固定奖励保持默认0。
    public int MaxAmount;
    public string StringValue = string.Empty;
    // 仅用于 ReplaceCardType：目标（新）CardType 枚举名称，StringValue 存旧CardType。
    public string SecondaryStringValue = string.Empty;
    // 仅用于 RandomEquipmentFromPool：包含 {name} 占位符时，结果文本中将自动替换为实际装备名称。
    public string ResultTextTemplate = string.Empty;
}

/// <summary>
/// Event System 的公开枚举：EventCostType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EventCostType
{
    Gold,
    Forage,
    CurrentHP,
    // 电量成本：调用 GameManager.TrySpendPower 结算，与 CanAffordPower 保持一致。
    Power,
    // 最大生命值成本：永久扣减，通过 GameManager.AddMaxHp 结算（已在方法内部保证不低于1）。
    MaxHealth,
    // 随机芯片成本：从当前持有的攻击/防御/知识芯片中等权重随机失去1个；无任何芯片时不可选。
    RandomChip
}

/// <summary>
/// Event System 的公开类：EventCost。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EventCost
{
    public EventCostType Type;
    public int Amount;
}

/// <summary>
/// Event System 的公开类：EventOption。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EventOption
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public string ResultTextKey = string.Empty;

    public string DisplayName => Localization.GetOrFallback(NameKey, Name);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public string DisplayResultText => Localization.GetOrFallback(ResultTextKey, ResultText);

    // 新奖励架构入口。新事件优先使用 RewardSequence；旧 Rewards/Costs 保持兼容。
    public RewardSequence RewardSequence { get; } = new();
    public List<EventReward> Rewards = new();
    public List<EventCost> Costs = new();
    // 选项级可见性条件：列表中所有条件均满足时该选项才显示并可选择；任一不满足则整个选项在 UI 中隐藏。
    public List<EventCondition> Conditions = new();
    // 选择该选项并成功结算后展示给玩家的结果文本（静态文案）。为空时使用 ApplyReward 产生的动态文本；
    // 两者都为空则不展示结果面板，直接关闭事件（兼容未设置该字段的既有事件）。
    public string ResultText = string.Empty;

    // 阵营命运·改命 用：默认 false/null，不影响现有12个初始事件的既有数据。
    // 只有显式把这两个字段都设置好的事件选项，才会在命中"改命"命运时显示刷新按钮。
    public bool CanFactionReroll = false;
    public string? RefreshPoolId = null;
}

/// <summary>
/// Event System 的公开类：EventData。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public class EventData : IAssetDefinition
{
    public string Id { get; set; } = string.Empty;
    // Permanent unique identifier in EV-CARNNN format. Never modify after creation.
    public string AssetCode { get; set; } = string.Empty;
    public string EventCode
    {
        get => AssetCode;
        set => AssetCode = value;
    }
    public string Name = string.Empty;
    public string Description = string.Empty;
    // Localization keys for the event name and description.
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string DisplayName => Localization.GetOrFallback(NameKey, Name);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public EventCategory Category;
    public EventRepeatType RepeatType;
    public EventStageRequirement StageRequirement = new();
    public List<EventCondition> Conditions = new();
    public List<EventOption> Options = new();
    public int Weight = 1;
    public EventRarity Rarity = EventRarity.Common;
    public List<string> EventTags = new();
    public bool IsRepeatable = true;
    // 事件选择结算后是否直接离开事件界面。默认保留所有既有事件的结果页行为；
    // 仅用于明确要求“不显示选项结果”的事件。
    public bool SuppressResultPresentation;
    // 若当前角色 Id 与此字段匹配，则该事件在满足其他条件时必定出现（优先于权重随机）。
    public string GuaranteedForCharacterId = string.Empty;
    // 若当前章节变种与此字段匹配，则该事件在满足其他条件时必定出现（优先于权重随机）。
    public string GuaranteedForChapterVariant = string.Empty;
    // 若玩家拥有该装备，则事件在满足其他条件时必定出现一次（优先于普通权重随机）。
    public string GuaranteedWhenOwnsEquipmentId = string.Empty;
    // 若玩家持有该 RunBuff Id，则事件在满足其他条件时必定出现一次（优先于普通权重随机）。
    public string GuaranteedWhenHasRunBuffId = string.Empty;
}

/// <summary>
/// Event System 的公开类：CharacterExclusiveEventData。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class CharacterExclusiveEventData : EventData
{
    public string RequiredCharacter = string.Empty;
    public CharacterEventTriggerType TriggerType = CharacterEventTriggerType.Random;
}

/// <summary>
/// Event System 的公开类：EventManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EventManager
{
    private static readonly Random Random = new();
    private static readonly HashSet<string> PermanentSeenEvents = new();

    /// <summary>
    /// Event System 的公开入口：GetAllEvents。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EventData> GetAllEvents()
    {
        return EventDatabase.GetAllEvents();
    }

    /// <summary>
    /// Event System 的公开入口：GetEventForNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData? GetEventForNode(MapNode node)
    {
        var shouldCacheReplacement = false;
        if (!string.IsNullOrWhiteSpace(node.FixedEventId))
        {
            var fixedEvent = EventDatabase.GetEvent(node.FixedEventId);
            if (fixedEvent != null && CanUseEvent(fixedEvent, node))
            {
                return fixedEvent;
            }

            // FixedEventId 是随机结果缓存，不是绕过章节/路线规则的特权入口。
            // 数据规则修正或旧Run恢复后若缓存已失效，清空并在当前上下文重新抽取，
            // 避免继续打开跨章节事件，也避免节点因旧缓存直接变成空事件。
            node.FixedEventId = null;
            shouldCacheReplacement = true;
        }

        // 初始事件⑯：第一章event_2 → 随机第二章事件
        if (GameManager.InitialEventCh1Event2Ch2 && node.Id == "event_2")
        {
            GameManager.ConsumeInitialEventCh1Event2Ch2();
            var ch2Event = GetRandomCh2EventForInitialEvent();
            if (ch2Event != null)
            {
                if (shouldCacheReplacement) node.FixedEventId = ch2Event.Id;
                return ch2Event;
            }
        }

        var candidates = new List<EventData>();
        var equipmentGuaranteedCandidates = new List<EventData>();
        var guaranteedCandidates = new List<EventData>();
        foreach (var eventData in GetAllEvents())
        {
            if (!CanUseEvent(eventData, node))
            {
                continue;
            }

            candidates.Add(eventData);
            if (!string.IsNullOrEmpty(eventData.GuaranteedWhenOwnsEquipmentId)
                && GameManager.OwnsEquipment(eventData.GuaranteedWhenOwnsEquipmentId))
            {
                equipmentGuaranteedCandidates.Add(eventData);
            }
            else if (!string.IsNullOrEmpty(eventData.GuaranteedWhenHasRunBuffId)
                && RunBuffManager.CountStacks(eventData.GuaranteedWhenHasRunBuffId) > 0)
            {
                equipmentGuaranteedCandidates.Add(eventData);
            }
            else if (!string.IsNullOrEmpty(eventData.GuaranteedForCharacterId)
                && eventData.GuaranteedForCharacterId == GameManager.CurrentCharacterId)
            {
                guaranteedCandidates.Add(eventData);
            }
            else if (!string.IsNullOrEmpty(eventData.GuaranteedForChapterVariant)
                     && string.Equals(
                         eventData.GuaranteedForChapterVariant,
                         GameManager.CurrentChapterVariant.ToString(),
                         StringComparison.Ordinal))
            {
                guaranteedCandidates.Add(eventData);
            }
        }

        var selected = equipmentGuaranteedCandidates.Count > 0
            ? PickWeightedEvent(equipmentGuaranteedCandidates)
            : guaranteedCandidates.Count > 0
                ? PickWeightedEvent(guaranteedCandidates)
                : PickWeightedEvent(candidates);
        if (shouldCacheReplacement && selected != null)
        {
            node.FixedEventId = selected.Id;
        }

        return selected;
    }

    // ── 电量系统·继续探索：按品质guaranteed取事件 ──────────────────────────
    // 只在生成继续探索节点时使用，不改变 GetEventForNode 本身的既有选取逻辑；
    // 选中的事件通过 MapNode.FixedEventId 固定下来，进入节点时仍然走
    // GetEventForNode 现有的"FixedEventId优先"分支，不新增额外调用路径。

    /// <summary>
    /// Event System 的公开入口：HasAnyEventForRarity。
    ///
    /// 判断当前上下文（章节/路线/角色等，由 probeNode 的 StageIndex/Type 决定）
    /// 是否存在至少一个可用的指定品质事件——用于避免刷新出"这个品质在本章
    /// 根本没有事件"的空节点。
    /// </summary>
    public static bool HasAnyEventForRarity(EventRarity rarity, MapNode probeNode)
    {
        foreach (var eventData in GetAllEvents())
        {
            if (eventData.Rarity == rarity && CanUseEvent(eventData, probeNode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Event System 的公开入口：GetEventForNodeWithRarity。
    ///
    /// 从指定品质的候选事件中按既有权重随机选一个；找不到时返回 null
    /// （调用方应先用 <see cref="HasAnyEventForRarity"/> 判断是否要提供该品质节点）。
    /// </summary>
    public static EventData? GetEventForNodeWithRarity(MapNode node, EventRarity rarity)
    {
        var candidates = new List<EventData>();
        foreach (var eventData in GetAllEvents())
        {
            if (eventData.Rarity == rarity && CanUseEvent(eventData, node))
            {
                candidates.Add(eventData);
            }
        }

        return candidates.Count > 0 ? PickWeightedEvent(candidates) : null;
    }

    // 检查选项的所有可见性条件是否满足；供 EventController 在渲染前过滤，以及 TryResolveOption 的防御性校验。
    /// <summary>
    /// Event System 的公开入口：IsOptionVisible。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsOptionVisible(EventOption option)
    {
        foreach (var condition in option.Conditions)
        {
            if (!MatchesCondition(condition))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 返回当前游戏状态下真正可见的事件选项。
    ///
    /// 事件 UI 只消费这个结果，不自行解释角色、阵营、装备、技能、RunBuff 或章节条件，
    /// 从而保证普通模式与开发者模式遵循完全相同的隐藏规则。
    /// </summary>
    public static IReadOnlyList<EventOption> GetVisibleOptions(EventData eventData)
    {
        var visibleOptions = new List<EventOption>();
        foreach (var option in eventData.Options)
        {
            if (IsOptionVisible(option))
            {
                visibleOptions.Add(option);
            }
        }

        return visibleOptions;
    }

    /// <summary>
    /// Event System 的公开入口：CanAffordOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanAffordOption(EventOption option, out string reason)
    {
        foreach (var cost in option.Costs)
        {
            if (!CanAffordCost(cost))
            {
                reason = GetInsufficientResourceMessage(cost.Type);
                return false;
            }
        }

        if (!RewardManager.CanExecute(option.RewardSequence, out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // 先校验选项的全部消耗是否可承担（金币/粮草/生命），任一不足则不执行任何结算，返回 false 并给出原因。
    // 校验通过后才真正扣除消耗、发放奖励、标记事件已见。resultText 优先取结算过程中产生的动态文本，
    // 没有动态文本时回退到该选项预设的静态 ResultText（两者都为空则由调用方决定是否展示结果面板）。
    /// <summary>
    /// Event System 的公开入口：TryResolveOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TryResolveOption(EventData eventData, EventOption option, out string resultText)
    {
        // 防御性检查：条件不满足的选项不应被结算（正常情况下 UI 已隐藏，此处为安全兜底）。
        if (!IsOptionVisible(option))
        {
            resultText = string.Empty;
            return false;
        }

        foreach (var cost in option.Costs)
        {
            if (!CanAffordCost(cost))
            {
                resultText = GetInsufficientResourceMessage(cost.Type);
                return false;
            }
        }

        var dynamicMessages = new List<string>();
        if (!option.RewardSequence.IsEmpty)
        {
            var rewardResult = RewardManager.Execute(option.RewardSequence);
            if (!string.IsNullOrEmpty(rewardResult.PlayerMessage))
            {
                dynamicMessages.Add(rewardResult.PlayerMessage);
            }
        }
        else
        {
            foreach (var cost in option.Costs)
            {
                ApplyCost(cost);
            }

            for (var rewardIndex = 0; rewardIndex < option.Rewards.Count; rewardIndex++)
            {
                var reward = option.Rewards[rewardIndex];

                // 旧事件数据用多条相同 RunBuff 奖励表达层数。结算时合并连续的
                // 同 Buff / 同持续时间条目，既保持既有数值语义，也避免显示三行
                // “Gain [Curse]”。其它同类 Buff 会自动获得相同的 ×N 展示方式。
                if (reward.Type == EventRewardType.RunBuff)
                {
                    var stackCount = 1;
                    while (rewardIndex + stackCount < option.Rewards.Count)
                    {
                        var nextReward = option.Rewards[rewardIndex + stackCount];
                        if (nextReward.Type != EventRewardType.RunBuff
                            || nextReward.StringValue != reward.StringValue
                            || nextReward.Amount != reward.Amount)
                        {
                            break;
                        }

                        stackCount++;
                    }

                    var runBuffResult = RewardManager.Execute(
                        new RewardSequence().Add(new AddRunBuffRewardAction(
                            reward.StringValue,
                            stackCount,
                            reward.Amount > 0 ? reward.Amount : null)));
                    if (!string.IsNullOrEmpty(runBuffResult.PlayerMessage))
                    {
                        dynamicMessages.Add(runBuffResult.PlayerMessage);
                    }

                    rewardIndex += stackCount - 1;
                    continue;
                }

                var message = ApplyReward(reward);
                if (!string.IsNullOrEmpty(message))
                {
                    dynamicMessages.Add(message);
                }
            }
        }

        GameManager.MarkEventSeen(eventData.Id, eventData.RepeatType);
        if (eventData.RepeatType == EventRepeatType.PermanentOnce)
        {
            PermanentSeenEvents.Add(eventData.Id);
        }
        if (eventData.RepeatType == EventRepeatType.OncePerChapter)
        {
            GameManager.MarkEventSeenInCurrentChapter(eventData.Id);
        }

        resultText = dynamicMessages.Count > 0 ? string.Join("\n", dynamicMessages) : option.DisplayResultText;

        // 图鉴：选项下标在这个事件定义的生命周期内视为稳定ID（EventOption本身没有独立的
        // 稳定字符串ID字段）。这里是全部事件选项结算成功的唯一出口，不会漏记也不会重复记。
        var optionIndex = eventData.Options.IndexOf(option);
        if (optionIndex >= 0)
        {
            CodexService.RecordEventChoice(eventData.Id, optionIndex);
        }

        return true;
    }

    private static bool CanAffordCost(EventCost cost)
    {
        return cost.Type switch
        {
            // 魏·官方特权：事件选项金币成本×25%。CanAffordCost 和 ApplyCost 必须调用同一个
            // FactionFateManager.ApplyEventGoldCostDiscount，否则会出现"显示折扣价、实扣原价"的bug。
            EventCostType.Gold => GameManager.Gold >= FactionFateManager.ApplyEventGoldCostDiscount(cost.Amount),
            EventCostType.Forage => GameManager.Forage >= cost.Amount,
            EventCostType.CurrentHP => GameManager.CurrentHP >= cost.Amount,
            EventCostType.Power => GameManager.CanAffordPower(cost.Amount),
            // 保证扣完后至少剩1点最大生命值，不能等于（AddMaxHp 内部也会兜底，这里提前拒绝显示为可选）。
            EventCostType.MaxHealth => GameManager.MaxHP > cost.Amount,
            EventCostType.RandomChip => GameManager.AttackChipCount + GameManager.DefenseChipCount + GameManager.KnowledgeChipCount > 0,
            _ => true
        };
    }

    private static string GetInsufficientResourceMessage(EventCostType type)
    {
        return type switch
        {
            EventCostType.Gold => Localization.Get("ui.insufficient_gold"),
            EventCostType.Forage => Localization.Get("ui.insufficient_forage"),
            EventCostType.CurrentHP => Localization.Get("ui.insufficient_hp"),
            EventCostType.Power => Localization.Get("ui.insufficient_power"),
            EventCostType.MaxHealth => Localization.Get("ui.insufficient_max_hp"),
            EventCostType.RandomChip => Localization.Get("ui.no_chip_to_lose"),
            _ => Localization.Get("ui.requirements_not_met")
        };
    }

    /// <summary>
    /// Event System 的公开入口：CanUseEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanUseEvent(EventData eventData, MapNode node)
    {
        if (node.Type == MapNodeType.Shop && eventData.Category != EventCategory.Shop)
        {
            return false;
        }

        if (node.Type == MapNodeType.Event && eventData.Category == EventCategory.Shop)
        {
            return false;
        }

        // Special 类事件仅通过 TriggerEvent 奖励直接加载，不参与节点随机选取。
        if (node.Type == MapNodeType.Event && eventData.Category == EventCategory.Special)
        {
            return false;
        }

        if (!MatchesRepeatRule(eventData))
        {
            return false;
        }

        if (!MatchesStage(eventData.StageRequirement, node))
        {
            return false;
        }

        if (eventData is CharacterExclusiveEventData characterEvent)
        {
            if (GameManager.CurrentCharacter?.Name != characterEvent.RequiredCharacter)
            {
                return false;
            }
        }

        foreach (var condition in eventData.Conditions)
        {
            if (!MatchesCondition(condition))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesRepeatRule(EventData eventData)
    {
        return eventData.RepeatType switch
        {
            EventRepeatType.Repeatable => true,
            EventRepeatType.RunOnce => !GameManager.HasSeenEventInRun(eventData.Id),
            EventRepeatType.PermanentOnce => !PermanentSeenEvents.Contains(eventData.Id),
            EventRepeatType.OncePerChapter => !GameManager.HasSeenEventInCurrentChapter(eventData.Id),
            _ => true
        };
    }

    private static bool MatchesStage(EventStageRequirement requirement, MapNode node)
    {
        var currentStage = node.StageIndex + 1;
        if (currentStage < requirement.MinStage || currentStage > requirement.MaxStage)
        {
            return false;
        }

        return requirement.StageTag switch
        {
            EventStageTag.Any => true,
            EventStageTag.EarlyGame => currentStage <= 2,
            EventStageTag.MidGame => currentStage >= 2 && currentStage <= 4,
            EventStageTag.LateGame => currentStage >= 4,
            EventStageTag.BossPreparation => node.Type == MapNodeType.Shop || node.Type == MapNodeType.Event,
            _ => true
        };
    }

    private static bool MatchesCondition(EventCondition condition)
    {
        var result = condition.Type switch
        {
            EventConditionType.CurrentStage => CompareInt(GameManager.CurrentStage + 1, condition),
            EventConditionType.Gold => CompareInt(GameManager.Gold, condition),
            EventConditionType.Forage => CompareInt(GameManager.Forage, condition),
            EventConditionType.CurrentHP => CompareInt(GameManager.CurrentHP, condition),
            EventConditionType.MaxHP => CompareInt(GameManager.MaxHP, condition),
            // CurrentCharacter 与角色 Id（非显示名称）比较，保持与 CharacterIds 常量一致。
            EventConditionType.CurrentCharacter => string.Equals(GameManager.CurrentCharacterId, condition.StringValue, StringComparison.Ordinal),
            EventConditionType.HasSkill =>
                GameManager.HasAcquiredSkill(condition.StringValue)
                || GameManager.CurrentCharacterHasSkill(condition.StringValue),
            EventConditionType.HasEquipment => GameManager.HasEquipment(condition.StringValue),
            EventConditionType.OwnsEquipment => GameManager.OwnsEquipment(condition.StringValue),
            EventConditionType.DefeatedEnemies => CompareInt(GameManager.DefeatedEnemyCount, condition),
            EventConditionType.ClearedStages => CompareInt(GameManager.ClearedBattleCount, condition),
            EventConditionType.CurrentChapter => CompareInt(GameManager.CurrentChapter, condition),
            EventConditionType.CurrentChapterRoute =>
                string.Equals(GameManager.CurrentChapterRoute.ToString(), condition.StringValue, StringComparison.Ordinal),
            EventConditionType.CurrentChapterVariant =>
                string.Equals(GameManager.CurrentChapterVariant.ToString(), condition.StringValue, StringComparison.Ordinal),
            EventConditionType.HasAnyEquipmentInPool => HasAnyEquipmentInPool(condition.StringValue),
            EventConditionType.OwnsAnyEquipmentInPool => OwnsAnyEquipmentInPool(condition.StringValue),
            EventConditionType.CurrentCharacterFaction =>
                GameManager.CurrentCharacter != null &&
                string.Equals(GameManager.CurrentCharacter.Faction.ToString(), condition.StringValue, StringComparison.OrdinalIgnoreCase),
            EventConditionType.HasPlayerCardType =>
                System.Enum.TryParse<CardType>(condition.StringValue, out var condCardType)
                && GameManager.HasPlayerCardType(condCardType),
            EventConditionType.HasAnyOwnedEquipment => InventoryManager.GetAllOwned().Count > 0,
            EventConditionType.CurrentCharacterOrPeachBaseHealAtLeast =>
                string.Equals(GameManager.CurrentCharacterId, condition.StringValue, StringComparison.Ordinal)
                || GameManager.CurrentPlayerPeachBaseHealAmount >= condition.IntValue,
            EventConditionType.HasRunBuff => RunBuffManager.CountStacks(condition.StringValue) > 0,
            EventConditionType.HasSeenEvent => GameManager.HasSeenEventInRun(condition.StringValue),
            EventConditionType.TutorialIntegratedActive => IntegratedTutorialFlow.IsActive,
            _ => true
        };
        return condition.Negate ? !result : result;
    }

    private static bool CompareInt(int current, EventCondition condition)
    {
        return condition.Comparison switch
        {
            EventComparison.Equal => current == condition.IntValue,
            EventComparison.GreaterOrEqual => current >= condition.IntValue,
            EventComparison.LessOrEqual => current <= condition.IntValue,
            _ => false
        };
    }

    // 检查玩家是否持有逗号分隔候选池中任意一件装备（OR 逻辑）。
    private static bool HasAnyEquipmentInPool(string idsCsv)
    {
        foreach (var id in idsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GameManager.HasEquipment(id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OwnsAnyEquipmentInPool(string idsCsv)
    {
        foreach (var id in idsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GameManager.OwnsEquipment(id))
            {
                return true;
            }
        }

        return false;
    }

    private static EventData? GetRandomCh2EventForInitialEvent()
    {
        var candidates = new List<EventData>();
        foreach (var eventData in GetAllEvents())
        {
            // 排除商店、特殊类事件
            if (eventData.Category == EventCategory.Shop || eventData.Category == EventCategory.Special)
                continue;
            // 排除本局已永久消耗的事件
            if (!MatchesRepeatRule(eventData))
                continue;
            // 排除角色专属（当前角色不匹配）
            if (eventData is CharacterExclusiveEventData charEvent
                && GameManager.CurrentCharacter?.Name != charEvent.RequiredCharacter)
                continue;
            // 必须有明确的 CurrentChapter == 2 条件，以识别第二章事件
            var isCh2 = false;
            foreach (var cond in eventData.Conditions)
            {
                if (cond.Type == EventConditionType.CurrentChapter
                    && cond.Comparison == EventComparison.Equal
                    && cond.IntValue == 2
                    && !cond.Negate)
                {
                    isCh2 = true;
                    break;
                }
            }
            if (!isCh2) continue;
            candidates.Add(eventData);
        }
        return PickWeightedEvent(candidates);
    }

    private static EventData? PickWeightedEvent(List<EventData> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var total = 0;
        foreach (var candidate in candidates)
        {
            total += Math.Max(1, candidate.Weight);
        }

        var roll = Random.Next(1, total + 1);
        foreach (var candidate in candidates)
        {
            roll -= Math.Max(1, candidate.Weight);
            if (roll <= 0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static void ApplyCost(EventCost cost)
    {
        switch (cost.Type)
        {
            case EventCostType.Gold:
                GameManager.AddGold(-FactionFateManager.ApplyEventGoldCostDiscount(cost.Amount));
                break;
            case EventCostType.Forage:
                GameManager.AddForage(-cost.Amount);
                break;
            case EventCostType.CurrentHP:
                GameManager.AddCurrentHp(-cost.Amount);
                break;
            case EventCostType.Power:
                GameManager.TrySpendPower(cost.Amount);
                break;
            case EventCostType.MaxHealth:
                GameManager.AddMaxHp(-cost.Amount);
                break;
            case EventCostType.RandomChip:
            {
                var candidates = new List<Action>();
                if (GameManager.AttackChipCount > 0) candidates.Add(GameManager.DecrementAttackChipCount);
                if (GameManager.DefenseChipCount > 0) candidates.Add(GameManager.DecrementDefenseChipCount);
                if (GameManager.KnowledgeChipCount > 0) candidates.Add(GameManager.DecrementKnowledgeChipCount);
                if (candidates.Count > 0)
                {
                    candidates[GameManager.EventRewardRandom.Next(candidates.Count)]();
                }
                break;
            }
        }
    }

    // 返回值为该奖励产生的玩家可见结果文本（动态文本）；返回 null 表示无需单独播报（沿用选项的静态 ResultText，
    // 或该奖励本身不需要文本提示，例如金币/粮草变化已在地图等界面其它位置实时可见）。
    private static string? ApplyReward(EventReward reward)
    {
        switch (reward.Type)
        {
            case EventRewardType.Gold:
                if (reward.Amount >= 0)
                    RewardManager.Execute(new RewardSequence().Add(new AddGoldRewardAction(reward.Amount)));
                else
                    RewardManager.Execute(new RewardSequence().Add(new LoseGoldRewardAction(-reward.Amount)));
                return null;
            case EventRewardType.RandomGold:
            {
                var min = System.Math.Min(reward.Amount, reward.MaxAmount);
                var max = System.Math.Max(reward.Amount, reward.MaxAmount);
                var amount = GameManager.EventRewardRandom.Next(min, max + 1);
                RewardManager.Execute(new RewardSequence().Add(new AddGoldRewardAction(amount)));
                return Localization.GetFmt("event.modification_shop.option2.result_fmt", amount);
            }
            case EventRewardType.Forage:
                if (reward.Amount >= 0)
                    RewardManager.Execute(new RewardSequence().Add(new AddFoodRewardAction(reward.Amount)));
                else
                    RewardManager.Execute(new RewardSequence().Add(new LoseFoodRewardAction(-reward.Amount)));
                return null;
            case EventRewardType.Heal:
                if (reward.Amount >= 0)
                    RewardManager.Execute(new RewardSequence().Add(new HealRewardAction(reward.Amount)));
                else
                    RewardManager.Execute(new RewardSequence().Add(new LoseCurrentHpRewardAction(-reward.Amount)));
                return null;
            case EventRewardType.TempHp:
                GameManager.AddTempHp(reward.Amount);
                return $"获得 {reward.Amount} 点临时生命值（当前：{GameManager.CurrentHP + GameManager.TempHp}/{GameManager.MaxHP}）。";
            case EventRewardType.MaxHp:
                if (reward.Amount >= 0)
                    RewardManager.Execute(new RewardSequence().Add(new AddMaxHpRewardAction(reward.Amount)));
                else
                    RewardManager.Execute(new RewardSequence().Add(new LoseMaxHpRewardAction(-reward.Amount)));
                // 负数变化不产生提示文本（配合血祭等"消耗"类奖励使用）。
                return reward.Amount > 0 ? $"你获得了{reward.Amount}点最大生命值。" : null;
            case EventRewardType.Skill:
                RewardManager.Execute(new RewardSequence().Add(new AddSkillRewardAction(reward.StringValue)));
                return null;
            case EventRewardType.Equipment:
                RewardManager.Execute(new RewardSequence().Add(new AddEquipmentRewardAction(reward.StringValue)));
                return null;
            case EventRewardType.StinkyMushroomEquipmentForOption:
            {
                var stinkyEquipId = GameManager.GetStinkyMushroomColorForOption(reward.Amount);
                RewardManager.Execute(new RewardSequence().Add(new AddEquipmentRewardAction(stinkyEquipId)));
                var stinkyEquipment = EquipmentDatabase.GetEquipment(stinkyEquipId);
                return stinkyEquipment is null ? null : $"获得了【{Localization.GetName(stinkyEquipment)}】。";
            }
            case EventRewardType.DeferredEquipmentAfterNextBattle:
                GameManager.SetPendingNextBattleEquipmentReward(reward.StringValue);
                return "你将在下一场胜利后获得一件随机装备。";
            case EventRewardType.DeferredEquipmentAfterChapterBoss:
                if (GameManager.IsChapterBossDefeated(reward.Amount))
                {
                    GameManager.AddEquipment(reward.StringValue);
                    var equipment = EquipmentDatabase.GetEquipment(reward.StringValue);
                    return $"你获得了老者赠予的{(equipment is null ? reward.StringValue : Localization.GetName(equipment))}。";
                }

                GameManager.SetPendingChapterBossEquipmentReward(reward.Amount, reward.StringValue);
                return $"你将在击败{GameManager.GetChapterDisplayName(reward.Amount)}Boss后获得装备。";
            case EventRewardType.RandomEquipmentFromPool:
            {
                // ======================================================
                // 固定候选池随机
                // ======================================================
                // 即使事件写了明确的装备 ID 池，只要最终是“随机抽取”，仍必须遵守
                // CanAppearInRandomPool。这样剧情装备可以通过事件固定给予，但不会被
                // 任何随机来源绕过设计限制。
                var ids = reward.StringValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ids.Length == 0)
                {
                    return null;
                }

                var candidates = new List<EquipmentDefinition>();
                foreach (var id in ids)
                {
                    var candidate = EquipmentDatabase.GetEquipment(id);
                    if (candidate != null && candidate.CanAppearInRandomPool)
                    {
                        candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                {
                    return null;
                }

                var definition = candidates[Random.Next(candidates.Count)];
                RewardManager.Execute(new RewardSequence().Add(new AddEquipmentRewardAction(definition.Id)));
                var displayName = Localization.GetName(definition);
                if (!string.IsNullOrEmpty(reward.ResultTextTemplate))
                {
                    return reward.ResultTextTemplate.Replace("{name}", displayName, StringComparison.Ordinal);
                }

                return $"你获得了：【{displayName}】";
            }
            case EventRewardType.RandomEquipmentByRarity:
            {
                // 按品质随机装备统一走 RewardManager.CanAppearInRandomEquipmentReward，
                // 避免每个事件各自维护剧情装备、隐藏装备、专属装备的排除列表。
                if (!Enum.TryParse<EquipmentRarity>(reward.StringValue, true, out var rarity))
                {
                    return null;
                }

                var candidates = new List<EquipmentDefinition>();
                foreach (var definition in EquipmentDatabase.GetAllEquipments())
                {
                    if (definition.Rarity != rarity) continue;
                    if (!RewardManager.CanAppearInRandomEquipmentReward(definition)) continue;
                    candidates.Add(definition);
                }

                if (candidates.Count == 0)
                {
                    return $"没有可获得的{FormatEquipmentRarity(rarity)}装备。";
                }

                var chosen = candidates[Random.Next(candidates.Count)];
                RewardManager.Execute(new RewardSequence().Add(new AddEquipmentRewardAction(chosen.Id)));
                var chosenName = Localization.GetName(chosen);
                if (!string.IsNullOrEmpty(reward.ResultTextTemplate))
                {
                    return reward.ResultTextTemplate.Replace("{name}", chosenName, StringComparison.Ordinal);
                }

                return $"你获得了：【{chosenName}】";
            }
            // ————————————————————————————————————————
            // 石碑 / 芯片系统
            // ————————————————————————————————————————
            case EventRewardType.AttackTrickDamageBonus:
                GameManager.AddSteleAttackTrickDamageBonus(reward.Amount);
                return $"攻击性锦囊牌伤害 +{reward.Amount}（本局永久，当前总加成 +{GameManager.AttackTrickDamageBonus}）。";

            case EventRewardType.RunKillDamageBonus:
                GameManager.AddRunKillDamageBonus(reward.Amount);
                return $"所有杀系伤害 +{reward.Amount}（本局永久，当前总加成 +{GameManager.RunKillDamageBonus}）。";

            case EventRewardType.MaxHpPercentWithCurrentSync:
            {
                var bonus = GameManager.AddMaxHpByPercentWithCurrentSync(reward.Amount);
                return $"生命之泉生效：最大生命提升{reward.Amount}%，并同步恢复{bonus}点生命。";
            }

            case EventRewardType.SteleBloodPriceRandom:
            {
                // 50%：最大生命 +20 / 当前生命 +20；50%：所有杀系伤害 +5。
                var prefix = "石碑涌出了黑色液体。\n它包裹了你的全身。\n你似乎失去了什么。又似乎获得了什么。\n\n";
                if (Random.Next(2) == 0)
                {
                    GameManager.AddMaxHp(20);
                    GameManager.AddCurrentHp(20);
                    return prefix + "石碑赐予了生命之力：最大生命 +20，当前生命 +20。";
                }
                else
                {
                    GameManager.AddSteleKillDamageBonus(5);
                    return prefix + $"石碑赋予了战斗之力：所有杀系伤害 +5（本局永久，当前总加成 +{GameManager.RunKillDamageBonus}）。";
                }
            }

            case EventRewardType.DefenseChipCount:
                RewardManager.Execute(new RewardSequence().Add(new AddChipRewardAction(RewardChipType.Defense)));
                return $"防御芯片安装完毕（共 {GameManager.DefenseChipCount} 个）。";

            case EventRewardType.AttackChipInstall:
                RewardManager.Execute(new RewardSequence().Add(new AddChipRewardAction(RewardChipType.Attack)));
                return $"攻击芯片安装完毕（共 {GameManager.AttackChipCount} 个，杀系伤害总加成 +{GameManager.RunKillDamageBonus}）。";

            case EventRewardType.KnowledgeChipInstall:
                RewardManager.Execute(new RewardSequence().Add(new AddChipRewardAction(RewardChipType.Knowledge)));
                return $"知识芯片安装完毕（共 {GameManager.KnowledgeChipCount} 个，锦囊伤害总加成 +{GameManager.AttackTrickDamageBonus}）。";

            case EventRewardType.FutureRustWeaponUpgrade:
            {
                var upgradedNames = InventoryManager.UpgradeRustWeapons();
                if (upgradedNames.Count == 0)
                {
                    return null;
                }

                return "你的装备在石碑上不断摩擦。\n锈迹逐渐脱落。\n它们似乎恢复了原本的样子。\n\n" +
                       string.Join("、", upgradedNames.ConvertAll(n => $"【{n}】"));
            }

            case EventRewardType.SetChapterRoute:
                GameManager.SetChapterRoute((ChapterRoute)reward.Amount);
                return null;

            case EventRewardType.RemovePlayerCardType:
                if (System.Enum.TryParse<CardType>(reward.StringValue, out var removeCardType))
                    GameManager.RemovePlayerCardType(removeCardType);
                return null;

            case EventRewardType.AddPlayerCardType:
                if (System.Enum.TryParse<CardType>(reward.StringValue, out var addCardType))
                    GameManager.AddPlayerCardType(addCardType);
                return null;

            case EventRewardType.ReplaceCardType:
                if (System.Enum.TryParse<CardType>(reward.StringValue, out var replaceOldType)
                    && System.Enum.TryParse<CardType>(reward.SecondaryStringValue, out var replaceNewType))
                    GameManager.ReplacePlayerCardType(replaceOldType, replaceNewType);
                return null;

            case EventRewardType.StartTreasurePavilionBattle:
                GameManager.BeginTreasurePavilionBattle();
                return null;

            case EventRewardType.ChooseOneEpicEquipment:
                return null;

            case EventRewardType.RunBuff:
            {
                var result = RewardManager.Execute(new RewardSequence().Add(new AddRunBuffRewardAction(
                    reward.StringValue,
                    1,
                    reward.Amount > 0 ? reward.Amount : null)));
                return result.PlayerMessage;
            }

            case EventRewardType.MaxPowerBonus:
                GameManager.AddMaxPowerBonus(reward.Amount);
                return $"最大电量永久+{reward.Amount}（当前电量：{GameManager.Power}/{GameManager.MaxPower}）。";

            case EventRewardType.FreeWineRevive:
                GameManager.GrantFreeWineRevive();
                return null;

            case EventRewardType.OpenBlackMarketShop:
                return null;

            case EventRewardType.TriggerEvent:
                return null;

            case EventRewardType.TriggerRandomChapterNonShopEvent:
                return null;

            case EventRewardType.OpenWitchShop:
                return null;

            case EventRewardType.HealFull:
            {
                var healed = GameManager.MaxHP - GameManager.CurrentHP;
                if (healed > 0)
                {
                    GameManager.AddCurrentHp(healed);
                    return $"回复至满血（+{healed}点生命）。";
                }

                return "生命已满，无需恢复。";
            }

            case EventRewardType.NanmanDamageMultiply:
                GameManager.AddNanmanDamageBonus(reward.Amount);
                return $"你的南蛮入侵伤害提升为原来的{GameManager.NanmanDamageMultiplier:0.##}倍（本局永久）。";

            case EventRewardType.EvolveManzuToManzuWang:
                GameManager.RemoveAcquiredSkill(SkillIds.Manzu);
                GameManager.AddAcquiredSkill(SkillIds.ManzuWang);
                return "蛮族技能进化：【蛮族】→【蛮族之王】。\n南蛮入侵伤害 ×2，每次命中敌人回复5生命和0.5费。";

            case EventRewardType.StartManZuCampBattle:
                GameManager.BeginManZuCampBattle();
                return null;

            case EventRewardType.ManZuCampTheft:
                if (Random.Next(2) == 0)
                {
                    GameManager.AddGold(200);
                    return "你成功偷走了蛮族的物资，获得200金币。";
                }

                GameManager.BeginManZuCampBattle();
                return "你被发现了！蛮族士兵将你包围——决斗开始！";

            case EventRewardType.DestroyAllEquipmentAndGold:
            {
                var names = InventoryManager.DestroyAllEquipment();
                var goldLost = GameManager.Gold;
                GameManager.AddGold(-goldLost);
                AncientForgeEventLog.Log($"[古蜀铸炉] 献祭装备：销毁 {names.Count} 件装备，清空金币 {goldLost}。");
                var nameStr = names.Count > 0 ? string.Join("、", names.ConvertAll(n => $"【{n}】")) : "（无装备）";
                return $"古炉的火焰将一切吞噬。\n焚毁：{nameStr}\n金币全部耗尽。";
            }

            case EventRewardType.LoseCurrentHpPercent:
            {
                var loss = GameManager.CurrentHP * reward.Amount / 100;
                loss = Math.Max(1, loss);
                loss = Math.Min(loss, GameManager.CurrentHP - 1);
                if (loss <= 0) return "你的生命值已岌岌可危，古炉无法汲取更多力量。";
                GameManager.AddCurrentHp(-loss);
                AncientForgeEventLog.Log($"[古蜀铸炉] 献祭灵魂：失去 {loss} 点生命（{reward.Amount}% 当前HP）。");
                return $"古炉汲取了你的生命精华。\n你失去了 {loss} 点生命。";
            }

            case EventRewardType.SetCurrentHpToOneAndLoseMaxHpPercent:
            {
                var maxHpLoss = Math.Max(1, GameManager.MaxHP * reward.Amount / 100);
                maxHpLoss = Math.Min(maxHpLoss, GameManager.MaxHP - 1);
                var previousMaxHp = GameManager.MaxHP;

                if (maxHpLoss > 0)
                {
                    GameManager.AddMaxHp(-maxHpLoss);
                }

                // 这是事件明确指定的状态，而不是一次可被回复/减伤效果改写的伤害。
                GameManager.SetCurrentHp(1);
                AncientForgeEventLog.Log(
                    $"[古蜀铸炉] 献祭灵魂：当前生命设为1，最大生命 {previousMaxHp}->{GameManager.MaxHP}（-{reward.Amount}%）。");
                return $"古炉吞没了你的灵魂。\n当前生命降至1点，最大生命降低 {maxHpLoss} 点。";
            }

            case EventRewardType.ChooseOneLegendaryEquipment:
                return null;

            case EventRewardType.ChooseOneLegendaryWeaponEquipment:
                return null;

            case EventRewardType.ChooseOneRareOrEpicEquipment:
                return null;

            case EventRewardType.TransformEquipment:
            {
                var result = new TransformEquipmentRewardAction(
                    reward.StringValue,
                    reward.SecondaryStringValue).Execute();
                return result.PlayerMessage;
            }

            case EventRewardType.RemoveSpecificEquipment:
            {
                var owned = InventoryManager.GetAllOwned();
                Guid? targetId = null;
                foreach (var item in owned)
                {
                    if (item.Definition.Id == reward.StringValue)
                    {
                        targetId = item.InstanceId;
                        break;
                    }
                }
                if (targetId.HasValue)
                {
                    InventoryManager.DestroyEquipment(targetId.Value);
                    AncientForgeEventLog.Log($"[古蜀铸炉] 移除装备：{reward.StringValue}。");
                }
                return null;
            }

            case EventRewardType.RemoveAllChips:
            {
                var total = GameManager.ClearAllChips();
                return total > 0
                    ? $"回收了 {total} 个记忆芯片（攻击/防御/知识计数已清零）。"
                    : "芯片栏为空，无芯片可回收。";
            }

            case EventRewardType.RandomRareSkill:
            {
                var skillId = GameManager.GetRandomRareSkillId();
                if (skillId == null) return "没有可获得的稀有技能。";
                GameManager.AddAcquiredSkill(skillId);
                var skill = SkillDatabase.GetSkill(skillId);
                return $"获得技能：【{(skill is null ? skillId : Localization.GetName(skill))}】";
            }

            case EventRewardType.ChooseOneChipTwice:
                return null;
            case EventRewardType.ChooseOneChipOnce:
                return null;

            case EventRewardType.EvolveKejiToKejiUnlimited:
                GameManager.AddAcquiredSkill(SkillIds.KejiUnlimited);
                return "克己升级：【克己·无限制协议】已激活。\n费用上限→20，每1费+10%伤害，免疫顺手牵羊。";

            case EventRewardType.StartQiXingTanBattle:
                GameManager.BeginQiXingTanBattle();
                return null;

            case EventRewardType.EvolveGuanxingToTianJi:
                GameManager.RemoveAcquiredSkill(SkillIds.Guanxing);
                GameManager.AddAcquiredSkill(SkillIds.TianJi);
                return "观星升级：【天机】已激活。\n使用观星时立即获得1费（仍正常扣费）。";

            case EventRewardType.SacrificeOneEquipmentForUpgrade:
                return null;

            case EventRewardType.RatKingOfferGift:
                return ResolveRatKingOfferGift();

            case EventRewardType.StartRatKingEventBattle:
                GameManager.BeginRatKingEventBattle();
                return null;

            case EventRewardType.RandomEpicAccessory:
            {
                var candidates = new List<EquipmentDefinition>();
                foreach (var def in EquipmentDatabase.GetAllEquipments())
                {
                    if (def.Rarity != EquipmentRarity.Epic) continue;
                    var isAccessory = false;
                    foreach (var t in def.Types)
                    {
                        if (t == EquipmentType.Accessory) { isAccessory = true; break; }
                    }
                    if (!isAccessory) continue;
                    candidates.Add(def);
                }
                if (candidates.Count == 0) return "没有可获得的史诗饰品。";
                var chosen = candidates[Random.Next(candidates.Count)];
                GameManager.AddEquipment(chosen.Id);
                return $"获得史诗饰品：【{Localization.GetName(chosen)}】。";
            }

            case EventRewardType.WineDamageMultiplierBonus:
                GameManager.AddWineDamageMultiplierBonus(0.5);
                return $"酒的增伤倍率永久增加0.5倍（当前额外 +{GameManager.WineDamageMultiplierBonus:0.#}）。";

            case EventRewardType.SoulPossession:
                return GameManager.ApplySoulPossession();

            case EventRewardType.AncestorBlessing:
                RunBuffManager.AddStacks(RunBuffIds.AncestorBlessing, 3);
                return null;

            case EventRewardType.ChooseElfElementBlessing:
            case EventRewardType.ElfEquipmentReforge:
            case EventRewardType.ModificationShopWeaponReforge:
                return null;

            case EventRewardType.PeachBaseHealBonus:
                GameManager.AddPeachBaseHealBonus(reward.Amount);
                return $"桃基础回复量永久 +{reward.Amount}（当前基础回复 {GameManager.PeachBaseHealAmount}）。";

            case EventRewardType.StartChasingPursuersBattle:
                GameManager.BeginChasingPursuersBattle();
                return null;

            case EventRewardType.ChasingPursuersDiversion:
                if (Random.Next(2) == 0)
                {
                    GameManager.AddGold(50);
                    return "你巧妙地制造了混乱，成功甩开了追捕者的视线。\n保留了全部遗物，获得50金币。";
                }
                else
                {
                    RemoveEquipmentIfOwned(EquipmentIds.ForgottenStone);
                    RemoveEquipmentIfOwned(EquipmentIds.StatueCore);
                    GameManager.AddCurrentHp(-15);
                    return "调虎离山的计策被识破了。\n追捕者夺回了属于他们的遗物，你也在混战中受了伤（-15点生命）。";
                }

            case EventRewardType.PowerGain:
                GameManager.AddPower(reward.Amount);
                return null;

            case EventRewardType.ExplosiveFruitTouchDamage:
                GameManager.ApplyExplosiveFruitTouchDamage(reward.Amount);
                return null;

            case EventRewardType.ExplosiveFruitAccessorySlot:
                GameManager.GrantExplosiveFruitAccessorySlot();
                return null;

            case EventRewardType.ExplosiveFruitArrowBarrageFireUpgrade:
                GameManager.GrantArrowBarrageFireUpgrade();
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// 结算鼠王的装备献礼。
    ///
    /// 必须先确定高一品质候选，再执行摧毁和发放，避免候选池为空时玩家永久丢失装备。
    /// </summary>
    internal static string ResolveRatKingOfferGift()
    {
        var sacrificeCandidates = new List<OwnedEquipment>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            // 传奇没有更高品质，不能作为“高一品质”献礼目标。
            if (item.Definition.Rarity >= EquipmentRarity.Rare
                && item.Definition.Rarity < EquipmentRarity.Legendary)
            {
                sacrificeCandidates.Add(item);
            }
        }

        if (sacrificeCandidates.Count == 0)
        {
            return "你的背包中没有可以提升品质的稀有或史诗装备。\n鼠王略显失望，但还是将金币收下了。";
        }

        // 随机尝试所有可献礼装备。某一件没有升级候选时继续检查其它装备，
        // 避免随机到单个死路后错误宣告整个奖励池为空。
        while (sacrificeCandidates.Count > 0)
        {
            var sacrificeIndex = Random.Next(sacrificeCandidates.Count);
            var sacrificed = sacrificeCandidates[sacrificeIndex];
            sacrificeCandidates.RemoveAt(sacrificeIndex);

            var nextRarity = sacrificed.Definition.Rarity == EquipmentRarity.Rare
                ? EquipmentRarity.Epic
                : EquipmentRarity.Legendary;
            var upgradeCandidates = RewardManager.GetEquipmentUpgradeCandidates(sacrificed.Definition);

            if (upgradeCandidates.Count == 0)
            {
                continue;
            }

            var upgraded = upgradeCandidates[Random.Next(upgradeCandidates.Count)];
            var added = GameManager.AddEquipment(upgraded.Id, EquipmentGainSource.EventReward);
            if (added == null)
            {
                continue;
            }

            if (!InventoryManager.DestroyEquipment(sacrificed.InstanceId))
            {
                // 原装备移除失败时回滚新装备，避免一次献礼复制出额外装备。
                InventoryManager.DestroyEquipment(added.InstanceId);
                continue;
            }

            var sacrificedName = Localization.GetName(sacrificed.Definition);
            var upgradedName = Localization.GetName(added.Definition);
            var rarityName = Localization.GetRarityName(nextRarity);
            return $"鼠王满意地接受了你的礼物。\n【{sacrificedName}】被摧毁。\n获得{rarityName}装备：【{upgradedName}】。";
        }

        return "没有可获得的高一品质装备。\n原装备未被摧毁。";
    }

    private static string FormatEquipmentRarity(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);

    // 若玩家当前持有（背包或已装备）指定 Id 的装备，则销毁该实例；未持有时安全地不做任何事。
    // 与 EventRewardType.RemoveSpecificEquipment 使用同样的查找/销毁方式。
    private static void RemoveEquipmentIfOwned(string equipmentId)
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Id == equipmentId)
            {
                InventoryManager.DestroyEquipment(item.InstanceId);
                return;
            }
        }
    }

    /// <summary>
    /// Event System 的公开入口：GetRandomNonShopEventForChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData? GetRandomNonShopEventForChapter(int chapter)
    {
        var candidates = new List<EventData>();
        foreach (var eventData in GetAllEvents())
        {
            if (eventData.Category is EventCategory.Shop or EventCategory.Special)
            {
                continue;
            }

            var chapterMatched = false;
            foreach (var condition in eventData.Conditions)
            {
                if (condition.Type == EventConditionType.CurrentChapter
                    && condition.Comparison == EventComparison.Equal
                    && condition.IntValue == chapter
                    && !condition.Negate)
                {
                    chapterMatched = true;
                    break;
                }
            }

            if (!chapterMatched)
            {
                continue;
            }

            candidates.Add(eventData);
        }

        return PickWeightedEvent(candidates);
    }
}
