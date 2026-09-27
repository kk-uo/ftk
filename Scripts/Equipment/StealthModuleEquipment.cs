//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/StealthModuleEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
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

// 隐身模块（史诗，商店获得）：
//   效果一：战斗开始时获得无敌（StealthModuleActivateEffect，OnGameStart）。
//   效果二：第10回合开始时（StealthModuleTurnLimitEffect，OnTurnStart），或本回合打出
//     费/杀类型牌（杀系）/锦囊牌（StealthModuleCardBreakEffect，OnBattlePhase/High，
//     在 BattlePhaseResolutionEffect 真正结算伤害之前执行）时，立即失去无敌——
//     即打出这类牌的那一回合，本回合受到的伤害就已经不再被无敌免除。
//   无敌本身复用 Player.JiGuActive 同款“伤害归零”模式，单独用
//   Player.StealthModuleActive 字段承载，不与击鼓无敌共用状态，可与其它无敌来源共存。

/// <summary>
/// Equipment System 的公开类：StealthModuleActivateEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StealthModuleActivateEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnGameStart;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver || !GameManager.HasEquipment(EquipmentIds.StealthModule))
        {
            return;
        }

        context.Player.ActivateStealthModule();
        context.RoundResult.AddLine("隐身模块：战斗开始，获得无敌。");
        context.AddTriggerLog("[Equipment/StealthModule]");
        context.AddTriggerLog("隐身模块：无敌已激活。");
    }
}

/// <summary>
/// Equipment System 的公开类：StealthModuleTurnLimitEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StealthModuleTurnLimitEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.StealthModuleActive || context.TurnCounter < 10)
        {
            return;
        }

        context.Player.ClearStealthModule();
        context.RoundResult.AddLine("隐身模块：第10回合开始，无敌失效。");
        context.AddTriggerLog("[Equipment/StealthModule]");
        context.AddTriggerLog("隐身模块：第10回合开始，无敌失效。");
    }
}

/// <summary>
/// Equipment System 的公开类：StealthModuleCardBreakEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StealthModuleCardBreakEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.StealthModuleActive || context.PlayerAction == null)
        {
            return;
        }

        var type = context.PlayerAction.Type;
        if (!BreaksStealth(type))
        {
            return;
        }

        context.Player.ClearStealthModule();
        context.RoundResult.AddLine($"隐身模块：打出{BattleRules.GetCardName(type)}，无敌失效。");
        context.AddTriggerLog("[Equipment/StealthModule]");
        context.AddTriggerLog($"隐身模块：打出{BattleRules.GetCardName(type)}，无敌失效。");
    }

    private static bool BreaksStealth(CardType type)
    {
        return type == CardType.Fee
            || BattleRules.IsShaAttack(type)
            || new Card(type).IsTrickCard;
    }
}

/// <summary>
/// Equipment System 的公开类：StealthModuleInvincibilityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StealthModuleInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields || !damage.Target.StealthModuleActive)
        {
            return;
        }

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"隐身模块：{damage.Target.DisplayName}免疫{BattleRules.GetCardName(damage.AttackType)}伤害。");
        context.AddTriggerLog("[Equipment/StealthModule]");
        context.AddTriggerLog($"隐身模块：{damage.Target.DisplayName}伤害归零。");
    }
}
