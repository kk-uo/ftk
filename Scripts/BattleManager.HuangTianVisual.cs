//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.HuangTianVisual.cs
//
// 模块：Battle UI / 黄天表现桥
//
// 规则层只在 BattleContext 中投递 HuangTianVisualRequest；本文件负责把
// 逻辑单位映射到 BattleStage 的实际锚点，不参与概率、转移或伤害结算。
//////////////////////////////////////////////////////////

using Godot;

public partial class BattleManager
{
    private void PresentHuangTianVisual(HuangTianVisualRequest request)
    {
        if (_battleStageVisualLayer == null)
        {
            return;
        }

        var stage = _battleStageVisualLayer;
        var globalTargetPosition = request.Target is EnemyInstance enemy
            ? GetControlCenter(GetEnemyStageModelAnchor(enemy), GetPlayerBattlePresentationPosition(stage))
            : GetPlayerBattlePresentationPosition(stage);

        HuangTianLightningVisual.Play(new HuangTianPresentationRequest(
            stage,
            globalTargetPosition,
            request.Kind));
    }
}
