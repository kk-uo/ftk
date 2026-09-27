//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyFactory.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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
/// Enemy System 的公开类：EnemyFactory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EnemyFactory
{
    /// <summary>
    /// Enemy System 的公开入口：CreateEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EnemyInstance? CreateEnemy(string enemyId)
    {
        var definition = EnemyDatabase.GetEnemy(enemyId);
        return definition == null ? null : CreateEnemy(definition);
    }

    /// <summary>
    /// Enemy System 的公开入口：CreateEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EnemyInstance CreateEnemy(EnemyDefinition definition)
    {
        var enemy = new EnemyInstance(definition);
        if (GameManager.CurrentRunState == RunState.Battle)
        {
            ApplyBattleStartHpScaling(enemy, definition);
        }

        // 特殊战斗倍率必须仅由“当前特殊战斗处于激活状态”决定，而不能依赖场景切换时
        // CurrentRunState 恰好已更新为 Battle。七星坛从事件界面直接切入战斗，若时序较早，
        // 原来的 RunState 门槛会导致召魂 Boss 以未强化实例出现。
        // EnemyDefinition 始终只读，因此不会污染第一章 Boss 的基础数据。
        GameManager.ApplyActiveSpecialBattleEnemyModifiers(enemy);

        return enemy;
    }

    /// <summary>
    /// 统一的"战斗开始时敌人生命值缩放"入口：局内 Buff 的百分比生命加成（如诅咒之夜）+
    /// 初始事件③代价（精英/Boss最大生命+30%）。
    ///
    /// 提取成独立方法而不是只在 <see cref="CreateEnemy"/> 内联，是因为左·慈的【化形】
    /// （<see cref="HuaXingTransformEffect"/>，OnGameStart）会在敌人已经创建之后，把
    /// MaxHealth 从基础值 1 直接覆盖成玩家当前生命值——如果不在覆盖之后重新调用一次这里，
    /// 覆盖之前基于"MaxHealth=1"算出来的百分比加成（几乎为0）就会被覆盖操作直接冲掉，
    /// 导致左·慈是全场唯一不吃"诅咒之夜""精英/Boss+30%生命"等全局生命缩放规则的Boss。
    /// </summary>
    public static void ApplyBattleStartHpScaling(EnemyInstance enemy, EnemyDefinition definition)
    {
        RunBuffManager.ApplyBattleStart(enemy);
        // 初始事件③代价：所有传奇敌人最大生命+30%
        if (GameManager.InitialEventLegendaryEnemyHpBonus
            && definition.RewardTier == RewardTier.Legendary)
        {
            var bonus = (int)Math.Ceiling(enemy.MaxHealth * 0.30);
            if (bonus > 0) enemy.AddMaxHealth(bonus);
        }
    }
}
