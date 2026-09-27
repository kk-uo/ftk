//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DodgeParryPresentationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证玩家使用【闪】成功格挡时会留下结构化闪格挡结果。
// 2. 验证专属护盾边框固定覆盖全屏且不创建局部贴图。
// 3. 验证边框不接收鼠标输入，并在播放后自动归零隐藏。
//
// 不负责：
// × 评价美术风格与实际屏幕观感。
// × 验证其它护盾来源的战斗规则。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 玩家【闪】成功格挡与全屏边缘护盾表现的 Headless 回归入口。
/// </summary>
public partial class DodgeParryPresentationRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行闪格挡结果与全屏边缘护盾布局测试。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            TestDodgeBlockResult(CardType.Kill);
            TestDodgeBlockResult(CardType.FireKill);
            TestDodgeBlockResult(CardType.ArrowBarrage);
            TestFutureDodgeBlockAggregation();
            await TestShieldBorder();
            GD.Print($"DODGE_SHIELD_BORDER_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DODGE_SHIELD_BORDER_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDodgeBlockResult(CardType attackType)
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "dodge_parry_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager())
        {
            Encounter = encounter,
            PlayerLockedTarget = enemy,
            TurnNumber = 1,
            PlayerAction = BattleAction.FromCard(Card.Dodge(), 1, enemy)
        };
        context.SetActionForEnemy(enemy, BattleAction.FromCard(new Card(attackType), 1, player));
        context.BeginRoundResult();

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(context.PlayerAction?.IsDodge == true, "测试行动没有保持为【闪】");
        Assert(context.RoundResult.PlayerCardDodgeBlocks == 1,
            $"【闪】抵挡{BattleRules.GetCardName(attackType)}后没有写入统一格挡结果");
        Assert(context.RoundResult.PlayerUniversalBlocks == 0, "闪格挡被误记为通用护盾");
        Assert(context.RoundResult.FullBlockTargets.Contains(player),
            $"【闪】抵挡{BattleRules.GetCardName(attackType)}后没有记录完全格挡目标");
        Assert(player.Health == player.MaxHealth,
            $"成功使用【闪】抵挡{BattleRules.GetCardName(attackType)}后玩家仍然受到伤害");
    }

    private void TestFutureDodgeBlockAggregation()
    {
        var result = new RoundResult();
        result.AddPersistentDodgeBlock(true, CardType.CelestialImpact);
        Assert(result.PlayerCardDodgeBlocks == 1, "新增可闪避牌没有进入统一闪格挡结果");
        Assert(result.PlayerDodgeBlocks == 1, "新增可闪避牌没有回退到通用闪格挡日志");

        result.RemovePersistentDodgeBlock(true, CardType.CelestialImpact);
        Assert(result.PlayerCardDodgeBlocks == 0, "撤销闪格挡后统一结果没有同步回退");
    }

    private async System.Threading.Tasks.Task TestShieldBorder()
    {
        var viewportRoot = new Control
        {
            Name = "ViewportRoot",
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(viewportRoot);

        var shield = new DodgeShieldScreenBorder
        {
            Name = "DodgeShieldScreenBorder",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        viewportRoot.AddChild(shield);
        shield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        shield.ShowShield();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Assert(shield.Visible, "闪格挡后护盾边框没有显示");
        Assert(shield.MouseFilter == Control.MouseFilterEnum.Ignore, "护盾边框会拦截鼠标输入");
        Assert(shield.Size.IsEqualApprox(viewportRoot.Size), "护盾边框没有覆盖完整视口");
        Assert(shield.GetChildCount() == 0, "护盾边框仍创建了局部格挡贴图节点");
        Assert(shield.DebugIntensity > 0f, "护盾边框动画强度没有开始变化");

        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        Assert(shield.Visible, "闪护盾没有按要求延长0.5秒");
        Assert(shield.DebugIntensity > 0f, "延长阶段内闪护盾已提前完全淡出");

        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        Assert(!shield.Visible, "护盾边框播放结束后没有隐藏");
        Assert(Mathf.IsZeroApprox(shield.DebugIntensity), "护盾边框播放结束后强度没有归零");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount += 1;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
