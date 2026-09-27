//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ZhangLiaoTuxiRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证张辽、突袭技能与专属卡牌数据。
// 2. 验证突袭逐目标复用南蛮入侵结算。
// 3. 验证只有实际伤害才通过统一顺手牵羊入口偷取费用。
//
// 不负责：
// × 验证最终动画美术。
// × 模拟完整地图与存档交互。
//
// 主要依赖：
// BattlePhaseResolutionEffect
// EnemyAI
// CardChoiceProvider
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 张辽【突袭】的 Headless 回归入口。
/// </summary>
public partial class ZhangLiaoTuxiRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行全部突袭回归并通过进程退出码报告结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestCharacterAndCardDefinitions();
            TestMultiTargetDamageAndSteal();
            TestTuxiStealsManaGeneratedByFee();
            TestNoDamageDoesNotSteal();
            TestCancelledDamageDoesNotSteal();
            TestShadowStateBlocksTuxiAndDirectEquipmentDamage();
            TestEnemyAiUsesTuxi();
            TestExclusiveCardIsNotRandomlyGranted();
            GD.Print($"ZHANGLIAO_TUXI_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ZHANGLIAO_TUXI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestCharacterAndCardDefinitions()
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.ZhangLiao);
        var skill = SkillDatabase.GetSkill(SkillIds.Tuxi);
        var card = Card.Tuxi();

        Assert(character.Name == "张辽", "张辽名称错误");
        Assert(character.Faction == Faction.Wei, "张辽阵营不是魏");
        Assert(character.MaxHp == 40, "张辽生命值不是40");
        Assert(character.SkillIds.SequenceEqual(new[] { SkillIds.Tuxi }), "张辽没有且仅有突袭作为默认技能");
        Assert(skill != null && skill.CharacterId == CharacterIds.ZhangLiao, "突袭技能未归属张辽");
        Assert(skill!.Rarity == SkillRarity.Rare, "突袭技能稀有度不是稀有");
        Assert(skill.Kinds.Contains(SkillKind.Active) && skill.Kinds.Contains(SkillKind.Card), "突袭技能类型不完整");
        Assert(card.Cost == 2, "突袭费用不是2");
        Assert(card.Categories.HasFlag(CardCategory.Trick), "突袭不是锦囊牌");
        Assert(card.TargetType == CardTargetType.NonTargeted, "突袭没有使用全体目标规则");
    }

    private void TestMultiTargetDamageAndSteal()
    {
        var player = CreateZhangLiao(5);
        var damagedWithMana = CreateEnemy("tuxi_target_a", 3);
        var counteredByKill = CreateEnemy("tuxi_target_b", 3);
        var damagedWithoutMana = CreateEnemy("tuxi_target_c", 0);
        var context = CreateContext(player, damagedWithMana, counteredByKill, damagedWithoutMana);
        var presentationRequests = new List<SkillTriggerPresentationRequest>();
        context.PlayerSkillPresentationRequested = presentationRequests.Add;
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Tuxi());
        context.SetActionForEnemy(damagedWithMana, BattleAction.FromCard(Card.Fee()));
        context.SetActionForEnemy(counteredByKill, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(damagedWithoutMana, BattleAction.FromCard(Card.Dodge()));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(damagedWithMana.Health == 30, "突袭没有按南蛮规则伤害使用费的目标");
        Assert(counteredByKill.Health == 40, "使用杀响应突袭的目标错误受伤");
        Assert(damagedWithoutMana.Health == 30, "突袭没有按南蛮规则穿透闪");
        Assert(damagedWithMana.CurrentMana == 0, "突袭没有偷走目标原有费用及本回合通过费获得的费用");
        Assert(counteredByKill.CurrentMana == 2, "未受伤目标仍触发了顺手牵羊");
        Assert(player.CurrentMana == 8, "突袭扣除2费、偷取目标结算后的4费并从0费目标获得1费补偿后的玩家费用错误");
        Assert(context.RoundResult.PlayerManaGain == 1, "突袭命中0费目标后没有记录1费补偿");
        Assert(context.RoundResult.Text.Contains("没有费用", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("获得1费", StringComparison.Ordinal),
            "突袭0费目标补偿缺少战报反馈");
        Assert(presentationRequests.Count == 1 && presentationRequests[0].SkillId == SkillIds.Tuxi, "突袭成功后没有且仅有一次技能大字请求");
        Assert(context.RoundResult.Text.Contains("顺手牵羊", StringComparison.Ordinal), "突袭成功后没有复用顺手牵羊战报");
    }

    private void TestTuxiStealsManaGeneratedByFee()
    {
        var player = CreateZhangLiao(2);
        var target = CreateEnemy("tuxi_fee_target", 0);
        var context = CreateContext(player, target);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Tuxi());
        context.SetActionForEnemy(target, BattleAction.FromCard(Card.Fee()));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(target.Health == 30, "突袭对使用费的0费目标没有造成伤害");
        Assert(target.CurrentMana == 0, "突袭没有偷走目标本回合通过费获得的1费");
        Assert(player.CurrentMana == 1, "突袭没有把目标本回合通过费获得的1费转给使用者");
        Assert(context.RoundResult.StealResolutions.Any(result => result.Resolved && result.Amount == 1),
            "突袭附带顺手牵羊没有记录偷取1费");
        Assert(!context.RoundResult.Text.Contains("没有费用", StringComparison.Ordinal),
            "目标已经通过费获得1费，却错误触发了0费补偿");
    }

    private void TestNoDamageDoesNotSteal()
    {
        var player = CreateZhangLiao(4);
        var target = CreateEnemy("tuxi_kill_counter", 4);
        var context = CreateContext(player, target);
        var presentationCount = 0;
        context.PlayerSkillPresentationRequested = _ => presentationCount++;
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Tuxi());
        context.SetActionForEnemy(target, BattleAction.FromCard(Card.Kill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(target.Health == 40, "杀响应突袭时目标仍受到伤害");
        Assert(target.CurrentMana == 3, "未造成伤害的突袭仍偷取目标费用");
        Assert(presentationCount == 0, "未造成伤害的突袭错误播放技能大字");
    }

    private void TestCancelledDamageDoesNotSteal()
    {
        var player = CreateZhangLiao(4);
        var target = CreateEnemy("tuxi_immune_target", 4);
        var triggerManager = CreateTriggerManager();
        triggerManager.Register(new CancelTuxiDamageEffect());
        var context = CreateContext(player, triggerManager, target);
        var presentationCount = 0;
        context.PlayerSkillPresentationRequested = _ => presentationCount++;
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Tuxi());
        context.SetActionForEnemy(target, BattleAction.FromCard(Card.Dodge()));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(target.Health == 40, "免疫后的突袭仍造成伤害");
        Assert(target.CurrentMana == 4, "最终伤害为0时突袭仍触发顺手牵羊");
        Assert(presentationCount == 0, "最终伤害为0时突袭错误播放技能大字");
    }

    private void TestShadowStateBlocksTuxiAndDirectEquipmentDamage()
    {
        var player = CreateZhangLiao(2);
        var shadowTarget = CreateEnemy("tuxi_shadow_target", 3);
        shadowTarget.EnterShadowState();

        var triggerManager = new TriggerManager();
        triggerManager.Register(new YingXiInvincibilityEffect());
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var context = CreateContext(player, triggerManager, shadowTarget);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Tuxi());
        context.SetActionForEnemy(shadowTarget, BattleAction.FromCard(Card.ShadowLurk()));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(shadowTarget.Health == shadowTarget.MaxHealth,
            "影袭状态仍受到突袭伤害");
        Assert(shadowTarget.CurrentMana == 3,
            "突袭未造成实际伤害时仍错误偷取影袭目标费用");

        // 模拟重锤、毒药等直接调用 TakeDamage 的装备伤害入口。
        shadowTarget.TakeDamage(80);
        Assert(shadowTarget.Health == shadowTarget.MaxHealth,
            "影袭状态被绕过伤害管线的装备直伤扣除了生命");
    }

    private void TestEnemyAiUsesTuxi()
    {
        var definition = new EnemyDefinition
        {
            Id = "zhangliao_ai",
            Name = "张辽AI",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 2,
            StartingDeck = new EnemyDeck(),
            SkillIds = new List<string> { SkillIds.Tuxi }
        };
        var enemy = new EnemyInstance(definition);
        var opponent = new Player("玩家", "ai_opponent", BattleTeam.Player);
        opponent.ResetForNewBattle(40, 40, 3);
        var ai = new EnemyAI();

        Assert(ai.IsCardAvailableThisTurn(enemy, definition, CardType.Tuxi), "AI张辽的行动栏没有突袭");
        Assert(ai.SelectAction(enemy, opponent, definition).Type == CardType.Tuxi, "AI张辽无法选择突袭");
    }

    private void TestExclusiveCardIsNotRandomlyGranted()
    {
        var choices = new CardChoiceProvider
        {
            Count = Enum.GetValues<CardType>().Length,
            Randomize = false
        }.CreateChoices();

        Assert(choices.All(choice => choice.Id != CardType.Tuxi.ToString()), "突袭进入了通用随机卡牌池");
        foreach (var character in CharacterDatabase.GetAllCharacters())
        {
            if (character.Id != CharacterIds.ZhangLiao)
            {
                Assert(!character.SkillIds.Contains(SkillIds.Tuxi), $"{character.Name}错误拥有突袭");
            }
        }
    }

    private static Player CreateZhangLiao(double mana)
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.ZhangLiao);
        var player = new Player(character.Name, character.Id, BattleTeam.Player);
        player.ResetForNewBattle(character.MaxHp, character.MaxHp, mana);
        player.SetCharacter(character);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Tuxi)!);
        return player;
    }

    private static EnemyInstance CreateEnemy(string id, int mana)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = id,
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = mana,
            StartingDeck = new EnemyDeck()
        });
    }

    private static TriggerManager CreateTriggerManager()
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        return triggerManager;
    }

    private static BattleContext CreateContext(Player player, params EnemyInstance[] enemies)
    {
        return CreateContext(player, CreateTriggerManager(), enemies);
    }

    private static BattleContext CreateContext(Player player, TriggerManager triggerManager, params EnemyInstance[] enemies)
    {
        var encounter = new BattleEncounter();
        encounter.Enemies.AddRange(enemies);
        return new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnNumber = 1
        };
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CancelTuxiDamageEffect : IBattleEffect
    {
        public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
        public EffectPriority Priority => EffectPriority.Immediate;

        public void Execute(BattleContext context)
        {
            if (context.DamageEvent?.AttackType == CardType.Tuxi)
            {
                context.DamageEvent.Cancelled = true;
            }
        }
    }
}
