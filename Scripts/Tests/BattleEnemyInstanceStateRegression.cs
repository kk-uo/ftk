//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/BattleEnemyInstanceStateRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证同模板多敌人的战斗状态不会互相覆盖。
// 2. 验证黄月英【如影随行】的全场费用重置规则。
// 3. 验证 RunBuff 战斗开始加费不会被误算成基础初始费用。
// 4. 验证影袭状态期间不会被顺手牵羊。
// 5. 验证曹真【司敌】只判定玩家当前锁定的敌人。
// 6. 验证三敌人战斗的锁定目标与范围杀结算不会串位或重复伤害。
// 7. 验证藏宝阁双敌人的公开四连击不会叠加隐藏装备倍率。
// 8. 验证酒护盾只保护实际饮酒的敌人实例，并在下一回合清除。
//
// 不负责：
// × 模拟完整 UI 点击。
// × 覆盖所有敌人 AI 决策。
//
// 主要依赖：
// BattleContext
// BattlePhaseResolutionEffect
// DamageEffects
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 多敌人实例状态与黄月英费用重置的 Headless 回归入口。
/// </summary>
public partial class BattleEnemyInstanceStateRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行回归用例，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"BATTLE_ENEMY_INSTANCE_STATE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BATTLE_ENEMY_INSTANCE_STATE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        TestRunBuffBattleStartManaIsNotFoldedIntoInitialMana();
        TestRuYingSuiXingLocksBattleMaxHealth();
        TestSameDefinitionEnemiesKeepSeparateCounterDefense();
        TestRuYingSuiXingResetsAllResourcesToOneOnAnyDamage();
        TestShadowStateBlocksEnemyStealFromPlayer();
        TestShadowStateBlocksPlayerStealFromEnemy();
        TestSiDiOnlyChecksCurrentLockedEnemy();
        TestSingleTargetKillOnlyDamagesLockedEnemy();
        TestSingleTargetKillClashesWithOtherEnemyAttack();
        TestAreaKillSettlesAgainstEachEnemyActionOnce();
        TestTreasurePavilionBurstHasNoHiddenEquipmentMultiplier();
        TestTreasurePavilionTurnFiveResolvesTwoIndependentActions();
        TestEnemyWineShieldIsScopedToItsOwner();
    }

    private void TestRunBuffBattleStartManaIsNotFoldedIntoInitialMana()
    {
        GameManager.BeginNewRun();
        RunBuffManager.Add(RunBuffIds.ExpiredEnhancer);

        Assert(GameManager.GetPlayerInitialMana() == BattleConstants.InitialMana, "RunBuff 战斗开始加费被误算进基础初始费用");

        var player = CreatePlayer();
        player.ResetForNewBattle(40, 40, GameManager.GetPlayerInitialMana());

        var triggerManager = new TriggerManager();
        triggerManager.Register(new BattlePrePhaseEffect());
        triggerManager.Register(new RunBuffBattleStartManaEffect());
        var context = new BattleContext(player, triggerManager)
        {
            TurnNumber = 1
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnBattlePrePhase, context);

        Assert(player.CurrentMana == 2, "RunBuff 战斗开始加费未通过 Trigger 流程生效");
        Assert(context.RoundResult.Text.Contains("RunBuff", StringComparison.Ordinal), "RunBuff 战斗开始加费缺少可追踪日志");
        GameManager.BeginNewRun();
    }

    private void TestRuYingSuiXingLocksBattleMaxHealth()
    {
        var player = CreatePlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.RuYingSuiXing)!);

        player.AddMaxHealth(10);
        player.AddMaxHealthWithoutHealing(10);

        Assert(player.MaxHealth == 40 && player.Health == 40,
            "黄月英【如影随行】没有拦截战斗内装备/技能带来的最大生命变化");
    }

    private void TestSameDefinitionEnemiesKeepSeparateCounterDefense()
    {
        var player = CreatePlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.RuYingSuiXing)!);
        player.DebugSetMana(5);

        var first = CreateDuplicateEnemy();
        var second = CreateDuplicateEnemy();
        first.DebugSetMana(5);
        second.DebugSetMana(5);

        var context = CreateContext(player, first, second);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.ShadowKill(), 1, first);
        context.SetActionForEnemy(first, BattleAction.FromCard(Card.Unassailable(), 1, player));
        context.SetActionForEnemy(second, BattleAction.FromCard(Card.Dodge(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(first.Health == first.MaxHealth, "同模板目标敌人的无懈防御被其它敌人覆盖，仍然扣血");
        Assert(context.RoundResult.Text.Contains("无懈", StringComparison.Ordinal), "回合结果未记录无懈防御");
    }

    private void TestRuYingSuiXingResetsAllResourcesToOneOnAnyDamage()
    {
        var player = CreatePlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.RuYingSuiXing)!);
        player.DebugSetMana(5);

        var first = CreateDuplicateEnemy();
        var second = CreateDuplicateEnemy();
        first.DebugSetMana(4);
        second.DebugSetMana(3);

        var context = CreateContext(player, first, second);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, first);
        context.SetActionForEnemy(first, BattleAction.FromCard(Card.Fee(), 1, player));
        context.SetActionForEnemy(second, BattleAction.FromCard(Card.Fee(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(first.Health == first.MaxHealth - BattleConstants.KillDamage, "实际伤害未正常结算");
        Assert(player.CurrentMana == 1, "如影随行未将玩家费用重置为1");
        Assert(first.CurrentMana == 1, "如影随行未将受伤敌人费用重置为1");
        Assert(second.CurrentMana == 1, "如影随行未将其它存活敌人费用重置为1");
    }

    private void TestShadowStateBlocksEnemyStealFromPlayer()
    {
        var player = CreatePlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.YingXi)!);
        player.EnterShadowState();
        player.DebugSetMana(5);

        var enemy = CreateDuplicateEnemy();
        enemy.DebugSetMana(2);

        var context = CreateContext(player, enemy);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.ShadowLurk(), 1, null);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Steal(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.CurrentMana == 5, "影袭状态中的玩家仍被顺手牵羊偷取费用");
        Assert(enemy.CurrentMana == 1, "影袭免疫顺手牵羊失败时敌人不应获得目标费用");
        Assert(context.RoundResult.Text.Contains("影袭", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("无法被顺手牵羊", StringComparison.Ordinal), "影袭免疫顺手牵羊缺少战报");
    }

    private void TestShadowStateBlocksPlayerStealFromEnemy()
    {
        var player = CreatePlayer();
        player.DebugSetMana(3);

        var enemy = CreateDuplicateEnemy();
        enemy.AddSkill(SkillDatabase.GetSkill(SkillIds.YingXi)!);
        enemy.EnterShadowState();
        enemy.DebugSetMana(4);

        var context = CreateContext(player, enemy);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Steal(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.ShadowLurk(), 1, null));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(enemy.CurrentMana == 4, "影袭状态中的敌人仍被顺手牵羊偷取费用");
        Assert(player.CurrentMana == 2, "影袭免疫顺手牵羊失败时玩家不应获得目标费用");
        Assert(context.RoundResult.Text.Contains("影袭", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("无法被顺手牵羊", StringComparison.Ordinal), "影袭目标免疫顺手牵羊缺少战报");
    }

    private void TestSiDiOnlyChecksCurrentLockedEnemy()
    {
        var player = CreatePlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.SiDi)!);
        var left = CreateDuplicateEnemy();
        var right = CreateDuplicateEnemy();
        var context = CreateContext(player, left, right);

        // 未锁定的右侧敌人出费，不应与玩家的费触发司敌。
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Fee());
        context.PlayerLockedTarget = left;
        context.SetActionForEnemy(left, BattleAction.FromCard(Card.Dodge(), 1, player));
        context.SetActionForEnemy(right, BattleAction.FromCard(Card.Fee(), 1, player));
        new SiDiEffect().Execute(context);
        Assert(player.CurrentMana == 1, "司敌错误读取了未锁定敌人的费");

        // 即使多个敌人都出费，也只能根据锁定目标触发一次。
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Fee());
        context.PlayerLockedTarget = left;
        context.SetActionForEnemy(left, BattleAction.FromCard(Card.Fee(), 1, player));
        context.SetActionForEnemy(right, BattleAction.FromCard(Card.Fee(), 1, player));
        new SiDiEffect().Execute(context);
        Assert(player.CurrentMana == 2, "司敌在多敌人同牌时重复触发或没有按锁定目标触发");

        // 攻击牌匹配时只取消锁定敌人的攻击，另一名敌人的攻击保持有效。
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, left);
        context.PlayerLockedTarget = left;
        context.SetActionForEnemy(left, BattleAction.FromCard(Card.FireKill(), 1, player));
        context.SetActionForEnemy(right, BattleAction.FromCard(Card.ThunderKill(), 1, player));
        new SiDiEffect().Execute(context);
        Assert(context.GetEnemyActionEntry(left)?.ActionCancelled == true, "司敌没有取消锁定敌人的攻击牌");
        Assert(context.GetEnemyActionEntry(right)?.ActionCancelled == false, "司敌错误取消了未锁定敌人的攻击牌");

        // 切换锁定目标后，判定对象必须同步切换。
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, right);
        context.PlayerLockedTarget = right;
        context.SetActionForEnemy(left, BattleAction.FromCard(Card.FireKill(), 1, player));
        context.SetActionForEnemy(right, BattleAction.FromCard(Card.ThunderKill(), 1, player));
        new SiDiEffect().Execute(context);
        Assert(context.GetEnemyActionEntry(left)?.ActionCancelled == false, "切换锁定目标后司敌仍引用旧敌人");
        Assert(context.GetEnemyActionEntry(right)?.ActionCancelled == true, "切换锁定目标后司敌没有判定新敌人");
    }

    private void TestSingleTargetKillOnlyDamagesLockedEnemy()
    {
        GameManager.BeginNewRun();
        var player = CreatePlayer();
        var thug = CreateEnemy("modified_thug", "改造打手");
        var firstDrinker = CreateEnemy("wine_drinker", "酒徒");
        var secondDrinker = CreateEnemy("wine_drinker", "酒徒");
        var context = CreateContext(player, thug, firstDrinker, secondDrinker);

        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, firstDrinker);
        context.PlayerLockedTarget = firstDrinker;
        context.SetActionForEnemy(thug, BattleAction.FromCard(Card.Fee(), 1, player));
        context.SetActionForEnemy(firstDrinker, BattleAction.FromCard(Card.Fee(), 1, player));
        context.SetActionForEnemy(secondDrinker, BattleAction.FromCard(Card.Fee(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(thug.Health == thug.MaxHealth, "单体杀错误伤害了未锁定的改造打手");
        Assert(firstDrinker.Health == firstDrinker.MaxHealth - BattleConstants.KillDamage, "单体杀没有命中锁定的酒徒实例");
        Assert(secondDrinker.Health == secondDrinker.MaxHealth, "单体杀错误伤害了同模板的另一名酒徒");
        Assert(context.GetTargetEnemyActionEntry()?.Enemy == firstDrinker, "目标行动条目没有绑定锁定的酒徒实例");
        Assert(firstDrinker.BattleStateKey != secondDrinker.BattleStateKey, "同模板酒徒错误共享了战斗实例键");
    }

    private void TestAreaKillSettlesAgainstEachEnemyActionOnce()
    {
        GameManager.BeginNewRun();
        var rustSpear = GameManager.AddEquipment(EquipmentIds.RustSpear);
        Assert(rustSpear != null && InventoryManager.EquipToSlot(rustSpear.InstanceId, EquipmentSlot.Weapon), "回归用例无法装备锈剑");

        var player = CreatePlayer();
        var thug = CreateEnemy("modified_thug", "改造打手");
        var firstDrinker = CreateEnemy("wine_drinker", "酒徒");
        var secondDrinker = CreateEnemy("wine_drinker", "酒徒");
        var context = CreateContext(player, thug, firstDrinker, secondDrinker);

        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, thug);
        context.PlayerLockedTarget = thug;
        context.SetActionForEnemy(thug, BattleAction.FromCard(Card.Fee(), 1, player));
        context.SetActionForEnemy(firstDrinker, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(secondDrinker, BattleAction.FromCard(Card.FireKill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(thug.Health == thug.MaxHealth - BattleConstants.KillDamage, "范围杀没有命中出费的改造打手");
        Assert(firstDrinker.Health == firstDrinker.MaxHealth, "范围杀没有与第一名酒徒的普通杀正确互消");
        Assert(secondDrinker.Health == secondDrinker.MaxHealth, "范围杀错误穿透了第二名酒徒的火杀");
        Assert(
            player.Health == player.MaxHealth - BattleRules.GetCardBaseDamage(CardType.FireKill),
            "范围杀绕过克制关系或重复结算了敌方攻击");
        Assert(context.RoundResult.Text.Contains("锈剑触发", StringComparison.Ordinal), "范围杀缺少装备来源战报");
        GameManager.BeginNewRun();
    }

    private void TestSingleTargetKillClashesWithOtherEnemyAttack()
    {
        GameManager.BeginNewRun();
        var player = CreatePlayer();
        var attackingEnemy = CreateEnemy("abandoned_servant", "废弃机仆");
        var lockedEnemy = CreateEnemy("scavenger", "拾荒者");
        var context = CreateContext(player, attackingEnemy, lockedEnemy);

        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, lockedEnemy);
        context.PlayerLockedTarget = lockedEnemy;
        context.SetActionForEnemy(attackingEnemy, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(lockedEnemy, BattleAction.FromCard(Card.Fee(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth, "非锁定敌人的普通杀绕过杀对杀互消并伤害了玩家");
        Assert(attackingEnemy.Health == attackingEnemy.MaxHealth, "防御碰撞错误地把单体杀伤害扩散到非锁定敌人");
        Assert(lockedEnemy.Health == lockedEnemy.MaxHealth - BattleConstants.KillDamage,
            "单体杀没有继续命中当前锁定目标");
        Assert(context.RoundResult.Text.Contains("互相抵消", StringComparison.Ordinal),
            "非锁定敌人的杀对杀互消缺少结算记录");
    }

    private void TestTreasurePavilionBurstHasNoHiddenEquipmentMultiplier()
    {
        var rollingStone = EnemyDatabase.GetEnemy("giant_rolling_stone");
        var rollingLog = EnemyDatabase.GetEnemy("giant_rolling_log");

        Assert(rollingStone != null, "藏宝阁缺少巨型滚石定义");
        Assert(rollingLog != null, "藏宝阁缺少巨型滚木定义");
        Assert(rollingStone!.EquipmentIds.Count == 0, "巨型滚石仍携带未公开的隐藏增伤装备");
        Assert(rollingLog!.EquipmentIds.Count == 0, "巨型滚木仍携带未公开的隐藏增伤装备");

        var stoneBurst = rollingStone.AiProfile.ScriptedActions.Find(rule => rule.MinTurn == 5 && rule.MaxTurn == 5);
        var logBurst = rollingLog.AiProfile.ScriptedActions.Find(rule => rule.MinTurn == 5 && rule.MaxTurn == 5);
        Assert(stoneBurst != null && stoneBurst.CardType == CardType.Kill && stoneBurst.Count == 4,
            "巨型滚石第5回合不再是明确的普通杀四连击");
        Assert(logBurst != null && logBurst.CardType == CardType.SureKill && logBurst.Count == 4,
            "巨型滚木第5回合不再是明确的必中杀四连击");
    }

    private void TestTreasurePavilionTurnFiveResolvesTwoIndependentActions()
    {
        var rollingStone = new EnemyInstance(EnemyDatabase.GetEnemy("giant_rolling_stone")!);
        var rollingLog = new EnemyInstance(EnemyDatabase.GetEnemy("giant_rolling_log")!);
        rollingStone.DebugSetMana(4);
        rollingLog.DebugSetMana(4);

        var player = CreatePlayer();
        player.ResetForNewBattle(200, 200, 1);
        var context = CreateContext(player, rollingStone, rollingLog);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Fee());
        context.SetActionForEnemy(rollingStone, BattleAction.FromCard(Card.Kill(), 4, player));
        context.SetActionForEnemy(rollingLog, BattleAction.FromCard(Card.SureKill(), 4, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(context.GetEnemyActionEntry(rollingStone)?.Action.Type == CardType.Kill,
            "巨型滚石行动错误绑定到巨型滚木");
        Assert(context.GetEnemyActionEntry(rollingLog)?.Action.Type == CardType.SureKill,
            "巨型滚木行动错误绑定到巨型滚石");
        Assert(player.Health == 120, "藏宝阁第5回合双敌基础四连击总伤害应为80");
    }

    private void TestEnemyWineShieldIsScopedToItsOwner()
    {
        var player = CreatePlayer();
        var drinkingEnemy = CreateEnemy("wine_owner", "饮酒敌人");
        var exposedEnemy = CreateEnemy("wine_bystander", "未饮酒敌人");
        var context = CreateContext(player, drinkingEnemy, exposedEnemy);

        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, exposedEnemy);
        context.PlayerLockedTarget = exposedEnemy;
        context.SetActionForEnemy(drinkingEnemy, BattleAction.FromCard(Card.Wine(), 1, drinkingEnemy));
        context.SetActionForEnemy(exposedEnemy, BattleAction.FromCard(Card.Fee(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(drinkingEnemy.Health == drinkingEnemy.MaxHealth, "饮酒敌人不应受到指向另一实例的攻击");
        Assert(exposedEnemy.Health == exposedEnemy.MaxHealth - BattleConstants.KillDamage,
            "另一敌人的酒护盾错误替未饮酒敌人格挡伤害");
        Assert(context.EnemyWineShieldLayers.TryGetValue(drinkingEnemy.BattleStateKey, out var ownerLayers)
            && ownerLayers == 1, "酒护盾没有绑定到实际饮酒的敌人实例");
        Assert(!context.EnemyWineShieldLayers.ContainsKey(exposedEnemy.BattleStateKey),
            "未饮酒敌人错误获得了酒护盾");

        context.BeginRoundResult();
        Assert(context.EnemyWineShieldLayers.Count == 0, "上一回合的敌方酒护盾错误保留到下一回合");
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        return player;
    }

    private static EnemyInstance CreateDuplicateEnemy()
    {
        return CreateEnemy("duplicate_guard", "重复守卫");
    }

    private static EnemyInstance CreateEnemy(string id, string name)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = name,
            MaxHP = 20,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
    }

    private static BattleContext CreateContext(Player player, params EnemyInstance[] enemies)
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        triggerManager.Register(new RuYingSuiXingCostResetEffect());

        var encounter = new BattleEncounter();
        encounter.Enemies.AddRange(enemies);

        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnNumber = 1
        };
        return context;
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
