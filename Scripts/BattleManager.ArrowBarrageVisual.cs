//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.ArrowBarrageVisual.cs
//
// 模块：Battle UI / Presentation Bridge
//
// 职责：
// 1. 把本回合已结算的【万箭齐发】转换为表现层请求。
// 2. 提供玩家、敌人及多目标的 Battle Stage 坐标。
//
// 不负责：
// × 结算万箭齐发。
// × 计算命中或伤害。
// × 创建具体箭矢节点。
//
// 主要依赖：
// BattleManager
// PresentationManager
// ArrowBarrageEffectVisual
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

public partial class BattleManager
{
    /// <summary>
    /// 提交本回合双方的万箭齐发表现。
    ///
    /// 该方法在伤害结算后调用，只读取取消状态与当前舞台锚点；
    /// 箭矢数量、曲线和清理由表现层负责。
    /// </summary>
    private void PresentArrowBarrageEffects(
        BattleAction playerAction,
        IReadOnlyList<EnemyActionEntry> enemyActions)
    {
        if (_context == null || _battleStageVisualLayer == null)
        {
            return;
        }

        // 右下角 PlayerStatusRoot 只是 HUD，不代表玩家在战场中的受击位置。
        // 玩家侧统一使用 Battle Stage 下半部中央作为表现锚点，避免敌方箭雨飞向 UI。
        var playerBattlePosition = GetPlayerBattlePresentationPosition(_battleStageVisualLayer);

        if (playerAction.IsArrowBarrage && !_context.PlayerActionCancelled)
        {
            var enemyTargets = new List<Vector2>();
            foreach (var entry in enemyActions)
            {
                if (entry.ActionCancelled)
                {
                    continue;
                }

                var enemyAnchor = GetEnemyStageModelAnchor(entry.Enemy);
                if (enemyAnchor != null)
                {
                    enemyTargets.Add(GetControlCenter(enemyAnchor, Vector2.Zero));
                }
            }

            PublishArrowBarrage(playerBattlePosition, enemyTargets, sourceIsPlayer: true);
        }

        foreach (var entry in enemyActions)
        {
            if (!entry.Action.IsArrowBarrage || entry.ActionCancelled)
            {
                continue;
            }

            var sourceAnchor = GetEnemyStageModelAnchor(entry.Enemy);
            if (sourceAnchor == null)
            {
                continue;
            }

            PublishArrowBarrage(
                GetControlCenter(sourceAnchor, Vector2.Zero),
                new[] { playerBattlePosition },
                sourceIsPlayer: false);
        }
    }

    private void PublishArrowBarrage(
        Vector2 sourceGlobalPosition,
        IReadOnlyList<Vector2> targetGlobalPositions,
        bool sourceIsPlayer)
    {
        if (_battleStageVisualLayer == null || targetGlobalPositions.Count == 0)
        {
            return;
        }

        var request = new ArrowBarragePresentationRequest(
            _battleStageVisualLayer,
            sourceGlobalPosition,
            targetGlobalPositions,
            sourceIsPlayer);
        PresentationManager.PlayEffect(new PresentationEvent(
            PresentationEventType.EffectRequested,
            effectId: ArrowBarrageEffectVisual.EffectId,
            payload: request));
    }

    private static Vector2 GetControlCenter(Control? control, Vector2 fallback)
    {
        if (control == null || !GodotObject.IsInstanceValid(control))
        {
            return fallback;
        }

        return control.GetGlobalTransformWithCanvas() * (control.Size * 0.5f);
    }

    /// <summary>
    /// 返回玩家在 Battle Stage 中的统一表现落点。
    ///
    /// 该点不读取 PlayerStatusRoot，因此 HUD 移动、缩放或重排都不会改变攻击落点。
    /// </summary>
    internal static Vector2 GetPlayerBattlePresentationPosition(Control stage)
    {
        return stage.GetGlobalTransformWithCanvas()
            * new Vector2(stage.Size.X * 0.5f, stage.Size.Y * 0.76f);
    }
}
