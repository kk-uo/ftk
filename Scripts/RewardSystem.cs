//////////////////////////////////////////////////////////
// 文件：Scripts/RewardSystem.cs
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

using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Reward System 的公开类：RewardData。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RewardData
{
    public int Gold;
    public List<string> SkillIds = new();
    public List<string> EquipmentIds = new();
    public List<string> EquipmentNames = new();

    public bool HasAnyReward =>
        Gold > 0 || SkillIds.Count > 0 || EquipmentIds.Count > 0;
}

/// <summary>
/// Reward System 的公开枚举：RewardChipType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum RewardChipType
{
    Attack,
    Defense,
    Knowledge,
    Expansion
}

/// <summary>
/// Reward System 的公开枚举：RewardChoiceType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum RewardChoiceType
{
    Chip,
    Skill,
    Equipment,
    Element,
    Inventory,
    Buff,
    Card
}

/// <summary>
/// Reward System 的公开类：RewardExecutionResult。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RewardExecutionResult
{
    public List<string> Messages { get; } = new();
    public List<string> Logs { get; } = new();
    public List<RewardChoiceType> ChoiceRequests { get; } = new();
    public List<string> FailedEquipmentIds { get; } = new();
    public string? JumpEventId { get; set; }
    public string? JumpBattleId { get; set; }

    public string PlayerMessage => string.Join("\n", Messages);
    public bool Succeeded => FailedEquipmentIds.Count == 0;

    /// <summary>
    /// Reward System 的公开入口：Merge。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Merge(RewardExecutionResult other)
    {
        Messages.AddRange(other.Messages);
        Logs.AddRange(other.Logs);
        ChoiceRequests.AddRange(other.ChoiceRequests);
        FailedEquipmentIds.AddRange(other.FailedEquipmentIds);
        JumpEventId ??= other.JumpEventId;
        JumpBattleId ??= other.JumpBattleId;
    }
}

/// <summary>
/// Reward System 的公开类：RewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public abstract class RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public virtual bool CanExecute(out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public abstract RewardExecutionResult Execute();

    /// <summary>
    /// Reward System 的派生类入口：Result。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    protected static RewardExecutionResult Result(string? message, string log)
    {
        var result = new RewardExecutionResult();
        if (!string.IsNullOrWhiteSpace(message))
        {
            result.Messages.Add(message);
        }
        result.Logs.Add(log);
        return result;
    }
}

/// <summary>
/// Reward System 的公开类：RewardSequence。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RewardSequence : IEnumerable<RewardAction>
{
    public List<RewardAction> Actions { get; } = new();
    public bool IsEmpty => Actions.Count == 0;

    /// <summary>
    /// Reward System 的公开入口：Add。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RewardSequence Add(RewardAction action)
    {
        Actions.Add(action);
        return this;
    }

    /// <summary>
    /// Reward System 的公开入口：GetEnumerator。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IEnumerator<RewardAction> GetEnumerator() => Actions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Reward System 的公开类：AddGoldRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddGoldRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddGoldRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddGoldRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddGold(Amount);
        return Result(null, $"+{Amount} Gold");
    }
}

/// <summary>
/// Reward System 的公开类：LoseGoldRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LoseGoldRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LoseGoldRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LoseGoldRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (GameManager.Gold < Amount)
        {
            reason = Localization.Get("ui.insufficient_gold");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddGold(-Amount);
        return Result(null, $"-{Amount} Gold");
    }
}

/// <summary>
/// Reward System 的公开类：LoseGoldUnlessFactionRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// 与 <see cref="LoseGoldRewardAction"/> 相同，但当前角色阵营等于 <see cref="FreeFaction"/> 时
/// 完全免除该笔金币消耗（不判定金币是否足够，也不实际扣除）；其它阵营按原有逻辑正常扣费。
/// 通用、数据驱动：任何"某阵营免费"的场景都可以复用，不针对具体角色/事件写死判断。
/// </summary>
public sealed class LoseGoldUnlessFactionRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LoseGoldUnlessFactionRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LoseGoldUnlessFactionRewardAction(int amount, Faction freeFaction)
    {
        Amount = amount;
        FreeFaction = freeFaction;
    }

    public int Amount { get; }
    public Faction FreeFaction { get; }

    private static bool IsFreeForCurrentCharacter(Faction freeFaction)
    {
        return GameManager.CurrentCharacter?.Faction == freeFaction;
    }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (IsFreeForCurrentCharacter(FreeFaction))
        {
            reason = string.Empty;
            return true;
        }

        if (GameManager.Gold < Amount)
        {
            reason = Localization.Get("ui.insufficient_gold");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        if (IsFreeForCurrentCharacter(FreeFaction))
        {
            return Result(null, "Gold cost waived (faction)");
        }

        GameManager.AddGold(-Amount);
        return Result(null, $"-{Amount} Gold");
    }
}

/// <summary>
/// Reward System 的公开类：HealRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HealRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：HealRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public HealRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var before = GameManager.CurrentHP;
        GameManager.AddCurrentHp(Amount);
        var healed = GameManager.CurrentHP - before;
        return Result(healed > 0 ? $"恢复：{healed} HP" : null, $"+{healed} HP");
    }
}

/// <summary>
/// Reward System 的公开类：LoseCurrentHpRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LoseCurrentHpRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LoseCurrentHpRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LoseCurrentHpRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (GameManager.CurrentHP < Amount)
        {
            reason = "生命不足";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var before = GameManager.CurrentHP;
        GameManager.AddCurrentHp(-Amount);
        return Result(null, $"-{before - GameManager.CurrentHP} HP");
    }
}

/// <summary>
/// Reward System 的公开类：LosePowerRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LosePowerRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LosePowerRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LosePowerRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (GameManager.Power < Amount)
        {
            reason = "电量不足";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddPower(-Amount);
        return Result(null, $"-{Amount} Power");
    }
}

/// <summary>
/// Reward System 的公开类：AddMaxHpRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddMaxHpRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddMaxHpRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddMaxHpRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddMaxHp(Amount);
        return Result(Amount > 0 ? $"最大生命 +{Amount}" : null, $"+{Amount} MaxHP");
    }
}

/// <summary>
/// Reward System 的公开类：LoseMaxHpRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LoseMaxHpRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LoseMaxHpRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LoseMaxHpRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (GameManager.MaxHP <= Amount)
        {
            reason = Localization.Get("ui.insufficient_max_hp");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddMaxHp(-Amount);
        return Result(null, $"-{Amount} MaxHP");
    }
}

/// <summary>按选择瞬间的最大生命值比例永久扣除上限，最低保留 1 点。</summary>
public sealed class LoseMaxHpPercentRewardAction : RewardAction
{
    public LoseMaxHpPercentRewardAction(int percent) => Percent = percent;
    public int Percent { get; }

    public override RewardExecutionResult Execute()
    {
        var before = GameManager.MaxHP;
        var loss = System.Math.Max(1, before * Percent / 100);
        loss = System.Math.Min(loss, System.Math.Max(0, before - 1));
        GameManager.AddMaxHp(-loss);
        return Result($"最大生命 -{loss}", $"LoseMaxHpPercent {Percent}% ({before}->{GameManager.MaxHP})");
    }
}

/// <summary>清空攻击、防御、知识与扩容芯片，不影响装备或其它奖励。</summary>
public sealed class RemoveAllChipsRewardAction : RewardAction
{
    public override RewardExecutionResult Execute()
    {
        var removed = GameManager.ClearAllChips();
        return Result(removed > 0 ? $"失去芯片 ×{removed}" : null, $"RemoveAllChips {removed}");
    }
}

/// <summary>摧毁全部拥有装备（含背包与已装备），不改变金币。</summary>
public sealed class DestroyAllEquipmentRewardAction : RewardAction
{
    public override RewardExecutionResult Execute()
    {
        var destroyed = InventoryManager.DestroyAllEquipment();
        return Result(destroyed.Count > 0 ? $"抛弃装备 ×{destroyed.Count}" : null, $"DestroyAllEquipment {destroyed.Count}");
    }
}

/// <summary>元素祭坛的玩家攻击牌元素词条奖励。</summary>
public sealed class AddPlayerAttackAttributesRewardAction : RewardAction
{
    public AddPlayerAttackAttributesRewardAction(AttackAttribute attributes) => Attributes = attributes;
    public AttackAttribute Attributes { get; }

    public override RewardExecutionResult Execute()
    {
        GameManager.GrantPlayerAttackAttributes(Attributes);
        return Result($"所有攻击牌获得{AttackAttributeRules.GetTraitText(Attributes)}", $"AddPlayerAttackAttributes {Attributes}");
    }
}

/// <summary>向玩家本局出牌栏加入一张非默认行动牌。</summary>
public sealed class AddPlayerCardRewardAction : RewardAction
{
    public AddPlayerCardRewardAction(CardType cardType) => CardType = cardType;
    public CardType CardType { get; }

    public override RewardExecutionResult Execute()
    {
        GameManager.AddPlayerCardType(CardType);
        return Result($"出牌栏加入【{BattleRules.GetCardName(CardType)}】", $"AddPlayerCard {CardType}");
    }
}

/// <summary>元素祭坛：升级月亮宝石，使天体撞击携带四元素。</summary>
public sealed class AwakenMoonGemElementsRewardAction : RewardAction
{
    public override bool CanExecute(out string reason)
    {
        if (!GameManager.OwnsEquipment(EquipmentIds.MoonGem))
        {
            reason = "未拥有月亮宝石";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public override RewardExecutionResult Execute()
    {
        var awakened = GameManager.TryAwakenMoonGemElements();
        return Result(awakened ? "月亮宝石进化：天体撞击获得全部元素词条。" : null, $"AwakenMoonGemElements {awakened}");
    }
}

/// <summary>
/// Reward System 的公开类：AddFoodRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddFoodRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddFoodRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddFoodRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddForage(Amount);
        return Result(null, $"+{Amount} Food");
    }
}

/// <summary>
/// Reward System 的公开类：LoseFoodRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LoseFoodRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LoseFoodRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LoseFoodRewardAction(int amount) => Amount = amount;
    public int Amount { get; }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (GameManager.Forage < Amount)
        {
            reason = Localization.Get("ui.insufficient_forage");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddForage(-Amount);
        return Result(null, $"-{Amount} Food");
    }
}

/// <summary>
/// Reward System 的公开类：AddEquipmentRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddEquipmentRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddEquipmentRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddEquipmentRewardAction(
        string equipmentId,
        EquipmentGainSource source = EquipmentGainSource.GameplayReward)
    {
        EquipmentId = equipmentId;
        Source = source;
    }
    public string EquipmentId { get; }
    public EquipmentGainSource Source { get; }

    /// <summary>
    /// 在执行奖励序列前确认装备定义存在，避免其它奖励已经发放后才发现装备 ID 无效。
    /// </summary>
    public override bool CanExecute(out string reason)
    {
        if (EquipmentDatabase.GetEquipment(EquipmentId) == null)
        {
            reason = $"Unknown equipment reward: {EquipmentId}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var attackChipCount = GameManager.AttackChipCount;
        var defenseChipCount = GameManager.DefenseChipCount;
        var knowledgeChipCount = GameManager.KnowledgeChipCount;
        var expansionChipCount = GameManager.ExpansionChipCount;
        var acquiredSkillCount = GameManager.AcquiredSkills.Count;
        var autoDestroyCount = GameManager.InitialEventAutoDestroyCount;
        var added = GameManager.AddEquipment(EquipmentId, Source);
        var finalDefinition = added?.Definition ?? EquipmentDatabase.GetEquipment(EquipmentId);
        var name = finalDefinition is null ? EquipmentId : Localization.GetName(finalDefinition);
        var delivered = added != null
            || GameManager.AttackChipCount > attackChipCount
            || GameManager.DefenseChipCount > defenseChipCount
            || GameManager.KnowledgeChipCount > knowledgeChipCount
            || GameManager.ExpansionChipCount > expansionChipCount
            || GameManager.AcquiredSkills.Count > acquiredSkillCount
            || GameManager.InitialEventAutoDestroyCount > autoDestroyCount;
        if (delivered)
        {
            return Result($"获得装备：【{name}】", $"AddEquipment {EquipmentId} source={Source} delivered=true");
        }

        var result = Result(null, $"AddEquipment {EquipmentId} source={Source} delivered=false");
        result.FailedEquipmentIds.Add(EquipmentId);
        return result;
    }
}

/// <summary>
/// Reward System 的公开类：RemoveEquipmentRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RemoveEquipmentRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：RemoveEquipmentRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RemoveEquipmentRewardAction(string equipmentId) => EquipmentId = equipmentId;
    public string EquipmentId { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Id != EquipmentId)
            {
                continue;
            }

            InventoryManager.DestroyEquipment(item.InstanceId);
            return Result($"移除装备：【{Localization.GetName(item.Definition)}】", $"RemoveEquipment {EquipmentId}");
        }

        return Result(null, $"RemoveEquipment missing {EquipmentId}");
    }
}

/// <summary>
/// Reward System 的公开类：TransformEquipmentRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TransformEquipmentRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：TransformEquipmentRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public TransformEquipmentRewardAction(string fromEquipmentId, string toEquipmentId)
    {
        FromEquipmentId = fromEquipmentId;
        ToEquipmentId = toEquipmentId;
    }

    public string FromEquipmentId { get; }
    public string ToEquipmentId { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Id != FromEquipmentId)
            {
                continue;
            }

            var toDef = EquipmentDatabase.GetEquipment(ToEquipmentId);
            if (toDef == null)
            {
                return Result(null, $"TransformEquipment target missing {ToEquipmentId}");
            }

            var previousSlot = item.EquippedSlot;
            InventoryManager.DestroyEquipment(item.InstanceId);
            var replacement = GameManager.AddEquipment(ToEquipmentId, EquipmentGainSource.Transformation);
            if (replacement == null)
            {
                // 目标定义已在销毁前确认；若正式获得入口仍拒绝，恢复原装备，避免升级导致装备丢失。
                var restored = GameManager.AddEquipment(FromEquipmentId, EquipmentGainSource.Transformation);
                if (restored != null && previousSlot.HasValue)
                {
                    InventoryManager.EquipToSlot(restored.InstanceId, previousSlot.Value);
                }
                return Result(null, $"TransformEquipment add failed {FromEquipmentId}->{ToEquipmentId}");
            }

            if (previousSlot.HasValue)
            {
                InventoryManager.EquipToSlot(replacement.InstanceId, previousSlot.Value);
            }

            var toName = toDef is null ? ToEquipmentId : Localization.GetName(toDef);
            return Result($"装备转化为：【{toName}】", $"TransformEquipment {FromEquipmentId}->{ToEquipmentId}");
        }

        return Result(null, $"TransformEquipment missing {FromEquipmentId}");
    }
}

/// <summary>
/// Reward System 的公开类：AddSkillRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddSkillRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddSkillRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddSkillRewardAction(string skillId, bool soulPossessionSkill = false)
    {
        SkillId = skillId;
        SoulPossessionSkill = soulPossessionSkill;
    }

    public string SkillId { get; }
    public bool SoulPossessionSkill { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.AddAcquiredSkill(SkillId, SoulPossessionSkill);
        var skillDef = SkillDatabase.GetSkill(SkillId);
        var name = skillDef is null ? SkillId : Localization.GetName(skillDef);
        return Result($"获得技能：【{name}】", $"AddSkill {SkillId}");
    }
}

/// <summary>
/// Reward System 的公开类：RemoveSkillRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RemoveSkillRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：RemoveSkillRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RemoveSkillRewardAction(string skillId) => SkillId = skillId;
    public string SkillId { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        GameManager.RemoveAcquiredSkill(SkillId);
        return Result(null, $"RemoveSkill {SkillId}");
    }
}

/// <summary>
/// Reward System 的公开类：AddBattleBuffRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddBattleBuffRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddBattleBuffRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddBattleBuffRewardAction(string buffId, int stacks = 1)
    {
        BuffId = buffId;
        Stacks = stacks;
    }

    public string BuffId { get; }
    public int Stacks { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
        => Result(null, $"AddBattleBuff pending {BuffId} x{Stacks}");
}

/// <summary>
/// Reward System 的公开类：AddRunBuffRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddRunBuffRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddRunBuffRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddRunBuffRewardAction(string buffId, int stacks = 1, int? remainingBattles = null)
    {
        BuffId = buffId;
        Stacks = stacks;
        RemainingBattles = remainingBattles;
    }

    public string BuffId { get; }
    public int Stacks { get; }
    public int? RemainingBattles { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var buff = RunBuffManager.AddStacks(BuffId, Stacks, RemainingBattles);
        if (BuffId == RunBuffIds.MoonAttention)
        {
            GameManager.InvalidateChapterBossEncounterPlan(1);
        }
        if (BuffId == RunBuffIds.ShaQiChenShen)
        {
            // RunBuffManager 也会处理自动触发（44层诅咒）的同一规则；这里保留
            // 显式奖励入口的保障，且使用“设为1”而非伤害，避免角色免疫扣血绕过它。
            GameManager.SetCurrentHp(1);
        }

        var name = buff is null ? BuffId : Localization.GetName(buff.Definition);
        return Result(
            Localization.GetFmt("reward.run_buff.gain_fmt", name, Stacks),
            $"AddRunBuff {BuffId} x{Stacks}");
    }
}

/// <summary>
/// Reward System 的公开类：RemoveRunBuffRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RemoveRunBuffRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：RemoveRunBuffRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RemoveRunBuffRewardAction(string buffId, int stacks = 0)
    {
        BuffId = buffId;
        Stacks = stacks;
    }

    public string BuffId { get; }
    public int Stacks { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        if (Stacks <= 0)
        {
            RunBuffManager.RemoveAllStacks(BuffId);
        }
        else
        {
            RunBuffManager.RemoveStacks(BuffId, Stacks);
        }

        return Result(null, Stacks <= 0 ? $"RemoveRunBuff {BuffId}" : $"RemoveRunBuff {BuffId} x{Stacks}");
    }
}

/// <summary>
/// Reward System 的公开类：AddChipRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AddChipRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：AddChipRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public AddChipRewardAction(RewardChipType chipType, int count = 1)
    {
        ChipType = chipType;
        Count = count;
    }

    public RewardChipType ChipType { get; }
    public int Count { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        for (var i = 0; i < Count; i++)
        {
            switch (ChipType)
            {
                case RewardChipType.Attack:
                    GameManager.IncrementAttackChipCount();
                    break;
                case RewardChipType.Defense:
                    // HP 加成已经内置在 IncrementDefenseChipCount 里，不需要在这里重复调用 AddMaxHp/AddCurrentHp。
                    GameManager.IncrementDefenseChipCount();
                    break;
                case RewardChipType.Knowledge:
                    GameManager.IncrementKnowledgeChipCount();
                    break;
                case RewardChipType.Expansion:
                    GameManager.IncrementExpansionChipCount();
                    break;
            }
        }

        return Result($"获得{FormatChipName(ChipType)} ×{Count}", $"AddChip {ChipType} x{Count}");
    }

    private static string FormatChipName(RewardChipType type)
        => type switch
        {
            RewardChipType.Attack => "攻击芯片",
            RewardChipType.Defense => "防御芯片",
            RewardChipType.Knowledge => "知识芯片",
            RewardChipType.Expansion => "扩容芯片",
            _ => "芯片"
        };
}

/// <summary>
/// Reward System 的公开类：OpenChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public class OpenChoiceRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenChoiceRewardAction(RewardChoiceType choiceType) => ChoiceType = choiceType;
    public RewardChoiceType ChoiceType { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var result = Result(null, $"OpenChoice {ChoiceType}");
        result.ChoiceRequests.Add(ChoiceType);
        return result;
    }
}

/// <summary>
/// Reward System 的公开类：OpenChipChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OpenChipChoiceRewardAction : OpenChoiceRewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenChipChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenChipChoiceRewardAction() : base(RewardChoiceType.Chip) { }
}

/// <summary>
/// Reward System 的公开类：OpenSkillChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OpenSkillChoiceRewardAction : OpenChoiceRewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenSkillChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenSkillChoiceRewardAction() : base(RewardChoiceType.Skill) { }
}

/// <summary>
/// Reward System 的公开类：OpenEquipmentChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OpenEquipmentChoiceRewardAction : OpenChoiceRewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenEquipmentChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenEquipmentChoiceRewardAction() : base(RewardChoiceType.Equipment) { }
}

/// <summary>
/// Reward System 的公开类：OpenElementChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OpenElementChoiceRewardAction : OpenChoiceRewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenElementChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenElementChoiceRewardAction() : base(RewardChoiceType.Element) { }
}

/// <summary>
/// Reward System 的公开类：OpenInventoryChoiceRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OpenInventoryChoiceRewardAction : OpenChoiceRewardAction
{
    /// <summary>
    /// Reward System 的公开入口：OpenInventoryChoiceRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OpenInventoryChoiceRewardAction() : base(RewardChoiceType.Inventory) { }
}

/// <summary>
/// Reward System 的公开类：JumpEventRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JumpEventRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：JumpEventRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public JumpEventRewardAction(string eventId) => EventId = eventId;
    public string EventId { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var result = Result(null, $"JumpEvent {EventId}");
        result.JumpEventId = EventId;
        return result;
    }
}

/// <summary>
/// Reward System 的公开类：JumpBattleRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JumpBattleRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：JumpBattleRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public JumpBattleRewardAction(string battleId) => BattleId = battleId;
    public string BattleId { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        switch (BattleId)
        {
            case "treasure_pavilion":
                GameManager.BeginTreasurePavilionBattle();
                break;
            case "manzu_camp":
                GameManager.BeginManZuCampBattle();
                break;
            case "qixingtan":
                GameManager.BeginQiXingTanBattle();
                break;
            case "rat_king_event":
                GameManager.BeginRatKingEventBattle();
                break;
        }

        var result = Result(null, $"JumpBattle {BattleId}");
        result.JumpBattleId = BattleId;
        return result;
    }
}

/// <summary>
/// Reward System 的公开类：LogRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LogRewardAction : RewardAction
{
    /// <summary>
    /// Reward System 的公开入口：LogRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LogRewardAction(string message) => Message = message;
    public string Message { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
        => Result(Message, $"Log {Message}");
}

/// <summary>
/// Reward System 的公开类：RandomEquipmentByRarityRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RandomEquipmentByRarityRewardAction : RewardAction
{
    private static readonly System.Random Random = new();

    /// <summary>
    /// Reward System 的公开入口：RandomEquipmentByRarityRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RandomEquipmentByRarityRewardAction(EquipmentRarity rarity, string resultTextTemplate = "")
    {
        Rarity = rarity;
        ResultTextTemplate = resultTextTemplate;
    }

    public EquipmentRarity Rarity { get; }
    public string ResultTextTemplate { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity != Rarity) continue;
            if (!RewardManager.CanAppearInRandomEquipmentReward(definition)) continue;
            candidates.Add(definition);
        }

        if (candidates.Count == 0)
        {
            return Result($"没有可获得的{Rarity}装备。", $"RandomEquipmentByRarity empty {Rarity}");
        }

        var chosen = candidates[Random.Next(candidates.Count)];
        GameManager.AddEquipment(chosen.Id);
        var chosenDisplayName = Localization.GetName(chosen);
        var message = string.IsNullOrWhiteSpace(ResultTextTemplate)
            ? $"获得装备：【{chosenDisplayName}】"
            : ResultTextTemplate.Replace("{name}", chosenDisplayName, System.StringComparison.Ordinal);
        return Result(message, $"RandomEquipmentByRarity {Rarity}: {chosen.Id}");
    }
}

/// <summary>
/// 从统一随机装备池中按装备栏位类别与品质抽取一件装备。
/// 候选规则与重铸/随机装备奖励一致，剧情与角色专属装备不会进入该池。
/// </summary>
public sealed class RandomEquipmentByCategoryAndRarityRewardAction : RewardAction
{
    private static readonly System.Random Random = new();

    public RandomEquipmentByCategoryAndRarityRewardAction(
        EquipmentSlotCategory category,
        EquipmentRarity rarity)
    {
        Category = category;
        Rarity = rarity;
    }

    public EquipmentSlotCategory Category { get; }
    public EquipmentRarity Rarity { get; }

    public override RewardExecutionResult Execute()
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity != Rarity
                || InventoryManager.GetSlotCategory(definition) != Category
                || !RewardManager.CanAppearInRandomEquipmentReward(definition))
            {
                continue;
            }

            candidates.Add(definition);
        }

        if (candidates.Count == 0)
        {
            return Result("没有可获得的装备。", $"RandomEquipmentByCategoryAndRarity empty {Category} {Rarity}");
        }

        var chosen = candidates[Random.Next(candidates.Count)];
        GameManager.AddEquipment(chosen.Id, EquipmentGainSource.EventReward);
        var displayName = Localization.GetName(chosen);
        return Result($"获得装备：【{displayName}】", $"RandomEquipmentByCategoryAndRarity {Category} {Rarity}: {chosen.Id}");
    }
}

/// <summary>
/// Reward System 的公开类：RandomEquipmentFromPoolRewardAction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RandomEquipmentFromPoolRewardAction : RewardAction
{
    private static readonly System.Random Random = new();

    /// <summary>
    /// Reward System 的公开入口：RandomEquipmentFromPoolRewardAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public RandomEquipmentFromPoolRewardAction(params string[] equipmentIds)
    {
        EquipmentIds = equipmentIds;
    }

    public IReadOnlyList<string> EquipmentIds { get; }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override RewardExecutionResult Execute()
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var id in EquipmentIds)
        {
            var def = EquipmentDatabase.GetEquipment(id);
            if (def != null) candidates.Add(def);
        }

        if (candidates.Count == 0)
            return Result("没有可用装备。", "RandomEquipmentFromPool empty");

        var chosen = candidates[Random.Next(candidates.Count)];
        GameManager.AddEquipment(chosen.Id);
        var displayName = Localization.GetName(chosen);
        return Result($"获得装备：【{displayName}】", $"RandomEquipmentFromPool: {chosen.Id}");
    }
}

/// <summary>
/// Reward System 的公开类：RewardManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class RewardManager
{
    private static readonly List<string> RewardLogsInternal = new();

    public static IReadOnlyList<string> RewardLogs => RewardLogsInternal;

    /// <summary>
    /// Reward System 的公开入口：GetRewardForNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RewardData GetRewardForNode(MapNode node)
    {
        var reward = new RewardData();

        if (node.Type is MapNodeType.Battle or MapNodeType.Elite or MapNodeType.Boss)
        {
            reward.Gold = 50;
        }

        return reward;
    }

    /// <summary>
    /// Reward System 的公开入口：ApplyReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RewardExecutionResult ApplyReward(
        RewardData reward,
        EquipmentGainSource equipmentSource = EquipmentGainSource.GameplayReward)
    {
        var sequence = new RewardSequence();
        // 装备是不可替代的实体奖励，先校验并执行它们，避免无效装备 ID 被静默吞掉后仍显示领取成功。
        for (var i = 0; i < reward.EquipmentIds.Count; i++)
        {
            sequence.Add(new AddEquipmentRewardAction(reward.EquipmentIds[i], equipmentSource));
        }

        if (reward.Gold > 0)
        {
            sequence.Add(new AddGoldRewardAction(reward.Gold));
        }

        foreach (var skillId in reward.SkillIds)
        {
            sequence.Add(new AddSkillRewardAction(skillId));
        }

        if (!CanExecute(sequence, out var reason))
        {
            var failed = new RewardExecutionResult();
            failed.Logs.Add($"Reward validation failed: {reason}");
            foreach (var equipmentId in reward.EquipmentIds)
            {
                if (EquipmentDatabase.GetEquipment(equipmentId) == null)
                {
                    failed.FailedEquipmentIds.Add(equipmentId);
                }
            }
            return failed;
        }

        return Execute(sequence);
    }

    /// <summary>
    /// Reward System 的公开入口：CanExecute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanExecute(RewardSequence sequence, out string reason)
    {
        foreach (var action in sequence.Actions)
        {
            if (!action.CanExecute(out reason))
            {
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reward System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RewardExecutionResult Execute(RewardSequence sequence)
    {
        // ======================================================
        // 奖励序列
        // ======================================================
        // 事件、Boss、商店、Debug 都应把奖励拆成 RewardAction 后交给这里执行。
        // RewardManager 只负责按顺序执行动作并收集日志，不关心奖励来自哪里。
        //
        // 设计原因：
        // - 避免事件代码直接改 GameManager / Inventory，减少重复逻辑。
        // - 多段奖励可以复用同一套失败检查与日志输出。
        // - ChoiceReward、JumpEvent 等“需要 UI 后续处理”的奖励也能被统一描述。
        var result = new RewardExecutionResult();
        foreach (var action in sequence.Actions)
        {
            var actionResult = action.Execute();
            result.Merge(actionResult);
            if (!actionResult.Succeeded)
            {
                break;
            }
        }

        RewardLogsInternal.AddRange(result.Logs);
        return result;
    }

    /// <summary>
    /// Reward System 的公开入口：HasChoiceRequest。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasChoiceRequest(RewardSequence sequence, RewardChoiceType choiceType)
    {
        foreach (var action in sequence.Actions)
        {
            if (action is OpenChoiceRewardAction choice && choice.ChoiceType == choiceType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reward System 的公开入口：HasJumpEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasJumpEvent(RewardSequence sequence, out string eventId)
    {
        foreach (var action in sequence.Actions)
        {
            if (action is JumpEventRewardAction jump)
            {
                eventId = jump.EventId;
                return true;
            }
        }

        eventId = string.Empty;
        return false;
    }

    /// <summary>
    /// Reward System 的公开入口：HasJumpBattle。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasJumpBattle(RewardSequence sequence)
    {
        foreach (var action in sequence.Actions)
        {
            if (action is JumpBattleRewardAction)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reward System 的公开入口：CanAppearInRandomEquipmentReward。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanAppearInRandomEquipmentReward(
        EquipmentDefinition definition,
        bool excludeOwned = true)
    {
        // 随机奖励与所有重铸入口共享同一候选规则：除剧情/角色专属装备外，
        // 所有可装备物品、所有品质均可出现。保留已拥有物品去重，以免普通奖励重复给装。
        return InventoryManager.IsEligibleForReforge(definition)
            && (!excludeOwned || !GameManager.OwnsEquipment(definition.Id));
    }

    /// <summary>
    /// 判断装备能否作为“提升品质”重铸的目标。此接口与其它重铸入口共用同一候选规则。
    /// </summary>
    public static bool CanAppearInRandomEquipmentUpgradeReward(
        EquipmentDefinition definition,
        bool excludeOwned = true)
    {
        return InventoryManager.IsEligibleForReforge(definition)
            && (!excludeOwned || !GameManager.OwnsEquipment(definition.Id));
    }

    /// <summary>
    /// 返回指定装备“高一品质”升级时可用的全部合法候选。
    ///
    /// 候选统一经过重铸池过滤：除剧情/角色专属外的所有装备均可参与。
    /// 传奇装备没有更高品质，因此返回空集合。
    /// </summary>
    public static IReadOnlyList<EquipmentDefinition> GetEquipmentUpgradeCandidates(
        EquipmentDefinition original,
        bool allowExclusiveOriginal = false)
    {
        return InventoryManager.GetUpgradeReforgeCandidates(original, allowExclusiveOriginal);
    }

    /// <summary>
    /// 判断装备是否已达到当前章节的最低开放章节。
    ///
    /// 仅解析完整的 chapterN 标签；chapter1_variant 等路线/变体标签不是章节门槛，
    /// 必须交给各自的变体系统处理，不能在这里误判为普通章节装备。
    /// </summary>
    public static bool IsEquipmentAvailableInChapter(EquipmentDefinition definition, int chapter)
    {
        foreach (var tag in definition.Tags)
        {
            const string prefix = "chapter";
            if (!tag.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = tag[prefix.Length..];
            if (int.TryParse(suffix, out var minimumChapter) && chapter < minimumChapter)
            {
                return false;
            }
        }

        return true;
    }
}
