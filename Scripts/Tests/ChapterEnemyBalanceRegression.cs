//////////////////////////////////////////////////////////
// 第二、三章指定敌人的数值与路线配置回归。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

public partial class ChapterEnemyBalanceRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestGiantMechCockroach();
            TestBenevolentKingGuard();
            TestGiantPusSacCombatProfile();
            TestRoyalDeathGuardShadowKillReserve();
            GD.Print($"CHAPTER_ENEMY_BALANCE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHAPTER_ENEMY_BALANCE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestGiantMechCockroach()
    {
        var enemy = EnemyDatabase.GetEnemy("giant_mech_cockroach")
            ?? throw new InvalidOperationException("缺少巨型机械蟑螂");
        Assert(enemy.MaxHP == 120, "巨型机械蟑螂生命值不是120");
        Assert(enemy.Type == EnemyType.Normal && enemy.RewardTier == RewardTier.Rare,
            "巨型机械蟑螂的普通敌人/稀有奖励配置错误");
        Assert(enemy.EquipmentIds.SequenceEqual(new[] { EquipmentIds.Poison, EquipmentIds.Poison }),
            "巨型机械蟑螂没有携带毒药×2");
        Assert(enemy.StartingResource == 0 && !enemy.UseSharedHealthPool,
            "巨型机械蟑螂的初始费用或生命池配置错误");
    }

    private void TestBenevolentKingGuard()
    {
        var enemy = EnemyDatabase.GetEnemy("renwan_guard")
            ?? throw new InvalidOperationException("缺少仁王卫");
        Assert(enemy.MaxHP == 140, "仁王卫生命值不是140");
        Assert(enemy.StartingResource == 2 && !enemy.UseSharedHealthPool,
            "仁王卫的初始费用或生命池配置错误");
        Assert(enemy.Tags.Contains(EnemyTag.Qun) && enemy.Tags.Contains(EnemyTag.RouteImperial),
            "仁王卫缺少群阵营或皇宫路线标签");
        Assert(enemy.EquipmentIds.Count(id => id == EquipmentIds.BenevolentKing) == 1
            && enemy.EquipmentIds.Count(id => id == EquipmentIds.AttackChip) == 2,
            "仁王卫没有携带仁王与攻击芯片×2");
        Assert(enemy.ActionWeights.Slash > enemy.ActionWeights.Dodge
            && enemy.ActionWeights.FireSlash > enemy.ActionWeights.Dodge
            && enemy.ActionWeights.Slash > enemy.ActionWeights.Resource,
            "仁王卫 AI 没有保持连续攻击倾向");
    }

    private void TestGiantPusSacCombatProfile()
    {
        var definition = EnemyDatabase.GetEnemy("giant_pus_sac")
            ?? throw new InvalidOperationException("缺少巨型脓包");
        Assert(definition.StartingDeck.Cards.Contains(CardType.Kill)
            && definition.StartingDeck.Cards.Contains(CardType.FireKill),
            "巨型脓包卡组缺少可主动使用的攻击牌");
        Assert(definition.ActionWeights.Slash > definition.ActionWeights.Resource
            && definition.ActionWeights.FireSlash > 0,
            "巨型脓包 AI 没有保持攻击倾向");

        var pusSac = new EnemyInstance(definition);
        pusSac.GainMana(1);
        var available = new EnemyAI().GetAvailableActionCards(pusSac, definition).Select(card => card.Type).ToList();
        Assert(available.Contains(CardType.Kill), "巨型脓包拥有费用后仍无法使用普通杀攻击");

        var player = new Player("测试玩家", "test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        pusSac.DebugSetHealth(0);
        var manager = new TriggerManager();
        manager.Register(new DefenseBeforeDamageEffect());
        manager.Register(new ApplyDamageEffect());
        var context = new BattleContext(player, manager);
        context.BeginRoundResult();
        context.DamageEvent = new DamageEvent(player, pusSac, CardType.Kill, 60, isDirectAttackDamage: true);
        new ZiBaoEffect().Execute(context);
        Assert(pusSac.Health == 1
            && pusSac.RuntimeStates.TryGetValue("zibao_pending", out var pending)
            && pending is true,
            "巨型脓包首次濒死没有恢复至1点生命并进入自爆待发状态");

        context.DamageEvent = null;
        context.PlayerAction = BattleAction.FromCard(Card.Fee(), 1);
        context.SetActionForEnemy(pusSac, BattleAction.FromCard(Card.ZiBaoAttack(), 1, player));
        new BattlePhaseResolutionEffect().Execute(context);
        Assert(player.Health == player.MaxHealth - 24
            && !pusSac.IsDead
            && pusSac.Health == 1
            && pusSac.RuntimeStates.TryGetValue("zibao_triggered", out var released)
            && released is true,
            "巨型脓包自爆没有造成60×40%伤害，或释放后错误永久死亡");
    }

    private void TestRoyalDeathGuardShadowKillReserve()
    {
        var definition = EnemyDatabase.GetEnemy("royal_death_guard")
            ?? throw new InvalidOperationException("缺少皇家死侍");
        Assert(definition.StartingResource == 2, "皇家死侍没有保留发动影袭与影袭杀所需的初始费用");

        var guard = new EnemyInstance(definition);
        guard.PayMana(1); // 模拟发动影袭后仅剩/耗尽费用的边界情况。
        guard.EnterShadowState();
        guard.PayMana(guard.CurrentMana); // 强制复现过去的0费影袭状态。

        var available = new EnemyAI().GetAvailableActionCards(guard, definition).Select(card => card.Type).ToList();
        Assert(guard.CurrentMana >= Card.ShadowKill().Cost && available.Contains(CardType.ShadowKill),
            "皇家死侍进入影袭后没有补足影袭杀费用，影袭杀仍被可用性检查排除");
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
