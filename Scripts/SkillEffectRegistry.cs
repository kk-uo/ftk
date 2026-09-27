//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffectRegistry.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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

public interface ISkillEffect
{
    string SkillId { get; }

    void Register(TriggerManager triggerManager, Player owner);
}

/// <summary>
/// Core System 的公开类：SkillEffectRegistry。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class SkillEffectRegistry
{
    /// <summary>
    /// Core System 的公开入口：Create。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static ISkillEffect? Create(string skillId)
    {
        switch (skillId)
        {
            case SkillIds.Wushuang:
                return new WushuangEffect();
            case SkillIds.Biyue:
                return new BiyueEffect();
            case SkillIds.Longdan:
                return new LongdanEffect();
            case SkillIds.Lianying:
                return new LianyingEffect();
            case SkillIds.Qingnang:
                return new QingnangEffect();
            case SkillIds.Keji:
                return new KejiEffect();
            case SkillIds.Luoshen:
                return new LuoshenEffect();
            case SkillIds.Wansha:
                return new WanshaEffect();
            case SkillIds.XueZhaiXueChou:
                return new XueZhaiXueChouEffect();
            case SkillIds.XueZhaiXueChouNearDeath:
                return new XueZhaiNearDeathSkillEffect();
            case SkillIds.HuaXing:
                return new HuaXingEffect();
            case SkillIds.PoJun:
                return new PoJunEffect();
            case SkillIds.YiCheng:
                return new YiChengEffect();
            case SkillIds.ChangZui:
                return new ChangZuiEffect();
            case SkillIds.ManzuWang:
                return new ManzuWangEffect();
            case SkillIds.Rende:
                return new RendeEffect();
            case SkillIds.TaoyuanJiyi:
                return new TaoyuanJiyiSkillEffect();
            case SkillIds.Wusheng:
                return new WushengSkillEffect();
            case SkillIds.Yijue:
                return new YijueSkillEffect();
            case SkillIds.Paoxiao:
                return new PaoxiaoSkillEffect();
            case SkillIds.YingXi:
                return new YingXiEffect();
            case SkillIds.RuYingSuiXing:
                return new RuYingSuiXingEffect();
            case SkillIds.ZhouTaiBuQu:
                return new ZhouTaiBuQuEffect();
            case SkillIds.PangTongNirvana:
                return new PangTongNirvanaEffect();
            case SkillIds.PangTongIronChain:
                return new PangTongIronChainEffect();
            case SkillIds.JiAng:
                return new JiAngEffect();
            case SkillIds.HunZi:
                return new HunZiEffect();
            case SkillIds.Illusion:
                return new IllusionSkillEffect();
            case SkillIds.Slime:
                return new SlimeSkillEffect();
            default:
                return null;
        }
    }
}
