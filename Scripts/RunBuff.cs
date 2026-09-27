//////////////////////////////////////////////////////////
// 文件：Scripts/RunBuff.cs
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

public sealed class RunBuff
{
    /// <summary>
    /// Run Buff System 的公开入口：RunBuff。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RunBuff(RunBuffDefinition definition, int remainingBattles)
    {
        Definition = definition;
        RemainingBattles = remainingBattles;
    }

    public RunBuffDefinition Definition { get; }
    public string BuffId => Definition.BuffId;
    public string BuffName => Localization.GetOrFallback(Definition.NameKey, Definition.BuffName);
    public string Description => Localization.GetOrFallback(Definition.DescriptionKey, Definition.Description);
    public bool Stackable => Definition.Stackable;
    public string Icon => Definition.Icon;
    public int RemainingBattles { get; set; }
}
