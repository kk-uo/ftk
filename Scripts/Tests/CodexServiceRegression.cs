//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CodexServiceRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 CodexService 各 Record* 方法记账语义（使用/命中/格挡/伤害/击杀/
//    获得/遭遇/选项/技能触发等）符合永久图鉴规格。
// 2. 验证 DebugBattleActive 开关能挡住调试战斗数据写入正式图鉴。
// 3. 验证 DebugUnlockAll / DebugReset / FindOrphanEntries 等开发者工具行为。
//
// 不负责：
// × 验证 CodexController UI 渲染与本地化文案（需要在 Godot 编辑器内人工验证）。
// × 验证 user://codex_save.json 的实际落盘/读档（会污染开发者本机真实存档，
//   本测试全程使用 CodexService.ResetForTest 的纯内存态，不调用 SaveIfDirty/Initialize）。
// × 验证战斗/教程系统如何在真实调用点触发这些 Record* 方法（各调用点已在对应
//   系统文件内联验证，见实现报告"已修改文件"列表）。
//
// 主要依赖：
// CodexService / EnemyDatabase / EquipmentDatabase / EventDatabase
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 永久图鉴（Codex）记账语义的无头回归入口。
/// </summary>
public partial class CodexServiceRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 依次执行各分类的记账语义回归并通过进程退出码报告结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();

            TestCardUsageAndHitCounting();
            TestCardValueAndSpecialSlots();
            TestEnemyEncounterAndDefeat();
            TestBossChallengeAndWinRate();
            TestEquipmentSeenVsObtained();
            TestEventEncounterAndHiddenOptions();
            TestCharacterAndSkillStats();
            TestChipAndGlobalGold();
            TestDebugBattleActiveBlocksTracking();
            TestDebugUnlockAllDoesNotFakeStats();
            TestFindOrphanEntries();
            TestDebugResetClearsEverything();

            GD.Print($"CODEX_SERVICE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CODEX_SERVICE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestCardUsageAndHitCounting()
    {
        CodexService.ResetForTest();

        CodexService.RecordCardUsed(CardType.Kill, 1);
        CodexService.RecordCardUsed(CardType.Kill, 2);
        var card = CodexService.GetCard(CardType.Kill);
        Assert(card != null && card.Discovered, "杀使用后未标记为已发现");
        Assert(card!.TimesUsed == 3, $"杀使用次数累计错误，期望3实际{card.TimesUsed}");
        Assert(CodexService.Global.TotalCardsUsed == 3, "全局卡牌使用次数未同步累计");

        // 多目标卡牌一次行动只算一次使用，但每个命中目标各记一次命中。
        CodexService.RecordCardUsed(CardType.ArrowBarrage, 1);
        CodexService.RecordCardHit(CardType.ArrowBarrage, 5, false);
        CodexService.RecordCardHit(CardType.ArrowBarrage, 8, false);
        CodexService.RecordCardHit(CardType.ArrowBarrage, 12, true);
        var arrow = CodexService.GetCard(CardType.ArrowBarrage);
        Assert(arrow!.TimesUsed == 1, $"万箭齐发使用次数应为1，实际{arrow.TimesUsed}");
        Assert(arrow.TimesHit == 3, $"万箭齐发命中次数应为3，实际{arrow.TimesHit}");
        Assert(arrow.TotalDamage == 25, $"万箭齐发累计伤害应为25，实际{arrow.TotalDamage}");
        Assert(arrow.MaxSingleDamage == 12, $"万箭齐发单次最高伤害应为12，实际{arrow.MaxSingleDamage}");
        Assert(arrow.KillCount == 1, $"万箭齐发击杀数应为1，实际{arrow.KillCount}");

        CodexService.RecordCardBlocked(CardType.Kill);
        var killAfterBlock = CodexService.GetCard(CardType.Kill);
        Assert(killAfterBlock!.TimesBlocked == 1, "格挡未计入TimesBlocked");
        Assert(killAfterBlock.TimesHit == 0, "格挡不应该被计入命中");
    }

    private void TestCardValueAndSpecialSlots()
    {
        CodexService.ResetForTest();

        CodexService.RecordCardValue(CardType.Fee, 3);
        CodexService.RecordCardValue(CardType.Fee, 2);
        Assert(CodexService.GetCard(CardType.Fee)!.TotalValue == 5, "费累计获得费用错误");

        CodexService.RecordCardValue(CardType.Peach, 10);
        CodexService.RecordCardSpecial(CardType.Peach);
        var peach = CodexService.GetCard(CardType.Peach);
        Assert(peach!.TotalValue == 10, "桃累计恢复量错误");
        Assert(peach.SpecialCount == 1, "桃濒死复活次数错误");

        CodexService.RecordCardSpecial(CardType.Wine);
        Assert(CodexService.GetCard(CardType.Wine)!.SpecialCount == 1, "酒濒死复活次数错误");

        CodexService.RecordCardSpecial(CardType.Steal);
        CodexService.RecordCardValue(CardType.Steal, 7);
        var steal = CodexService.GetCard(CardType.Steal);
        Assert(steal!.SpecialCount == 1, "顺手牵羊成功次数错误");
        Assert(steal.TotalValue == 7, "顺手牵羊累计偷取数值错误");
    }

    private void TestEnemyEncounterAndDefeat()
    {
        CodexService.ResetForTest();
        var enemyId = EnemyDatabase.GetAllEnemies().First(e => e.Type != EnemyType.Boss).Id;

        CodexService.RecordEnemyEncounter(enemyId, 2);
        var entry = CodexService.GetEnemy(enemyId);
        Assert(entry != null && entry.Discovered, "遭遇后敌人未标记为已发现");
        Assert(entry!.EncounterCount == 1, "敌人遭遇次数错误");
        Assert(entry.FirstSeenChapter == 2, "敌人首次发现章节记录错误");

        // 再次遭遇不应该覆盖首次发现章节。
        CodexService.RecordEnemyEncounter(enemyId, 4);
        Assert(CodexService.GetEnemy(enemyId)!.FirstSeenChapter == 2, "二次遭遇错误覆盖了首次发现章节");

        // 共享HP池去重：同一次血池耗尽事件里，调用方应该对同一个 EnemyDefinition.Id
        // 只调用一次 RecordEnemyDefeated（这里模拟调用方已完成去重后的行为）。
        CodexService.RecordEnemyDefeated(enemyId, 5);
        CodexService.RecordEnemyDefeated(enemyId, 3);
        entry = CodexService.GetEnemy(enemyId);
        Assert(entry!.DefeatedCount == 2, "敌人击败次数累计错误");
        Assert(entry.FastestDefeatTurn == 3, $"最速击败回合应取最小值3，实际{entry.FastestDefeatTurn}");
        Assert(CodexService.Global.TotalEnemiesDefeated == 2, "全局击败敌人数未同步累计");

        CodexService.RecordPlayerDefeatedBy(enemyId);
        Assert(CodexService.GetEnemy(enemyId)!.PlayerDefeatedByCount == 1, "玩家被该敌人击败次数错误");

        CodexService.RecordEnemyDamageDealt(enemyId, 15);
        CodexService.RecordEnemyDamageTaken(enemyId, 9);
        entry = CodexService.GetEnemy(enemyId);
        Assert(entry!.DamageDealtToEnemy == 15, "对敌人造成伤害累计错误");
        Assert(entry.DamageTakenFromEnemy == 9, "承受该敌人伤害累计错误");
    }

    private void TestBossChallengeAndWinRate()
    {
        CodexService.ResetForTest();
        var bossId = EnemyDatabase.GetAllEnemies().First(e => e.Type == EnemyType.Boss).Id;

        CodexService.RecordBossChallenge(bossId);
        CodexService.RecordBossChallenge(bossId);
        CodexService.RecordBossChallenge(bossId);
        CodexService.RecordEnemyDefeatedByBoss(bossId, CharacterIds.ZhaoYun, EnemyType.Boss);

        var entry = CodexService.GetEnemy(bossId);
        Assert(entry!.ChallengeCount == 3, "Boss挑战次数错误");
        Assert(entry.DefeatedByCharacterIds.Contains(CharacterIds.ZhaoYun), "Boss击败角色列表未记录赵云");
        Assert(CodexService.Global.TotalBossesDefeated == 1, "全局Boss击败数未同步累计");

        // 普通敌人不应该被 RecordEnemyDefeatedByBoss 误计入Boss统计。
        var normalId = EnemyDatabase.GetAllEnemies().First(e => e.Type != EnemyType.Boss).Id;
        CodexService.RecordEnemyDefeatedByBoss(normalId, CharacterIds.ZhaoYun, EnemyType.Normal);
        Assert(CodexService.GetEnemy(normalId)?.DefeatedByCharacterIds.Count is null or 0, "非Boss敌人被错误计入Boss击败角色统计");
    }

    private void TestEquipmentSeenVsObtained()
    {
        CodexService.ResetForTest();
        CodexService.CurrentRunId = 7;
        var equipmentId = EquipmentIds.SilverLion;

        CodexService.RecordEquipmentSeen(equipmentId);
        var seenOnly = CodexService.GetEquipment(equipmentId);
        Assert(seenOnly != null && seenOnly.Seen, "商店见过后未标记Seen");
        Assert(!seenOnly!.Obtained, "只是见过（未真正获得）就被错误标记为Obtained");
        Assert(seenOnly.ObtainedCount == 0, "只是见过不应该增加ObtainedCount");

        CodexService.RecordEquipmentObtained(equipmentId);
        var obtained = CodexService.GetEquipment(equipmentId);
        Assert(obtained!.Obtained, "真正获得后未标记为Obtained");
        Assert(obtained.ObtainedCount == 1, "获得次数累计错误");
        Assert(obtained.RunsObtainedIn.Contains(7), "获得装备的Run编号未记录");
        Assert(CodexService.Global.TotalEquipmentObtained == 1, "全局获得装备数未同步累计");

        CodexService.RecordEquipmentEquipped(equipmentId);
        CodexService.RecordEquipmentSold(equipmentId);
        CodexService.RecordEquipmentTriggered(equipmentId);
        obtained = CodexService.GetEquipment(equipmentId);
        Assert(obtained!.EquippedCount == 1, "装备次数错误");
        Assert(obtained.SoldCount == 1, "出售次数错误");
        Assert(obtained.TriggeredCount == 1, "触发次数错误");
    }

    private void TestEventEncounterAndHiddenOptions()
    {
        CodexService.ResetForTest();
        var eventData = EventDatabase.GetAllEvents().First(e => e.Options.Count >= 2);

        CodexService.RecordEventEncounter(eventData.Id);
        var entry = CodexService.GetEvent(eventData.Id);
        Assert(entry != null && entry.Discovered, "遭遇事件后未标记为已发现");
        Assert(entry!.EncounterCount == 1, "事件遭遇次数错误");
        Assert(entry.DiscoveredOptionIndexes.Count == 0, "尚未选择任何选项时不应该有已发现选项");

        CodexService.RecordEventChoice(eventData.Id, 0);
        CodexService.RecordEventChoice(eventData.Id, 0);
        CodexService.RecordEventChoice(eventData.Id, 1);
        entry = CodexService.GetEvent(eventData.Id);
        Assert(entry!.ChoiceCounts[0] == 2, "选项0选择次数错误");
        Assert(entry.ChoiceCounts[1] == 1, "选项1选择次数错误");
        Assert(entry.DiscoveredOptionIndexes.Contains(0) && entry.DiscoveredOptionIndexes.Contains(1),
            "已选择的选项未加入已发现集合");
        if (eventData.Options.Count > 2)
        {
            Assert(!entry.DiscoveredOptionIndexes.Contains(2), "未选择过的选项被错误标记为已发现（隐藏选项泄露）");
        }
    }

    private void TestCharacterAndSkillStats()
    {
        CodexService.ResetForTest();
        const string characterId = CharacterIds.ZhaoYun;

        CodexService.RecordCharacterRunStart(characterId);
        var entry = CodexService.GetCharacter(characterId);
        Assert(entry != null && entry.Unlocked, "开始一局后角色未标记为已解锁");
        Assert(entry!.RunsStarted == 1, "角色开始局数错误");
        Assert(CodexService.Global.TotalRuns == 1, "全局总Run次数未同步累计");

        CodexService.RecordCharacterBattleResult(characterId, true);
        CodexService.RecordCharacterBattleResult(characterId, false);
        entry = CodexService.GetCharacter(characterId);
        Assert(entry!.BattlesFought == 2 && entry.BattlesWon == 1, "角色战斗场次/胜场统计错误");

        CodexService.RecordCharacterBossDefeated(characterId);
        CodexService.RecordCharacterDeath(characterId);
        CodexService.RecordCharacterRunCompleted(characterId);
        entry = CodexService.GetCharacter(characterId);
        Assert(entry!.BossesDefeated == 1, "角色Boss击败数错误");
        Assert(entry.Deaths == 1, "角色死亡次数错误");
        Assert(entry.RunsCompleted == 1, "角色通关局数错误");

        CodexService.RecordCharacterChapterReached(characterId, 2);
        CodexService.RecordCharacterChapterReached(characterId, 1);
        Assert(CodexService.GetCharacter(characterId)!.HighestChapterReached == 2,
            "最高到达章节应该只增不减（后续更低章节不应覆盖）");

        CodexService.RecordCharacterDamageDealt(characterId, 20);
        CodexService.RecordCharacterDamageTaken(characterId, 8);
        CodexService.RecordCharacterHealing(characterId, 6);
        entry = CodexService.GetCharacter(characterId);
        Assert(entry!.DamageDealt == 20 && entry.DamageTaken == 8 && entry.HealingDone == 6,
            "角色伤害/承伤/治疗累计错误");

        CodexService.RecordSkillTriggered(characterId, "jiang_test_skill");
        CodexService.RecordSkillTriggered(characterId, "jiang_test_skill");
        Assert(CodexService.GetCharacter(characterId)!.SkillTriggerCounts["jiang_test_skill"] == 2,
            "技能触发次数累计错误");
    }

    private void TestChipAndGlobalGold()
    {
        CodexService.ResetForTest();

        CodexService.RecordChipObtained("attack");
        CodexService.RecordChipObtained("attack");
        Assert(CodexService.GetChip("attack")!.ObtainedCount == 2, "芯片获得次数累计错误");

        CodexService.RecordGoldEarned(100);
        CodexService.RecordGoldSpent(40);
        Assert(CodexService.Global.TotalGoldEarned == 100, "全局累计获得金币错误");
        Assert(CodexService.Global.TotalGoldSpent == 40, "全局累计消费金币错误");
    }

    private void TestDebugBattleActiveBlocksTracking()
    {
        CodexService.ResetForTest();
        CodexService.DebugBattleActive = true;

        CodexService.RecordCardUsed(CardType.Kill, 1);
        CodexService.RecordEnemyEncounter("regression_debug_enemy", 1);
        CodexService.RecordGoldEarned(999);

        Assert(CodexService.GetCard(CardType.Kill) == null, "调试战斗中的卡牌使用被错误计入正式图鉴");
        Assert(CodexService.GetEnemy("regression_debug_enemy") == null, "调试战斗中的敌人遭遇被错误计入正式图鉴");
        Assert(CodexService.Global.TotalGoldEarned == 0, "调试战斗中的金币获得被错误计入全局统计");

        CodexService.DebugBattleActive = false;
    }

    private void TestDebugUnlockAllDoesNotFakeStats()
    {
        CodexService.ResetForTest();
        CodexService.DebugUnlockAll();

        var card = CodexService.GetCard(CardType.Kill);
        Assert(card != null && card.Discovered, "DebugUnlockAll未解锁卡牌发现状态");
        Assert(card!.TimesUsed == 0, "DebugUnlockAll不应该伪造使用次数等统计");

        var anyEnemy = EnemyDatabase.GetAllEnemies().First();
        var enemyEntry = CodexService.GetEnemy(anyEnemy.Id);
        Assert(enemyEntry != null && enemyEntry.Discovered, "DebugUnlockAll未解锁敌人发现状态");
        Assert(enemyEntry!.DefeatedCount == 0, "DebugUnlockAll不应该伪造敌人击败次数");

        var anyEquipment = EquipmentDatabase.GetAllEquipments().First();
        var equipEntry = CodexService.GetEquipment(anyEquipment.Id);
        Assert(equipEntry != null && equipEntry.Seen && equipEntry.Obtained, "DebugUnlockAll未解锁装备Seen/Obtained");
        Assert(equipEntry!.ObtainedCount == 0, "DebugUnlockAll不应该伪造装备获得次数");
    }

    private void TestFindOrphanEntries()
    {
        CodexService.ResetForTest();
        var realEnemyId = EnemyDatabase.GetAllEnemies().First().Id;
        CodexService.RecordEnemyEncounter(realEnemyId, 1);
        CodexService.RecordEnemyEncounter("regression_orphan_enemy_id_that_should_not_exist", 1);

        var orphans = CodexService.FindOrphanEntries();
        Assert(orphans.Contains("enemy:regression_orphan_enemy_id_that_should_not_exist"),
            "孤立敌人ID未被FindOrphanEntries检测出来");
        Assert(!orphans.Contains($"enemy:{realEnemyId}"), "真实存在的敌人ID被误报为孤立条目");
    }

    private void TestDebugResetClearsEverything()
    {
        CodexService.ResetForTest();
        CodexService.RecordCardUsed(CardType.Kill, 5);
        CodexService.RecordGoldEarned(50);

        CodexService.DebugReset();

        Assert(CodexService.GetCard(CardType.Kill) == null, "DebugReset后卡牌统计未被清空");
        Assert(CodexService.Global.TotalGoldEarned == 0, "DebugReset后全局统计未被清空");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
