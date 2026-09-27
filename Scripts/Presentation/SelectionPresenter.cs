//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/SelectionPresenter.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 统一 Battle 中敌人目标的 Hover/Selected 高亮表现。
// 2. 保证同一时间只有一个敌人状态 UI 高亮。
// 3. 使用统一的目标高亮颜色，避免不同敌人类型在暗色 Battle UI 中辨识度不一致。
//
// 不负责：
// × 选择目标的战斗规则。
// × 修改 BattleResolver、Trigger 或伤害。
// × 创建敌人模型。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// Battle 敌人选中高亮 Presenter。
///
/// 当前支持普通敌人的 <see cref="EnemyStatusUI"/> 与顶部 Boss 的
/// <see cref="BossStatusUI"/>，以后正式 EnemyPresenter 可复用同一入口。
/// </summary>
public sealed class SelectionPresenter
{
    private static readonly Color TargetHighlightColor = new(1.00f, 0.92f, 0.12f);

    private EnemyStatusUI? _highlightedEnemyStatus;
    private BossStatusUI? _highlightedBossStatus;

    /// <summary>
    /// 高亮普通敌人的模型绑定状态 UI。
    /// </summary>
    public void HighlightEnemy(BattleUnit unit, EnemyStatusUI statusUi)
    {
        ClearExcept(statusUi, null);
        _highlightedEnemyStatus = statusUi;
        statusUi.SetHighlighted(GetHighlightColor(unit), true);
    }

    /// <summary>
    /// 高亮 Boss 独立状态 UI。
    /// </summary>
    public void HighlightBoss(BattleUnit unit, BossStatusUI statusUi)
    {
        ClearExcept(null, statusUi);
        _highlightedBossStatus = statusUi;
        statusUi.SetHighlighted(GetHighlightColor(unit), true);
    }

    /// <summary>
    /// 当前目标死亡或战场不存在合法目标时清除所有高亮。
    /// 正常鼠标离开不能调用本入口，目标切换由 HighlightEnemy/HighlightBoss 自动处理旧高亮。
    /// </summary>
    public void ClearAll()
    {
        ClearExcept(null, null);
    }

    private void ClearExcept(EnemyStatusUI? enemyStatus, BossStatusUI? bossStatus)
    {
        if (_highlightedEnemyStatus != null && _highlightedEnemyStatus != enemyStatus)
        {
            _highlightedEnemyStatus.SetHighlighted(GetHighlightColor(_highlightedEnemyStatus.Unit), false);
            _highlightedEnemyStatus = null;
        }

        if (_highlightedBossStatus != null && _highlightedBossStatus != bossStatus)
        {
            _highlightedBossStatus.SetHighlighted(GetHighlightColor(_highlightedBossStatus.Unit), false);
            _highlightedBossStatus = null;
        }
    }

    /// <summary>
    /// 获取当前目标高亮颜色。
    ///
    /// 目标选择是战斗中的高频信息，因此不再按普通/精英/Boss 区分颜色；
    /// 统一使用亮黄色，让玩家能一眼看出当前攻击目标。
    /// </summary>
    public static Color GetHighlightColor(BattleUnit? unit)
    {
        if (unit is EnemyInstance enemy)
        {
            return TargetHighlightColor;
        }

        return new Color(0.22f, 0.88f, 1.00f);
    }
}
