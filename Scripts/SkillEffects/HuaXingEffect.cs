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

// 化形（左·慈专属·传奇）：战斗开始时以敌方角色基础生命+防御芯片为新基础，
// 获得敌方技能/装备，同时保留自身既有的技能、装备与芯片类加成。
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
                    enemy.RuntimeStates["clamp_exo_invincible_this_round"] = false;
                }
                break;
        }
    }

    private static int GetEquipmentMaxHpBonus(string equipmentId)
    {
        return equipmentId switch
        {
            EquipmentIds.Tengjia or EquipmentIds.RustShield or EquipmentIds.ScaleArmor => 10,
            EquipmentIds.GangDun => 30,
            EquipmentIds.GiantShield => 45,
            EquipmentIds.SilverLion => 20,
            EquipmentIds.IronHeavyArmor => 20,
            EquipmentIds.ClampExoskeleton => 80,
            EquipmentIds.Conductor or EquipmentIds.TycoonArmor => 10,
            _ => 0
        };
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

        // 仅以角色原始生命和防御芯片计算基础值：不读取玩家战斗中的当前生命、
        // 临时生命、装备、技能或其它事件加成。这样【化形】不会把一时的治疗/扣血
        // 或外部最大生命加成错误当作角色本体。
        var playerBaseHealth = (GameManager.CurrentCharacter?.MaxHp ?? context.Player.MaxHealth)
            + GameManager.DefenseChipCount * 10 * FactionFateManager.GetChipEffectMultiplier();

        // 保留左慈原有技能，并叠加敌方技能；同 ID 不重复添加。
        var copiedSkillNames = new List<string>();
        foreach (var skill in context.Player.Skills)
        {
            if (!zuoci.HasSkill(skill.Id))
            {
                zuoci.AddSkill(skill);
                copiedSkillNames.Add(Localization.GetName(skill));
            }
        }

        // 保留左慈原有装备，并获得敌方已装备的装备。芯片不是背包装备，已在基础
        // 生命值中处理防御芯片；攻击/知识芯片不会被伪装成可装备物品。
        var copiedEquipNames = new List<string>();
        foreach (var equipDef in InventoryManager.GetEquippedDefinitions())
        {
            if (!zuoci.HasEquipment(equipDef.Id))
            {
                zuoci.Equipments.Add(equipDef);
                copiedEquipNames.Add(Localization.GetName(equipDef));
                InitializeRuntimeState(zuoci, equipDef.Id);
            }
        }

        // 先设置“化形基础生命”，再按保留/复制后的装备统一补上生命修正。
        // 这保证“基础值不包括装备”与“获得装备后装备仍有效”同时成立。
        var equipmentHealth = 0;
        foreach (var equipment in zuoci.Equipments)
        {
            equipmentHealth += GetEquipmentMaxHpBonus(equipment.Id);
        }
        var difficultyBossHealth = DifficultyManager.GetBossHealthBonus(GameManager.CurrentChapter);
        zuoci.DebugSetMaxHealth(playerBaseHealth + equipmentHealth + difficultyBossHealth);
        zuoci.DebugSetHealth(zuoci.MaxHealth);

        // 战斗日志
        var skillText = copiedSkillNames.Count > 0 ? string.Join("、", copiedSkillNames) : "（无）";
        var equipText = copiedEquipNames.Count > 0 ? string.Join("、", copiedEquipNames) : "（无）";
        context.RoundResult.AddLine($"左·慈发动【化形】：基础生命变为 {playerBaseHealth}（含防御芯片，不含装备/技能加成），当前生命 {zuoci.Health}/{zuoci.MaxHealth}。");
        context.RoundResult.AddLine($"化形复制技能：{skillText}。");
        context.RoundResult.AddLine($"化形复制装备：{equipText}。");
        context.AddTriggerLog($"[化形] BaseHP={playerBaseHealth}，技能=[{skillText}]，装备=[{equipText}]");
    }
}
