//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/FactionShopAndSunCeRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证吴·先行采购按商店访问恢复免费购买机会。
// 2. 验证孙策【魂姿】低于半血时可反复触发。
// 3. 验证孙策【激昂】击杀持有【完杀】的目标后仍会回复生命。
//
// 不负责：
// × 模拟完整商店 UI 点击。
// × 覆盖所有阵营命运组合。
//
// 主要依赖：
// ShopManager
// FactionFateManager
// BattleContext
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 商店阵营命运与孙策技能的 Headless 回归入口。
/// </summary>
public partial class FactionShopAndSunCeRegression : Node
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
            GD.Print($"FACTION_SHOP_SUNCE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FACTION_SHOP_SUNCE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        TestWuFirstPurchaseFreeResetsPerShopVisit();
        TestHunZiTriggersRepeatedlyBelowHalfHealth();
        TestJiAngRestoresHealthAfterKillingWanshaTarget();
        TestPlayerDamageBorderRequestsOnlyForActualPlayerDamage();
    }

    private void TestWuFirstPurchaseFreeResetsPerShopVisit()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.SunCe);
        InventoryManager.Reset();
        FactionFateManager.DebugForceFate(FactionFateIds.WuFirstPurchaseFree);

        ShopManager.GenerateSlots(ShopType.Normal);
        Assert(FactionFateManager.IsFirstPurchaseFreeAvailable(), "进入商店后先行采购未开启免费机会");

        var firstIndex = FindFirstShopOfferIndex();
        var firstOffer = ShopManager.CurrentSlots[firstIndex]!;
        Assert(firstOffer.Price > GameManager.Gold, "测试前提失败：首件商品价格应高于当前金币");
        Assert(ShopManager.CanBuy(firstOffer), "先行采购免费机会没有绕过金币不足");
        Assert(ShopManager.TryBuy(firstIndex), "先行采购免费购买失败");
        Assert(GameManager.Gold == GameManager.InitialGold, "先行采购免费购买错误扣除了金币");
        Assert(!FactionFateManager.IsFirstPurchaseFreeAvailable(), "同一商店购买后免费机会未消耗");

        Assert(ShopManager.TryRefresh(), "测试商店刷新失败");
        Assert(!FactionFateManager.IsFirstPurchaseFreeAvailable(), "刷新同一商店不应恢复先行采购免费机会");

        ShopManager.GenerateSlots(ShopType.Normal);
        Assert(FactionFateManager.IsFirstPurchaseFreeAvailable(), "重新进入商店后先行采购没有恢复免费机会");
    }

    private void TestHunZiTriggersRepeatedlyBelowHalfHealth()
    {
        var player = new Player("孙策", CharacterIds.SunCe, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.HunZi)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "hunzi_test_enemy",
            Name = "测试敌人",
            MaxHP = 20,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        triggerManager.Register(new HunZiTriggerEffect());
        triggerManager.Register(new DamageTakenEffect());
        triggerManager.Register(new DyingEffect());
        triggerManager.Register(new DeathEffect());
        var context = new BattleContext(player, triggerManager)
        {
            TurnNumber = 1
        };
        context.BeginRoundResult();

        ApplyDamage(context, triggerManager, enemy, player, 20);
        Assert(!player.RuntimeStates.ContainsKey("hunzi_triggered"), "魂姿在正好半血时不应触发");
        Assert(player.MaxHealth == 40, "正好半血错误降低了最大生命值");
        Assert(player.Health == 20, "正好半血错误回复了生命值");

        ApplyDamage(context, triggerManager, enemy, player, 1);
        Assert(player.RuntimeStates.TryGetValue("hunzi_triggered", out var triggered) && triggered is true, "魂姿在低于半血时没有触发");
        Assert(player.MaxHealth == 22, "魂姿未正确扣除20%最大生命值与额外10点上限");
        Assert(player.Health == 22, "魂姿触发后未回复至新的最大生命值");
        Assert(player.RuntimeStates.TryGetValue("hunzi_original_max_hp", out var originalMaxObj) && originalMaxObj is int originalMax && originalMax == 40, "魂姿未记录战斗开始最大生命值");

        ApplyDamage(context, triggerManager, enemy, player, 12);
        Assert(player.MaxHealth == 8, "魂姿第二次触发未正确扣除20%与10点上限");
        Assert(player.Health == 8, "魂姿第二次触发后未回复至新的最大生命值");
        Assert(player.RuntimeStates.TryGetValue("hunzi_original_max_hp", out var keptOriginalObj) && keptOriginalObj is int keptOriginal && keptOriginal == 40, "魂姿重复触发时覆盖了战斗开始最大生命值");

        ApplyDamage(context, triggerManager, enemy, player, 4);
        Assert(player.MaxHealth == 8, "最大生命值20或以下时魂姿不应继续触发");
        Assert(player.Health == 4, "魂姿停止触发后错误恢复了生命值");
        Assert(context.RoundResult.Text.Contains("魂姿触发", StringComparison.Ordinal), "魂姿缺少战报反馈");

        HunZiTriggerEffect.RestoreBattleMaxHealth(player);
        Assert(player.MaxHealth == 40, "战斗结束后魂姿没有恢复战斗开始时的最大生命值");
        Assert(player.Health == 4, "战斗结束恢复最大生命值时不应额外治疗");
        Assert(!player.RuntimeStates.ContainsKey("hunzi_original_max_hp"), "战斗结束后魂姿基准状态没有清理");

        TestHunZiRequiresActualDamageAndPrecedesDying(enemy);
    }

    private void TestHunZiRequiresActualDamageAndPrecedesDying(Player enemy)
    {
        var player = new Player("孙策", CharacterIds.SunCe, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.HunZi)!);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        triggerManager.Register(new HunZiTriggerEffect());
        triggerManager.Register(new DamageTakenEffect());
        triggerManager.Register(new DyingEffect());
        triggerManager.Register(new DeathEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();

        player.DebugSetHealth(10);
        ApplyDamage(context, triggerManager, enemy, player, 0);
        Assert(player.MaxHealth == 40 && player.Health == 10, "未实际扣血时魂姿不应触发");
        Assert(!player.RuntimeStates.ContainsKey("hunzi_triggered"), "0实际伤害错误记录了魂姿触发状态");

        // 致命伤不触发魂姿，继续交给正常濒死与死亡流程处理。
        player.DebugSetHealth(40);
        ApplyDamage(context, triggerManager, enemy, player, 40);
        Assert(player.MaxHealth == 40, "致命伤错误触发魂姿并降低了最大生命值");
        Assert(!player.RuntimeStates.ContainsKey("hunzi_triggered"), "致命伤错误记录了魂姿触发状态");

        // 对照：非致命的低血量伤害同样继续触发。
        player.DebugSetHealth(40);
        ApplyDamage(context, triggerManager, enemy, player, 39);
        Assert(player.RuntimeStates.TryGetValue("hunzi_triggered", out var stillAliveTriggered) && stillAliveTriggered is true,
            "生命值降到1但未死亡时魂姿应该正常触发");
        Assert(player.Health > 0, "魂姿触发后不应该把生命值留在0或以下");
    }

    private void TestJiAngRestoresHealthAfterKillingWanshaTarget()
    {
        var player = new Player("孙策", CharacterIds.SunCe, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        player.DebugSetHealth(30);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiAng)!);

        var wanshaEnemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "wansha_jjiang_regression_target",
            Name = "完杀测试目标",
            MaxHP = 10,
            Type = EnemyType.Elite,
            StartingResource = 1,
            StartingDeck = new EnemyDeck(),
            SkillIds = new System.Collections.Generic.List<string> { SkillIds.Wansha }
        });
        // 模拟原杀已实际命中并击杀完杀持有者后的 OnDamageTaken 时机。
        wanshaEnemy.MarkDead();

        var context = new BattleContext(player, new TriggerManager());
        context.BeginRoundResult();
        context.DamageEvent = new DamageEvent(player, wanshaEnemy, CardType.Kill, 10, isDirectAttackDamage: true)
        {
            ActualDamageDealt = 10
        };

        new JiAngBattleEffect().Execute(context);
        context.DamageEvent = null;

        Assert(player.Health == 35, "击杀持有完杀的目标后，激昂命中回血被错误跳过");
        Assert(context.RoundResult.PlayerHeal == 5, "激昂命中回血没有写入本回合治疗统计");
        Assert(context.RoundResult.Text.Contains("激昂：孙策回复5点生命。", StringComparison.Ordinal), "激昂命中回血缺少战报记录");
    }

    private void TestPlayerDamageBorderRequestsOnlyForActualPlayerDamage()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "damage_border_test_enemy",
            Name = "测试敌人",
            MaxHP = 20,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        triggerManager.Register(new PlayerDamageBorderTriggerEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();

        var requestCount = 0;
        var requestedDamage = 0;
        var requestedMaxHp = 0;
        context.PlayerDamageBorderRequested = (damage, maxHp) =>
        {
            requestCount++;
            requestedDamage = damage;
            requestedMaxHp = maxHp;
        };

        ApplyDamage(context, triggerManager, enemy, player, 10);
        Assert(requestCount == 1, "玩家实际受伤没有请求屏幕受伤边框");
        Assert(requestedDamage == 10 && requestedMaxHp == 40, "屏幕受伤边框请求的伤害/最大生命错误");

        ApplyDamage(context, triggerManager, enemy, player, 0);
        Assert(requestCount == 1, "0伤害不应请求屏幕受伤边框");

        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10, isDirectAttackDamage: true)
        {
            Cancelled = true
        };
        triggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
        Assert(requestCount == 1, "Cancelled伤害不应请求屏幕受伤边框");

        ApplyDamage(context, triggerManager, player, enemy, 10);
        Assert(requestCount == 1, "敌人受伤不应请求玩家屏幕受伤边框");

        player.Heal(5);
        Assert(requestCount == 1, "治疗不应请求屏幕受伤边框");
    }

    private static void ApplyDamage(BattleContext context, TriggerManager triggerManager, Player source, Player target, int damage)
    {
        context.DamageEvent = new DamageEvent(source, target, CardType.Kill, damage, isDirectAttackDamage: true);
        triggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        triggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
    }

    private static int FindFirstShopOfferIndex()
    {
        for (var i = 0; i < ShopManager.CurrentSlots.Count; i++)
        {
            if (ShopManager.CurrentSlots[i] != null)
            {
                return i;
            }
        }

        throw new InvalidOperationException("商店没有生成任何测试商品。");
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
