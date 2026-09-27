//////////////////////////////////////////////////////////
// 文件：Scripts/GameManager.cs
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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core System 的公开枚举：RunState。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum RunState
{
    MainMenu,
    CharacterSelect,
    Map,
    Battle,
    Victory,
    GameOver
}

/// <summary>
/// Core System 的公开类：GameManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class GameManager
{
    public const int TotalLevels = 8;
    public const int TotalChapters = 4;
    public const int InitialForage = 2;
    public const int InitialGold = 50;
    // 电量（Power）：地图探索资源，每章开始时重置为满，只影响地图节点进入
    // （战斗内不消耗/恢复，Boss节点不消耗）。
    public const int InitialMaxPower = 100;
    public const int EnemyInitialHealth = 40;
    public const int InitialMana = BattleConstants.InitialMana;
    public const int BattleEndHealthRecovery = 10;

    private static readonly List<string> AcquiredSkillIds = new();
    private static readonly HashSet<string> SoulPossessionSkillIds = new();
    private static readonly Dictionary<string, int> EquipmentUseCounts = new();
    private static readonly HashSet<int> UnlockedStages = new();
    private static readonly HashSet<int> ClearedStages = new();
    private static readonly HashSet<string> SeenRunEventIds = new();
    internal static readonly HashSet<string> ClearedNodeIds = new();
    internal static readonly HashSet<string> UnlockedNodeIds = new();
    private static readonly HashSet<string> ClaimedRewardNodeIds = new();
    internal static readonly List<MapNode> MapNodes = new();
    private static readonly HashSet<int> DefeatedChapterBosses = new();
    private static readonly Dictionary<int, List<string>> PlannedChapterBossEncounterEnemyIds = new();
    private static readonly Dictionary<int, string> DefeatedChapterBossEnemyIds = new();
    internal static readonly Random EventRewardRandom = new();
    private static string _pendingNextBattleEquipmentRarityPool = string.Empty;
    private static int _pendingChapterBossEquipmentChapter;
    private static string _pendingChapterBossEquipmentId = string.Empty;
    // 当前战斗结束后待发放的随机装备掉落（概率已在 BattleManager 结算）。
    private static readonly List<string> PendingBattleDropEquipmentIds = new();
    private static readonly List<string> ActiveSpecialBattleEnemyIds = new();
    // 当前战斗结束后待发放的金币（来自各敌方按 EnemyRewardConfig 统一区间随机之和）。
    private static int _pendingBattleGold;
    private static string _activeSpecialBattleId = string.Empty;
    private static double _activeSpecialBattleEnemyMaxHpMultiplier = 1.0;
    private static double _activeSpecialBattleEnemyFinalDamageMultiplier = 1.0;
    // 鼠王事件特殊战斗：和平解决桃计数器
    private static int _ratKingEventPeachCount;
    private static bool _ratKingEventPeacefulResolved;
    // ————————————————————————————————————————
    // 芯片系统（改造铺）+ 本局永久伤害加成（石碑 / 芯片叠加，不互相覆盖）
    // ————————————————————————————————————————
    private static int _defenseChipCount;
    private static int _attackChipCount;
    private static int _knowledgeChipCount;
    private static int _expansionChipCount;
    // 恶臭蘑菇事件：红/黄/绿三色装备到三个选项的随机映射，本局内首次访问时用 Fisher-Yates 洗牌一次并缓存，
    // 保证同一局内多次打开事件面板/多次调用奖励结算时颜色分配保持一致。
    private static List<string>? _stinkyMushroomColorAssignment;
    // 战斗开始前的状态快照（用于战败后重试恢复）。
    private static int _preBattleHP;
    private static double _preBattleMana;
    // 玩家本局被移除或新增的牌型（事件触发）。
    private static readonly HashSet<CardType> _removedPlayerCardTypes = new();
    private static readonly HashSet<CardType> _addedPlayerCardTypes = new();
    // 来自石碑选项一的攻击锦囊固定加伤（叠加，非替换）。
    private static int _steleAttackTrickDamageBonus;
    // 来自石碑选项三血祭的杀系固定加伤（叠加，非替换）。
    private static int _steleKillDamageBonus;
    // 来自生命之泉”淬炼刀刃”的本局永久杀系固定加伤（叠加，非替换）。
    private static int _lifeSpringKillDamageBonus;
    // 张飞咆哮：每场胜利积累的本局永久杀系加伤（每胜+5，跨战斗保留，新局重置）。
    private static int _paoxiaoPlayerKillBonus;
    // 周泰【奋激】：每次进入 OnDying 结算积累的本局永久攻击性锦囊加伤（每次+2，跨战斗保留，新局重置）。
    private static int _zhouTaiFenjiBonus;
    private static double _wineDamageMultiplierBonus;
    private static int _peachBaseHealBonus;
    private static bool _defaultCharacterSkillsRemovedBySoulPossession;
    // 爆炸果实①"碰一下"：是否已获得第4个"普通饰品槽"（EquipmentSlot.Accessory5，
    // 仅允许普通品质饰品，与吴·多宝架的 Accessory4 相互独立）。
    private static bool _hasExplosiveFruitAccessorySlot;
    // 爆炸果实②"远处射箭激发果实"：万箭齐发本局是否已被永久强化为火属性伤害。
    private static bool _arrowBarrageFireUpgraded;
    // 【元素祭坛】本局为全部玩家攻击牌追加的元素词条（可同时拥有火、雷）。
    private static AttackAttribute _playerAttackAttributeBonus = AttackAttribute.None;
    // 【元素祭坛】消耗月亮宝石后，天体撞击拥有四种元素词条。
    private static bool _moonGemElementallyAwakened;
    // 招募援军事件：南蛮入侵本局永久伤害加成百分比（0=无加成，50=×1.5，100=×2等）。
    private static int _nanmanBonusPercent;
    // 每章最多出现一次的事件追踪：”eventId_chN” → 本章是否已触发。
    private static readonly HashSet<string> SeenEventsByChapter = new();
    private static ChapterRoute _chapterRoute = ChapterRoute.Default;
    private static ChapterVariant _chapterVariant = ChapterVariant.None;
    private static bool _curseBladeCurrentCurseAmplified;
    private static bool _hasFreeWineRevive; // 酒馆·点杯酒：濒死自动饮酒时费用变为0（原为1费），本局永久生效
    // ————————————————————————————————————————
    // 第一章初始事件持久效果
    // ————————————————————————————————————————
    private static bool _initialEventCh2SkillPick;            // ①效果：第二章开始时技能三选一
    private static bool _initialEventNoBattleEndRecovery;     // ①代价：战斗结束不回血
    private static double _initialEventStartingManaBonus;     // ㉔：每场战斗初始费用加成
    private static bool _initialEventAutoDestroyEquip;        // ②：自动销毁装备开启
    private static int _initialEventAutoDestroyCount;         // ②：已销毁装备数
    private static bool _initialEventAutoDestroyRewardPending; // ②：传奇二选一待领取
    private static bool _initialEventLegendaryEnemyHpBonus;   // ③代价：传奇敌人+30%HP
    private static bool _initialEventCh1BossSkillPick;        // ③效果：击败第一章Boss后随机Boss技能
    private static bool _initialEventShopRefreshFree;         // ⑦效果：商店刷新永久免费
    private static bool _initialEventAllShopsBlackMarket;     // ⑦效果：所有商店使用黑市池且无溢价
    private static bool _initialEventChipChoiceEnhanced;      // ④效果：神秘芯片5%，扩容/技能芯片各10%
    private static int _initialEventBattleEndMaxHpGain;       // ⑨效果：每场战斗胜利后+N最大HP
    private static bool _initialEventCh1BossExpansionChip;    // ⑪效果：第一章Boss奖励→扩容芯片
    private static int _initialEventMaxPowerBonus;            // ⑫效果：最大电量永久+25
    private static int _maxPowerBonus;                        // 普通事件（如废弃实验室④回收电池）：最大电量永久加成
    private static int _factionFateMaxPowerBonus;              // 阵营命运（如魏·备用电池）：最大电量永久加成，独立字段避免与其它来源混用
    private static int _initialEventExtraShopRefreshCount;    // ⑧效果：每商店额外免费刷新次数
    private static bool _initialEventBattleEndFullHeal;       // ⑰：战斗结束满血回复
    // 战场崩坏（55回合后环境伤害，详见 BattlefieldCollapseEffect）胜利后的特殊
    // 战后血量处理：只要这场战斗触发过战场崩坏，战斗结束后就不再走正常的+10/满血
    // 等既有回复规则，改用战斗结束瞬间的真实血量决定——
    //   · 普通胜利时玩家必然仍有正生命，直接按战斗结束生命退出，不额外回复。
    //   · 同批次崩坏导致双方死亡并触发互伤胜利时，保留既有最低1HP结算规则。
    // 由 BattleManager.Selection.cs 的 CheckBattleOver() 在判定胜利、且当前回合数
    // 达到 BattlefieldCollapseEffect.TriggerTurn 时，把战斗结束瞬间的真实血量存进来。
    private static int? _pendingCollapseVictoryHpOverride;
    private static bool _initialEventNoDamageTrackActive;     // ⑭：0伤害追踪激活
    private static int _initialEventNoDamageBattleCount;      // ⑭：已完成战斗数
    private static bool _initialEventNoDamageFailed;          // ⑭：已失效
    private static bool _initialEventNoDamageRewardPending;   // ⑭：史诗三选一待领取
    private static bool _initialEventFirstBattleElite;        // ⑯：第一战精英替换
    private static bool _initialEventCh1Event2Ch2;            // ⑯：event_2替换为第二章事件
    private static bool _initialEventIe06RarePending;         // ⑥：稀有装备三选一待领取
    private static bool _initialEventUpgradeFirstEquipSelected;  // ⑱：玩家是否选择了品质跃迁
    private static bool _initialEventUpgradeFirstEquipArmed;     // ⑱：正式进入1-1后才允许监听装备奖励
    private static bool _initialEventUpgradeFirstEquipTriggered; // ⑱：本Run是否已经成功替换过一次
    private static bool _initialEventFortuneContractPending;  // ㉑财富契约：三件专属装备三选一待领取
    private static bool _initialEventAttackTrickChoicePending; // ㉒：特殊锦囊三选一待领取

    public static string CurrentCharacterId { get; private set; } = string.Empty;
    public static int CurrentChapter { get; private set; } = 1;
    public static int CurrentStage { get; private set; } = -1;
    public static string CurrentNodeId { get; private set; } = string.Empty;
    public static int Forage { get; private set; }
    public static int Gold { get; private set; }
    public static int Power { get; private set; }
    // 电量上限 = 基础值100 + 初始事件永久加成（如【最大电量+25】）+ 载具等装备加成
    // （框架预留，目前没有任何装备提供加成）。三者都是"本局永久"的，不随章节切换重置——
    // 每章开始只是把 Power 重新填满到这个（可能已提高的）上限，上限本身不会掉回100。
    public static int MaxPower => InitialMaxPower + _initialEventMaxPowerBonus + _maxPowerBonus + GetVehiclePowerBonus() + _factionFateMaxPowerBonus;
    // 上一次进入节点前的电量快照，供地图HUD返回地图屏幕时对比、播放电量增/减脉冲动画
    // （地图屏幕本身展示期间电量不会变化，只能靠前后对比触发）。
    public static int PowerBeforeLastNode { get; set; }
    // 当前战斗的回合数快照，与 BattleContext.TurnCounter 保持同步（BattleManager 每次推进
    // 回合时一并写入）。存在这里是因为 Player.TakeDamage 没有 BattleContext 参数，但
    // 遗忘之石这类"按战斗回合数生效"的效果需要在那里统一拦截所有伤害来源（含毒素/反伤/
    // 自身技能伤害等不经过 DamageEvent 触发链的直接 TakeDamage 调用），不能只靠
    // OnBeforeDamage 触发器覆盖。
    public static int CurrentBattleTurnNumber { get; set; } = 1;
    public static int CurrentHP { get; private set; }
    public static int MaxHP { get; private set; }
    // 临时生命值：可超过 MaxHP，不修改最大生命上限，战斗结束后自动清除。
    public static int TempHp { get; private set; }
    public static double CurrentMana { get; private set; }
    public static int DefeatedEnemyCount { get; private set; }
    public static RunState CurrentRunState { get; private set; } = RunState.MainMenu;
    public static bool DebugMapEnabled { get; private set; }
    public static ChapterRoute CurrentChapterRoute => _chapterRoute;
    public static ChapterVariant CurrentChapterVariant => _chapterVariant;

    // 芯片计数（背包界面实时读取）。
    public static int DefenseChipCount => _defenseChipCount;
    public static int AttackChipCount => _attackChipCount;
    public static int KnowledgeChipCount => _knowledgeChipCount;
    public static int ExpansionChipCount => _expansionChipCount;
    // 爆炸果实：普通饰品槽（Accessory5）是否已获得；万箭齐发是否已被永久强化为火属性。
    public static bool HasExplosiveFruitAccessorySlot => _hasExplosiveFruitAccessorySlot;
    public static bool IsArrowBarrageFireUpgraded => _arrowBarrageFireUpgraded;
    public static AttackAttribute PlayerAttackAttributeBonus => _playerAttackAttributeBonus;
    public static bool IsMoonGemElementallyAwakened => _moonGemElementallyAwakened;

    // 本局累计攻击性锦囊（万箭/南蛮）伤害加成：知识芯片×5 + 石碑选项一 + 周泰【奋激】；全部叠加，在 BattleRules 应用。
    public static int AttackTrickDamageBonus =>
        _knowledgeChipCount * 5 * FactionFateManager.GetChipEffectMultiplier()
        + _steleAttackTrickDamageBonus
        + _zhouTaiFenjiBonus;
    // 本局累计杀系（普通杀/火杀/雷杀/必中杀）伤害加成：攻击芯片×3 + 石碑血祭；全部叠加，在 BattleRules 应用。
    public static int RunKillDamageBonus =>
        _attackChipCount * 3 * FactionFateManager.GetChipEffectMultiplier()
        + _steleKillDamageBonus
        + _lifeSpringKillDamageBonus;
    // 张飞咆哮：本局累计永久杀系加伤（每场胜利+5）。
    public static int PaoxiaoPlayerKillBonus => _paoxiaoPlayerKillBonus;
    // 周泰【奋激】：本局累计永久攻击性锦囊加伤（每次触发+2）。
    public static int ZhouTaiFenjiBonus => _zhouTaiFenjiBonus;
    public static double WineDamageMultiplierBonus => _wineDamageMultiplierBonus;
    public static int PeachBaseHealAmount => BattleConstants.PeachHeal + _peachBaseHealBonus;
    public static int CurrentPlayerPeachBaseHealAmount => PeachBaseHealAmount + 5 * CountEquipment(EquipmentIds.SproutingBonsai);
    // 招募援军：南蛮入侵本局永久伤害倍率（1.0=无加成，1.5=+50%等）。
    public static double NanmanDamageMultiplier => 1.0 + _nanmanBonusPercent / 100.0;

    public static CharacterData? CurrentCharacter =>
        string.IsNullOrWhiteSpace(CurrentCharacterId) ? null : CharacterDatabase.GetCharacter(CurrentCharacterId);

    public static IReadOnlyList<Skill> AcquiredSkills
    {
        get
        {
            var skills = new List<Skill>();
            foreach (var id in AcquiredSkillIds)
            {
                var skill = SkillDatabase.GetSkill(id);
                if (skill != null)
                {
                    skills.Add(skill);
                }
            }

            return skills;
        }
    }

    // 仅返回当前已装备（在装备槽中）的装备，不包含背包内未装备的装备。
    public static IReadOnlyList<EquipmentDefinition> Equipment => InventoryManager.GetEquippedDefinitions();

    // ————————————————————————————————————————
    // 第一章初始事件：公开属性与设置方法
    // ————————————————————————————————————————
    public static bool InitialEventShopRefreshFree => _initialEventShopRefreshFree;
    public static bool InitialEventAllShopsBlackMarket => _initialEventAllShopsBlackMarket;
    public static bool InitialEventChipChoiceEnhanced => _initialEventChipChoiceEnhanced;
    public const double InitialEventMysteriousChipChance = 0.05;
    public const double InitialEventEnhancedChipExpansionChance = 0.10;
    public const double InitialEventEnhancedSkillChipChance = 0.10;
    public static int InitialEventBattleEndMaxHpGain => _initialEventBattleEndMaxHpGain;
    public static bool InitialEventAutoDestroyEquip => _initialEventAutoDestroyEquip;
    public static int InitialEventAutoDestroyCount => _initialEventAutoDestroyCount;
    public static bool InitialEventAutoDestroyRewardPending
    {
        get => _initialEventAutoDestroyRewardPending;
        set => _initialEventAutoDestroyRewardPending = value;
    }
    public static bool InitialEventCh1BossExpansionChip => _initialEventCh1BossExpansionChip;
    public static bool InitialEventCh2SkillPick
    {
        get => _initialEventCh2SkillPick;
        set => _initialEventCh2SkillPick = value;
    }
    public static bool InitialEventCh1BossSkillPick
    {
        get => _initialEventCh1BossSkillPick;
        set => _initialEventCh1BossSkillPick = value;
    }
    public static int InitialEventExtraShopRefreshCount => _initialEventExtraShopRefreshCount;
    public static bool InitialEventLegendaryEnemyHpBonus => _initialEventLegendaryEnemyHpBonus;
    public static bool InitialEventBattleEndFullHeal => _initialEventBattleEndFullHeal;
    public static bool InitialEventNoDamageTrackActive => _initialEventNoDamageTrackActive;
    public static bool InitialEventNoDamageFailed => _initialEventNoDamageFailed;
    public static bool InitialEventNoDamageRewardPending
    {
        get => _initialEventNoDamageRewardPending;
        set => _initialEventNoDamageRewardPending = value;
    }
    public static bool InitialEventFirstBattleElite => _initialEventFirstBattleElite;
    public static bool InitialEventCh1Event2Ch2 => _initialEventCh1Event2Ch2;
    public static bool InitialEventIe06RarePending
    {
        get => _initialEventIe06RarePending;
        set => _initialEventIe06RarePending = value;
    }
    public static bool InitialEventFortuneContractPending
    {
        get => _initialEventFortuneContractPending;
        set => _initialEventFortuneContractPending = value;
    }
    public static bool InitialEventAttackTrickChoicePending
    {
        get => _initialEventAttackTrickChoicePending;
        set => _initialEventAttackTrickChoicePending = value;
    }
    public static double InitialEventStartingManaBonus => _initialEventStartingManaBonus;
    public static bool InitialEventUpgradeFirstEquip =>
        _initialEventUpgradeFirstEquipSelected && !_initialEventUpgradeFirstEquipTriggered;
    public static bool InitialEventUpgradeFirstEquipIsSelected => _initialEventUpgradeFirstEquipSelected;
    public static bool InitialEventUpgradeFirstEquipIsArmed => _initialEventUpgradeFirstEquipArmed;
    public static bool InitialEventUpgradeFirstEquipHasTriggered => _initialEventUpgradeFirstEquipTriggered;

    /// <summary>
    /// 品质跃迁成功时发布最终的原装备与替换装备。
    ///
    /// 事件只承载事实，不包含任何 UI 节点引用；MainFlow 可据此显示即时反馈，测试也可验证
    /// 触发次数。场景切换不会影响三个 Run 状态字段。
    /// </summary>
    public static event Action<EquipmentDefinition, EquipmentDefinition>? InitialEventQualityUpgradeTriggered;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventUpgradeFirstEquip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventUpgradeFirstEquip()
    {
        _initialEventUpgradeFirstEquipSelected = true;
        _initialEventUpgradeFirstEquipArmed = false;
        _initialEventUpgradeFirstEquipTriggered = false;
    }

    /// <summary>
    /// 玩家成功支付节点消耗并正式进入1-1时激活品质跃迁。
    ///
    /// 选择初始事件只记录意图，不立即监听背包变化；这个独立生命周期门禁确保角色初始装备、
    /// Run 初始化装备和初始事件前已有装备都不会消费效果。
    /// </summary>
    public static void ArmInitialEventUpgradeFirstEquip(string stageId)
    {
        if (_initialEventUpgradeFirstEquipSelected
            && !_initialEventUpgradeFirstEquipTriggered
            && stageId == "1-1")
        {
            _initialEventUpgradeFirstEquipArmed = true;
        }
    }

    private static bool CanTriggerInitialEventUpgrade(EquipmentGainSource source)
    {
        return source is EquipmentGainSource.GameplayReward
            or EquipmentGainSource.ShopPurchase
            or EquipmentGainSource.EventReward
            or EquipmentGainSource.BattleReward
            or EquipmentGainSource.BossReward
            or EquipmentGainSource.ChoiceReward
            or EquipmentGainSource.EnemyDrop;
    }

    private static EquipmentDefinition? FindInitialEventUpgradeCandidate(
        EquipmentDefinition original,
        EquipmentGainSource source)
    {
        if (!_initialEventUpgradeFirstEquipSelected
            || !_initialEventUpgradeFirstEquipArmed
            || _initialEventUpgradeFirstEquipTriggered
            || !CanTriggerInitialEventUpgrade(source))
        {
            return null;
        }

        var candidates = RewardManager.GetEquipmentUpgradeCandidates(original);

        return candidates.Count == 0
            ? null
            : candidates[EventRewardRandom.Next(candidates.Count)];
    }

    /// <summary>
    /// Core System 的公开入口：SetInitialEventNoBattleEndRecovery。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventNoBattleEndRecovery() => _initialEventNoBattleEndRecovery = true;

    /// <summary>
    /// 初始事件㉔：本局每场战斗的初始费用加成。此值由 GetPlayerInitialMana 统一读取，
    /// 因而不会误变成一次性当前费用，也不会在章节切换时丢失。
    /// </summary>
    public static void AddInitialEventStartingMana(double amount)
    {
        _initialEventStartingManaBonus += amount;
    }

    /// <summary>
    /// 是否已获得酒馆·点杯酒效果：濒死时自动饮酒救援不再消耗费用。
    /// </summary>
    public static bool HasFreeWineRevive => _hasFreeWineRevive;

    /// <summary>
    /// 授予酒馆·点杯酒效果：本局永久生效，濒死自动饮酒的费用变为0。
    /// </summary>
    public static void GrantFreeWineRevive() => _hasFreeWineRevive = true;

    /// <summary>
    /// Core System 的公开入口：SetInitialEventAutoDestroyEquip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventAutoDestroyEquip()
    {
        _initialEventAutoDestroyEquip = true;
        _initialEventAutoDestroyCount = 0;
    }
    /// <summary>
    /// Core System 的公开入口：IncrementInitialEventAutoDestroyCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementInitialEventAutoDestroyCount()
    {
        _initialEventAutoDestroyCount++;
        if (_initialEventAutoDestroyCount >= 5)
        {
            _initialEventAutoDestroyRewardPending = true;
            _initialEventAutoDestroyEquip = false;
        }
    }
    /// <summary>
    /// Core System 的公开入口：SetInitialEventLegendaryEnemyHpBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventLegendaryEnemyHpBonus() => _initialEventLegendaryEnemyHpBonus = true;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventShopRefreshFree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventShopRefreshFree() => _initialEventShopRefreshFree = true;
    public static void SetInitialEventAllShopsBlackMarket() => _initialEventAllShopsBlackMarket = true;
    public static void SetInitialEventChipChoiceEnhancement() => _initialEventChipChoiceEnhanced = true;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventBattleEndMaxHpGain。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventBattleEndMaxHpGain(int value) => _initialEventBattleEndMaxHpGain = value;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventCh1BossExpansionChip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventCh1BossExpansionChip() => _initialEventCh1BossExpansionChip = true;
    /// <summary>
    /// Core System 的公开入口：AddInitialEventMaxPowerBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddInitialEventMaxPowerBonus(int amount)
    {
        _initialEventMaxPowerBonus += amount;
        // 永久提高电量上限的同时，把当前电量也按相同幅度补满（而不是只提高上限、
        // 当前电量不变），保证选择该初始事件后立刻就是"新上限/新上限"。
        AddPower(amount);
    }
    /// <summary>
    /// Core System 的公开入口：AddMaxPowerBonus。
    ///
    /// 供普通事件（非初始抉择事件）使用的最大电量永久加成，语义与 AddInitialEventMaxPowerBonus
    /// 一致（本局永久生效，章节切换不重置），但状态字段独立，避免与初始事件专属效果混用。
    /// </summary>
    public static void AddMaxPowerBonus(int amount)
    {
        _maxPowerBonus += amount;
        AddPower(amount);
    }

    /// <summary>
    /// Core System 的公开入口：GrantFactionFateMaxPowerBonus。
    ///
    /// 供阵营命运（如魏·备用电池）使用的最大电量永久加成，语义与 AddMaxPowerBonus 一致
    /// （本局永久生效，章节切换不重置），状态字段独立，避免与普通事件/初始事件的加成混用。
    /// </summary>
    public static void GrantFactionFateMaxPowerBonus(int amount)
    {
        _factionFateMaxPowerBonus += amount;
        AddPower(amount);
    }
    /// <summary>
    /// Core System 的公开入口：AddInitialEventExtraShopRefresh。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddInitialEventExtraShopRefresh() => _initialEventExtraShopRefreshCount++;

    /// <summary>
    /// 战场崩坏胜利（含互伤胜利）专用：传入战斗结束瞬间玩家生命。
    /// 下一次 RecoverHealthAfterBattle() 使用该值且最低保留1HP，不叠加正常回复。
    /// </summary>
    public static void ArmCollapseVictoryHealthOverride(int battleEndHealth) =>
        _pendingCollapseVictoryHpOverride = System.Math.Max(1, battleEndHealth);

    /// <summary>
    /// Core System 的公开入口：SetInitialEventBattleEndFullHeal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventBattleEndFullHeal() => _initialEventBattleEndFullHeal = true;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventNoDamageTrack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventNoDamageTrack()
    {
        _initialEventNoDamageTrackActive = true;
        _initialEventNoDamageBattleCount = 0;
        _initialEventNoDamageFailed = false;
    }
    /// <summary>
    /// Core System 的公开入口：RecordInitialEventDamageTaken。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RecordInitialEventDamageTaken()
    {
        if (!_initialEventNoDamageTrackActive || _initialEventNoDamageFailed || _initialEventNoDamageBattleCount >= 3)
            return;
        _initialEventNoDamageFailed = true;
    }
    /// <summary>
    /// Core System 的公开入口：CheckInitialEventNoDamageBattleEnd。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void CheckInitialEventNoDamageBattleEnd()
    {
        if (!_initialEventNoDamageTrackActive || _initialEventNoDamageFailed) return;
        _initialEventNoDamageBattleCount++;
        if (_initialEventNoDamageBattleCount >= 3)
        {
            _initialEventNoDamageRewardPending = true;
            _initialEventNoDamageTrackActive = false;
        }
    }
    /// <summary>
    /// Core System 的公开入口：SetInitialEventFirstBattleElite。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventFirstBattleElite() => _initialEventFirstBattleElite = true;
    /// <summary>
    /// Core System 的公开入口：ConsumeInitialEventFirstBattleElite。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConsumeInitialEventFirstBattleElite() => _initialEventFirstBattleElite = false;
    /// <summary>
    /// Core System 的公开入口：SetInitialEventCh1Event2Ch2。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetInitialEventCh1Event2Ch2() => _initialEventCh1Event2Ch2 = true;
    /// <summary>
    /// Core System 的公开入口：ConsumeInitialEventCh1Event2Ch2。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConsumeInitialEventCh1Event2Ch2() => _initialEventCh1Event2Ch2 = false;

    // ②效果：从Boss技能中随机选一个玩家尚未拥有的技能。
    /// <summary>
    /// Core System 的公开入口：GetRandomBossSkillId。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string? GetRandomBossSkillId()
    {
        var candidates = new System.Collections.Generic.List<string>();
        foreach (var skill in SkillDatabase.All())
        {
            if (skill.Source != SkillSource.Boss) continue;
            if (CurrentCharacterHasSkill(skill.Id)) continue;
            if (HasAcquiredSkill(skill.Id)) continue;
            candidates.Add(skill.Id);
        }
        return candidates.Count > 0 ? candidates[EventRewardRandom.Next(candidates.Count)] : null;
    }

    // 初始事件③效果（新）：从当前章Boss实际拥有的技能中随机选一个玩家尚未拥有的技能。
    // 以下技能的效果实现里显式把持有者 as EnemyInstance（见 Scripts/Skills/BossChapter1Skills.cs），
    // 玩家（Player）持有时该转换恒为 null，效果直接空转——必须排除，否则玩家抽到后"技能到手但完全不生效"。
    private static readonly HashSet<string> _nonObtainableBossSkillIds = new()
    {
        SkillIds.BossHealerPassive,
        SkillIds.BossTraitorCombo,
        SkillIds.BuDao
    };

    /// <summary>
    /// Core System 的公开入口：GetRandomSkillIdFromCurrentBoss。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string? GetRandomSkillIdFromCurrentBoss()
    {
        var bossEnemyIds = GetOrCreateChapterBossEncounterEnemyIds(CurrentChapter);
        var candidates = new System.Collections.Generic.List<string>();
        foreach (var enemyId in bossEnemyIds)
        {
            var definition = EnemyDatabase.GetEnemy(enemyId);
            if (definition == null) continue;
            foreach (var skillId in definition.SkillIds)
            {
                if (_nonObtainableBossSkillIds.Contains(skillId)) continue;
                if (CurrentCharacterHasSkill(skillId)) continue;
                if (HasAcquiredSkill(skillId)) continue;
                if (!candidates.Contains(skillId)) candidates.Add(skillId);
            }
        }
        return candidates.Count > 0 ? candidates[EventRewardRandom.Next(candidates.Count)] : null;
    }

    // ①效果：供 MainFlow 展示技能三选一时使用，返回最多 count 个普通/稀有非Boss技能。
    // 角色专属技能也属于玩家可习得技能池，不因当前英雄不同而排除。
    /// <summary>
    /// Core System 的公开入口：CreateRandomNonBossSkillChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<Skill> CreateRandomNonBossSkillChoices(int count)
    {
        var candidates = new System.Collections.Generic.List<Skill>();
        foreach (var skill in SkillDatabase.All())
        {
            if (!skill.CanAppearInRandomChoicePool) continue;
            if (skill.Rarity == SkillRarity.Epic || skill.Rarity == SkillRarity.Legendary) continue;
            if (skill.Source == SkillSource.Boss) continue;
            if (CurrentCharacterHasSkill(skill.Id)) continue;
            if (HasAcquiredSkill(skill.Id)) continue;
            candidates.Add(skill);
        }

        var result = new List<Skill>();
        while (result.Count < count && candidates.Count > 0)
        {
            var idx = EventRewardRandom.Next(candidates.Count);
            result.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return result;
    }
    public static IReadOnlyList<EquipmentDefinition> AcquiredEquipment => Equipment;
    public static IReadOnlyList<RunBuff> ActiveRunBuffs => RunBuffManager.ActiveBuffs;
    public static IReadOnlyList<MapNode> Nodes => MapNodes;
    public static int ClearedBattleCount => ClearedStages.Count;
    public static bool IsFinalChapter => CurrentChapter >= TotalChapters;
    public static bool HasActiveSpecialBattle => !string.IsNullOrEmpty(_activeSpecialBattleId);
    public static string ActiveSpecialBattleId => _activeSpecialBattleId;
    public static double ActiveSpecialBattleEnemyMaxHpMultiplier => _activeSpecialBattleEnemyMaxHpMultiplier;
    public static double ActiveSpecialBattleEnemyFinalDamageMultiplier => _activeSpecialBattleEnemyFinalDamageMultiplier;

    /// <summary>
    /// Core System 的公开入口：GetChapterDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetChapterDisplayName(int chapter)
    {
        var key = $"chapter.{chapter}.name";
        return Localization.Get(key);
    }

    /// <summary>
    /// Core System 的公开入口：GetChapterVariantDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetChapterVariantDisplayName(ChapterVariant variant)
    {
        return variant switch
        {
            ChapterVariant.CurseNight => Localization.Get("chapter.variant.curse_night"),
            ChapterVariant.BloodMoon => Localization.Get("chapter.variant.blood_moon"),
            ChapterVariant.Rainstorm => Localization.Get("chapter.variant.rainstorm"),
            _ => Localization.Get("chapter.variant.none")
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetChapterRouteDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetChapterRouteDisplayName(ChapterRoute route)
    {
        return route switch
        {
            ChapterRoute.Sewer => Localization.Get("chapter.route.sewer"),
            ChapterRoute.Imperial => Localization.Get("chapter.route.imperial"),
            _ => Localization.Get("chapter.route.default")
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetCurrentChapterBossDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetCurrentChapterBossDisplayName(int chapter)
    {
        foreach (var enemyId in GetOrCreateChapterBossEncounterEnemyIds(chapter))
        {
            var enemy = EnemyDatabase.GetEnemy(enemyId);
            if (enemy != null && enemy.Type == EnemyType.Boss)
            {
                return enemy.Name;
            }
        }

        return "未知统治者";
    }

    /// <summary>
    /// Core System 的公开入口：GetOrCreateChapterBossEncounterEnemyIds。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<string> GetOrCreateChapterBossEncounterEnemyIds(int chapter)
    {
        if (chapter == 1 && RunBuffManager.CountStacks(RunBuffIds.MoonAttention) > 0)
        {
            var moonEncounter = new List<string> { "moon_boss" };
            PlannedChapterBossEncounterEnemyIds[chapter] = moonEncounter;
            return moonEncounter;
        }

        if (PlannedChapterBossEncounterEnemyIds.TryGetValue(chapter, out var cached))
        {
            return cached;
        }

        var stageId = StageDatabase.GetBossStageIdForChapter(chapter);
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return Array.Empty<string>();
        }

        var encounter = StageDatabase.RollEncounter(stageId);
        if (encounter == null)
        {
            return Array.Empty<string>();
        }

        var selectedEnemyIds = new List<string>(encounter.EnemyIds);
        PlannedChapterBossEncounterEnemyIds[chapter] = selectedEnemyIds;
        return selectedEnemyIds;
    }

    /// <summary>
    /// Core System 的公开入口：GetAvailableBossEncounterPlans。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> GetAvailableBossEncounterPlans(int chapter)
    {
        if (chapter == 1 && RunBuffManager.CountStacks(RunBuffIds.MoonAttention) > 0)
        {
            return new List<IReadOnlyList<string>> { new List<string> { "moon_boss" } };
        }

        var stageId = StageDatabase.GetBossStageIdForChapter(chapter);
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return Array.Empty<IReadOnlyList<string>>();
        }

        var allowed = StageDatabase.GetAllowedEncounters(stageId);
        var plans = new List<IReadOnlyList<string>>();
        foreach (var entry in allowed)
        {
            plans.Add(new List<string>(entry.EnemyIds));
        }

        return plans;
    }

    /// <summary>
    /// Core System 的公开入口：SetDebugBossEncounterPlan。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetDebugBossEncounterPlan(int chapter, IReadOnlyList<string> enemyIds)
    {
        PlannedChapterBossEncounterEnemyIds[chapter] = new List<string>(enemyIds);
    }

    /// <summary>
    /// Core System 的公开入口：BuildBossEncounterDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string BuildBossEncounterDisplayName(IReadOnlyList<string> enemyIds)
    {
        var names = new List<string>();
        foreach (var enemyId in enemyIds)
        {
            var enemy = EnemyDatabase.GetEnemy(enemyId);
            names.Add(enemy?.Name ?? enemyId);
        }

        return string.Join(" + ", names);
    }

    /// <summary>
    /// Core System 的公开入口：InvalidateChapterBossEncounterPlan。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void InvalidateChapterBossEncounterPlan(int chapter)
    {
        PlannedChapterBossEncounterEnemyIds.Remove(chapter);
    }


    /// <summary>
    /// Core System 的公开入口：EnterMainMenu。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void EnterMainMenu()
    {
        CurrentRunState = RunState.MainMenu;
    }

    /// <summary>
    /// Core System 的公开入口：BeginNewRun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginNewRun()
    {
        ResetRunData();
        CurrentRunState = RunState.CharacterSelect;
    }

    /// <summary>
    /// Core System 的公开入口：SelectCharacter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SelectCharacter(string characterId)
    {
        ResetRunData();
        CurrentCharacterId = characterId;
        // 图鉴：角色选择才是"一次Run真正开始"的判定点（角色选择界面本身被返回不算），
        // CurrentRunId 用于装备"完成过的Run数量"去重统计，必须在下面的初始装备发放之前递增。
        CodexService.CurrentRunId++;
        CodexService.RecordCharacterRunStart(characterId);
        CodexService.RecordCharacterChapterReached(characterId, 1);
        MaxHP = CurrentCharacter?.MaxHp ?? BattleConstants.InitialHealth;
        CurrentHP = MaxHP;
        CurrentMana = InitialMana;
        if (CurrentCharacter != null)
        {
            foreach (var equipId in CurrentCharacter.StartingEquipmentIds)
                AddEquipment(equipId, EquipmentGainSource.CharacterStartingEquipment);
        }
        BuildDefaultMap();
        RollChapterVariant();
        // 阵营命运：按当前角色阵营在新 Run 一次性随机；第1章天命骰也从这里判定。必须放在
        // 角色/地图数据都已就绪之后，因为随机与判定过程可能读取 CurrentCharacter/AddGold/AddEquipment。
        FactionFateManager.RollFateIfEligible();
        FactionFateManager.RollChapterDiceIfNeeded(1);
        // 魏·双线征伐：命运随机之后（此时 SelectedFateId 已就绪）、主Boss节点已经建好之后，
        // 追加第一章的额外Boss节点。非该命运/无合法候选时方法内部直接跳过。
        FactionFateManager.TryAddExtraBossNodeForChapter(1);
        CurrentRunState = RunState.Map;
    }

    /// <summary>
    /// Rolls the persistent weather/event modifier for the chapter being entered.
    /// Each listed variant has an independent 10% slice; rain is therefore a 10% chance.
    /// </summary>
    private static void RollChapterVariant()
    {
        var roll = Random.Shared.NextDouble();
        _chapterVariant = roll switch
        {
            < 0.10 => ChapterVariant.BloodMoon,
            < 0.20 => ChapterVariant.CurseNight,
            < 0.30 => ChapterVariant.Rainstorm, // 10%: [20%, 30%)
            _ => ChapterVariant.None
        };

        if (_chapterVariant == ChapterVariant.CurseNight)
        {
            RunBuffManager.AddStacks(RunBuffIds.Curse, 2);
        }
        else if (_chapterVariant == ChapterVariant.BloodMoon)
        {
            RunBuffManager.Add(RunBuffIds.BloodMoonDarkness);
        }
        else if (_chapterVariant == ChapterVariant.Rainstorm)
        {
            RunBuffManager.Add(RunBuffIds.Wet);
        }
    }

    /// <summary>
    /// Core System 的公开入口：SetRunState。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetRunState(RunState state)
    {
        CurrentRunState = state;
    }

    /// <summary>
    /// Core System 的公开入口：HasSelectedCharacter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasSelectedCharacter()
    {
        return !string.IsNullOrWhiteSpace(CurrentCharacterId);
    }

    /// <summary>
    /// Core System 的公开入口：SetCurrentStage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetCurrentStage(int stageIndex)
    {
        CurrentStage = stageIndex;
        CurrentRunState = RunState.Battle;
    }

    /// <summary>
    /// Core System 的公开入口：SetCurrentNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetCurrentNode(string nodeId)
    {
        CurrentNodeId = nodeId;
        var node = GetNode(nodeId);
        if (node != null)
        {
            CurrentStage = node.StageIndex;
            CurrentRunState = node.Type is MapNodeType.Battle or MapNodeType.Elite or MapNodeType.Boss
                ? RunState.Battle
                : RunState.Map;
        }
    }

    /// <summary>
    /// Core System 的公开入口：GetPlayerInitialHealth。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetPlayerInitialHealth()
    {
        // 兼容已在旧版本存档/进行中 Run 中持有煞气缠身的情况：即使该 Buff 在
        // 本次启动前已经存在，下一场战斗也不能带着旧的高生命值进入。
        if (RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) > 0)
        {
            CurrentHP = 1;
            return 1;
        }

        var base_ = CurrentHP > 0 ? CurrentHP : (MaxHP > 0 ? MaxHP : BattleConstants.InitialHealth);
        return base_ + TempHp;
    }

    /// <summary>
    /// Core System 的公开入口：GetPlayerInitialMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetPlayerInitialMana()
    {
        if (HasEquipment(EquipmentIds.YellowTalisman))
        {
            return 2 + _initialEventStartingManaBonus;
        }

        if (CurrentCharacterHasSkill(SkillIds.Keji) || HasAcquiredSkill(SkillIds.Keji)
            || HasAcquiredSkill(SkillIds.KejiUnlimited))
        {
            return System.Math.Max(CurrentMana, InitialMana + _initialEventStartingManaBonus);
        }

        return InitialMana + _initialEventStartingManaBonus;
    }

    /// <summary>
    /// Core System 的公开入口：IsStageUnlocked。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsStageUnlocked(int stageIndex)
    {
        return UnlockedStages.Contains(stageIndex);
    }

    /// <summary>
    /// Core System 的公开入口：IsStageCleared。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsStageCleared(int stageIndex)
    {
        return ClearedStages.Contains(stageIndex);
    }

    /// <summary>
    /// Core System 的公开入口：IsNodeUnlocked。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsNodeUnlocked(string nodeId)
    {
        if (DebugMapEnabled)
        {
            return true;
        }

        return UnlockedNodeIds.Contains(nodeId);
    }

    /// <summary>
    /// Core System 的公开入口：IsNodeCleared。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsNodeCleared(string nodeId)
    {
        return ClearedNodeIds.Contains(nodeId);
    }

    /// <summary>
    /// Core System 的公开入口：GetNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static MapNode? GetNode(string nodeId)
    {
        foreach (var node in MapNodes)
        {
            if (node.Id == nodeId)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Core System 的公开入口：MarkCurrentStageCleared。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkCurrentStageCleared()
    {
        // A map node is a one-time step.  The UI already renders cleared nodes as
        // disabled, but this guard keeps an old/deferred click from awarding the
        // same battle twice or consuming the last Forage on an accidental replay.
        if (CurrentStage < 0 || string.IsNullOrWhiteSpace(CurrentNodeId) || IsNodeCleared(CurrentNodeId))
        {
            return;
        }

        ClearedStages.Add(CurrentStage);
        DefeatedEnemyCount += 1;
        MarkCurrentNodeCleared();
    }

    /// <summary>
    /// Core System 的公开入口：HandleBattleLoss。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HandleBattleLoss()
    {
        TempHp = 0;
        RunBuffManager.ConsumeBattle();

        // 粮草是“死亡时消耗”的重试机会：持有最后1点粮草死亡时，必须先消耗
        // 它并允许本次复活；只有死亡发生时已经没有粮草，才结束本局。
        if (Forage <= 0)
        {
            Forage = 0;
            CurrentHP = 0;
            CurrentRunState = RunState.GameOver;
            CodexService.RecordCharacterDeath(CurrentCharacterId);
            return false;
        }

        Forage -= 1;
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstForageSpent);

        // 初始事件⑰描述的是“每次战斗结束”，因此仍可继续的战败同样回满。
        // 永久禁疗 Buff 仍优先，避免绕过【煞气缠身】“无法通过任何方式恢复”的明确规则。
        SetCurrentHp(_initialEventBattleEndFullHeal && !RunBuffManager.IsHealingBlocked()
            ? MaxHP
            : Math.Max(1, _preBattleHP));
        CurrentMana = _preBattleMana;
        CurrentRunState = RunState.Map;
        return true;
    }

    /// <summary>
    /// Core System 的公开入口：SaveBattleState。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SaveBattleState(int currentHp, double currentMana)
    {
        // 临时生命在战斗结束后消失：将战斗血量截回 MaxHP 上限，再清除 TempHp 记录。
        // 通过 SetCurrentHp 落盘，避免【煞气缠身】在战斗内正确保持1点、
        // 但战后被保存的旧数值重新覆盖。
        SetCurrentHp(currentHp);
        TempHp = 0;
        CurrentMana = currentMana;
    }

    // 战斗开始时调用：保存进入战斗前的 HP/Mana 快照，用于战败重试时完全恢复。
    /// <summary>
    /// Core System 的公开入口：RecordPreBattleState。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RecordPreBattleState()
    {
        _preBattleHP = CurrentHP;
        _preBattleMana = CurrentMana;
    }

    /// <summary>
    /// Core System 的公开入口：RecoverHealthAfterBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RecoverHealthAfterBattle()
    {
        if (_pendingCollapseVictoryHpOverride.HasValue)
        {
            var overrideValue = _pendingCollapseVictoryHpOverride.Value;
            _pendingCollapseVictoryHpOverride = null;
            SetCurrentHp(System.Math.Clamp(overrideValue, 1, MaxHP));
            return;
        }

        if (RunBuffManager.IsHealingBlocked()) return;
        if (_initialEventBattleEndFullHeal)
        {
            // 这是战后状态结算而非普通治疗；禁疗已在上方统一判断，直接写入上限，
            // 避免 AddCurrentHp 再次解释门禁而让初始事件效果静默失效。
            SetCurrentHp(MaxHP);
            return;
        }
        if (_initialEventNoBattleEndRecovery) return;
        if (HasEquipment(EquipmentIds.MiJiang))
        {
            SetCurrentHp(MaxHP);
            return;
        }
        AddCurrentHp(BattleEndHealthRecovery);
    }

    /// <summary>
    /// Core System 的公开入口：HasClaimedReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasClaimedReward(string nodeId)
    {
        return ClaimedRewardNodeIds.Contains(nodeId);
    }

    /// <summary>
    /// Core System 的公开入口：MarkRewardClaimed。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkRewardClaimed(string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            ClaimedRewardNodeIds.Add(nodeId);
        }
    }

    /// <summary>
    /// Core System 的公开入口：AllStagesCleared。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool AllStagesCleared()
    {
        var lastNode = MapNodes.Count > 0 ? MapNodes[^1] : null;
        return lastNode != null && ClearedNodeIds.Contains(lastNode.Id);
    }

    /// <summary>
    /// Core System 的公开入口：EndRun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void EndRun()
    {
        ResetRunData();
        CurrentRunState = RunState.MainMenu;
    }

    /// <summary>
    /// Core System 的公开入口：AdvanceToNextChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AdvanceToNextChapter()
    {
        if (IsFinalChapter)
        {
            return;
        }

        CurrentChapter += 1;
        CodexService.RecordCharacterChapterReached(CurrentCharacterId, CurrentChapter);
        // 天命骰在章节号更新之后立刻判定，保证骰子历史里记录的是新章节号，
        // 与 SelectCharacter 里"第1章"判定的调用位置（角色/地图数据就绪之后）保持一致的时机原则。
        FactionFateManager.RollChapterDiceIfNeeded(CurrentChapter);
        PlannedChapterBossEncounterEnemyIds.Remove(CurrentChapter);
        CurrentStage = -1;
        CurrentNodeId = string.Empty;
        ClearedStages.Clear();
        ClearedNodeIds.Clear();
        UnlockedStages.Clear();
        UnlockedNodeIds.Clear();
        ClaimedRewardNodeIds.Clear();
        ClearCurrentChapterVariant();
        RunBuffManager.ApplyChapterStart(CurrentChapter);
        BuildMapForCurrentChapter();
        RollChapterVariant();
        // 魏·双线征伐：必须放在 BuildMapForCurrentChapter() 之后（主Boss节点已就绪）、
        // UnlockedNodeIds.Clear() 之后（不会被随后的清空冲掉）。
        FactionFateManager.TryAddExtraBossNodeForChapter(CurrentChapter);
        MapRunState.ResetForNewChapter(CurrentChapter);

        ContinueExploreManager.ResetForNewChapter();
        ResetPowerForChapter();
        SetCurrentHp(MaxHP);
        CurrentRunState = RunState.Map;
    }

    /// <summary>
    /// Core System 的公开入口：SetDebugMapEnabled。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetDebugMapEnabled(bool enabled)
    {
        DebugMapEnabled = enabled;
    }

    /// <summary>
    /// Core System 的公开入口：DebugGoToChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void DebugGoToChapter(int chapter)
    {
        if (CurrentRunState == RunState.Battle) return;
        CurrentChapter = chapter;
        PlannedChapterBossEncounterEnemyIds.Remove(CurrentChapter);
        CurrentStage = -1;
        CurrentNodeId = string.Empty;
        ClearedStages.Clear();
        ClearedNodeIds.Clear();
        UnlockedStages.Clear();
        UnlockedNodeIds.Clear();
        ClaimedRewardNodeIds.Clear();
        ClearCurrentChapterVariant();
        BuildMapForCurrentChapter();
        MapRunState.ResetForNewChapter(CurrentChapter);
        ContinueExploreManager.ResetForNewChapter();
        ResetPowerForChapter();
    }

    /// <summary>
    /// Core System 的公开入口：SetDebugChapterVariant。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetDebugChapterVariant(ChapterVariant variant)
    {
        // Remove effects of the current variant before switching.
        ClearCurrentChapterVariant();

        _chapterVariant = variant;

        if (_chapterVariant == ChapterVariant.CurseNight)
            RunBuffManager.AddStacks(RunBuffIds.Curse, 2);
        else if (_chapterVariant == ChapterVariant.BloodMoon)
            RunBuffManager.Add(RunBuffIds.BloodMoonDarkness);
        else if (_chapterVariant == ChapterVariant.Rainstorm)
            RunBuffManager.Add(RunBuffIds.Wet);
    }

    private static void ClearCurrentChapterVariant()
    {
        if (_chapterVariant == ChapterVariant.CurseNight)
            RunBuffManager.RemoveAllStacks(RunBuffIds.Curse);
        else if (_chapterVariant == ChapterVariant.BloodMoon)
            RunBuffManager.RemoveAllStacks(RunBuffIds.BloodMoonDarkness);
        else if (_chapterVariant == ChapterVariant.Rainstorm)
            RunBuffManager.RemoveAllStacks(RunBuffIds.Wet);

        _chapterVariant = ChapterVariant.None;
    }

    /// <summary>
    /// Core System 的公开入口：SetChapterRoute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetChapterRoute(ChapterRoute route)
    {
        _chapterRoute = route;
        PlannedChapterBossEncounterEnemyIds.Remove(CurrentChapter);
        if (route != ChapterRoute.Default)
        {
            MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstSpecialRoute);
        }
    }

    /// <summary>
    /// Core System 的公开入口：HasChapterVariant。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasChapterVariant(ChapterVariant variant)
    {
        return _chapterVariant == variant;
    }

    /// <summary>
    /// Core System 的公开入口：OnCurseBladeEquipped。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void OnCurseBladeEquipped()
    {
        if (_curseBladeCurrentCurseAmplified)
        {
            return;
        }

        RunBuffManager.AmplifyExistingCurseStacksForCurseBlade();
        _curseBladeCurrentCurseAmplified = true;
    }

    public static bool ShouldLoadDefaultCharacterSkills => !_defaultCharacterSkillsRemovedBySoulPossession;

    /// <summary>
    /// Core System 的公开入口：AddAcquiredSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddAcquiredSkill(string skillId)
    {
        AddAcquiredSkill(skillId, false);
    }

    /// <summary>
    /// Core System 的公开入口：AddAcquiredSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddAcquiredSkill(string skillId, bool soulPossessionSkill)
    {
        if (!AcquiredSkillIds.Contains(skillId))
        {
            AcquiredSkillIds.Add(skillId);
        }

        if (soulPossessionSkill)
        {
            SoulPossessionSkillIds.Add(skillId);
        }
    }

    /// <summary>
    /// Core System 的公开入口：RemoveAcquiredSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RemoveAcquiredSkill(string skillId)
    {
        AcquiredSkillIds.RemoveAll(id => id == skillId);
        SoulPossessionSkillIds.Remove(skillId);
    }

    /// <summary>
    /// Core System 的公开入口：AddGold。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddGold(int amount)
    {
        // 黄金雕像（背包生效，见 EquipmentIds.GoldenStatue）：金币获得量 +20%。
        // 只读取 HasActiveEquipment 这个通用查询，不针对黄金雕像写死判断以外的逻辑，
        // 以后其它"背包生效"装备如果也想影响资源获取，同样调用 HasActiveEquipment 即可。
        if (amount > 0 && HasActiveEquipment(EquipmentIds.GoldenStatue))
        {
            amount = (int)Math.Round(amount * 1.2, MidpointRounding.AwayFromZero);
        }

        // Hero Unlock System：累计"获得金币"成就进度（只统计净增加，不统计消费）。
        if (amount > 0)
        {
            HeroUnlockProgress.AddAchievementProgress(AchievementType.GoldEarned, amount);
            CodexService.RecordGoldEarned(amount);
        }
        else if (amount < 0)
        {
            CodexService.RecordGoldSpent(-amount);
        }

        // 允许金币为负数（初始事件㉑【财富契约】起始-50金）。所有消费入口本来就用
        // "Gold >= 价格" 判断可购买性，负金币只是暂时买不起任何东西，不会引发异常。
        Gold += amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddForage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddForage(int amount)
    {
        Forage = Math.Max(0, Forage + amount);
    }

    // ————————————————————————————————————————
    // 电量（Power）系统
    // ————————————————————————————————————————

    /// <summary>
    /// 载具等装备提供的电量上限加成（框架预留，目前没有任何装具提供加成，恒为0）。
    /// 以后新增载具时，直接在这里按 CountEquipment/HasEquipment 累加即可，
    /// 不需要改这个方法以外的任何电量相关代码。
    /// </summary>
    private static int GetVehiclePowerBonus()
    {
        return 0;
    }

    /// <summary>
    /// Core System 的公开入口：AddPower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// 只封顶 MaxPower，不设下限——"被动失去电量"（敌方技能/装备副作用/Debuff/强制惩罚等）
    /// 允许把电量打到负数；真正需要"不能透支"语义的主动支付走 TrySpendPower（它自己会在
    /// 扣减前检查 Power &gt;= cost，不经过这个方法的加减逻辑），两者职责不重叠。
    /// </summary>
    public static void AddPower(int amount)
    {
        Power = Math.Min(Power + amount, MaxPower);
    }

    /// <summary>
    /// Core System 的公开入口：CanAffordPower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanAffordPower(int cost)
    {
        return Power >= cost;
    }

    /// <summary>
    /// Core System 的公开入口：TrySpendPower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TrySpendPower(int cost)
    {
        if (cost <= 0)
        {
            return true;
        }

        if (Power < cost)
        {
            return false;
        }

        Power -= cost;
        return true;
    }

    /// <summary>
    /// 地图节点电量消耗的唯一正式计算入口——UI显示（MapController.GetNodeText）、
    /// 可负担性预检查（MapExplorationView）、实际扣费（MainFlow.OnNodeSelected）
    /// 三处必须都调用这一个函数，禁止任何一处再单独写一套消耗规则，否则会出现
    /// "UI显示15、实际扣10"这类不一致。
    ///
    /// 优先级（对应设计文档"固定节点配置 → 第一章前五关保护 → Boss特殊配置 →
    /// 事件品质规则 → 战斗/精英规则 → 当前默认消耗"）：
    /// 1. node.UseFixedEnergyCost：第一章前五关与各章常规Boss都通过这个标记锁定历史
    ///    固定值，不受下面任何新规则影响。
    /// 2. 常规Boss兜底：万一某个Boss节点忘记设置 UseFixedEnergyCost，也不会被
    ///    "战斗类型"规则误判成20，双重保险。
    /// 3. 战斗/精英节点：统一走新的 CombatEnergyCost。
    /// 4. 事件节点：按其已解析事件（EventManager.GetEventForNode——若节点已经在建图时
    ///    写入 FixedEventId，这里会直接命中缓存，不会重新随机）的 Rarity 决定消耗档位。
    /// 5. 其它类型（商店/剧情等）：维持节点自身 PowerCost，本次规则未涉及。
    /// </summary>
    public static int GetNodeEnergyCost(MapNode node)
    {
        if (node.UseFixedEnergyCost)
        {
            return node.FixedEnergyCost;
        }

        // 自由探索节点在生成时已经按品类确定价格。必须锁定这份报价，不能在地图显示、
        // 可负担性判断和点击扣费时反复根据事件随机结果重新估价，否则同一节点可能先
        // 显示20电量、点击时又按15电量结算。
        if (node.IsContinueExploring)
        {
            return node.PowerCost;
        }

        // 第二/三/四章前5关（固定流程节点，StageIndex 0~4）：普通战斗与事件统一10点，
        // 精英20点，不沿用"第6关及以后"的新电量规则（普通事件15/稀有20/史诗25/战斗20）。
        // 只覆盖 Battle/Elite/Event 三种类型——Boss（含魏·双线征伐的额外Boss）已经在
        // 上面的 UseFixedEnergyCost 分支提前返回，不会走到这里，天然不受影响。
        if ((CurrentChapter == 2 || CurrentChapter == 3 || CurrentChapter == 4) && node.StageIndex is >= 0 and <= 4)
        {
            if (node.Type == MapNodeType.Elite)
            {
                return ContinueExploreConfig.ElitePowerCost;
            }

            if (node.Type == MapNodeType.Battle)
            {
                return ContinueExploreConfig.NormalBattlePowerCost;
            }

            if (node.Type == MapNodeType.Event)
            {
                return ContinueExploreConfig.NormalEventPowerCost;
            }
        }

        if (node.Type == MapNodeType.Boss)
        {
            return ContinueExploreConfig.BossPowerCost;
        }

        if (node.Type is MapNodeType.Battle or MapNodeType.Elite)
        {
            return ContinueExploreConfig.CombatEnergyCost;
        }

        if (node.Type == MapNodeType.Event)
        {
            var eventData = EventManager.GetEventForNode(node);
            if (eventData == null)
            {
                return node.PowerCost;
            }

            return eventData.Rarity switch
            {
                EventRarity.Common => ContinueExploreConfig.EventCommonEnergyCost,
                EventRarity.Rare => ContinueExploreConfig.EventRareEnergyCost,
                EventRarity.Epic => ContinueExploreConfig.EventEpicEnergyCost,
                _ => LogUnmappedEventRarityFallback(eventData.Rarity, node)
            };
        }

        return node.PowerCost;
    }

    /// <summary>
    /// 事件品质出现了新规则未配置的档位（目前只有 Legendary，尚无任何事件实际使用）时，
    /// 打印一次开发警告并回退到普通品质消耗，而不是直接按史诗价处理——避免"没配置就
    /// 收最贵价"这种对玩家不友好的隐性行为。
    /// </summary>
    private static int LogUnmappedEventRarityFallback(EventRarity rarity, MapNode node)
    {
        Godot.GD.Print($"[MapEnergyCost] 事件品质 {rarity}（节点 {node.Id}）没有配置对应电量消耗，已回退为普通品质消耗。");
        return ContinueExploreConfig.EventCommonEnergyCost;
    }

    /// <summary>
    /// 每章开始时把电量恢复满（用当前的 MaxPower——如果本局已经获得了永久上限加成，
    /// 恢复的就是"新的"上限，不是每章都掉回基础值100）。
    /// </summary>
    private static void ResetPowerForChapter()
    {
        Power = MaxPower;
        // 同步一次，避免章节/整局刚开始时地图HUD误把"从0变为满电量"当成一次真实的
        // 电量变化并播放脉冲动画。
        PowerBeforeLastNode = Power;
    }

    /// <summary>
    /// Core System 的公开入口：AddCurrentHp。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddCurrentHp(int amount)
    {
        // 如影随行效果二：黄月英免疫事件扣血（负向HP变化）。
        if (amount < 0 && HasPersistentPlayerSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        // 煞气缠身：禁止所有正向HP变化（事件回复、装备回复等）。
        if (amount > 0 && RunBuffManager.IsHealingBlocked()) return;

        CurrentHP = Math.Clamp(CurrentHP + amount, 0, MaxHP);
    }

    /// <summary>
    /// 将地图/事件生命值设为指定数值，并限制在有效生命范围内。
    /// 用于“生命降至1”等明确状态变更；它不是伤害或治疗，因此不会触发伤害减免或治疗禁用规则。
    /// 【煞气缠身】持有期间，所有正向生命设定都必须保持为1，防止章节/战后结算绕过该状态。
    /// </summary>
    public static void SetCurrentHp(int value)
    {
        if (RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) > 0)
        {
            // 仍允许明确的“设为0”走死亡流程；任何存活状态一律保持在1点生命。
            CurrentHP = value <= 0 ? 0 : 1;
            return;
        }

        CurrentHP = Math.Clamp(value, 0, MaxHP);
    }

    /// <summary>
    /// 爆炸果实①"碰一下"专用扣血入口：与普通事件 HP 消耗（EventCostType.CurrentHP）不同，
    /// 这个选项必须始终可选（不受"生命不足则禁用选项"的 CanAffordCost 门槛限制），
    /// 因此扣血导致的死亡不能靠"选项不可用"来避免，而是要按本项目已有的"战败"流程处理：
    /// 若死亡时持有粮草则消耗1点并回复至满生命；只有死亡时已无粮草才结束本局。
    /// </summary>
    public static void ApplyExplosiveFruitTouchDamage(int amount)
    {
        var beforeHp = CurrentHP;
        AddCurrentHp(-amount);
        if (beforeHp > 0 && CurrentHP <= 0)
        {
            HandleMapEventLethalDamage();
        }
    }

    /// <summary>
    /// 地图事件（非战斗）导致生命值耗尽时的统一处理：与 HandleBattleLoss 使用同一套
    /// 粮草消耗/游戏结束规则，但不触碰战斗专属的状态（_preBattleHP/RunBuffManager.ConsumeBattle
    /// 等），因为这次死亡根本没有发生在战斗中。
    /// </summary>
    private static void HandleMapEventLethalDamage()
    {
        if (Forage <= 0)
        {
            Forage = 0;
            CurrentHP = 0;
            CurrentRunState = RunState.GameOver;
            CodexService.RecordCharacterDeath(CurrentCharacterId);
        }
        else
        {
            Forage -= 1;
            MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstForageSpent);
            SetCurrentHp(MaxHP);
        }
    }

    /// <summary>
    /// 爆炸果实①"碰一下"：永久获得第4个"普通饰品槽"（EquipmentSlot.Accessory5）。
    /// 与吴·多宝架命中时出现的 Accessory4 完全独立，互不影响。
    /// </summary>
    public static void GrantExplosiveFruitAccessorySlot()
    {
        _hasExplosiveFruitAccessorySlot = true;
    }

    /// <summary>
    /// 爆炸果实②"远处射箭激发果实"：本局内永久将万箭齐发的伤害属性强化为火属性。
    /// 只是设置一个标记位，真正生效由 BattlePhaseResolutionEffect.ApplyArrowBarrageHit
    /// 读取本标记后覆盖 DamageEvent 的 DamageType，不修改万箭齐发的费用/目标数量/
    /// 基础伤害/卡牌类型/是否属于杀。
    /// </summary>
    public static void GrantArrowBarrageFireUpgrade()
    {
        _arrowBarrageFireUpgraded = true;
    }

    /// <summary>元素祭坛：为本局所有玩家攻击牌追加元素属性。</summary>
    public static void GrantPlayerAttackAttributes(AttackAttribute attributes)
    {
        _playerAttackAttributeBonus |= attributes;
    }

    /// <summary>元素祭坛：将已拥有的月亮宝石升级为四元素天体撞击。</summary>
    public static bool TryAwakenMoonGemElements()
    {
        if (!OwnsEquipment(EquipmentIds.MoonGem))
        {
            return false;
        }

        _moonGemElementallyAwakened = true;
        return true;
    }

    // 临时生命：叠加在 CurrentHP 之上，可使显示值超过 MaxHP，不修改上限。战斗结束后由 SaveBattleState 清除。
    /// <summary>
    /// Core System 的公开入口：AddTempHp。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddTempHp(int amount)
    {
        if (amount <= 0) return;
        TempHp += amount;
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstTempHp);
    }

    /// <summary>
    /// Core System 的公开入口：AddMaxHp。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddMaxHp(int amount)
    {
        // 如影随行效果一：黄月英无法获得额外最大生命值。
        if (amount > 0 && HasPersistentPlayerSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        // 如影随行效果二：黄月英免疫扣血（负向HP变化），与 AddCurrentHp 保持一致，
        // 覆盖最大生命值的扣减（例如商店事件"抢劫"），而不仅仅是当前生命值。
        if (amount < 0 && HasPersistentPlayerSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        MaxHP = Math.Max(1, MaxHP + amount);
        CurrentHP = Math.Min(CurrentHP, MaxHP);
    }

    /// <summary>
    /// Core System 的公开入口：AddMaxHpByPercentWithCurrentSync。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int AddMaxHpByPercentWithCurrentSync(int percent)
    {
        if (percent <= 0 || MaxHP <= 0)
        {
            return 0;
        }

        var bonus = Math.Max(1, (int)Math.Ceiling(MaxHP * (percent / 100d)));
        AddMaxHp(bonus);
        AddCurrentHp(bonus);
        return bonus;
    }

    /// <summary>
    /// Core System 的公开入口：HasAcquiredSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasAcquiredSkill(string skillId)
    {
        return AcquiredSkillIds.Contains(skillId);
    }

    /// <summary>
    /// Core System 的公开入口：CurrentCharacterHasSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CurrentCharacterHasSkill(string skillId)
    {
        return ShouldLoadDefaultCharacterSkills
            && CurrentCharacter != null
            && CurrentCharacter.SkillIds.Contains(skillId);
    }

    /// <summary>
    /// 判断本局玩家是否持有一项会影响地图/战斗初始化的持久技能。
    /// 默认角色技能与奖励获得的角色专属技能都必须被视为有效来源。
    /// </summary>
    public static bool HasPersistentPlayerSkill(string skillId)
    {
        return CurrentCharacterHasSkill(skillId) || HasAcquiredSkill(skillId);
    }

    /// <summary>
    /// Core System 的公开入口：HasSeenEventInRun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasSeenEventInRun(string eventId)
    {
        return SeenRunEventIds.Contains(eventId);
    }

    /// <summary>
    /// Core System 的公开入口：MarkEventSeen。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkEventSeen(string eventId, EventRepeatType repeatType)
    {
        if (repeatType != EventRepeatType.Repeatable)
        {
            SeenRunEventIds.Add(eventId);
        }

        // Hero Unlock System：记录"完成过这个事件"，供【完成指定事件】类型的
        // 解锁条件使用；不影响本方法原有的本局内事件去重逻辑。
        HeroUnlockProgress.RecordEventCompleted(eventId);
    }

    // ===== 事件延迟装备奖励（仙丹术士：问路 / 解惑）=====

    public static bool HasPendingNextBattleEquipmentReward => !string.IsNullOrEmpty(_pendingNextBattleEquipmentRarityPool);

    // ————————————————————————————————————————
    // 芯片系统方法
    // ————————————————————————————————————————

    // 防御芯片：计数 +1，并同步永久获得 10×阵营命运芯片倍率 的最大/当前生命值。
    // 这条 HP 加成原本要求每个调用方各自额外调用 AddMaxHp/AddCurrentHp（见
    // MainFlow.ApplyChipType、RewardSystem.AddChipRewardAction），但灵魂石出售
    // （InventoryManager.ApplySoulStoneChipBonus）、女巫头皮出售、初始事件④同类型
    // 芯片×2、战斗掉落 DefenseChipDropCount 这几个调用点都忘了补这一步，导致玩家
    // 拿到"防御芯片"却没有实际获得生命值加成（原bug）。现在把 HP 加成收口到这里，
    // 所有调用点自动一致，不需要再各自记得叠加。
    /// <summary>
    /// Core System 的公开入口：IncrementDefenseChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementDefenseChipCount()
    {
        GrantDefenseChipInternal();
    }

    /// <summary>
    /// 群·芯片重构只在“芯片三选一”候选生成时替换基础候选；战斗掉落、事件固定奖励、
    /// 装备附带芯片等直接获取方式必须保留原芯片类型，不能在这里二次改写。
    /// 芯片数值翻倍仍由各 Internal 方法读取 GetChipEffectMultiplier 统一处理。
    /// </summary>
    private static void GrantDefenseChipInternal()
    {
        // 孙尚香【武库】：获得任何芯片时改为直接随机获得一件普通/稀有装备，
        // 芯片本身不进入计数、不进入背包。仅当前角色是孙尚香时生效，
        // 其它角色的芯片逻辑完全不受影响。
        if (CurrentCharacterHasSkill(SkillIds.WuKu))
        {
            GrantWuKuChipEquipment();
            return;
        }

        var healthBonus = 10 * FactionFateManager.GetChipEffectMultiplier();
        AddMaxHp(healthBonus);
        AddCurrentHp(healthBonus);

        _defenseChipCount++;
        CodexService.RecordChipObtained("Defense");
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstChip);
    }

    // 攻击芯片：计数 +1；杀系加伤由 RunKillDamageBonus（= count × 3）在 BattleRules 动态计算。
    /// <summary>
    /// Core System 的公开入口：IncrementAttackChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementAttackChipCount()
    {
        GrantAttackChipInternal();
    }

    private static void GrantAttackChipInternal()
    {
        // 孙尚香【武库】：见 IncrementDefenseChipCount 的说明。
        if (CurrentCharacterHasSkill(SkillIds.WuKu))
        {
            GrantWuKuChipEquipment();
            return;
        }

        _attackChipCount++;
        CodexService.RecordChipObtained("Attack");
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstChip);
    }

    // 知识芯片：计数 +1；锦囊加伤由 AttackTrickDamageBonus（= count × 5）在 BattleRules 动态计算。
    /// <summary>
    /// Core System 的公开入口：IncrementKnowledgeChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementKnowledgeChipCount()
    {
        GrantKnowledgeChipInternal();
    }

    private static void GrantKnowledgeChipInternal()
    {
        // 孙尚香【武库】：见 IncrementDefenseChipCount 的说明。
        if (CurrentCharacterHasSkill(SkillIds.WuKu))
        {
            GrantWuKuChipEquipment();
            return;
        }

        _knowledgeChipCount++;
        CodexService.RecordChipObtained("Knowledge");
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstChip);
    }

    // 清除全部芯片计数（不退还HP/加伤）。供事件系统使用。返回清除的总芯片数。
    /// <summary>
    /// Core System 的公开入口：ClearAllChips。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int ClearAllChips()
    {
        var total = _defenseChipCount + _attackChipCount + _knowledgeChipCount;
        _defenseChipCount = 0;
        _attackChipCount = 0;
        _knowledgeChipCount = 0;
        return total;
    }

    // 恶臭蘑菇事件：随机失去1点芯片储备，供 EventSystem.ApplyCost(RandomChip) 调用。
    // 与 IncrementXxxChipCount 对称，直接扣减对应计数，最低为0；不触发孙尚香【武库】等获得逻辑。
    /// <summary>
    /// Core System 的公开入口：DecrementAttackChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void DecrementAttackChipCount()
    {
        _attackChipCount = Math.Max(0, _attackChipCount - 1);
    }

    /// <summary>
    /// Core System 的公开入口：DecrementDefenseChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void DecrementDefenseChipCount()
    {
        _defenseChipCount = Math.Max(0, _defenseChipCount - 1);
    }

    /// <summary>
    /// Core System 的公开入口：DecrementKnowledgeChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void DecrementKnowledgeChipCount()
    {
        _knowledgeChipCount = Math.Max(0, _knowledgeChipCount - 1);
    }

    // 恶臭蘑菇事件：红/黄/绿三色装备Id的本局随机排列，首次访问（懒加载）时用 Fisher-Yates 洗牌一次，
    // 之后同一局内固定不变。所有随机性统一使用 EventRewardRandom（本局唯一官方随机源）。
    private static void EnsureStinkyMushroomColorAssignment()
    {
        if (_stinkyMushroomColorAssignment != null)
        {
            return;
        }

        var ids = new List<string>
        {
            EquipmentIds.StinkyMushroomRed,
            EquipmentIds.StinkyMushroomYellow,
            EquipmentIds.StinkyMushroomGreen
        };
        for (var i = ids.Count - 1; i > 0; i--)
        {
            var j = EventRewardRandom.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        _stinkyMushroomColorAssignment = ids;
    }

    /// <summary>
    /// Core System 的公开入口：GetStinkyMushroomColorForOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetStinkyMushroomColorForOption(int optionIndex)
    {
        EnsureStinkyMushroomColorAssignment();
        return _stinkyMushroomColorAssignment![optionIndex];
    }

    // 调试专用：直接覆盖本局的恶臭蘑菇三色分配，供 DeveloperDebugPanel 指定排列顺序测试。
    /// <summary>
    /// Core System 的公开入口：DebugSetStinkyMushroomColorAssignment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void DebugSetStinkyMushroomColorAssignment(List<string> ids)
    {
        _stinkyMushroomColorAssignment = ids;
    }

    // 从稀有技能池中随机获取一个玩家尚未拥有的技能ID（排除Boss技能）。
    /// <summary>
    /// Core System 的公开入口：GetRandomRareSkillId。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string? GetRandomRareSkillId()
    {
        var candidates = new System.Collections.Generic.List<string>();
        foreach (var skill in SkillDatabase.All())
        {
            if (skill.Rarity != SkillRarity.Rare) continue;
            if (skill.Source == SkillSource.Boss) continue;
            if (CurrentCharacterHasSkill(skill.Id)) continue;
            if (HasAcquiredSkill(skill.Id)) continue;
            candidates.Add(skill.Id);
        }
        return candidates.Count > 0 ? candidates[EventRewardRandom.Next(candidates.Count)] : null;
    }

    /// <summary>
    /// 【技能芯片】的唯一结算入口：从与常规随机技能选择相同的合法池中随机获得技能。
    /// 群·芯片重构会把芯片效果倍率设为2，因此一次技能芯片会获得两项不同技能；
    /// 其它情况下仍只获得一项。返回第一项实际获得的技能，供既有调用方兼容使用。
    /// </summary>
    public static Skill? GrantRandomSkillFromSkillChip()
    {
        var choices = new SkillChoiceProvider
        {
            Count = FactionFateManager.GetChipEffectMultiplier(),
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = true,
            ExcludeOwnedSkills = true
        }.CreateChoices();

        Skill? firstGranted = null;
        foreach (var choice in choices)
        {
            if (choice.Payload is not Skill skill)
            {
                continue;
            }

            AddAcquiredSkill(skill.Id);
            firstGranted ??= skill;
        }

        return firstGranted;
    }

    // 扩容芯片：计数 +1，永久解锁万能装备槽（InventoryManager 在 IsUniversalSlotUnlocked 中检查此值）。
    /// <summary>
    /// Core System 的公开入口：IncrementExpansionChipCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementExpansionChipCount()
    {
        // 孙尚香【武库】：见 IncrementDefenseChipCount 的说明。
        if (CurrentCharacterHasSkill(SkillIds.WuKu))
        {
            GrantWuKuChipEquipment();
            return;
        }

        _expansionChipCount += FactionFateManager.GetChipEffectMultiplier();
        CodexService.RecordChipObtained("Expansion");
    }

    /// <summary>
    /// 孙尚香【武库】专用：随机获得一件普通或稀有装备（各 50% 概率），直接加入背包，
    /// 不经过芯片计数。复用既有的 <see cref="EquipmentDatabase.GetAllEquipments"/> +
    /// <see cref="EquipmentDefinition.CanAppearInRandomPool"/> 过滤规则（和商店随机池、
    /// 商店随机池使用的过滤规则），避免剧情/隐藏装备通过这个入口被抽到。
    /// </summary>
    private static void GrantWuKuChipEquipment()
    {
        var targetRarity = EventRewardRandom.NextDouble() < 0.5 ? EquipmentRarity.Common : EquipmentRarity.Rare;

        var candidates = new System.Collections.Generic.List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity != targetRarity) continue;
            if (!definition.CanAppearInRandomPool) continue;
            candidates.Add(definition);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var chosen = candidates[EventRewardRandom.Next(candidates.Count)];
        AddEquipment(chosen.Id);
    }

    /// <summary>
    /// Core System 的公开入口：AddWineDamageMultiplierBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddWineDamageMultiplierBonus(double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _wineDamageMultiplierBonus += amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddPeachBaseHealBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddPeachBaseHealBonus(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _peachBaseHealBonus += amount;
    }

    /// <summary>
    /// Core System 的公开入口：GetPeachHealAmountFor。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetPeachHealAmountFor(Player player)
    {
        var amount = player.Team == BattleTeam.Player
            ? PeachBaseHealAmount
            : BattleConstants.PeachHeal;

        if (player.Team == BattleTeam.Player)
        {
            amount = CurrentPlayerPeachBaseHealAmount;
            if (HasEquipment(EquipmentIds.ElfDust))
            {
                amount *= 2;
            }
        }
        else if (player is EnemyInstance enemy)
        {
            amount += 5 * enemy.CountEquipment(EquipmentIds.SproutingBonsai);
            if (enemy.HasEquipment(EquipmentIds.ElfDust))
            {
                amount *= 2;
            }
        }

        return amount;
    }

    /// <summary>
    /// Core System 的公开入口：ApplySoulPossession。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string ApplySoulPossession()
    {
        _defaultCharacterSkillsRemovedBySoulPossession = true;

        var preservedSkills = new List<string>();
        foreach (var skillId in AcquiredSkillIds)
        {
            if (SoulPossessionSkillIds.Contains(skillId))
            {
                preservedSkills.Add(skillId);
            }
        }

        AcquiredSkillIds.Clear();
        foreach (var skillId in preservedSkills)
        {
            AcquiredSkillIds.Add(skillId);
        }

        var destroyedEquipmentNames = InventoryManager.DestroyAllEquipment();

        var newlyGrantedSkillIds = new List<string>();
        GrantSoulPossessionSkill(SkillRarity.Legendary, newlyGrantedSkillIds);
        GrantSoulPossessionSkill(SkillRarity.Epic, newlyGrantedSkillIds);
        GrantSoulPossessionSkill(SkillRarity.Rare, newlyGrantedSkillIds);
        GrantSoulPossessionSkill(SkillRarity.Common, newlyGrantedSkillIds);

        var skillNames = new List<string>();
        foreach (var skillId in newlyGrantedSkillIds)
        {
            var skill = SkillDatabase.GetSkill(skillId);
            skillNames.Add(skill?.Name ?? skillId);
        }

        var destroyedText = destroyedEquipmentNames.Count > 0
            ? string.Join("、", destroyedEquipmentNames.ConvertAll(name => $"【{name}】"))
            : "无";
        var skillText = skillNames.Count > 0
            ? string.Join("、", skillNames.ConvertAll(name => $"【{name}】"))
            : "无可获得技能";

        return $"夺舍灵魂完成。\n已移除所有未受灵魂标记保护的技能，并摧毁全部装备：{destroyedText}。\n获得SoulPossessionSkill：{skillText}。";
    }

    private static void GrantSoulPossessionSkill(SkillRarity rarity, List<string> grantedSkillIds)
    {
        var skillId = GetRandomSkillIdByRarity(rarity, grantedSkillIds);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return;
        }

        AddAcquiredSkill(skillId, true);
        grantedSkillIds.Add(skillId);
    }

    private static string? GetRandomSkillIdByRarity(SkillRarity rarity, List<string> excludedThisRoll)
    {
        var candidates = new List<string>();
        foreach (var skill in SkillDatabase.All())
        {
            if (skill.Rarity != rarity)
            {
                continue;
            }

            if (AcquiredSkillIds.Contains(skill.Id) || excludedThisRoll.Contains(skill.Id))
            {
                continue;
            }

            candidates.Add(skill.Id);
        }

        return candidates.Count > 0 ? candidates[EventRewardRandom.Next(candidates.Count)] : null;
    }

    // ————————————————————————————————————————
    // 玩家牌型管理（事件触发的增减）
    // ————————————————————————————————————————

    /// <summary>
    /// Core System 的公开入口：HasPlayerCardType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasPlayerCardType(CardType type)
    {
        if (_removedPlayerCardTypes.Contains(type)) return false;
        return IsDefaultPlayerCardType(type) || _addedPlayerCardTypes.Contains(type);
    }

    /// <summary>
    /// 返回当前 Run 是否已明确移除指定牌型。
    ///
    /// 技能和装备直接提供的行动牌不属于默认牌池，不能用
    /// <see cref="HasPlayerCardType"/> 判断；出牌栏通过本方法统一尊重玩家的移除选择。
    /// </summary>
    public static bool IsPlayerCardTypeRemoved(CardType type) => _removedPlayerCardTypes.Contains(type);

    /// <summary>
    /// Core System 的公开入口：AddPlayerCardType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddPlayerCardType(CardType type) => _addedPlayerCardTypes.Add(type);

    /// <summary>
    /// 初始事件㉒专用的特殊锦囊授予入口。把候选范围收拢在状态层，避免 UI 回调
    /// 传入无关卡牌时仍污染出牌栏；成功时写入的仍是统一的本局新增牌型集合。
    /// </summary>
    public static bool TryAddInitialEventAttackTrick(CardType type)
    {
        if (type is not (CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.IronChain))
        {
            return false;
        }

        AddPlayerCardType(type);
        return true;
    }
    /// <summary>
    /// Core System 的公开入口：RemovePlayerCardType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RemovePlayerCardType(CardType type) => _removedPlayerCardTypes.Add(type);

    /// <summary>
    /// Core System 的公开入口：ReplacePlayerCardType。
    ///
    /// 原子化替换：一次调用同时完成"移除旧牌型+加入新牌型"，供事件等调用方在数据/
    /// 逻辑层面表达"这是一次替换"而不是两个独立、可能被拆散的操作。
    /// </summary>
    public static void ReplacePlayerCardType(CardType oldType, CardType newType)
    {
        RemovePlayerCardType(oldType);
        AddPlayerCardType(newType);
    }

    private static bool IsDefaultPlayerCardType(CardType type)
    {
        return type is CardType.Fee or CardType.Dodge or CardType.Kill
            or CardType.FireKill or CardType.ThunderKill
            or CardType.Peach or CardType.Wine or CardType.Unassailable;
    }

    // 石碑选项一：攻击锦囊伤害永久 +amount（叠加）。
    /// <summary>
    /// Core System 的公开入口：AddSteleAttackTrickDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddSteleAttackTrickDamageBonus(int amount)
    {
        _steleAttackTrickDamageBonus += amount;
    }

    // 石碑血祭随机：杀系伤害永久 +amount（叠加）。
    /// <summary>
    /// Core System 的公开入口：AddSteleKillDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddSteleKillDamageBonus(int amount)
    {
        _steleKillDamageBonus += amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddRunKillDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddRunKillDamageBonus(int amount)
    {
        _lifeSpringKillDamageBonus += amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddPaoxiaoPlayerKillBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddPaoxiaoPlayerKillBonus(int amount)
    {
        _paoxiaoPlayerKillBonus += amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddZhouTaiFenjiBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddZhouTaiFenjiBonus(int amount)
    {
        _zhouTaiFenjiBonus += amount;
    }

    // 招募援军：南蛮入侵本局永久伤害提升（百分比，累加）。
    /// <summary>
    /// Core System 的公开入口：AddNanmanDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddNanmanDamageBonus(int percentAmount)
    {
        _nanmanBonusPercent += percentAmount;
    }

    /// <summary>
    /// Core System 的公开入口：BeginManZuCampBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginManZuCampBattle()
    {
        _activeSpecialBattleId = "manzu_camp";
        ActiveSpecialBattleEnemyIds.Clear();
        ActiveSpecialBattleEnemyIds.Add("exile_barbarian");
        ActiveSpecialBattleEnemyIds.Add("exile_barbarian");
        CurrentRunState = RunState.Battle;
    }

    /// <summary>
    /// Core System 的公开入口：BeginChasingPursuersBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginChasingPursuersBattle()
    {
        _activeSpecialBattleId = "chasing_pursuers";
        ActiveSpecialBattleEnemyIds.Clear();
        ActiveSpecialBattleEnemyIds.Add("hunter");
        ActiveSpecialBattleEnemyIds.Add("hunter");
        ActiveSpecialBattleEnemyIds.Add("hunter");
        CurrentRunState = RunState.Battle;
    }

    /// <summary>
    /// Core System 的公开入口：BeginRatKingEventBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginRatKingEventBattle()
    {
        _activeSpecialBattleId = "rat_king_event";
        _ratKingEventPeachCount = 0;
        _ratKingEventPeacefulResolved = false;
        ActiveSpecialBattleEnemyIds.Clear();
        ActiveSpecialBattleEnemyIds.Add("rat_king");
        ActiveSpecialBattleEnemyIds.Add("giant_mech_rat");
        ActiveSpecialBattleEnemyIds.Add("giant_mech_rat");
        CurrentRunState = RunState.Battle;
    }

    public static bool RatKingEventPeacefulResolved => _ratKingEventPeacefulResolved;

    /// <summary>
    /// Core System 的公开入口：IncrementRatKingEventPeachCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void IncrementRatKingEventPeachCount()
    {
        if (_activeSpecialBattleId != "rat_king_event") return;
        _ratKingEventPeachCount++;
    }

    public static int RatKingEventPeachCount => _ratKingEventPeachCount;

    /// <summary>
    /// Core System 的公开入口：SetRatKingEventPeacefulResolved。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetRatKingEventPeacefulResolved() => _ratKingEventPeacefulResolved = true;

    /// <summary>
    /// Core System 的公开入口：BeginQiXingTanBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginQiXingTanBattle()
    {
        var bossId = ResolveQiXingTanBossEnemyId()
            ?? throw new InvalidOperationException("第一章 Boss 遭遇池中没有可用于七星坛招魂的 Boss。");

        // 旧实现把强化存成 RunBuff。清理旧存档可能遗留的同名 Buff，避免与本场
        // 特殊战斗实例倍率重复叠加；新倍率的生命周期完全跟随 ActiveSpecialBattle。
        RunBuffManager.RemoveAllStacks(RunBuffIds.QiXingTanBattle);
        _activeSpecialBattleId = "qixingtan";
        _activeSpecialBattleEnemyMaxHpMultiplier = 3.0;
        _activeSpecialBattleEnemyFinalDamageMultiplier = 2.0;
        ActiveSpecialBattleEnemyIds.Clear();
        ActiveSpecialBattleEnemyIds.Add(bossId);
        CurrentRunState = RunState.Battle;
    }

    /// <summary>
    /// 返回七星坛允许召魂的第一章 Boss 候选。
    ///
    /// 候选直接由第一章 Boss 关遭遇池推导，避免维护会与关卡数据失去同步的 Boss Id 名单。
    /// </summary>
    public static IReadOnlyList<string> GetQiXingTanBossCandidateIds()
    {
        var candidates = new List<string>();
        foreach (var encounter in GetAvailableBossEncounterPlans(1))
        {
            var bossId = FindBossEnemyId(encounter);
            if (!string.IsNullOrEmpty(bossId) && !candidates.Contains(bossId))
            {
                candidates.Add(bossId);
            }
        }

        return candidates;
    }

    private static string? ResolveQiXingTanBossEnemyId()
    {
        var candidates = GetQiXingTanBossCandidateIds();
        var defeatedBossId = GetDefeatedChapterBossEnemyId(1);
        if (!string.IsNullOrEmpty(defeatedBossId) && ContainsEnemyId(candidates, defeatedBossId))
        {
            return defeatedBossId;
        }

        // 兼容尚未写入实际击败记录的当前 Run：第一章战斗使用的锁定遭遇仍保存在计划中。
        if (PlannedChapterBossEncounterEnemyIds.TryGetValue(1, out var plannedEncounter))
        {
            var plannedBossId = FindBossEnemyId(plannedEncounter);
            if (!string.IsNullOrEmpty(plannedBossId) && ContainsEnemyId(candidates, plannedBossId))
            {
                return plannedBossId;
            }
        }

        return candidates.Count > 0
            ? candidates[EventRewardRandom.Next(candidates.Count)]
            : null;
    }

    private static string? FindBossEnemyId(IReadOnlyList<string> enemyIds)
    {
        foreach (var enemyId in enemyIds)
        {
            if (EnemyDatabase.GetEnemy(enemyId)?.Type == EnemyType.Boss)
            {
                return enemyId;
            }
        }

        return null;
    }

    private static bool ContainsEnemyId(IReadOnlyList<string> enemyIds, string expectedId)
    {
        foreach (var enemyId in enemyIds)
        {
            if (string.Equals(enemyId, expectedId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将当前特殊战斗的生命倍率应用到新创建的敌人运行时实例。
    ///
    /// 该方法只调用 EnemyInstance 的生命接口，不修改 EnemyDefinition 或数据库数据。
    /// </summary>
    public static void ApplyActiveSpecialBattleEnemyModifiers(EnemyInstance enemy)
    {
        if (_activeSpecialBattleId != "qixingtan"
            || _activeSpecialBattleEnemyMaxHpMultiplier <= 1.0
            || !ActiveSpecialBattleEnemyIds.Contains(enemy.Definition.Id)
            || enemy.RuntimeStates.ContainsKey("qixingtan_empowered"))
        {
            return;
        }

        var bonus = (int)Math.Ceiling(enemy.MaxHealth * (_activeSpecialBattleEnemyMaxHpMultiplier - 1.0));
        if (bonus > 0)
        {
            enemy.AddMaxHealth(bonus);
        }

        // 仅作运行时标记，供战斗日志、调试和回归测试确认这确实是招魂强化实例。
        enemy.RuntimeStates["qixingtan_empowered"] = true;
    }

    /// <summary>
    /// 发放七星坛招魂战斗的固定胜利奖励。
    ///
    /// 特殊 Boss 自身掉落由 MainFlow 丢弃；这里只发放事件规定的两枚灵魂石。
    /// </summary>
    public static void GrantQiXingTanVictoryReward()
    {
        AddEquipment(EquipmentIds.SoulStone);
        AddEquipment(EquipmentIds.SoulStone);
    }

    // 每章最多一次事件：本章是否已触发。
    /// <summary>
    /// Core System 的公开入口：HasSeenEventInCurrentChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasSeenEventInCurrentChapter(string eventId)
    {
        return SeenEventsByChapter.Contains($"{eventId}_ch{CurrentChapter}");
    }

    /// <summary>
    /// Core System 的公开入口：MarkEventSeenInCurrentChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkEventSeenInCurrentChapter(string eventId)
    {
        SeenEventsByChapter.Add($"{eventId}_ch{CurrentChapter}");
    }

    // ————————————————————————————————————————
    // 战斗掉落：BattleManager 胜利时写入，MainFlow 在 ShowReward 中消费。
    // ————————————————————————————————————————

    /// <summary>
    /// Core System 的公开入口：AddPendingBattleDrop。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddPendingBattleDrop(string equipmentId)
    {
        PendingBattleDropEquipmentIds.Add(equipmentId);
    }

    /// <summary>
    /// Core System 的公开入口：ConsumePendingBattleDrops。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<string> ConsumePendingBattleDrops()
    {
        var drops = new List<string>(PendingBattleDropEquipmentIds);
        PendingBattleDropEquipmentIds.Clear();
        return drops;
    }

    /// <summary>
    /// Core System 的公开入口：SetPendingBattleGold。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetPendingBattleGold(int gold)
    {
        _pendingBattleGold = gold;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumePendingBattleGold。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int ConsumePendingBattleGold()
    {
        var gold = _pendingBattleGold;
        _pendingBattleGold = 0;
        return gold;
    }

    // ————————————————————————————————————————

    // 记录”下一场战斗胜利后随机获得一件装备”。rarityPoolCsv 为逗号分隔的稀有度列表（如 “Common,Rare”）。
    /// <summary>
    /// Core System 的公开入口：SetPendingNextBattleEquipmentReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetPendingNextBattleEquipmentReward(string rarityPoolCsv)
    {
        _pendingNextBattleEquipmentRarityPool = string.IsNullOrWhiteSpace(rarityPoolCsv) ? "Common" : rarityPoolCsv;
    }

    // 取出并清除“下一场战斗胜利”随机装备奖励；没有待发放奖励时返回 null。
    /// <summary>
    /// Core System 的公开入口：ConsumePendingNextBattleEquipmentReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EquipmentDefinition? ConsumePendingNextBattleEquipmentReward()
    {
        if (!HasPendingNextBattleEquipmentReward)
        {
            return null;
        }

        var pool = _pendingNextBattleEquipmentRarityPool;
        _pendingNextBattleEquipmentRarityPool = string.Empty;
        return PickRandomEventEquipmentReward(pool);
    }

    // 记录“击败指定章节Boss后获得指定装备”。
    /// <summary>
    /// Core System 的公开入口：SetPendingChapterBossEquipmentReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetPendingChapterBossEquipmentReward(int chapter, string equipmentId)
    {
        _pendingChapterBossEquipmentChapter = chapter;
        _pendingChapterBossEquipmentId = equipmentId;
    }

    // 取出并清除“击败指定章节Boss”的装备奖励；章节不匹配或没有待发放奖励时返回 null。
    /// <summary>
    /// Core System 的公开入口：ConsumePendingChapterBossEquipmentReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EquipmentDefinition? ConsumePendingChapterBossEquipmentReward(int chapter)
    {
        if (string.IsNullOrEmpty(_pendingChapterBossEquipmentId) || _pendingChapterBossEquipmentChapter != chapter)
        {
            return null;
        }

        var equipment = EquipmentDatabase.GetEquipment(_pendingChapterBossEquipmentId);
        _pendingChapterBossEquipmentId = string.Empty;
        return equipment;
    }

    /// <summary>
    /// Core System 的公开入口：IsChapterBossDefeated。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsChapterBossDefeated(int chapter)
    {
        return DefeatedChapterBosses.Contains(chapter);
    }

    /// <summary>
    /// Core System 的公开入口：MarkChapterBossDefeated。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkChapterBossDefeated(int chapter)
    {
        DefeatedChapterBosses.Add(chapter);
        var bossId = FindBossEnemyId(GetOrCreateChapterBossEncounterEnemyIds(chapter));
        if (!string.IsNullOrEmpty(bossId))
        {
            DefeatedChapterBossEnemyIds[chapter] = bossId;
        }
    }

    /// <summary>
    /// 返回指定章节在当前 Run 中实际击败的主 Boss Id。
    ///
    /// 记录来自主 Boss 战已经锁定的遭遇，不会重新随机 Boss。
    /// </summary>
    public static string? GetDefeatedChapterBossEnemyId(int chapter)
    {
        return DefeatedChapterBossEnemyIds.TryGetValue(chapter, out var enemyId)
            ? enemyId
            : null;
    }

    // 随机池：StringValue 形如 "Common,Rare"。统一使用 CanAppearInRandomPool 与随机池规则过滤。
    private static EquipmentDefinition? PickRandomEventEquipmentReward(string rarityPoolCsv)
    {
        var allowedRarities = new HashSet<EquipmentRarity>();
        foreach (var token in rarityPoolCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<EquipmentRarity>(token, true, out var rarity))
            {
                allowedRarities.Add(rarity);
            }
        }

        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (!allowedRarities.Contains(definition.Rarity))
            {
                continue;
            }

            if (!RewardManager.CanAppearInRandomEquipmentReward(definition))
            {
                continue;
            }

            candidates.Add(definition);
        }

        return candidates.Count > 0 ? candidates[EventRewardRandom.Next(candidates.Count)] : null;
    }

    /// <summary>
    /// Core System 的公开入口：MarkCurrentNodeCleared。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void MarkCurrentNodeCleared()
    {
        if (string.IsNullOrWhiteSpace(CurrentNodeId))
        {
            return;
        }

        var node = GetNode(CurrentNodeId);
        if (node == null || ClearedNodeIds.Contains(CurrentNodeId))
        {
            return;
        }

        ClearedNodeIds.Add(CurrentNodeId);
        if (node != null)
        {
            foreach (var nextNodeId in node.NextNodeIds)
            {
                UnlockedNodeIds.Add(nextNodeId);
                var unlockedNode = GetNode(nextNodeId);
                if (unlockedNode != null && (unlockedNode.Type is MapNodeType.Battle or MapNodeType.Elite or MapNodeType.Boss))
                {
                    UnlockedStages.Add(unlockedNode.StageIndex);
                }
            }

            // The HUD represents the next map decision, not the battle that was
            // just completed.  Keep CurrentNodeId until the player actually picks
            // that node so reward/event code can still identify the cleared node.
            foreach (var nextNodeId in node.NextNodeIds)
            {
                var nextProgressNode = GetNode(nextNodeId);
                if (nextProgressNode != null && IsNodeUnlocked(nextProgressNode.Id) && !IsNodeCleared(nextProgressNode.Id))
                {
                    CurrentStage = nextProgressNode.StageIndex;
                    break;
                }
            }
        }

        // 电量系统：精英战清完后开放Boss入口+首批自由探索节点；
        // 之后每完成一个自由探索节点，整批未选节点一起作废、重新刷新一批。
        // 泛化到所有章节：不再按 CurrentChapter==1 / 写死的 "battle_5" 判断，
        // 而是按节点类型（Elite）通用触发，四个章节建图方法产出的形状完全一致。
        if (node is { Type: MapNodeType.Elite } && !ContinueExploreManager.IsActive)
        {
            ContinueExploreManager.Enter(CurrentChapter);
        }
        else if (ContinueExploreManager.IsActive && node is { IsContinueExploring: true })
        {
            ContinueExploreManager.RegenerateChoices(CurrentChapter);
        }

        // 图鉴：只在真正首次进入 Victory 状态时记一次通关，避免这个方法后续再被调用
        // （理论上不应该，但作为防御）时重复计数。
        var justWon = CurrentRunState != RunState.Victory && AllStagesCleared();
        CurrentRunState = AllStagesCleared() ? RunState.Victory : RunState.Map;
        if (justWon)
        {
            CodexService.RecordCharacterRunCompleted(CurrentCharacterId);
        }
    }

    // 新获得的装备进入背包，不会自动装备；需在背包界面手动拖入装备槽才会生效。
    /// <summary>
    /// Core System 的公开入口：AddEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static OwnedEquipment? AddEquipment(
        EquipmentDefinition equipment,
        EquipmentGainSource source = EquipmentGainSource.GameplayReward)
    {
        return AddEquipment(equipment.Id, source);
    }

    /// <summary>
    /// Core System 的公开入口：AddEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static OwnedEquipment? AddEquipment(
        string equipmentId,
        EquipmentGainSource source = EquipmentGainSource.GameplayReward)
    {
        var original = EquipmentDatabase.GetEquipment(equipmentId);
        if (original == null)
        {
            return null;
        }

        // 候选必须在原装备被移除或替换前确定。这里发生在正式入包之前，因此无候选时完全
        // 不改变原奖励；有候选时，背包只接收最终装备，后续工坊重选等获得后处理也只会看到它。
        var replacement = FindInitialEventUpgradeCandidate(original, source);
        var initialEventUpgrade = replacement != null;
        var qunFateUpgrade = false;

        // 初始事件的“一件品质跃迁”优先于群·淬炼开局：两者同时存在时，先完整结算
        // 已选初始事件，不让同一件装备在一次获得中连续跳两个品质；群命运次数会留给下一件。
        if (replacement == null
            && FactionFateManager.TryFindQunEquipmentUpgradeCandidate(original, source, out var qunReplacement))
        {
            replacement = qunReplacement;
            qunFateUpgrade = replacement != null;
        }

        var finalDefinition = replacement ?? original;
        var finalSource = replacement == null
            ? source
            : initialEventUpgrade
                ? EquipmentGainSource.QualityUpgradeReplacement
                : EquipmentGainSource.FactionFateReplacement;
        var added = InventoryManager.AddToInventory(finalDefinition.Id, finalSource);

        // 板甲：装备后立即获得15点生命值。
        if (added?.Definition.Id == EquipmentIds.BanJia)
            AddCurrentHp(15);
        // Hero Unlock System：累计"获得装备"成就进度，供【达成指定成就】类型的解锁条件使用。
        HeroUnlockProgress.AddAchievementProgress(AchievementType.EquipmentAcquired, 1);

        // 只有最终装备确实进入背包后才提交状态。若其它前置效果拒绝了获得，品质跃迁仍保持待触发。
        if (initialEventUpgrade && replacement != null && added != null)
        {
            _initialEventUpgradeFirstEquipTriggered = true;
            _initialEventUpgradeFirstEquipArmed = false;
            InitialEventQualityUpgradeTriggered?.Invoke(original, replacement);
        }

        if (qunFateUpgrade && added != null)
        {
            FactionFateManager.CommitQunEquipmentUpgrade();
        }

        return added;
    }

    // 仅统计已装备（装备槽中）的数量，背包中未装备的同名装备不计入，因此战斗系统只读取当前装备槽中的装备。
    /// <summary>
    /// Core System 的公开入口：HasEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasEquipment(string equipmentId)
    {
        return InventoryManager.CountEquipped(equipmentId) > 0;
    }

    /// <summary>
    /// Core System 的公开入口：OwnsEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool OwnsEquipment(string equipmentId)
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Id == equipmentId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断玩家是否拥有一件"当前生效"的指定装备——统一走
    /// <see cref="InventoryManager.IsActive"/>：默认（EquippedOnly）装备必须已装备，
    /// 背包生效（Inventory）装备只要拥有就算生效。
    ///
    /// 这是一个通用查询，不针对任何具体装备写特殊判断；黄金雕像等背包生效装备的效果
    /// 应该调用这个方法判断是否生效，而不是各自重新实现"背包生效"逻辑。
    /// </summary>
    public static bool HasActiveEquipment(string equipmentId)
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Id == equipmentId && InventoryManager.IsActive(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Core System 的公开入口：CountEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int CountEquipment(string equipmentId)
    {
        return InventoryManager.CountEquipped(equipmentId);
    }

    /// <summary>
    /// Core System 的公开入口：GetRemainingEquipmentUses。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetRemainingEquipmentUses(string equipmentId)
    {
        EquipmentUseCounts.TryGetValue(equipmentId, out var usedCount);
        return Math.Max(0, CountEquipment(equipmentId) - usedCount);
    }

    /// <summary>
    /// Core System 的公开入口：TryConsumeEquipmentUse。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TryConsumeEquipmentUse(string equipmentId)
    {
        if (GetRemainingEquipmentUses(equipmentId) <= 0)
        {
            return false;
        }

        EquipmentUseCounts.TryGetValue(equipmentId, out var usedCount);
        EquipmentUseCounts[equipmentId] = usedCount + 1;
        return true;
    }

    /// <summary>
    /// Core System 的公开入口：BeginTreasurePavilionBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void BeginTreasurePavilionBattle()
    {
        _activeSpecialBattleId = "treasure_pavilion";
        ActiveSpecialBattleEnemyIds.Clear();
        ActiveSpecialBattleEnemyIds.Add("giant_rolling_stone");
        ActiveSpecialBattleEnemyIds.Add("giant_rolling_log");
        CurrentRunState = RunState.Battle;
    }

    /// <summary>
    /// Core System 的公开入口：GetActiveSpecialBattleEnemyIds。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<string> GetActiveSpecialBattleEnemyIds()
    {
        return ActiveSpecialBattleEnemyIds;
    }

    /// <summary>
    /// Core System 的公开入口：CompleteSpecialBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void CompleteSpecialBattle()
    {
        _activeSpecialBattleId = string.Empty;
        ActiveSpecialBattleEnemyIds.Clear();
        _activeSpecialBattleEnemyMaxHpMultiplier = 1.0;
        _activeSpecialBattleEnemyFinalDamageMultiplier = 1.0;
    }

    /// <summary>
    /// Core System 的公开入口：HandleTreasurePavilionBattleLoss。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HandleTreasurePavilionBattleLoss()
    {
        Gold = 0;
        CurrentHP = 0;
        CompleteSpecialBattle();
        RunBuffManager.ConsumeBattle();

        // 与普通战败完全一致：最后1点粮草可用于本次复活；粮草为0后下一次死亡
        // 才会结束 Run。
        if (Forage <= 0)
        {
            Forage = 0;
            CurrentRunState = RunState.GameOver;
            CodexService.RecordCharacterDeath(CurrentCharacterId);
            return false;
        }

        Forage -= 1;
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstForageSpent);

        RecoverHealthAfterBattle();
        CurrentRunState = RunState.Map;
        return true;
    }

    /// <summary>
    /// Core System 的公开入口：CreateRandomEpicEquipmentChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> CreateRandomEpicEquipmentChoices(int count)
    {
        return CreateRandomEquipmentChoices(EquipmentRarity.Epic, count);
    }

    /// <summary>
    /// Core System 的公开入口：CreateRandomRareEquipmentChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> CreateRandomRareEquipmentChoices(int count)
    {
        return CreateRandomEquipmentChoices(EquipmentRarity.Rare, count);
    }

    /// <summary>
    /// Core System 的公开入口：CreateRandomLegendaryEquipmentChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> CreateRandomLegendaryEquipmentChoices(int count)
    {
        return CreateRandomEquipmentChoices(EquipmentRarity.Legendary, count);
    }

    /// <summary>
    /// Core System 的公开入口：CreateRandomRareOrEpicEquipmentChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> CreateRandomRareOrEpicEquipmentChoices(int count)
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity != EquipmentRarity.Rare && definition.Rarity != EquipmentRarity.Epic)
                continue;
            if (!RewardManager.CanAppearInRandomEquipmentReward(definition))
                continue;
            candidates.Add(definition);
        }

        var choices = new List<EquipmentDefinition>();
        while (choices.Count < count && candidates.Count > 0)
        {
            var index = EventRewardRandom.Next(candidates.Count);
            choices.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        if (choices.Count > 0 && choices.Count < count)
        {
            while (choices.Count < count)
                choices.Add(choices[EventRewardRandom.Next(choices.Count)]);
        }

        return choices;
    }

    /// <summary>
    /// Core System 的公开入口：CreateRandomEquipmentChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> CreateRandomEquipmentChoices(EquipmentRarity rarity, int count)
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity != rarity)
            {
                continue;
            }

            if (!RewardManager.CanAppearInRandomEquipmentReward(definition))
            {
                continue;
            }

            candidates.Add(definition);
        }

        var choices = new List<EquipmentDefinition>();
        while (choices.Count < count && candidates.Count > 0)
        {
            var index = EventRewardRandom.Next(candidates.Count);
            choices.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        if (choices.Count > 0 && choices.Count < count)
        {
            while (choices.Count < count)
            {
                choices.Add(choices[EventRewardRandom.Next(choices.Count)]);
            }
        }

        return choices;
    }

    private static void ResetRunData()
    {
        BattleLogRuntime.Service.BeginRun();
        CurrentChapter = 1;
        CurrentStage = -1;
        CurrentCharacterId = string.Empty;
        Forage = InitialForage;
        Gold = InitialGold;
        ContinueExploreManager.ResetForNewRun();
        MaxHP = 0;
        CurrentHP = 0;
        TempHp = 0;
        CurrentMana = InitialMana;
        CurrentNodeId = string.Empty;
        DefeatedEnemyCount = 0;
        AcquiredSkillIds.Clear();
        SoulPossessionSkillIds.Clear();
        InventoryManager.Reset();
        EquipmentUseCounts.Clear();
        UnlockedStages.Clear();
        ClearedStages.Clear();
        SeenRunEventIds.Clear();
        ClearedNodeIds.Clear();
        UnlockedNodeIds.Clear();
        ClaimedRewardNodeIds.Clear();
        MapNodes.Clear();
        DebugMapEnabled = false;
        DefeatedChapterBosses.Clear();
        PlannedChapterBossEncounterEnemyIds.Clear();
        DefeatedChapterBossEnemyIds.Clear();
        _pendingNextBattleEquipmentRarityPool = string.Empty;
        _pendingChapterBossEquipmentChapter = 0;
        _pendingChapterBossEquipmentId = string.Empty;
        PendingBattleDropEquipmentIds.Clear();
        ActiveSpecialBattleEnemyIds.Clear();
        _pendingBattleGold = 0;
        _activeSpecialBattleId = string.Empty;
        _activeSpecialBattleEnemyMaxHpMultiplier = 1.0;
        _activeSpecialBattleEnemyFinalDamageMultiplier = 1.0;
        _ratKingEventPeachCount = 0;
        _ratKingEventPeacefulResolved = false;
        _defenseChipCount = 0;
        _attackChipCount = 0;
        _knowledgeChipCount = 0;
        _expansionChipCount = 0;
        _stinkyMushroomColorAssignment = null;
        _preBattleHP = 0;
        _preBattleMana = 0;
        _removedPlayerCardTypes.Clear();
        _addedPlayerCardTypes.Clear();
        _steleAttackTrickDamageBonus = 0;
        _steleKillDamageBonus = 0;
        _lifeSpringKillDamageBonus = 0;
        _paoxiaoPlayerKillBonus = 0;
        _zhouTaiFenjiBonus = 0;
        _wineDamageMultiplierBonus = 0;
        _peachBaseHealBonus = 0;
        _defaultCharacterSkillsRemovedBySoulPossession = false;
        _nanmanBonusPercent = 0;
        _hasExplosiveFruitAccessorySlot = false;
        _arrowBarrageFireUpgraded = false;
        _playerAttackAttributeBonus = AttackAttribute.None;
        _moonGemElementallyAwakened = false;
        SeenEventsByChapter.Clear();
        _chapterRoute = ChapterRoute.Default;
        _chapterVariant = ChapterVariant.None;
        _curseBladeCurrentCurseAmplified = false;
        _hasFreeWineRevive = false;
        RunBuffManager.Reset();
        // 初始事件持久效果重置
        _initialEventCh2SkillPick = false;
        _initialEventNoBattleEndRecovery = false;
        _initialEventAutoDestroyEquip = false;
        _initialEventAutoDestroyCount = 0;
        _initialEventAutoDestroyRewardPending = false;
        _initialEventLegendaryEnemyHpBonus = false;
        _initialEventCh1BossSkillPick = false;
        _initialEventShopRefreshFree = false;
        _initialEventAllShopsBlackMarket = false;
        _initialEventChipChoiceEnhanced = false;
        _initialEventBattleEndMaxHpGain = 0;
        _initialEventCh1BossExpansionChip = false;
        _initialEventMaxPowerBonus = 0;
        _maxPowerBonus = 0;
        _factionFateMaxPowerBonus = 0;
        _initialEventExtraShopRefreshCount = 0;
        _initialEventBattleEndFullHeal = false;
        _pendingCollapseVictoryHpOverride = null;
        _initialEventNoDamageTrackActive = false;
        _initialEventNoDamageBattleCount = 0;
        _initialEventNoDamageFailed = false;
        _initialEventNoDamageRewardPending = false;
        _initialEventFirstBattleElite = false;
        _initialEventCh1Event2Ch2 = false;
        _initialEventIe06RarePending = false;
        _initialEventUpgradeFirstEquipSelected = false;
        _initialEventUpgradeFirstEquipArmed = false;
        _initialEventUpgradeFirstEquipTriggered = false;
        _initialEventFortuneContractPending = false;
        _initialEventAttackTrickChoicePending = false;
        _initialEventStartingManaBonus = 0;
        // 放在所有 _initialEventXxx 重置之后：ResetPowerForChapter 读取的 MaxPower
        // 依赖 _initialEventMaxPowerBonus 已经清零，否则新开局会把上一局的电量上限带过来。
        ResetPowerForChapter();
        MapRunState.ResetForNewRun();
        // 阵营命运是跨越整个 Run 的全局效果，必须随 Run 重置一并清空——放在最后一行，
        // 保证清空时不依赖上面任何字段的重置顺序。
        FactionFateManager.ResetForNewRun();
    }

    private static void BuildChapterTwoDefaultMap()
    {
        MapNodes.Clear();
        MapNodes.Add(new MapNode
        {
            Id = "battle_2_1",
            Name = "第二章第一战",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_2_2" }
        });
        var event22 = new MapNode
        {
            Id = "event_2_2",
            Name = "事件一",
            Type = MapNodeType.Event,
            StageIndex = 1,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_2_3" }
        };
        // 第二/三章的固定事件节点属于新电量规则范围，电量取决于实际会触发的事件品质；
        // 要让地图上"点击前"就能正确显示，必须在建图时就选定具体事件（与自由探索节点
        // 完全同样的写法），而不是像第一章前五关那样等玩家点进去才现场随机。
        event22.FixedEventId = EventManager.GetEventForNode(event22)?.Id;
        MapNodes.Add(event22);
        MapNodes.Add(new MapNode
        {
            Id = "battle_2_3",
            Name = "第二章第三战",
            Type = MapNodeType.Battle,
            StageIndex = 2,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_2_4" }
        });
        var event24 = new MapNode
        {
            Id = "event_2_4",
            Name = "事件二",
            Type = MapNodeType.Event,
            StageIndex = 3,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_2_5" }
        };
        event24.FixedEventId = EventManager.GetEventForNode(event24)?.Id;
        MapNodes.Add(event24);
        MapNodes.Add(new MapNode
        {
            Id = "battle_2_5",
            Name = "精英战",
            Type = MapNodeType.Elite,
            StageIndex = 4,
            PowerCost = ContinueExploreConfig.ElitePowerCost
            // 无 NextNodeIds：精英战清完后交给自由探索接管，见 BuildDefaultMap 的注释。
        });
        MapNodes.Add(new MapNode
        {
            Id = "boss_2",
            Name = "首领战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            PowerCost = ContinueExploreConfig.BossPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.BossPowerCost
        });

        UnlockedNodeIds.Add("battle_2_1");
        UnlockedStages.Add(0);
    }

    private static void BuildSewerChapterTwoMap()
    {
        MapNodes.Clear();
        MapNodes.Add(new MapNode
        {
            Id = "sewer_battle_2_1",
            Name = "下水道・第一战",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "sewer_event_2_2" }
        });
        var sewerEvent22 = new MapNode
        {
            Id = "sewer_event_2_2",
            Name = "事件一",
            Type = MapNodeType.Event,
            StageIndex = 1,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "sewer_battle_2_3" }
        };
        sewerEvent22.FixedEventId = EventManager.GetEventForNode(sewerEvent22)?.Id;
        MapNodes.Add(sewerEvent22);
        MapNodes.Add(new MapNode
        {
            Id = "sewer_battle_2_3",
            Name = "下水道・第三战",
            Type = MapNodeType.Battle,
            StageIndex = 2,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "sewer_event_2_4" }
        });
        var sewerEvent24 = new MapNode
        {
            Id = "sewer_event_2_4",
            Name = "事件二",
            Type = MapNodeType.Event,
            StageIndex = 3,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "sewer_battle_2_5" }
        };
        sewerEvent24.FixedEventId = EventManager.GetEventForNode(sewerEvent24)?.Id;
        MapNodes.Add(sewerEvent24);
        MapNodes.Add(new MapNode
        {
            Id = "sewer_battle_2_5",
            Name = "下水道・精英战",
            Type = MapNodeType.Elite,
            StageIndex = 4,
            PowerCost = ContinueExploreConfig.ElitePowerCost
            // 无 NextNodeIds：精英战清完后交给自由探索接管，见 BuildDefaultMap 的注释。
        });
        MapNodes.Add(new MapNode
        {
            Id = "sewer_boss_2",
            Name = "首领战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            PowerCost = ContinueExploreConfig.BossPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.BossPowerCost
        });

        UnlockedNodeIds.Add("sewer_battle_2_1");
        UnlockedStages.Add(0);
    }

    private static void BuildMapForCurrentChapter()
    {
        switch (CurrentChapter)
        {
            case 2:
                if (_chapterRoute == ChapterRoute.Sewer)
                {
                    BuildSewerChapterTwoMap();
                }
                else
                {
                    BuildChapterTwoDefaultMap();
                }
                break;
            case 3:
                // 第三章当前只有一套战斗内容，但地图不再强制展示为“皇宫路线”。
                // 这里保留底层路线为 Imperial，避免第三章遭遇池被 Sewer/Default 路线过滤为空。
                _chapterRoute = ChapterRoute.Imperial;
                BuildChapterThreeMap();
                break;
            case 4:
                BuildChapterFourMap();
                break;
            default:
                BuildDefaultMap();
                break;
        }
    }

    private static void BuildChapterThreeMap()
    {
        MapNodes.Clear();
        MapNodes.Add(new MapNode
        {
            Id = "battle_3_1",
            Name = "第三章・第一战",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_3_2" }
        });
        var event32 = new MapNode
        {
            Id = "event_3_2",
            Name = "事件一",
            Type = MapNodeType.Event,
            StageIndex = 1,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_3_3" }
        };
        event32.FixedEventId = EventManager.GetEventForNode(event32)?.Id;
        MapNodes.Add(event32);
        MapNodes.Add(new MapNode
        {
            Id = "battle_3_3",
            Name = "第三章・第三战",
            Type = MapNodeType.Battle,
            StageIndex = 2,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_3_4" }
        });
        var event34 = new MapNode
        {
            Id = "event_3_4",
            Name = "事件二",
            Type = MapNodeType.Event,
            StageIndex = 3,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_3_5" }
        };
        event34.FixedEventId = EventManager.GetEventForNode(event34)?.Id;
        MapNodes.Add(event34);
        MapNodes.Add(new MapNode
        {
            Id = "battle_3_5",
            Name = "第三章・精英战",
            Type = MapNodeType.Elite,
            StageIndex = 4,
            PowerCost = ContinueExploreConfig.ElitePowerCost
            // 无 NextNodeIds：精英战清完后交给自由探索接管，见 BuildDefaultMap 的注释。
        });
        MapNodes.Add(new MapNode
        {
            Id = "boss_3",
            Name = "第三章・首领战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            PowerCost = ContinueExploreConfig.BossPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.BossPowerCost
        });

        UnlockedNodeIds.Add("battle_3_1");
        UnlockedStages.Add(0);
    }

    // 第四章·深渊：普通战斗①→深渊呼唤→普通战斗②→元素祭坛→精英→Boss。
    // 形状与第二/三章完全一致：普通战斗①→事件①→普通战斗②→事件②→精英→Boss。
    // battle_4_1/battle_4_3 在这里立刻各随机roll一次编队并缓存到 FixedEncounterEnemyIds
    // 上（复用 StageDatabase.RollEncounter，与 event32.FixedEventId 是同一个"建图时锁定，
    // 之后只读缓存、不再重新随机"模式），保证同一个节点进入后编队固定。
    private const string AbyssCallEventId = "abyss_call";
    private const string ElementalAltarEventId = "elemental_altar";

    private static void BuildChapterFourMap()
    {
        MapNodes.Clear();
        var battle41 = new MapNode
        {
            Id = "battle_4_1",
            Name = "第四章・第一战",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_4_2" }
        };
        battle41.FixedEncounterEnemyIds = StageDatabase.RollEncounter("4-1")?.EnemyIds;
        MapNodes.Add(battle41);

        var event42 = new MapNode
        {
            Id = "event_4_2",
            Name = "事件一",
            Type = MapNodeType.Event,
            StageIndex = 1,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_4_3" }
        };
        // 第四章的两个正式、不可重复事件分别锁定在两个深渊节点，避免混入其它章节事件池。
        event42.FixedEventId = AbyssCallEventId;
        MapNodes.Add(event42);

        var battle43 = new MapNode
        {
            Id = "battle_4_3",
            Name = "第四章・第三战",
            Type = MapNodeType.Battle,
            StageIndex = 2,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_4_4" }
        };
        battle43.FixedEncounterEnemyIds = StageDatabase.RollEncounter("4-3")?.EnemyIds;
        MapNodes.Add(battle43);

        var event44 = new MapNode
        {
            Id = "event_4_4",
            Name = "事件二",
            Type = MapNodeType.Event,
            StageIndex = 3,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_4_5" }
        };
        event44.FixedEventId = ElementalAltarEventId;
        MapNodes.Add(event44);

        MapNodes.Add(new MapNode
        {
            Id = "battle_4_5",
            Name = "第四章・精英战",
            Type = MapNodeType.Elite,
            StageIndex = 4,
            PowerCost = ContinueExploreConfig.ElitePowerCost
            // 无 NextNodeIds：精英战清完后交给自由探索接管，见 BuildDefaultMap 的注释。
        });
        MapNodes.Add(new MapNode
        {
            Id = "boss_4",
            Name = "第四章・首领战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            PowerCost = ContinueExploreConfig.BossPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.BossPowerCost
        });

        UnlockedNodeIds.Add("battle_4_1");
        UnlockedStages.Add(0);
    }

    // 第一章固定流程（电量系统）：
    // 初始抉择 → 普通敌人①(⚡10) → 事件一(⚡10) → 普通敌人②(⚡10) → 事件二(⚡10) → 精英(⚡20)
    // → [自由探索阶段，见 ContinueExploreManager.Enter/RegenerateChoices]
    // → Boss（不消耗电量，全程可进）。
    // 精英战之后不再预先铺好固定节点——原来的"事件三/额外事件/商店"固定尾巴
    // 被自由探索动态生成的节点取代。Boss 节点建图时就已声明好（保持锁定），
    // 由 ContinueExploreManager.Enter 在精英战清完时解锁，四个章节建图方法统一同一形状。
    /// <summary>
    /// 原本是 private——本次整合式教程新增的"跳过"入口需要在角色已选定、教程
    /// 中途中断的情况下切回真实第一章地图形状（教程流程内部用的是独立的
    /// <see cref="BuildTutorialMap"/>），因此放开为 public；不重置玩家已经在
    /// 教程里获得的金币/装备/角色，只重建地图节点本身。
    /// </summary>
    public static void BuildDefaultMap()
    {
        MapNodes.Clear();
        // 第一章前五关（initial_event/battle_1/event_2/battle_3/event_4）是历史固定消耗，
        // 不参与新电量规则——UseFixedEnergyCost=true 让 GameManager.GetNodeEnergyCost
        // 直接返回 FixedEnergyCost，无论以后新规则怎么调整都不会波及这五个节点。
        MapNodes.Add(new MapNode
        {
            Id = "initial_event",
            Name = "初始抉择",
            Type = MapNodeType.Event,
            StageIndex = -1,
            UseFixedEnergyCost = true,
            FixedEnergyCost = 0,
            NextNodeIds = new List<string> { "battle_1" }
        });
        MapNodes.Add(new MapNode
        {
            Id = "battle_1",
            Name = "普通敌人①",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_2" }
        });
        MapNodes.Add(new MapNode
        {
            Id = "event_2",
            Name = "事件一",
            Type = MapNodeType.Event,
            StageIndex = 1,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_3" }
        });
        MapNodes.Add(new MapNode
        {
            Id = "battle_3",
            Name = "普通敌人②",
            Type = MapNodeType.Battle,
            StageIndex = 2,
            PowerCost = ContinueExploreConfig.NormalBattlePowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.NormalBattlePowerCost,
            NextNodeIds = new List<string> { "event_4" }
        });
        MapNodes.Add(new MapNode
        {
            Id = "event_4",
            Name = "事件二",
            Type = MapNodeType.Event,
            StageIndex = 3,
            PowerCost = ContinueExploreConfig.NormalEventPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.NormalEventPowerCost,
            NextNodeIds = new List<string> { "battle_5" }
        });
        MapNodes.Add(new MapNode
        {
            Id = "battle_5",
            Name = "精英战",
            Type = MapNodeType.Elite,
            StageIndex = 4,
            PowerCost = ContinueExploreConfig.ElitePowerCost
            // 无 NextNodeIds：精英战清完后由 MarkCurrentNodeCleared 里的
            // ContinueExploreManager.Enter 特判触发自由探索，不走固定链路的自动解锁。
        });
        MapNodes.Add(new MapNode
        {
            Id = "boss_1",
            Name = "首领战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            PowerCost = ContinueExploreConfig.BossPowerCost,
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.BossPowerCost
            // 建图时就声明好，但不加入 UnlockedNodeIds：精英战清完前保持锁定。
        });

        UnlockedNodeIds.Add("initial_event");
    }

    // 整合式教程专用的两节点小地图（教学战斗+教学事件）：完全独立于正式章节的
    // BuildMapForCurrentChapter switch，只由 IntegratedTutorialFlow 在需要时直接
    // 调用；不影响 TotalChapters/BuildDefaultMap 等任何既有章节建图逻辑。
    // 教学战斗节点10电量（对应用户§8"进入消耗：10电量"的真实演示），教学事件
    // 节点固定0电量（教学范围内不需要第二次电量演示）。
    public static void BuildTutorialMap()
    {
        MapNodes.Clear();

        MapNodes.Add(new MapNode
        {
            Id = IntegratedTutorialFlow.TutorialBattleNodeId,
            Name = "教学战斗",
            Type = MapNodeType.Battle,
            StageIndex = 0,
            UseFixedEnergyCost = true,
            FixedEnergyCost = 10,
            NextNodeIds = new List<string> { IntegratedTutorialFlow.TutorialEventNodeId }
        });

        var eventNode = new MapNode
        {
            Id = IntegratedTutorialFlow.TutorialEventNodeId,
            Name = "教学事件",
            Type = MapNodeType.Event,
            StageIndex = 1,
            UseFixedEnergyCost = true,
            FixedEnergyCost = 0
        };
        eventNode.FixedEventId = IntegratedTutorialFlow.TutorialEventId;
        MapNodes.Add(eventNode);

        UnlockedNodeIds.Add(IntegratedTutorialFlow.TutorialBattleNodeId);
    }
}
