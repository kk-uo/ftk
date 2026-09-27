//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CombatFeedAndDamagePopupRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证伤害结果按实际目标保留，死亡后仍可找到对应表现锚点。
// 2. 验证局内 Combat Feed 固定分区、条数上限和调试信息隔离。
//
// 不负责：
// × 验证飘字字体与动画像素效果。
// × 验证伤害计算规则。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 敌方飘字目标与局内精简战报的 Headless 回归入口。
/// </summary>
public partial class CombatFeedAndDamagePopupRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行目标伤害汇总和 Combat Feed 投影测试。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDamageIsKeptPerTarget();
            TestUnitSpecificRoundResults();
            TestAttackAgainstEmptyStealSettlement();
            TestPlayerAndEnemyDamageAppearInFeed();
            TestCombatFeedProjection();
            GD.Print($"COMBAT_FEED_DAMAGE_POPUP_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"COMBAT_FEED_DAMAGE_POPUP_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDamageIsKeptPerTarget()
    {
        var result = new RoundResult();
        var first = new Player("酒徒", "enemy_a", BattleTeam.Enemy);
        var second = new Player("盾卫", "enemy_b", BattleTeam.Enemy);

        result.AddDamage(first, 10);
        result.AddDamage(first, 5);
        result.AddDamage(second, 7);

        Assert(result.DamageByTarget.Count == 2, "伤害目标被错误合并为单个敌人");
        Assert(result.DamageByTarget[first] == 15, "同一敌人的多段伤害没有汇总");
        Assert(result.DamageByTarget[second] == 7, "第二个敌人的伤害丢失");

        var player = new Player("孙策", CharacterIds.SunCe, BattleTeam.Player);
        result.AddDamage(player, 15);
        Assert(result.PlayerDamage == 15, "具体角色名导致玩家伤害被错误累计为敌方伤害");
        Assert(result.EnemyDamage == 22, "按阵营记录玩家伤害时破坏了敌方伤害合计");
    }

    private void TestUnitSpecificRoundResults()
    {
        var left = new EnemyInstance(new EnemyDefinition
        {
            Id = "feed_left",
            Name = "左敌人",
            MaxHP = 20,
            StartingDeck = new EnemyDeck()
        });
        var right = new EnemyInstance(new EnemyDefinition
        {
            Id = "feed_right",
            Name = "右敌人",
            MaxHP = 20,
            StartingDeck = new EnemyDeck()
        });
        var result = new RoundResult();

        result.AddResourceGain(left, 1);
        result.AddResourceGain(right, 2);
        result.AddHeal(right, 5);
        result.AddWineStatus(left, 1);

        Assert(result.ManaGainByUnit[left] == 1 && result.ManaGainByUnit[right] == 2,
            "多敌人费用获得仍被合并为同一个敌方结果");
        Assert(result.EnemyManaGain == 3, "按单位记录费用后旧的敌方合计没有同步");
        Assert(result.HealByUnit[right] == 5, "敌方治疗没有保留具体单位");
        Assert(result.WineGainByUnit[left] == 1, "敌方酒Buff没有保留具体单位");
    }

    private void TestPlayerAndEnemyDamageAppearInFeed()
    {
        var service = new BattleLogService();
        service.BeginRun();
        service.BeginBattle("damage_feed");
        service.RecordStructured(
            BattleLogEventKind.Damage,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.damage",
            new[] { "拾荒者", "10" },
            actor: "孙策",
            target: "拾荒者",
            card: "杀",
            damageAfter: 10);
        service.RecordStructured(
            BattleLogEventKind.Damage,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.damage",
            new[] { "孙策", "15" },
            actor: "废弃机仆",
            target: "孙策",
            card: "杀",
            damageAfter: 15);

        var sections = CombatFeedFormatter.BuildRound(service.CurrentBattle!.Entries, 6, "孙策");
        var damageItems = sections
            .Single(section => section.TitleKey == "combat_feed.damage")
            .Items;
        Assert(damageItems.Contains("拾荒者：-10HP"), "伤害分区遗漏了敌方受到的伤害");
        Assert(damageItems.Contains("孙策：-15HP"), "伤害分区遗漏了我方受到的伤害");
    }

    private void TestAttackAgainstEmptyStealSettlement()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "combat_feed_steal_enemy",
            Name = "拾荒者",
            MaxHP = 30,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetMana(1);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            PlayerLockedTarget = enemy,
            TurnNumber = 1,
            PlayerAction = BattleAction.FromCard(Card.Kill(), 1, enemy)
        };
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Steal(), 1, player));
        context.BeginRoundResult();

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(context.RoundResult.Relations.Contains("杀克制顺手牵羊"),
            "杀与顺手牵羊没有生成克制关系战报");
        Assert(enemy.MaxHealth - enemy.Health == BattleConstants.KillDamage,
            "杀对顺手牵羊没有造成正常伤害");
        Assert(context.RoundResult.StealResolutions.Count == 1
            && context.RoundResult.StealResolutions[0].Resolved
            && context.RoundResult.StealResolutions[0].Amount == 0,
            "顺手牵羊面对0费目标没有记录“生效，偷走0费”");
    }

    private void TestCombatFeedProjection()
    {
        var service = new BattleLogService();
        service.BeginRun();
        service.BeginBattle("combat_feed_regression");

        service.RecordStructured(
            BattleLogEventKind.PlayerAction,
            6,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.player_use_card",
            new[] { "火杀" },
            actor: "玩家",
            target: "酒徒",
            card: "火杀");
        service.RecordStructured(
            BattleLogEventKind.EnemyAction,
            6,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.enemy_use_card",
            new[] { "酒徒", "酒" },
            actor: "酒徒",
            target: "玩家",
            source: "enemy:left",
            card: "酒");
        service.RecordStructured(
            BattleLogEventKind.EnemyAction,
            6,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.enemy_use_card",
            new[] { "盾卫", "杀" },
            actor: "盾卫",
            target: "玩家",
            source: "enemy:right",
            card: "杀");
        service.RecordStructured(
            BattleLogEventKind.Counter,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.raw",
            new[] { "杀克制顺手牵羊" },
            result: "杀克制顺手牵羊");
        service.RecordStructured(
            BattleLogEventKind.CardResolution,
            6,
            BattlePhase.BattlePhase.ToString(),
            "combat_feed.steal_resolved",
            new[] { "顺手牵羊", "0" },
            actor: "酒徒",
            target: "赵云",
            card: "顺手牵羊",
            energyAfter: 0,
            result: "Resolved");
        service.RecordStructured(
            BattleLogEventKind.Skill,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.skill_activated",
            new[] { "无双" },
            skill: "无双");
        service.RecordStructured(
            BattleLogEventKind.Damage,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.damage",
            new[] { "酒徒", "20" },
            target: "酒徒",
            card: "杀",
            damageAfter: 20);
        service.RecordStructured(
            BattleLogEventKind.Energy,
            6,
            BattlePhase.EndPhase.ToString(),
            "battlelog.mana_gain",
            new[] { "赵云", "1" },
            actor: "赵云",
            energyAfter: 1);
        service.RecordStructured(
            BattleLogEventKind.Energy,
            6,
            BattlePhase.EndPhase.ToString(),
            "battlelog.mana_gain",
            new[] { "盾卫", "1" },
            actor: "盾卫",
            source: "enemy:right",
            energyAfter: 1);
        service.RecordStructured(
            BattleLogEventKind.EffectQueue,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.debug_line",
            new[] { "TargetBinding Priority=Highest" },
            debugOnly: true,
            source: "TargetBindingEffect");
        service.RecordStructured(
            BattleLogEventKind.Status,
            6,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.raw",
            new[] { "DamageModifierPipeline internal class" });

        var sections = CombatFeedFormatter.BuildRound(
            service.CurrentBattle!.Entries,
            6,
            "赵云");
        var items = sections.SelectMany(section => section.Items).ToList();

        Assert(sections.Select(section => section.TitleKey).SequenceEqual(new[]
        {
            "combat_feed.player_actions",
            "combat_feed.enemy_actions",
            "combat_feed.counter_results",
            "combat_feed.skills",
            "combat_feed.damage",
            "combat_feed.round_end"
        }), "Combat Feed 分区顺序不正确");
        Assert(items.Count <= CombatFeedFormatter.MaxItemsPerRound, "单回合 Combat Feed 超过条数限制");
        Assert(items.Any(item => item == "赵云：火杀"), "我方行动没有使用当前角色名");
        Assert(items.Any(item => item == "左·酒徒：酒") && items.Any(item => item == "右·盾卫：杀"),
            "多个敌人的行动没有按左/右位置独立显示");
        Assert(items.Any(item => item == "杀克制顺手牵羊"), "克制关系没有显示在左侧战报");
        Assert(items.Any(item => item == "杀对酒徒造成20点伤害"), "卡牌伤害结算没有显示在克制结果");
        Assert(items.Any(item => item == "顺手牵羊生效，偷走0费"), "0费顺手牵羊结果没有显示");
        Assert(items.IndexOf("杀克制顺手牵羊") < items.IndexOf("杀对酒徒造成20点伤害")
            && items.IndexOf("杀对酒徒造成20点伤害") < items.IndexOf("顺手牵羊生效，偷走0费"),
            "克制结算没有按关系、伤害、附加效果排序");
        Assert(items.Any(item => item == "酒徒：-20HP"), "结构化伤害没有生成精简伤害行");
        var roundEnd = sections.Single(section => section.TitleKey == "combat_feed.round_end");
        Assert(roundEnd.Items.SequenceEqual(new[] { "赵云获得1费。", "右·盾卫获得1费。" }),
            "回合结束结果没有显示具体敌人及其战场位置");
        Assert(items.All(item => !item.Contains("TargetBinding", StringComparison.Ordinal)
            && !item.Contains("Priority", StringComparison.Ordinal)
            && !item.Contains("DamageModifierPipeline", StringComparison.Ordinal)),
            "Combat Feed 泄漏了开发调试信息");

        service.RecordStructured(
            BattleLogEventKind.EnemyAction,
            7,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.enemy_use_card",
            new[] { "枪手", "雷杀" },
            actor: "枪手",
            target: "玩家",
            card: "雷杀");
        var singleEnemyItems = CombatFeedFormatter.BuildRound(
                service.CurrentBattle.Entries,
                7,
                "赵云")
            .SelectMany(section => section.Items)
            .ToList();
        Assert(singleEnemyItems.Contains("中·枪手：雷杀"), "单个敌人没有标记为中间位置");

        foreach (var enemyAction in new[]
                 {
                     (Name: "酒徒", Card: "酒"),
                     (Name: "盾卫", Card: "杀"),
                     (Name: "枪手", Card: "雷杀")
                 })
        {
            service.RecordStructured(
                BattleLogEventKind.EnemyAction,
                8,
                BattlePhase.BattlePrePhase.ToString(),
                "battlelog.enemy_use_card",
                new[] { enemyAction.Name, enemyAction.Card },
                actor: enemyAction.Name,
                target: "玩家",
                card: enemyAction.Card);
        }

        var threeEnemyItems = CombatFeedFormatter.BuildRound(
                service.CurrentBattle.Entries,
                8,
                "赵云")
            .SelectMany(section => section.Items)
            .ToList();
        Assert(threeEnemyItems.Contains("左·酒徒：酒")
            && threeEnemyItems.Contains("中·盾卫：杀")
            && threeEnemyItems.Contains("右·枪手：雷杀"),
            "三个敌人没有按左/中/右位置标记");
    }

    private void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        _assertionCount += 1;
    }
}
