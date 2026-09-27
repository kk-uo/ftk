//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/HuangTianLightningVisual.cs
//
// 模块：Presentation / Effect System
//
// 使用可打包的像素 PNG：小乌云表示【闪电】生成或转移，大落雷只在
// 实际20点伤害已经触发后播放。该文件不改写任何 BattleContext 状态。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>【黄天】单次战场特效的表现参数。</summary>
public sealed class HuangTianPresentationRequest
{
    public HuangTianPresentationRequest(
        Control stage,
        Vector2 targetGlobalPosition,
        HuangTianVisualKind kind)
    {
        Stage = stage;
        TargetGlobalPosition = targetGlobalPosition;
        Kind = kind;
    }

    public Control Stage { get; }
    public Vector2 TargetGlobalPosition { get; }
    public HuangTianVisualKind Kind { get; }
}

/// <summary>【黄天】的低精度像素乌云与落雷。</summary>
public static class HuangTianLightningVisual
{
    private const string StormCloudTexturePath =
        "res://Assets/Effects/HuangTian/huangtian_storm_cloud.png";
    private const string LightningStrikeTexturePath =
        "res://Assets/Effects/HuangTian/huangtian_lightning_strike.png";

    private const int EffectZIndex = 42;
    private const float CloudScale = 0.58f;
    private const float LightningScale = 0.94f;

    public static void Play(HuangTianPresentationRequest request)
    {
        if (!GodotObject.IsInstanceValid(request.Stage)
            || request.Stage.Size.X <= 1f
            || request.Stage.Size.Y <= 1f)
        {
            return;
        }

        var texturePath = request.Kind == HuangTianVisualKind.LightningStruck
            ? LightningStrikeTexturePath
            : StormCloudTexturePath;
        if (!ResourceLoader.Exists(texturePath))
        {
            return;
        }

        var texture = GD.Load<Texture2D>(texturePath);
        if (texture == null)
        {
            return;
        }

        var localTargetPosition = request.Stage.GetGlobalTransformWithCanvas().AffineInverse()
            * request.TargetGlobalPosition;
        var isStrike = request.Kind == HuangTianVisualKind.LightningStruck;
        var sprite = new Sprite2D
        {
            Name = isStrike ? "HuangTianLightningStrike" : "HuangTianStormCloud",
            Texture = texture,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = EffectZIndex,
            Scale = Vector2.One * (isStrike ? LightningScale : CloudScale),
            Position = localTargetPosition + (isStrike ? new Vector2(0f, -86f) : new Vector2(0f, -122f))
        };
        request.Stage.AddChild(sprite);

        var tree = request.Stage.GetTree();
        if (tree == null)
        {
            sprite.QueueFree();
            return;
        }

        if (isStrike)
        {
            PlayLightningStrike(tree, sprite);
        }
        else
        {
            PlayStormCloud(tree, sprite);
        }
    }

    private static void PlayStormCloud(SceneTree tree, Sprite2D sprite)
    {
        var startPosition = sprite.Position;
        sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        sprite.Scale = Vector2.One * (CloudScale * 0.76f);

        var tween = tree.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "modulate:a", 1f, 0.12f);
        tween.TweenProperty(sprite, "scale", Vector2.One * CloudScale, 0.16f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position:y", startPosition.Y - 8f, 0.82f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.SetParallel(false);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.24f).SetDelay(0.72f);
        tween.Finished += sprite.QueueFree;
    }

    private static void PlayLightningStrike(SceneTree tree, Sprite2D sprite)
    {
        sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        sprite.Scale = Vector2.One * (LightningScale * 0.68f);

        var tween = tree.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "modulate:a", 1f, 0.045f);
        tween.TweenProperty(sprite, "scale", Vector2.One * LightningScale, 0.08f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.SetParallel(false);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.26f).SetDelay(0.18f);
        tween.Finished += sprite.QueueFree;
    }
}
