//////////////////////////////////////////////////////////
// 文件：Scripts/Player.cs
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
using System.Linq;

/// <summary>
/// Core System 的公开枚举：GuanxingPhase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum GuanxingPhase
{
    None,
    Recording,
    Repeating
}

/// <summary>
/// Core System 的公开类：Player。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class Player : BattleUnit
{
    private const int GuanxingRepeatTurnCount = 2;

    private static readonly CharacterData WhiteboardCharacterData = new()
    {
        Id = "whiteboard",
        Name = string.Empty,
        Gender = Gender.Male,
        Faction = Faction.Qun,
        MaxHp = BattleConstants.InitialHealth
    };

    private int _currentHealth = BattleConstants.InitialHealth;
    private int _maxHealth = BattleConstants.InitialHealth;

    /// <summary>
    /// Core System 的公开入口：Player。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Player(string displayName, string? id = null, BattleTeam team = BattleTeam.Player)
    {
        DisplayName = displayName;
        UnitId = string.IsNullOrWhiteSpace(id) ? displayName : id;
        Team = team;
    }

    public string UnitId { get; }
    public string DisplayName { get; private set; }
    public CharacterInstance Character { get; private set; } = new(WhiteboardCharacterData);
    public int Health => _currentHealth;
    public int MaxHealth => _maxHealth;
    public string CharacterName => Character.Data.Name;
    public double CurrentMana { get; private set; } = BattleConstants.InitialMana;
    public double ProtectedStealMana { get; private set; }
    public int WinePower { get; private set; }
    public int PendingWinePower { get; private set; }
    public bool HasUsedWineRevive { get; private set; }
    // 濒死救援中的【桃】次数：首张按正常费用，此后每次救援费用都在上一次基础上翻倍。
    public int DyingPeachReviveUses { get; private set; }
    // 连营重新设计：不再是"整场战斗只触发一次"的永久栓，而是"费用非0→0"
    // 这一次转变触发一次、随后必须先让费用离开0（GainMana 检测到 0→非0
    // 时调用 RearmLianying 重新置为 true）才能再次触发的可重复循环栓。
    // 默认 true（战斗开始时就处于"可触发"状态）。
    public bool LianyingArmed { get; private set; } = true;
    public bool LianyingPrepared { get; private set; }
    public bool LianyingFreeKillAvailable { get; private set; }
    // 白马：每场战斗第一次打出普通杀时费用为0；每场战斗开始时重置为 true。
    public bool WhiteHorseFreeKillAvailable { get; private set; }
    // 影袭（黄月英专属）：每场战斗第一次激活影袭时费用为0；每场战斗开始时重置为 true。
    public bool YingXiFirstUseFreeAvailable { get; private set; }
    public bool DiLuFirstDamageImmuneAvailable { get; private set; }
    public bool ZhuaHuangFreeAttackAvailable { get; private set; }
    // 铁卫重甲：每场战斗首次受到超过20点伤害时归零，每场战斗开始时重置为 true。
    public bool IronHeavyArmorActive { get; private set; }
    // 喷气式狼牙棒：每场战斗首次攻击三倍伤害，每场战斗开始时重置为 true。
    public bool JetMaceActive { get; private set; }
    // 钳制机械外骨骼：生命首次跌至50%以下时触发，每场战斗重置为 true。
    public bool ClampExoskeletonActive { get; private set; }
    // 钳制机械外骨骼修复待机：触发后置 true，下一回合开始时全量恢复生命。
    public bool ClampExoskeletonRepairPending { get; private set; }
    // 短弓：本场战斗已触发次数（0–3，超过3后失效）及本回合是否已触发。
    public int ShortBowBattleTriggers { get; private set; }
    public bool ShortBowTriggeredThisRound { get; private set; }
    // 长弓：本场战斗第一张普通杀效果是否已消耗。
    public bool LongBowUsed { get; private set; }
    public DyingState DyingState { get; private set; } = DyingState.Alive;
    public List<Skill> Skills { get; } = new();
    public GuanxingPhase GuanxingPhase { get; private set; } = GuanxingPhase.None;
    public int GuanxingRepeatsRemaining { get; private set; }
    public CardType? GuanxingRecordedCardType { get; private set; }
    public int GuanxingRecordedCount { get; private set; }
    public bool JiGuTriggered { get; private set; }
    // 击鼓反应已经入队、尚未由玩家选择时的短暂锁；放弃后解除，使下一次实际受伤仍可再次触发。
    public bool JiGuReactionPending { get; private set; }
    public bool JiGuActive { get; private set; }
    public int JiGuTurnsRemaining { get; private set; }
    public bool WarDrumFirstAttackPlayed { get; private set; }
    public bool WarDrumPending { get; private set; }
    public bool WarDrumActive { get; private set; }
    public bool WarDrumAttackedThisTurn { get; private set; }
    // 隐身模块：战斗开始时获得无敌，第10回合开始时或本方打出费/杀类型牌/锦囊牌时失去。
    public bool StealthModuleActive { get; private set; }
    // 洛神：连续出费回合数（值为N时本回合出费得N+1费）。
    public int LuoshenFeeStreak { get; private set; }
    // 洛神：下回合开始时是否需要执行一次费用减半（仅一次，触发后立即清除）。
    public bool LuoshenDecayPending { get; private set; }
    // 洛神：本回合是否使用过费。
    public bool LuoshenUsedFeeThisTurn { get; private set; }
    // 洛神：本回合是否已经提交过行动（仅用于调试/行为记录；未出费本身即可触发衰减）。
    public bool LuoshenActionStartedThisTurn { get; private set; }
    public bool DebugInvincible { get; private set; }
    // 雕像核心：本场战斗已触发溅射次数（上限3次）。
    public int StatueCoreAttacksTriggered { get; private set; }
    public Dictionary<string, object> RuntimeStates { get; } = new();
    // 冰冻：剩余冻结回合数；>0 时只能出费。0 = 未冻结。
    public int FrozenTurnsRemaining { get; private set; }
    public bool IsFrozen => FrozenTurnsRemaining > 0;
    // 眩晕：剩余回合数；>0 时，本回合使用指向性攻击牌有50%概率不造成伤害。
    public int StunTurnsRemaining { get; private set; }
    public bool IsStunned => StunTurnsRemaining > 0;
    // 凶残：攻击时杀系伤害 ×1.5，每回合结束 -1 层。
    public int FerocityLayers { get; private set; }
    public bool HasFerocity => FerocityLayers > 0;
    // 虚弱：造成的最终伤害固定×0.75（不随层数加深），层数=剩余持续回合数；
    // 每回合结束-1层，重新获得虚弱时叠加层数=叠加持续时间，不会让减伤幅度变化。
    public int WeaknessLayers { get; private set; }
    public bool HasWeakness => WeaknessLayers > 0;
    /// <summary>
    /// 【泉水精华】仅免疫战斗内新施加的负面状态；它不会清除已经存在的局外 RunBuff，
    /// 也不会阻止伤害本身结算。
    /// </summary>
    public bool IsCombatDebuffImmune => Team == BattleTeam.Player
        ? GameManager.HasEquipment(EquipmentIds.SpringEssence)
        : this is EnemyInstance enemy && enemy.HasEquipment(EquipmentIds.SpringEssence);
    // 影袭（黄月英）：影袭状态机
    public bool InShadowState { get; private set; }
    public int RemainingTurns { get; private set; }
    // 影袭杀已出牌（出牌即标记，不依赖伤害触发器）。
    public bool ShadowSlashUsed { get; private set; }
    // 影袭杀未被取消（!Cancelled），供Developer Mode展示。
    public bool ShadowSlashHit { get; private set; }
    // 影袭杀实际造成伤害 > 0，决定下次影袭是否免费。
    public bool ShadowSlashDealDamage { get; private set; }
    // 下一次激活影袭的费用（0/1/2），由本次影袭结果决定。
    public double ShadowCost { get; private set; } = 1.0;
    // 如影随行：历史状态标记，当前规则已改为由 BattleContext 记录“受伤后费用设为1”的 pending 状态。
    public bool YueYingCostResetTriggered { get; private set; }
    // 酒池（董卓专属）：本回合剩余免费酒次数（每回合开始时由酒池效果重置为3）。
    public int FreeWineUsesRemaining { get; private set; }
    // 魅惑（貂蝉专属）：本场战斗剩余可使用次数，每场战斗开始时重置为3。
    public int MeihuoUsesRemaining { get; private set; }
    public override bool IsDead => DyingState == DyingState.Dead;
    public override string Id => UnitId;
    public override string Name => DisplayName;
    public override int CurrentHP => Health;
    public override int MaxHP => MaxHealth;
    public override double Resource => CurrentMana;

    /// <summary>
    /// Core System 的公开入口：SetCharacter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetCharacter(CharacterData characterData)
    {
        Character = new CharacterInstance(characterData);
    }

    /// <summary>
    /// Core System 的公开入口：SetDisplayName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetDisplayName(string displayName)
    {
        DisplayName = displayName;
    }

    // maxHealth = 战斗中的生命值上限（等于 GameManager.MaxHP）。
    // currentHealth = 进入战斗时的当前生命（= GameManager.CurrentHP + TempHp，可超过 maxHealth）。
    /// <summary>
    /// Core System 的公开入口：ResetForNewBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResetForNewBattle(int maxHealth, int currentHealth, double initialMana = BattleConstants.InitialMana)
    {
        Character = new CharacterInstance(WhiteboardCharacterData);
        _maxHealth = maxHealth;
        _currentHealth = currentHealth;
        CurrentMana = initialMana;
        ProtectedStealMana = 0;
        WinePower = 0;
        PendingWinePower = 0;
        HasUsedWineRevive = false;
        DyingPeachReviveUses = 0;
        LianyingArmed = true;
        LianyingPrepared = false;
        LianyingFreeKillAvailable = false;
        WhiteHorseFreeKillAvailable = true;
        YingXiFirstUseFreeAvailable = true;
        DiLuFirstDamageImmuneAvailable = true;
        ZhuaHuangFreeAttackAvailable = true;
        IronHeavyArmorActive = true;
        JetMaceActive = true;
        ClampExoskeletonActive = true;
        ClampExoskeletonRepairPending = false;
        ShortBowBattleTriggers = 0;
        ShortBowTriggeredThisRound = false;
        LongBowUsed = false;
        DyingState = DyingState.Alive;
        Skills.Clear();
        GuanxingPhase = GuanxingPhase.None;
        GuanxingRepeatsRemaining = 0;
        GuanxingRecordedCardType = null;
        GuanxingRecordedCount = 0;
        JiGuTriggered = false;
        JiGuReactionPending = false;
        JiGuActive = false;
        JiGuTurnsRemaining = 0;
        WarDrumFirstAttackPlayed = false;
        WarDrumPending = false;
        WarDrumActive = false;
        WarDrumAttackedThisTurn = false;
        StealthModuleActive = false;
        LuoshenFeeStreak = 0;
        LuoshenDecayPending = false;
        LuoshenUsedFeeThisTurn = false;
        LuoshenActionStartedThisTurn = false;
        DebugInvincible = false;
        StatueCoreAttacksTriggered = 0;
        FrozenTurnsRemaining = 0;
        StunTurnsRemaining = 0;
        FerocityLayers = 0;
        WeaknessLayers = 0;
        InShadowState = false;
        RemainingTurns = 0;
        ShadowSlashUsed = false;
        ShadowSlashHit = false;
        ShadowSlashDealDamage = false;
        ShadowCost = 1.0;
        YueYingCostResetTriggered = false;
        FreeWineUsesRemaining = 0;
        MeihuoUsesRemaining = 3;
        RuntimeStates.Clear();
    }

    /// <summary>
    /// Core System 的公开入口：CanPay。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool CanPay(Card card)
    {
        return BattleRules.CanAffordActionCost(this, card.Cost);
    }

    /// <summary>
    /// Core System 的公开入口：HasSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool HasSkill(string skillId)
    {
        return Skills.Any(skill => skill.Id == skillId);
    }

    /// <summary>
    /// Core System 的公开入口：AddSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddSkill(Skill skill)
    {
        if (!HasSkill(skill.Id))
        {
            Skills.Add(skill);
        }
    }

    /// <summary>
    /// Core System 的公开入口：RemoveSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RemoveSkill(string skillId)
    {
        Skills.RemoveAll(skill => skill.Id == skillId);
        if (skillId == SkillIds.Lianying)
        {
            LianyingPrepared = false;
            LianyingFreeKillAvailable = false;
        }
    }

    /// <summary>
    /// Core System 的公开入口：Pay。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Pay(Card card)
    {
        PayMana(card.Cost);
    }

    /// <summary>
    /// Core System 的公开入口：PayMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void PayMana(double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var normalMana = System.Math.Max(0, CurrentMana - ProtectedStealMana);
        var normalSpent = System.Math.Min(normalMana, amount);
        CurrentMana -= normalSpent;

        var remaining = amount - normalSpent;
        if (remaining > 0)
        {
            var protectedSpent = System.Math.Min(ProtectedStealMana, remaining);
            ProtectedStealMana -= protectedSpent;
            CurrentMana -= protectedSpent;
        }

        if (CurrentMana < 0)
        {
            CurrentMana = 0;
        }
        if (ProtectedStealMana < 0)
        {
            ProtectedStealMana = 0;
        }
    }

    /// <summary>
    /// Core System 的公开入口：GainMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void GainMana()
    {
        GainMana(1);
    }

    /// <summary>
    /// Core System 的公开入口：GainMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void GainMana(double amount)
    {
        // 连营重新充能检测：必须在这里（唯一真正让费用增加的方法）拿"改变前"
        // 和"改变后"的值做一次性判断，同一次调用内原子完成——不依赖任何
        // "上一帧记录的费用"之类的轮询状态，UI刷新/读档/重新计算费用都不会
        // 调用这个方法，天然不会误触发重新充能。amount<=0（理论上不会调用
        // 到这里，因为0费获得费用没有意义）时不判断，避免误把"没有变化"
        // 当成"离开了0"。
        var wasAtOrBelowZero = CurrentMana <= 0;
        CurrentMana += amount;
        if (HasSkill(SkillIds.KejiUnlimited))
            CurrentMana = System.Math.Min(20, CurrentMana);
        else if (HasSkill(SkillIds.Keji))
            CurrentMana = System.Math.Min(15, CurrentMana);

        if (amount > 0 && wasAtOrBelowZero && CurrentMana > 0 && HasSkill(SkillIds.Lianying))
        {
            RearmLianying();
        }
    }

    /// <summary>
    /// Core System 的公开入口：GainProtectedStealMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void GainProtectedStealMana(double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GainMana(amount);
        ProtectedStealMana += amount;
        if (ProtectedStealMana > CurrentMana)
        {
            ProtectedStealMana = CurrentMana;
        }
    }

    /// <summary>
    /// Core System 的公开入口：GetStealableMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public double GetStealableMana()
    {
        return System.Math.Max(0, CurrentMana - ProtectedStealMana);
    }

    /// <summary>
    /// Core System 的公开入口：ClearProtectedStealMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearProtectedStealMana()
    {
        ProtectedStealMana = 0;
    }

    /// <summary>
    /// Core System 的公开入口：QueueWinePower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void QueueWinePower(int amount)
    {
        PendingWinePower += amount;
    }

    /// <summary>
    /// Core System 的公开入口：ActivatePendingWinePower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ActivatePendingWinePower()
    {
        // 蜀·奇策增幅：本回合真正激活了酒（PendingWinePower>0）时，标记"多存活1回合"。
        // ActivatePendingWinePower 是 Player/EnemyInstance 共用方法（敌方酒状态也走这里），
        // 必须排除敌方；用简单 bool 标记（不是计数器）避免多次饮酒在同一回合内重复叠加"多1回合"。
        if (this is not EnemyInstance && PendingWinePower > 0 && FactionFateManager.IsShuAmplificationActive())
        {
            RuntimeStates["shu_amp_wine_extra_turn_pending"] = true;
        }

        WinePower = PendingWinePower;
        PendingWinePower = 0;
    }

    /// <summary>
    /// Core System 的公开入口：CancelOnePendingWinePower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void CancelOnePendingWinePower()
    {
        if (PendingWinePower > 0)
            PendingWinePower -= 1;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeClampExoskeleton。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeClampExoskeleton()
    {
        ClampExoskeletonActive = false;
        ClampExoskeletonRepairPending = true;
    }

    /// <summary>
    /// Core System 的公开入口：ClearClampRepairPending。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearClampRepairPending()
    {
        ClampExoskeletonRepairPending = false;
    }

    /// <summary>
    /// Core System 的公开入口：TriggerShortBow。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void TriggerShortBow()
    {
        ShortBowBattleTriggers++;
        ShortBowTriggeredThisRound = true;
    }

    /// <summary>
    /// Core System 的公开入口：ResetShortBowRound。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResetShortBowRound()
    {
        ShortBowTriggeredThisRound = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeLongBow。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeLongBow()
    {
        LongBowUsed = true;
    }

    /// <summary>
    /// Core System 的公开入口：ClearWinePower。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearWinePower()
    {
        WinePower = 0;
    }

    // 冰冻：施加或刷新冰冻状态（不叠加层数，仅刷新持续时间）。
    // 观星阶段免疫冰冻（由调用方判断，此方法不做过滤）。
    /// <summary>
    /// Core System 的公开入口：SetFrozen。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetFrozen(int turns)
    {
        if (turns > 0 && !IsCombatDebuffImmune)
        {
            FrozenTurnsRemaining = turns;
        }
    }

    // 冰冻：回合结束时调用，递减剩余冻结时间。
    /// <summary>
    /// Core System 的公开入口：DecrementFreezeTimer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementFreezeTimer()
    {
        if (FrozenTurnsRemaining > 0)
        {
            FrozenTurnsRemaining -= 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddFerocityLayers。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddFerocityLayers(int n)
    {
        if (n > 0)
        {
            FerocityLayers += n;
        }
    }

    /// <summary>
    /// Core System 的公开入口：SetStun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetStun(int turns)
    {
        if (turns > 0 && !IsCombatDebuffImmune)
        {
            StunTurnsRemaining = turns;
        }
    }

    /// <summary>
    /// Core System 的公开入口：DecrementStunTimer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementStunTimer()
    {
        if (StunTurnsRemaining > 0)
        {
            StunTurnsRemaining -= 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：DecrementFerocity。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementFerocity()
    {
        if (FerocityLayers > 0)
        {
            FerocityLayers -= 1;
        }
    }

    // 虚弱：叠加层数=叠加持续时间（不是叠加减伤幅度）。上限99只是防止数值溢出的
    // 软上限，只要层数>0，最终伤害统一×0.75，不随层数变化。
    /// <summary>
    /// Core System 的公开入口：AddWeaknessLayers。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddWeaknessLayers(int n)
    {
        if (n > 0 && !IsCombatDebuffImmune)
        {
            WeaknessLayers = System.Math.Min(99, WeaknessLayers + n);
        }
    }

    // 虚弱：回合结束时调用，剩余持续时间-1层。
    /// <summary>
    /// Core System 的公开入口：DecrementWeakness。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementWeakness()
    {
        if (WeaknessLayers > 0)
        {
            WeaknessLayers -= 1;
        }
    }

    // 冰冻：强制清除冰冻状态（影袭无敌期间调用）。
    /// <summary>
    /// Core System 的公开入口：ClearFreeze。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearFreeze()
    {
        FrozenTurnsRemaining = 0;
    }

    /// <summary>
    /// Core System 的公开入口：ClearStun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearStun()
    {
        StunTurnsRemaining = 0;
    }

    // 影袭：进入影袭状态。
    /// <summary>
    /// Core System 的公开入口：EnterShadowState。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void EnterShadowState()
    {
        ShadowSlashDealDamage = false;
        InShadowState = true;
        RemainingTurns = 4; // 在激活那回合TurnEnd递减1，剩余3回合可使用影袭牌
        // 蜀·奇策增幅：影袭持续时间+1回合。EnterShadowState 是 Player/EnemyInstance 共用方法
        // （EnemyInstance : Player，敌方影袭技能也走这同一个入口），阵营命运只属于玩家这个 Run，
        // 必须排除 `this is EnemyInstance` 才不会连带把敌人的影袭也延长。只加 RemainingTurns，
        // 不动 ShadowCost/ShadowSlashUsed 等结算字段。
        if (this is not EnemyInstance && FactionFateManager.IsShuAmplificationActive())
        {
            RemainingTurns += 1;
        }
        ShadowSlashUsed = false;
        ShadowSlashHit = false;
    }

    // 影袭：出牌时立即标记本期已使用影袭杀（在BattleResolver出牌阶段调用，不依赖伤害触发器）。
    /// <summary>
    /// Core System 的公开入口：MarkShadowSlashUsed。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void MarkShadowSlashUsed()
    {
        ShadowSlashUsed = true;
    }

    // 影袭：影袭杀未被取消（!Cancelled），供Developer Mode展示。
    /// <summary>
    /// Core System 的公开入口：MarkShadowSlashHit。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void MarkShadowSlashHit()
    {
        ShadowSlashHit = true;
    }

    // 影袭：影袭杀实际造成伤害 > 0，下次影袭免费。
    /// <summary>
    /// Core System 的公开入口：MarkShadowSlashDealDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void MarkShadowSlashDealDamage()
    {
        ShadowSlashDealDamage = true;
    }

    // 影袭：每回合结束递减倒计时，归零时进入单一退出点。
    /// <summary>
    /// Core System 的公开入口：DecrementShadowTurns。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementShadowTurns()
    {
        if (!InShadowState) return;
        RemainingTurns--;
        if (RemainingTurns <= 0)
        {
            ResolveShadowEnd();
        }
    }

    // 影袭：单一退出点。①未出招→费用1；②出招+造伤→免费(0)；③其余→2费。
    /// <summary>
    /// Core System 的公开入口：ResolveShadowEnd。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResolveShadowEnd()
    {
        InShadowState = false;
        RemainingTurns = 0;
        if (!ShadowSlashUsed)
            ShadowCost = 1.0;
        else if (ShadowSlashDealDamage)
            ShadowCost = 0.0;
        else
            ShadowCost = 2.0;
        ShadowSlashUsed = false;
        ShadowSlashHit = false;
        ShadowSlashDealDamage = false;
    }

    // 如影随行：回合开始时重置首次伤害标记。
    /// <summary>
    /// Core System 的公开入口：ResetYueYingCostResetPerTurn。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResetYueYingCostResetPerTurn()
    {
        YueYingCostResetTriggered = false;
    }

    // 如影随行：本回合首次伤害后标记已触发。
    /// <summary>
    /// Core System 的公开入口：TriggerYueYingCostReset。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void TriggerYueYingCostReset()
    {
        YueYingCostResetTriggered = true;
    }

    // 如影随行：旧版效果保留的通用工具，当前新版费用重置使用 SetManaToOne。
    /// <summary>
    /// Core System 的公开入口：SetManaToZero。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetManaToZero()
    {
        CurrentMana = 0;
        ProtectedStealMana = 0;
    }

    /// <summary>
    /// Core System 的公开入口：SetManaToOne。
    ///
    /// 用于“全场费用重置为1”类效果。保护费用属于临时窃取保护资源，
    /// 重置费用时必须同步清空，避免显示费用与可被偷取费用不一致。
    /// </summary>
    public void SetManaToOne()
    {
        CurrentMana = 1;
        ProtectedStealMana = 0;
    }

    // 装备MaxHP加成（仅在战斗初始化时由 EnemyInstance 调用，不用于运行时动态增减）。
    /// <summary>
    /// Core System 的公开入口：AddMaxHealth。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddMaxHealth(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        // 黄月英【如影随行】：战斗内技能、装备触发等后续来源同样不能改变
        // 她的最大生命值，不能只依赖地图层的 GameManager.AddMaxHp。
        if (Team == BattleTeam.Player && HasSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        _maxHealth += amount;
        _currentHealth = System.Math.Min(_currentHealth + amount, _maxHealth);
    }

    /// <summary>
    /// 仅提高最大生命值，不改变当前生命值。
    /// 用于【毒丹】等明确要求“获得生命上限”但不能顺带恢复生命的战斗内效果。
    /// </summary>
    public void AddMaxHealthWithoutHealing(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (Team == BattleTeam.Player && HasSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        _maxHealth += amount;
    }

    /// <summary>
    /// Core System 的公开入口：Heal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public virtual int Heal(int amount, bool allowOverheal = false)
    {
        // 禁疗 RunBuff 只约束玩家，不能误伤敌人的独立治疗逻辑。
        if (amount > 0 && Team == BattleTeam.Player && RunBuffManager.IsHealingBlocked()) return 0;
        var previousHealth = Health;
        _currentHealth += amount;
        if (!allowOverheal && Health > MaxHealth)
        {
            _currentHealth = MaxHealth;
        }

        return Health - previousHealth;
    }

    /// <summary>
    /// Core System 的公开入口：TakeDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        // 【影袭】的无敌属于单位状态，而不只是 DamageEvent 上的一层防御。
        // 部分装备、技能和环境伤害会直接调用 TakeDamage 并绕过 OnBeforeDamage；
        // 在生命值最终写入点再兜底一次，保证影袭期间不会受到任何“伤害”。
        // 明确写作“失去生命”的效果仍走 LoseHealth，不属于本规则的伤害免疫范围。
        if (amount > 0 && InShadowState)
        {
            return;
        }

        // 遗忘之石：在这里统一拦截，而不是只挂在 DamageEvent 触发链上，因为毒素/反伤/
        // 自身技能伤害等大量伤害来源是直接调用 TakeDamage 的“真实伤害”，根本不会经过
        // OnBeforeDamage。第一回合的免疫仍然额外由 ForgottenStoneEffect（OnBeforeDamage/
        // Highest）在触发链最前面拦截一次：那部分伤害因为 damage.Cancelled=true 永远不会
        // 走到这里，从而不会触发任何 OnDamageTaken 时机的受伤相关效果；这里的第一回合分支
        // 只用来兜底触发链之外的直接伤害。第二、三回合没有等价的“提前取消”机制，只能在这里
        // 对真正写入生命值的最终数值统一减半（向下取整），這也天然保证了不会和触发链的百分比
        // 减伤重复叠加（因为触发链最终也是把 damage.Amount 传进这个方法）。
        if (amount > 0 && this is not EnemyInstance && GameManager.HasEquipment(EquipmentIds.ForgottenStone))
        {
            amount = GameManager.CurrentBattleTurnNumber switch
            {
                1 => 0,
                2 or 3 => (int)System.Math.Floor(amount * 0.5),
                _ => amount
            };
        }

        _currentHealth = System.Math.Max(0, _currentHealth - amount);
        // 仅追踪玩家实体受到的伤害；EnemyInstance继承此方法但不应触发无伤事件
        if (amount > 0 && this is not EnemyInstance) GameManager.RecordInitialEventDamageTaken();
    }

    /// <summary>
    /// 直接失去当前生命值，不视为受到伤害。
    ///
    /// 用于【崩坏】等明确写作“失去生命”的规则：不会经过护盾、免伤、受伤统计或
    /// OnDamageTaken，但生命归零后仍由调用方接入统一 OnDying 流程。
    /// </summary>
    public virtual void LoseHealth(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _currentHealth = System.Math.Max(0, _currentHealth - amount);
    }

    /// <summary>
    /// Core System 的公开入口：EnterDying。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void EnterDying()
    {
        if (DyingState != DyingState.Dead)
        {
            DyingState = DyingState.Dying;
        }
    }

    /// <summary>
    /// Core System 的公开入口：RecoverFromDying。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RecoverFromDying()
    {
        if (Health > 0 && DyingState == DyingState.Dying)
        {
            DyingState = DyingState.Alive;
        }
    }

    /// <summary>
    /// Core System 的公开入口：MarkDead。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void MarkDead()
    {
        DyingState = DyingState.Dead;
    }

    /// <summary>
    /// Core System 的公开入口：MarkWineReviveUsed。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void MarkWineReviveUsed()
    {
        HasUsedWineRevive = true;
    }

    /// <summary>
    /// 记录本场战斗一次濒死【桃】救援。该计数只影响后续濒死救援费用，战斗重置时清零。
    /// </summary>
    public void MarkDyingPeachReviveUsed()
    {
        DyingPeachReviveUses += 1;
    }

    /// <summary>
    /// Core System 的公开入口：PrepareLianying。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void PrepareLianying()
    {
        // 触发后立即上锁：必须先经过 RearmLianying（费用离开0）才能再次触发，
        // 不是永久一次性——这是本次重设计与旧版最核心的区别。
        LianyingArmed = false;
        LianyingPrepared = true;
    }

    /// <summary>
    /// 连营重新充能：费用从"≤0"变为"&gt;0"时调用（见 GainMana），把
    /// LianyingArmed 重新置为 true。不依赖"上一帧费用"这种易错的轮询写法——
    /// 直接在唯一真正改变 CurrentMana 的方法内部、拿改变前后的值做一次性
    /// 判断，同一帧内原子完成，UI刷新/读档/重新计算费用都不会触碰这个方法，
    /// 自然不会误触发。
    /// </summary>
    public void RearmLianying()
    {
        LianyingArmed = true;
    }

    /// <summary>
    /// Core System 的公开入口：ActivateLianyingFreeKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ActivateLianyingFreeKill()
    {
        LianyingPrepared = false;
        LianyingFreeKillAvailable = true;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeLianyingFreeKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeLianyingFreeKill()
    {
        LianyingFreeKillAvailable = false;
    }

    /// <summary>
    /// Core System 的公开入口：ResetFreeWineUses。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResetFreeWineUses()
    {
        FreeWineUsesRemaining = 3;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeFreeWineUse。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeFreeWineUse()
    {
        if (FreeWineUsesRemaining > 0) FreeWineUsesRemaining--;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeMeihuoUse。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeMeihuoUse()
    {
        if (MeihuoUsesRemaining > 0) MeihuoUsesRemaining--;
    }

    // 供AI叠加出牌数量计算使用：返回当前可免费额外使用的指定牌型张数。
    // 连营：普通杀可多打一张（免费）。后续如有其他技能提供免费牌，在此扩展。
    /// <summary>
    /// Core System 的公开入口：GetExtraFreeCardCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int GetExtraFreeCardCount(CardType type)
    {
        if (type == CardType.Kill && LianyingFreeKillAvailable)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeWhiteHorseFreeKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeWhiteHorseFreeKill()
    {
        WhiteHorseFreeKillAvailable = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeYingXiFirstUseFree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeYingXiFirstUseFree()
    {
        YingXiFirstUseFreeAvailable = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeDiLuFirstDamageImmune。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeDiLuFirstDamageImmune()
    {
        DiLuFirstDamageImmuneAvailable = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeZhuaHuangFreeAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeZhuaHuangFreeAttack()
    {
        ZhuaHuangFreeAttackAvailable = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeIronHeavyArmor。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeIronHeavyArmor()
    {
        IronHeavyArmorActive = false;
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeJetMace。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeJetMace()
    {
        JetMaceActive = false;
    }

    /// <summary>
    /// Core System 的公开入口：ClearExpiredLianyingFreeKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool ClearExpiredLianyingFreeKill()
    {
        if (!LianyingFreeKillAvailable)
        {
            return false;
        }

        LianyingFreeKillAvailable = false;
        return true;
    }

    /// <summary>
    /// Core System 的公开入口：StartGuanxingRecording。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void StartGuanxingRecording()
    {
        GuanxingPhase = GuanxingPhase.Recording;
        GuanxingRecordedCardType = null;
        GuanxingRecordedCount = 0;
    }

    /// <summary>
    /// Core System 的公开入口：RecordGuanxingAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RecordGuanxingAction(CardType cardType, int count)
    {
        GuanxingRecordedCardType = cardType;
        GuanxingRecordedCount = System.Math.Max(1, count);
        GuanxingPhase = GuanxingPhase.Repeating;
        GuanxingRepeatsRemaining = GuanxingRepeatTurnCount;
        // 蜀·奇策增幅：观星重复持续时间+1回合。RecordGuanxingAction 同样是 Player/EnemyInstance
        // 共用方法（敌方观星也走这里），必须排除敌方，否则会连带增强敌方观星。
        if (this is not EnemyInstance && FactionFateManager.IsShuAmplificationActive())
        {
            GuanxingRepeatsRemaining += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：ConsumeGuanxingRepeat。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConsumeGuanxingRepeat()
    {
        if (GuanxingRepeatsRemaining > 0)
        {
            GuanxingRepeatsRemaining--;
        }

        if (GuanxingRepeatsRemaining <= 0)
        {
            GuanxingPhase = GuanxingPhase.None;
            GuanxingRecordedCardType = null;
            GuanxingRecordedCount = 0;
        }
    }

    /// <summary>
    /// Core System 的公开入口：TriggerJiGu。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void TriggerJiGu()
    {
        JiGuTriggered = true;
        JiGuReactionPending = false;
    }

    /// <summary>标记击鼓反应已显示，避免同一结算批次重复入队。</summary>
    public void QueueJiGuReaction()
    {
        JiGuReactionPending = true;
    }

    /// <summary>放弃击鼓后解除等待状态，下一次实际受到伤害可再次触发。</summary>
    public void ClearJiGuReactionPending()
    {
        JiGuReactionPending = false;
    }

    /// <summary>
    /// Core System 的公开入口：ActivateJiGu。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ActivateJiGu()
    {
        JiGuActive = true;
        JiGuTurnsRemaining = 4;
    }

    /// <summary>
    /// Core System 的公开入口：DecrementJiGu。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DecrementJiGu()
    {
        if (!JiGuActive)
        {
            return;
        }

        JiGuTurnsRemaining--;
        if (JiGuTurnsRemaining <= 0)
        {
            JiGuActive = false;
            JiGuTurnsRemaining = 0;
        }
    }

    /// <summary>
    /// Core System 的公开入口：TriggerWarDrum。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void TriggerWarDrum()
    {
        WarDrumFirstAttackPlayed = true;
        WarDrumPending = true;
    }

    /// <summary>
    /// Core System 的公开入口：ActivateStealthModule。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ActivateStealthModule()
    {
        StealthModuleActive = true;
    }

    /// <summary>
    /// Core System 的公开入口：ClearStealthModule。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearStealthModule()
    {
        StealthModuleActive = false;
    }

    /// <summary>
    /// Core System 的公开入口：ActivateWarDrum。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ActivateWarDrum()
    {
        WarDrumPending = false;
        WarDrumActive = true;
        WarDrumAttackedThisTurn = false;
    }

    /// <summary>
    /// Core System 的公开入口：SetWarDrumAttackedThisTurn。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetWarDrumAttackedThisTurn()
    {
        WarDrumAttackedThisTurn = true;
    }

    /// <summary>
    /// Core System 的公开入口：DeactivateWarDrum。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DeactivateWarDrum()
    {
        WarDrumActive = false;
        WarDrumAttackedThisTurn = false;
    }

    // 本回合出费时调用（在 BattleResolver.ApplyResourceAndHealing 中）。
    /// <summary>
    /// Core System 的公开入口：LuoshenOnFeeUsed。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void LuoshenOnFeeUsed()
    {
        LuoshenUsedFeeThisTurn = true;
    }

    // 本回合出过牌（含费）时调用。是否为费由 LuoshenOnFeeUsed 单独标记，
    // 这里只负责记录“本回合确实出过牌”，不再记录费用数值——衰减基准改为
    // 触发时（下回合开始）读取当时的实时费用，避免快照与实际费用脱节。
    /// <summary>
    /// Core System 的公开入口：LuoshenOnActionStarted。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void LuoshenOnActionStarted(double manaBeforeAction)
    {
        LuoshenActionStartedThisTurn = true;
    }

    // 回合结束时调用：根据本回合是否出费，决定下回合是否需要减半，并更新连费计数。
    /// <summary>
    /// Core System 的公开入口：LuoshenUpdateEndOfTurn。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void LuoshenUpdateEndOfTurn()
    {
        if (LuoshenUsedFeeThisTurn)
        {
            // 本回合出过费：无论是否还出过其它牌，都不触发衰减，连费链继续累加。
            LuoshenFeeStreak++;
            LuoshenDecayPending = false;
        }
        else
        {
            // 【洛神】的断费条件只看“是否使用费”。即便本回合没有出任何牌，
            // 只要未使用费，也应在下回合开始时执行一次费用减半判定。
            LuoshenFeeStreak = 0;
            LuoshenDecayPending = true;
        }

        LuoshenUsedFeeThisTurn = false;
        LuoshenActionStartedThisTurn = false;
    }

    // 回合开始时调用：若有衰减待处理，且当前费用>2才减半；无论是否实际减半，
    // 待触发标记都会立即清除，确保只判定一次、不会连续生效。
    // 返回 (before, after, basis) 供日志使用；before==after 时未触发。
    /// <summary>
    /// Core System 的公开入口：LuoshenApplyDecay。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public (double before, double after, double basis) LuoshenApplyDecay()
    {
        if (!LuoshenDecayPending)
            return (CurrentMana, CurrentMana, 0);

        LuoshenDecayPending = false;

        var before = CurrentMana;
        if (before <= 2)
        {
            return (before, before, before);
        }

        var after = System.Math.Floor(before / 2);
        CurrentMana = after;
        return (before, after, before);
    }

    /// <summary>
    /// Core System 的公开入口：IncrementStatueCoreAttacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void IncrementStatueCoreAttacks()
    {
        StatueCoreAttacksTriggered++;
    }

    /// <summary>
    /// Core System 的公开入口：ClearStatuses。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearStatuses()
    {
        WinePower = 0;
        PendingWinePower = 0;
        FrozenTurnsRemaining = 0;
        StunTurnsRemaining = 0;
        LianyingPrepared = false;
        LianyingFreeKillAvailable = false;
        WhiteHorseFreeKillAvailable = true;
        DiLuFirstDamageImmuneAvailable = true;
        ZhuaHuangFreeAttackAvailable = true;
        ProtectedStealMana = 0;
    }

    /// <summary>
    /// Core System 的公开入口：DebugClearSkillsAndTemporaryStates。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DebugClearSkillsAndTemporaryStates()
    {
        Skills.Clear();
        WinePower = 0;
        PendingWinePower = 0;
        LianyingArmed = true;
        LianyingPrepared = false;
        LianyingFreeKillAvailable = false;
        ProtectedStealMana = 0;
        GuanxingPhase = GuanxingPhase.None;
        GuanxingRepeatsRemaining = 0;
        GuanxingRecordedCardType = null;
        GuanxingRecordedCount = 0;
        JiGuTriggered = false;
        JiGuReactionPending = false;
        JiGuActive = false;
        JiGuTurnsRemaining = 0;
        WarDrumFirstAttackPlayed = false;
        WarDrumPending = false;
        WarDrumActive = false;
        WarDrumAttackedThisTurn = false;
        StealthModuleActive = false;
        LuoshenFeeStreak = 0;
        LuoshenDecayPending = false;
        LuoshenUsedFeeThisTurn = false;
        LuoshenActionStartedThisTurn = false;
        FrozenTurnsRemaining = 0;
        StunTurnsRemaining = 0;
        RuntimeStates.Clear();
    }

    /// <summary>
    /// Core System 的公开入口：DebugSetHealth。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DebugSetHealth(int value)
    {
        _currentHealth = value;
    }

    /// <summary>
    /// 将战斗内生命设为一个合法值。用于【煞气缠身】这类明确的状态变更，
    /// 不应通过 DebugSetHealth 或普通伤害/治疗接口绕行。
    /// </summary>
    public void SetCurrentHealth(int value)
    {
        // 【煞气缠身】在战斗内也必须恒为1；允许设为0进入死亡流程。
        if (Team == BattleTeam.Player
            && RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) > 0)
        {
            _currentHealth = value <= 0 ? 0 : 1;
            return;
        }

        _currentHealth = System.Math.Clamp(value, 0, _maxHealth);
    }

    /// <summary>
    /// Core System 的公开入口：DebugSetMaxHealth。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DebugSetMaxHealth(int value)
    {
        _maxHealth = value < 1 ? 1 : value;
        if (_currentHealth > _maxHealth)
        {
            _currentHealth = _maxHealth;
        }
    }

    /// <summary>
    /// Core System 的公开入口：DebugSetMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DebugSetMana(double value)
    {
        CurrentMana = value < 0 ? 0 : value;
        if (ProtectedStealMana > CurrentMana)
        {
            ProtectedStealMana = CurrentMana;
        }
    }

    /// <summary>
    /// Core System 的公开入口：DebugSetInvincible。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void DebugSetInvincible(bool value)
    {
        DebugInvincible = value;
    }

    /// <summary>
    /// Core System 的派生类入口：SetHealthDirectly。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    protected void SetHealthDirectly(int value)
    {
        _currentHealth = value;
    }

    /// <summary>
    /// Core System 的派生类入口：SetManaDirectly。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    protected void SetManaDirectly(double value)
    {
        CurrentMana = value < 0 ? 0 : value;
    }
}
