//////////////////////////////////////////////////////////
// 徐盛【破军】【疑城】回归测试。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class XuShengSkillRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestCharacterDefinition();
            TestYiChengResourceHealFixed();
            TestYiChengDamageManaEveryHit();
            TestPoJunUsesAdditiveMultiplier();
            TestPoJunReactionCard();
            TestPoJunPlayerHitQueuesReaction();
            TestPoisonDanConvertsBattleHealing();
            GD.Print($"XUSHENG_SKILL_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"XUSHENG_SKILL_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestCharacterDefinition()
    {
        var data = CharacterDatabase.GetCharacter(CharacterIds.XuSheng);
        Assert(data.MaxHp == 40 && data.Faction == Faction.Wu, "徐盛必须是40生命的吴阵营角色");
        Assert(data.SkillIds.Contains(SkillIds.PoJun) && data.SkillIds.Contains(SkillIds.YiCheng), "徐盛应默认携带破军和疑城");
    }

    private void TestYiChengResourceHealFixed()
    {
        GameManager.BeginNewRun();
        var player = CreateXuSheng(10, 40);
        var manager = new TriggerManager();
        manager.Register(new YiChengResourceHealEffect());
        var context = new BattleContext(player, manager);
        context.BeginRoundResult();

        BattleRules.PayManaAndRaiseResourceChanged(context, player, 1, true);
        Assert(player.Health == 45, "疑城首次失去费用时应固定回复5生命");

        BattleRules.PayManaAndRaiseResourceChanged(context, player, 1, true);
        Assert(player.Health == 50, "疑城每次失去费用都应固定回复5生命");
    }

    private void TestYiChengDamageManaEveryHit()
    {
        GameManager.BeginNewRun();
        var player = CreateXuSheng(0, 80);
        var enemy = new Player("测试敌人", "xusheng_test_enemy", BattleTeam.Enemy);
        enemy.ResetForNewBattle(100, 100, 0);
        var manager = new TriggerManager();
        manager.Register(new ApplyDamageEffect());
        manager.Register(new YiChengDamageManaEffect());
        var context = new BattleContext(player, manager);
        context.BeginRoundResult();

        ResolveDamage(context, enemy, player, 10);
        Assert(player.CurrentMana == 0.25, "疑城每次受伤应获得0.25费");

        ResolveDamage(context, enemy, player, 10);
        Assert(player.CurrentMana == 0.5, "疑城受伤回费同一回合可重复触发");

        context.TurnCounter++;
        ResolveDamage(context, enemy, player, 10);
        Assert(player.CurrentMana == 0.75, "疑城进入下一回合后仍应按每次受伤获得0.25费");
    }

    private void TestPoJunUsesAdditiveMultiplier()
    {
        var target = new Player("测试目标", "xusheng_test_target", BattleTeam.Player);
        target.ResetForNewBattle(100, 100, 0);
        var source = new Player("徐盛AI", "xusheng_test_ai", BattleTeam.Enemy);
        source.ResetForNewBattle(100, 100, 2);
        source.AddSkill(SkillDatabase.GetSkill(SkillIds.PoJun)!);
        var manager = new TriggerManager();
        manager.Register(new PoJunEnemyDamageEffect());
        var context = new BattleContext(target, manager);
        context.BeginRoundResult();
        context.DamageEvent = new DamageEvent(source, target, CardType.Kill, 10);
        manager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.DamageEvent!.ResolveModifiers();

        Assert(context.DamageEvent.Amount == 50, "2费可触发4次破军，应为原始伤害×5而非指数翻倍");
        Assert(source.CurrentMana == 0, "破军每次触发应实际失去0.5费");
    }

    private void TestPoJunReactionCard()
    {
        var target = new Player("测试目标", "pojun_reaction_target", BattleTeam.Enemy);
        target.ResetForNewBattle(100, 100, 0);
        var reaction = new BreakArmyReaction(1, 10, CardType.Kill, target);
        var cardOptions = reaction.Options.FindAll(option => option.CardType.HasValue);

        Assert(cardOptions.Count == 1, "破军反应条只能显示一张【破军】卡牌");
        Assert(cardOptions[0].CardType == CardType.PoJunBoost
               && cardOptions[0].ReactionCost == 0.5
               && cardOptions[0].Enabled,
            "破军反应卡应消耗0.5费且在费用足够时可用");
        Assert(BattleRules.GetCardName(CardType.PoJunBoost) == "破军"
               && new Card(CardType.PoJunBoost).Description == "增加一倍伤害",
            "破军反应卡名称和描述必须与技能规则一致");
    }

    private void TestPoJunPlayerHitQueuesReaction()
    {
        var player = new Player("徐盛", CharacterIds.XuSheng, BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 1.5);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.PoJun)!);

        var target = new Player("测试敌人", "pojun_player_hit_target", BattleTeam.Enemy);
        target.ResetForNewBattle(100, 100, 0);

        var manager = new TriggerManager();
        manager.Register(new ApplyDamageEffect());
        manager.Register(new PoJunPlayerCaptureEffect());
        var context = new BattleContext(player, manager);
        context.BeginRoundResult();
        context.DamageEvent = new DamageEvent(player, target, CardType.Kill, 10);

        manager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        Assert(context.PoJunPendingAmount == 10, "徐盛的杀实际命中后必须记录破军待追加伤害");

        var reaction = context.Reactions.Dequeue();
        Assert(reaction is BreakArmyReaction && reaction.Timing == TriggerTiming.OnDamageTaken,
            "命中且剩余费用至少0.5时，破军必须立即进入反应队列，而非等待战斗回合后");
    }

    private void TestPoisonDanConvertsBattleHealing()
    {
        GameManager.BeginNewRun();
        var poisonDan = GameManager.AddEquipment(EquipmentIds.PoisonDan)
            ?? throw new InvalidOperationException("毒丹无法加入背包");
        Assert(InventoryManager.EquipToSlot(poisonDan.InstanceId, EquipmentSlot.Accessory1), "毒丹无法装备到饰品槽");

        var player = CreateXuSheng(0, 40);
        var context = new BattleContext(player, new TriggerManager());
        context.BeginRoundResult();
        var result = BattleHealing.Apply(
            context,
            player,
            10,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "测试治疗", "test_heal", player));

        Assert(result.HealedAmount == 0 && result.ConvertedDamage == 10, "毒丹应将10点治疗转换为10点生命损失");
        Assert(player.Health == 30 && player.MaxHealth == 101, "毒丹转换后应只增加1点最大生命，不能顺带回血");
    }

    private static Player CreateXuSheng(double mana, int health)
    {
        var player = new Player("徐盛", CharacterIds.XuSheng, BattleTeam.Player);
        player.ResetForNewBattle(100, health, mana);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.YiCheng)!);
        return player;
    }

    private static void ResolveDamage(BattleContext context, Player source, Player target, int amount)
    {
        context.DamageEvent = new DamageEvent(source, target, CardType.Kill, amount);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
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
