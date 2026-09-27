//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ArrowBarragePresentationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证玩家万箭齐发能够覆盖多个目标。
// 2. 验证敌人万箭齐发能够反向射向玩家。
// 3. 验证箭雨临时节点在播放结束后自动清理。
//
// 不负责：
// × 验证万箭齐发伤害数值。
// × 验证最终美术观感。
//
// 主要依赖：
// PresentationManager
// ArrowBarrageEffectVisual
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 万箭齐发表现的 Headless 回归入口。
/// </summary>
public partial class ArrowBarragePresentationRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行表现回归并以进程退出码报告结果。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print($"ARROW_BARRAGE_PRESENTATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ARROW_BARRAGE_PRESENTATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var stage = new Control
        {
            Name = "TestBattleStage",
            Size = new Vector2(1200f, 700f)
        };
        AddChild(stage);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var stageOrigin = stage.GetGlobalTransformWithCanvas() * Vector2.Zero;
        var playerBattlePoint = BattleManager.GetPlayerBattlePresentationPosition(stage);
        Assert(playerBattlePoint.IsEqualApprox(stageOrigin + new Vector2(600f, 532f)),
            "玩家箭雨落点没有固定在 Battle Stage 下半部中央");

        var fireProfile = CardVisualProfileDatabase.Get(CardType.FireKill);
        var thunderProfile = CardVisualProfileDatabase.Get(CardType.ThunderKill);
        Assert(fireProfile?.EffectId == EffectDatabase.FireSlashEffectId, "火杀没有绑定火焰斩击特效");
        Assert(thunderProfile?.EffectId == EffectDatabase.ThunderSlashEffectId, "雷杀没有绑定雷电斩击特效");

        var fireDefinition = EffectDatabase.Get(EffectDatabase.FireSlashEffectId);
        var thunderDefinition = EffectDatabase.Get(EffectDatabase.ThunderSlashEffectId);
        Assert(fireDefinition != null && ResourceLoader.Exists(SpriteDatabase.GetPath(fireDefinition.SpriteId)),
            "火杀特效图片未进入项目资源");
        Assert(thunderDefinition != null && ResourceLoader.Exists(SpriteDatabase.GetPath(thunderDefinition.SpriteId)),
            "雷杀特效图片未进入项目资源");

        _ = new WeaponPresenter();
        var defaultWeaponAnimation = AnimationDatabase.GetAnimation("weapon_default_attack");
        var folderWeaponAnimation = AnimationDatabase.GetAnimation("weapon_folder_attack");
        Assert(defaultWeaponAnimation?.HoldDuration >= 0.55f, "默认武器没有延长约0.5秒停留");
        Assert(folderWeaponAnimation?.HoldDuration >= 0.545f, "目录化武器没有延长约0.5秒停留");

        var playerRequest = new ArrowBarragePresentationRequest(
            stage,
            playerBattlePoint,
            new[]
            {
                stageOrigin + new Vector2(300f, 180f),
                stageOrigin + new Vector2(600f, 160f),
                stageOrigin + new Vector2(900f, 190f)
            },
            sourceIsPlayer: true);

        PresentationManager.PlayEffect(new PresentationEvent(
            PresentationEventType.EffectRequested,
            effectId: ArrowBarrageEffectVisual.EffectId,
            payload: playerRequest));

        Assert(ArrowBarrageEffectVisual.ActiveVolleyCount == 1, "玩家箭雨没有创建表现节点");
        Assert(ArrowBarrageEffectVisual.LastTargetCount == 3, "玩家箭雨没有覆盖三个目标");
        Assert(ArrowBarrageEffectVisual.LastSpawnedArrowCount > 18, "多目标箭雨没有扩大覆盖数量");

        var enemyRequest = new ArrowBarragePresentationRequest(
            stage,
            stageOrigin + new Vector2(300f, 180f),
            new[] { playerBattlePoint },
            sourceIsPlayer: false);
        PresentationManager.PlayEffect(new PresentationEvent(
            PresentationEventType.EffectRequested,
            effectId: ArrowBarrageEffectVisual.EffectId,
            payload: enemyRequest));

        Assert(ArrowBarrageEffectVisual.ActiveVolleyCount == 2, "敌方箭雨没有复用同一表现系统");
        Assert(ArrowBarrageEffectVisual.LastTargetCount == 1, "敌方箭雨目标数量错误");
        Assert(ArrowBarrageEffectVisual.LastSpawnedArrowCount == 18, "单目标箭雨基础数量错误");

        await ToSignal(GetTree().CreateTimer(1.3), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(ArrowBarrageEffectVisual.ActiveVolleyCount == 0, "箭雨播放完成后没有清理临时节点");
        Assert(stage.GetNodeOrNull("ArrowBarrageEffect") == null, "Battle Stage 中残留箭雨节点");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
