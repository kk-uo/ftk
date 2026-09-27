//////////////////////////////////////////////////////////
// 文件：Scripts/RunBuffManager.cs
//
// 模块：Run Buff System
//
// 职责：
// 1. 承载跨战斗 Buff 定义、生命周期与结算相关代码。
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
/// Run Buff System 的公开类：RunBuffManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class RunBuffManager
{
    private static readonly List<RunBuff> ActiveBuffsInternal = new();

    public static IReadOnlyList<RunBuff> ActiveBuffs => ActiveBuffsInternal;

    /// <summary>
    /// Run Buff System 的公开入口：Add。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RunBuff? Add(string buffId, int? remainingBattlesOverride = null)
    {
        return AddStacks(buffId, 1, remainingBattlesOverride);
    }

    /// <summary>
    /// Run Buff System 的公开入口：AddStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RunBuff? AddStacks(string buffId, int stackCount, int? remainingBattlesOverride = null, bool applyEquipmentMultiplier = true)
    {
        if (stackCount <= 0)
        {
            return null;
        }

        var definition = RunBuffDatabase.Get(buffId);
        if (definition == null)
        {
            return null;
        }

        if (buffId == RunBuffIds.Curse
            && applyEquipmentMultiplier
            && GameManager.HasEquipment(EquipmentIds.CurseBlade))
        {
            stackCount *= 5;
        }

        var remainingBattles = remainingBattlesOverride.GetValueOrDefault(definition.DefaultRemainingBattles);
        if (remainingBattles <= 0)
        {
            remainingBattles = definition.DefaultRemainingBattles;
        }

        if (!definition.Stackable)
        {
            foreach (var existing in ActiveBuffsInternal)
            {
                if (existing.BuffId != buffId)
                {
                    continue;
                }

                existing.RemainingBattles = Math.Max(existing.RemainingBattles, remainingBattles);
                EnforceShaQiHealth(buffId);
                return existing;
            }
        }

        RunBuff? lastAdded = null;
        for (var i = 0; i < stackCount; i++)
        {
            var buff = new RunBuff(definition, remainingBattles);
            ActiveBuffsInternal.Add(buff);
            lastAdded = buff;
        }

        MainFlow.TryShowFirstTimeHint(definition.Type == RunBuffType.Curse
            ? FirstTimeHintManager.HintIds.FirstDebuff
            : FirstTimeHintManager.HintIds.FirstBuff);

        // 【煞气缠身】不是诅咒的替换形态：达到阈值后保留全部诅咒层数，
        // 额外获得一个不可叠加的煞气缠身。
        if (buffId == RunBuffIds.Curse
            && CountStacks(RunBuffIds.Curse) >= 44
            && CountStacks(RunBuffIds.ShaQiChenShen) == 0)
        {
            lastAdded = Add(RunBuffIds.ShaQiChenShen);
        }

        EnforceShaQiHealth(buffId);

        return lastAdded;
    }

    private static void EnforceShaQiHealth(string buffId)
    {
        if (buffId == RunBuffIds.ShaQiChenShen)
        {
            // 统一在 Buff 获得入口处理，避免直接奖励与诅咒阈值自动获得走出不同结果。
            GameManager.SetCurrentHp(1);
        }
    }

    /// <summary>
    /// Run Buff System 的公开入口：RemoveAllStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RemoveAllStacks(string buffId)
    {
        ActiveBuffsInternal.RemoveAll(b => b.BuffId == buffId);
    }

    /// <summary>
    /// Run Buff System 的公开入口：RemoveStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int RemoveStacks(string buffId, int stackCount)
    {
        if (stackCount <= 0)
        {
            return 0;
        }

        var removed = 0;
        for (var i = ActiveBuffsInternal.Count - 1; i >= 0 && removed < stackCount; i--)
        {
            if (ActiveBuffsInternal[i].BuffId != buffId)
            {
                continue;
            }

            ActiveBuffsInternal.RemoveAt(i);
            removed += 1;
        }

        return removed;
    }

    /// <summary>
    /// Run Buff System 的公开入口：CountStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int CountStacks(string buffId)
    {
        var count = 0;
        foreach (var buff in ActiveBuffsInternal)
        {
            if (buff.BuffId == buffId)
            {
                count += 1;
            }
        }

        return count;
    }

    /// <summary>
    /// Run Buff System 的公开入口：AmplifyExistingCurseStacksForCurseBlade。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AmplifyExistingCurseStacksForCurseBlade()
    {
        var currentStacks = CountStacks(RunBuffIds.Curse);
        if (currentStacks <= 0)
        {
            return;
        }

        AddStacks(RunBuffIds.Curse, currentStacks * 4, null, false);
    }

    /// <summary>
    /// Run Buff System 的公开入口：ApplyBattleStart。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ApplyBattleStart(EnemyInstance enemy)
    {
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing != RunBuffTriggerTiming.OnBattleStart)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case RunBuffEffectType.EnemyMaxHpPercentBonus:
                        var bonus = (int)Math.Ceiling(enemy.MaxHealth * effect.Value);
                        if (bonus > 0)
                        {
                            enemy.AddMaxHealth(bonus);
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetPlayerDamageTakenMultiplier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetPlayerDamageTakenMultiplier()
    {
        double bonus = 0;
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing != RunBuffTriggerTiming.OnDamageCalculate
                    || effect.Type != RunBuffEffectType.PlayerDamageTakenMultiplierBonus)
                {
                    continue;
                }

                bonus += effect.Value;
            }
        }

        return 1d + bonus;
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetPlayerDamageDealtMultiplier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetPlayerDamageDealtMultiplier()
    {
        double bonus = 0;
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing != RunBuffTriggerTiming.OnDamageCalculate
                    || effect.Type != RunBuffEffectType.PlayerDamageDealtMultiplierBonus)
                    continue;
                bonus += effect.Value;
            }
        }

        return 1d + bonus;
    }

    /// <summary>
    /// Run Buff System 的公开入口：IsHealingBlocked。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsHealingBlocked()
    {
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Type == RunBuffEffectType.BlockPlayerHealing)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetEnemyDamageDealtMultiplier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static double GetEnemyDamageDealtMultiplier()
    {
        double bonus = 0;
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing != RunBuffTriggerTiming.OnDamageCalculate
                    || effect.Type != RunBuffEffectType.EnemyDamageMultiplierBonus)
                    continue;
                bonus += effect.Value;
            }
        }

        return 1d + bonus;
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetPlayerElementDamageFlatBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetPlayerElementDamageFlatBonus(DamageType damageType)
    {
        var bonus = 0d;
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing != RunBuffTriggerTiming.OnDamageCalculate)
                {
                    continue;
                }

                if (damageType.HasFlag(DamageType.Fire) && effect.Type == RunBuffEffectType.PlayerFireDamageFlatBonus)
                {
                    bonus += effect.Value;
                }

                if (damageType.HasFlag(DamageType.Thunder) && effect.Type == RunBuffEffectType.PlayerThunderDamageFlatBonus)
                {
                    bonus += effect.Value;
                }

                if (damageType.HasFlag(DamageType.Ice) && effect.Type == RunBuffEffectType.PlayerIceDamageFlatBonus)
                {
                    bonus += effect.Value;
                }

                if (damageType.HasFlag(DamageType.Poison) && effect.Type == RunBuffEffectType.PlayerPoisonDamageFlatBonus)
                {
                    bonus += effect.Value;
                }
            }
        }

        return (int)Math.Round(bonus);
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetPlayerBattleStartManaBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetPlayerBattleStartManaBonus()
    {
        var bonus = 0;
        foreach (var buff in ActiveBuffsInternal)
        {
            foreach (var effect in buff.Definition.Effects)
            {
                if (effect.Timing == RunBuffTriggerTiming.OnBattleStart
                    && effect.Type == RunBuffEffectType.PlayerStartBattleManaBonus)
                    bonus += (int)effect.Value;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Run Buff System 的公开入口：ApplyChapterStart。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ApplyChapterStart(int chapter)
    {
        for (var i = ActiveBuffsInternal.Count - 1; i >= 0; i--)
        {
            var def = ActiveBuffsInternal[i].Definition;
            if (def.TransformAtChapterStart <= 0 || def.TransformAtChapterStart > chapter)
                continue;
            if (string.IsNullOrEmpty(def.TransformIntoBuff))
                continue;

            ActiveBuffsInternal.RemoveAt(i);
            Add(def.TransformIntoBuff);
            if (def.TransformMaxHpDelta != 0)
                GameManager.AddMaxHp(def.TransformMaxHpDelta);
        }
    }

    /// <summary>
    /// Run Buff System 的公开入口：ConsumeBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConsumeBattle()
    {
        for (var i = ActiveBuffsInternal.Count - 1; i >= 0; i--)
        {
            if (ActiveBuffsInternal[i].RemainingBattles < 0)
            {
                continue;
            }

            ActiveBuffsInternal[i].RemainingBattles -= 1;
            if (ActiveBuffsInternal[i].RemainingBattles <= 0)
            {
                ActiveBuffsInternal.RemoveAt(i);
            }
        }

        // 【诅咒】的描述是“每次战斗结束后-1层”，战败后消耗粮草继续时同样算
        // 一场已结束的战斗。装备【诅咒之刃】后，该自动衰减被完全关闭。
        if (!GameManager.HasEquipment(EquipmentIds.CurseBlade))
        {
            RemoveStacks(RunBuffIds.Curse, 1);
        }
    }

    /// <summary>
    /// Run Buff System 的公开入口：ConsumeBattleVictory。
    ///
    /// 战斗胜利入口。诅咒的战后衰减已统一由 <see cref="ConsumeBattle"/> 处理，
    /// 以确保胜利、战败和特殊战斗走一致的“每次战斗结束”规则。
    /// </summary>
    public static void ConsumeBattleVictory()
    {
        ConsumeBattle();
    }

    /// <summary>
    /// Run Buff System 的公开入口：Reset。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Reset()
    {
        ActiveBuffsInternal.Clear();
    }

    /// <summary>
    /// Run Buff System 的公开入口：ClearAll。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ClearAll()
    {
        ActiveBuffsInternal.Clear();
    }
}
