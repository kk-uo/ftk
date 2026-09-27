//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyReward.cs
//
// 模块：Reward System
//
// 职责：
// 1. 承载奖励动作、奖励序列与奖励执行相关代码。
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

// 概率性装备掉落：击败该敌人时按 Probability 概率（0~1）随机获得 EquipmentId 对应的装备。
/// <summary>
/// Reward System 的公开类：EnemyEquipmentDrop。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyEquipmentDrop
{
    public string EquipmentId = string.Empty;
    public float Probability = 1.0f;
}

/// <summary>
/// Reward System 的公开类：EnemyReward。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyReward
{
    // 是否参与统一金币掉落结算（按 EnemyType 从 EnemyRewardConfig 读取区间随机）。
    // 默认 true；仅用于"共享血条"类多体敌人（例如蜀汉共生体的刘备/关羽）避免同一场
    // 战斗被算作多份金币——这类敌人里只留一个成员为 true，其余设为 false。
    public bool GrantsGold = true;
    // 非空时使用固定金币奖励，适用于有明确、非随机金币奖励的特殊 Boss。
    public int? GoldOverride;
    public List<string> SkillRewards = new();
    public List<string> EquipmentRewards = new();
    public List<EnemyEquipmentDrop> EquipmentDrops = new();
    // 概率掉落知识芯片（0 = 不掉落）
    public float KnowledgeChipDropProbability;
    // 必定掉落防护芯片数量（第一章 Boss 固定掉落）
    public int DefenseChipDropCount;
    // 随机掉落池：从列表中随机选择恰好1件装备必定掉落（列表为空时不生效）
    public List<string> RandomEquipmentPool = new();
}
