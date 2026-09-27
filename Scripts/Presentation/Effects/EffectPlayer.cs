//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/EffectPlayer.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// 如果 WeaponPresenter、CharacterPresenter、UIPresenter 各自创建自己的特效节点，
// 以后每个 Presenter 都要重复一遍“建 CanvasLayer、算位置、写 Tween、淡入淡出、
// 播完销毁”的样板代码。EffectPlayer 是唯一负责真正播放 Effect 的地方，
// 其它 Presenter 只需要调用 EffectPlayer.Play(effectId)。
//
// 职责：
// 1. 读取 EffectDatabase 里的 EffectDefinition，按 EffectType 分支实例化
//    Sprite2D / 粒子 / Shader / 音效等真实表现节点。
// 2. 提供 PlayPreset，依次播放一个 EffectPreset 里的所有效果，支持"一个技能
//    同时播放多个效果"的组合场景，不需要为每种组合写专属代码。
// 3. 对尚未接入真实资源/系统的效果类型（Fire/Thunder/Ice/Particle/Shader/
//    CameraShake/HitStop/FlashScreen 等）静默跳过，不抛异常、不影响调用方。
//
// 不负责：
// × 决定战斗是否命中、伤害多少（由 Battle/Damage 负责）。
// × 决定什么时候该播放哪个效果（由 Battle/PresentationManager/Skill 负责调用）。
// × 管理 AnimationDatabase 或 WeaponPresenter 的挥砍动画（两者职责不同：
//   Animation 负责"怎么动"，Effect 负责"播放什么"，本类只处理后者）。
//
// 主要依赖：
// EffectDatabase / EffectDefinition / EffectPreset / EffectType / RenderLayerManager
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 效果播放器：Effect System 中唯一负责把 <see cref="EffectDefinition"/> 变成真实
/// Godot 节点并播放出来的地方。
///
/// 调用方式：
/// <code>
/// EffectPlayer.Play(EffectDatabase.SlashDefaultEffectId, anchor);
/// EffectPlayer.PlayPreset("fire_kill_combo", anchor);
/// </code>
/// 新增一种效果大类的真实播放逻辑时，只需要在 <see cref="Play"/> 的 switch 里
/// 新增一个分支；新增效果内容本身（贴图、参数）只需要在 EffectDatabase 里注册，
/// 完全不需要改这个类。
/// </summary>
public static class EffectPlayer
{
    /// <summary>
    /// 播放程序化完全格挡特效。
    ///
    /// 格挡来源和是否成立由战斗层决定；这里仅把已确认的结果转换为护盾闪光。
    /// </summary>
    public static void PlayBlock(Control? anchor)
    {
        BlockEffectVisual.Play(anchor);
    }

    /// <summary>
    /// 播放一个效果。
    ///
    /// <paramref name="anchor"/> 用作定位参考（通常是角色卡片或目标节点）；
    /// 部分效果大类（例如未来的 Screen/Camera 全局效果）可以不传锚点。
    /// 效果不存在、资源缺失或该大类尚未实现真实播放逻辑时，方法会静默返回，
    /// 不影响调用方，也不影响战斗结算。
    /// </summary>
    public static void Play(string effectId, Control? anchor = null)
    {
        var definition = EffectDatabase.Get(effectId);
        if (definition == null)
        {
            return;
        }

        // FlashTarget 独立于 EffectType 分支：不生成新的 Sprite2D，直接闪烁锚点自身，
        // 迁移自原 EffectPresenter.PresentHitEffect（受击闪烁），是 PresentationManager.PlayHit
        // 现在统一调用的效果。
        if (definition.FlashTarget)
        {
            PlayFlashEffect(definition, anchor);
            return;
        }

        switch (definition.EffectType)
        {
            case EffectType.Slash:
                PlaySpriteEffect(definition, anchor);
                break;

            // 以下大类目前只保存数据，尚未接入真实资源/系统：
            // Fire/Thunder/Ice/Heal/Shield/Buff/Debuff 等可以先用和 Slash 相同的
            // PlaySpriteEffect 播放（只要注册了 SpriteId），也可以以后各自扩展专属分支；
            // Screen/Camera/Particle/Shader/UI/Custom 需要引用具体的相机、UI 或粒子系统，
            // 留到真正接入时再实现，这里先不做任何事，保证不报错、不影响已完成表现。
            case EffectType.Fire:
            case EffectType.Thunder:
            case EffectType.Ice:
            case EffectType.Heal:
            case EffectType.Shield:
            case EffectType.Buff:
            case EffectType.Debuff:
                PlaySpriteEffect(definition, anchor);
                break;

            case EffectType.UI:
            case EffectType.Screen:
            case EffectType.Camera:
            case EffectType.Particle:
            case EffectType.Shader:
            case EffectType.Custom:
            default:
                break;
        }
    }

    /// <summary>
    /// 在指定战场全局坐标播放贴图特效。
    ///
    /// 武器 Presenter 使用该入口让元素特效与武器动画共用同一个实际播放点，
    /// 避免特效错误跟随右下角玩家 HUD。
    /// </summary>
    public static void PlayAt(string effectId, SceneTree tree, Vector2 globalPosition)
    {
        var definition = EffectDatabase.Get(effectId);
        if (definition == null || definition.FlashTarget)
        {
            return;
        }

        switch (definition.EffectType)
        {
            case EffectType.Slash:
            case EffectType.Fire:
            case EffectType.Thunder:
            case EffectType.Ice:
            case EffectType.Heal:
            case EffectType.Shield:
            case EffectType.Buff:
            case EffectType.Debuff:
                PlaySpriteEffectAt(definition, tree, globalPosition);
                break;
        }
    }

    /// <summary>
    /// 依次播放一个效果组合里的全部效果。
    ///
    /// 例如火杀 = 挥砍 + 火焰 + 受击 + 震屏，Boss 技能 = 雷电 + 闪屏 + 顿帧 + 震屏，
    /// 都只需要注册一条 <see cref="EffectPreset"/>，调用这里即可全部播放。
    /// </summary>
    public static void PlayPreset(string presetId, Control? anchor = null)
    {
        var preset = EffectDatabase.GetPreset(presetId);
        if (preset == null)
        {
            return;
        }

        foreach (var effectId in preset.EffectIds)
        {
            Play(effectId, anchor);
        }
    }

    /// <summary>
    /// 闪烁类效果的播放逻辑：把锚点自身的 Modulate 瞬间调亮，再用 Tween 还原。
    ///
    /// 不生成任何新节点，<paramref name="anchor"/> 缺失时静默跳过，不影响调用方。
    /// 数值上完全对应原 EffectPresenter.PresentHitEffect（0.1 秒 / (1.6,1.6,1.6,1)），
    /// 用 <see cref="EffectDefinition.FadeOut"/> 复用"多久还原"这个时间字段，
    /// 不新增专属参数。
    /// </summary>
    private static void PlayFlashEffect(EffectDefinition definition, Control? anchor)
    {
        if (anchor == null || !GodotObject.IsInstanceValid(anchor))
        {
            return;
        }

        var originalModulate = anchor.Modulate;
        anchor.Modulate = new Color(1.6f, 1.6f, 1.6f, 1f);

        var tween = anchor.GetTree()?.CreateTween();
        if (tween == null)
        {
            anchor.Modulate = originalModulate;
            return;
        }

        var duration = definition.FadeOut > 0f ? definition.FadeOut : 0.1f;
        tween.TweenProperty(anchor, "modulate", originalModulate, duration);
    }

    /// <summary>
    /// 贴图类效果的通用播放逻辑：出现 → （可选淡入）→ 停留 Duration → （可选淡出）→ 销毁。
    ///
    /// Slash/Fire/Thunder/Ice/Heal/Shield/Buff/Debuff 等只要配置了 SpriteId，
    /// 都可以复用这一套播放流程；效果之间的区别完全来自 EffectDefinition 的数据，
    /// 不需要为每一类效果单独写一份播放代码。
    /// </summary>
    private static void PlaySpriteEffect(EffectDefinition definition, Control? anchor)
    {
        if (string.IsNullOrEmpty(definition.SpriteId))
        {
            // 尚未配置贴图资源时静默跳过——这也是当前 slash_default 的状态，
            // 用来验证整条调用链路不会报错，等真正有美术资源时补上 SpriteId 即可显示。
            return;
        }

        var texturePath = SpriteDatabase.GetPath(definition.SpriteId);
        if (string.IsNullOrEmpty(texturePath) || !ResourceLoader.Exists(texturePath))
        {
            return;
        }

        var texture = GD.Load<Texture2D>(texturePath);
        if (texture == null)
        {
            return;
        }

        SceneTree? tree = anchor?.GetTree();
        if (tree == null)
        {
            return;
        }

        var sprite = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(definition.Scale, definition.Scale),
            RotationDegrees = definition.Rotation,
            ZIndex = definition.ZIndex
        };

        if (definition.FollowTarget && anchor != null)
        {
            // 跟随锚点：直接挂在锚点下面，用局部 Position，随锚点一起移动。
            anchor.AddChild(sprite);
            sprite.Position = definition.Offset;
        }
        else
        {
            // 不跟随：挂到 RenderLayerManager 提供的共享 CanvasLayer（按
            // EffectDefinition.RenderLayer 决定），用 GlobalPosition 定位一次，
            // 之后即使锚点移动也不会跟着走。
            var overlay = RenderLayerManager.GetOrCreateCanvasLayer(tree, definition.RenderLayer);
            overlay.AddChild(sprite);
            if (anchor != null)
            {
                sprite.GlobalPosition = anchor.GlobalPosition + definition.Offset;
            }
            else
            {
                sprite.Position = definition.Offset;
            }
        }

        if (definition.FadeIn > 0f)
        {
            sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        var tween = tree.CreateTween();
        tween.SetParallel(true);

        if (definition.FadeIn > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 1.0, definition.FadeIn);
        }

        tween.SetParallel(false);
        if (definition.Duration > 0f)
        {
            tween.TweenInterval(definition.Duration);
        }

        if (definition.FadeOut > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 0.0, definition.FadeOut);
        }

        if (definition.Loop)
        {
            // 循环效果：淡入-停留-淡出这一整段作为一个循环单元反复播放，
            // 由调用方自行决定何时用 ClearWeapon 类似的方式移除节点（当前 Effect System
            // 尚未提供停止循环的公开入口，属于预留能力，真正接入循环型效果时再扩展）。
            tween.SetLoops();
        }
        else
        {
            tween.Finished += () =>
            {
                if (GodotObject.IsInstanceValid(sprite))
                {
                    sprite.QueueFree();
                }
            };
        }
    }

    private static void PlaySpriteEffectAt(
        EffectDefinition definition,
        SceneTree tree,
        Vector2 globalPosition)
    {
        if (!TryLoadEffectTexture(definition, out var texture))
        {
            return;
        }

        var sprite = CreateEffectSprite(definition, texture!);
        var overlay = RenderLayerManager.GetOrCreateCanvasLayer(tree, definition.RenderLayer);
        overlay.AddChild(sprite);
        sprite.GlobalPosition = globalPosition + definition.Offset;
        PlayEffectLifetime(definition, tree, sprite);
    }

    private static bool TryLoadEffectTexture(EffectDefinition definition, out Texture2D? texture)
    {
        texture = null;
        if (string.IsNullOrEmpty(definition.SpriteId))
        {
            return false;
        }

        var texturePath = SpriteDatabase.GetPath(definition.SpriteId);
        if (string.IsNullOrEmpty(texturePath) || !ResourceLoader.Exists(texturePath))
        {
            return false;
        }

        texture = GD.Load<Texture2D>(texturePath);
        return texture != null;
    }

    private static Sprite2D CreateEffectSprite(EffectDefinition definition, Texture2D texture)
    {
        var sprite = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(definition.Scale, definition.Scale),
            RotationDegrees = definition.Rotation,
            ZIndex = definition.ZIndex,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };

        if (definition.FadeIn > 0f)
        {
            sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        return sprite;
    }

    private static void PlayEffectLifetime(
        EffectDefinition definition,
        SceneTree tree,
        Sprite2D sprite)
    {
        var tween = tree.CreateTween();
        tween.SetParallel(true);
        if (definition.FadeIn > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 1.0, definition.FadeIn);
        }

        tween.SetParallel(false);
        if (definition.Duration > 0f)
        {
            tween.TweenInterval(definition.Duration);
        }

        if (definition.FadeOut > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 0.0, definition.FadeOut);
        }

        if (definition.Loop)
        {
            tween.SetLoops();
            return;
        }

        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(sprite))
            {
                sprite.QueueFree();
            }
        };
    }
}
