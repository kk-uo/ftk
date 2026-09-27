//////////////////////////////////////////////////////////
// 文件：Scripts/ShopManager.cs
//
// 模块：Shop System
//
// 职责：
// 1. 承载商店商品池、购买流程与商店界面相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

/// <summary>
/// Shop System 的公开枚举：ShopType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum ShopType
{
    Normal,
    Vehicle,
    Witch,
    BlackMarket
}

// 商店中的一个商品（装备 + 购买价格）。
/// <summary>
/// Shop System 的公开类：ShopOffer。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ShopOffer
{
    /// <summary>
    /// Shop System 的公开入口：ShopOffer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public ShopOffer(EquipmentDefinition definition, int price)
    {
        Definition = definition;
        Price = price;
    }

    public EquipmentDefinition Definition { get; }
    public int Price { get; }
}

// Shop System V1：4个商品槽位，每次进入商店重新生成；售出后保持空白，不自动补货。
// 仅读取 EquipmentDatabase / InventoryManager 的既有公开方法，不修改背包与装备系统本身。
/// <summary>
/// Shop System 的公开类：ShopManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class ShopManager
{
    public const int SlotCount = 4;
    /// <summary>第四章普通商店中，每一个商品槽位独立刷新传奇装备的概率。</summary>
    public const double ChapterFourLegendarySlotChance = 0.10;

    private static readonly Random Random = new();
    private static readonly ShopOffer?[] Slots = new ShopOffer?[SlotCount];
    private static bool _hasRefreshedCurrentShop;
    private static ShopType _currentShopType = ShopType.Normal;
    private static int _shopRefreshCountThisVisit;

    public static IReadOnlyList<ShopOffer?> CurrentSlots => Slots;
    public static bool HasRefreshedCurrentShop => _hasRefreshedCurrentShop;
    public static ShopType CurrentShopType => _currentShopType;

    /// <summary>
    /// Shop System 的公开入口：GetBuyPrice。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetBuyPrice(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => 75,
            EquipmentRarity.Rare => 150,
            EquipmentRarity.Epic => 300,
            EquipmentRarity.Legendary => 600,
            _ => 0
        };
    }

    // 每次进入商店调用：重新生成全部槽位，本次生成内不允许重复装备，并重置刷新次数。
    /// <summary>
    /// Shop System 的公开入口：GenerateSlots。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void GenerateSlots(ShopType shopType = ShopType.Normal)
    {
        if (GameManager.InitialEventAllShopsBlackMarket && !IntegratedTutorialFlow.IsActive)
        {
            shopType = ShopType.BlackMarket;
        }
        _currentShopType = shopType;
        _hasRefreshedCurrentShop = false;
        _shopRefreshCountThisVisit = 0;
        FactionFateManager.BeginShopVisit();
        var chapter = GameManager.CurrentChapter;
        PopulateSlots(BuildPoolForShop(shopType, chapter), chapter);
    }

    /// <summary>
    /// 整合式教程专用：把指定槽位的商品强制覆盖成一件固定装备+固定价格，供"购买
    /// 指定教学商品"这一步使用。必须在 <see cref="GenerateSlots"/> 之后调用（覆盖
    /// 而不是替代随机生成），价格是独立于 <see cref="GetBuyPrice"/> 真实稀有度经济
    /// 的固定教学价，不影响正式商店的定价规则。找不到装备定义时安全跳过。
    /// </summary>
    public static void ForceTutorialOffer(int slotIndex, string equipmentId, int price)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        var definition = EquipmentDatabase.GetEquipment(equipmentId);
        if (definition == null) return;
        Slots[slotIndex] = new ShopOffer(definition, price);
    }

    // 刷新商店：每次进入商店最多允许刷新 maxRefreshes 次；首次之后的额外刷新（来自初始事件⑪）免费；
    // 初始事件③激活时所有刷新永久免费；否则第一次刷新消耗50金币。
    /// <summary>
    /// Shop System 的公开入口：TryRefresh。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TryRefresh()
    {
        if (_currentShopType == ShopType.Witch)
        {
            return false;
        }

        var maxRefreshes = 1 + GameManager.InitialEventExtraShopRefreshCount;
        if (_shopRefreshCountThisVisit >= maxRefreshes)
        {
            return false;
        }

        var isFree = GameManager.InitialEventShopRefreshFree || _shopRefreshCountThisVisit >= 1;
        if (!isFree && GameManager.Gold < 50)
        {
            return false;
        }

        if (!isFree)
        {
            RewardManager.Execute(new RewardSequence().Add(new LoseGoldRewardAction(50)));
        }

        _shopRefreshCountThisVisit++;
        _hasRefreshedCurrentShop = _shopRefreshCountThisVisit >= maxRefreshes;
        GenerateNewSlots();
        return true;
    }

    // 仅重新生成槽位内容，不重置刷新标记（供 TryRefresh 内部调用）。
    private static void GenerateNewSlots()
    {
        var chapter = GameManager.CurrentChapter;
        PopulateSlots(BuildPoolForShop(_currentShopType, chapter), chapter);
    }

    private static List<EquipmentDefinition> BuildPoolForShop(ShopType shopType, int chapter)
    {
        return shopType switch
        {
            ShopType.Vehicle => BuildVehiclePool(),
            ShopType.Witch => BuildWitchPool(chapter),
            _ => BuildEligiblePool(chapter)
        };
    }

    // 槽位生成再次检查可售品质。第四章普通商店的每个槽位可独立命中传奇；
    // 黑市保留自身的传奇规则，载具/巫婆等专属商店不混入这一普通商店加成。
    private static void PopulateSlots(IReadOnlyList<EquipmentDefinition> pool, int chapter)
    {
        var chosenIds = new HashSet<string>();

        for (var i = 0; i < SlotCount; i++)
        {
            var remaining = new List<EquipmentDefinition>();
            foreach (var definition in pool)
            {
                if (CanAppearInShop(definition) && !chosenIds.Contains(definition.Id))
                {
                    remaining.Add(definition);
                }
            }

            if (remaining.Count == 0)
            {
                Slots[i] = null;
                continue;
            }

            var rarity = RollRarity(chapter);
            var sameRarity = new List<EquipmentDefinition>();
            foreach (var definition in remaining)
            {
                if (definition.Rarity == rarity)
                {
                    sameRarity.Add(definition);
                }
            }

            var picked = sameRarity.Count > 0
                ? sameRarity[Random.Next(sameRarity.Count)]
                : remaining[Random.Next(remaining.Count)];

            chosenIds.Add(picked.Id);
            Slots[i] = new ShopOffer(picked, GetCurrentShopBuyPrice(picked.Rarity));
            // 图鉴：商店里第一次看见这件装备（不要求买下）即解锁基础展示信息。
            CodexService.RecordEquipmentSeen(picked.Id);
        }
    }

    /// <summary>
    /// 判断装备品质是否允许进入任意商店的随机商品槽位。
    ///
    /// 固定事件、Boss 奖励与 Developer Mode 不调用此入口，因此传奇装备的其它获取方式不受影响。
    /// 黑市与第四章普通商店可放行传奇品质；后者只在每个栏位命中10%刷新时实际抽取。
    /// </summary>
    public static bool CanAppearInShop(EquipmentDefinition definition)
    {
        if (definition.Rarity == EquipmentRarity.Legendary)
        {
            return _currentShopType == ShopType.BlackMarket
                || (_currentShopType == ShopType.Normal && GameManager.CurrentChapter == 4);
        }

        return definition.Rarity is EquipmentRarity.Common
            or EquipmentRarity.Rare
            or EquipmentRarity.Epic;
    }

    // 金币足够：扣除金币，装备进入背包（不自动装备），槽位清空。金币不足：不可购买。
    /// <summary>
    /// Shop System 的公开入口：TryBuy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool TryBuy(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            return false;
        }

        var offer = Slots[slotIndex];
        if (offer == null || !CanBuy(offer))
        {
            return false;
        }

        // 吴·先行采购：每个商店第一件商品免费——必须在这里把实际扣费金额也改成0，
        // 不能只在 CanBuy 里放行，否则会出现"UI显示可买、实际仍然扣了原价"的bug。
        var isFreeFirstPurchase = FactionFateManager.IsFirstPurchaseFreeAvailable();
        var effectivePrice = isFreeFirstPurchase ? 0 : offer.Price;

        var sequence = new RewardSequence()
            .Add(new LoseGoldRewardAction(effectivePrice))
            .Add(new AddEquipmentRewardAction(offer.Definition.Id, EquipmentGainSource.ShopPurchase));
        if (_currentShopType == ShopType.Witch)
        {
            sequence.Add(new LoseMaxHpRewardAction(10));
        }

        RewardManager.Execute(sequence);
        Slots[slotIndex] = null;

        if (isFreeFirstPurchase)
        {
            FactionFateManager.ConsumeFirstPurchaseFree();
        }

        return true;
    }

    /// <summary>
    /// Shop System 的公开入口：CanBuy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanBuy(ShopOffer offer)
    {
        // 吴·先行采购：当前商店第一件商品免费，必须绕过金币是否足够的判定——否则玩家金币为0时
        // 免费购买的按钮会一直保持禁用状态，机制形同虚设。巫婆商店的生命上限门槛不受影响。
        var isFreeFirstPurchase = FactionFateManager.IsFirstPurchaseFreeAvailable();

        if (!isFreeFirstPurchase && GameManager.Gold < offer.Price)
        {
            return false;
        }

        if (_currentShopType == ShopType.Witch && GameManager.MaxHP <= 10)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Shop System 的公开入口：GetOfferPriceText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetOfferPriceText(ShopOffer offer)
    {
        if (FactionFateManager.IsFirstPurchaseFreeAvailable())
        {
            return _currentShopType == ShopType.Witch
                ? "价格：免费（先行采购）  +  永久-10生命上限"
                : "价格：免费（先行采购）";
        }

        return _currentShopType == ShopType.Witch
            ? $"价格：{offer.Price}金币  +  永久-10生命上限"
            : $"价格：{offer.Price}";
    }

    /// <summary>
    /// Shop System 的公开入口：GetOfferDisabledReason。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetOfferDisabledReason(ShopOffer offer)
    {
        if (GameManager.Gold < offer.Price)
        {
            return Localization.Get("ui.insufficient_gold");
        }

        if (_currentShopType == ShopType.Witch && GameManager.MaxHP <= 10)
        {
            return Localization.Get("ui.insufficient_max_hp");
        }

        return string.Empty;
    }

    // 载具商店：仅收录 EquipmentType.Vehicle 且 AcquisitionMethod.Shop 的装备。
    private static List<EquipmentDefinition> BuildVehiclePool()
    {
        var owned = new HashSet<string>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            owned.Add(item.Definition.Id);
        }

        var pool = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (IsVehicleEligible(definition, owned))
            {
                pool.Add(definition);
            }
        }

        return pool;
    }

    private static bool IsVehicleEligible(EquipmentDefinition definition, HashSet<string> owned)
    {
        if (owned.Contains(definition.Id))
        {
            return false;
        }

        if (!definition.CanAppearInRandomPool)
        {
            return false;
        }

        if (!CanAppearInShop(definition))
        {
            return false;
        }

        if (definition.AcquisitionMethod != EquipmentAcquisitionMethod.Shop)
        {
            return false;
        }

        foreach (var type in definition.Types)
        {
            if (type == EquipmentType.Vehicle)
            {
                return true;
            }
        }

        return false;
    }

    private static List<EquipmentDefinition> BuildEligiblePool(int chapter)
    {
        var owned = new HashSet<string>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            owned.Add(item.Definition.Id);
        }

        var pool = new List<EquipmentDefinition>();
        // ======================================================
        // 商店随机池
        // ======================================================
        // 商店展示虽然不是战斗奖励，但本质仍是随机装备来源。
        // 因此这里也使用 CanAppearInRandomPool，保证剧情装备只会通过固定事件、
        // Boss 或 Developer Mode 出现，不会混入普通商店刷新。
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (IsEligible(definition, owned, chapter))
            {
                pool.Add(definition);
            }
        }

        return pool;
    }

    private static List<EquipmentDefinition> BuildWitchPool(int chapter)
    {
        // 巫婆商店保留其独立价格与生命上限代价，但商品品质遵守全局商店规则。
        return BuildEligiblePool(chapter);
    }

    private static int GetCurrentShopBuyPrice(EquipmentRarity rarity)
    {
        var basePrice = _currentShopType switch
        {
            ShopType.Witch => 750,
            ShopType.BlackMarket => GameManager.InitialEventAllShopsBlackMarket
                ? GetBuyPrice(rarity)
                : (int)Math.Round(GetBuyPrice(rarity) * 1.5),
            _ => GetBuyPrice(rarity)
        };
        if (GameManager.HasEquipment(EquipmentIds.DiscountVoucher))
        {
            basePrice = (int)Math.Floor(basePrice * 0.75);
        }
        return Math.Max(1, basePrice);
    }

    // 过滤：背包中已有（含已装备）的装备 / 剧情道具 / 特殊奖励装备 / 随机池禁用装备。
    // AcquisitionMethod=Reward 为随机掉落装备；AcquisitionMethod=Shop 为商店专属装备；两者均可进入商店池。
    // AcquisitionMethod=Event / Debug / Unknown 的装备不进入商店池。
    // 品质默认限制为普通、稀有、史诗；第四章普通商店与黑市按各自规则额外放行传奇。
    private static bool IsEligible(EquipmentDefinition definition, HashSet<string> owned, int chapter)
    {
        if (owned.Contains(definition.Id))
        {
            return false;
        }

        if (!definition.CanAppearInRandomPool)
        {
            return false;
        }

        if (!CanAppearInShop(definition))
        {
            return false;
        }

        if (definition.AcquisitionMethod != EquipmentAcquisitionMethod.Reward
            && definition.AcquisitionMethod != EquipmentAcquisitionMethod.Shop)
        {
            return false;
        }

        foreach (var tag in definition.Tags)
        {
            if (tag == "story")
            {
                return false;
            }

            // 可售商店额外放行传奇品质时，不能连带放出"boss"标签的传奇装备——
            // 那些是隐藏Boss等固定来源的专属掉落
            // （如月亮宝石"隐藏Boss【月亮】100%掉落"），不该被黑市明码标价买到。
            // 只在传奇稀有度且黑市场景下排除，不影响史诗装备现有的商店可售性
            // （神秘药水等 Epic+boss 标签装备在其它商店/黑市里仍照常可售）。
            if (tag == "boss"
                && definition.Rarity == EquipmentRarity.Legendary
                && (_currentShopType == ShopType.BlackMarket
                    || (_currentShopType == ShopType.Normal && GameManager.CurrentChapter == 4)))
            {
                return false;
            }
        }

        return true;
    }

    // 所有商店统一使用普通/稀有/史诗三档章节成长权重：
    // 第1章 — 普通70% / 稀有25% / 史诗5%
    // 第2章 — 普通30% / 稀有50% / 史诗20%
    // 第3章 — 普通10% / 稀有40% / 史诗50%
    // 黑市与第四章普通商店额外叠加：每个槽位独立有10%概率直接出传奇，不占用/不影响普通商店三档权重的分布，
    // 剩余90%仍按章节权重从普通/稀有/史诗中抽取。
    private static EquipmentRarity RollRarity(int chapter)
    {
        var canRollLegendary = _currentShopType == ShopType.BlackMarket
            || (_currentShopType == ShopType.Normal && chapter == 4);
        if (canRollLegendary && Random.NextDouble() < ChapterFourLegendarySlotChance)
        {
            return EquipmentRarity.Legendary;
        }

        var roll = Random.NextDouble() * 100.0;

        if (chapter >= 3)
        {
            if (roll < 10.0) return EquipmentRarity.Common;
            if (roll < 50.0) return EquipmentRarity.Rare;
            return EquipmentRarity.Epic;
        }

        if (chapter == 2)
        {
            if (roll < 30.0) return EquipmentRarity.Common;
            if (roll < 80.0) return EquipmentRarity.Rare;
            return EquipmentRarity.Epic;
        }

        // 第1章
        if (roll < 70.0) return EquipmentRarity.Common;
        if (roll < 95.0) return EquipmentRarity.Rare;
        return EquipmentRarity.Epic;
    }
}
