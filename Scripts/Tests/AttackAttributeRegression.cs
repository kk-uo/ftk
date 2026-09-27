//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/AttackAttributeRegression.cs
//
// 覆盖攻击招式属性定义、无属性展示与局内火属性强化的卡面同步。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class AttackAttributeRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestElementalAttackAttributes();
            TestNoAttributeAttacksShowNoneTrait();
            TestAllAttackActionsExposeTrait();
            TestArrowBarrageFireUpgradeUpdatesTrait();
            GD.Print($"ATTACK_ATTRIBUTE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ATTACK_ATTRIBUTE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestElementalAttackAttributes()
    {
        Assert(new Card(CardType.FireKill).Attributes == AttackAttribute.Fire, "火杀应只有火属性");
        Assert(new Card(CardType.ThunderKill).Attributes == AttackAttribute.Thunder, "雷杀应只有雷属性");
        Assert(new Card(CardType.FireThunderKill).Attributes == (AttackAttribute.Fire | AttackAttribute.Thunder),
            "火雷杀应同时拥有火属性与雷属性");
        Assert(new Card(CardType.IceKill).Attributes == AttackAttribute.Ice, "冰杀应只有冰属性");
        Assert(new Card(CardType.PoisonKill).Attributes == AttackAttribute.Poison, "毒杀应只有毒属性");
        Assert(new Card(CardType.FireAttack).Attributes == AttackAttribute.Fire, "火攻应具有火属性");

        var fireThunderTrait = new Card(CardType.FireThunderKill).AttributeTraitText;
        Assert(fireThunderTrait.Contains("火属性", StringComparison.Ordinal)
            && fireThunderTrait.Contains("雷属性", StringComparison.Ordinal),
            "火雷杀卡面词条没有同时展示火属性与雷属性");
    }

    private void TestNoAttributeAttacksShowNoneTrait()
    {
        var kill = new Card(CardType.Kill);
        Assert(kill.Attributes == AttackAttribute.None, "普通杀应为无属性");
        Assert(kill.AttributeTraitText == "属性：无", "普通杀应显示“属性：无”词条");
        Assert(kill.DisplayTypeLabel.Contains("属性：无", StringComparison.Ordinal), "普通杀类型行没有显示无属性词条");
    }

    private void TestAllAttackActionsExposeTrait()
    {
        foreach (CardType type in Enum.GetValues(typeof(CardType)))
        {
            var card = new Card(type);
            if (!card.IsAttackAction)
            {
                continue;
            }

            Assert(!string.IsNullOrWhiteSpace(card.AttributeTraitText), $"攻击行动【{card.Name}】缺少属性词条");
        }
    }

    private void TestArrowBarrageFireUpgradeUpdatesTrait()
    {
        GameManager.BeginNewRun();
        var arrowBarrage = new Card(CardType.ArrowBarrage);
        Assert(arrowBarrage.Attributes == AttackAttribute.None, "未强化的万箭齐发应为无属性");

        GameManager.GrantArrowBarrageFireUpgrade();
        Assert(arrowBarrage.Attributes == AttackAttribute.Fire, "火属性强化后的万箭齐发应具有火属性");
        Assert(arrowBarrage.AttributeTraitText.Contains("火属性", StringComparison.Ordinal),
            "火属性强化后的万箭齐发卡面没有同步显示火属性");
        GameManager.BeginNewRun();
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
