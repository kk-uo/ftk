//////////////////////////////////////////////////////////
// 文件：Scripts/Damage/DamageEffects.cs
//
// 模块：Damage System
//
// 职责：
// 1. 承载伤害事件、伤害修正与命中后效果相关代码。
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

/// <summary>
/// Damage System 的公开类：DebugInvincibilityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DebugInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        if (damage.Target != context.Player || !context.Player.DebugInvincible)
        {
            return;
        }

        context.RoundResult.AddLine("调试无敌：玩家免疫本次伤害。");
        context.AddTriggerLog("[OnBeforeDamage]");
        context.AddTriggerLog("Branch=DebugInvincible");
        damage.Cancelled = true;
    }
}

/// <summary>
/// Damage System 的公开类：StunBeforeDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StunBeforeDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || !damage.Source.IsStunned)
        {
            return;
        }

        if (!BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        if (new Card(damage.AttackType).TargetType != CardTargetType.Targeted)
        {
            return;
        }

        if (Random.Shared.NextDouble() >= 0.5)
        {
            return;
        }

        damage.Cancelled = true;
        context.RoundResult.AddLine($"眩晕生效：{damage.Source.DisplayName}本次{BattleRules.GetCardName(damage.AttackType)}未造成伤害。");
        context.AddTriggerLog("[眩晕]");
        context.AddTriggerLog($"{damage.Source.DisplayName}处于眩晕状态，{BattleRules.GetCardName(damage.AttackType)}伤害被取消。");
    }
}

/// <summary>
/// Damage System 的公开类：DefenseBeforeDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DefenseBeforeDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        // ======================================================
        // 防御窗口
        // ======================================================
        // OnBeforeDamage 是“伤害是否成立”的最后确认阶段。
        // 闪、桃盾、酒盾、仁德盾等应在这里取消 DamageEvent，而不是在
        // ApplyDamageEffect 中扣血后再回滚。
        //
        // 这样做的原因：
        // - 被取消的伤害不会触发 OnDamageTaken 的命中后效果。
        // - 装备免疫、护盾、调试无敌都能共享 Cancelled 语义。
        // - 后续伤害修正管线只处理已经成立的伤害，逻辑更稳定。
        context.AddTriggerLog("[OnBeforeDamage]");
        context.AddTriggerLog($"Attack={BattleRules.GetCardName(damage.AttackType)}");
        context.AddTriggerLog($"Source={damage.Source.DisplayName}");
        context.AddTriggerLog($"Target={damage.Target.DisplayName}");

        var defenderIsPlayer = damage.Target == context.Player;

        // 【裸衣】穿透无懈、闪等招式防御关系；酒盾、桃盾等护盾仍会正常消耗并抵挡伤害。
        // 装备、场景与角色被动提供的独立免疫也继续走各自的触发链。
        var luoyiPiercesActionDefense = !defenderIsPlayer
            && ReferenceEquals(damage.Source, context.Player)
            && context.Player.HasSkill(SkillIds.Luoyi)
            && BattleRules.IsAnyAttackCard(damage.AttackType);

        if (!luoyiPiercesActionDefense && TryConsumeCounterDefense(context, defenderIsPlayer, damage.AttackType))
        {
            context.AddTriggerLog("Branch=CounterDefense");
            RouLinHelper.TryTrigger(context, damage);
            damage.CancelAsFullyBlocked();
            return;
        }

        // 酒与桃现在共享“一层抵挡一次完整伤害”的标准语义。元素、必中和锦囊只决定
        // 牌面克制，不再让同名护盾出现不同的穿透例外；对应酒/桃效果仍在消费层数时撤销。
        // 保留既有消费顺序，避免同一单位同时拥有两种卡牌护盾时发生隐式行为变化。
        if (TryConsumeWineShield(context, defenderIsPlayer))
        {
            RouLinHelper.TryTrigger(context, damage);
            damage.CancelAsFullyBlocked();
            context.AddTriggerLog("Branch=DefenseConsumed");
            // 如影随行：玩家攻击被酒护盾抵消
            if (!defenderIsPlayer && ReferenceEquals(damage.Source, context.Player))
                context.PlayerAttackAbsorbedByShield = true;
            return;
        }

        // 桃护盾同样抵挡任意进入正式 DamageEvent 管线的正伤害。
        if (TryConsumePeachShield(context, defenderIsPlayer))
        {
            RouLinHelper.TryTrigger(context, damage);
            damage.CancelAsFullyBlocked();
            context.AddTriggerLog("Branch=DefenseConsumed");
            // 如影随行：玩家攻击被桃护盾抵消
            if (!defenderIsPlayer && ReferenceEquals(damage.Source, context.Player))
                context.PlayerAttackAbsorbedByShield = true;
            return;
        }

        // 【自爆】是受限防御的特殊伤害：无懈、酒盾、桃盾之外的所有行动防御都无效。
        // 这个分支必须位于三种允许的防御之后、闪的处理之前。
        if (damage.OnlyAllowCounterOrCardShields)
        {
            context.AddTriggerLog("Branch=RestrictedDefensePassThrough");
            return;
        }

        if (luoyiPiercesActionDefense)
        {
            context.RoundResult.AddLine($"{damage.Source.DisplayName}【裸衣】：{damage.Target.DisplayName}的闪类防御无效。");
            context.AddTriggerLog("[裸衣] Branch=PierceActionDefense");
            context.ReportPlayerCharacterSkillTriggered(
                context.Player, SkillIds.Luoyi, Timing, Priority, "pierce_action_defense");
            return;
        }

        // 必中杀穿透闪类防御（青囊/普通闪）。
        if (new Card(damage.AttackType).AttackType == AttackType.DirectSha)
        {
            context.AddTriggerLog("Branch=DirectShaBypassDodge");
            return;
        }

        if (TryConsumeQingnangDodge(context, defenderIsPlayer)
            || TryConsumeDodge(context, defenderIsPlayer, damage.AttackType))
        {
            RouLinHelper.TryTrigger(context, damage);
            damage.CancelAsFullyBlocked();
            context.AddTriggerLog("Branch=DefenseConsumed");
            return;
        }

        context.AddTriggerLog("Branch=DamagePassThrough");
    }

    private static bool TryConsumeCounterDefense(BattleContext context, bool defenderIsPlayer, CardType attackType)
    {
        var hasCounterDefense = defenderIsPlayer
            ? context.PlayerCounterDefenseActive
            : context.DamageEvent?.Target is EnemyInstance enemy
                && context.EnemyCounterDefenseActive.TryGetValue(BattleContext.GetUnitStateKey(enemy), out var active)
                && active;

        if (!hasCounterDefense || !BattleRules.CanUnassailableCounter(attackType))
        {
            return false;
        }

        context.RoundResult.AddCounterDefenseBlock(defenderIsPlayer, attackType);
        if (attackType == CardType.ShadowKill)
        {
            var defenderName = defenderIsPlayer
                ? context.Player.DisplayName
                : (context.DamageEvent?.Target as EnemyInstance)?.DisplayName ?? "敌方";
            context.RoundResult.AddLine($"{defenderName}无懈可击抵消影袭杀。");
        }
        return true;
    }

    private static bool TryConsumeQingnangDodge(BattleContext context, bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            if (context.PlayerQingnangDodgeLayers <= 0)
            {
                return false;
            }

            context.PlayerQingnangDodgeLayers -= 1;
        }
        else
        {
            var targetEnemy = context.DamageEvent?.Target as EnemyInstance;
            var enemyKey = targetEnemy == null ? string.Empty : BattleContext.GetUnitStateKey(targetEnemy);
            if (string.IsNullOrEmpty(enemyKey)
                || !context.EnemyQingnangDodgeLayers.TryGetValue(enemyKey, out var layers)
                || layers <= 0)
            {
                return false;
            }

            context.EnemyQingnangDodgeLayers[enemyKey] = layers - 1;
        }

        context.RoundResult.AddQingnangDodgeBlock(defenderIsPlayer);
        return true;
    }

    private static bool TryConsumePeachShield(BattleContext context, bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            if (context.PlayerPeachShieldLayers <= 0) return false;
            context.PlayerPeachShieldLayers -= 1;
            // 普通桃在护盾触发时撤销对应治疗；青囊明确允许“格挡与治疗同时生效”，
            // 因此保留 PeachHealGranted，最终仍由统一治疗阶段结算并触发 OnHeal。
            if (!context.Player.HasSkill(SkillIds.Qingnang) && context.PlayerPeachHealGranted > 0)
            {
                context.PlayerPeachHealGranted -= 1;
            }
            var healState = context.Player.HasSkill(SkillIds.Qingnang)
                ? "青囊保留对应回复"
                : "对应回复撤销";
            context.RoundResult.AddLine($"桃护盾消耗1层，{healState}（剩余护盾 {context.PlayerPeachShieldLayers} 层，本回合可回复 {context.PlayerPeachHealGranted} 桃）。");
            if (context.Player.HasSkill(SkillIds.Qingnang))
            {
                context.ReportPlayerCharacterSkillTriggered(
                    context.Player, SkillIds.Qingnang, TriggerTiming.OnBeforeDamage, variant: "peach_block");
            }
        }
        else
        {
            var targetEnemy = context.DamageEvent?.Target as EnemyInstance;
            var enemyKey = targetEnemy == null ? string.Empty : BattleContext.GetUnitStateKey(targetEnemy);
            if (string.IsNullOrEmpty(enemyKey)
                || !context.EnemyPeachShieldLayers.TryGetValue(enemyKey, out var layers)
                || layers <= 0) return false;
            context.EnemyPeachShieldLayers[enemyKey] = layers - 1;
            if (targetEnemy?.HasSkill(SkillIds.Qingnang) != true
                && context.EnemyPeachHealGranted.TryGetValue(enemyKey, out var pending)
                && pending > 0)
            {
                context.EnemyPeachHealGranted[enemyKey] = pending - 1;
            }
            var healState = targetEnemy?.HasSkill(SkillIds.Qingnang) == true
                ? "，青囊保留对应回复"
                : string.Empty;
            context.RoundResult.AddLine($"桃护盾消耗1层{healState}（剩余 {context.EnemyPeachShieldLayers[enemyKey]} 层）。");
        }

        context.RoundResult.AddUniversalBlock(defenderIsPlayer);
        return true;
    }

    private static bool TryConsumeWineShield(BattleContext context, bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            if (context.PlayerWineShieldLayers <= 0) return false;
            context.PlayerWineShieldLayers -= 1;
            // 对应酒的增伤撤销（取消一层待激活的酒增伤）。
            context.Player.CancelOnePendingWinePower();
            context.RoundResult.AddLine($"酒护盾消耗1层，对应增伤撤销（剩余酒护盾 {context.PlayerWineShieldLayers} 层）。");
        }
        else
        {
            var targetEnemy = context.DamageEvent?.Target as EnemyInstance;
            var enemyKey = targetEnemy == null ? string.Empty : BattleContext.GetUnitStateKey(targetEnemy);
            if (string.IsNullOrEmpty(enemyKey)
                || targetEnemy == null
                || !context.EnemyWineShieldLayers.TryGetValue(enemyKey, out var layers)
                || layers <= 0) return false;
            context.EnemyWineShieldLayers[enemyKey] = layers - 1;
            targetEnemy.CancelOnePendingWinePower();
            context.RoundResult.AddLine($"酒护盾消耗1层，对应增伤撤销（剩余酒护盾 {context.EnemyWineShieldLayers[enemyKey]} 层）。");
        }

        context.RoundResult.AddWineBlock(defenderIsPlayer);
        return true;
    }

    private static bool TryConsumeDodge(BattleContext context, bool defenderIsPlayer, CardType attackType)
    {
        // 长弓/古锭刀：本次攻击强制忽略闪（仅对敌方防御方生效）。
        if (!defenderIsPlayer && context.IgnoreDefenderDodgeForThisAttack)
        {
            context.IgnoreDefenderDodgeForThisAttack = false;
            context.RoundResult.AddLine("（闪被无视）");
            return false;
        }

        var hasDodgeDefense = defenderIsPlayer
            ? context.PlayerDodgeDefenseActive
            : context.DamageEvent?.Target is EnemyInstance defendingEnemy
                && context.EnemyDodgeDefenseActive.TryGetValue(BattleContext.GetUnitStateKey(defendingEnemy), out var active)
                && active;

        if (!hasDodgeDefense)
        {
            return false;
        }

        // 火攻（周瑜专属）复用火杀的闪判定；只归一化用于分类比较的这个局部变量，
        // 日志/战报仍然使用原始 attackType，不会把火攻显示成火杀。
        var profileType = BattleRules.NormalizeCounterProfile(attackType);

        // 火雷杀含雷属性，穿透闪（与雷杀/必中杀/南蛮入侵一致）。冰杀同样穿透闪。
        // 天体撞击完整复用南蛮入侵的响应关系：闪只会显示为无效响应，不能在通用
        // 防御管线里第二次把已判定为命中的伤害错误抵消。
        if (profileType is CardType.ThunderKill or CardType.SureKill or CardType.NanmanInvasion
            or CardType.Tuxi or CardType.CelestialImpact or CardType.FireThunderKill or CardType.IceKill)
        {
            context.RoundResult.AddDodgeFailed(defenderIsPlayer, attackType);
            return false;
        }

        if (profileType is CardType.Kill or CardType.FireKill or CardType.ArrowBarrage)
        {
            context.RoundResult.AddPersistentDodgeBlock(defenderIsPlayer, attackType);
            return true;
        }

        if (defenderIsPlayer)
        {
            if (context.PlayerDodgeLayers <= 0)
            {
                return false;
            }

            if (attackType is CardType.ThunderKill or CardType.SureKill or CardType.FireThunderKill or CardType.IceKill)
            {
                context.RoundResult.AddDodgeFailed(defenderIsPlayer, attackType);
                return false;
            }

            if (attackType is CardType.Kill or CardType.FireKill)
            {
                context.RoundResult.AddPersistentDodgeBlock(defenderIsPlayer, attackType);
                return true;
            }

            context.PlayerDodgeLayers -= 1;
        }
        else
        {
            var targetEnemy = context.DamageEvent?.Target as EnemyInstance;
            var enemyKey = targetEnemy == null ? string.Empty : BattleContext.GetUnitStateKey(targetEnemy);
            if (string.IsNullOrEmpty(enemyKey)
                || !context.EnemyDodgeLayers.TryGetValue(enemyKey, out var layers)
                || layers <= 0)
            {
                return false;
            }

            if (attackType is CardType.ThunderKill or CardType.SureKill or CardType.FireThunderKill or CardType.IceKill)
            {
                context.RoundResult.AddDodgeFailed(defenderIsPlayer, attackType);
                return false;
            }

            if (attackType is CardType.Kill or CardType.FireKill)
            {
                context.RoundResult.AddPersistentDodgeBlock(defenderIsPlayer, attackType);
                return true;
            }

            context.EnemyDodgeLayers[enemyKey] = layers - 1;
        }

        context.RoundResult.AddDodgeBlock(defenderIsPlayer);
        return true;
    }
}

/// <summary>
/// Damage System 的公开类：EquipmentBeforeDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentBeforeDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    private const string EnemyBenevolentKingUsedKey = "benevolent_king_used";

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields)
        {
            return;
        }

        if (damage.Target == context.Player)
        {
            if (!GameManager.TryConsumeEquipmentUse(EquipmentIds.BenevolentKing))
            {
                return;
            }
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            if (!enemyTarget.HasEquipment(EquipmentIds.BenevolentKing))
            {
                return;
            }

            if (enemyTarget.RuntimeStates.ContainsKey(EnemyBenevolentKingUsedKey))
            {
                return;
            }

            enemyTarget.RuntimeStates[EnemyBenevolentKingUsedKey] = true;
        }
        else
        {
            return;
        }

        damage.CancelAsFullyBlocked();

        if (damage.Target is EnemyInstance && ReferenceEquals(damage.Source, context.Player))
        {
            context.PlayerAttackAbsorbedByShield = true;
        }

        context.RoundResult.AddLine("仁王触发：本局第一次受到伤害时免疫该次伤害。");
        context.AddTriggerLog("[Equipment]");
        context.AddTriggerLog("仁王：首次即将受到的伤害被免疫。");
    }
}

/// <summary>
/// 在 OnBeforeDamage 的所有防御效果执行完毕后，将完全格挡结果统一写入回合结算。
/// </summary>
public sealed class FullBlockRecordingEffect : IBattleEffect
{
    private readonly TriggerTiming _timing;

    /// <summary>
    /// 创建指定伤害阶段的完全格挡登记器。
    /// </summary>
    public FullBlockRecordingEffect(TriggerTiming timing = TriggerTiming.OnBeforeDamage)
    {
        _timing = timing;
    }

    public TriggerTiming Timing => _timing;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// 登记明确标记为完全格挡的目标；普通取消和零伤害修正不会进入该记录。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage?.WasFullyBlocked == true && damage.BaseAmount > 0)
        {
            context.RoundResult.AddFullBlock(damage.Target);
        }
    }
}

/// <summary>
/// Damage System 的公开类：EquipmentDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        var flatBonus = 0;

        if (damage.Source == context.Player)
        {
            // 玩家装备加伤（武器）。
            if (damage.AttackType == CardType.Kill)
            {
                flatBonus += GameManager.CountEquipment(EquipmentIds.RustSword) * 7;
                flatBonus += GameManager.CountEquipment(EquipmentIds.HejinJian) * 5;
            }

            if (damage.AttackType == CardType.FireKill)
            {
                flatBonus += GameManager.CountEquipment(EquipmentIds.Gunpowder) * 4;
            }
            else if (damage.AttackType == CardType.ThunderKill)
            {
                flatBonus += GameManager.CountEquipment(EquipmentIds.Gunpowder) * 4;
            }
            else if (damage.AttackType == CardType.FireThunderKill)
            {
                flatBonus += GameManager.CountEquipment(EquipmentIds.Gunpowder) * 8;
            }

            // 蜀·兵谋同源生效时，玩家侧杀类型武器加伤额外对攻击性锦囊牌生效——切到兼容感知版判定；
            // 敌方（下方 enemySource 分支）不受玩家阵营命运影响，故只在这里（玩家侧）替换，不动敌方分支。
            if (BattleRules.IsShaAttackWithFactionCompat(damage.AttackType))
            {
                flatBonus += GameManager.CountEquipment(EquipmentIds.RustBlueSteelSword) * 5;
                flatBonus += GameManager.CountEquipment(EquipmentIds.BlueSteelSword) * 8;
                flatBonus += GameManager.CountEquipment(EquipmentIds.TrueBlueSteelSword) * 15;
                flatBonus += GameManager.CountEquipment(EquipmentIds.HejinMao) * 10;
                flatBonus += GameManager.CountEquipment(EquipmentIds.AttackChip) * 3;
                // 本局杀系加伤（石碑血祭）；与装备加成叠加，不替换。
                flatBonus += GameManager.RunKillDamageBonus;
            }
        }
        else if (damage.Source is EnemyInstance enemySource)
        {
            // 敌方装备加伤：与玩家共用 EquipmentDefinition，仅计数该敌人自身携带的装备。
            if (BattleRules.IsShaAttack(damage.AttackType))
            {
                flatBonus += enemySource.CountEquipment(EquipmentIds.RustBlueSteelSword) * 5;
                flatBonus += enemySource.CountEquipment(EquipmentIds.BlueSteelSword) * 8;
                flatBonus += enemySource.CountEquipment(EquipmentIds.TrueBlueSteelSword) * 15;
                flatBonus += enemySource.CountEquipment(EquipmentIds.AttackChip) * 3;
                // 蜀汉共生体 Boss 专属武器加伤。
                if (enemySource.HasEquipment(EquipmentIds.ZhangBaSheMao))
                    flatBonus += 5;
                if (enemySource.HasEquipment(EquipmentIds.QingLongYanYueDao))
                    flatBonus += 5;
            }
        }

        if (flatBonus > 0)
        {
            damage.AddModifier(new DamageModifier(
                "装备/芯片固定加伤",
                DamageModifierPriority.FlatBonus,
                DamageModifierOperation.Add,
                flatBonus));
            context.RoundResult.AddLine($"加伤生效：{BattleRules.GetCardName(damage.AttackType)}伤害 +{flatBonus}。");
            context.AddTriggerLog("[Equipment/Chip]");
            context.AddTriggerLog($"固定加伤：{BattleRules.GetCardName(damage.AttackType)} +{flatBonus}");
        }

        // 攻击锦囊加伤（知识芯片×5 + 石碑选项一）：除攻击性锦囊外，
        // 张角【黄天】制造的【闪电】也明确享受该加成。它仍是技能伤害，
        // 不会因此变成可响应的攻击牌或影响卡牌克制关系。
        // 蜀·兵谋同源生效时，杀类型牌额外视为满足这个标签——切到兼容感知版判定。
        var usesAttackTrickBonus = BattleRules.IsAttackingTrickWithFactionCompat(damage.AttackType)
            || (damage.AttackType == CardType.LightningStrike
                && damage.Origin.Kind == HealthChangeSourceKind.Skill
                && damage.Origin.Id == SkillIds.HuangTian);
        if (damage.Source == context.Player && usesAttackTrickBonus)
        {
            var trickBonus = GameManager.AttackTrickDamageBonus;
            if (trickBonus > 0)
            {
                damage.AddModifier(new DamageModifier(
                    "攻击锦囊加伤",
                    DamageModifierPriority.FlatBonus,
                    DamageModifierOperation.Add,
                    trickBonus));
                context.RoundResult.AddLine($"攻击锦囊加伤生效：{BattleRules.GetCardName(damage.AttackType)}伤害 +{trickBonus}。");
                context.AddTriggerLog("[ChipSystem/Stele]");
                context.AddTriggerLog($"攻击锦囊加伤：{BattleRules.GetCardName(damage.AttackType)} +{trickBonus}");
            }
            return;
        }

        if (flatBonus <= 0)
        {
            return;
        }
    }
}

/// <summary>
/// Damage System 的公开类：ElementRunBuffDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ElementRunBuffDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source != context.Player)
        {
            return;
        }

        var bonus = RunBuffManager.GetPlayerElementDamageFlatBonus(damage.DamageType);
        if (bonus <= 0)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "元素强化",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            bonus));
        context.RoundResult.AddLine($"元素强化生效：伤害 +{bonus}。");
        context.AddTriggerLog("[RunBuff/Element]");
        context.AddTriggerLog($"元素强化：DamageType={damage.DamageType} +{bonus}");
    }
}

/// <summary>
/// Damage System 的公开类：WineDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WineDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source.WinePower <= 0 || !BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "酒",
            DamageModifierPriority.WineMultiplier,
            DamageModifierOperation.MultiplyFloat,
            BattleRules.GetWineDamageMultiplier(damage.Source, damage.Source.WinePower)));
    }
}

/// <summary>
/// Damage System 的公开类：KejiUnlimitedDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class KejiUnlimitedDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!ReferenceEquals(damage.Source, context.Player)) return;
        if (!context.Player.HasSkill(SkillIds.KejiUnlimited)) return;

        var mana = System.Math.Min(20.0, context.Player.CurrentMana);
        var multiplier = System.Math.Min(3.0, 1.0 + mana * 0.1);
        if (multiplier <= 1.0) return;

        damage.AddModifier(new DamageModifier(
            "克己·无限制协议",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.AddTriggerLog("[克己·无限制协议]");
        context.AddTriggerLog($"当前费用 {mana:0.#} 点 → 伤害 ×{multiplier:0.0}（+{(multiplier - 1) * 100:0}%）。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.KejiUnlimited, Timing, Priority, "damage_bonus");
    }
}

/// <summary>
/// Damage System 的公开类：RunBuffDamageTakenEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RunBuffDamageTakenEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Target != context.Player)
        {
            return;
        }

        var multiplier = RunBuffManager.GetPlayerDamageTakenMultiplier();
        if (multiplier <= 1d)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "诅咒",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.RoundResult.AddLine($"诅咒生效：玩家受到伤害×{multiplier:0.##}。");
        context.AddTriggerLog("[RunBuff/Curse]");
        context.AddTriggerLog($"诅咒：玩家受到伤害倍率 ×{multiplier:0.##}");
    }
}

// 煞气缠身：玩家造成所有伤害×1.5（SpecialMultiplier 阶段）。
/// <summary>
/// Damage System 的公开类：ShaQiDamageOutputEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ShaQiDamageOutputEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source != context.Player) return;

        var multiplier = RunBuffManager.GetPlayerDamageDealtMultiplier();
        if (multiplier <= 1d) return;

        damage.AddModifier(new DamageModifier(
            "煞气缠身",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.RoundResult.AddLine($"煞气缠身生效：玩家造成伤害×{multiplier:0.##}。");
        context.AddTriggerLog("[RunBuff/ShaQiChenShen]");
        context.AddTriggerLog($"煞气缠身：玩家造成伤害倍率 ×{multiplier:0.##}");
    }
}

// 敌方最终伤害倍率：合并 RunBuff 与当前特殊战斗倍率，在 FinalMultiplier 阶段统一结算。
/// <summary>
/// Damage System 的公开类：RunBuffEnemyDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RunBuffEnemyDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source == context.Player) return;

        var multiplier = RunBuffManager.GetEnemyDamageDealtMultiplier()
            * GameManager.ActiveSpecialBattleEnemyFinalDamageMultiplier;
        if (multiplier <= 1d) return;

        damage.AddModifier(new DamageModifier(
            GameManager.ActiveSpecialBattleId == "qixingtan" ? "招魂强化" : "敌方伤害强化",
            DamageModifierPriority.FinalMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.RoundResult.AddLine($"敌方伤害强化：敌人造成伤害×{multiplier:0.##}。");
        context.AddTriggerLog("[EnemyFinalDamageMultiplier]");
        context.AddTriggerLog($"敌方最终伤害倍率 ×{multiplier:0.##}");
    }
}

// 朱雀羽扇：玩家使用火杀时伤害×2；若玩家为周瑜，火攻同样×2。
/// <summary>
/// Damage System 的公开类：ZhuQueYuShanEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ZhuQueYuShanEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source != context.Player) return;
        if (damage.Origin.Kind != HealthChangeSourceKind.AttackAction) return;
        if (!GameManager.HasEquipment(EquipmentIds.ZhuQueYuShan)) return;

        var fireKill = damage.AttackType == CardType.FireKill;
        var zhouYuFireAttack = damage.AttackType == CardType.FireAttack
            && string.Equals(damage.Source.Character.Data.Id, CharacterIds.ZhouYu, System.StringComparison.Ordinal);
        if (!fireKill && !zhouYuFireAttack) return;

        var effectName = fireKill
            ? "朱雀羽扇（火杀加倍）"
            : "朱雀羽扇（周瑜火攻加倍）";
        var effectDescription = fireKill
            ? "你的火杀伤害加倍。"
            : "周瑜的火攻伤害加倍。";

        damage.AddModifier(new DamageModifier(
            effectName,
            DamageModifierPriority.ArmorEquipmentMultiplier,
            DamageModifierOperation.Multiply,
            2));
        context.RoundResult.AddLine($"朱雀羽扇触发：{effectDescription}");
        context.AddTriggerLog("[朱雀羽扇]");
        context.AddTriggerLog($"朱雀羽扇：{effectDescription}");
    }
}

/// <summary>
/// Damage System 的公开类：WushuangDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WushuangDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || !damage.Source.HasSkill(SkillIds.Wushuang))
        {
            return;
        }

        if (!BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        var wushuangMultiplier = GameManager.HasEquipment(EquipmentIds.ChiTu)
            && string.Equals(GameManager.CurrentCharacterId, CharacterIds.LuBu, System.StringComparison.Ordinal)
            ? 3 : 2;
        damage.AddModifier(new DamageModifier(
            "无双",
            DamageModifierPriority.Wushuang,
            DamageModifierOperation.Multiply,
            wushuangMultiplier));
        var wushuangDesc = wushuangMultiplier == 3 ? "伤害×3（赤兔强化）" : "伤害翻倍";
        context.RoundResult.AddLine($"{damage.Source.DisplayName}【无双】生效，{wushuangDesc}。");
        context.ReportPlayerCharacterSkillTriggered(
            damage.Source,
            SkillIds.Wushuang,
            Timing,
            Priority,
            wushuangMultiplier == 3 ? "chitu" : "default");
        context.AddTriggerLog("[无双]");
        context.AddTriggerLog("Trigger: OnDamage");
        context.AddTriggerLog("Priority: Wushuang");
        context.AddTriggerLog($"{BattleRules.GetCardName(damage.AttackType)}属于杀系攻击牌，最终伤害翻倍。");
    }
}

// 藤甲：受到物理属性伤害（DamageType.Physical）时最终伤害减半（÷2）；
//       受到任意火属性伤害（DamageType.Fire）时最终伤害加倍（×2）。
// 判断依据为 DamageEvent.DamageType 标志，而非具体牌型，因此：
//   Physical → 普通杀 / 南蛮入侵 / 万箭齐发 等所有 Physical 伤害
//   Fire     → 火杀 / 火雷杀 等所有含 Fire 标志的伤害
// 两个效果均在 ArmorEquipmentMultiplier 优先级区处理（加法完成后、无双之前）。
// 同时对玩家和敌方单位生效——检查受击方（Target）是否携带藤甲。
/// <summary>
/// Damage System 的公开类：TengjiaEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TengjiaEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        // 检查受击方是否携带藤甲：玩家方查 GameManager，敌方查 EnemyInstance.Equipments。
        bool targetHasTengjia;
        if (damage.Target == context.Player)
        {
            targetHasTengjia = GameManager.HasEquipment(EquipmentIds.Tengjia);
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            targetHasTengjia = enemyTarget.HasEquipment(EquipmentIds.Tengjia);
        }
        else
        {
            return;
        }

        if (!targetHasTengjia)
        {
            return;
        }

        // 物理伤害减半（Physical 标志：普通杀、南蛮入侵、万箭齐发等）
        if (damage.DamageType.HasFlag(DamageType.Physical))
        {
            damage.AddModifier(new DamageModifier(
                "藤甲（物理减半）",
                DamageModifierPriority.ArmorEquipmentMultiplier,
                DamageModifierOperation.Divide,
                2));
            context.RoundResult.AddLine($"藤甲触发（{damage.Target.DisplayName}）：受到物理伤害减半。");
            context.AddTriggerLog("[Equipment]");
            context.AddTriggerLog($"藤甲：{damage.Target.DisplayName} 物理伤害最终 ÷2（DamageType={damage.DamageType}）。");
        }

        // 火属性伤害加倍（Fire 标志：火杀、火雷杀等所有含 Fire 的伤害）
        if (damage.DamageType.HasFlag(DamageType.Fire))
        {
            damage.AddModifier(new DamageModifier(
                "藤甲（火属性加倍）",
                DamageModifierPriority.ArmorEquipmentMultiplier,
                DamageModifierOperation.Multiply,
                2));
            context.RoundResult.AddLine($"藤甲触发（{damage.Target.DisplayName}）：受到火属性伤害加倍。");
            context.AddTriggerLog("[Equipment]");
            context.AddTriggerLog($"藤甲：{damage.Target.DisplayName} 火属性伤害最终 ×2（DamageType={damage.DamageType}）。");
        }
    }
}

// 鳞甲：每次受到伤害时，单次最终伤害上限为15（在所有加法和乘法完成后封顶）。
// 每回合每个生命单位（共享HP池视为一个单位）最多触发一次。
// 同时对玩家和敌方单位生效——检查受击方（Target）是否携带鳞甲。
/// <summary>
/// Damage System 的公开类：ScaleArmorEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ScaleArmorEffect : IBattleEffect
{
    private const int DamageCapValue = 15;

    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        bool targetHasScaleArmor;
        string armorKey;
        if (damage.Target == context.Player)
        {
            targetHasScaleArmor = GameManager.HasEquipment(EquipmentIds.ScaleArmor);
            armorKey = "player";
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            targetHasScaleArmor = enemyTarget.HasEquipment(EquipmentIds.ScaleArmor)
                || (enemyTarget.SharedPool != null && HasSharedPoolMemberWithScaleArmor(enemyTarget.SharedPool));
            armorKey = enemyTarget.SharedPool != null
                ? "pool_" + enemyTarget.SharedPool.Members[0].Id
                : enemyTarget.Id;
        }
        else
        {
            return;
        }

        if (!targetHasScaleArmor)
        {
            return;
        }

        if (!context.ScaleArmorTriggeredThisRound.Add(armorKey))
        {
            context.AddTriggerLog($"鳞甲：{damage.Target.DisplayName}本回合已触发，跳过。");
            return;
        }

        damage.AddModifier(new DamageModifier(
            $"鳞甲（伤害上限{DamageCapValue}）",
            DamageModifierPriority.DamageCap,
            DamageModifierOperation.Cap,
            DamageCapValue));
        context.AddTriggerLog("[Equipment]");
        context.AddTriggerLog($"鳞甲：{damage.Target.DisplayName}伤害上限 {DamageCapValue}。");
    }

    private static bool HasSharedPoolMemberWithScaleArmor(SharedHealthPool pool)
    {
        foreach (var member in pool.Members)
        {
            if (member.HasEquipment(EquipmentIds.ScaleArmor))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Damage System 的公开类：ApplyDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ApplyDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        // ======================================================
        // 伤害修正管线
        // ======================================================
        // 所有加法、倍率、易伤、酒增伤等修正先进入 DamageModifierPipeline，
        // 最后在这里一次性 Resolve。不要在各个效果里直接改 HP。
        //
        // 设计原因：
        // - 修正顺序可审计，Debug Log 能打印完整管线。
        // - 叠加规则集中，避免装备和技能分别覆盖最终伤害。
        // - ActualDamageDealt 在扣血后记录，供“命中后”效果判断是否真正造成伤害。
        damage.ResolveModifiers(damage.Source.WinePower);
        context.RoundResult.AddWineDamageBonus(
            damage.Source.DisplayName,
            damage.WineStacks,
            damage.WineMultiplier,
            damage.Amount);
        context.AddTriggerLog("[DamageModifierPipeline]");
        foreach (var modifier in damage.Modifiers.Modifiers)
        {
            context.AddTriggerLog($"{modifier.Name} | Priority={(int)modifier.Priority} | {modifier.Operation} {modifier.Value}");
        }

        var healthBeforeDamage = damage.Target.CurrentHP;
        damage.Target.TakeDamage(damage.Amount);
        damage.ActualDamageDealt = System.Math.Max(0, healthBeforeDamage - damage.Target.CurrentHP);
        context.RoundResult.AddDamageCalculation(
            damage.Source.DisplayName,
            damage.Target.DisplayName,
            BattleRules.GetCardName(damage.AttackType),
            damage.BaseAmount,
            damage.FlatBonusTotal,
            damage.WineMultiplier,
            damage.SpecialMultiplier,
            damage.VulnerableMultiplier,
            damage.WushuangMultiplier,
            damage.WineStacks,
            damage.ActualDamageDealt);
        context.RoundResult.AddDamage(damage.Target, damage.ActualDamageDealt);
        context.RecordDamageResolved(damage, healthBeforeDamage, damage.Target.CurrentHP);
    }
}

/// <summary>
/// Damage System 的公开类：DamageTakenEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DamageTakenEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Target.Health > 0)
        {
            return;
        }

        // 有反应明确要求先完成玩家选择时，暂不进入同步死亡流程。反应完成后 BattleManager
        // 会携带原 DamageEvent 重新 Raise OnDying，因此这里只延后，不取消濒死。
        if (context.Reactions.HasDeferredDyingReaction(damage.Target))
        {
            context.AddTriggerLog($"{damage.Target.DisplayName}的濒死结算等待反应选择完成。");
            return;
        }

        context.RaiseOnDying();
    }
}

/// <summary>
/// Damage System 的公开类：DyingEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DyingEffect : IBattleEffect
{
    private const int PeachReviveCostMultiplier = 2;
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target == null || target.Health > 0 || target.DyingState == DyingState.Dead)
        {
            return;
        }

        target.EnterDying();
        context.RoundResult.AddLine($"{target.DisplayName}进入濒死状态。");
        if (ReferenceEquals(target, context.Player))
        {
            MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstNearDeath);
        }

        if (TryUseSilverLionRevive(context, target) || TryUsePeachRevive(context, target) || TryUseWineRevive(context, target))
        {
            target.RecoverFromDying();
            return;
        }

        context.RoundResult.AddLine("救援失败。");
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDeath, context);
    }

    private static bool TryUseSilverLionRevive(BattleContext context, Player target)
    {
        if (target != context.Player || !GameManager.TryConsumeEquipmentUse(EquipmentIds.SilverLion))
        {
            return false;
        }

        target.DebugSetHealth(10);
        context.RoundResult.AddLine("白银狮子触发：本局第一次死亡时生命重置为10。");
        context.AddTriggerLog("[Equipment]");
        context.AddTriggerLog("白银狮子：濒死时生命重置为10。");
        return target.Health > 0;
    }

    private static bool TryUsePeachRevive(BattleContext context, Player target)
    {
        var basePeachCost = BattleRules.GetCardCost(target, CardType.Peach);
        // 每次濒死时实际使用【桃】后，下一次濒死桃救援费用再翻倍。
        // 因此费用序列为基础费用、×2、×4、×8……；计数仅在战斗开始时重置。
        var peachCost = basePeachCost * Math.Pow(PeachReviveCostMultiplier, target.DyingPeachReviveUses);
        if (target.CurrentMana < peachCost)
        {
            return false;
        }

        BattleRules.PayManaAndRaiseResourceChanged(context, target, peachCost, true);
        var healed = BattleHealing.Apply(
            context,
            target,
            GameManager.GetPeachHealAmountFor(target),
            target.HasSkill(SkillIds.Qingnang),
            new HealthChangeSource(HealthChangeSourceKind.AttackAction, BattleRules.GetCardName(CardType.Peach), CardType.Peach.ToString(), target)).HealedAmount;
        target.MarkDyingPeachReviveUsed();
        var useCount = target.DyingPeachReviveUses;
        context.RoundResult.AddLine($"{target.DisplayName}使用桃进行救援（第{useCount}次，消耗{BattleRules.FormatMana(peachCost)}费）。");
        context.RoundResult.AddLine($"恢复{healed}生命。");
        if (healed > 0 && target.HasSkill(SkillIds.Qingnang))
        {
            context.ReportPlayerCharacterSkillTriggered(
                target, SkillIds.Qingnang, TriggerTiming.OnDying, variant: "rescue");
        }
        CodexService.RecordCardSpecial(CardType.Peach);
        return target.Health > 0;
    }

    private static bool TryUseWineRevive(BattleContext context, Player target)
    {
        if (target != context.Player)
            return false;

        if (target.HasUsedWineRevive)
        {
            return false;
        }

        // 酒馆·点杯酒：GameManager.HasFreeWineRevive 为本局永久生效的费用减免，
        // 濒死救援不再需要实际支付酒的费用（原本需要 wine.Cost 点费用，费用不足时救援失败）。
        var free = GameManager.HasFreeWineRevive;
        var wine = Card.Wine();
        if (!free && !target.CanPay(wine))
        {
            return false;
        }

        if (!free)
        {
            BattleRules.PayManaAndRaiseResourceChanged(context, target, wine.Cost, true);
        }

        target.MarkWineReviveUsed();
        var healed = BattleHealing.Apply(
            context,
            target,
            BattleConstants.PeachHeal,
            false,
            new HealthChangeSource(HealthChangeSourceKind.AttackAction, BattleRules.GetCardName(CardType.Wine), CardType.Wine.ToString(), target)).HealedAmount;
        context.RoundResult.AddLine($"{target.DisplayName}使用酒进行救援。");
        context.RoundResult.AddLine($"恢复{healed}生命。");
        CodexService.RecordCardSpecial(CardType.Wine);
        return target.Health > 0;
    }
}

/// <summary>
/// Damage System 的公开类：DeathEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DeathEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDeath;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target == null || target.DyingState == DyingState.Dead)
        {
            return;
        }

        target.MarkDead();
        target.ClearStatuses();
        context.RoundResult.AddLine($"{target.DisplayName}死亡。");
        if (target == context.Player)
        {
            context.PlayerDiedThisRound = true;

            // 双方在同一回合内都被伤害击倒（典型场景：战场崩坏把双方生命扣至0）：
            // 按设计判定为玩家胜利（互伤胜利），不让"谁的死亡先被结算处理"这种
            // 和处理顺序有关的偶然性决定输赢。
            if (context.AllEnemiesDiedThisRound)
            {
                context.GameOver = true;
                context.Outcome = BattleOutcome.Victory;
                context.GameOverText = Localization.Get("battle.gameover.victory");
                context.MutualCollapseVictory = true;
                context.RoundResult.AddLine("双方本回合同时倒下，判定为互伤胜利。");
                return;
            }

            context.GameOver = true;
            context.Outcome = BattleOutcome.Defeat;
            context.GameOverText = Localization.Get("battle.gameover.defeat");
            return;
        }

        // 图鉴：本次死亡结算里所有"真正被标记为死亡"的敌人实体，按 EnemyDefinition.Id
        // 去重后各记一次击败——共享HP Pool耗尽时最多再带出若干个 sibling 一起死亡，
        // 但如果它们和 target 是同一个 Definition.Id（多个显示实体共享一条血条），
        // 只应该算一次，不能因为"看起来死了3个"就记3次不存在的击杀。
        var defeatedDefinitionIds = new System.Collections.Generic.HashSet<string>();
        if (target is EnemyInstance deadEnemy)
        {
            defeatedDefinitionIds.Add(deadEnemy.Definition.Id);
        }

        // 共享生命池耗尽时，令所有成员同时死亡。
        if (target is EnemyInstance deadPoolMember && deadPoolMember.SharedPool != null && deadPoolMember.SharedPool.IsDepleted)
        {
            foreach (var sibling in deadPoolMember.SharedPool.Members)
            {
                if (!sibling.IsDead)
                {
                    sibling.MarkDead();
                    sibling.ClearStatuses();
                    context.RoundResult.AddLine($"{sibling.DisplayName}（共享生命池耗尽）随之倒下。");
                    defeatedDefinitionIds.Add(sibling.Definition.Id);
                }
            }
        }

        foreach (var definitionId in defeatedDefinitionIds)
        {
            CodexService.RecordEnemyDefeated(definitionId, context.TurnCounter);
        }

        var allEnemiesDead = true;
        if (context.Encounter != null && context.Encounter.Enemies.Count > 0)
        {
            foreach (var enemy in context.Encounter.Enemies)
            {
                if (!enemy.IsDead)
                {
                    allEnemiesDead = false;
                    break;
                }
            }
        }
        else
        {
            allEnemiesDead = true;
        }

        if (allEnemiesDead)
        {
            context.AllEnemiesDiedThisRound = true;
            context.GameOver = true;
            context.Outcome = BattleOutcome.Victory;
            context.GameOverText = Localization.Get("battle.gameover.victory");

            // 对称处理：如果玩家本回合也已经先一步死亡（同一回合互相击倒），
            // 同样打上互伤胜利标记，供 GameManager 结算战后血量时识别。
            if (context.PlayerDiedThisRound)
            {
                context.MutualCollapseVictory = true;
                context.RoundResult.AddLine("双方本回合同时倒下，判定为互伤胜利。");
            }
        }
    }
}
