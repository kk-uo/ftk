//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/WeaponPresenter.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 武器视觉由装备和表现资源共同决定，但战斗规则只关心装备效果。
// 独立武器 Presenter 可以避免装备逻辑直接管理图片、挂点或挥动动画。
//
// 职责：
// 1. 定义武器表现接口。
// 2. 让装备视觉、武器挥动、轨迹和挂点从战斗规则中独立出来。
// 3. 为未来不同武器类型的表现差异预留统一入口。
//
// 不负责：
// × 装备属性。
// × 装备掉落。
// × 装备战斗效果。
//
// 主要依赖：
// PresentationEvent / WeaponVisualDatabase / CombatVisualProfileDatabase / EffectPlayer
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 武器表现接口。
///
/// 装备什么武器由逻辑层决定，具体显示哪张图、哪段动画由武器表现层决定。
/// </summary>
public interface IWeaponPresenter
{
    /// <summary>
    /// 显示或刷新武器表现。
    /// </summary>
    void PresentWeapon(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放武器攻击表现。
    /// </summary>
    void PresentWeaponAttack(PresentationEvent presentationEvent);

    /// <summary>
    /// 隐藏或回收武器表现。
    /// </summary>
    void ClearWeapon(PresentationEvent presentationEvent);
}

/// <summary>
/// Phase 2 第一版具体实现：只处理"玩家出【杀】时，在角色右侧生成默认武器精灵，
/// 播放一次挥砍 Tween，结束后自动销毁"这一个场景。
///
/// 不涉及其它卡牌类型、按装备区分武器、连续挥砍等——这些留给后续版本按需扩展，
/// 本版本刻意保持最小实现。
/// </summary>
public sealed class WeaponPresenter : IWeaponPresenter
{
    private const string DefaultWeaponVisualId = "weapon_default";
    private const string DefaultWeaponSpritePath = "res://Assets/Weapons/Sprites/weapon_default.png";
    private const string DefaultWeaponTrailId = "weapon_default_trail";
    private const string DefaultWeaponTrailPath = "res://Assets/Weapons/Trails/weapon_default_trail.png";
    private const string DefaultWeaponAttackAnimationId = "weapon_default_attack";
    private const string FolderWeaponAttackAnimationId = "weapon_folder_attack";

    // 职责划分（第一轮架构整理后）：
    // - WeaponVisualDatabase / WeaponVisualDefinition 只负责武器自己的视觉信息——
    //   武器贴图（SpriteId）、默认 Trail（TrailSpriteId）、武器/Trail 所在的 RenderLayer。
    // - CombatVisualProfileDatabase / AttackVisualProfile 负责这次攻击具体怎么表现——
    //   用哪个 AnimationDefinition（挥砍角度/位移/时长）、Trail 是否被覆盖、
    //   攻击特效/命中特效/攻击音效/命中音效用哪个 Id。
    // 两者不再保存相同职责的数据；以后新增武器（青钢剑/锈剑/方天画戟/双股剑等）或新增
    // 攻击表现（火杀/雷杀/技能/Boss），只需要分别注册一条 WeaponVisualDefinition 和一条
    // AttackVisualProfile，不需要修改这里的播放逻辑。

    // 武器精灵挂在独立 CanvasLayer 下：角色状态卡（CharacterStatusCard）显式设置了
    // ClipContents = true，武器精灵若直接挂在卡片下面、又画到卡片边界之外，会被卡片自己
    // 裁剪掉而完全不可见。这里改用 GlobalPosition 定位，从根源上绕开任何父节点的
    // 裁剪/布局限制，不改动 CharacterStatusCard 本身。具体挂到哪个 CanvasLayer
    // 现在由 RenderLayerManager 按 WeaponVisualDefinition.WeaponLayer/TrailLayer 决定，
    // 不再自己维护一个写死数字的私有 CanvasLayer。

    public WeaponPresenter()
    {
        // 复用 SpriteDatabase / WeaponVisualDatabase 已有的 Register 入口登记默认武器视觉
        // 资源；不修改这两个类本身，只调用它们已经公开的方法，保持既有 Framework 结构不变。
        if (SpriteDatabase.GetPath(DefaultWeaponVisualId) == null)
        {
            SpriteDatabase.Register(DefaultWeaponVisualId, DefaultWeaponSpritePath);
        }

        if (SpriteDatabase.GetPath(DefaultWeaponTrailId) == null)
        {
            SpriteDatabase.Register(DefaultWeaponTrailId, DefaultWeaponTrailPath);
        }

        if (WeaponVisualDatabase.Get(DefaultWeaponVisualId) == null)
        {
            WeaponVisualDatabase.Register(new WeaponVisualDefinition(DefaultWeaponVisualId, spriteId: DefaultWeaponVisualId, trailSpriteId: DefaultWeaponTrailId));
        }

        if (AnimationDatabase.GetAnimation(DefaultWeaponAttackAnimationId) == null)
        {
            // 默认武器是初始原型武器：动作要明显，但不能像高阶武器那样有很强的
            // 科技感。因此只做短距离挥砍、少量淡白拖尾和轻微青色轮廓。
            AnimationDatabase.RegisterAnimation(new AnimationDefinition(
                animationId: DefaultWeaponAttackAnimationId,
                duration: 0.24f,
                startRotation: -62f,
                endRotation: 34f,
                startOffset: new Vector2(-32f, -64f),
                endOffset: new Vector2(-86f, -18f),
                scale: 11.0f,
                fadeInTime: 0f,
                fadeOutTime: 0.16f,
                holdDuration: 0.55f,
                trailEnabled: true,
                trailFadeTime: 0.14f,
                trailScale: 0.92f,
                trailAlpha: 0.42f,
                weaponVisible: true,
                playOnTop: true,
                loop: false));
        }

        if (AnimationDatabase.GetAnimation(FolderWeaponAttackAnimationId) == null)
        {
            // 目录化武器从战场上方向下劈落。起点略窄、终点放大，模拟武器从远处
            // 靠近镜头的纵深感；主要位移保持在Y轴，避免重新产生横向挥舞的观感。
            AnimationDatabase.RegisterAnimation(new AnimationDefinition(
                animationId: FolderWeaponAttackAnimationId,
                duration: 0.24f,
                startRotation: -46f,
                endRotation: -20f,
                startOffset: new Vector2(-18f, -190f),
                endOffset: new Vector2(22f, 42f),
                scale: 1.9f,
                fadeInTime: 0f,
                fadeOutTime: 0.14f,
                holdDuration: 0.545f,
                trailEnabled: false,
                trailFadeTime: 0.10f,
                trailScale: 1.0f,
                trailAlpha: 0.70f,
                weaponVisible: true,
                playOnTop: true,
                loop: false,
                startScale: new Vector2(1.48f, 1.62f),
                endScale: new Vector2(1.96f, 1.82f)));
        }
    }

    /// <summary>
    /// 当前版本不需要独立的"显示/刷新"语义，武器只在攻击瞬间出现并自动销毁。
    /// </summary>
    public void PresentWeapon(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放一次武器挥砍表现：在 <see cref="PresentationEvent.Payload"/> 指定的锚点右侧
    /// 生成默认武器 Sprite2D，Tween 播放旋转 + 位移，结束后自动 QueueFree。
    ///
    /// Payload 必须是一个有效的 Control（通常是角色状态卡片），用作武器的挂载父节点和
    /// 定位参考；缺失贴图或锚点时静默跳过，不影响战斗结算。
    /// </summary>
    public void PresentWeaponAttack(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Payload is not Control anchor || !GodotObject.IsInstanceValid(anchor))
        {
            return;
        }

        var weaponId = string.IsNullOrEmpty(presentationEvent.WeaponId) ? DefaultWeaponVisualId : presentationEvent.WeaponId;
        var tree = anchor.GetTree();
        if (tree == null)
        {
            return;
        }

        var folderResource = WeaponAnimationController.Load(weaponId);
        var usesFolderResource = folderResource?.WeaponTexture != null;
        Texture2D? texture = null;
        AnimationDefinition? animation = null;
        RenderLayer weaponLayer = RenderLayer.Weapon;
        AttackVisualProfile? profile = null;
        string trailSpriteId = string.Empty;

        if (usesFolderResource)
        {
            texture = folderResource!.WeaponTexture;
            animation = AnimationDatabase.GetAnimation(FolderWeaponAttackAnimationId);
        }
        else
        {
            var visual = WeaponVisualDatabase.Get(weaponId);
            if (visual == null || string.IsNullOrEmpty(visual.SpriteId))
            {
                return;
            }

            // 攻击表现（动画/Trail 覆盖/攻击特效/攻击音效）统一从 CombatVisualProfile 读取，
            // 不再按“武器 ID + _attack”约定直接查 AnimationDatabase——WeaponVisualDefinition
            // 只保留武器图片、默认 Trail 和武器自身的图层信息。GetAttackProfile 查不到
            // 专属配置时会自动回退到 DefaultAttackVisualProfile，这里不需要判空。
            profile = CombatVisualProfileDatabase.GetAttackProfile(weaponId);
            animation = AnimationDatabase.GetAnimation(profile.AttackAnimationId);
            weaponLayer = visual.WeaponLayer;
            trailSpriteId = string.IsNullOrEmpty(profile.TrailSpriteId) ? visual.TrailSpriteId : profile.TrailSpriteId;

            var texturePath = SpriteDatabase.GetPath(visual.SpriteId);
            if (string.IsNullOrEmpty(texturePath) || !ResourceLoader.Exists(texturePath))
            {
                // 美术资源尚未放入项目时静默跳过——缺资源不应该影响战斗表现之外的任何逻辑。
                return;
            }

            texture = GD.Load<Texture2D>(texturePath);
        }

        if (texture == null || animation == null || !animation.WeaponVisible)
        {
            return;
        }

        var overlay = RenderLayerManager.GetOrCreateCanvasLayer(tree, weaponLayer);

        var sprite = new Sprite2D
        {
            Texture = texture,
            Scale = animation.StartScale,
            RotationDegrees = animation.StartRotation,
            ZIndex = animation.PlayOnTop ? 20 : 0
        };
        overlay.AddChild(sprite);

        // Trail 贴图：CombatVisualProfile.TrailSpriteId 有值时覆盖
        // WeaponVisualDefinition.TrailSpriteId（武器自身的默认 Trail）；
        // 两者都是同一个概念，不需要两份数据各自维护——Profile 有配置就用 Profile 的，
        // 没配置（默认攻击 Profile 的常见情况）就用武器自身的默认 Trail。
        // 作为武器 Sprite2D 的子节点：不单独维护位置/旋转/缩放，武器的 Transform
        // 变化（挥砍旋转、位移、Scale）会自动带动 Trail 一起变化，无需额外同步代码。
        var trailSprite = !usesFolderResource && animation.TrailEnabled ? CreateTrailSpriteIfAvailable(trailSpriteId, animation) : null;
        if (trailSprite != null)
        {
            sprite.AddChild(trailSprite);
        }

        var basePosition = CalculateWeaponBasePosition(anchor);
        var startPosition = basePosition + animation.StartOffset;
        var endPosition = basePosition + animation.EndOffset;
        sprite.GlobalPosition = startPosition;

        if (usesFolderResource && folderResource != null)
        {
            PlaySlashFrameAnimation(folderResource, tree, overlay, basePosition);
        }
        else if (profile != null && !string.IsNullOrEmpty(profile.AttackEffectId))
        {
            // 攻击特效：交给 EffectPlayer（Effect System 的唯一播放入口），不在这里自己
            // 实例化特效节点。AttackAudioId 目前只读取不播放——项目里还没有真正的音频
            // 播放系统（AudioDatabase 仍是纯登记表），留给以后接入音频时再消费这个字段。
            EffectPlayer.Play(profile.AttackEffectId, anchor);
        }

        // 火杀、雷杀与普通杀使用完全相同的当前武器和挥砍动作，只在武器落点叠加
        // CardVisualProfile 指定的元素层。该坐标来自 Battle Stage 播放点，不跟随 HUD。
        if (!string.IsNullOrEmpty(presentationEvent.EffectId))
        {
            EffectPlayer.PlayAt(presentationEvent.EffectId, tree, basePosition + new Vector2(-8f, -12f));
        }

        // FadeInTime = 0（默认武器的当前配置）时武器一出现就是完全不透明，
        // 只有配置了淡入时间的动画才会先设为透明、再在下面的 Tween 里淡入。
        if (animation.FadeInTime > 0f)
        {
            sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        // 挥砍分三段：① 旋转+位移（劈砍） ② 停留 ③ 武器与 Trail 一起淡出后删除。
        // Trail 不需要独立 Tween 描述位置/旋转——它是武器的子节点，会随父节点的 Transform
        // 自动同步；这里只在最后的淡出阶段让它和武器一起降低透明度。
        var tween = tree.CreateTween();

        tween.SetParallel(true);
        if (animation.FadeInTime > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 1.0, animation.FadeInTime);
        }
        tween.TweenProperty(sprite, "rotation_degrees", animation.EndRotation, animation.Duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(sprite, "global_position", endPosition, animation.Duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(sprite, "scale", animation.EndScale, animation.Duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);

        tween.SetParallel(false);
        if (animation.HoldDuration > 0f)
        {
            tween.TweenInterval(animation.HoldDuration);
        }

        tween.SetParallel(true);
        if (animation.FadeOutTime > 0f)
        {
            tween.TweenProperty(sprite, "modulate:a", 0.0, animation.FadeOutTime);
            if (trailSprite != null)
            {
                tween.TweenProperty(trailSprite, "modulate:a", 0.0, animation.TrailFadeTime);
            }
        }

        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(sprite))
            {
                // Trail 是武器的子节点，QueueFree 武器会一并回收 Trail，不需要单独销毁。
                sprite.QueueFree();
            }
        };
    }

    private static Vector2 CalculateWeaponBasePosition(Control anchor)
    {
        var rect = anchor.GetGlobalRect();
        var viewportSize = anchor.GetViewportRect().Size;

        // 玩家 HUD 已经被放到出牌栏右侧。攻击表现不应该继续贴着 HUD 播放，
        // 否则会挤到手牌和玩家状态 UI 上；右侧 HUD 只作为“玩家侧”判断依据，
        // 真实播放点改到 Battle Stage 中部偏玩家侧。
        if (rect.Position.X + rect.Size.X * 0.5f > viewportSize.X * 0.62f)
        {
            return new Vector2(viewportSize.X * 0.58f, viewportSize.Y * 0.56f);
        }

        return rect.Position + new Vector2(rect.Size.X, rect.Size.Y * 0.5f);
    }

    /// <summary>
    /// 播放目录化武器自带的攻击特效帧。
    ///
    /// 这些帧来自 Weapons/<WeaponId>/slash_XX.png，属于 Battle Stage 表现，
    /// 不进入 UI 层；如果目录里没有帧，只播放武器本体挥动。
    /// </summary>
    private static void PlaySlashFrameAnimation(WeaponAnimationResource resource, SceneTree tree, CanvasLayer overlay, Vector2 basePosition)
    {
        if (resource.SlashFrames.Count == 0)
        {
            return;
        }

        var effect = new Sprite2D
        {
            Texture = resource.SlashFrames[0],
            GlobalPosition = basePosition + new Vector2(26f, 18f),
            RotationDegrees = 90f,
            Scale = new Vector2(1.12f, 1.12f),
            ZIndex = 18,
            Modulate = new Color(1f, 1f, 1f, 0.78f)
        };
        overlay.AddChild(effect);

        var frameTween = tree.CreateTween();
        for (var frameIndex = 0; frameIndex < resource.SlashFrames.Count; frameIndex++)
        {
            var capturedIndex = frameIndex;
            frameTween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(effect))
                {
                    effect.Texture = resource.SlashFrames[capturedIndex];
                }
            }));
            frameTween.TweenInterval(0.045f);
        }

        frameTween.TweenProperty(effect, "modulate:a", 0.0, 0.08f);
        frameTween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(effect))
            {
                effect.QueueFree();
            }
        };
    }

    /// <summary>
    /// 如果武器定义了 Trail 且对应贴图资源存在，创建一个跟随武器的 Trail Sprite2D；
    /// 否则返回 null（不影响武器本身的显示）。缩放和起始透明度读取自
    /// <see cref="AnimationDefinition.TrailScale"/> / <see cref="AnimationDefinition.TrailAlpha"/>。
    ///
    /// 注意：Trail 目前仍然是武器 Sprite2D 的子节点（保证"完全跟随武器、不单独算位置"），
    /// 这意味着它实际显示的 CanvasLayer 始终等于武器所在的 WeaponLayer，
    /// WeaponVisualDefinition.TrailLayer 字段已经存在、可以配置，但只有当它和
    /// WeaponLayer 相同时才会符合实际效果；配成不同层这个能力先留作预留，
    /// 真正需要独立层级的 Trail（不跟随武器父节点）需要额外做位置同步，
    /// 属于以后的扩展，不在这次任务范围内。
    /// </summary>
    private Sprite2D? CreateTrailSpriteIfAvailable(string trailSpriteId, AnimationDefinition animation)
    {
        if (string.IsNullOrEmpty(trailSpriteId))
        {
            return null;
        }

        var trailTexturePath = SpriteDatabase.GetPath(trailSpriteId);
        if (string.IsNullOrEmpty(trailTexturePath) || !ResourceLoader.Exists(trailTexturePath))
        {
            return null;
        }

        var trailTexture = GD.Load<Texture2D>(trailTexturePath);
        if (trailTexture == null)
        {
            return null;
        }

        return new Sprite2D
        {
            Texture = trailTexture,
            // 子节点 Scale = TrailScale：作为武器的子节点，最终显示缩放 = 武器 Scale × 该值，
            // 不单独维护第二套“绝对缩放”，武器放大/缩小时 Trail 自动同步。
            Scale = new Vector2(animation.TrailScale, animation.TrailScale),
            Position = Vector2.Zero,
            // z_as_relative 默认为 true，ZIndex = -1 表示比父节点（武器）低一级，
            // 视觉上位于武器后方、角色前方。
            ZIndex = -1,
            Modulate = new Color(1f, 1f, 1f, animation.TrailAlpha)
        };
    }

    /// <summary>
    /// 当前武器精灵在挥砍动画结束后已经自动销毁，不需要额外的隐藏/回收逻辑。
    /// </summary>
    public void ClearWeapon(PresentationEvent presentationEvent)
    {
    }
}
