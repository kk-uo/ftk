//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CodexEquipmentEffectLocalizationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证装备"效果规则"文本（图鉴详情、背包信息面板共用）会走本地化查表
//    Localization.GetEquipmentEffectDescription（equipment.<id>.effect.<序号>），
//    而不是永远显示硬编码的中文 EquipmentEffect.Description（原bug：切到英文
//    语言后这部分文本仍然固定显示中文，本地化完全没有生效）。
// 2. 验证还没来得及补充效果词条的装备会安全回退到硬编码文本，
//    不会显示"【Missing: ...】"或空白。
//
// 不负责：
// × 验证图鉴/背包UI的视觉布局。
// × 校验全部装备效果译文的具体措辞是否精确。
//
// 主要依赖：
// Localization.GetEquipmentEffectDescription
// EquipmentDatabase
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 装备"效果规则"本地化的 Headless 回归入口。
/// </summary>
public partial class CodexEquipmentEffectLocalizationRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();

            TestKnownKeyUsesLocalizedTextInBothLanguages();
            TestMissingKeyFallsBackToHardcodedText();
            TestAllEquipmentEffectsResolveWithoutMissingPlaceholder();

            GD.Print($"CODEX_EQUIPMENT_EFFECT_LOCALIZATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CODEX_EQUIPMENT_EFFECT_LOCALIZATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    // 排箫（PanXiao）已有对应的 equipment.accessory_pan_xiao.effect.1 词条，且中英文本
    // 都跟硬编码的 EquipmentEffect.Description（中文）不同——用它验证查表确实生效，
    // 而不是巧合地和硬编码文本长得一样。
    private void TestKnownKeyUsesLocalizedTextInBothLanguages()
    {
        var def = EquipmentDatabase.GetEquipment(EquipmentIds.PanXiao)
            ?? throw new InvalidOperationException("找不到排箫装备定义");
        Assert(def.Effects.Count > 0, "排箫没有配置任何 EquipmentEffect");
        var hardcodedText = def.Effects[0].Description;

        Localization.SetLanguage("zh_CN");
        var zhText = Localization.GetEquipmentEffectDescription(def, 0);
        Assert(zhText == Localization.Get("equipment.accessory_pan_xiao.effect.1"),
            "中文环境下效果文本没有走本地化查表");

        Localization.SetLanguage("en_US");
        var enText = Localization.GetEquipmentEffectDescription(def, 0);
        Assert(enText == Localization.Get("equipment.accessory_pan_xiao.effect.1"),
            "英文环境下效果文本没有走本地化查表");
        Assert(enText != hardcodedText,
            "英文环境下效果文本仍然显示硬编码中文，本地化没有生效（原bug）");

        Localization.SetLanguage("zh_CN");
    }

    // 构造一个查表必定查不到的临时 EquipmentDefinition（用一个不存在的 descriptionKey 前缀），
    // 确认查不到词条时会老老实实回退到硬编码文本，而不是抛异常或返回 Missing 占位符。
    private void TestMissingKeyFallsBackToHardcodedText()
    {
        var fakeDef = new EquipmentDefinition(
            "test_fake_equipment_no_effect_key",
            "测试装备",
            "测试描述",
            new[] { EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[] { new EquipmentEffect("硬编码回退文本", 0) },
            EquipmentAcquisitionMethod.Debug,
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            0,
            nameKey: "equipment.__nonexistent_test_stub__.name",
            descriptionKey: "equipment.__nonexistent_test_stub__.desc");

        var result = Localization.GetEquipmentEffectDescription(fakeDef, 0);
        Assert(result == "硬编码回退文本", "查不到本地化词条时没有正确回退到硬编码文本");
        Assert(!result.StartsWith("【Missing", StringComparison.Ordinal), "回退结果不应该出现Missing占位符");
    }

    // 全量扫描：每一件装备的每一条 Effect 都跑一遍查表，确认结果既不是空字符串，
    // 也不是 Localization 的 Missing 占位符——查不到词条时必须干净地回退到硬编码文本。
    private void TestAllEquipmentEffectsResolveWithoutMissingPlaceholder()
    {
        var checkedCount = 0;
        foreach (var def in EquipmentDatabase.GetAllEquipments())
        {
            for (var i = 0; i < def.Effects.Count; i++)
            {
                var text = Localization.GetEquipmentEffectDescription(def, i);
                checkedCount++;
                Assert(!string.IsNullOrWhiteSpace(text), $"{def.Id} 第{i + 1}条效果解析出空文本");
                Assert(!text.StartsWith("【Missing", StringComparison.Ordinal), $"{def.Id} 第{i + 1}条效果显示了Missing占位符");
            }
        }

        Assert(checkedCount > 0, "测试前提失败：没有扫描到任何装备效果条目");
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
