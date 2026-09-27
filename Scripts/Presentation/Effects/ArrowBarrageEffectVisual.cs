//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/ArrowBarrageEffectVisual.cs
//
// 模块：Presentation / Effect System
//
// 职责：
// 1. 播放【万箭齐发】的大范围曲线箭雨表现。
// 2. 根据施法端和目标端自动决定箭矢方向与覆盖范围。
// 3. 在同一个 Battle Stage 节点中复用并自动清理所有临时节点。
//
// 不负责：
// × 判断万箭齐发是否合法。
// × 计算命中、格挡或伤害。
// × 查找战斗单位或改变战斗状态。
//
// 主要依赖：
// Godot Control / Node2D / Tween
// PresentationManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 一次万箭齐发表现所需的舞台坐标。
///
/// 坐标由 Battle UI 提供，特效本身不持有 BattleContext 或 BattleUnit，
/// 因此玩家和敌人均可通过同一条表现链路播放。
/// </summary>
public sealed class ArrowBarragePresentationRequest
{
    /// <summary>
    /// 创建万箭齐发表现请求。
    /// </summary>
    public ArrowBarragePresentationRequest(
        Control stage,
        Vector2 sourceGlobalPosition,
        IReadOnlyList<Vector2> targetGlobalPositions,
        bool sourceIsPlayer)
    {
        Stage = stage;
        SourceGlobalPosition = sourceGlobalPosition;
        TargetGlobalPositions = targetGlobalPositions;
        SourceIsPlayer = sourceIsPlayer;
    }

    public Control Stage { get; }
    public Vector2 SourceGlobalPosition { get; }
    public IReadOnlyList<Vector2> TargetGlobalPositions { get; }
    public bool SourceIsPlayer { get; }
}

/// <summary>
/// 【万箭齐发】程序化像素箭雨。
///
/// 每支箭使用二次贝塞尔曲线移动，让箭群从施法者一侧升起并以弧线落向目标。
/// 箭雨只存在于 Battle Stage，不会进入 HUD 或参与输入。
/// </summary>
public static partial class ArrowBarrageEffectVisual
{
    public const string EffectId = "arrow_barrage";

    private const int BaseArrowCount = 18;
    private const int AdditionalTargetArrowCount = 8;
    private const int MaximumArrowCount = 34;
    private const int EffectZIndex = 25;
    private const float StageMargin = 34f;
    private const float MinimumDuration = 0.46f;
    private const float MaximumDuration = 0.68f;

    /// <summary>
    /// 当前仍在场上的箭雨数量，仅用于自动化验证和 Developer 调试。
    /// </summary>
    public static int ActiveVolleyCount { get; private set; }

    /// <summary>
    /// 最近一次请求实际生成的箭矢数量。
    /// </summary>
    public static int LastSpawnedArrowCount { get; private set; }

    /// <summary>
    /// 最近一次请求包含的目标数量。
    /// </summary>
    public static int LastTargetCount { get; private set; }

    /// <summary>
    /// 播放一次万箭齐发。
    ///
    /// 无有效舞台或目标时静默跳过，避免表现层异常影响战斗流程。
    /// </summary>
    public static void Play(ArrowBarragePresentationRequest request)
    {
        if (!GodotObject.IsInstanceValid(request.Stage)
            || request.Stage.Size.X <= 1f
            || request.Stage.Size.Y <= 1f
            || request.TargetGlobalPositions.Count == 0)
        {
            return;
        }

        var root = new Control
        {
            Name = "ArrowBarrageEffect",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = EffectZIndex
        };
        request.Stage.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var inverseCanvasTransform = request.Stage.GetGlobalTransformWithCanvas().AffineInverse();
        var source = ClampToStage(inverseCanvasTransform * request.SourceGlobalPosition, request.Stage.Size);
        var targets = new List<Vector2>(request.TargetGlobalPositions.Count);
        foreach (var globalTarget in request.TargetGlobalPositions)
        {
            targets.Add(ClampToStage(inverseCanvasTransform * globalTarget, request.Stage.Size));
        }

        var arrowCount = Math.Min(
            MaximumArrowCount,
            BaseArrowCount + Math.Max(0, targets.Count - 1) * AdditionalTargetArrowCount);
        LastSpawnedArrowCount = arrowCount;
        LastTargetCount = targets.Count;
        ActiveVolleyCount++;

        var random = new RandomNumberGenerator();
        random.Randomize();
        var longestLifetime = 0f;

        // 箭群按目标轮流分配，既能覆盖多目标，又不会因为目标数量增加而产生满屏粒子。
        for (var index = 0; index < arrowCount; index++)
        {
            var targetIndex = index % targets.Count;
            var target = targets[targetIndex];
            var start = BuildSpreadPoint(source, index, arrowCount, random, request.Stage.Size, true);
            var end = BuildSpreadPoint(target, index, arrowCount, random, request.Stage.Size, false);
            var control = BuildCurveControl(start, end, index, random, request.SourceIsPlayer);
            var delay = random.RandfRange(0f, 0.14f) + targetIndex * 0.015f;
            var duration = random.RandfRange(MinimumDuration, MaximumDuration);
            longestLifetime = Math.Max(longestLifetime, delay + duration + 0.12f);

            var arrow = new ArrowProjectile(start, control, end, index);
            root.AddChild(arrow);
            arrow.Play(delay, duration, () => SpawnImpact(root, end, index));
        }

        root.TreeExiting += () => ActiveVolleyCount = Math.Max(0, ActiveVolleyCount - 1);
        var cleanupTween = root.CreateTween();
        cleanupTween.TweenInterval(longestLifetime + 0.12f);
        cleanupTween.TweenCallback(Callable.From(root.QueueFree));
    }

    private static Vector2 ClampToStage(Vector2 point, Vector2 stageSize)
    {
        return new Vector2(
            Mathf.Clamp(point.X, StageMargin, Math.Max(StageMargin, stageSize.X - StageMargin)),
            Mathf.Clamp(point.Y, StageMargin, Math.Max(StageMargin, stageSize.Y - StageMargin)));
    }

    private static Vector2 BuildSpreadPoint(
        Vector2 center,
        int index,
        int count,
        RandomNumberGenerator random,
        Vector2 stageSize,
        bool isSource)
    {
        var normalized = count <= 1 ? 0f : index / (float)(count - 1) - 0.5f;
        var width = Math.Min(stageSize.X * (isSource ? 0.20f : 0.14f), isSource ? 230f : 150f);
        var offset = new Vector2(
            normalized * width + random.RandfRange(-22f, 22f),
            random.RandfRange(-28f, 28f));
        return ClampToStage(center + offset, stageSize);
    }

    private static Vector2 BuildCurveControl(
        Vector2 start,
        Vector2 end,
        int index,
        RandomNumberGenerator random,
        bool sourceIsPlayer)
    {
        var direction = end - start;
        var normal = direction.LengthSquared() > 0.01f
            ? new Vector2(-direction.Y, direction.X).Normalized()
            : Vector2.Right;
        var alternatingSide = index % 2 == 0 ? 1f : -1f;
        var arcHeight = random.RandfRange(70f, 145f);

        // 玩家和敌人共用同一曲线算法；方向来自实际起终点，sourceIsPlayer 只用于
        // 让两侧的主弧朝舞台外侧偏移，视觉上明确表现“从自己一侧射出”。
        var sideBias = sourceIsPlayer ? -1f : 1f;
        return start.Lerp(end, 0.48f)
            + normal * arcHeight * alternatingSide
            + Vector2.Up * arcHeight * 0.35f * sideBias;
    }

    private static void SpawnImpact(Control root, Vector2 position, int variant)
    {
        if (!GodotObject.IsInstanceValid(root))
        {
            return;
        }

        var impact = new ArrowImpact(variant)
        {
            Position = position
        };
        root.AddChild(impact);
        impact.Play();
    }

    private sealed partial class ArrowProjectile : Node2D
    {
        private readonly Vector2 _start;
        private readonly Vector2 _control;
        private readonly Vector2 _end;
        private readonly Color _accent;

        public ArrowProjectile(Vector2 start, Vector2 control, Vector2 end, int variant)
        {
            _start = start;
            _control = control;
            _end = end;
            _accent = variant % 5 == 0
                ? new Color("38a7a8")
                : new Color("8d3028");
            Position = start;
        }

        public void Play(float delay, float duration, Action onImpact)
        {
            var tween = CreateTween();
            tween.TweenInterval(delay);
            tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(onImpact));
            tween.TweenProperty(this, "modulate:a", 0f, 0.08f);
            tween.Finished += QueueFree;
        }

        public override void _Draw()
        {
            var shaft = new Color("49372f");
            var steel = new Color("a9adb0");
            var darkSteel = new Color("343a40");
            var feather = new Color("6f2b27");

            DrawLine(new Vector2(-24f, 0f), new Vector2(7f, 0f), shaft, 3f, false);
            DrawLine(new Vector2(-22f, -1f), new Vector2(5f, -1f), new Color("806052"), 1f, false);
            DrawColoredPolygon(
                new[] { new Vector2(5f, -6f), new Vector2(16f, 0f), new Vector2(5f, 6f) },
                steel);
            DrawLine(new Vector2(5f, -5f), new Vector2(15f, 0f), darkSteel, 2f, false);
            DrawLine(new Vector2(-23f, 0f), new Vector2(-15f, -6f), feather, 3f, false);
            DrawLine(new Vector2(-23f, 0f), new Vector2(-15f, 6f), feather, 3f, false);
            DrawLine(new Vector2(-40f, 0f), new Vector2(-28f, 0f), new Color(_accent, 0.55f), 2f, false);
        }

        private void SetProgress(float progress)
        {
            var first = _start.Lerp(_control, progress);
            var second = _control.Lerp(_end, progress);
            Position = first.Lerp(second, progress);

            var tangent = 2f * (1f - progress) * (_control - _start)
                + 2f * progress * (_end - _control);
            if (tangent.LengthSquared() > 0.01f)
            {
                Rotation = tangent.Angle();
            }
        }
    }

    private sealed partial class ArrowImpact : Node2D
    {
        private readonly Color _accent;

        public ArrowImpact(int variant)
        {
            _accent = variant % 5 == 0
                ? new Color("38a7a8")
                : new Color("8d3028");
        }

        public void Play()
        {
            Scale = new Vector2(0.65f, 0.65f);
            var tween = CreateTween();
            tween.SetParallel();
            tween.TweenProperty(this, "scale", Vector2.One, 0.10f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(this, "modulate:a", 0f, 0.16f)
                .SetDelay(0.04f);
            tween.Finished += QueueFree;
        }

        public override void _Draw()
        {
            for (var index = 0; index < 6; index++)
            {
                var angle = Mathf.Tau * index / 6f;
                var direction = Vector2.FromAngle(angle);
                DrawLine(direction * 5f, direction * 16f, new Color(_accent, 0.75f), 2f, false);
            }
        }
    }
}
