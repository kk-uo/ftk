//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ActionCardCapacityRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证行动栏固定提供11个槽位。
// 2. 验证数字快捷键仍只绑定前10个槽位。
// 3. 验证溢出移除候选可包含角色专属行动牌。
// 4. 验证移除状态在当前Run持续，并在返回主菜单或新Run时重置。
//
// 不负责：
// × 模拟ChoicePanel视觉点击。
// × 验证卡牌布局的像素位置。
//
// 主要依赖：
// BattleHotkeySystem
// CardChoiceProvider
// GameManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 行动牌容量与溢出移除规则的 Headless 回归入口。
/// </summary>
public partial class ActionCardCapacityRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行行动牌容量回归测试，并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestFixedSlotAndHotkeyCounts();
            TestOverflowProviderIncludesAllCurrentCards();
            TestRemovedCardRunLifetime();
            GD.Print($"ACTION_CARD_CAPACITY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ACTION_CARD_CAPACITY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestFixedSlotAndHotkeyCounts()
    {
        Assert(BattleHotkeySystem.ActionSlotCount == 11, "行动栏固定槽位数量不是11");
        Assert(BattleHotkeySystem.MaxActionHotkeys == 10, "数字快捷键数量不再是1至0共10个");
        Assert(BattleHotkeySystem.GetKeyForActionIndex(9) == Key.Key0, "第10槽没有绑定数字键0");
        Assert(BattleHotkeySystem.GetKeyForActionIndex(10) == Key.None, "第11槽不应错误占用数字快捷键");
        Assert(BattleHotkeySystem.GetDisplayTextForActionIndex(10) == string.Empty, "第11槽不应显示错误数字提示");
    }

    private void TestOverflowProviderIncludesAllCurrentCards()
    {
        var pool = new[]
        {
            CardType.Fee,
            CardType.Dodge,
            CardType.Kill,
            CardType.FireKill,
            CardType.ThunderKill,
            CardType.Peach,
            CardType.Wine,
            CardType.Steal,
            CardType.Unassailable,
            CardType.SureKill,
            CardType.IceKill,
            CardType.Tuxi
        };

        var options = new CardChoiceProvider
        {
            Count = pool.Length,
            Randomize = false,
            IncludeCharacterExclusive = true,
            CardPool = pool
        }.CreateChoices();

        Assert(options.Count == 12, "溢出移除界面没有列出全部12张当前行动牌");
        Assert(options.Any(option => option.Payload is CardType.Tuxi), "角色专属行动牌未进入溢出移除候选");
        Assert(options.Select(option => option.Id).Distinct().Count() == options.Count, "溢出移除候选出现重复牌型");
    }

    private void TestRemovedCardRunLifetime()
    {
        GameManager.BeginNewRun();
        GameManager.AddPlayerCardType(CardType.ArrowBarrage);
        Assert(GameManager.HasPlayerCardType(CardType.ArrowBarrage), "测试行动牌未成功加入Run牌池");

        GameManager.RemovePlayerCardType(CardType.ArrowBarrage);
        Assert(GameManager.IsPlayerCardTypeRemoved(CardType.ArrowBarrage), "移除选择没有写入Run状态");
        Assert(!GameManager.HasPlayerCardType(CardType.ArrowBarrage), "已移除行动牌仍被当前Run判定为可用");

        GameManager.RemovePlayerCardType(CardType.Tuxi);
        Assert(GameManager.IsPlayerCardTypeRemoved(CardType.Tuxi), "技能提供的行动牌无法记录移除状态");

        GameManager.EndRun();
        Assert(!GameManager.IsPlayerCardTypeRemoved(CardType.ArrowBarrage), "返回主菜单没有重置普通行动牌移除状态");
        Assert(!GameManager.IsPlayerCardTypeRemoved(CardType.Tuxi), "返回主菜单没有重置技能行动牌移除状态");

        GameManager.RemovePlayerCardType(CardType.ArrowBarrage);
        GameManager.BeginNewRun();
        Assert(!GameManager.IsPlayerCardTypeRemoved(CardType.ArrowBarrage), "新Run没有重置普通行动牌移除状态");
        Assert(!GameManager.IsPlayerCardTypeRemoved(CardType.Tuxi), "新Run没有重置技能行动牌移除状态");
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
