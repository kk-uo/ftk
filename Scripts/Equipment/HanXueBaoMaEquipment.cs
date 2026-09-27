//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/HanXueBaoMaEquipment.cs
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

// 汗血宝马（稀有·载具）：战斗开始时获得20点临时生命值。
//
// 复用说明：GameManager.TempHp/AddTempHp 只在 BattleManager.StartBattle 里
// _player.ResetForNewBattle(...) 时被读取一次（合并进开战时的初始生命值），
// 而这一步发生在 OnGameStart 触发链之前——如果在 OnGameStart 效果里调用
// GameManager.AddTempHp，本场战斗根本不会生效（只会残留到下一场战斗开局，
// 且届时会被 SaveBattleState 清零，实际上等于完全无效）。
// 本项目里"生命值可以暂时超过上限、优先被伤害消耗、战斗结束后清零"这套语义
// 真正落地的机制是 Player.Heal(amount, allowOverheal: true)（青囊桃的超治疗
// 就是同一个入口），且 GameManager.TempHp 概念本身也是通过这个不做上限裁剪的
// 加法实现的。因此这里直接调用 Heal(20, allowOverheal:true)，复用的是同一套
// "生命值可超过上限"的底层机制，而不是另起一套新的生命系统。
/// <summary>
/// Equipment System 的公开类：HanXueBaoMaEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HanXueBaoMaEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnGameStart;
    public EffectPriority Priority => EffectPriority.Mid;

    private const int TempHpAmount = 20;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver)
        {
            return;
        }

        if (GameManager.HasEquipment(EquipmentIds.HanXueBaoMa) && !context.Player.IsDead)
        {
            GrantTempHp(context, context.Player);
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.HanXueBaoMa))
            {
                continue;
            }

            GrantTempHp(context, enemy);
        }
    }

    private static void GrantTempHp(BattleContext context, Player unit)
    {
        BattleHealing.Apply(
            context,
            unit,
            TempHpAmount,
            allowOverheal: true,
            new HealthChangeSource(HealthChangeSourceKind.Equipment, "汗血宝马", EquipmentIds.HanXueBaoMa, unit));
        context.RoundResult.AddLine($"汗血宝马：{unit.DisplayName}获得{TempHpAmount}点临时生命值。");
        context.AddTriggerLog("[Equipment/汗血宝马]");
        context.AddTriggerLog($"{unit.DisplayName}获得{TempHpAmount}点临时生命值（当前 {unit.Health}/{unit.MaxHealth}）。");
    }
}
