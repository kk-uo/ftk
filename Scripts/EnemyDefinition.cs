//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyDefinition.cs
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

using System.Collections.Generic;

/// <summary>
/// Enemy System 的公开类：EnemyDefinition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyDefinition : ILocalizedDefinition
{
    public string Id { get; set; } = string.Empty;
    private string _name = string.Empty;
    public string Name
    {
        get => Localization.GetOrFallback(NameKey, _name);
        set => _name = value;
    }
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey => string.Empty;
    public int MaxHP;
    public List<string> SkillIds = new();
    public EnemyType Type;
    public EnemyAiProfile AiProfile = new();
    public StageRange StageRange = new();
    public int StartingResource;
    public EnemyDeck StartingDeck = new();
    public EnemyActionWeights ActionWeights = new();
    public List<AiRule> AiRules = new();
    public EnemyReward Reward = new();
    public RewardTier RewardTier;
    // 敌人携带的装备 Id 列表；实例化时自动解析为 EquipmentDefinition 并生效于战斗结算。
    public List<string> EquipmentIds = new();
    public List<IEnemyCondition> SpawnConditions = new();
    public List<EnemyTag> Tags = new();
    public string LoreId = string.Empty;
    public bool UseSharedHealthPool;
}
