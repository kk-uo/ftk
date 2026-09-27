//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/EquipmentDescriptionRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证所有装备都有独立的外观/使用说明。
// 2. 验证所有装备效果都通过效果文本接口显示。
// 3. 验证装备描述不会回退为装备功能描述。
//
// 不负责：
// × 验证图鉴的像素布局。
// × 验证装备战斗效果。
//
// 主要依赖：
// EquipmentDatabase
// Localization
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 装备描述与装备功能分离规则的 Headless 回归入口。
/// </summary>
public partial class EquipmentDescriptionRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 验证中英文环境下所有装备均能分别生成描述与功能文本。
    /// </summary>
    public override void _Ready()
    {
        var originalLanguage = Localization.CurrentLanguage;
        try
        {
            Localization.Initialize();
            originalLanguage = Localization.CurrentLanguage;
            VerifyLanguage("zh_CN");
            VerifyLanguage("en_US");
            GD.Print($"EQUIPMENT_DESCRIPTION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EQUIPMENT_DESCRIPTION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            Localization.SetLanguage(originalLanguage);
        }
    }

    private void VerifyLanguage(string language)
    {
        Localization.SetLanguage(language);
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            var dedicatedFlavor = Localization.GetOrFallback(definition.FlavorDescriptionKey, string.Empty);
            Assert(!string.IsNullOrWhiteSpace(dedicatedFlavor), $"{language}/{definition.Id} 缺少专属装备描述键");

            var flavor = Localization.GetEquipmentFlavorDescription(definition);
            Assert(!string.IsNullOrWhiteSpace(flavor), $"{language}/{definition.Id} 缺少装备描述");
            Assert(!flavor.StartsWith("【Missing:", StringComparison.Ordinal), $"{language}/{definition.Id} 装备描述缺少本地化");
            Assert(flavor != Localization.GetDescription(definition), $"{language}/{definition.Id} 装备描述仍与功能总描述相同");

            for (var index = 0; index < definition.UnlockConditions.Count; index++)
            {
                var source = Localization.GetEquipmentUnlockCondition(definition, index);
                Assert(!string.IsNullOrWhiteSpace(source), $"{language}/{definition.Id}/source.{index + 1} 缺少装备来源");
                Assert(!source.StartsWith("【Missing:", StringComparison.Ordinal), $"{language}/{definition.Id}/source.{index + 1} 显示了Missing占位符");
                if (language == "en_US")
                {
                    Assert(!ContainsCjk(source), $"en_US/{definition.Id}/source.{index + 1} 仍显示中文来源文本：{source}");
                }
            }

            for (var index = 0; index < definition.Effects.Count; index++)
            {
                var effect = Localization.GetEquipmentEffectDescription(definition, index);
                Assert(!string.IsNullOrWhiteSpace(effect), $"{language}/{definition.Id}/effect.{index + 1} 缺少装备功能");
                Assert(!effect.StartsWith("【Missing:", StringComparison.Ordinal), $"{language}/{definition.Id}/effect.{index + 1} 未正确回退");
                if (language == "en_US")
                {
                    Assert(!ContainsCjk(effect), $"en_US/{definition.Id}/effect.{index + 1} 仍显示中文装备效果：{effect}");
                }
            }
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
