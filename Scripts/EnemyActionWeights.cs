//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyActionWeights.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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

public sealed class EnemyActionWeights
{
    public float Slash;
    public float FireSlash;
    public float ThunderSlash;
    public float FireThunderSlash;
    public float CelestialImpact;
    public float ArrowBarrage;
    public float NanmanInvasion;
    public float Dodge;
    public float Peach;
    public float Wuxie;
    public float Resource;
    public float ShunShou;
    public float SureSlash;
    public float Wine;
    public float Guanxing;
    public float IceSlash;

    /// <summary>
    /// Enemy System 的公开入口：GetWeight。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public float GetWeight(CardType cardType)
    {
        return cardType switch
        {
            CardType.Kill => Slash,
            CardType.FireKill => FireSlash,
            CardType.ThunderKill => ThunderSlash,
            CardType.FireThunderKill => FireThunderSlash,
            CardType.CelestialImpact => CelestialImpact,
            CardType.ArrowBarrage => ArrowBarrage,
            CardType.NanmanInvasion => NanmanInvasion,
            CardType.Dodge => Dodge,
            CardType.Peach => Peach,
            CardType.Unassailable => Wuxie,
            CardType.Fee => Resource,
            CardType.Steal => ShunShou,
            CardType.SureKill => SureSlash,
            CardType.Wine => Wine,
            CardType.Guanxing => Guanxing,
            CardType.IceKill => IceSlash,
            _ => 0
        };
    }

    /// <summary>
    /// Enemy System 的公开入口：AddWeight。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddWeight(EnemyActionWeightType type, float delta)
    {
        switch (type)
        {
            case EnemyActionWeightType.Slash:
                Slash += delta;
                break;
            case EnemyActionWeightType.FireSlash:
                FireSlash += delta;
                break;
            case EnemyActionWeightType.ThunderSlash:
                ThunderSlash += delta;
                break;
            case EnemyActionWeightType.FireThunderSlash:
                FireThunderSlash += delta;
                break;
            case EnemyActionWeightType.CelestialImpact:
                CelestialImpact += delta;
                break;
            case EnemyActionWeightType.ArrowBarrage:
                ArrowBarrage += delta;
                break;
            case EnemyActionWeightType.NanmanInvasion:
                NanmanInvasion += delta;
                break;
            case EnemyActionWeightType.Dodge:
                Dodge += delta;
                break;
            case EnemyActionWeightType.Peach:
                Peach += delta;
                break;
            case EnemyActionWeightType.Wuxie:
                Wuxie += delta;
                break;
            case EnemyActionWeightType.Resource:
                Resource += delta;
                break;
            case EnemyActionWeightType.ShunShou:
                ShunShou += delta;
                break;
            case EnemyActionWeightType.SureSlash:
                SureSlash += delta;
                break;
            case EnemyActionWeightType.Wine:
                Wine += delta;
                break;
            case EnemyActionWeightType.Guanxing:
                Guanxing += delta;
                break;
            case EnemyActionWeightType.IceSlash:
                IceSlash += delta;
                break;
        }
    }

    /// <summary>
    /// Enemy System 的公开入口：Clone。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EnemyActionWeights Clone()
    {
        return new EnemyActionWeights
        {
            Slash = Slash,
            FireSlash = FireSlash,
            ThunderSlash = ThunderSlash,
            FireThunderSlash = FireThunderSlash,
            CelestialImpact = CelestialImpact,
            ArrowBarrage = ArrowBarrage,
            NanmanInvasion = NanmanInvasion,
            Dodge = Dodge,
            Peach = Peach,
            Wuxie = Wuxie,
            Resource = Resource,
            ShunShou = ShunShou,
            SureSlash = SureSlash,
            Wine = Wine,
            Guanxing = Guanxing,
            IceSlash = IceSlash
        };
    }
}
