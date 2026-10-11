//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/EventEquipmentRewardLocalizationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证事件获得装备提示使用当前语言的模板。
// 2. 验证英文提示不会回退或泄漏中文文本。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>事件装备奖励本地化的无头回归入口。</summary>
public partial class EventEquipmentRewardLocalizationRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        var originalLanguage = Localization.CurrentLanguage;
        try
        {
            Localization.Initialize();
            originalLanguage = Localization.CurrentLanguage;
            VerifyLanguage("zh_CN");
            VerifyLanguage("en_US");
            VerifyAllEventDefinitionsInEnglish();
            GD.Print($"EVENT_EQUIPMENT_REWARD_LOCALIZATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EVENT_EQUIPMENT_REWARD_LOCALIZATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            Localization.SetLanguage(originalLanguage);
        }
    }

    private void VerifyAllEventDefinitionsInEnglish()
    {
        Localization.SetLanguage("en_US");
        foreach (var eventData in EventDatabase.GetAllEvents())
        {
            AssertLocalizedEnglish(eventData.DisplayName, $"event {eventData.Id} name");
            AssertLocalizedEnglish(eventData.DisplayDescription, $"event {eventData.Id} description");
            for (var index = 0; index < eventData.Options.Count; index++)
            {
                var option = eventData.Options[index];
                AssertLocalizedEnglish(option.DisplayName, $"event {eventData.Id} option {index + 1} name");
                AssertLocalizedEnglish(option.DisplayDescription, $"event {eventData.Id} option {index + 1} description");
                if (!string.IsNullOrWhiteSpace(option.ResultText))
                {
                    AssertLocalizedEnglish(option.DisplayResultText, $"event {eventData.Id} option {index + 1} result");
                }
            }
        }
    }

    private void AssertLocalizedEnglish(string text, string context)
    {
        Assert(!string.IsNullOrWhiteSpace(text), $"{context} is empty");
        Assert(!text.StartsWith("【Missing:", StringComparison.Ordinal), $"{context} is missing a localization key: {text}");
        Assert(!ContainsCjk(text), $"{context} still contains Chinese: {text}");
    }

    private void VerifyLanguage(string language)
    {
        Localization.SetLanguage(language);
        GameManager.BeginNewRun();

        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.RustSword);
        Assert(definition != null, "奖励本地化测试缺少锈剑定义");

        var directResult = new AddEquipmentRewardAction(
            EquipmentIds.RustSword,
            EquipmentGainSource.EventReward).Execute();
        var expected = Localization.GetFmt(
            "event.reward.gain_equipment_fmt",
            Localization.GetName(definition!));

        Assert(directResult.PlayerMessage == expected,
            $"{language} 事件装备奖励提示未使用本地化模板：{directResult.PlayerMessage}");
        Assert(!directResult.PlayerMessage.StartsWith("【Missing:", StringComparison.Ordinal),
            $"{language} 事件装备奖励提示缺少本地化键");

        GameManager.BeginNewRun();
        var poolResult = new RandomEquipmentFromPoolRewardAction(EquipmentIds.RustSword).Execute();
        Assert(poolResult.PlayerMessage == expected,
            $"{language} 随机事件装备奖励提示未使用本地化模板：{poolResult.PlayerMessage}");

        if (language == "en_US")
        {
            Assert(!ContainsCjk(directResult.PlayerMessage),
                $"en_US 事件装备奖励提示仍含中文：{directResult.PlayerMessage}");
            Assert(!ContainsCjk(poolResult.PlayerMessage),
                $"en_US 随机事件装备奖励提示仍含中文：{poolResult.PlayerMessage}");
        }
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var character in text)
        {
            if (character is >= '\u4e00' and <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }
}
