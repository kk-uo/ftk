//////////////////////////////////////////////////////////
// 文件：Scripts/RoundResult.cs
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
/// 单次顺手牵羊的结构化结算结果。
///
/// 左侧战报通过该结果显示“生效并偷取多少费用”，不从自然语言日志反推规则。
/// </summary>
public readonly record struct StealResolution(
    string Actor,
    string Target,
    double Amount,
    bool Resolved);

/// <summary>
/// Core System 的公开类：RoundResult。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RoundResult
{
    private readonly List<string> _relations = new();
    private readonly List<string> _lines = new();
    private readonly List<Player> _fullBlockTargets = new();
    private readonly Dictionary<Player, int> _damageByTarget = new();
    private readonly Dictionary<Player, double> _manaGainByUnit = new();
    private readonly Dictionary<Player, int> _healByUnit = new();
    private readonly Dictionary<Player, int> _wineGainByUnit = new();
    private readonly List<StealResolution> _stealResolutions = new();

    /// <summary>亮牌克制关系文案（例如"火杀克制杀"），由 BattleResolver 在结算前调用 AddRelation 写入。</summary>
    public IReadOnlyList<string> Relations => _relations;

    /// <summary>结算过程中的详细叙述文案（例如"龙胆使玩家受到的伤害无效"、酒/伤害计算明细），由各处调用 AddLine 写入。</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>本次结算中完全格挡过伤害的单位，供表现层在对应模型上显示统一飘字。</summary>
    public IReadOnlyList<Player> FullBlockTargets => _fullBlockTargets;

    /// <summary>
    /// 本次结算中每个实际受伤目标的伤害合计。
    ///
    /// 表现层必须按目标寻找模型锚点，不能在结算后重新寻找“第一个存活敌人”；
    /// 否则最后一击杀死敌人时会退回旧卡片坐标，导致飘字出现在屏幕左上角。
    /// </summary>
    public IReadOnlyDictionary<Player, int> DamageByTarget => _damageByTarget;

    /// <summary>
    /// 本回合每个单位通过【费】或即时补偿实际获得的费用。
    ///
    /// 保留单位引用是为了让多敌人日志能区分左、中、右目标；EnemyManaGain 仍作为
    /// 兼容旧统计的合计值存在，但表现层不再依赖该合计猜测具体获得者。
    /// </summary>
    public IReadOnlyDictionary<Player, double> ManaGainByUnit => _manaGainByUnit;

    /// <summary>本回合每个单位的实际治疗量，供多敌人日志显示具体对象。</summary>
    public IReadOnlyDictionary<Player, int> HealByUnit => _healByUnit;

    /// <summary>本回合每个单位获得的酒层数，供多敌人日志显示具体对象。</summary>
    public IReadOnlyDictionary<Player, int> WineGainByUnit => _wineGainByUnit;

    /// <summary>本回合每次顺手牵羊的最终生效状态和实际偷取费用。</summary>
    public IReadOnlyList<StealResolution> StealResolutions => _stealResolutions;

    public int PlayerDamage { get; private set; }
    public int EnemyDamage { get; private set; }
    public int PlayerDodgeBlocks { get; private set; }
    public int EnemyDodgeBlocks { get; private set; }
    /// <summary>玩家主动使用【闪】后实际成功抵挡的总次数，不区分攻击牌种类。</summary>
    public int PlayerCardDodgeBlocks { get; private set; }
    /// <summary>敌人主动使用【闪】后实际成功抵挡的总次数，不区分攻击牌种类。</summary>
    public int EnemyCardDodgeBlocks { get; private set; }
    public int PlayerKillDodgeBlocks { get; private set; }
    public int EnemyKillDodgeBlocks { get; private set; }
    public int PlayerFireKillDodgeBlocks { get; private set; }
    public int EnemyFireKillDodgeBlocks { get; private set; }
    public int PlayerArrowDodgeBlocks { get; private set; }
    public int EnemyArrowDodgeBlocks { get; private set; }
    public int PlayerThunderCounterBlocks { get; private set; }
    public int EnemyThunderCounterBlocks { get; private set; }
    public int PlayerSureKillCounterBlocks { get; private set; }
    public int EnemySureKillCounterBlocks { get; private set; }
    public int PlayerArrowCounterBlocks { get; private set; }
    public int EnemyArrowCounterBlocks { get; private set; }
    public int PlayerStealCounterBlocks { get; private set; }
    public int EnemyStealCounterBlocks { get; private set; }
    public int PlayerQingnangDodgeBlocks { get; private set; }
    public int EnemyQingnangDodgeBlocks { get; private set; }
    public int PlayerUniversalBlocks { get; private set; }
    public int EnemyUniversalBlocks { get; private set; }
    public int PlayerWineBlocks { get; private set; }
    public int EnemyWineBlocks { get; private set; }
    public int PlayerDodgeFailed { get; private set; }
    public int EnemyDodgeFailed { get; private set; }
    public int PlayerSureKillDodgeFailed { get; private set; }
    public int EnemySureKillDodgeFailed { get; private set; }
    public int PlayerNanmanDodgeFailed { get; private set; }
    public int EnemyNanmanDodgeFailed { get; private set; }
    public int PlayerHeal { get; private set; }
    public int EnemyHeal { get; private set; }
    public double PlayerManaGain { get; private set; }
    public double EnemyManaGain { get; private set; }
    public int PlayerWineGain { get; private set; }
    public int EnemyWineGain { get; private set; }
    public double PlayerStealGain { get; private set; }
    public double EnemyStealGain { get; private set; }

    public bool HasLines => _relations.Count > 0
        || _lines.Count > 0
        || PlayerDamage > 0
        || EnemyDamage > 0
        || PlayerDodgeBlocks > 0
        || EnemyDodgeBlocks > 0
        || PlayerKillDodgeBlocks > 0
        || EnemyKillDodgeBlocks > 0
        || PlayerFireKillDodgeBlocks > 0
        || EnemyFireKillDodgeBlocks > 0
        || PlayerArrowDodgeBlocks > 0
        || EnemyArrowDodgeBlocks > 0
        || PlayerThunderCounterBlocks > 0
        || EnemyThunderCounterBlocks > 0
        || PlayerSureKillCounterBlocks > 0
        || EnemySureKillCounterBlocks > 0
        || PlayerArrowCounterBlocks > 0
        || EnemyArrowCounterBlocks > 0
        || PlayerStealCounterBlocks > 0
        || EnemyStealCounterBlocks > 0
        || PlayerQingnangDodgeBlocks > 0
        || EnemyQingnangDodgeBlocks > 0
        || PlayerUniversalBlocks > 0
        || EnemyUniversalBlocks > 0
        || PlayerWineBlocks > 0
        || EnemyWineBlocks > 0
        || PlayerDodgeFailed > 0
        || EnemyDodgeFailed > 0
        || PlayerSureKillDodgeFailed > 0
        || EnemySureKillDodgeFailed > 0
        || PlayerNanmanDodgeFailed > 0
        || EnemyNanmanDodgeFailed > 0
        || _stealResolutions.Count > 0;

    public string Text
    {
        get
        {
            var parts = new List<string>();
            parts.AddRange(_relations);
            AddBlockLines(parts, "玩家", PlayerUniversalBlocks, PlayerWineBlocks, PlayerDodgeBlocks, PlayerKillDodgeBlocks, PlayerFireKillDodgeBlocks, PlayerArrowDodgeBlocks, PlayerThunderCounterBlocks, PlayerSureKillCounterBlocks, PlayerArrowCounterBlocks, PlayerStealCounterBlocks, PlayerQingnangDodgeBlocks, PlayerDodgeFailed, PlayerSureKillDodgeFailed, PlayerNanmanDodgeFailed);
            AddBlockLines(parts, "敌方", EnemyUniversalBlocks, EnemyWineBlocks, EnemyDodgeBlocks, EnemyKillDodgeBlocks, EnemyFireKillDodgeBlocks, EnemyArrowDodgeBlocks, EnemyThunderCounterBlocks, EnemySureKillCounterBlocks, EnemyArrowCounterBlocks, EnemyStealCounterBlocks, EnemyQingnangDodgeBlocks, EnemyDodgeFailed, EnemySureKillDodgeFailed, EnemyNanmanDodgeFailed);
            parts.AddRange(_lines);

            if (PlayerHeal > 0)
            {
                parts.Add($"玩家恢复{PlayerHeal}点生命。");
            }
            if (EnemyHeal > 0)
            {
                parts.Add($"敌方恢复{EnemyHeal}点生命。");
            }
            if (PlayerManaGain > 0)
            {
                parts.Add($"玩家获得{BattleRules.FormatMana(PlayerManaGain)}点费用。");
            }
            if (EnemyManaGain > 0)
            {
                parts.Add($"敌方获得{BattleRules.FormatMana(EnemyManaGain)}点费用。");
            }
            if (PlayerStealGain > 0)
            {
                parts.Add($"玩家窃取{BattleRules.FormatMana(PlayerStealGain)}点费用。");
            }
            if (EnemyStealGain > 0)
            {
                parts.Add($"敌方窃取{BattleRules.FormatMana(EnemyStealGain)}点费用。");
            }
            if (PlayerWineGain > 0)
            {
                parts.Add($"玩家获得酒状态 ×{PlayerWineGain}（下一回合杀系伤害 ×{FormatMultiplier(BattleRules.GetWineDamageMultiplier(PlayerWineGain) + GameManager.WineDamageMultiplierBonus)}）。");
            }
            if (EnemyWineGain > 0)
            {
                parts.Add($"敌方获得酒状态 ×{EnemyWineGain}（下一回合杀系伤害 ×{BattleRules.GetWineDamageMultiplier(EnemyWineGain)}）。");
            }
            if (PlayerDamage > 0)
            {
                parts.Add($"玩家受到{PlayerDamage}点伤害。");
            }
            if (EnemyDamage > 0)
            {
                parts.Add($"敌方受到{EnemyDamage}点伤害。");
            }
            if (parts.Count == 0)
            {
                parts.Add("无人受伤。");
            }

            return string.Join("\n", parts);
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddRelation。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddRelation(string line)
    {
        if (!_relations.Contains(line))
        {
            _relations.Add(line);
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddLine。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddLine(string line)
    {
        if (!_lines.Contains(line))
        {
            _lines.Add(line);
        }
    }

    /// <summary>
    /// 记录一次完全格挡。
    ///
    /// 同一结算阶段内每个单位只登记一次，避免一个伤害事件被多个监听器观察时重复飘字。
    /// </summary>
    public void AddFullBlock(Player target)
    {
        if (!_fullBlockTargets.Contains(target))
        {
            _fullBlockTargets.Add(target);
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDamage(Player target, int amount)
    {
        if (amount > 0)
        {
            _damageByTarget[target] = _damageByTarget.GetValueOrDefault(target) + amount;
        }

        // 阵营才是战斗身份的稳定来源。角色选择后玩家名称会变成“孙策”等具体名称，
        // 继续用显示名判断会把玩家受到的伤害错误累计到 EnemyDamage。
        if (target.Team == BattleTeam.Player)
        {
            PlayerDamage += amount;
        }
        else
        {
            EnemyDamage += amount;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddDodgeBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDodgeBlock(bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            PlayerDodgeBlocks += 1;
            PlayerCardDodgeBlocks += 1;
        }
        else
        {
            EnemyDodgeBlocks += 1;
            EnemyCardDodgeBlocks += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddPersistentDodgeBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddPersistentDodgeBlock(bool defenderIsPlayer, CardType attackType)
    {
        if (defenderIsPlayer)
        {
            PlayerCardDodgeBlocks += 1;
        }
        else
        {
            EnemyCardDodgeBlocks += 1;
        }

        if (attackType == CardType.Kill)
        {
            if (defenderIsPlayer)
            {
                PlayerKillDodgeBlocks += 1;
            }
            else
            {
                EnemyKillDodgeBlocks += 1;
            }

            return;
        }

        if (attackType == CardType.FireKill)
        {
            if (defenderIsPlayer)
            {
                PlayerFireKillDodgeBlocks += 1;
            }
            else
            {
                EnemyFireKillDodgeBlocks += 1;
            }

            return;
        }

        if (attackType == CardType.ArrowBarrage)
        {
            if (defenderIsPlayer)
            {
                PlayerArrowDodgeBlocks += 1;
            }
            else
            {
                EnemyArrowDodgeBlocks += 1;
            }

            return;
        }

        // 新增可被【闪】抵挡的牌无需再扩展表现层；未设置专用日志字段时，
        // 统一归入普通闪格挡，同时保留上面的结构化总次数。
        if (defenderIsPlayer)
        {
            PlayerDodgeBlocks += 1;
        }
        else
        {
            EnemyDodgeBlocks += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：RemovePersistentDodgeBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RemovePersistentDodgeBlock(bool defenderIsPlayer, CardType attackType)
    {
        if (defenderIsPlayer)
        {
            PlayerCardDodgeBlocks = System.Math.Max(0, PlayerCardDodgeBlocks - 1);
        }
        else
        {
            EnemyCardDodgeBlocks = System.Math.Max(0, EnemyCardDodgeBlocks - 1);
        }

        if (attackType == CardType.Kill)
        {
            if (defenderIsPlayer)
            {
                PlayerKillDodgeBlocks = System.Math.Max(0, PlayerKillDodgeBlocks - 1);
            }
            else
            {
                EnemyKillDodgeBlocks = System.Math.Max(0, EnemyKillDodgeBlocks - 1);
            }
            return;
        }

        if (attackType == CardType.FireKill)
        {
            if (defenderIsPlayer)
            {
                PlayerFireKillDodgeBlocks = System.Math.Max(0, PlayerFireKillDodgeBlocks - 1);
            }
            else
            {
                EnemyFireKillDodgeBlocks = System.Math.Max(0, EnemyFireKillDodgeBlocks - 1);
            }
            return;
        }

        if (attackType == CardType.ArrowBarrage)
        {
            if (defenderIsPlayer)
            {
                PlayerArrowDodgeBlocks = System.Math.Max(0, PlayerArrowDodgeBlocks - 1);
            }
            else
            {
                EnemyArrowDodgeBlocks = System.Math.Max(0, EnemyArrowDodgeBlocks - 1);
            }
            return;
        }

        if (defenderIsPlayer)
        {
            PlayerDodgeBlocks = System.Math.Max(0, PlayerDodgeBlocks - 1);
        }
        else
        {
            EnemyDodgeBlocks = System.Math.Max(0, EnemyDodgeBlocks - 1);
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddQingnangDodgeBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddQingnangDodgeBlock(bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            PlayerQingnangDodgeBlocks += 1;
        }
        else
        {
            EnemyQingnangDodgeBlocks += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddUniversalBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddUniversalBlock(bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            PlayerUniversalBlocks += 1;
        }
        else
        {
            EnemyUniversalBlocks += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddWineBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddWineBlock(bool defenderIsPlayer)
    {
        if (defenderIsPlayer)
        {
            PlayerWineBlocks += 1;
        }
        else
        {
            EnemyWineBlocks += 1;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddDodgeFailed。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDodgeFailed(bool defenderIsPlayer, CardType attackType)
    {
        if (attackType == CardType.SureKill)
        {
            if (defenderIsPlayer)
            {
                PlayerSureKillDodgeFailed += 1;
            }
            else
            {
                EnemySureKillDodgeFailed += 1;
            }
        }
        else if (attackType is CardType.NanmanInvasion or CardType.Tuxi)
        {
            if (defenderIsPlayer)
            {
                PlayerNanmanDodgeFailed += 1;
            }
            else
            {
                EnemyNanmanDodgeFailed += 1;
            }
        }
        else
        {
            if (defenderIsPlayer)
            {
                PlayerDodgeFailed += 1;
            }
            else
            {
                EnemyDodgeFailed += 1;
            }
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddCounterDefenseBlock。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddCounterDefenseBlock(bool defenderIsPlayer, CardType attackType)
    {
        switch (attackType)
        {
            case CardType.ThunderKill:
                if (defenderIsPlayer)
                {
                    PlayerThunderCounterBlocks += 1;
                }
                else
                {
                    EnemyThunderCounterBlocks += 1;
                }
                break;
            case CardType.SureKill:
                if (defenderIsPlayer)
                {
                    PlayerSureKillCounterBlocks += 1;
                }
                else
                {
                    EnemySureKillCounterBlocks += 1;
                }
                break;
            case CardType.ArrowBarrage:
                if (defenderIsPlayer)
                {
                    PlayerArrowCounterBlocks += 1;
                }
                else
                {
                    EnemyArrowCounterBlocks += 1;
                }
                break;
            case CardType.Steal:
                if (defenderIsPlayer)
                {
                    PlayerStealCounterBlocks += 1;
                }
                else
                {
                    EnemyStealCounterBlocks += 1;
                }
                break;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddHeal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddHeal(string ownerName, int amount)
    {
        if (IsPlayerDisplayName(ownerName))
        {
            PlayerHeal += amount;
        }
        else
        {
            EnemyHeal += amount;
        }
    }

    /// <summary>
    /// 记录指定单位的实际治疗量，并同步维护旧的双方合计字段。
    /// </summary>
    public void AddHeal(Player owner, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _healByUnit[owner] = _healByUnit.GetValueOrDefault(owner) + amount;
        if (owner.Team == BattleTeam.Player)
        {
            PlayerHeal += amount;
        }
        else
        {
            EnemyHeal += amount;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddResourceGain。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddResourceGain(string ownerName)
    {
        if (IsPlayerDisplayName(ownerName))
        {
            PlayerManaGain += 1;
        }
        else
        {
            EnemyManaGain += 1;
        }
    }

    /// <summary>
    /// 记录指定战斗单位实际获得的费用，并同步维护旧的双方合计字段。
    /// </summary>
    public void AddResourceGain(Player owner, double amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        _manaGainByUnit[owner] = _manaGainByUnit.GetValueOrDefault(owner) + amount;
        if (owner.Team == BattleTeam.Player)
        {
            PlayerManaGain += amount;
        }
        else
        {
            EnemyManaGain += amount;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddWineStatus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddWineStatus(string ownerName, int amount)
    {
        if (IsPlayerDisplayName(ownerName))
        {
            PlayerWineGain += amount;
        }
        else
        {
            EnemyWineGain += amount;
        }
    }

    /// <summary>
    /// 记录指定单位获得的酒层数，并同步维护旧的双方合计字段。
    /// </summary>
    public void AddWineStatus(Player owner, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _wineGainByUnit[owner] = _wineGainByUnit.GetValueOrDefault(owner) + amount;
        if (owner.Team == BattleTeam.Player)
        {
            PlayerWineGain += amount;
        }
        else
        {
            EnemyWineGain += amount;
        }
    }

    /// <summary>
    /// Core System 的公开入口：AddSteal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddSteal(string ownerName, double amount)
    {
        if (IsPlayerDisplayName(ownerName))
        {
            PlayerStealGain += amount;
        }
        else
        {
            EnemyStealGain += amount;
        }
    }

    /// <summary>
    /// Records a steal from unit identity rather than a localized display name.
    /// New battle code must use this overload.
    /// </summary>
    public void AddSteal(Player owner, double amount)
    {
        if (owner.Team == BattleTeam.Player)
        {
            PlayerStealGain += amount;
        }
        else
        {
            EnemyStealGain += amount;
        }
    }

    /// <summary>
    /// 记录顺手牵羊最终是否生效以及实际转移的费用。
    ///
    /// 生效但目标没有费用时 Amount 为 0；被取消或不满足发动条件时 Resolved 为 false。
    /// </summary>
    public void AddStealResolution(string actor, string target, double amount, bool resolved)
    {
        _stealResolutions.Add(new StealResolution(actor, target, amount, resolved));
    }

    /// <summary>
    /// Core System 的公开入口：AddStealFailure。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddStealFailure(string ownerName)
    {
        AddLine(IsPlayerDisplayName(ownerName)
            ? Localization.Get("battle.steal.failure.player")
            : Localization.Get("battle.steal.failure.enemy"));
        AddLine(Localization.Get("battle.steal.failure.resource"));
    }

    /// <summary>Records a failed steal from unit identity rather than display text.</summary>
    public void AddStealFailure(Player owner)
    {
        AddLine(Localization.Get(owner.Team == BattleTeam.Player
            ? "battle.steal.failure.player"
            : "battle.steal.failure.enemy"));
        AddLine(Localization.Get("battle.steal.failure.resource"));
    }

    private static bool IsPlayerDisplayName(string ownerName) =>
        ownerName == Localization.Get("battle.player_name");

    /// <summary>
    /// Core System 的公开入口：NullifyPlayerDamageByLongdan。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public int NullifyPlayerDamageByLongdan()
    {
        var amount = PlayerDamage;
        if (amount <= 0)
        {
            return 0;
        }

        PlayerDamage = 0;
        AddLine("龙胆使玩家受到的伤害无效。");
        return amount;
    }

    /// <summary>
    /// Core System 的公开入口：AddWineDamageBonus。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddWineDamageBonus(string ownerName, int wineStacks, double wineMultiplier, int finalDamage)
    {
        if (wineStacks <= 0)
        {
            return;
        }

        AddLine($"{ownerName}酒状态生效。");
        AddLine($"酒层数：{wineStacks}");
        AddLine($"酒倍率：×{FormatMultiplier(wineMultiplier)}");
        AddLine($"酒结算后伤害：{finalDamage}");
    }

    /// <summary>
    /// Core System 的公开入口：AddDamageCalculation。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDamageCalculation(string sourceName, string targetName, string attackName, int baseDamage, int flatBonus, double wineMultiplier, int specialMultiplier, int vulnerableMultiplier, int wushuangMultiplier, int wineStacks, int finalDamage)
    {
        AddLine($"{sourceName} 对 {targetName} 的{attackName}伤害结算：");
        AddLine($"基础伤害：{baseDamage}");
        AddLine($"固定加伤：+{flatBonus}");
        AddLine($"酒倍率：×{FormatMultiplier(wineMultiplier)}");
        AddLine($"特殊倍率：×{specialMultiplier}");
        AddLine($"易伤/减伤倍率：×{vulnerableMultiplier}");
        AddLine($"无双倍率：×{wushuangMultiplier}");
        AddLine($"酒层数：{wineStacks}");
        AddLine($"最终伤害：{finalDamage}");
    }

    private static string FormatMultiplier(double value)
    {
        return value.ToString("0.##");
    }

    private static void AddBlockLines(List<string> parts, string ownerName, int universalBlocks, int wineBlocks, int dodgeBlocks, int killDodgeBlocks, int fireKillDodgeBlocks, int arrowDodgeBlocks, int thunderCounterBlocks, int sureKillCounterBlocks, int arrowCounterBlocks, int stealCounterBlocks, int qingnangDodgeBlocks, int dodgeFailed, int sureKillDodgeFailed, int nanmanDodgeFailed)
    {
        if (universalBlocks > 0)
        {
            parts.Add($"{ownerName}桃护盾抵挡{universalBlocks}次伤害。");
        }
        if (wineBlocks > 0)
        {
            parts.Add($"{ownerName}酒护盾抵挡{wineBlocks}次伤害，酒状态减少{wineBlocks}层。");
        }
        if (killDodgeBlocks > 0)
        {
            parts.Add($"{ownerName}闪抵消全部杀。");
        }
        if (fireKillDodgeBlocks > 0)
        {
            parts.Add($"{ownerName}闪抵消全部火杀。");
        }
        if (arrowDodgeBlocks > 0)
        {
            parts.Add($"{ownerName}闪抵消全部万箭齐发。");
        }
        if (dodgeBlocks > 0 && killDodgeBlocks == 0 && fireKillDodgeBlocks == 0 && arrowDodgeBlocks == 0)
        {
            parts.Add($"{ownerName}闪抵挡{dodgeBlocks}次伤害。");
        }
        if (thunderCounterBlocks > 0)
        {
            parts.Add($"{ownerName}无懈可击抵消全部雷杀。");
        }
        if (sureKillCounterBlocks > 0)
        {
            parts.Add($"{ownerName}无懈可击抵消全部必中杀。");
        }
        if (arrowCounterBlocks > 0)
        {
            parts.Add($"{ownerName}无懈可击抵消全部万箭齐发。");
        }
        if (stealCounterBlocks > 0)
        {
            parts.Add($"{ownerName}无懈可击抵消全部顺手牵羊。");
        }
        if (qingnangDodgeBlocks > 0)
        {
            parts.Add($"{ownerName}青囊闪避抵挡{qingnangDodgeBlocks}次伤害。");
        }
        if (dodgeFailed > 0)
        {
            parts.Add($"{ownerName}闪无法抵挡雷杀。");
        }
        if (sureKillDodgeFailed > 0)
        {
            parts.Add($"{ownerName}闪无法抵挡必中杀。");
        }
        if (nanmanDodgeFailed > 0)
        {
            parts.Add($"{ownerName}闪无法抵挡南蛮入侵。");
        }
    }
}
