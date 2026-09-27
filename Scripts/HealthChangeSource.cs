/// <summary>生命值变动的方向：伤害与治疗共用同一份来源模型。</summary>
public enum HealthChangeKind { Damage, Heal }

/// <summary>产生生命值变动的规则类别。</summary>
public enum HealthChangeSourceKind { AttackAction, Equipment, Skill, Environment, System }

/// <summary>来源或目标所属阵营；环境和规则没有归属时使用 Neutral。</summary>
public enum HealthChangeSide { Player, Enemy, Neutral }

/// <summary>不可变的生命值变动来源描述，供结算与战斗日志共用。</summary>
public readonly record struct HealthChangeSource(
    HealthChangeSourceKind Kind,
    string Name,
    string Id = "",
    Player? Owner = null);
