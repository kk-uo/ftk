//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/HuaXingEffect.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载技能效果实现与 Trigger 接入相关代码。
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

// 化形（左·慈专属·传奇）：战斗开始时触发，复制玩家当前血量/技能/装备。
// 保留【化形】和【仙毫】，排除芯片加成装备，复制仅发生一次。
/// <summary>
/// Skill Effect System 的公开类：HuaXingEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HuaXingEffect : ISkillEffect
{
    public string SkillId => SkillIds.HuaXing;

    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}

/// <summary>
/// Skill Effect System 的公开类：HuaXingTransformEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HuaXingTransformEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnGameStart;
    public EffectPriority Priority => EffectPriority.High;

    private static bool IsChip(string equipmentId)
    {
        return equipmentId is EquipmentIds.AttackChip
            or EquipmentIds.ExpansionChip
            or EquipmentIds.MysteriousChip;
    }

    private static void InitializeRuntimeState(EnemyInstance enemy, string equipmentId)
    {
        switch (equipmentId)
        {
            case EquipmentIds.IronHeavyArmor:
                if (!enemy.RuntimeStates.ContainsKey("iron_heavy_armor_active"))
                    enemy.RuntimeStates["iron_heavy_armor_active"] = true;
                break;
            case EquipmentIds.JetMace:
                if (!enemy.RuntimeStates.ContainsKey("jet_mace_active"))
                    enemy.RuntimeStates["jet_mace_active"] = true;
                break;
            case EquipmentIds.ClampExoskeleton:
                if (!enemy.RuntimeStates.ContainsKey("clamp_exo_active"))
                {
                    enemy.RuntimeStates["clamp_exo_active"] = true;
                    enemy.RuntimeStates["clamp_exo_repair_pending"] = false;
                }
                break;
        }
    }

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.Encounter == null || context.GameOver) return;

        EnemyInstance? zuoci = null;
        foreach (var enemy in context.Encounter.Enemies)
        {
            if (!enemy.IsDead && enemy.HasSkill(SkillIds.HuaXing))
            {
                zuoci = enemy;
                break;
            }
        }
        if (zuoci == null) return;

        var playerHp = context.Player.Health;

        // 清除自身技能（保留化形）
        var skillsToRemove = new List<string>();
        foreach (var skill in zuoci.Skills)
        {
            if (skill.Id != SkillIds.HuaXing)
                skillsToRemove.Add(skill.Id);
        }
        foreach (var skillId in skillsToRemove)
            zuoci.RemoveSkill(skillId);

        // 清除自身装备（保留仙毫）
        zuoci.Equipments.RemoveAll(e => e.Id != EquipmentIds.XianHao);

        // 设置生命值为玩家当前血量
        zuoci.DebugSetMaxHealth(playerHp);
        zuoci.DebugSetHealth(playerHp);

        // 重新套用一次"战斗开始生命缩放"（诅咒之夜等局内Buff的百分比生命加成 + 初始事件③
        // 精英/Boss+30%生命）——这两项在 EnemyFactory.CreateEnemy 里已经算过一次，但当时
        // zuoci 的 MaxHealth 还是基础值1，算出来的加成接近0，而且会被上面这次覆盖直接冲掉。
        // 不重新套用的话，左·慈会是全场唯一不受这些全局生命缩放规则影响的Boss。
        // ApplyBattleStartHpScaling 内部统一走 AddMaxHealth，会按同样的量同步提升当前生命值
        // （此时 Health 已经等于 MaxHealth，提升后两者会继续保持相等），不需要额外再设置一次。
        EnemyFactory.ApplyBattleStartHpScaling(zuoci, zuoci.Definition);

        // 复制玩家技能
        var copiedSkillNames = new List<string>();
        foreach (var skill in context.Player.Skills)
        {
            if (!zuoci.HasSkill(skill.Id))
            {
                zuoci.AddSkill(skill);
                copiedSkillNames.Add(Localization.GetName(skill));
            }
        }

        // 复制玩家装备（排除芯片）
        var copiedEquipNames = new List<string>();
        foreach (var equipDef in InventoryManager.GetEquippedDefinitions())
        {
            if (IsChip(equipDef.Id)) continue;
            zuoci.Equipments.Add(equipDef);
            copiedEquipNames.Add(Localization.GetName(equipDef));
            InitializeRuntimeState(zuoci, equipDef.Id);
        }

        // 战斗日志
        var skillText = copiedSkillNames.Count > 0 ? string.Join("、", copiedSkillNames) : "（无）";
        var equipText = copiedEquipNames.Count > 0 ? string.Join("、", copiedEquipNames) : "（无）";
        context.RoundResult.AddLine($"左·慈发动【化形】：生命值变为 {playerHp}/{playerHp}。");
        context.RoundResult.AddLine($"化形复制技能：{skillText}。");
        context.RoundResult.AddLine($"化形复制装备：{equipText}。");
        context.AddTriggerLog($"[化形] HP={playerHp}，技能=[{skillText}]，装备=[{equipText}]");
    }
}
