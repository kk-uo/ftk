//////////////////////////////////////////////////////////
// 文件：Scripts/DamageModifier.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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
using System.Linq;

/// <summary>
/// Core System 的公开枚举：DamageModifierPriority。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum DamageModifierPriority
{
    BaseDamage = 100,
    FlatBonus = 200,
    WineMultiplier = 300,
    SpecialMultiplier = 400,
    VulnerableOrReduction = 500,
    // 装备最终乘区（藤甲 ×0.5 / ×2），在克己/无懈等易伤区之后，无双之前结算。
    ArmorEquipmentMultiplier = 600,
    Wushuang = 1000,
    // 裸衣：祢衡攻击伤害×2，与无双同区但不共存（角色专属，不叠加）。
    Luoyi = 1050,
    // 最终伤害倍率：在常规技能、装备与易伤乘区之后结算，但仍受最终伤害上限约束。
    FinalMultiplier = 8000,
    // 鳞甲伤害上限，在所有乘法（含无双）完成后最后封顶。
    DamageCap = 9000
}

/// <summary>
/// Core System 的公开枚举：DamageModifierOperation。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum DamageModifierOperation
{
    Add,
    Multiply,
    // 整数向下取整除法，用于 ×0.5 等无法用整数乘法表达的比例削减。
    Divide,
    // 将最终伤害限制在不超过 Value 的上限。
    Cap,
    // 将最终伤害提高到至少 Value。用于处决等“确保致死”规则，避免以虚假的超额伤害达成斩杀。
    Minimum,
    // 浮点倍率（向下取整），用于非整数倍率（如裸衣×1.5）。使用 FloatValue 字段存储实际倍率。
    MultiplyFloat,
    // 平坦减伤（在所有乘法之后执行，最终伤害减少 Value，最低为 0）。用于仁德护盾等固定减伤效果。
    FlatReduction
}

/// <summary>
/// Core System 的公开类：DamageModifier。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DamageModifier
{
    /// <summary>
    /// Core System 的公开入口：DamageModifier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public DamageModifier(string name, DamageModifierPriority priority, DamageModifierOperation operation, int value)
    {
        Name = name;
        Priority = priority;
        Operation = operation;
        Value = value;
        FloatValue = value;
    }

    /// <summary>
    /// Core System 的公开入口：DamageModifier。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public DamageModifier(string name, DamageModifierPriority priority, DamageModifierOperation operation, double floatValue)
    {
        Name = name;
        Priority = priority;
        Operation = operation;
        Value = (int)System.Math.Floor(floatValue);
        FloatValue = floatValue;
    }

    public string Name { get; }
    public DamageModifierPriority Priority { get; }
    public DamageModifierOperation Operation { get; }
    public int Value { get; }
    public double FloatValue { get; }
}

/// <summary>
/// Core System 的公开类：DamageModifierPipeline。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DamageModifierPipeline
{
    private readonly List<DamageModifier> _modifiers = new();

    public IReadOnlyList<DamageModifier> Modifiers => _modifiers;

    public int FlatBonusTotal { get; private set; }
    public double WineMultiplier { get; private set; } = 1;
    public int SpecialMultiplier { get; private set; } = 1;
    public int VulnerableMultiplier { get; private set; } = 1;
    public int WushuangMultiplier { get; private set; } = 1;

    /// <summary>
    /// Core System 的公开入口：Add。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Add(DamageModifier modifier)
    {
        _modifiers.Add(modifier);
    }

    /// <summary>
    /// Core System 的公开入口：Resolve。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int Resolve(int baseDamage)
    {
        FlatBonusTotal = 0;
        WineMultiplier = 1;
        SpecialMultiplier = 1;
        VulnerableMultiplier = 1;
        WushuangMultiplier = 1;

        // 伤害统一遵循“先加算、后乘算”：不论固定增伤来自芯片、装备还是技能，
        // 都先汇总到基础伤害，再进入按优先级排列的倍率、减伤和封顶区。
        // 这样不会因为某个调用方误用了较晚的 Priority 而把固定值加在倍率之后。
        var total = baseDamage;
        foreach (var modifier in _modifiers
                     .Where(modifier => modifier.Operation == DamageModifierOperation.Add)
                     .OrderBy(modifier => (int)modifier.Priority))
        {
            total += modifier.Value;
            if (modifier.Priority == DamageModifierPriority.FlatBonus)
            {
                FlatBonusTotal += modifier.Value;
            }
        }

        foreach (var modifier in _modifiers
                     .Where(modifier => modifier.Operation != DamageModifierOperation.Add)
                     .OrderBy(modifier => (int)modifier.Priority))
        {
            switch (modifier.Operation)
            {
                case DamageModifierOperation.Multiply:
                    total *= modifier.Value;
                    switch (modifier.Priority)
                    {
                        case DamageModifierPriority.WineMultiplier:
                            WineMultiplier *= modifier.Value;
                            break;
                        case DamageModifierPriority.SpecialMultiplier:
                            SpecialMultiplier *= modifier.Value;
                            break;
                        case DamageModifierPriority.VulnerableOrReduction:
                            VulnerableMultiplier *= modifier.Value;
                            break;
                        case DamageModifierPriority.Wushuang:
                        case DamageModifierPriority.Luoyi:
                            WushuangMultiplier *= modifier.Value;
                            break;
                    }
                    break;

                case DamageModifierOperation.Divide:
                    if (modifier.Value > 0)
                    {
                        total /= modifier.Value;
                    }
                    break;

                case DamageModifierOperation.Cap:
                    if (total > modifier.Value)
                    {
                        total = modifier.Value;
                    }
                    break;

                case DamageModifierOperation.Minimum:
                    if (total < modifier.Value)
                    {
                        total = modifier.Value;
                    }
                    break;

                case DamageModifierOperation.MultiplyFloat:
                    total = (int)System.Math.Floor(total * modifier.FloatValue);
                    if (modifier.Priority == DamageModifierPriority.WineMultiplier)
                    {
                        WineMultiplier *= modifier.FloatValue;
                    }
                    break;

                case DamageModifierOperation.FlatReduction:
                    total = total - modifier.Value;
                    break;
            }
        }

        return total < 0 ? 0 : total;
    }

    // 计算所有加法/乘法修正后的伤害，跳过 Cap（封顶）操作。
    // 用于铁卫重甲等需要在鳞甲封顶前判断原始伤害量的效果。
    /// <summary>
    /// Core System 的公开入口：ResolvePreCap。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int ResolvePreCap(int baseDamage)
    {
        // 与 Resolve 保持同一条加算→乘算规则；这里只是刻意跳过伤害上限，
        // 供需要读取封顶前伤害的效果使用。
        var total = baseDamage;
        foreach (var modifier in _modifiers
                     .Where(modifier => modifier.Operation == DamageModifierOperation.Add)
                     .OrderBy(modifier => (int)modifier.Priority))
        {
            total += modifier.Value;
        }

        foreach (var modifier in _modifiers
                     .Where(modifier => modifier.Operation != DamageModifierOperation.Add)
                     .OrderBy(modifier => (int)modifier.Priority))
        {
            switch (modifier.Operation)
            {
                case DamageModifierOperation.Multiply:
                    total *= modifier.Value;
                    break;
                case DamageModifierOperation.Divide:
                    if (modifier.Value > 0)
                        total /= modifier.Value;
                    break;
                case DamageModifierOperation.MultiplyFloat:
                    total = (int)System.Math.Floor(total * modifier.FloatValue);
                    break;
                case DamageModifierOperation.Cap:
                    break;
                case DamageModifierOperation.Minimum:
                    total = System.Math.Max(total, modifier.Value);
                    break;
                case DamageModifierOperation.FlatReduction:
                    total = total - modifier.Value;
                    break;
            }
        }

        return total < 0 ? 0 : total;
    }
}
