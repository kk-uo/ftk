using Godot;
using System;

/// <summary>验证第四章普通商店的传奇栏位资格与独立10%配置。</summary>
public partial class ChapterFourShopLegendaryRegression : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            GameManager.BeginNewRun();
            GameManager.SelectCharacter(CharacterIds.ZhaoYun);
            InventoryManager.Reset();

            var legendary = EquipmentDatabase.GetEquipment(EquipmentIds.XianHao)
                ?? throw new InvalidOperationException("测试前提失败：找不到仙毫");

            GameManager.DebugGoToChapter(3);
            ShopManager.GenerateSlots(ShopType.Normal);
            Assert(!ShopManager.CanAppearInShop(legendary), "第三章普通商店错误放行传奇装备");

            GameManager.DebugGoToChapter(4);
            ShopManager.GenerateSlots(ShopType.Normal);
            Assert(ShopManager.ChapterFourLegendarySlotChance == 0.10, "第四章传奇栏位概率不是10%");
            Assert(ShopManager.CanAppearInShop(legendary), "第四章普通商店没有放行可售传奇装备");

            // 每次生成四个槽位；连续生成200次，确保实际传奇抽取路径可达。
            var sawLegendary = false;
            for (var i = 0; i < 200; i++)
            {
                ShopManager.GenerateSlots(ShopType.Normal);
                foreach (var offer in ShopManager.CurrentSlots)
                {
                    if (offer?.Definition.Rarity == EquipmentRarity.Legendary)
                    {
                        sawLegendary = true;
                        break;
                    }
                }

                if (sawLegendary) break;
            }
            Assert(sawLegendary, "第四章普通商店从未进入传奇栏位抽取路径");

            GD.Print($"CHAPTER_FOUR_SHOP_LEGENDARY_TEST_PASS assertions={_assertions}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHAPTER_FOUR_SHOP_LEGENDARY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
