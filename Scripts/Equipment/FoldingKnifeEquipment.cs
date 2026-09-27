//////////////////////////////////////////////////////////
// 折叠刀（史诗·武器）
//
// 普通杀的两段化发生在 DealAttackDamage 的入口，而不是将一段伤害改成
// 10 点。这样每一段都会独立进入 OnBeforeDamage / OnDamage /
// OnDamageTaken，完整触发普通杀加伤、护盾消耗及命中后效果。
//////////////////////////////////////////////////////////

/// <summary>
/// 折叠刀的无状态规则定义，供真实结算与伤害预览共用。
/// </summary>
public static class FoldingKnifeEquipment
{
    public const int HitCount = 2;
    public const int DamagePerHit = 5;

    /// <summary>
    /// 仅通过正常出招入口结算的普通杀会被折叠刀拆成两段；火杀、雷杀、技能追加的杀伤害
    /// 与其它攻击牌保留原有结算。
    /// </summary>
    public static bool AppliesTo(Player attacker, CardType attackType)
    {
        return attackType == CardType.Kill
            && BattleRules.HasEquipment(attacker, EquipmentIds.FoldingKnife);
    }
}
