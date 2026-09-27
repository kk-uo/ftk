//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyAI.cs
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

using System;
using System.Collections.Generic;

/// <summary>
/// Enemy System 的公开类：EnemyAI。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed partial class EnemyAI
{
    private readonly Random _random = new();
    private readonly Dictionary<string, CardType> _lastActionByEnemyId = new();
    private readonly Dictionary<string, int> _sameActionStreakByEnemyId = new();

    /// <summary>
    /// Enemy System 的公开入口：Reset。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Reset()
    {
        _lastActionByEnemyId.Clear();
        _sameActionStreakByEnemyId.Clear();
    }

    /// <summary>
    /// Enemy System 的公开入口：Evaluate。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyAiEvaluation Evaluate(
        Player enemy,
        Player opponent,
        EnemyDefinition? definition = null,
        IReadOnlyList<EnemyInstance>? allies = null)
    {
        if (definition == null)
        {
            return BuildFallbackEvaluation(enemy, opponent);
        }

        var evaluation = new EnemyAiEvaluation(definition.ActionWeights.Clone());
        var effectiveWeights = BuildEffectiveWeights(enemy, opponent, definition, evaluation, allies);
        evaluation.FinalWeights = effectiveWeights.Clone();
        return evaluation;
    }

    /// <summary>
    /// Enemy System 的公开入口：SelectAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Card SelectAction(
        Player enemy,
        Player opponent,
        EnemyDefinition? definition = null,
        IReadOnlyList<EnemyInstance>? allies = null)
    {
        Card selected;
        // 自爆待发：优先级最高，直接返回自爆攻击（跳过所有普通AI逻辑）。
        if (enemy is EnemyInstance zibaoEnemy
            && zibaoEnemy.RuntimeStates.TryGetValue("zibao_pending", out var ziPending)
            && ziPending is true)
        {
            selected = Card.ZiBaoAttack();
        }
        // 血债血偿濒死待发：优先级次高，直接返回血债血偿攻击（跳过所有普通AI逻辑）。
        else if (enemy is EnemyInstance xzEnemy
            && xzEnemy.RuntimeStates.TryGetValue("xuezhaixuechou_near_death_pending", out var xzPending)
            && xzPending is true)
        {
            selected = Card.XueZhaiAttack();
        }
        else if (definition?.Id == "royal_death_guard")
        {
            selected = SelectRoyalDeathGuardAction(enemy, opponent);
        }
        else if (definition?.Id == "huan_xiang_chu_shou")
        {
            selected = SelectHuanXiangChuShouAction(enemy, opponent, definition);
        }
        else if (definition?.Id == "scout" && enemy is EnemyInstance scoutEnemy && IsScoutVigilant(scoutEnemy))
        {
            // 警戒状态：硬性只能出费，不走任何权重/规则逻辑。
            selected = Card.Fee();
        }
        else if (definition?.Id == "ju_kou")
        {
            selected = SelectJuKouAction(enemy, opponent, definition, allies);
        }
        else
        {
            selected = definition == null
                ? PickFallbackAction(enemy, opponent)
                : PickDefinitionAction(enemy, opponent, definition, allies);
        }

        RecordSelection(enemy, selected.Type);
        return selected;
    }

    // 皇家死侍专属AI：影袭状态机驱动，始终优先进入影袭；3回合内等概率随机一回合出影袭杀。
    private Card SelectRoyalDeathGuardAction(Player enemy, Player opponent)
    {
        // 优先级1：非影袭状态且费用足够（含免费影袭）→ 100%发动影袭。
        if (!enemy.InShadowState && CanPlayActionFromPool(enemy, Card.YingXiActivate()))
        {
            return Card.YingXiActivate();
        }

        // 优先级2-5：影袭状态中，按状态机决定出招。
        if (enemy.InShadowState)
        {
            EnsureRoyalDeathGuardShadowKillReserve(enemy);

            // 优先级5：本期已出过影袭杀 → 潜伏。
            if (enemy.ShadowSlashUsed || !CanPlayActionFromPool(enemy, Card.ShadowKill()))
            {
                return Card.ShadowLurk();
            }

            // 优先级3：等概率随机选择本回合是否出影袭杀（1/剩余回合数），确保3回合内恰好出一次。
            var remainingTurns = System.Math.Max(1, enemy.RemainingTurns);
            if (_random.NextDouble() < 1.0 / remainingTurns)
            {
                return Card.ShadowKill();
            }

            return Card.ShadowLurk();
        }

        return Card.Fee();
    }

    /// <summary>
    /// 皇家死侍的影袭杀是影袭状态的核心威胁，不能因为发动影袭恰好耗尽费用而被排除。
    /// 每次进入影袭后，在尚未使用影袭杀时保留至少1费；这只适用于该精英的专属AI。
    /// </summary>
    private static void EnsureRoyalDeathGuardShadowKillReserve(Player enemy)
    {
        if (!enemy.InShadowState || enemy.ShadowSlashUsed || enemy.CurrentMana >= Card.ShadowKill().Cost)
        {
            return;
        }

        enemy.GainMana(Card.ShadowKill().Cost - enemy.CurrentMana);
        enemy.RuntimeStates["royal_death_guard_shadow_reserve"] = true;
    }

    // 幻象触手专属AI：防御/攻击双模式状态机。
    // 初始防御模式：优先出闪/无懈/费；被命中后切攻击模式；攻击命中后切回防御模式。
    private Card SelectHuanXiangChuShouAction(Player enemy, Player opponent, EnemyDefinition definition)
    {
        var mode = HuanXiangKeys.AiModeDefend;
        if (enemy is EnemyInstance inst
            && inst.RuntimeStates.TryGetValue(HuanXiangKeys.AiMode, out var modeVal)
            && modeVal is string modeStr)
        {
            mode = modeStr;
        }

        var available = GetAvailableActions(enemy, definition);
        var choices = new List<WeightedCard>();

        foreach (var card in available)
        {
            var weight = mode == HuanXiangKeys.AiModeAttack
                ? GetHuanXiangAttackModeWeight(card.Type, opponent)
                : GetHuanXiangDefendModeWeight(card.Type, opponent);
            if (weight > 0)
            {
                AddChoice(choices, enemy, card, weight);
            }
        }

        return PickFromAvailableActions(choices, available);
    }

    private static float GetHuanXiangAttackModeWeight(CardType type, Player opponent)
    {
        return type switch
        {
            CardType.ThunderKill => 80,
            CardType.Kill        => 70,
            CardType.FireKill    => 60,
            CardType.Fee         => 25,
            CardType.Dodge       => 5,
            CardType.Unassailable => 5,
            CardType.Peach       => opponent.CurrentMana <= 0 ? 10 : 5,
            _ => 0
        };
    }

    private static float GetHuanXiangDefendModeWeight(CardType type, Player opponent)
    {
        return type switch
        {
            CardType.Dodge       => opponent.CurrentMana >= 1 ? 80 : 20,
            CardType.Unassailable => opponent.CurrentMana >= 1 ? 60 : 10,
            CardType.Peach       => 30,
            CardType.Fee         => 35,
            CardType.Kill        => 8,
            CardType.ThunderKill => 8,
            _ => 0
        };
    }

    // 侦测者是否仍处于【警戒状态】（战斗开始默认true，任意实际伤害或第4回合起自动解除，
    // 见 Scripts/ChapterFourEnemyBehaviors.cs 的 ScoutVigilanceClearOnDamageEffect/
    // ScoutVigilanceTurnStartEffect，以及 EnemyInstance 构造函数里的默认置位）。
    private static bool IsScoutVigilant(EnemyInstance enemy)
    {
        return enemy.RuntimeStates.TryGetValue("scout_vigilant", out var vigilant) && vigilant is true;
    }

    // 巨口专属AI：在默认权重系统之上叠加"必中杀最高优先级"“连续2回合未造成伤害则大幅
    // 提权攻击”“吸血之牙本回合未触发则优先主动攻击”三条规则——后两条依赖自定义
    // RuntimeStates 计数器（见 Scripts/ChapterFourEnemyBehaviors.cs），声明式 AiRule 系统
    // 无法表达，只能在这里用代码分支叠加。HP<50% 的提权已经在 definition.AiRules 里用
    // 现成的 SelfHpBelow 声明式表达，BuildEffectiveWeights 会自动包含。
    private Card SelectJuKouAction(
        Player enemy,
        Player opponent,
        EnemyDefinition definition,
        IReadOnlyList<EnemyInstance>? allies)
    {
        var effectiveWeights = BuildEffectiveWeights(enemy, opponent, definition, null, allies);
        var availableActions = GetAvailableActions(enemy, definition);

        // 必中杀最高优先级：只要能出，就给一个远超其它选项的权重。
        if (availableActions.Exists(card => card.Type == CardType.SureKill))
        {
            effectiveWeights.SureSlash = Math.Max(effectiveWeights.SureSlash, 500f);
        }

        if (enemy is EnemyInstance juKouEnemy)
        {
            // 连续2回合未造成伤害：下一回合大幅提权攻击牌，主动寻找输出机会。
            if (JuKouNoDamageTrackEffect.GetNoDamageTurns(juKouEnemy) >= 2)
            {
                effectiveWeights.AddWeight(EnemyActionWeightType.Slash, 60);
                effectiveWeights.AddWeight(EnemyActionWeightType.FireSlash, 40);
                effectiveWeights.AddWeight(EnemyActionWeightType.ThunderSlash, 40);
                effectiveWeights.AddWeight(EnemyActionWeightType.SureSlash, 60);
            }

            // 吸血之牙本回合尚未触发：优先发动一次主动攻击，以触发回血效果。
            var fangTriggered = juKouEnemy.RuntimeStates.TryGetValue("vampiric_fang_triggered", out var used) && used is true;
            if (!fangTriggered && juKouEnemy.HasEquipment(EquipmentIds.VampiricFang))
            {
                effectiveWeights.AddWeight(EnemyActionWeightType.Slash, 30);
                effectiveWeights.AddWeight(EnemyActionWeightType.FireSlash, 20);
                effectiveWeights.AddWeight(EnemyActionWeightType.ThunderSlash, 20);
                effectiveWeights.AddWeight(EnemyActionWeightType.SureSlash, 30);
            }
        }

        var choices = new List<WeightedCard>();
        foreach (var card in availableActions)
        {
            AddConfiguredChoice(choices, enemy, opponent, definition, card, effectiveWeights.GetWeight(card.Type));
        }

        return PickFromAvailableActions(choices, availableActions);
    }

    /// <summary>
    /// Enemy System 的公开入口：GetAvailableActions。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public List<Card> GetAvailableActions(Player enemy, EnemyDefinition? definition = null)
    {
        // 自爆待发：唯一可用行动为自爆攻击，无视冰冻、影袭等状态。
        if (enemy is EnemyInstance ziEnemy
            && ziEnemy.RuntimeStates.TryGetValue("zibao_pending", out var ziPend)
            && ziPend is true)
        {
            return new List<Card> { Card.ZiBaoAttack() };
        }

        // 血债血偿濒死待发：唯一可用行动为血债血偿攻击，无视冰冻、影袭等状态。
        if (enemy is EnemyInstance xzAvailEnemy
            && xzAvailEnemy.RuntimeStates.TryGetValue("xuezhaixuechou_near_death_pending", out var xzAvailPend)
            && xzAvailPend is true)
        {
            return new List<Card> { Card.XueZhaiAttack() };
        }

        if (enemy.GuanxingPhase == GuanxingPhase.Repeating && enemy.GuanxingRecordedCardType.HasValue)
        {
            return new List<Card> { new Card(enemy.GuanxingRecordedCardType.Value) };
        }

        if (enemy.IsFrozen)
        {
            return new List<Card> { Card.Fee() };
        }

        // 侦测者【警戒状态】：硬性只能出费，与冰冻同款处理，不走权重系统。
        if (enemy is EnemyInstance scoutAvailEnemy && IsScoutVigilant(scoutAvailEnemy))
        {
            return new List<Card> { Card.Fee() };
        }

        // 影袭状态：仅可出影袭杀（若未出招）或潜伏。
        if (enemy.InShadowState)
        {
            if (definition?.Id == "royal_death_guard")
            {
                EnsureRoyalDeathGuardShadowKillReserve(enemy);
            }

            var shadowCards = new List<Card>();
            if (!enemy.ShadowSlashUsed && CanPlayActionFromPool(enemy, Card.ShadowKill()))
            {
                shadowCards.Add(Card.ShadowKill());
            }
            shadowCards.Add(Card.ShadowLurk());
            return shadowCards;
        }

        var cards = new List<Card>();
        var seen = new HashSet<CardType>();

        void AddCard(Card card)
        {
            if (seen.Add(card.Type))
            {
                cards.Add(card);
            }
        }

        bool HasInCurrentActionBar(CardType type)
        {
            if (enemy is EnemyInstance instance)
            {
                return instance.RuntimeDeck.Contains(type);
            }

            return definition != null && HasCard(definition, type);
        }

        void AddIfAvailable(Card card)
        {
            if (HasInCurrentActionBar(card.Type) && CanPlayActionFromPool(enemy, card))
            {
                AddCard(card);
            }
        }

        AddIfAvailable(Card.Fee());
        AddIfAvailable(Card.Dodge());
        AddIfAvailable(Card.Kill());
        AddIfAvailable(Card.FireKill());
        AddIfAvailable(Card.ThunderKill());
        AddIfAvailable(Card.FireThunderKill());
        AddIfAvailable(Card.CelestialImpact());
        AddIfAvailable(Card.ArrowBarrage());
        AddIfAvailable(Card.NanmanInvasion());
        AddIfAvailable(Card.Peach());
        AddIfAvailable(Card.Wine());
        AddIfAvailable(Card.Steal());
        AddIfAvailable(Card.Unassailable());

        if ((enemy.HasSkill(SkillIds.Bizhong) || HasInCurrentActionBar(CardType.SureKill))
            && CanPlayActionFromPool(enemy, Card.SureKill()))
        {
            AddCard(Card.SureKill());
        }

        if ((enemy.HasSkill(SkillIds.JiHan) || HasInCurrentActionBar(CardType.IceKill))
            && CanPlayActionFromPool(enemy, Card.IceKill()))
        {
            AddCard(Card.IceKill());
        }

        if ((enemy.HasSkill(SkillIds.Guanxing) || HasInCurrentActionBar(CardType.Guanxing))
            && enemy.GuanxingPhase == GuanxingPhase.None
            && CanPlayActionFromPool(enemy, Card.Guanxing()))
        {
            AddCard(Card.Guanxing());
        }

        // 影袭技能：非影袭状态时可激活影袭。
        if (enemy.HasSkill(SkillIds.YingXi) && CanPlayActionFromPool(enemy, Card.YingXiActivate()))
        {
            AddCard(Card.YingXiActivate());
        }

        if (enemy.HasSkill(SkillIds.Tuxi) && CanPlayActionFromPool(enemy, Card.Tuxi()))
        {
            AddCard(Card.Tuxi());
        }

        return cards;
    }

    /// <summary>
    /// Enemy System 的公开入口：GetAvailableActionCards。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public List<Card> GetAvailableActionCards(Player enemy, EnemyDefinition? definition = null)
    {
        return GetAvailableActions(enemy, definition);
    }

    /// <summary>
    /// Enemy System 的公开入口：IsCardAvailableThisTurn。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool IsCardAvailableThisTurn(Player enemy, EnemyDefinition? definition, CardType type)
    {
        foreach (var card in GetAvailableActions(enemy, definition))
        {
            if (card.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    // Actor-aware target selection: Wine is redirected to the highest-value ally.
    /// <summary>
    /// Enemy System 的公开入口：SelectTarget。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public BattleUnit SelectTarget(
        EnemyInstance actor,
        Card card,
        Player opponent,
        IReadOnlyList<EnemyInstance>? allies = null)
    {
        if (card.Type == CardType.Wine && allies != null)
        {
            return SelectWineTarget(actor, opponent, allies);
        }

        return opponent;
    }

    private BattleUnit SelectWineTarget(
        EnemyInstance actor,
        Player opponent,
        IReadOnlyList<EnemyInstance> allies)
    {
        // 酒徒：自饮自爆型，始终只给自己上酒，不参与"给爆发潜力最高的队友"评分。
        if (actor.Definition?.Id == "wine_drinker")
        {
            return actor;
        }

        EnemyInstance? bestAlly = null;
        var bestScore = -1f;

        foreach (var ally in allies)
        {
            if (ally.IsDead)
            {
                continue;
            }

            var score = EstimateAllyOutputScore(ally);
            if (score > bestScore)
            {
                bestScore = score;
                bestAlly = ally;
            }
        }

        // Fall back to self only when no other ally is alive.
        return bestAlly ?? (BattleUnit)actor;
    }

    // Score how much value a Wine buff would add to this ally next turn.
    // Higher = better candidate to receive Wine.
    private static float EstimateAllyOutputScore(EnemyInstance ally)
    {
        var score = 0f;

        // JetMace still active: next attack will be ×3 — highest burst potential.
        if (ally.HasEquipment(EquipmentIds.JetMace)
            && ally.RuntimeStates.TryGetValue("jet_mace_active", out var jmActive)
            && jmActive is true)
        {
            score += 60f;
        }

        // Sword equipment provides flat attack bonus.
        if (ally.HasEquipment(EquipmentIds.BlueSteelSword) || ally.HasEquipment(EquipmentIds.RustBlueSteelSword))
        {
            score += 15f;
        }

        // High-damage card types in deck.
        if (HasDeckCard(ally, CardType.FireThunderKill)) score += 30f;
        if (HasDeckCard(ally, CardType.FireKill))       score += 15f;
        if (HasDeckCard(ally, CardType.ThunderKill))    score += 15f;
        if (HasDeckCard(ally, CardType.SureKill))       score += 10f;
        if (HasDeckCard(ally, CardType.IceKill))        score += 20f;
        if (HasDeckCard(ally, CardType.Kill))           score += 5f;
        if (HasDeckCard(ally, CardType.ArrowBarrage))   score += 5f;

        // Mana: higher mana = more likely to attack this turn.
        if (ally.CurrentMana >= 2)      score += 20f;
        else if (ally.CurrentMana >= 1) score += 10f;

        return score;
    }

    private static bool HasDeckCard(EnemyInstance enemy, CardType type)
    {
        if (enemy.Definition == null)
        {
            return false;
        }

        foreach (var card in enemy.Definition.StartingDeck.Cards)
        {
            if (card == type)
            {
                return true;
            }
        }

        return false;
    }

    private Card PickDefinitionAction(
        Player enemy,
        Player opponent,
        EnemyDefinition definition,
        IReadOnlyList<EnemyInstance>? allies = null)
    {
        var effectiveWeights = BuildEffectiveWeights(enemy, opponent, definition, null, allies);
        var choices = new List<WeightedCard>();
        var availableActions = GetAvailableActions(enemy, definition);
        foreach (var card in availableActions)
        {
            AddConfiguredChoice(choices, enemy, opponent, definition, card, effectiveWeights.GetWeight(card.Type));
        }

        return PickFromAvailableActions(choices, availableActions);
    }

    private EnemyActionWeights BuildEffectiveWeights(
        Player enemy,
        Player opponent,
        EnemyDefinition definition,
        EnemyAiEvaluation? evaluation,
        IReadOnlyList<EnemyInstance>? allies = null)
    {
        var weights = new EnemyActionWeights
        {
            Slash = definition.ActionWeights.Slash,
            FireSlash = definition.ActionWeights.FireSlash,
            ThunderSlash = definition.ActionWeights.ThunderSlash,
            FireThunderSlash = definition.ActionWeights.FireThunderSlash,
            CelestialImpact = definition.ActionWeights.CelestialImpact,
            ArrowBarrage = definition.ActionWeights.ArrowBarrage,
            NanmanInvasion = definition.ActionWeights.NanmanInvasion,
            Dodge = definition.ActionWeights.Dodge,
            Peach = definition.ActionWeights.Peach,
            Wuxie = definition.ActionWeights.Wuxie,
            Resource = definition.ActionWeights.Resource,
            ShunShou = definition.ActionWeights.ShunShou,
            SureSlash = definition.ActionWeights.SureSlash,
            Wine = definition.ActionWeights.Wine,
            Guanxing = definition.ActionWeights.Guanxing,
            IceSlash = definition.ActionWeights.IceSlash
        };

        foreach (var rule in definition.AiRules)
        {
            if (!MatchesRule(enemy, opponent, rule))
            {
                continue;
            }

            evaluation?.AppliedRuleLines.Add(DescribeRule(rule));

            foreach (var modifier in rule.Modifiers)
            {
                weights.AddWeight(modifier.WeightType, modifier.Delta);
                evaluation?.ModifierLines.Add($"{GetWeightTypeName(modifier.WeightType)} {(modifier.Delta >= 0 ? "+" : string.Empty)}{modifier.Delta:0.#}");
            }
        }

        // 闪现在可在整回合内抵消所有杀与火杀，因此当对手仍有攻击费用时，适当提高其评估价值。
        if (opponent.CurrentMana >= 1)
        {
            weights.Dodge += 12;
            evaluation?.ModifierLines.Add("闪 +12（对手仍可发起攻击）");
        }
        if (opponent.CurrentMana >= 2)
        {
            weights.Dodge += 8;
            evaluation?.ModifierLines.Add("闪 +8（对手高费用威胁）");
        }

        ApplyLowThreatDodgeAdjustment(weights, opponent, evaluation);

        // 无懈可击现在同样是整回合防御状态，面对高费用对手时应适当提高评估。
        if (opponent.CurrentMana >= 1.5)
        {
            weights.Wuxie += 10;
            evaluation?.ModifierLines.Add("无懈可击 +10（对手高费用威胁）");
        }
        if (opponent.GetStealableMana() >= 1)
        {
            weights.Wuxie += 8;
            evaluation?.ModifierLines.Add("无懈可击 +8（可防顺手牵羊）");
        }

        // 击鼓状态：不使用防御类牌（闪/桃/无懈），偏向雷杀。
        if (enemy.JiGuActive)
        {
            weights.Dodge = 0;
            weights.Peach = 0;
            weights.Wuxie = 0;
            weights.ThunderSlash += 50;
            evaluation?.ModifierLines.Add("击鼓：防御类归零，雷杀 +50");
        }

        // 医者强化状态：专用必中杀，停止回血。
        if (enemy.HasSkill(SkillIds.BossHealerPassive)
            && enemy.RuntimeStates.TryGetValue("healer_enhanced", out var healerEnhanced)
            && healerEnhanced is true)
        {
            weights.Slash = 0;
            weights.FireSlash = 0;
            weights.ThunderSlash = 0;
            weights.Peach = 0;
            weights.SureSlash += 100;
            evaluation?.ModifierLines.Add("医者强化：必中杀 +100，普通/火/雷杀归零，停止回血");
        }

        // 背叛者连击状态：全面提权攻击，偏向火/雷。
        if (enemy.HasSkill(SkillIds.BossTraitorCombo)
            && enemy.RuntimeStates.TryGetValue("traitor_combo_active", out var traitorCombo)
            && traitorCombo is true)
        {
            weights.Slash += 30;
            weights.FireSlash += 50;
            weights.ThunderSlash += 50;
            evaluation?.ModifierLines.Add("背叛者连击：杀+30，火/雷杀+50");
        }

        // 对手冰冻：大幅提升攻击权重，趁冻发力。
        if (opponent.IsFrozen)
        {
            weights.IceSlash += 30;
            weights.Slash += 20;
            weights.FireSlash += 20;
            weights.ThunderSlash += 20;
            weights.SureSlash += 20;
            weights.Dodge = System.Math.Max(0, weights.Dodge - 20);
            evaluation?.ModifierLines.Add("对手冰冻：攻击权重大幅提升，防御降低");
        }

        // 队友输出潜力评估：如有高爆发队友（如喷气式狼牙棒激活），显著提升酒的权重。
        // 通用逻辑：不绑定具体敌人ID，依据装备/卡组/费用综合评分。
        if (allies != null && weights.Wine > 0)
        {
            var bestAllyScore = 0f;
            foreach (var ally in allies)
            {
                if (ally.IsDead || ally == enemy)
                {
                    continue;
                }

                var score = EstimateAllyOutputScore(ally);
                if (score > bestAllyScore)
                {
                    bestAllyScore = score;
                }
            }

            if (bestAllyScore >= 60f)
            {
                weights.Wine += 100;
                evaluation?.ModifierLines.Add("酒 +100（队友拥有高爆发输出潜力）");
            }
            else if (bestAllyScore >= 30f)
            {
                weights.Wine += 40;
                evaluation?.ModifierLines.Add("酒 +40（队友拥有较高输出潜力）");
            }
        }

        ApplyImperialGunnerAlternation(enemy, definition, weights, evaluation, allies);

        return weights;
    }

    private static void ApplyImperialGunnerAlternation(
        Player enemy,
        EnemyDefinition definition,
        EnemyActionWeights weights,
        EnemyAiEvaluation? evaluation,
        IReadOnlyList<EnemyInstance>? allies)
    {
        if (definition.Id != "gunner" || allies == null)
        {
            return;
        }

        var gunners = new List<EnemyInstance>();
        foreach (var ally in allies)
        {
            if (!ally.IsDead && ally.Definition.Id == "gunner")
            {
                gunners.Add(ally);
            }
        }

        if (gunners.Count < 2)
        {
            return;
        }

        var gunnerIndex = gunners.IndexOf((EnemyInstance)enemy);
        if (gunnerIndex < 0)
        {
            return;
        }

        var turn = 1;
        if (enemy is EnemyInstance enemyInstance
            && enemyInstance.RuntimeStates.TryGetValue("current_turn", out var turnValue)
            && turnValue is int currentTurn
            && currentTurn > 0)
        {
            turn = currentTurn;
        }

        var firstGunnerPrefersFee = turn % 2 == 1;
        var preferFee = gunnerIndex == 0 ? firstGunnerPrefersFee : !firstGunnerPrefersFee;

        if (preferFee)
        {
            weights.Resource += 120;
            weights.Slash = System.Math.Max(0, weights.Slash - 30);
            weights.FireSlash = System.Math.Max(0, weights.FireSlash - 20);
            weights.ThunderSlash = System.Math.Max(0, weights.ThunderSlash - 20);
            evaluation?.ModifierLines.Add($"枪手轮换：第{turn}回合，本单位优先出费");
        }
        else
        {
            weights.Slash += 120;
            weights.Resource = System.Math.Max(0, weights.Resource - 30);
            evaluation?.ModifierLines.Add($"枪手轮换：第{turn}回合，本单位优先出杀");
        }
    }

    private EnemyAiEvaluation BuildFallbackEvaluation(Player enemy, Player opponent)
    {
        var evaluation = new EnemyAiEvaluation(new EnemyActionWeights());
        var fallbackWeights = evaluation.BaseWeights;

        if (enemy.CurrentMana <= 0)
        {
            fallbackWeights.Resource = 80;
            fallbackWeights.Dodge = 15;
            fallbackWeights.Wuxie = 5;
        }
        else if (enemy.CurrentMana < 1)
        {
            fallbackWeights.Resource = 55;
            fallbackWeights.Dodge = 25;
            fallbackWeights.Wuxie = 20;
        }
        else if (enemy.CurrentMana < 2)
        {
            fallbackWeights.Slash = 24;
            fallbackWeights.Dodge = opponent.CurrentMana >= 1 ? 28 : 18;
            fallbackWeights.Resource = 24;
            fallbackWeights.Peach = enemy.Health <= enemy.MaxHealth / 2 ? 28 : 8;
            fallbackWeights.ShunShou = opponent.GetStealableMana() >= 2 ? 26 : 16;
            fallbackWeights.Wuxie = 10;
        }
        else
        {
            fallbackWeights.Slash = 18;
            fallbackWeights.FireSlash = 16;
            fallbackWeights.ThunderSlash = 16;
            fallbackWeights.Dodge = opponent.CurrentMana >= 1 ? 22 : 10;
            fallbackWeights.Peach = enemy.Health <= enemy.MaxHealth / 2 ? 30 : 6;
            fallbackWeights.Resource = 12;
            fallbackWeights.ShunShou = opponent.GetStealableMana() >= 3 ? 26 : 10;
            fallbackWeights.Wuxie = 8;
        }

        ApplyLowThreatDodgeAdjustment(fallbackWeights, opponent, evaluation);
        evaluation.FinalWeights = fallbackWeights.Clone();
        return evaluation;
    }

    private bool MatchesRule(Player enemy, Player opponent, AiRule rule)
    {
        foreach (var condition in rule.Conditions)
        {
            var matched = condition.Type switch
            {
                AiConditionType.PlayerResourceAtLeast => opponent.GetStealableMana() >= condition.Value,
                AiConditionType.SelfResourceAtLeast => enemy.CurrentMana >= condition.Value,
                AiConditionType.PlayerResourceBelow => opponent.GetStealableMana() < condition.Value,
                AiConditionType.SelfResourceBelow => enemy.CurrentMana < condition.Value,
                AiConditionType.PlayerHpBelow => opponent.Health < condition.Value,
                AiConditionType.SelfHpBelow => enemy.Health < condition.Value,
                AiConditionType.SelfLastActionKill => WasLastActionKill(enemy),
                AiConditionType.SelfLastActionAnySha => WasLastActionAnySha(enemy),
                AiConditionType.SelfLastActionWine => WasLastActionWine(enemy),
                AiConditionType.SelfLastActionGuanxing => WasLastActionGuanxing(enemy),
                AiConditionType.SelfLastActionCelestialImpact => WasLastActionCelestialImpact(enemy),
                AiConditionType.SelfExoskeletonPhaseTwo =>
                    enemy is EnemyInstance ei2
                    && ei2.HasEquipment(EquipmentIds.ClampExoskeleton)
                    && ei2.RuntimeStates.TryGetValue("clamp_exo_active", out var exoActiveVal)
                    && exoActiveVal is false,
                AiConditionType.SelfGuanxingPhaseNone =>
                    enemy is EnemyInstance guanxingEi && guanxingEi.GuanxingPhase == GuanxingPhase.None,
                AiConditionType.SelfWineLayersAtLeast =>
                    (enemy.WinePower + enemy.PendingWinePower) >= condition.Value,
                AiConditionType.SelfLastActionSteal =>
                    _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastStealAct) && lastStealAct == CardType.Steal,
                AiConditionType.PlayerIsFrozen => opponent.IsFrozen,
                _ => false
            };

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private void AddConfiguredChoice(
        List<WeightedCard> choices,
        Player enemy,
        Player opponent,
        EnemyDefinition definition,
        Card card,
        float weight)
    {
        // 张辽的角色专属牌不要求 EnemyDefinition 重复声明一套权重。
        // 目标持有可窃取费用时提高突袭优先级，仍保留普通加权随机行为。
        if (card.Type == CardType.Tuxi && enemy.HasSkill(SkillIds.Tuxi))
        {
            weight = Math.Max(weight, opponent.GetStealableMana() > 0 ? 34f : 18f);
        }

        if (weight <= 0 || !CanPlayActionFromPool(enemy, card))
        {
            return;
        }

        AddChoice(choices, enemy, card, weight);
    }

    private static bool HasCard(EnemyDefinition definition, CardType type)
    {
        foreach (var card in definition.StartingDeck.Cards)
        {
            if (card == type)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanPlayActionFromPool(Player enemy, Card card)
    {
        // 冰冻：只能出费。
        if (enemy.IsFrozen && card.Type != CardType.Fee)
        {
            return false;
        }

        // 侦测者【警戒状态】：只能出费。
        if (enemy is EnemyInstance scoutPoolEnemy && IsScoutVigilant(scoutPoolEnemy) && card.Type != CardType.Fee)
        {
            return false;
        }

        // 连营免费杀：下一回合第一张普通杀可在0费时使用。
        if (card.Type == CardType.Kill && enemy.LianyingFreeKillAvailable)
        {
            return true;
        }

        // 观星：已处于观星阶段时禁止再次使用。
        if (card.Type == CardType.Guanxing && enemy.GuanxingPhase != GuanxingPhase.None)
        {
            return false;
        }

        // 使用实际费用（考虑蛮族等技能的费用折减），而非卡牌静态费用。
        var effectiveCost = BattleRules.GetActionCost(enemy, BattleAction.FromCard(card, 1));
        return BattleRules.CanAffordActionCost(enemy, effectiveCost);
    }

    private Card PickFallbackAction(Player enemy, Player opponent)
    {
        var availableActions = GetAvailableActions(enemy, null);
        var stateKey = GetStateKey(enemy);
        if (enemy.CurrentMana <= 0
            && _lastActionByEnemyId.TryGetValue(stateKey, out var lastAction)
            && lastAction == CardType.Dodge
            && _sameActionStreakByEnemyId.TryGetValue(stateKey, out var dodgeStreak)
            && dodgeStreak >= 2)
        {
            foreach (var available in availableActions)
            {
                if (available.Type == CardType.Fee)
                {
                    return available;
                }
            }
        }

        var choices = new List<WeightedCard>();
        foreach (var available in availableActions)
        {
            var weight = GetFallbackWeight(enemy, opponent, available.Type);
            AddChoice(choices, enemy, available, weight);
        }

        return PickFromAvailableActions(choices, availableActions);
    }

    private static float GetFallbackWeight(Player enemy, Player opponent, CardType type)
    {
        float weight;
        if (enemy.CurrentMana <= 0)
        {
            weight = type switch
            {
                CardType.Fee => 80,
                CardType.Dodge => 15,
                CardType.Unassailable => 5,
                _ => 0
            };
            return type == CardType.Dodge ? AdjustLowThreatDodgeWeight(weight, opponent) : weight;
        }

        if (enemy.CurrentMana < 1)
        {
            weight = type switch
            {
                CardType.Fee => 55,
                CardType.Dodge => 25,
                CardType.Unassailable => 20,
                _ => 0
            };
            return type == CardType.Dodge ? AdjustLowThreatDodgeWeight(weight, opponent) : weight;
        }

        if (enemy.CurrentMana < 2)
        {
            weight = type switch
            {
                CardType.Kill => 24,
                CardType.Dodge => opponent.CurrentMana >= 1 ? 28 : 18,
                CardType.Fee => 24,
                CardType.Peach => enemy.Health <= enemy.MaxHealth / 2 ? 28 : 8,
                CardType.Steal => opponent.GetStealableMana() >= 2 ? 26 : 16,
                CardType.Unassailable => 10,
                _ => 0
            };
            return type == CardType.Dodge ? AdjustLowThreatDodgeWeight(weight, opponent) : weight;
        }

        weight = type switch
        {
            CardType.Kill => 18,
            CardType.FireKill => 16,
            CardType.ThunderKill => 16,
            CardType.Tuxi => opponent.GetStealableMana() > 0 ? 34 : 18,
            CardType.Dodge => opponent.CurrentMana >= 1 ? 22 : 10,
            CardType.Peach => enemy.Health <= enemy.MaxHealth / 2 ? 30 : 6,
            CardType.Fee => 12,
            CardType.Steal => opponent.GetStealableMana() >= 3 ? 26 : 10,
            CardType.Unassailable => 8,
            _ => 0
        };
        return type == CardType.Dodge ? AdjustLowThreatDodgeWeight(weight, opponent) : weight;
    }

    private static void ApplyLowThreatDodgeAdjustment(
        EnemyActionWeights weights,
        Player opponent,
        EnemyAiEvaluation? evaluation)
    {
        var adjusted = AdjustLowThreatDodgeWeight(weights.Dodge, opponent);
        if (adjusted >= weights.Dodge)
        {
            return;
        }

        weights.Dodge = adjusted;
        evaluation?.ModifierLines.Add("闪 ×0.3（对手当前费用 <= 0）");
    }

    private static float AdjustLowThreatDodgeWeight(float originalWeight, Player opponent)
    {
        if (opponent.CurrentMana > 0 || originalWeight <= 0)
        {
            return originalWeight;
        }

        return Math.Max(1f, originalWeight * 0.3f);
    }

    private void AddChoice(List<WeightedCard> choices, Player enemy, Card card, float weight)
    {
        if (weight <= 0)
        {
            return;
        }

        var adjustedWeight = weight;
        var stateKey = GetStateKey(enemy);
        if (_lastActionByEnemyId.TryGetValue(stateKey, out var lastAction) && lastAction == card.Type)
        {
            var streak = _sameActionStreakByEnemyId.TryGetValue(stateKey, out var sameActionStreak)
                ? sameActionStreak
                : 0;
            adjustedWeight = streak >= 2 ? Math.Max(1, adjustedWeight / 4f) : Math.Max(1, adjustedWeight / 2f);
        }

        choices.Add(new WeightedCard(card, adjustedWeight));
    }

    private Card Pick(List<WeightedCard> choices)
    {
        if (choices.Count == 0)
        {
            return Card.Fee();
        }

        var totalWeight = 0f;
        foreach (var choice in choices)
        {
            totalWeight += choice.Weight;
        }

        var roll = (float)_random.NextDouble() * totalWeight;
        foreach (var choice in choices)
        {
            roll -= choice.Weight;
            if (roll <= 0)
            {
                return choice.Card;
            }
        }

        return choices[^1].Card;
    }

    private Card PickFromAvailableActions(List<WeightedCard> choices, List<Card> availableActions)
    {
        if (choices.Count > 0)
        {
            return Pick(choices);
        }

        if (availableActions.Count > 0)
        {
            return availableActions[0];
        }

        return Card.Fee();
    }

    private void RecordSelection(Player enemy, CardType type)
    {
        var stateKey = GetStateKey(enemy);
        if (_lastActionByEnemyId.TryGetValue(stateKey, out var lastAction) && lastAction == type)
        {
            _sameActionStreakByEnemyId[stateKey] = _sameActionStreakByEnemyId.TryGetValue(stateKey, out var currentStreak)
                ? currentStreak + 1
                : 2;
        }
        else
        {
            _lastActionByEnemyId[stateKey] = type;
            _sameActionStreakByEnemyId[stateKey] = 1;
        }
    }

    private bool WasLastActionKill(Player enemy)
    {
        return _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastAction)
            && lastAction == CardType.Kill;
    }

    // 上一回合使用过任意杀系牌（普通杀/火杀/雷杀/火雷杀/必中杀）即满足。
    private bool WasLastActionAnySha(Player enemy)
    {
        return _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastAction)
            && lastAction is CardType.Kill or CardType.FireKill or CardType.ThunderKill
                or CardType.FireThunderKill or CardType.SureKill;
    }

    private bool WasLastActionWine(Player enemy)
    {
        return _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastAction)
            && lastAction == CardType.Wine;
    }

    private bool WasLastActionGuanxing(Player enemy)
    {
        return _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastAction)
            && lastAction == CardType.Guanxing;
    }

    private bool WasLastActionCelestialImpact(Player enemy)
    {
        return _lastActionByEnemyId.TryGetValue(GetStateKey(enemy), out var lastAction)
            && lastAction == CardType.CelestialImpact;
    }

    private static string GetStateKey(Player enemy)
    {
        // 同一战场允许出现多个相同 EnemyDefinition。AI 历史若使用模板 Id，
        // 两个流亡蛮族会共享“上一张牌”和连续次数，表现为彼此出招状态串联。
        return BattleContext.GetUnitStateKey(enemy);
    }

    private static string DescribeRule(AiRule rule)
    {
        var conditions = new List<string>();
        foreach (var condition in rule.Conditions)
        {
            conditions.Add(condition.Type switch
            {
                AiConditionType.PlayerResourceAtLeast => $"玩家费用 >= {condition.Value:0.#}",
                AiConditionType.SelfResourceAtLeast => $"自身费用 >= {condition.Value:0.#}",
                AiConditionType.PlayerResourceBelow => $"玩家费用 < {condition.Value:0.#}",
                AiConditionType.SelfResourceBelow => $"自身费用 < {condition.Value:0.#}",
                AiConditionType.PlayerHpBelow => $"玩家生命 < {condition.Value:0.#}",
                AiConditionType.SelfHpBelow => $"自身生命 < {condition.Value:0.#}",
                AiConditionType.SelfLastActionKill => "上一回合成功出杀（普通杀）",
                AiConditionType.SelfLastActionAnySha => "上一回合使用过任意杀系牌",
                AiConditionType.SelfLastActionWine => "上一回合使用酒",
                AiConditionType.SelfLastActionGuanxing => "上一回合使用观星",
                AiConditionType.SelfLastActionCelestialImpact => "上一回合使用天体撞击",
                AiConditionType.SelfExoskeletonPhaseTwo => "钳制机械外骨骼已触发（第二阶段）",
                AiConditionType.SelfGuanxingPhaseNone => "当前未处于观星阶段",
                AiConditionType.SelfWineLayersAtLeast => $"自身酒层 >= {condition.Value:0.#}",
                AiConditionType.SelfLastActionSteal => "上一回合使用顺手牵羊",
                AiConditionType.PlayerIsFrozen => "对手处于冰冻状态",
                _ => condition.Type.ToString()
            });
        }

        return string.Join("，", conditions);
    }

    private static string GetWeightTypeName(EnemyActionWeightType type)
    {
        return type switch
        {
            EnemyActionWeightType.Slash => "杀",
            EnemyActionWeightType.FireSlash => "火杀",
            EnemyActionWeightType.ThunderSlash => "雷杀",
            EnemyActionWeightType.FireThunderSlash => "火雷杀",
            EnemyActionWeightType.CelestialImpact => "天体撞击",
            EnemyActionWeightType.ArrowBarrage => "万箭齐发",
            EnemyActionWeightType.NanmanInvasion => "南蛮入侵",
            EnemyActionWeightType.Dodge => "闪",
            EnemyActionWeightType.Peach => "桃",
            EnemyActionWeightType.Wuxie => "无懈可击",
            EnemyActionWeightType.Resource => "费",
            EnemyActionWeightType.ShunShou => "顺手牵羊",
            EnemyActionWeightType.SureSlash => "必中杀",
            EnemyActionWeightType.Wine => "酒",
            EnemyActionWeightType.Guanxing => "观星",
            EnemyActionWeightType.IceSlash => "冰杀",
            _ => type.ToString()
        };
    }

    // 观星录制阶段专用：从牌库中选择最优攻击牌作为录制动作。
    // 优先级：火雷杀 > 雷杀 > 火杀 > 杀 > 万箭齐发 > 南蛮入侵；
    // 只要仍有费用且存在其它可用动作，绝不以【费】作为录制牌。
    /// <summary>
    /// Enemy System 的公开入口：SelectGuanxingRecordCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Card SelectGuanxingRecordCard(EnemyInstance enemy, EnemyDefinition? definition)
    {
        if (definition == null)
        {
            return Card.Fee();
        }

        var availableTypes = new HashSet<CardType>();
        foreach (var card in GetAvailableActions(enemy, definition))
        {
            availableTypes.Add(card.Type);
        }

        var attackPriority = new[]
        {
            CardType.FireThunderKill,
            CardType.ThunderKill,
            CardType.FireKill,
            CardType.Kill,
            CardType.ArrowBarrage,
            CardType.NanmanInvasion
        };

        foreach (var type in attackPriority)
        {
            var card = new Card(type);
            if (availableTypes.Contains(type) && CanPlayActionFromPool(enemy, card))
            {
                return card;
            }
        }

        foreach (var card in GetAvailableActions(enemy, definition))
        {
            // 观星后的记录牌会被免费重复。费用尚存时优先记录任意非【费】动作，
            // 防止异常状态或未来卡组改动令卧龙集智体把【费】锁入观星循环。
            if (enemy.CurrentMana > 0 && card.Type == CardType.Fee)
            {
                continue;
            }

            return card;
        }

        return Card.Fee();
    }

    private readonly struct WeightedCard
    {
        /// <summary>
        /// Enemy System 的公开入口：WeightedCard。
        ///
        /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
        /// </summary>
        public WeightedCard(Card card, float weight)
        {
            Card = card;
            Weight = weight;
        }

        public Card Card { get; }
        public float Weight { get; }
    }
}

/// <summary>
/// Enemy System 的公开类：EnemyAiEvaluation。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EnemyAiEvaluation
{
    /// <summary>
    /// Enemy System 的公开入口：EnemyAiEvaluation。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyAiEvaluation(EnemyActionWeights baseWeights)
    {
        BaseWeights = baseWeights;
        FinalWeights = baseWeights.Clone();
    }

    public EnemyActionWeights BaseWeights { get; }
    public EnemyActionWeights FinalWeights { get; set; }
    public List<string> AppliedRuleLines { get; } = new();
    public List<string> ModifierLines { get; } = new();
}
