//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatVisualProfile/CardVisualProfile.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 出一张卡应该长什么样（贴图）、播放什么表现（动画/特效/音效）——
// 费=蓝色能量流入、桃=绿色治疗粒子、酒=橙色强化光效、顺手牵羊=金币飞向玩家、
// 无懈可击=护盾展开、火杀/雷杀=对应属性挥砍——都应该是数据，不是
// 写在 Battle 或 CardUI 里的 if/switch。
//
// 职责：
// 1. 描述一张卡牌类型对应的贴图 Id（CardSpriteId）和表现 Id（动画/特效/音效）。
// 2. 让新增卡牌变成"注册一条 CardVisualProfile"，不需要改播放逻辑。
//
// 不负责：
// × 卡牌规则、费用、效果结算（完全不涉及，Card/CardType 的枚举定义不受影响）。
// × 播放表现（由未来的 Presenter 负责读取这些 Id 并播放）。
//
// 主要依赖：
// CardType（Scripts/Card.cs）
//////////////////////////////////////////////////////////

/// <summary>
/// 一张卡牌类型的完整表现参数集合。
///
/// 除 <see cref="CardType"/> 外全部可选：<see cref="CardSpriteId"/> 对应
/// SpriteDatabase，<see cref="AnimationId"/> 对应 AnimationDatabase，
/// <see cref="EffectId"/> 对应 EffectDatabase，<see cref="AudioId"/> 预留给未来的
/// AudioDatabase/AudioPlayer；留空表示这张卡暂时没有对应资源，播放方应静默跳过。
/// </summary>
public sealed class CardVisualProfile
{
    /// <summary>
    /// 创建一份卡牌表现 Profile。
    /// </summary>
    public CardVisualProfile(
        CardType cardType,
        string cardSpriteId = "",
        string animationId = "",
        string effectId = "",
        string audioId = "",
        string description = "")
    {
        CardType = cardType;
        CardSpriteId = cardSpriteId;
        AnimationId = animationId;
        EffectId = effectId;
        AudioId = audioId;
        Description = description;
    }

    /// <summary>该 Profile 对应的卡牌类型。</summary>
    public CardType CardType { get; }

    /// <summary>卡牌贴图资源 Id，交给 SpriteDatabase 解析。</summary>
    public string CardSpriteId { get; }

    /// <summary>出牌表现动画参数 Id，交给 AnimationDatabase 解析。</summary>
    public string AnimationId { get; }

    /// <summary>出牌特效 Id，交给 EffectDatabase 解析。</summary>
    public string EffectId { get; }

    /// <summary>出牌音效 Id（预留，交给未来的 AudioDatabase/AudioPlayer 解析）。</summary>
    public string AudioId { get; }

    /// <summary>备注，方便以后维护时理解这条 Profile 的表现意图，不参与播放逻辑。</summary>
    public string Description { get; }
}
