//////////////////////////////////////////////////////////
// 文件：Scripts/SharedHealthPool.cs
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

/// <summary>
/// Core System 的公开类：SharedHealthPool。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SharedHealthPool
{
    private int _currentHP;
    private readonly List<EnemyInstance> _members = new();

    /// <summary>
    /// Core System 的公开入口：SharedHealthPool。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public SharedHealthPool(int maxHP)
    {
        MaxHP = maxHP;
        _currentHP = maxHP;
    }

    public int MaxHP { get; }
    public int CurrentHP => _currentHP;
    public bool IsDepleted => _currentHP <= 0;
    public IReadOnlyList<EnemyInstance> Members => _members;

    /// <summary>
    /// Core System 的公开入口：AddMember。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddMember(EnemyInstance enemy)
    {
        _members.Add(enemy);
    }

    /// <summary>
    /// Core System 的公开入口：TakeDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        _currentHP -= amount;
        if (_currentHP < 0) _currentHP = 0;
        SyncAll();
    }

    /// <summary>
    /// Core System 的公开入口：Heal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0) return 0;
        var before = _currentHP;
        _currentHP = System.Math.Min(MaxHP, _currentHP + amount);
        SyncAll();
        return _currentHP - before;
    }

    private void SyncAll()
    {
        foreach (var m in _members)
        {
            m.SyncFromSharedPool();
        }
    }
}
