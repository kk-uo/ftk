//////////////////////////////////////////////////////////
// 文件：Scripts/CharacterIds.cs
//
// 模块：Character System
//
// 职责：
// 1. 承载角色定义、角色选择与角色展示相关代码。
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

public static class CharacterIds
{
    public const string ZhaoYun = "zhaoyun";
    public const string LuBu = "lvbu";
    public const string MaChao = "machao";
    public const string DiaoChan = "diaochan";
    public const string LuXun = "luxun";
    public const string HuaTuo = "huatuo";
    public const string LuMeng = "lvmeng";
    public const string MengHuo = "menghuo";
    public const string ZhuGeLiang = "zhugeliang";
    public const string MiHeng = "miheng";

    public const string ZhenJi = "zhenji";
    public const string JiaXu = "jiaxu";
    public const string XiaHouDun = "xiaohoudun";
    public const string ZuoCi = "zuoci";
    public const string XuSheng = "xusheng";

    // 蜀汉共生体 Boss
    public const string LiuBei = "liubei";
    public const string GuanYu = "guanyu";
    public const string ZhangFei = "zhangfei";

    public const string HuangYueYing = "huangyueying";

    public const string DongZhuo = "dongzhuo";

    public const string ZhangJiao = "zhangjiao";
    public const string ZhangLiao = "zhangliao";
    public const string CaoZhen = "caozhen";
    public const string SunCe = "sunce";

    /// <summary>孙尚香：传奇被动【武库】——统计背包内攻击/防御/饰品/载具装备，转化为战斗与资源加成。</summary>
    public const string SunShangXiang = "sunshangxiang";

    /// <summary>周泰：史诗被动【不屈】与稀有被动【奋激】——濒死掷骰保命，并永久强化攻击性锦囊。占位角色，人设待补完。</summary>
    public const string ZhouTai = "zhoutai";
    /// <summary>周瑜：稀有卡牌技能【英姿】将【火杀】替换为专属锦囊【火攻】；普通被动【业炎】造成火伤后给目标施加2层【虚弱】。</summary>
    public const string ZhouYu = "zhou_yu";
    /// <summary>庞统：传奇被动【涅槃】——本场战斗首次濒死时回满生命、清负面Buff、双方费用重置为1。占位角色，人设待补完。</summary>
    public const string PangTong = "pangtong";

    // Backward-compatible aliases for existing code.
    public const string Zhaoyun = ZhaoYun;
    public const string Lvbu = LuBu;
    public const string Machao = MaChao;
    public const string Diaochan = DiaoChan;
    public const string Luxun = LuXun;
    public const string Huatuo = HuaTuo;
    public const string Lvmeng = LuMeng;
    public const string Sunce = SunCe;
}
