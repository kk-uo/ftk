//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/HuaTuoQingnangRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证华佗基础资料与【青囊】技能定义。
// 2. 验证桃按1费结算，格挡成功后仍然回复生命。
// 3. 验证桃可超出最大生命回复，且超量部分不会带出本场战斗。
//
// 不负责：
// × 模拟完整战斗界面。
// × 验证桃与所有装备组合的数值。
//
// 主要依赖：
// BattlePhaseResolutionEffect
// BattleTriggerEffects
// GameManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 华佗【青囊】完整战斗结算的 Headless 回归入口。
/// </summary>
public partial class HuaTuoQingnangRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行【青囊】回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestCharacterAndSkillDefinition();
            TestBlockedPeachStillOverheals();
            TestUnblockedPeachOverheals();
            TestOverhealExpiresAfterBattle();
            GD.Print($"HUA_TUO_QINGNANG_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"HUA_TUO_QINGNANG_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestCharacterAndSkillDefinition()
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.HuaTuo);
        var skill = SkillDatabase.GetSkill(SkillIds.Qingnang);

        Assert(character.Gender == Gender.Male, "华佗性别不是男性");
        Assert(character.Faction == Faction.Qun, "华佗阵营不是群雄");
        Assert(character.MaxHp == 30, "华佗最大生命值不是30");
        Assert(character.SkillIds.Contains(SkillIds.Qingnang), "华佗没有默认携带【青囊】");
        Assert(skill != null && skill.Rarity == SkillRarity.Rare, "【青囊】不是稀有技能");
        Assert(skill!.Kinds.Contains(SkillKind.Passive), "【青囊】不是被动技能");
    }

    private void TestBlockedPeachStillOverheals()
    {
        ResetRun();
        var player = CreateHuaTuo(30, 30, 2);
        var enemy = CreateEnemy(CardType.Kill);
        var context = CreateContext(player, enemy, Card.Peach(), Card.Kill());

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(Math.Abs(player.CurrentMana - 1) < 0.001, "【青囊】桃没有按1费扣除");
        Assert(player.Health == 40, "【青囊】桃格挡成功后没有保留10点回复或未允许超量回复");
        Assert(context.PlayerPeachShieldLayers == 0, "桃成功格挡后护盾层没有消耗");
        Assert(context.RoundResult.PlayerUniversalBlocks == 1, "桃没有成功记录格挡");
    }

    private void TestUnblockedPeachOverheals()
    {
        ResetRun();
        var player = CreateHuaTuo(30, 25, 2);
        var enemy = CreateEnemy(CardType.Fee);
        var context = CreateContext(player, enemy, Card.Peach(), Card.Fee());

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == 35, "【青囊】桃在未格挡时没有超过30点生命上限回复");
        Assert(Math.Abs(player.CurrentMana - 1) < 0.001, "未格挡时桃费用不是1");
    }

    private void TestOverhealExpiresAfterBattle()
    {
        ResetRun();
        var player = CreateHuaTuo(30, 30, 2);
        player.Heal(10, allowOverheal: true);
        Assert(player.Health == 40, "测试前提失败：未产生超量生命");

        GameManager.SaveBattleState(player.Health, player.CurrentMana);
        Assert(GameManager.CurrentHP == 30, "【青囊】超量生命被错误保留到战斗外");
        Assert(GameManager.MaxHP == 30, "清理超量生命时错误修改了最大生命值");
    }

    private static BattleContext CreateContext(
        Player player,
        EnemyInstance enemy,
        Card playerCard,
        Card enemyCard)
    {
        var triggerManager = BattleTriggerEffects.CreateDefaultManager();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnCounter = 1,
            PlayerAction = BattleAction.FromCard(playerCard)
        };
        context.SetActionForEnemy(enemy, BattleAction.FromCard(enemyCard, 1, player));
        context.BeginRoundResult();
        return context;
    }

    private static Player CreateHuaTuo(int maxHealth, int health, double mana)
    {
        var player = new Player("华佗", CharacterIds.HuaTuo, BattleTeam.Player);
        player.ResetForNewBattle(maxHealth, health, mana);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Qingnang)!);
        return player;
    }

    private static EnemyInstance CreateEnemy(CardType cardType)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = $"qingnang_test_{cardType}",
            Name = "测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 2,
            StartingDeck = new EnemyDeck()
        });
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.HuaTuo);
        InventoryManager.Reset();
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
