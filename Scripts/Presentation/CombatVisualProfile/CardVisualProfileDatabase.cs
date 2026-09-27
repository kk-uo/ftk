//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatVisualProfile/CardVisualProfileDatabase.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 每张卡牌的贴图和出牌表现都应该是"注册一条数据"，而不是在 CardUI 或 Battle
// 里写 switch(cardType)。这个数据库集中管理所有卡牌的 CardVisualProfile。
//
// 职责：
// 1. 按 CardType 注册/查询 CardVisualProfile。
// 2. 预先为费/杀/火杀/雷杀/闪/桃/酒/顺手牵羊/无懈可击这九种卡牌注册了
//    Profile（贴图/动画/特效 Id 大多数还没有对应资源，先用空字符串占位，
//    Description 说明了以后期望的表现意图），以后新增卡牌只需要照着
//    格式再注册一条。
//
// 不负责：
// × 卡牌规则、费用、效果结算（不修改 Scripts/Card.cs 的任何定义）。
// × 播放表现（由未来的 Presenter 负责）。
//
// 主要依赖：
// CardVisualProfile / CardType（Scripts/Card.cs）
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 卡牌表现 Profile 数据库：CardType → CardVisualProfile。
///
/// 查询不到时返回 null——调用方（未来的卡牌 Presenter）应该回退到只显示
/// 卡牌基础文字/边框，不报错、不影响出牌逻辑；这和 CombatVisualProfileDatabase
/// 用"默认 Profile 对象"兜底的策略不同，是因为卡牌哪怕完全没有表现资源，
/// 也早就有一套能正常显示的卡面 UI（CardUI.cs），不需要一个假的默认贴图。
/// </summary>
public static class CardVisualProfileDatabase
{
    private static readonly Dictionary<CardType, CardVisualProfile> Profiles = new();

    static CardVisualProfileDatabase()
    {
        Register(new CardVisualProfile(CardType.Fee,
            cardSpriteId: "card_fee",
            description: "费：蓝色能量流入角色。"));

        Register(new CardVisualProfile(CardType.Kill,
            cardSpriteId: "card_kill",
            animationId: "weapon_default_attack",
            effectId: EffectDatabase.SlashDefaultEffectId,
            description: "杀：普通挥砍，复用默认武器挥砍动画与默认斩击特效作为起始表现。"));

        Register(new CardVisualProfile(CardType.FireKill,
            cardSpriteId: "card_fire_kill",
            animationId: "weapon_default_attack",
            effectId: EffectDatabase.FireSlashEffectId,
            description: "火杀：复用当前武器挥砍，并叠加低像素火焰斩击。"));

        Register(new CardVisualProfile(CardType.ThunderKill,
            cardSpriteId: "card_thunder_kill",
            animationId: "weapon_default_attack",
            effectId: EffectDatabase.ThunderSlashEffectId,
            description: "雷杀：复用当前武器挥砍，并叠加低像素雷电斩击。"));

        Register(new CardVisualProfile(CardType.Dodge,
            cardSpriteId: "card_dodge",
            description: "闪：格挡表现，具体动作交给 DefenseVisualProfile 决定。"));

        Register(new CardVisualProfile(CardType.Peach,
            cardSpriteId: "card_peach",
            description: "桃：绿色治疗粒子。"));

        Register(new CardVisualProfile(CardType.Wine,
            cardSpriteId: "card_wine",
            description: "酒：橙色强化光效。"));

        Register(new CardVisualProfile(CardType.Steal,
            cardSpriteId: "card_steal",
            description: "顺手牵羊：金币飞向玩家。"));

        Register(new CardVisualProfile(CardType.Tuxi,
            cardSpriteId: "card_tuxi",
            description: "突袭：复用攻击性锦囊表现；实际造成伤害后继续播放顺手牵羊表现。"));

        Register(new CardVisualProfile(CardType.Unassailable,
            cardSpriteId: "card_unassailable",
            description: "无懈可击：护盾展开。"));
    }

    /// <summary>
    /// 注册（或覆盖）某种卡牌类型的表现 Profile。
    /// </summary>
    public static void Register(CardVisualProfile profile)
    {
        Profiles[profile.CardType] = profile;
    }

    /// <summary>
    /// 查询某种卡牌类型的表现 Profile；未注册时返回 null，调用方应回退到
    /// 卡牌基础 UI（贴图/文字/边框），不报错、不影响出牌逻辑。
    /// </summary>
    public static CardVisualProfile? Get(CardType cardType)
    {
        return Profiles.TryGetValue(cardType, out var profile) ? profile : null;
    }
}
