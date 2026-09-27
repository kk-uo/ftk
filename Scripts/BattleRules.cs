//////////////////////////////////////////////////////////
// 文件：Scripts/BattleRules.cs
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

/// <summary>
/// Core System 的公开类：BattleRules。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleRules
{
    /// <summary>
    /// Core System 的公开入口：FormatMana。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string FormatMana(double amount)
    {
        return Math.Abs(amount % 1) < 0.001 ? ((int)amount).ToString() : amount.ToString("0.#");
    }

    /// <summary>
    /// Core System 的公开入口：GetAttackDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetAttackDamage(int wineLayers)
    {
        return BattleConstants.KillDamage * GetWineDamageMultiplier(wineLayers);
    }

    /// <summary>
    /// Core System 的公开入口：GetWineDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetWineDamageBonus(int wineLayers)
    {
        return BattleConstants.KillDamage * wineLayers;
    }

    /// <summary>
    /// Core System 的公开入口：GetWineDamageMultiplier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetWineDamageMultiplier(int wineLayers)
    {
        return 1 + wineLayers;
    }

    /// <summary>
    /// Core System 的公开入口：GetWineDamageMultiplier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetWineDamageMultiplier(Player source, int wineLayers)
    {
        var baseMultiplier = GetWineDamageMultiplier(wineLayers);
        return source.Team == BattleTeam.Player
            ? baseMultiplier + GameManager.WineDamageMultiplierBonus
            : baseMultiplier;
    }

    /// <summary>
    /// Core System 的公开入口：GetCardCost。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetCardCost(Player player, CardType type, BattleUnit? target = null)
    {
        // 影袭：费用由玩家的 ShadowCost 状态机决定（0/1/2）；本场战斗第一次激活免费，
        // 这里必须和 BattlePhaseResolutionEffect.PayActionCost 的实际扣费判断保持一致，
        // 否则会出现"显示1费、实际扣0费"的不一致（原bug）。
        if (type == CardType.YingXiActivate && player.HasSkill(SkillIds.YingXi))
        {
            return player.YingXiFirstUseFreeAvailable ? 0 : player.ShadowCost;
        }

        if (type == CardType.Peach && player.HasSkill(SkillIds.Qingnang))
        {
            return 1;
        }

        if (type == CardType.Wine && HasEquipment(player, EquipmentIds.XianNiang))
        {
            return 0;
        }

        // 蛮族/蛮族之王（孟获专属）：南蛮入侵费用 3 → 2。
        if (type == CardType.NanmanInvasion
            && (player.HasSkill(SkillIds.Manzu) || player.HasSkill(SkillIds.ManzuWang)))
        {
            return 2;
        }

        if (type == CardType.Wine
            && player.Team == BattleTeam.Player
            && GameManager.GetRemainingEquipmentUses(EquipmentIds.WinePouch) > 0)
        {
            return 0;
        }

        // 诸葛连弩：持有者（玩家或敌方）普通杀费用降为0。
        if (type == CardType.Kill)
        {
            var playerOwns = player.Team == BattleTeam.Player && GameManager.HasEquipment(EquipmentIds.ZhugeCrossbow);
            var enemyOwns = player is EnemyInstance crossbowEnemy && crossbowEnemy.HasEquipment(EquipmentIds.ZhugeCrossbow);
            if (playerOwns || enemyOwns)
            {
                return 0;
            }
        }

        // 火纹银枪：持有者（玩家或敌方）火杀费用降为1（原为2）。
        if (type == CardType.FireKill)
        {
            var playerOwns = player.Team == BattleTeam.Player && GameManager.HasEquipment(EquipmentIds.FirePatternSilverSpear);
            var enemyOwns = player is EnemyInstance spearEnemy && spearEnemy.HasEquipment(EquipmentIds.FirePatternSilverSpear);
            if (playerOwns || enemyOwns)
            {
                return 1;
            }
        }

        // 雷矛：持有者（玩家或敌方）雷杀费用降为1（原为2）。
        if (type == CardType.ThunderKill)
        {
            var playerOwns = player.Team == BattleTeam.Player && GameManager.HasEquipment(EquipmentIds.ThunderSpear);
            var enemyOwns = player is EnemyInstance thunderEnemy && thunderEnemy.HasEquipment(EquipmentIds.ThunderSpear);
            if (playerOwns || enemyOwns)
            {
                return 1;
            }
        }

        // 真·古锭刀：费用取决于这次行动真正锁定的敌人。目标参数由卡面预览和
        // BattleAction 正式扣费共同传入，防止显示1费但结算仍扣3费。
        if (type == CardType.FireThunderKill
            && HasEquipment(player, EquipmentIds.TrueGuDingDao)
            && target is EnemyInstance targetEnemy
            && targetEnemy.CurrentMana <= 0)
        {
            return 1;
        }

        var baseCost = new Card(type).Cost;

        // 谦逊（陆逊专属）：所有非攻击类锦囊费用-0.5（最低0）。
        if (player.HasSkill(SkillIds.Qianxun)
            && type is CardType.Wine or CardType.Peach or CardType.Steal or CardType.Unassailable or CardType.Guanxing)
        {
            return Math.Max(0, baseCost - 0.5);
        }

        return baseCost;
    }

    /// <summary>
    /// Core System 的公开入口：HasEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasEquipment(Player player, string equipmentId)
    {
        return player.Team == BattleTeam.Player
            ? GameManager.HasEquipment(equipmentId)
            : player is EnemyInstance enemy && enemy.HasEquipment(equipmentId);
    }

    /// <summary>
    /// 判断持有者当前是否应使用真·古锭刀提供的火雷杀行动组。
    ///
    /// 该判断只读取已装备状态，不修改 Run 牌池，因此卸下装备后原有牌型会立即恢复。
    /// </summary>
    public static bool UsesTrueGuDingDaoActionSet(Player player)
    {
        return HasEquipment(player, EquipmentIds.TrueGuDingDao);
    }

    /// <summary>
    /// Core System 的公开入口：GetWinePlayLimitPerTurn。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetWinePlayLimitPerTurn(Player player)
    {
        return player.HasSkill(SkillIds.JiuChi) || HasEquipment(player, EquipmentIds.XianNiang)
            ? 3
            : int.MaxValue;
    }

    /// <summary>
    /// Core System 的公开入口：GetActionCost。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetActionCost(Player player, BattleAction action)
    {
        if (action.IsWine)
        {
            // 酒池（董卓专属）：前N张酒（FreeWineUsesRemaining）免费，玩家与AI均生效，超出部分按正常费用结算。
            if (player.HasSkill(SkillIds.JiuChi))
            {
                var freeCount = Math.Min(action.Count, player.FreeWineUsesRemaining);
                var paidCount = action.Count - freeCount;
                return paidCount <= 0 ? 0 : Math.Max(0, GetCardCost(player, action.CostType, action.Target) * paidCount);
            }
        }

        if (action.IsWine && player.Team == BattleTeam.Player)
        {
            var perUnitCost = GetCardCost(player, action.CostType, action.Target);
            if (perUnitCost <= 0) return 0;

            var freeWineCount = Math.Min(
                action.Count,
                GameManager.GetRemainingEquipmentUses(EquipmentIds.WinePouch));
            return Math.Max(0, action.Count - freeWineCount) * new Card(CardType.Wine).Cost;
        }

        var perActionCost = GetCardCost(player, action.CostType, action.Target);
        var cost = perActionCost * action.Count;
        if (CanUseZhuaHuangFreeAttack(player, action.Type) && action.Count > 0)
        {
            cost = Math.Max(0, cost - perActionCost);
        }
        if (FactionFateManager.CanUseShuFirstTrickFree(player, action.Type) && action.Count > 0)
        {
            cost = Math.Max(0, cost - perActionCost);
        }

        return cost;
    }

    /// <summary>
    /// 判断角色能否支付一次已经计算完成的行动费用。
    ///
    /// 0费行动不依赖当前费用是否为负数。【恶臭蘑菇·黄】会把费用设为-1，
    /// 此时角色必须仍能使用【费】等0费牌，才能逐步填平费用亏空。
    /// </summary>
    public static bool CanAffordActionCost(Player player, double cost)
    {
        return cost <= 0 || player.CurrentMana >= cost;
    }

    /// <summary>
    /// Core System 的公开入口：CanUseZhuaHuangFreeAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanUseZhuaHuangFreeAttack(Player player, CardType type)
    {
        var normalCost = GetCardCost(player, type);
        if (normalCost <= 0 || !IsAnyAttackCard(type))
        {
            return false;
        }

        if (type is CardType.Kill or CardType.PoisonKill && player.LianyingFreeKillAvailable)
        {
            return false;
        }

        if (player.Team == BattleTeam.Player
            && (type is CardType.Kill or CardType.PoisonKill)
            && player.WhiteHorseFreeKillAvailable
            && GameManager.HasEquipment(EquipmentIds.WhiteHorse))
        {
            return false;
        }

        if (player.Team == BattleTeam.Player)
        {
            return player.ZhuaHuangFreeAttackAvailable && GameManager.HasEquipment(EquipmentIds.ZhuaHuangFeiDian);
        }

        return player is EnemyInstance enemy
            && enemy.HasEquipment(EquipmentIds.ZhuaHuangFeiDian)
            && (!enemy.RuntimeStates.TryGetValue("zhua_huang_free_attack_used", out var used) || used is not true);
    }

    /// <summary>
    /// 把"借用其它卡牌克制关系"的卡牌归一化为它所复用的目标 CardType。
    /// 目前只有火攻（周瑜专属）复用火杀的判定；未来如果有新卡牌需要复用某张已有
    /// 卡牌的克制关系，只需要在这里追加一行映射，不需要改 BeatsAttack/TryConsumeDodge
    /// 等具体判定逻辑本身。
    /// </summary>
    public static CardType NormalizeCounterProfile(CardType type)
    {
        return type == CardType.FireAttack ? CardType.FireKill : type;
    }

    /// <summary>
    /// Core System 的公开入口：BeatsAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool BeatsAttack(CardType attack, CardType defense)
    {
        // 火攻（周瑜专属）的循环克制关系与火杀完全一致，复用同一套判定而不是另抄
        // 一份表：这里把火攻在进入下方所有比较之前直接归一化成火杀，今后如果修改
        // 火杀的克制规则，火攻会自动同步，不需要额外维护。
        attack = NormalizeCounterProfile(attack);
        defense = NormalizeCounterProfile(defense);

        // 必中杀（DirectSha）克制所有杀系牌，包括火雷杀。
        if (new Card(attack).AttackType == AttackType.DirectSha
            && defense is CardType.Kill or CardType.FireKill or CardType.ThunderKill or CardType.FireThunderKill)
        {
            return true;
        }

        // 三元循环克制（绝对规则，不可运行时修改）：火杀 > 杀 > 雷杀 > 火杀。
        if (attack == CardType.FireKill && defense == CardType.Kill)
        {
            return true;
        }

        if (attack == CardType.Kill && defense == CardType.ThunderKill)
        {
            return true;
        }

        if (attack == CardType.ThunderKill && defense == CardType.FireKill)
        {
            return true;
        }

        // 火雷杀克制普通杀与火杀；与雷杀为互消（平局），不进入此分支。
        if (attack == CardType.FireThunderKill
            && defense is CardType.Kill or CardType.FireKill or CardType.IceKill)
        {
            return true;
        }

        // 冰杀克制普通杀与雷杀；败于火杀、必中杀、火雷杀。
        if (attack == CardType.IceKill && defense is CardType.Kill or CardType.ThunderKill)
        {
            return true;
        }

        if (attack == CardType.FireKill && defense == CardType.IceKill)
        {
            return true;
        }

        // 毒杀等价于普通杀：火杀克制毒杀；毒杀克制雷杀。
        if (attack == CardType.FireKill && defense == CardType.PoisonKill)
        {
            return true;
        }

        if (attack == CardType.PoisonKill && defense == CardType.ThunderKill)
        {
            return true;
        }

        return false;
    }

    // 启动校验：确保三元循环克制关系完整。任何一项失败均直接抛出，阻止游戏运行。
    // 调用方：MainFlow._Ready()。
    /// <summary>
    /// Core System 的公开入口：ValidateCombatRelations。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ValidateCombatRelations()
    {
        // 火杀 > 杀
        if (!BeatsAttack(CardType.FireKill, CardType.Kill))
            throw new System.Exception("[BattleRules] 校验失败：火杀 应克制 杀，但 BeatsAttack(FireKill, Kill) 返回 false。");
        if (BeatsAttack(CardType.Kill, CardType.FireKill))
            throw new System.Exception("[BattleRules] 校验失败：杀 不应克制 火杀，但 BeatsAttack(Kill, FireKill) 返回 true。");

        // 杀 > 雷杀
        if (!BeatsAttack(CardType.Kill, CardType.ThunderKill))
            throw new System.Exception("[BattleRules] 校验失败：杀 应克制 雷杀，但 BeatsAttack(Kill, ThunderKill) 返回 false。");
        if (BeatsAttack(CardType.ThunderKill, CardType.Kill))
            throw new System.Exception("[BattleRules] 校验失败：雷杀 不应克制 杀，但 BeatsAttack(ThunderKill, Kill) 返回 true。");

        // 雷杀 > 火杀
        if (!BeatsAttack(CardType.ThunderKill, CardType.FireKill))
            throw new System.Exception("[BattleRules] 校验失败：雷杀 应克制 火杀，但 BeatsAttack(ThunderKill, FireKill) 返回 false。");
        if (BeatsAttack(CardType.FireKill, CardType.ThunderKill))
            throw new System.Exception("[BattleRules] 校验失败：火杀 不应克制 雷杀，但 BeatsAttack(FireKill, ThunderKill) 返回 true。");
    }

    /// <summary>
    /// Core System 的公开入口：IsShaAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsShaAttack(CardType type)
    {
        return new Card(type).AttackType is AttackType.Sha
            or AttackType.FireSha
            or AttackType.ThunderSha
            or AttackType.FireThunderSha
            or AttackType.DirectSha
            or AttackType.IceSha;
    }

    /// <summary>
    /// Core System 的公开入口：IsCelestialImpactAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsCelestialImpactAttack(CardType type)
    {
        return type == CardType.CelestialImpact;
    }

    // 返回卡牌的基础伤害值。火雷杀基础伤害为 KillDamage×3（30），火杀为15，其余攻击为 KillDamage（10）。
    /// <summary>
    /// Core System 的公开入口：GetCardBaseDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetCardBaseDamage(CardType type)
    {
        return type switch
        {
            CardType.FireThunderKill => BattleConstants.KillDamage * 3,
            CardType.FireKill => BattleConstants.FireKillDamage,
            _ => BattleConstants.KillDamage
        };
    }

    /// <summary>
    /// 火攻（周瑜专属）的基础伤害：5 + 目标当前战斗内有效最大生命值的10%（向下取整，整数除法天然向下取整）。
    /// 读取 target.MaxHealth（战斗内实际生效值，随装备/技能临时提升同步变化），不读取
    /// EnemyDefinition/CharacterData 的原始数值；临时生命（TempHp）不计入 MaxHealth，不参与该计算。
    /// 唯一调用点：DealAttackDamage（真实结算）与 DamagePreviewService（预估），
    /// 确保火攻的伤害公式只在一处维护。
    /// </summary>
    public static int GetFireAttackDamage(Player target)
    {
        return 5 + target.MaxHealth / 10;
    }

    // 所有造成伤害的攻击牌（杀系 + 攻击性锦囊）。
    /// <summary>
    /// Core System 的公开入口：IsAnyAttackCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsAnyAttackCard(CardType type)
    {
        return IsShaAttack(type)
            || type is CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.Tuxi or CardType.CelestialImpact or CardType.FireAttack;
    }

    /// <summary>
    /// 蜀·兵谋同源：杀类型牌与攻击性锦囊牌（万箭齐发/南蛮入侵/突袭）互相视为
    /// 对方的兼容标签，仅用于触发/计数/伤害加成等"查询这两类标签"的效果——不修改 CardType
    /// 原始定义、不影响杀/闪/克制关系/无懈规则/AI/出牌限制。只有这个命运真正生效时才追加
    /// 兼容判定，其余情况与 IsShaAttack 完全等价。
    /// </summary>
    public static bool IsShaAttackWithFactionCompat(CardType type)
    {
        if (IsShaAttack(type)) return true;
        return FactionFateManager.IsShuUnifiedTacticsActive() && IsAttackingTrick(type);
    }

    /// <summary>
    /// 蜀·兵谋同源的另一侧：攻击性锦囊牌判定（万箭齐发/南蛮入侵/突袭，排除杀类型
    /// 本身），命运生效时杀类型牌额外视为满足这个标签。与 <see cref="IsShaAttackWithFactionCompat"/>
    /// 配套使用，其余情况与"IsAnyAttackCard 且非杀"完全等价。
    /// </summary>
    public static bool IsAttackingTrickWithFactionCompat(CardType type)
    {
        if (IsAttackingTrick(type)) return true;
        return FactionFateManager.IsShuUnifiedTacticsActive() && IsShaAttack(type);
    }

    /// <summary>
    /// 攻击性锦囊必须同时具有 Attack 与 Trick 标签；纯攻击牌不会因造成伤害而被误算为锦囊。
    /// </summary>
    public static bool IsAttackingTrick(CardType type)
    {
        var card = new Card(type);
        return card.IsAttack && card.IsTrickCard;
    }

    /// <summary>
    /// 返回本次行动是否应结算桃效果。桃酒同源只作用于玩家，不改变敌方卡牌定义。
    /// </summary>
    public static bool ShouldApplyPeachEffect(Player player, CardType type)
    {
        return type == CardType.Peach
            || (player.Team == BattleTeam.Player
                && FactionFateManager.IsShuPeachWineUnityActive()
                && type == CardType.Wine);
    }

    /// <summary>
    /// 返回本次行动是否应结算酒效果。该查询只追加效果，不修改卡牌原始费用。
    /// </summary>
    public static bool ShouldApplyWineEffect(Player player, CardType type)
    {
        return type == CardType.Wine
            || (player.Team == BattleTeam.Player
                && FactionFateManager.IsShuPeachWineUnityActive()
                && type == CardType.Peach);
    }

    /// <summary>
    /// Core System 的公开入口：GetCardNameKey。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetCardNameKey(CardType type) => type switch
    {
        CardType.Kill => "card.kill.name",
        CardType.FireKill => "card.firekill.name",
        CardType.ThunderKill => "card.thunderkill.name",
        CardType.FireThunderKill => "card.firethunderkill.name",
        CardType.SureKill => "card.surekill.name",
        CardType.IceKill => "card.icekill.name",
        CardType.CelestialImpact => "card.celestialimpact.name",
        CardType.ArrowBarrage => "card.arrowbarrage.name",
        CardType.NanmanInvasion => "card.nanmaninvasion.name",
        CardType.Tuxi => "card.tuxi.name",
        CardType.Dodge => "card.dodge.name",
        CardType.Peach => "card.peach.name",
        CardType.Wine => "card.wine.name",
        CardType.Steal => "card.steal.name",
        CardType.Unassailable => "card.unassailable.name",
        CardType.Fee => "card.fee.name",
        CardType.Guanxing => "card.guanxing.name",
        CardType.JiGuActivate => "card.jiguactivate.name",
        CardType.YingXiActivate => "card.yingxiactivate.name",
        CardType.ShadowKill => "card.shadowkill.name",
        CardType.ShadowLurk => "card.shadowlurk.name",
        CardType.ZiBaoAttack => "card.zibaoattack.name",
        CardType.LightningStrike => "card.lightningstrike.name",
        CardType.Meihuo => "card.meihuo.name",
        CardType.PoisonKill => "card.poisonkill.name",
        CardType.PoJunBoost => "card.pojunboost.name",
        CardType.XueZhaiAttack => "card.xuezhaiattack.name",
        CardType.FireAttack => "card.fireattack.name",
        CardType.IronChain => "card.ironchain.name",
        _ => string.Empty
    };

    /// <summary>
    /// Core System 的公开入口：GetCardName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetCardName(CardType type)
    {
        var key = GetCardNameKey(type);
        return string.IsNullOrEmpty(key) ? string.Empty : Localization.Get(key);
    }

    /// <summary>
    /// Core System 的公开入口：CanUnassailableCounter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanUnassailableCounter(CardType type)
    {
        // 火雷杀含雷属性，可被无懈可击反制（与雷杀相同）。
        // 冰杀同样可被无懈可击反制（冰杀 vs 无懈可击：双方均 x）。
        // 火攻（周瑜专属）虽然循环克制关系复用火杀，但它本质仍是攻击性锦囊牌，
        // 无懈可击必须能正常反制——这一点与"克制关系同火杀"是两件独立的事，
        // 不能因为复用了火杀的判定就丢失锦囊牌该有的被无懈可击反制的资格。
        return type is CardType.Steal
            or CardType.ArrowBarrage
            or CardType.SureKill
            or CardType.ThunderKill
            or CardType.FireThunderKill
            or CardType.NanmanInvasion
            or CardType.Tuxi
            or CardType.CelestialImpact
            or CardType.IceKill
            or CardType.ShadowKill
            or CardType.FireAttack
            or CardType.IronChain
            or CardType.ZiBaoAttack;
    }

    /// <summary>
    /// Core System 的公开入口：CanRespondToArrowBarrage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanRespondToArrowBarrage(CardType type)
    {
        // 火雷杀可响应万箭齐发：反制造成 30 点伤害，自身承受万箭 10 点。
        // 必中杀、影袭杀均可响应万箭齐发：双方各自造成 10 点伤害。
        // 影袭杀只会在影袭状态中出现，因影袭无敌而不会实际承受箭雨伤害。
        // 冰杀可响应万箭齐发：反制造成 10 点伤害，自身承受万箭 10 点，万箭使用者获得冰冻。
        return type is CardType.Dodge
            or CardType.Unassailable
            or CardType.Kill
            or CardType.PoisonKill
            or CardType.FireKill
            or CardType.ThunderKill
            or CardType.FireThunderKill
            or CardType.SureKill
            or CardType.ShadowKill
            or CardType.IceKill;
    }

    /// <summary>
    /// Core System 的公开入口：PayManaAndRaiseResourceChanged。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void PayManaAndRaiseResourceChanged(BattleContext context, Player player, double amount, bool isCostPayment)
    {
        if (amount <= 0)
        {
            return;
        }

        var before = player.CurrentMana;
        player.PayMana(amount);
        var after = player.CurrentMana;
        var actualLost = System.Math.Max(0, before - after);
        if (actualLost <= 0)
        {
            return;
        }

        // 资源变动必须记录实际损失量。费用可包含受保护的窃取费用，或在结算期间被
        // 其它效果改变；使用请求值会使【疑城】等“失去费用”效果错误触发。
        context.ResourceChangeEvent = new ResourceChangeEvent(player, before, after, -actualLost, isCostPayment);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnResourceChanged, context);
        context.ResourceChangeEvent = null;
    }

    /// <summary>
    /// Core System 的公开入口：CanPlayReactionCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanPlayReactionCard(string reactionId, CardType cardType, double currentMana)
    {
        if (reactionId == ReactionIds.Longdan)
        {
            return cardType switch
            {
                CardType.Kill => true,
                CardType.FireKill or CardType.ThunderKill => currentMana >= 1,
                _ => false
            };
        }

        return false;
    }

    /// <summary>
    /// Core System 的公开入口：GetReactionCardCost。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetReactionCardCost(string reactionId, CardType cardType)
    {
        if (reactionId == ReactionIds.Longdan)
        {
            return cardType switch
            {
                CardType.Kill => 0,
                CardType.FireKill or CardType.ThunderKill => 1,
                _ => 0
            };
        }

        return new Card(cardType).Cost;
    }
}

/// <summary>
/// Core System 的公开类：BattleTriggerEffects。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleTriggerEffects
{
    /// <summary>
    /// Core System 的公开入口：CreateDefaultManager。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static TriggerManager CreateDefaultManager()
    {
        var manager = new TriggerManager();
        manager.Register(new HuaXingTransformEffect());
        manager.Register(new HanXueBaoMaEffect());
        manager.Register(new ChangZuiRestoreEffect());
        manager.Register(new MoonGazeEffect());
        // 【洛神】在下一回合开始时按角色当前持有费用执行断费减半；
        // 必须注册在默认管理器中，不能只依赖旧的 SkillEffectRegistry 存根。
        manager.Register(new LuoshenDecayEffect());
        manager.Register(new TurnStartWineEffect());
        manager.Register(new WarDrumActivationEffect());
        manager.Register(new ClampExoskeletonRepairEffect());
        manager.Register(new ShortBowRoundResetEffect());
        manager.Register(new PoisonApplyEffect());
        manager.Register(new BattlePrePhaseEffect());
        manager.Register(new StinkyYellowInitialManaOverrideEffect());
        manager.Register(new EquipmentBattlePrePhaseEffect());
        manager.Register(new RunBuffBattleStartManaEffect());
        manager.Register(new ShuInitiativeManaEffect());
        manager.Register(new ShuEnemyInitialManaEffect());
        manager.Register(new ParalysisDeviceEffect());
        manager.Register(new CurseBladeBattleStartEffect());
        manager.Register(new TimeHourglassEffect());
        manager.Register(new StealthModuleActivateEffect());
        manager.Register(new StealthModuleTurnLimitEffect());
        manager.Register(new StealthModuleCardBreakEffect());
        manager.Register(new LianyingBattlePrePhaseEffect());
        manager.Register(new LianyingEnemyBattlePrePhaseEffect());
        manager.Register(new LianyingResourceChangedEffect());
        manager.Register(new YiChengResourceHealEffect());
        manager.Register(new BattlePhaseResolutionEffect());
        manager.Register(new PangTongIronChainActivateEffect());
        manager.Register(new ChituFirstRoundExtraKillEffect());
        manager.Register(new LongBowBeforeDamageEffect());
        manager.Register(new GuDingDaoRevealEffect());
        manager.Register(new RustGuDingDaoBeforeDamageEffect());
        manager.Register(new MysteriousPotionRevealEffect());
        manager.Register(new DebugInvincibilityEffect());
        manager.Register(new PangTongNirvanaInvincibilityEffect());
        manager.Register(new StunBeforeDamageEffect());
        // 【幻象】此前只存在于未接入默认战斗管理器的 SkillEffectRegistry，导致 Boss
        // 在正式战斗中不会记录或免疫攻击牌。改为全局效果后由持有技能的目标自检。
        manager.Register(new IllusionImmunityEffect());
        manager.Register(new TyrantCrownGuardEffect());
        manager.Register(new DefenseBeforeDamageEffect());
        manager.Register(new ForgottenStoneEffect());
        manager.Register(new DiLuDamageImmunityEffect());
        manager.Register(new XianHaoDodgeEffect());
        manager.Register(new EquipmentBeforeDamageEffect());
        manager.Register(new QingnangPeachEffect());
        manager.Register(new EquipmentDamageBonusEffect());
        manager.Register(new GiantShieldDamageEffect());
        manager.Register(new GuDingDaoDamageBonusEffect());
        manager.Register(new ElementRunBuffDamageBonusEffect());
        manager.Register(new WetDamageEffect());
        manager.Register(new FerocityEffect());
        manager.Register(new WeaknessDamageEffect());
        manager.Register(new YeYanEffect());
        manager.Register(new NanmanMultiplierEffect());
        manager.Register(new ManzuWangDamageEffect());
        manager.Register(new PanXiaoEffect());
        manager.Register(new ChituFirstRoundDoubleKillEffect());
        manager.Register(new LongBowDamageBonusEffect());
        manager.Register(new NightVisionGogglesSingleTargetBonusEffect());
        manager.Register(new NightVisionGogglesGroupFocusEffect());
        manager.Register(new WineDamageBonusEffect());
        manager.Register(new PoJunEnemyDamageEffect());
        manager.Register(new PoJunPlayerCaptureEffect());
        manager.Register(new WushuangDamageEffect());
        manager.Register(new LuoyiVulnerableEffect());
        manager.Register(new WarDrumEffect());
        manager.Register(new KejiUnlimitedDamageBonusEffect());
        manager.Register(new TyrantCrownVulnerableEffect());
        manager.Register(new RunBuffDamageTakenEffect());
        manager.Register(new ShaQiDamageOutputEffect());
        manager.Register(new CurseBladeDamageBonusEffect());
        manager.Register(new StinkyRedOutgoingEffect());
        manager.Register(new StinkyRedIncomingEffect());
        manager.Register(new StinkyGreenEffect());
        manager.Register(new BigBoneClubDamageEffect());
        manager.Register(new RunBuffEnemyDamageBonusEffect());
        manager.Register(new ZhuQueYuShanEffect());
        manager.Register(new HighTemperatureModuleEffect());
        manager.Register(new TengjiaEffect());
        manager.Register(new ScaleArmorEffect());
        manager.Register(new ConductorEffect());
        manager.Register(new JetMaceEffect());
        manager.Register(new JiGuInvincibilityEffect());
        manager.Register(new YingXiInvincibilityEffect());
        manager.Register(new StealthModuleInvincibilityEffect());
        var hookChainEffect = new HookChainEffect();
        manager.Register(hookChainEffect);
        manager.Register(new HookChainResetEffect(hookChainEffect));
        manager.Register(new DanEffect());
        manager.Register(new WanshaHealCancelEffect());
        manager.Register(new IronHeavyArmorEffect());
        manager.Register(new ApplyDamageEffect());
        // 【疑城】在实际扣血后、濒死检查前获得费用，使本次所得费用能参与随后救援。
        manager.Register(new YiChengDamageManaEffect());
        // 铁索连环必须在实际扣血之后、正常的命中后被动（尤其【血债血偿】）之前复制首段伤害。
        manager.Register(new PangTongIronChainShareDamageEffect());
        manager.Register(new ClampExoskeletonTriggerEffect());
        // 魂姿必须在实际扣血完成后、正常濒死检查前执行；否则致死伤害会先进入死亡流程，
        // 或被旧的濒死状态截断，无法按当前最大生命值继续反复触发。
        manager.Register(new HunZiTriggerEffect());
        manager.Register(new DamageTakenEffect());
        manager.Register(new IllusionRecordEffect());
        manager.Register(new IllusionAiModeEffect());
        manager.Register(new XianHaoHitChanceEffect());
        // 图鉴：Lowest 优先级，保证在 ApplyDamageEffect/DamageTakenEffect（含嵌套的
        // OnDying→OnDeath 死亡结算）都跑完之后再读取最终命中/伤害/死亡结果。
        manager.Register(new CodexDamageTrackingEffect());
        manager.Register(new HeavyHammerRecordEffect());
        manager.Register(new VampiricFangHealEffect());
        // 第四章·深渊：侦测者警戒解除 + 巨口连续未造成伤害计数。
        manager.Register(new ScoutVigilanceClearOnDamageEffect());
        manager.Register(new JuKouNoDamageTrackEffect());
        manager.Register(new IceKillFreezeEffect());
        manager.Register(new ShortBowSplashEffect());
        manager.Register(new StatueCoreSplashEffect());
        manager.Register(new PoisonMarkEffect());
        manager.Register(new BigBoneClubStunEffect());
        manager.Register(new JiGuTriggerEffect());
        manager.Register(new JiGuHealOnDealEffect());
        manager.Register(new RuYingSuiXingCostResetEffect());
        manager.Register(new YingXiShadowAttackTrackEffect());
        manager.Register(new ManzuWangHealOnHitEffect());
        manager.Register(new JiAngBattleEffect());
        manager.Register(new RendeShieldAbsorbEffect());
        manager.Register(new RendePlayerShieldAbsorbEffect());
        manager.Register(new WushengEffect());
        manager.Register(new WushengPlayerEffect());
        manager.Register(new YijueEffect());
        manager.Register(new YijuePlayerDamageEffect());
        manager.Register(new PaoxiaoDamageEffect());
        manager.Register(new TaoyuanJiyiEffect());
        manager.Register(new PlayerDamageBorderTriggerEffect());
        manager.Register(new TaoyuanJiyiPlayerEffect());
        manager.Register(new YijueTrackEffect());
        manager.Register(new YijuePlayerTrackEffect());
        manager.Register(new PaoxiaoStackEffect());
        manager.Register(new ZhangBaSheMaoTrackEffect());
        manager.Register(new QingLongYanYueDaoEffect());
        manager.Register(new BaoNueEffect());
        manager.Register(new RuYingSuiXingShieldGainEffect());
        manager.Register(new XueZhaiXueChouRetaliateEffect());
        manager.Register(new XueZhaiNearDeathEffect());
        manager.Register(new ZiBaoEffect());
        manager.Register(new LeiJiEffect());
        manager.Register(new HuangTianEffect());
        manager.Register(new QianXinEffect());
        manager.Register(new ElectricShackleReductionEffect());
        manager.Register(new FrenzyDamageEffect());
        // 初始事件㉑【财富契约】三件装备。
        // 收割者的处决（OnDamage/Lowest）在伤害管线内抬到必死值，必须先于结算；
        // 三个金币效果都在 OnDamageTaken（伤害已实际生效）后统一走 GameManager.AddGold。
        manager.Register(new TycoonArmorGoldOnDamagedEffect());
        manager.Register(new SpeculatorBladeDamageEffect());
        manager.Register(new SpeculatorBladeGoldOnHitEffect());
        manager.Register(new ReaperExecutionEffect());
        manager.Register(new ReaperGoldOnExecuteEffect());
        manager.Register(new AncestorBlessingDyingEffect());
        // 周泰专属【不屈】【奋激】、庞统专属【涅槃】：均需抢在 DyingEffect 之前触发。
        // 【奋激】(Immediate) 必须先于【不屈】(Highest) 执行——它只关心"是否发生了这次
        // OnDying"，与后续是否被【不屈】救回无关；【不屈】负责实际取消本次死亡判定。
        manager.Register(new ZhouTaiFenJiTriggerEffect());
        manager.Register(new ZhouTaiBuQuTriggerEffect());
        manager.Register(new PangTongNirvanaTriggerEffect());
        // 所有濒死技能都以高于 DyingEffect 的优先级处理；仅当它们均未救回目标时，
        // DyingEffect 才会使用白银狮子、桃或酒兜底。
        manager.Register(new DyingEffect());
        manager.Register(new DeathEffect());
        manager.Register(new BattleEndEffect());
        // 载宝驴必须排在死亡判定和其它同阶段结算之后读取最终剩余费用。
        manager.Register(new TreasureDonkeyBattleEndEffect());
        manager.Register(new BiyueSkillEffect());
        manager.Register(new LongdanReactionEffect());
        manager.Register(new EquipmentBattlePostPhaseEffect());
        manager.Register(new BattlePostPhaseEffect());
        manager.Register(new RendeShieldReplenishEffect());
        manager.Register(new RendePlayerShieldReplenishEffect());
        manager.Register(new YijueTurnShiftEffect());
        manager.Register(new YijuePlayerTurnShiftEffect());
        manager.Register(new ZhangBaSheMaoEffect());
        manager.Register(new DongZhuoBengHuaiEffect());
        manager.Register(new BrokenEmotionalComponentEffect());
        manager.Register(new MuNiuLiuMaResourceEffect());
        manager.Register(new HeavyHammerBurstEffect());
        manager.Register(new XianNiangEffect());
        manager.Register(new ChangZuiPreserveEffect());
        // 全局战斗规则【战场崩坏】：第55回合起环境伤害，见 Scripts/Battle/BattlefieldCollapseEffect.cs。
        manager.Register(new BattlefieldCollapseEffect());
        manager.Register(new TurnEndCleanupEffect());
        manager.Register(new PangTongNirvanaInvincibilityTurnEndEffect());
        manager.Register(new JuKouNoDamageTurnEndEffect());
        manager.Register(new JiGuTurnCountdownEffect());
        manager.Register(new YingXiTurnCountdownEffect());
        manager.Register(new WarDrumMaintenanceEffect());
        manager.Register(new YingXiControlImmunityEffect());
        manager.Register(new RuYingSuiXingTurnResetEffect());
        manager.Register(new JiuChiTurnStartEffect());
        manager.Register(new VampiricFangTurnStartEffect());
        manager.Register(new ScoutVigilanceTurnStartEffect());
        manager.Register(new LightningBuffTurnEffect());
        manager.Register(new ElectricShackleEffect());
        manager.Register(new FrenzyDecayEffect());
        manager.Register(new ChangYinEffect());
        manager.Register(new PlagueAddEffect());
        manager.Register(new PlagueHealEffect());
        manager.Register(new MeihuoApplyEffect());
        manager.Register(new MeihuoShieldEffect());
        manager.Register(new MeihuoActivateEffect());
        manager.Register(new SiDiEffect());
        manager.Register(new PlagueStaffPoisonEffect());
        manager.Register(new PaoxiaoPlayerDamageEffect());
        manager.Register(new PaoxiaoPlayerStackEffect());
        manager.Register(new PaoxiaoPlayerBattleEndEffect());
        // 第一章 Boss 专属技能效果
        manager.Register(new BossHealerEnhancedCheckEffect());
        manager.Register(new BossHealerEnhancedDamageEffect());
        manager.Register(new BuDaoInvincibilityEffect());
        manager.Register(new BuDaoTurnStartEffect());
        manager.Register(new BossTraitorComboDamageEffect());
        manager.Register(new BuDaoEffect());
        manager.Register(new BossTraitorComboEffect());
        manager.Register(new BuDaoTurnEndEffect());
        manager.Register(new PangTongIronChainShieldEffect());
        manager.Register(new PangTongIronChainActivateNextTurnEffect());
        // 孙尚香专属传奇被动【武库】：见 Scripts/SkillEffects/WuKuEffect.cs。
        manager.Register(new WuKuMaxHpEffect());
        manager.Register(new WuKuVehicleManaEffect());
        manager.Register(new WuKuAttackDamageBonusEffect());
        manager.Register(new WuKuFeeAccessoryBonusEffect());
        // Hero Unlock System：纯记录型效果，见 Scripts/HeroUnlock/HeroUnlockTrackingEffects.cs。
        manager.Register(new HeroUnlockEnemyKilledTrackingEffect());
        manager.Register(new HeroUnlockKillCardTrackingEffect());
        manager.Register(new HeroUnlockHealingTrackingEffect());
        manager.Register(new HeroUnlockFeeStreakTrackingEffect());
        // 必须最后注册：同为 Lowest 时也应在所有防御监听器之后汇总完全格挡结果。
        // OnBeforeDamage 处理取消型护盾；OnDamage 处理仁德这类数值减免至 0 的护盾。
        manager.Register(new FullBlockRecordingEffect(TriggerTiming.OnBeforeDamage));
        manager.Register(new FullBlockRecordingEffect(TriggerTiming.OnDamage));
        return manager;
    }
}
