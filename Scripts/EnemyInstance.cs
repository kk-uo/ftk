//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyInstance.cs
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
using System;

/// <summary>
/// Enemy System 的公开类：EnemyInstance。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyInstance : Player
{
    private readonly string _battleStateKey = $"enemy-{Guid.NewGuid():N}";

    /// <summary>
    /// Enemy System 的公开入口：EnemyInstance。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyInstance(EnemyDefinition definition)
        : base(definition.Name, definition.Id, BattleTeam.Enemy)
    {
        Definition = definition;
        ResetForNewBattle(definition.MaxHP, definition.MaxHP, definition.StartingResource);
        RuntimeSkillIds = new List<string>(definition.SkillIds);
        RuntimeDeck = new List<CardType>(definition.StartingDeck.Cards);
        foreach (var skillId in RuntimeSkillIds)
        {
            var skill = SkillDatabase.GetSkill(skillId);
            if (skill != null)
            {
                AddSkill(skill);
            }
        }

        // 解析并记录敌人携带的装备（与玩家共用 EquipmentDefinition，效果在 BattleRules 中统一结算）。
        foreach (var equipmentId in definition.EquipmentIds)
        {
            var equipmentDef = EquipmentDatabase.GetEquipment(equipmentId);
            if (equipmentDef != null)
            {
                Equipments.Add(equipmentDef);
            }
        }

        // 应用装备的最大生命加成（藤甲/鳞甲/白银狮子等；敌人在定义中使用基础HP，加成在此实例化时叠加）。
        foreach (var equip in Equipments)
        {
            var hpBonus = GetEquipmentMaxHpBonus(equip.Id);
            if (hpBonus > 0)
            {
                AddMaxHealth(hpBonus);
            }
        }

        // 初始化每场战斗的装备触发标记。
        if (HasEquipment(EquipmentIds.IronHeavyArmor))
        {
            RuntimeStates["iron_heavy_armor_active"] = true;
        }

        if (HasEquipment(EquipmentIds.JetMace))
        {
            RuntimeStates["jet_mace_active"] = true;
        }

        if (HasEquipment(EquipmentIds.ClampExoskeleton))
        {
            RuntimeStates["clamp_exo_active"] = true;
            RuntimeStates["clamp_exo_repair_pending"] = false;
        }

        // 侦测者：战斗开始默认进入警戒状态（前3回合只出费），见 EnemyAI.cs 的
        // SelectScoutAction/GetAvailableActions 硬限定，以及 Scripts/Enemy/ScoutBehavior.cs
        // 的自动解除逻辑（任意实际伤害立即解除；第4回合起自动解除）。
        if (Definition.Id == "scout")
        {
            RuntimeStates["scout_vigilant"] = true;
        }
    }

    private static int GetEquipmentMaxHpBonus(string equipmentId)
    {
        return equipmentId switch
        {
            EquipmentIds.Tengjia => 10,
            EquipmentIds.RustShield => 10,
            EquipmentIds.GangDun => 30,
            EquipmentIds.GiantShield => 45,
            EquipmentIds.SilverLion => 20,
            EquipmentIds.ScaleArmor => 10,
            EquipmentIds.IronHeavyArmor => 20,
            EquipmentIds.ClampExoskeleton => 50,
            EquipmentIds.Conductor => 10,
            EquipmentIds.TycoonArmor => 10,
            _ => 0
        };
    }

    public EnemyDefinition Definition { get; }
    public string BattleStateKey => _battleStateKey;
    public SharedHealthPool? SharedPool { get; private set; }

    public int CurrentHpValue
    {
        get => Health;
        set => SetHealthDirectly(value);
    }
    public int CurrentResource
    {
        get => (int)System.Math.Floor(CurrentMana);
        set => SetManaDirectly(value);
    }
    public List<string> RuntimeSkillIds { get; }
    public List<CardType> RuntimeDeck { get; }
    // 该敌人携带的装备列表（战斗内生效，不进入玩家背包系统）。
    public List<EquipmentDefinition> Equipments { get; } = new();

    public bool IsElite => Definition.Type == EnemyType.Elite;
    public bool IsBoss => Definition.Type == EnemyType.Boss;
    public override double Resource => CurrentMana;
    public override int CurrentHP => SharedPool?.CurrentHP ?? Health;
    public override int MaxHP => SharedPool?.MaxHP ?? MaxHealth;

    /// <summary>
    /// Enemy System 的公开入口：SetSharedPool。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetSharedPool(SharedHealthPool pool)
    {
        SharedPool = pool;
        pool.AddMember(this);
        SyncFromSharedPool();
    }

    /// <summary>
    /// Enemy System 的公开入口：SyncFromSharedPool。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SyncFromSharedPool()
    {
        if (SharedPool != null)
        {
            SetHealthDirectly(SharedPool.CurrentHP);
        }
    }

    /// <summary>
    /// Enemy System 的公开入口：TakeDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void TakeDamage(int amount)
    {
        // 共享生命池敌人不会进入 Player.TakeDamage；影袭无敌需在此保持同样的最终兜底。
        if (amount > 0 && InShadowState)
        {
            return;
        }

        if (SharedPool != null)
        {
            SharedPool.TakeDamage(amount);
        }
        else
        {
            base.TakeDamage(amount);
        }
    }

    /// <summary>
    /// 使敌方单位直接失去生命；共享生命池单位仍需同步扣除同一生命池。
    /// </summary>
    public override void LoseHealth(int amount)
    {
        if (SharedPool != null)
        {
            SharedPool.TakeDamage(amount);
        }
        else
        {
            base.LoseHealth(amount);
        }
    }

    /// <summary>
    /// Enemy System 的公开入口：Heal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override int Heal(int amount, bool allowOverheal = false)
    {
        if (SharedPool != null)
        {
            return SharedPool.Heal(amount);
        }
        return base.Heal(amount, allowOverheal);
    }

    /// <summary>
    /// Enemy System 的公开入口：HasEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool HasEquipment(string equipmentId)
    {
        foreach (var equip in Equipments)
        {
            if (equip.Id == equipmentId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Enemy System 的公开入口：CountEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int CountEquipment(string equipmentId)
    {
        var count = 0;
        foreach (var equip in Equipments)
        {
            if (equip.Id == equipmentId)
            {
                count++;
            }
        }
        return count;
    }
}
