//////////////////////////////////////////////////////////
// 文件：Scripts/BattleContext.cs
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
using Godot;

/// <summary>Machine-readable result of a completed battle.</summary>
public enum BattleOutcome
{
    None,
    Victory,
    Defeat
}

/// <summary>
/// Core System 的公开类：BattleContext。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattleContext
{
    // Some legacy equipment effects write "[Equipment]" first and place the
    // player-readable result on the following line. Keep this pairing so the
    // battle log shows the result rather than an empty marker.
    private bool _awaitingEquipmentTriggerDetail;

    /// <summary>
    /// Core System 的公开入口：BattleContext。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public BattleContext(Player player, TriggerManager triggerManager)
    {
        Player = player;
        TriggerManager = triggerManager;
    }

    public Player Player { get; }
    public TriggerManager TriggerManager { get; }
    public BattlePhase Phase { get; set; } = BattlePhase.StartPhase;
    public int TurnNumber { get; set; } = 1;
    public int TurnCounter
    {
        get => TurnNumber;
        set => TurnNumber = value;
    }
    public BattleAction? PlayerAction { get; set; }
    /// <summary>
    /// 玩家确认本回合行动时锁定的敌人。
    ///
    /// 该字段独立于 <see cref="BattleAction.Target"/>：桃、酒、费、闪等牌并不以敌人为
    /// 规则目标，但【司敌】仍需要知道玩家当时锁定的是哪一名敌人。
    /// </summary>
    public EnemyInstance? PlayerLockedTarget { get; set; }
    public List<EnemyActionEntry> EnemyActions { get; } = new();
    public BattleEncounter? Encounter { get; set; }
    public DamageEvent? DamageEvent { get; set; }
    public Action<int, int>? PlayerDamageBorderRequested { get; set; }
    public Action<SkillTriggerPresentationRequest>? PlayerSkillPresentationRequested { get; set; }
    /// <summary>
    /// 【黄天】专用的战场表现桥。规则层只上报"给谁施加闪电 / 闪电落下"，
    /// BattleManager 决定目标的实际舞台位置并播放素材；Headless 测试无需注入。
    /// </summary>
    public Action<HuangTianVisualRequest>? HuangTianVisualRequested { get; set; }
    public ResourceChangeEvent? ResourceChangeEvent { get; set; }
    public HealEvent? HealEvent { get; set; }
    public ReactionQueue Reactions { get; } = new();
    public RoundResult RoundResult { get; private set; } = new();
    public List<string> TriggerLogs { get; } = new();
    public bool EnableEffectDebugLog { get; set; }
    /// <summary>
    /// 伤害预览专用上下文标记。预览可以复用 OnDamage 的数值修正，但绝不能消耗
    /// 装备次数、写入单位状态或推进任何真实战斗状态。
    /// </summary>
    public bool IsDamagePreview { get; init; }
    public BattleLogService? LogService { get; set; }
    public TriggerTiming? CurrentTriggerTiming { get; set; }
    public bool GameOver { get; set; }
    // GameOverText is presentation-only and is localized.  Game flow must use
    // Outcome so switching the display language cannot turn a victory into loss.
    public BattleOutcome Outcome { get; set; }
    public bool IsPlayerVictory => Outcome == BattleOutcome.Victory;
    public string GameOverText { get; set; } = string.Empty;
    // 战场崩坏"互伤胜利"判定用：记录玩家/全部敌人是否在【本回合】内死亡。
    // DeathEffect 每次处理一方死亡时都会检查对方这两个标记是否已经在本回合置真——
    // 如果双方在同一回合内都被对方造成的伤害击倒，无论谁的死亡先被结算处理，
    // 最终都会被改判为"玩家胜利"（互伤胜利），而不是由结算顺序这种偶然因素
    // 决定谁赢——由 BeginRoundResult() 每回合开始时重置。
    public bool PlayerDiedThisRound { get; set; }
    public bool AllEnemiesDiedThisRound { get; set; }
    /// <summary>【铁索连环】每回合只复制第一段实际伤害，防止链式伤害递归结算。</summary>
    public bool IronChainSharedDamageSettledThisRound { get; set; }
    // 本局是否通过"双方同时倒下→改判玩家胜利"判定获胜；供 MainFlow/GameManager
    // 在结算胜利奖励时识别这是一次特殊的互伤胜利（例如战后血量特殊处理）。
    public bool MutualCollapseVictory { get; set; }
    /// <summary>本场战斗中玩家对敌方造成的最终实际伤害总和。</summary>
    public int PlayerDamageDealtThisBattle { get; private set; }
    public int PlayerDodgeLayers { get; set; }
    public bool PlayerDodgeDefenseActive { get; set; }
    public bool PlayerCounterDefenseActive { get; set; }
    public int PlayerQingnangDodgeLayers { get; set; }
    public int PlayerPeachShieldLayers { get; set; }
    // 本回合桃的待回复数（每层桃护盾触发时 -1；最终用于计算实际回血量）。
    public int PlayerPeachHealGranted { get; set; }
    public int PlayerWineShieldLayers { get; set; }
    public bool PlayerActionCancelled { get; set; }
    // 义绝（玩家侧/关羽）：追踪"上回合是否有敌人受到过伤害"，用于本回合杀系伤害翻倍判定。
    public bool AnyEnemyHitThisRound { get; set; }
    public bool AnyEnemyHitLastRound { get; set; }
    public Dictionary<string, int> EnemyDodgeLayers { get; } = new();
    public Dictionary<string, bool> EnemyDodgeDefenseActive { get; } = new();
    public Dictionary<string, bool> EnemyCounterDefenseActive { get; } = new();
    public Dictionary<string, int> EnemyQingnangDodgeLayers { get; } = new();
    public Dictionary<string, int> EnemyPeachShieldLayers { get; } = new();
    public Dictionary<string, int> EnemyPeachHealGranted { get; } = new();
    public Dictionary<string, int> EnemyWineShieldLayers { get; } = new();

    // 鳞甲：每回合每个生命单位（或共享HP池）最多触发一次；存储已触发的 key，回合开始时清空。
    public HashSet<string> ScaleArmorTriggeredThisRound { get; } = new();

    // 夜视镜：本回合玩家群体伤害攻击牌（万箭齐发/南蛮入侵/突袭/天启）被集中锁定的唯一目标与
    // 场上存活敌人数（伤害倍率）；在处理这类攻击命中的第一个敌人时惰性计算并缓存，回合开始时清空。
    public EnemyInstance? NightVisionAoeLockedTarget { get; set; }
    public int NightVisionAoeEnemyCount { get; set; }

    // 业炎（周瑜）：同一回合内即使多个目标先后受到火属性伤害而各自获得虚弱，
    // 技能大字也只播放一次；虚弱本身仍然对每个受伤目标分别生效，只去重表现。
    public bool YeYanTriggeredThisRound { get; set; }

    // 长弓/古锭刀：本次攻击忽略防御方的闪；由相应的 OnBeforeDamage 效果（Highest，在 DefenseBeforeDamageEffect 之前注册）设为 true，
    // 由 DefenseBeforeDamageEffect.TryConsumeDodge 读取并重置为 false。
    public bool IgnoreDefenderDodgeForThisAttack { get; set; }

    // 破军：玩家杀系命中后捕获的基础伤害（ApplyDamageEffect 已结算的最终值）。
    // 命中时立即建立 ReactionWindow；这里保留本次数据，仅用于日志和调试追踪。
    public int PoJunPendingAmount { get; set; }
    // 被【破军】追加的原始杀类型。追加伤害继承该次命中的属性/来源，不会退化为普通杀。
    public CardType PoJunPendingAttackType { get; set; } = CardType.Kill;
    // 破军：命中的目标（Player? 以兼容泛型存储，实际为 EnemyInstance）。
    public Player? PoJunPendingTarget { get; set; }
    // 破军：设为 true 时，PoJunPlayerCaptureEffect 不捕获此次伤害（用于额外补刀，防止无限循环）。
    public bool IgnorePoJunCapture { get; set; }

    // 如影随行：本次玩家攻击被护盾（桃/酒/仁德）完全抵消，用于给黄月英+1费触发检测。
    // 由 DefenseBeforeDamageEffect 和 RendeShieldAbsorbEffect 在检测到护盾抵消时设置，每回合结束前清除。
    public bool PlayerAttackAbsorbedByShield { get; set; }
    // 如影随行：本回合场上已发生实际伤害，等 BattlePhase 中资源/偷取等行动都结算完毕后
    // 再统一把所有存活单位费用设为 1，避免“伤害先触发重置，随后敌方出费又加上去”的顺序问题。
    public bool RuYingSuiXingResourceResetPending { get; set; }

    // 周泰【不屈】/【奋激】等 OnDying 判定去重：每次真正触发 TriggerTiming.OnDying 的调用点
    // 都应在 RaiseTrigger 之前写入一个新的 Guid，供同一 context 下所有 OnDying 效果判断
    // "这是不是我已经处理过的那一次 OnDying"，从而在管线里出现冗余/重复 raise 时仍保证
    // "一次 OnDying 发生 → 每个效果最多处理一次"。
    public string? CurrentDyingEventId { get; set; }
    internal long CurrentResolutionChainId { get; set; }
    private long _nextResolutionChainId;

    /// <summary>
    /// 统一的 OnDying 触发入口：所有"生命值降到0或以下、需要走濒死结算"的调用点都必须通过
    /// 这个方法，而不是直接调用 TriggerManager.RaiseTrigger(TriggerTiming.OnDying, ...)。
    ///
    /// 为什么存在：CurrentDyingEventId 是周泰【不屈】/【奋激】等效果判断"这是不是同一次
    /// OnDying"的去重键，规则要求每次真正的濒死事件都必须先写入一个新 Guid。但毒素/瘟疫/
    /// 血债血偿/自伤类装备等大量调用点是各自直接调用 TakeDamage/LoseHealth 后手动 raise
    /// OnDying，之前都忘了先刷新这个 Guid——于是它们复用了本场战斗上一次"正常攻击致死"
    /// 事件遗留下来的旧 Guid。结果是【奋激】的去重检查把这次全新的濒死事件误判成"已经
    /// 处理过的那次"，直接跳过，导致奋激不能在每次掉血到0都生效（原bug）。统一收口到这个
    /// 方法，新增的 OnDying 调用点也不会再犯同样的错误。
    /// </summary>
    public void RaiseOnDying()
    {
        CurrentDyingEventId = System.Guid.NewGuid().ToString("N");
        TriggerManager.RaiseTrigger(TriggerTiming.OnDying, this);
    }

    // 周泰【不屈】：后续致命伤 1D6 判定的待播放结果，由 ZhouTaiBuQuTriggerEffect 在同步的
    // Execute() 里写入（计算与即时后果均已同步完成），实际的骰子动画由 BattleManager 在
    // 战斗阶段的同步触发链结束后异步播放，播放完毕后清空。
    public (int Result, bool Success)? PendingDiceRollRequest { get; set; }

    /// <summary>
    /// Core System 的公开入口：GetFirstAliveEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyInstance? GetFirstAliveEnemy()
    {
        if (Encounter == null)
        {
            return null;
        }

        foreach (var enemy in Encounter.Enemies)
        {
            if (!enemy.IsDead)
            {
                return enemy;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回战斗内状态字典使用的单位 Key。
    ///
    /// 敌人定义 Id 允许重复，例如同一场内多个城关守卫。防御层、护盾层和
    /// 费用快照必须绑定到敌人实例，而不能绑定到模板 Id。
    /// </summary>
    public static string GetUnitStateKey(Player unit)
    {
        return unit is EnemyInstance enemy ? enemy.BattleStateKey : unit.Id;
    }

    /// <summary>
    /// Core System 的公开入口：GetEnemyActionEntry。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyActionEntry? GetEnemyActionEntry(EnemyInstance enemy)
    {
        foreach (var entry in EnemyActions)
        {
            if (entry.Enemy == enemy)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 请求表现层播放玩家受伤屏幕边框。
    ///
    /// 该入口只接收已经确认的实际扣血值；Damage、Trigger、技能等规则仍在各自系统内完成。
    /// BattleManager 创建 BattleContext 后注入回调，Headless 测试可以不注入。
    /// </summary>
    public void RequestPlayerDamageBorder(int actualDamage, int maxHp)
    {
        if (actualDamage <= 0 || maxHp <= 0)
        {
            return;
        }

        PlayerDamageBorderRequested?.Invoke(actualDamage, maxHp);
    }

    /// <summary>
    /// 为一次 Trigger 调度创建稳定的结算链标识。
    ///
    /// 表现层用它合并同一链中多目标或重复 Raise 产生的同技能提示；
    /// 该标识不参与战斗规则和优先级计算。
    /// </summary>
    internal long CreateResolutionChainId()
    {
        return ++_nextResolutionChainId;
    }

    /// <summary>
    /// 上报我方角色自身技能已经实际产生效果。
    ///
    /// 调用位置必须在效果成功写入战斗状态之后。装备技能、通用技能、
    /// 敌方技能以及仅完成条件检查的分支会在这里被统一拒绝。
    /// </summary>
    public void ReportPlayerCharacterSkillTriggered(
        Player actor,
        string skillId,
        TriggerTiming timing,
        EffectPriority? priority = null,
        string variant = "")
    {
        if (!ReferenceEquals(actor, Player) || !actor.HasSkill(skillId))
        {
            return;
        }

        var skill = SkillDatabase.GetSkill(skillId);
        if (skill == null
            || skill.Category != SkillCategory.CharacterExclusive
            || skill.Source != SkillSource.Character
            || !string.Equals(skill.CharacterId, actor.Character.Data.Id, StringComparison.Ordinal))
        {
            return;
        }

        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstSkillTrigger);

        var localizedName = skill.DisplayName;
        if (string.IsNullOrWhiteSpace(skill.NameKey)
            || Localization.Get(skill.NameKey).StartsWith("【Missing:", StringComparison.Ordinal))
        {
            GD.PushWarning($"[SkillTriggerPresentation] Missing localization for skill '{skill.Id}', fallback to data display name.");
        }

        if (string.IsNullOrWhiteSpace(localizedName))
        {
            localizedName = skill.Id;
        }

        var chainId = CurrentResolutionChainId;
        if (chainId <= 0)
        {
            chainId = CreateResolutionChainId();
        }

        PlayerSkillPresentationRequested?.Invoke(new SkillTriggerPresentationRequest(
            BattleTeam.Player,
            actor.Id,
            skill.Id,
            localizedName,
            timing,
            priority,
            chainId,
            variant));

        // 图鉴：这里是"角色专属技能已经真正生效"的唯一统一出口（上方已经排除了敌方/装备/
        // 通用技能和仅完成条件检查的调用），不需要在各个技能效果文件里各自计数。
        CodexService.RecordSkillTriggered(skill.CharacterId ?? string.Empty, skill.Id);

        LogService?.RecordStructured(
            BattleLogEventKind.Skill,
            TurnNumber,
            Phase.ToString(),
            "battlelog.skill_activated",
            new[] { localizedName },
            actor: actor.DisplayName,
            source: skill.Id,
            skill: localizedName,
            timing: timing,
            priority: priority,
            result: string.IsNullOrWhiteSpace(variant) ? "Triggered" : variant);
    }

    /// <summary>
    /// Core System 的公开入口：GetActionForEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public BattleAction? GetActionForEnemy(EnemyInstance enemy)
    {
        return GetEnemyActionEntry(enemy)?.Action;
    }

    /// <summary>
    /// Core System 的公开入口：SetActionForEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetActionForEnemy(EnemyInstance enemy, BattleAction action)
    {
        var entry = GetEnemyActionEntry(enemy);
        if (entry != null)
        {
            entry.Action = action;
            entry.ActionCancelled = false;
            return;
        }

        EnemyActions.Add(new EnemyActionEntry(enemy, action));
    }

    /// <summary>
    /// Core System 的公开入口：ClearEnemyActions。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ClearEnemyActions()
    {
        EnemyActions.Clear();
    }

    // 返回玩家行动的明确目标所对应的敌方行动条目。
    // 正确规则：每张牌只与其目标之间进行结算。
    // 不再回退到"第一个存活单位"——当玩家没有明确目标，或目标没有行动条目时，返回 null，
    // 以防止非目标单位的行动被错误拉入同一结算池。
    /// <summary>
    /// Core System 的公开入口：GetTargetEnemyActionEntry。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyActionEntry? GetTargetEnemyActionEntry()
    {
        if (PlayerAction?.Target is EnemyInstance targetedEnemy)
        {
            return GetEnemyActionEntry(targetedEnemy);
        }

        return null;
    }

    /// <summary>
    /// 返回玩家本回合锁定敌人对应的行动条目。
    ///
    /// 仅用于依赖“当前锁定目标”的规则，不回退到第一个存活敌人，避免多敌人战斗中
    /// 把其它敌人的行动错误纳入判定。测试或旧调用未显式写入锁定目标时，允许从明确的
    /// 单体卡牌目标恢复同一个敌人实例。
    /// </summary>
    public EnemyActionEntry? GetPlayerLockedEnemyActionEntry()
    {
        var lockedEnemy = PlayerLockedTarget ?? PlayerAction?.Target as EnemyInstance;
        return lockedEnemy == null ? null : GetEnemyActionEntry(lockedEnemy);
    }

    /// <summary>
    /// Core System 的公开入口：BeginRoundResult。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void BeginRoundResult()
    {
        RoundResult = new RoundResult();
        _awaitingEquipmentTriggerDetail = false;
        DamageEvent = null;
        PlayerLockedTarget = null;
        ResourceChangeEvent = null;
        Reactions.Clear();
        PlayerDodgeLayers = 0;
        PlayerDodgeDefenseActive = false;
        PlayerCounterDefenseActive = false;
        PlayerQingnangDodgeLayers = 0;
        PlayerPeachShieldLayers = 0;
        PlayerPeachHealGranted = 0;
        PlayerWineShieldLayers = 0;
        PlayerActionCancelled = false;
        PoJunPendingAmount = 0;
        PoJunPendingAttackType = CardType.Kill;
        PoJunPendingTarget = null;
        IgnorePoJunCapture = false;
        PlayerAttackAbsorbedByShield = false;
        PlayerDiedThisRound = false;
        AllEnemiesDiedThisRound = false;
        IronChainSharedDamageSettledThisRound = false;
        RuYingSuiXingResourceResetPending = false;
        PendingDiceRollRequest = null;
        EnemyDodgeLayers.Clear();
        EnemyDodgeDefenseActive.Clear();
        EnemyCounterDefenseActive.Clear();
        EnemyQingnangDodgeLayers.Clear();
        EnemyPeachShieldLayers.Clear();
        EnemyPeachHealGranted.Clear();
        EnemyWineShieldLayers.Clear();
        ScaleArmorTriggeredThisRound.Clear();
        NightVisionAoeLockedTarget = null;
        NightVisionAoeEnemyCount = 0;
        YeYanTriggeredThisRound = false;
        foreach (var entry in EnemyActions)
        {
            entry.ActionCancelled = false;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddTriggerLog。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddTriggerLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        TriggerLogs.Add(text);
        var kind = ClassifyDebugEvent(text);
        var isEquipmentMarker = IsEquipmentMarker(text);
        var isEquipmentTrigger = kind == BattleLogEventKind.Equipment;

        if (isEquipmentMarker)
        {
            // Retain the marker for developer traces; the following line is the
            // actual player-facing equipment result.
            _awaitingEquipmentTriggerDetail = true;
            return;
        }

        if (isEquipmentTrigger || _awaitingEquipmentTriggerDetail)
        {
            LogService?.RecordStructured(
                BattleLogEventKind.Equipment,
                TurnNumber,
                Phase.ToString(),
                "battlelog.raw",
                new[] { text },
                category: BattleLogCategory.Action,
                timing: CurrentTriggerTiming,
                source: "Equipment",
                equipment: isEquipmentTrigger ? GetEquipmentLogName(text) : "Equipment",
                result: text);
            _awaitingEquipmentTriggerDetail = false;
            return;
        }

        LogService?.RecordStructured(
            kind,
            TurnNumber,
            Phase.ToString(),
            "battlelog.debug_line",
            new[] { text },
            debugOnly: true,
            timing: CurrentTriggerTiming,
            buff: kind == BattleLogEventKind.Buff ? text : string.Empty,
            debugMessage: text);
    }

    private static bool IsEquipmentMarker(string text)
    {
        return string.Equals(text.Trim(), "[Equipment]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text.Trim(), "[装备]", StringComparison.Ordinal);
    }

    private static string GetEquipmentLogName(string text)
    {
        const string equipmentPrefix = "[Equipment/";
        if (text.StartsWith(equipmentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var closingIndex = text.IndexOf(']');
            if (closingIndex > equipmentPrefix.Length)
            {
                return text.Substring(equipmentPrefix.Length, closingIndex - equipmentPrefix.Length);
            }
        }

        return "Equipment";
    }

    private static BattleLogEventKind ClassifyDebugEvent(string text)
    {
        if (text.Contains("DamageModifierPipeline", StringComparison.Ordinal)
            || text.Contains("Damage=", StringComparison.Ordinal))
        {
            return BattleLogEventKind.DamagePipeline;
        }
        if (text.Contains("Equipment", StringComparison.OrdinalIgnoreCase)
            || text.Contains("装备", StringComparison.Ordinal))
        {
            return BattleLogEventKind.Equipment;
        }
        if (text.Contains("Buff", StringComparison.OrdinalIgnoreCase)
            || text.Contains("状态", StringComparison.Ordinal))
        {
            return BattleLogEventKind.Buff;
        }
        if (text.Contains("AI", StringComparison.OrdinalIgnoreCase))
        {
            return BattleLogEventKind.AiDecision;
        }
        if (text.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("错误", StringComparison.Ordinal))
        {
            return BattleLogEventKind.Error;
        }
        if (text.Contains("Warning", StringComparison.OrdinalIgnoreCase)
            || text.Contains("警告", StringComparison.Ordinal))
        {
            return BattleLogEventKind.Warning;
        }

        return BattleLogEventKind.Debug;
    }

    /// <summary>
    /// Core System 的公开入口：AddEffectDebugLog。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddEffectDebugLog(TriggerTiming timing, IReadOnlyList<IBattleEffect> effects)
    {
        if (!EnableEffectDebugLog || effects.Count == 0)
        {
            return;
        }

        TriggerLogs.Add($"[{timing}]");
        foreach (var priority in new[]
        {
            EffectPriority.Immediate,
            EffectPriority.Highest,
            EffectPriority.High,
            EffectPriority.Mid,
            EffectPriority.Low,
            EffectPriority.Lowest
        })
        {
            var names = new List<string>();
            foreach (var effect in effects)
            {
                if (effect.Priority == priority)
                {
                    names.Add(GetEffectDebugName(effect));
                }
            }

            if (names.Count > 0)
            {
                TriggerLogs.Add($"{priority}: {string.Join(", ", names)}");
            }
        }
    }

    /// <summary>
    /// 将 Trigger 阶段写入统一开发者报告。
    /// </summary>
    public void RecordTriggerPhase(TriggerTiming timing)
    {
        LogService?.RecordStructured(
            BattleLogEventKind.TriggerPhase,
            TurnNumber,
            Phase.ToString(),
            "battlelog.debug_line",
            new[] { $"Trigger {timing}" },
            debugOnly: true,
            timing: timing,
            debugMessage: timing.ToString());
    }

    /// <summary>
    /// 将 EffectQueue 中的最终排序写入统一开发者报告。
    /// </summary>
    public void RecordEffectQueued(TriggerTiming timing, IBattleEffect effect, int queueIndex)
    {
        LogService?.RecordStructured(
            BattleLogEventKind.EffectQueue,
            TurnNumber,
            Phase.ToString(),
            "battlelog.debug_line",
            new[] { $"Queue[{queueIndex}] {effect.GetType().Name}" },
            debugOnly: true,
            timing: timing,
            priority: effect.Priority,
            source: effect.GetType().FullName ?? effect.GetType().Name,
            result: "Queued",
            debugMessage: $"Queue[{queueIndex}]");
    }

    /// <summary>
    /// 将单个 Effect 的执行结果写入统一开发者报告。
    /// </summary>
    public void RecordEffectExecuted(
        TriggerTiming timing,
        IBattleEffect effect,
        double? damageBefore,
        double? damageAfter,
        int? hpBefore,
        int? hpAfter,
        string result)
    {
        LogService?.RecordStructured(
            BattleLogEventKind.EffectExecute,
            TurnNumber,
            Phase.ToString(),
            "battlelog.debug_line",
            new[] { $"{effect.GetType().Name}: {result}" },
            debugOnly: true,
            timing: timing,
            priority: effect.Priority,
            actor: DamageEvent?.Source.DisplayName ?? string.Empty,
            target: DamageEvent?.Target.DisplayName ?? string.Empty,
            source: effect.GetType().FullName ?? effect.GetType().Name,
            damageBefore: damageBefore,
            damageAfter: damageAfter,
            hpBefore: hpBefore,
            hpAfter: hpAfter,
            result: result);
    }

    /// <summary>
    /// 记录最终实际伤害及其生命值变化。
    /// </summary>
    public void RecordDamageResolved(DamageEvent damage, int hpBefore, int hpAfter)
    {
        if (damage.Source == Player && damage.Target.Team == BattleTeam.Enemy && damage.ActualDamageDealt > 0)
        {
            PlayerDamageDealtThisBattle += damage.ActualDamageDealt;
        }

        var origin = damage.Origin;
        var entry = LogService?.RecordStructured(
            BattleLogEventKind.Damage,
            TurnNumber,
            Phase.ToString(),
            "battlelog.health_change",
            new[]
            {
                Localization.Get("health_change.damage"),
                GetHealthChangeSourceKindLabel(origin.Kind),
                GetHealthChangeSideLabel(GetHealthChangeSide(origin.Owner)),
                GetHealthChangeSideLabel(GetHealthChangeSide(damage.Target)),
                origin.Name,
                damage.ActualDamageDealt.ToString()
            },
            actor: origin.Owner?.DisplayName ?? GetHealthChangeSideLabel(HealthChangeSide.Neutral),
            target: damage.Target.DisplayName,
            source: origin.Id,
            card: origin.Kind == HealthChangeSourceKind.AttackAction ? BattleRules.GetCardName(damage.AttackType) : string.Empty,
            equipment: origin.Kind == HealthChangeSourceKind.Equipment ? origin.Name : string.Empty,
            skill: origin.Kind == HealthChangeSourceKind.Skill ? origin.Name : string.Empty,
            damageBefore: damage.BaseAmount,
            damageAfter: damage.ActualDamageDealt,
            hpBefore: hpBefore,
            hpAfter: hpAfter,
            result: damage.ActualDamageDealt > 0 ? "Applied" : "NoDamage");
        if (entry != null)
        {
            entry.HealthChangeKind = HealthChangeKind.Damage;
            entry.HealthChangeSourceKind = origin.Kind;
            entry.SourceSide = GetHealthChangeSide(origin.Owner);
            entry.TargetSide = GetHealthChangeSide(damage.Target);
        }
    }

    public void RecordHealResolved(Player source, Player target, int amount, int hpBefore, int hpAfter, HealthChangeSource origin)
    {
        if (amount <= 0) return;
        var entry = LogService?.RecordStructured(
            BattleLogEventKind.Heal, TurnNumber, Phase.ToString(), "battlelog.health_change",
            new[] { Localization.Get("health_change.heal"), GetHealthChangeSourceKindLabel(origin.Kind),
                GetHealthChangeSideLabel(GetHealthChangeSide(origin.Owner ?? source)), GetHealthChangeSideLabel(GetHealthChangeSide(target)), origin.Name, amount.ToString() },
            actor: (origin.Owner ?? source).DisplayName, target: target.DisplayName, source: origin.Id,
            card: origin.Kind == HealthChangeSourceKind.AttackAction ? origin.Name : string.Empty,
            equipment: origin.Kind == HealthChangeSourceKind.Equipment ? origin.Name : string.Empty,
            skill: origin.Kind == HealthChangeSourceKind.Skill ? origin.Name : string.Empty,
            hpBefore: hpBefore, hpAfter: hpAfter, result: "Applied");
        if (entry != null)
        {
            entry.HealthChangeKind = HealthChangeKind.Heal;
            entry.HealthChangeSourceKind = origin.Kind;
            entry.SourceSide = GetHealthChangeSide(origin.Owner ?? source);
            entry.TargetSide = GetHealthChangeSide(target);
        }
    }

    public void RecordDirectDamage(Player target, int requestedAmount, int actualAmount, int hpBefore, int hpAfter, HealthChangeSource origin)
    {
        var eventSource = origin.Owner ?? target;
        var damage = new DamageEvent(eventSource, target, CardType.Fee, requestedAmount,
            overrideDamageType: DamageType.Mechanic, origin: origin)
        {
            ActualDamageDealt = actualAmount
        };
        RecordDamageResolved(damage, hpBefore, hpAfter);
    }

    public HealthChangeSide GetHealthChangeSide(Player? unit)
        => unit == null ? HealthChangeSide.Neutral : unit == Player ? HealthChangeSide.Player : HealthChangeSide.Enemy;

    private static string GetHealthChangeSideLabel(HealthChangeSide side) => Localization.Get($"health_change.side.{side.ToString().ToLowerInvariant()}");
    private static string GetHealthChangeSourceKindLabel(HealthChangeSourceKind kind) => Localization.Get($"health_change.source.{kind.ToString().ToLowerInvariant()}");

    private static string GetEffectDebugName(IBattleEffect effect)
    {
        return effect switch
        {
            TurnStartWineEffect _ => "酒状态生效",
            BattlePrePhaseEffect _ => "战斗前准备",
            EquipmentBattlePrePhaseEffect _ => "装备战斗前效果",
            ParalysisDeviceEffect _ => "瘫痪装置",
            LianyingBattlePrePhaseEffect _ => "连营生效",
            LianyingResourceChangedEffect _ => "连营触发",
            BattlePhaseResolutionEffect _ => "卡牌结算",
            DefenseBeforeDamageEffect _ => "防御层",
            DebugInvincibilityEffect _ => "调试无敌",
            EquipmentBeforeDamageEffect _ => "装备防御",
            QingnangPeachEffect _ => "青囊",
            EquipmentDamageBonusEffect _ => "装备增伤",
            WineDamageBonusEffect _ => "酒增伤",
            KejiUnlimitedDamageBonusEffect _ => "克己·无限制协议",
            ApplyDamageEffect _ => "伤害生效",
            DamageTakenEffect _ => "濒死检查",
            AncestorBlessingDyingEffect _ => "先祖的赐福",
            DyingEffect _ => "濒死救援",
            DeathEffect _ => "死亡判定",
            BattleEndEffect _ => "战斗结束检查",
            BiyueSkillEffect _ => "闭月",
            LongdanReactionEffect _ => "龙胆",
            EquipmentBattlePostPhaseEffect _ => "装备战斗后效果",
            BattlePostPhaseEffect _ => "战斗后占位",
            TurnEndCleanupEffect _ => "回合清理",
            _ => effect.GetType().Name
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public BattleAction? GetAction(Player player)
    {
        if (player == Player)
        {
            return PlayerAction;
        }

        return player is EnemyInstance enemy ? GetActionForEnemy(enemy) : null;
    }

    /// <summary>
    /// Core System 的公开入口：IsPlayer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool IsPlayer(Player player)
    {
        return player == Player;
    }

    /// <summary>
    /// Core System 的公开入口：IsActionCancelled。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool IsActionCancelled(Player player)
    {
        if (player == Player)
        {
            return PlayerActionCancelled;
        }

        return player is EnemyInstance enemy && GetEnemyActionEntry(enemy)?.ActionCancelled == true;
    }
}

/// <summary>【黄天】逻辑层提交给战场表现层的单次视觉请求。</summary>
public sealed record HuangTianVisualRequest(Player Target, HuangTianVisualKind Kind);

/// <summary>小乌云用于【闪电】出现/转移；落雷用于20点真实结算。</summary>
public enum HuangTianVisualKind
{
    LightningApplied,
    LightningTransferred,
    LightningStruck
}

/// <summary>
/// Core System 的公开类：EnemyActionEntry。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyActionEntry
{
    /// <summary>
    /// Core System 的公开入口：EnemyActionEntry。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyActionEntry(EnemyInstance enemy, BattleAction action)
    {
        Enemy = enemy;
        Action = action;
    }

    public EnemyInstance Enemy { get; }
    public BattleAction Action { get; set; }
    public bool ActionCancelled { get; set; }
}

/// <summary>
/// Core System 的公开类：ResourceChangeEvent。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ResourceChangeEvent
{
    /// <summary>
    /// Core System 的公开入口：ResourceChangeEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public ResourceChangeEvent(Player owner, double before, double after, double amount, bool isCostPayment)
    {
        Owner = owner;
        Before = before;
        After = after;
        Amount = amount;
        IsCostPayment = isCostPayment;
    }

    public Player Owner { get; }
    public double Before { get; }
    public double After { get; }
    public double Amount { get; }
    public bool IsCostPayment { get; }
}

// 生命恢复事件：OnHeal 触发时填充，供 DanEffect 等效果读取恢复方与恢复量。
/// <summary>
/// Core System 的公开类：HealEvent。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HealEvent
{
    /// <summary>
    /// Core System 的公开入口：HealEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public HealEvent(Player healer, int amount)
    {
        Healer = healer;
        Amount = amount;
    }

    public Player Healer { get; }
    public int Amount { get; }
}

/// <summary>
/// Core System 的公开类：DamageEvent。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DamageEvent
{
    /// <summary>
    /// Core System 的公开入口：DamageEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public DamageEvent(Player source, Player target, CardType attackType, int amount, bool isDirectAttackDamage = false, DamageType? overrideDamageType = null, HealthChangeSource? origin = null, bool onlyAllowCounterOrCardShields = false)
    {
        Source = source;
        Target = target;
        AttackType = attackType;
        Amount = amount;
        BaseAmount = amount;
        IsDirectAttackDamage = isDirectAttackDamage;
        DamageType = overrideDamageType ?? DeriveFromAttackType(attackType);
        // 元素祭坛只影响玩家主动打出的攻击牌；装备、技能或反伤复用 Kill 作为
        // 日志类型时不能被错误转换成元素伤害。
        if (isDirectAttackDamage
            && source.Team == BattleTeam.Player
            && new Card(attackType).IsAttack)
        {
            DamageType = AttackAttributeRules.ToDamageType(AttackAttributeRules.GetAttributes(attackType));
        }
        Origin = origin ?? new HealthChangeSource(HealthChangeSourceKind.AttackAction, BattleRules.GetCardName(attackType), attackType.ToString(), source);
        OnlyAllowCounterOrCardShields = onlyAllowCounterOrCardShields;
    }

    private static DamageType DeriveFromAttackType(CardType type) => type switch
    {
        CardType.FireKill => DamageType.Fire,
        CardType.ThunderKill => DamageType.Thunder,
        CardType.IceKill => DamageType.Ice,
        CardType.FireThunderKill => DamageType.Fire | DamageType.Thunder,
        CardType.LightningStrike => DamageType.Thunder,
        CardType.FireAttack => DamageType.Fire,
        _ => DamageType.Physical
    };

    public Player Source { get; }
    public Player Target { get; }
    public CardType AttackType { get; }
    public DamageType DamageType { get; }
    public HealthChangeSource Origin { get; }
    public int BaseAmount { get; }
    public int Amount { get; set; }
    public DamageModifierPipeline Modifiers { get; } = new();
    public int FlatBonusTotal { get; private set; }
    public double WineMultiplier { get; private set; } = 1;
    public int SpecialMultiplier { get; private set; } = 1;
    public int VulnerableMultiplier { get; private set; } = 1;
    public int WushuangMultiplier { get; private set; } = 1;
    public int WineStacks { get; private set; }
    public int ActualDamageDealt { get; set; }
    public bool IsDirectAttackDamage { get; }
    /// <summary>
    /// 特殊伤害的防御白名单。为 true 时，只有无懈可击、桃护盾和酒护盾可以取消本次伤害；
    /// 闪与其它行动防御均不参与结算。
    /// </summary>
    public bool OnlyAllowCounterOrCardShields { get; }
    public bool Cancelled { get; set; }
    /// <summary>
    /// 目标因【涅槃】免疫而未扣血时，仍允许【铁索连环】把这次原始伤害同步给其它连锁单位。
    /// 仅用于该明确规则，不能泛化为所有被取消伤害都可继续传播。
    /// </summary>
    public bool PropagateThroughIronChainWhenCancelled { get; set; }
    public bool WasFullyBlocked { get; private set; }
    public bool IsResolved { get; private set; }

    /// <summary>
    /// 将本次伤害标记为被目标的防御效果完全格挡。
    ///
    /// 与普通 Cancelled 分开记录，避免把眩晕导致的攻击失败等情况错误显示为“格挡”。
    /// </summary>
    public void CancelAsFullyBlocked()
    {
        Cancelled = true;
        MarkAsFullyBlocked();
    }

    /// <summary>
    /// 标记伤害最终被数值型护盾完全抵消，但不提前终止伤害修正管线。
    /// </summary>
    public void MarkAsFullyBlocked()
    {
        WasFullyBlocked = true;
    }

    /// <summary>
    /// Core System 的公开入口：AddModifier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddModifier(DamageModifier modifier)
    {
        if (IsResolved)
        {
            return;
        }

        Modifiers.Add(modifier);
    }

    /// <summary>
    /// Core System 的公开入口：ResolveModifiers。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ResolveModifiers(int currentWineStacks = 0)
    {
        if (IsResolved)
        {
            return;
        }

        WineStacks = currentWineStacks;
        Amount = Modifiers.Resolve(BaseAmount);
        FlatBonusTotal = Modifiers.FlatBonusTotal;
        WineMultiplier = Modifiers.WineMultiplier;
        SpecialMultiplier = Modifiers.SpecialMultiplier;
        VulnerableMultiplier = Modifiers.VulnerableMultiplier;
        WushuangMultiplier = Modifiers.WushuangMultiplier;
        IsResolved = true;
    }
}
