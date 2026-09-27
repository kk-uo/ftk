//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/BattleLogArchitectureRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 Combat Feed、Battle History、Developer Report 共用事件源。
// 2. 验证 Run/Battle/Round/Phase 报告在跨战斗时保留。
// 3. 验证 Trigger、伤害、技能、装备、Buff、AI 和多敌人字段可查询导出。
//
// 不负责：
// × 验证日志窗口的像素布局。
// × 验证具体卡牌规则。
//
// 主要依赖：
// BattleLogService
// TriggerManager
// ApplyDamageEffect
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;
using System.Text.Json;

/// <summary>
/// 统一战斗日志架构的 Headless 回归入口。
/// </summary>
public partial class BattleLogArchitectureRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行统一数据源、报告层级、过滤和导出测试。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestUnifiedProjectionsAndStructuredEvents();
            GD.Print($"BATTLE_LOG_ARCHITECTURE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BATTLE_LOG_ARCHITECTURE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestUnifiedProjectionsAndStructuredEvents()
    {
        var service = new BattleLogService();
        service.BeginRun();
        var firstBattle = service.BeginBattle("chapter2_three_enemies");

        var playerAction = service.RecordStructured(
            BattleLogEventKind.PlayerAction,
            1,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.player_use_card",
            new[] { "火杀" },
            actor: "玩家",
            target: "酒徒",
            card: "火杀");
        service.RecordStructured(
            BattleLogEventKind.EnemyAction,
            1,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.enemy_use_card",
            new[] { "酒徒", "酒" },
            actor: "酒徒",
            target: "玩家",
            card: "酒");
        service.RecordStructured(
            BattleLogEventKind.EnemyAction,
            1,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.enemy_use_card",
            new[] { "盾卫", "杀" },
            actor: "盾卫",
            target: "玩家",
            card: "杀");
        service.RecordStructured(
            BattleLogEventKind.Skill,
            1,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.skill_activated",
            new[] { "无双" },
            skill: "无双");
        service.RecordStructured(
            BattleLogEventKind.Equipment,
            1,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.debug_line",
            new[] { "排箫触发" },
            equipment: "排箫");
        service.RecordStructured(
            BattleLogEventKind.Buff,
            1,
            BattlePhase.BattlePostPhase.ToString(),
            "battlelog.debug_line",
            new[] { "酒Buff持续" },
            buff: "酒");
        service.RecordStructured(
            BattleLogEventKind.AiDecision,
            1,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.debug_line",
            new[] { "AI selected 酒" },
            debugOnly: true,
            actor: "酒徒",
            debugMessage: "weighted choice");

        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new Player("酒徒", "enemy", BattleTeam.Enemy);
        enemy.ResetForNewBattle(40, 40, 1);
        var triggerManager = new TriggerManager();
        triggerManager.Register(new TestHighEffect());
        triggerManager.Register(new TestLowEffect());
        var context = new BattleContext(player, triggerManager)
        {
            LogService = service,
            TurnNumber = 1,
            Phase = BattlePhase.BattlePhase,
            DamageEvent = new DamageEvent(player, enemy, CardType.FireKill, 10)
        };
        // Legacy equipment effects may emit a marker and the actual result as
        // separate trigger lines. Both this form and an inline equipment line
        // must be visible in the ordinary Battle Log.
        context.AddTriggerLog("[Equipment]");
        context.AddTriggerLog("虎符：第4回合结束，获得1费。");
        context.AddTriggerLog("[Equipment/战鼓] 杀伤害 +5。");
        triggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        new ApplyDamageEffect().Execute(context);

        var history = service.Query(BattleLogScope.CurrentBattle, 1, developerMode: false);
        var developer = service.Query(BattleLogScope.CurrentBattle, 1, developerMode: true);
        Assert(history.Contains(playerAction), "Battle History 没有引用玩家行动事件");
        Assert(service.CombatFeed.Contains(playerAction), "Combat Feed 没有引用同一个玩家行动事件");
        Assert(ReferenceEquals(
            history.First(entry => entry.EventId == playerAction.EventId),
            service.CombatFeed.First(entry => entry.EventId == playerAction.EventId)),
            "Combat Feed 与 Battle History 复制了事件对象");
        Assert(history.All(entry => !entry.DebugOnly), "玩家 Battle History 泄漏了开发日志");
        Assert(history.Count(entry => entry.Kind == BattleLogEventKind.Equipment) == 3,
            "装备触发没有完整写入普通战斗日志");
        Assert(history.Any(entry => entry.Kind == BattleLogEventKind.Equipment
            && entry.Equipment == "Equipment"
            && entry.GetText().Contains("虎符", StringComparison.Ordinal)),
            "分行记录的装备触发没有显示实际效果");
        Assert(history.Any(entry => entry.Kind == BattleLogEventKind.Equipment
            && entry.Equipment == "战鼓"
            && entry.GetText().Contains("+5", StringComparison.Ordinal)),
            "内联装备触发没有显示在普通战斗日志中");
        Assert(developer.Any(entry => entry.Kind == BattleLogEventKind.TriggerPhase), "Developer Report 缺少 Trigger 阶段");
        Assert(developer.Any(entry => entry.Kind == BattleLogEventKind.EffectQueue), "Developer Report 缺少 EffectQueue");
        Assert(developer.Any(entry => entry.Kind == BattleLogEventKind.EffectExecute), "Developer Report 缺少 Effect 执行结果");
        Assert(developer.Any(entry => entry.Kind == BattleLogEventKind.Damage
            && entry.Actor == "玩家"
            && entry.Target == "酒徒"
            && entry.HpBefore == 40
            && entry.HpAfter == 30), "正式伤害没有记录来源、目标和HP前后值");
        Assert(service.Query(
            BattleLogScope.CurrentBattle,
            1,
            true,
            developerFilter: DeveloperLogFilter.Skill).All(entry => entry.Kind == BattleLogEventKind.Skill),
            "技能过滤混入其它事件");
        Assert(service.Query(
            BattleLogScope.CurrentBattle,
            1,
            true,
            developerFilter: DeveloperLogFilter.Ai).Single().Actor == "酒徒",
            "AI过滤或Actor字段错误");
        Assert(history.Count(entry => entry.Kind == BattleLogEventKind.EnemyAction) == 2,
            "多个敌人的行动没有独立记录");
        Assert(firstBattle.Rounds.Count == 1 && firstBattle.Rounds[0].Phases.Count >= 3,
            "Battle→Round→Phase 层级未建立");

        for (var index = 0; index < BattleLogService.MaxCombatFeedEntries + 5; index++)
        {
            service.RecordStructured(
                BattleLogEventKind.Status,
                2,
                BattlePhase.EndPhase.ToString(),
                "battlelog.raw",
                new[] { $"feed-{index}" });
        }

        Assert(service.CombatFeed.Count == BattleLogService.MaxCombatFeedEntries,
            "Combat Feed 没有限制为1000条");
        Assert(firstBattle.Entries.Count > service.CombatFeed.Count,
            "Feed截断错误删除了Battle History");

        service.EndBattle("Victory");
        service.BeginBattle("chapter3_boss");
        service.RecordStructured(
            BattleLogEventKind.BattleStart,
            0,
            BattlePhase.StartPhase.ToString(),
            "battlelog.battle_start");
        Assert(service.CurrentRun.Battles.Count == 2, "新战斗覆盖了上一场Battle Report");
        Assert(service.CurrentRun.Battles[0].Result == "Victory", "上一场战斗结果未保留");
        Assert(service.CombatFeed.Count == 1, "新战斗没有重置实时Feed");

        var text = service.ExportText();
        var json = service.ExportJson();
        Assert(text.Contains("Battle 1", StringComparison.Ordinal)
            && text.Contains("Battle 2", StringComparison.Ordinal), "TXT没有导出整个Run");
        using var document = JsonDocument.Parse(json);
        Assert(document.RootElement.GetProperty("runs")[0].GetProperty("battles").GetArrayLength() == 2,
            "JSON没有导出整个Run的两场战斗");
        var exportedEvents = document.RootElement
            .GetProperty("runs")[0]
            .GetProperty("battles")[0]
            .GetProperty("rounds")[0]
            .GetProperty("phases")[0]
            .GetProperty("events");
        Assert(exportedEvents.GetArrayLength() > 0, "JSON没有导出阶段事件");

        var firstRunId = service.CurrentRun.RunId;
        service.BeginRun();
        var secondRunId = service.CurrentRun.RunId;
        service.BeginBattle("run2_battle");
        service.RecordStructured(
            BattleLogEventKind.BattleStart,
            0,
            BattlePhase.StartPhase.ToString(),
            "battlelog.battle_start");
        service.BeginRun();
        var thirdRunId = service.CurrentRun.RunId;
        service.BeginBattle("run3_battle");
        service.RecordStructured(
            BattleLogEventKind.BattleStart,
            0,
            BattlePhase.StartPhase.ToString(),
            "battlelog.battle_start");

        Assert(service.Runs.Count == BattleLogService.MaxRetainedRuns,
            "没有严格保留最近两个Run");
        Assert(service.Runs.All(run => run.RunId != firstRunId),
            "第三个Run开始后没有删除最旧Run");
        Assert(service.Runs.Any(run => run.RunId == secondRunId)
            && service.Runs.Any(run => run.RunId == thirdRunId),
            "最近两个Run的日志没有完整保留");
        Assert(service.Query(
                BattleLogScope.AllRun,
                0,
                true,
                runId: secondRunId).Count == 1,
            "按历史Run查询没有返回对应日志");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestHighEffect : IBattleEffect
    {
        public EffectPriority Priority => EffectPriority.High;
        public TriggerTiming Timing => TriggerTiming.OnDamage;

        public void Execute(BattleContext context)
        {
            context.DamageEvent?.AddModifier(new DamageModifier(
                "TestHigh",
                DamageModifierPriority.FlatBonus,
                DamageModifierOperation.Add,
                0));
        }
    }

    private sealed class TestLowEffect : IBattleEffect
    {
        public EffectPriority Priority => EffectPriority.Low;
        public TriggerTiming Timing => TriggerTiming.OnDamage;

        public void Execute(BattleContext context)
        {
            context.AddTriggerLog("[TestSkill]");
        }
    }
}
