//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.BattleStageVisual.cs
//
// 模块：Battle UI
//
// 职责：
// 1. 管理 Battle Stage 内的敌人与 Boss 立绘表现。
// 2. 为普通敌人、精英与 Boss 创建模型绑定组合。
// 3. 让敌人状态 UI 跟随模型，而不是固定在屏幕角落。
//
// 不负责：
// × 敌人数据库或正式视觉资源系统。
// × 敌人 AI、Trigger、伤害、技能或任何战斗规则。
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

/// <summary>
/// BattleManager 的 Battle Stage 立绘扩展。
///
/// Boss 的顶部状态 UI 保持不变；Boss 立绘与普通敌人共用既有舞台锚点，
/// 因此不需要改变场景布局或战斗结算。
/// </summary>
public partial class BattleManager
{
    private readonly struct TemporaryEnemyStageVisual
    {
        public TemporaryEnemyStageVisual(
            string texturePath,
            float scale,
            float statusOffsetY,
            bool isFullWidthBackdrop = false)
        {
            TexturePath = texturePath;
            Scale = scale;
            StatusOffsetY = statusOffsetY;
            IsFullWidthBackdrop = isFullWidthBackdrop;
        }

        public string TexturePath { get; }
        public float Scale { get; }
        public float StatusOffsetY { get; }
        /// <summary>月亮这类巨型背景 Boss 占据战场上半屏，而非普通敌人的站位。</summary>
        public bool IsFullWidthBackdrop { get; }
    }

    private sealed class EnemyStagePresentation
    {
        public EnemyStagePresentation(
            EnemyInstance enemy,
            Control modelAnchor,
            TextureRect model,
            Control hoverArea,
            EnemyStatusUI statusUi,
            EnemyPresenter presenter,
            TemporaryEnemyStageVisual visual)
        {
            Enemy = enemy;
            ModelAnchor = modelAnchor;
            Model = model;
            HoverArea = hoverArea;
            StatusUi = statusUi;
            Presenter = presenter;
            Visual = visual;
        }

        public EnemyInstance Enemy { get; }
        // 布局刷新只移动这个锚点；Model 本身的 Position/Scale 完全交给下面的待机/攻击动画
        // 独占控制，避免 RefreshTemporaryEnemyStageVisualLayout（几乎每次 RefreshUi 都会跑）
        // 把动画中途的位置/缩放强行拉回原点。
        public Control ModelAnchor { get; }
        public TextureRect Model { get; }
        public Control HoverArea { get; }
        public EnemyStatusUI StatusUi { get; }
        public EnemyPresenter Presenter { get; }
        public TemporaryEnemyStageVisual Visual { get; }
        public Tween? IdleTween { get; set; }
        public Tween? AttackTween { get; set; }
    }

    private static readonly TemporaryEnemyStageVisual NormalEnemyFallbackStageVisual = new(
        "res://Assets/Enemies/BattleModels/feather_guard_temp.png",
        2.25f,
        12f);

    private static readonly Dictionary<string, TemporaryEnemyStageVisual> TemporaryEnemyStageVisualsByEnemyId = new()
    {
        // 运行时图统一压缩到固定画布，沿用现有模型锚点与缩放，不改动战场布局。
        ["gate_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/gate_guard_temp.png",
            2.25f,
            12f),
        ["feather_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/feather_guard_temp.png",
            2.25f,
            12f),
        ["abandoned_servant"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/abandoned_servant_temp.png",
            2.25f,
            12f),
        ["scavenger"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/scavenger_temp.png",
            2.25f,
            12f),
        ["exile_barbarian"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/exile_barbarian_temp.png",
            2.5f,
            12f),
        ["corpse_collector"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Generated/corpse_collector_v2.png",
            2.4f,
            12f),
        ["hunter"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Generated/pursuer.png",
            2.4f,
            12f),
        ["steel_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/steel_guard.png",
            2.25f,
            12f),
        ["wine_drinker"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/wine_drinker.png",
            2.25f,
            12f),
        ["modified_thug"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/modified_thug.png",
            2.25f,
            12f),
        ["collective_intelligence"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/collective_intelligence.png",
            2.4f,
            12f),
        ["shixin_zhe"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/shixin_zhe.png",
            2.4f,
            12f),
        ["cyclops"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/cyclops.png",
            2.4f,
            12f),
        ["yellow_turban_devotee"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/yellow_turban_devotee.png",
            2.4f,
            12f),
        ["giant_pus_sac"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/giant_pus_sac.png",
            2.25f,
            12f),
        ["giant_mech_cockroach"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/giant_mech_cockroach.png",
            2.25f,
            12f),
        ["giant_mech_rat"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/giant_mech_rat.png",
            2.25f,
            12f),
        ["mihuan_xiao_shou"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/mihuan_xiao_shou.png",
            2.25f,
            12f),
        ["gunner"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/gunner.png",
            2.25f,
            12f),
        ["renwan_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/renwan_guard.png",
            2.25f,
            12f),
        ["heavy_armor_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/heavy_armor_guard.png",
            2.25f,
            12f),
        ["ice_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/ice_guard.png",
            2.25f,
            12f),
        ["royal_death_guard"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/royal_death_guard.png",
            2.4f,
            12f),
        ["giant_rolling_stone"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/giant_rolling_stone.png",
            2.4f,
            12f),
        ["giant_rolling_log"] = new TemporaryEnemyStageVisual(
            "res://Assets/Enemies/BattleModels/Runtime/giant_rolling_log.png",
            2.4f,
            12f),
        ["crazy_performer"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/crazy_performer.png",
            2.45f,
            12f),
        ["boss_healer"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/boss_healer.png",
            2.45f,
            12f),
        ["boss_traitor"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/boss_traitor.png",
            2.45f,
            12f),
        ["wulong_collective_intelligence"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/wulong_collective_intelligence.png",
            2.45f,
            12f),
        ["zuoci"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/zuoci.png",
            2.45f,
            12f),
        ["gang_boss"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/gang_boss.png",
            2.45f,
            12f),
        ["huang_yi_zhi_zhu"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/huang_yi_zhi_zhu.png",
            2.45f,
            12f),
        ["boss_tyrant"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/boss_tyrant.png",
            2.45f,
            12f),
        ["liu_bei"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/liu_bei.png",
            2.45f,
            12f),
        ["guan_yu"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/guan_yu.png",
            2.45f,
            12f),
        ["zhang_fei"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/zhang_fei.png",
            2.45f,
            12f),
        ["rat_king"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/rat_king.png",
            2.45f,
            12f),
        ["moon_boss"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/moon_boss_rising_true_pixel.png",
            1f,
            12f,
            isFullWidthBackdrop: true),
        ["huan_xiang_chu_shou"] = new TemporaryEnemyStageVisual(
            "res://Assets/Bosses/BattlePortraits/Runtime/huan_xiang_chu_shou.png",
            2.10f,
            12f)
    };

    private readonly List<EnemyStagePresentation> _enemyStagePresentations = new();
    private EnemyInstance? _geometryHoveredStageEnemy;

    private void UpdateBattleStageEnemyVisuals()
    {
        if (_battleStageVisualLayer == null)
        {
            return;
        }

        var enemies = GetStageVisualEnemies();
        SyncEnemyStagePresentationCount(enemies);

        for (var i = 0; i < _enemyStagePresentations.Count; i++)
        {
            var presentation = _enemyStagePresentations[i];
            presentation.StatusUi.Refresh(presentation.Enemy, _context);
            presentation.StatusUi.Visible = ShouldShowIndividualStageStatus(presentation.Enemy);
            presentation.Model.Modulate = presentation.Enemy.IsDead
                ? new Color(0.55f, 0.55f, 0.55f, 0.72f)
                : Colors.White;

            // 死亡后停止待机呼吸动画，把 Model 复位到基准位置/缩放，避免残影般地继续浮动。
            if (presentation.Enemy.IsDead && presentation.IdleTween != null)
            {
                presentation.IdleTween.Kill();
                presentation.IdleTween = null;
                presentation.Model.Position = Vector2.Zero;
                presentation.Model.Scale = new Vector2(presentation.Visual.Scale, presentation.Visual.Scale);
            }
        }

        RefreshTemporaryEnemyStageVisualLayout();
    }

    private List<EnemyInstance> GetStageVisualEnemies()
    {
        var result = new List<EnemyInstance>();
        foreach (var enemy in _encounter.Enemies)
        {
            result.Add(enemy);
        }

        return result;
    }

    private List<EnemyInstance> GetVisibleBossEnemies()
    {
        var result = new List<EnemyInstance>();
        foreach (var enemy in _encounter.Enemies)
        {
            if (enemy.Definition.Type == EnemyType.Boss && !enemy.IsDead)
            {
                result.Add(enemy);
            }
        }

        return result;
    }

    private void SyncEnemyStagePresentationCount(List<EnemyInstance> enemies)
    {
        while (_enemyStagePresentations.Count > enemies.Count)
        {
            var last = _enemyStagePresentations[^1];
            RemoveEnemyStagePresentation(last);
            _enemyStagePresentations.RemoveAt(_enemyStagePresentations.Count - 1);
        }

        for (var i = 0; i < enemies.Count; i++)
        {
            if (i < _enemyStagePresentations.Count && ReferenceEquals(_enemyStagePresentations[i].Enemy, enemies[i]))
            {
                continue;
            }

            if (i < _enemyStagePresentations.Count)
            {
                RemoveEnemyStagePresentation(_enemyStagePresentations[i]);
                _enemyStagePresentations.RemoveAt(i);
            }

            _enemyStagePresentations.Insert(i, CreateEnemyStagePresentation(enemies[i]));
        }
    }

    private static void RemoveEnemyStagePresentation(EnemyStagePresentation presentation)
    {
        presentation.IdleTween?.Kill();
        presentation.AttackTween?.Kill();
        presentation.ModelAnchor.QueueFree();
        presentation.HoverArea.QueueFree();
        presentation.StatusUi.QueueFree();
    }

    private EnemyStagePresentation CreateEnemyStagePresentation(EnemyInstance enemy)
    {
        var visual = GetTemporaryEnemyStageVisual(enemy);
        var texture = LoadEnemyStageTexture(visual.TexturePath);

        // 锚点负责“这个敌人站在舞台上的哪个位置”（由 RefreshTemporaryEnemyStageVisualLayout
        // 每次布局刷新时设置）；Model 自己的 Position/Scale 则完全交给待机/攻击动画独占，
        // 两者分层后动画就不会被频繁的布局刷新打断、瞬间弹回原位。
        var modelAnchor = new Control
        {
            Name = $"EnemyModelAnchor_{enemy.Definition.Id}",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 20
        };
        _battleStageVisualLayer!.AddChild(modelAnchor);

        var model = new TextureRect
        {
            Name = $"EnemyModel_{enemy.Definition.Id}",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = visual.IsFullWidthBackdrop
                ? TextureRect.StretchModeEnum.Scale
                : TextureRect.StretchModeEnum.Keep,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest
        };
        model.Size = texture == null ? new Vector2(128, 128) : new Vector2(texture.GetWidth(), texture.GetHeight());
        model.Scale = new Vector2(visual.Scale, visual.Scale);
        modelAnchor.AddChild(model);

        var hoverArea = new Control
        {
            Name = $"EnemyHoverArea_{enemy.Definition.Id}",
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 30
        };
        _battleStageVisualLayer.AddChild(hoverArea);

        var statusUi = new EnemyStatusUI
        {
            Name = $"EnemyStatusUI_{enemy.Definition.Id}",
            ZIndex = 35,
            MouseFilter = MouseFilterEnum.Stop,
            Visible = ShouldShowIndividualStageStatus(enemy)
        };
        statusUi.MouseEntered += () => OnEnemyStageVisualMouseEntered(enemy, statusUi);
        statusUi.MouseExited += () => OnEnemyStageVisualMouseExited(enemy);
        statusUi.GuiInput += @event => OnEnemyStageVisualInput(enemy, @event);
        _battleStageVisualLayer.AddChild(statusUi);

        var presenter = new EnemyPresenter(
            hoverArea,
            enemy,
            OnEnemyStageVisualMouseEntered,
            OnEnemyStageVisualMouseExited,
            OnEnemyStageVisualInput);

        var presentation = new EnemyStagePresentation(enemy, modelAnchor, model, hoverArea, statusUi, presenter, visual);
        StartEnemyIdleAnimation(presentation);
        return presentation;
    }

    /// <summary>
    /// 待机呼吸动画：Model 的位置/缩放围绕其基准值做一个缓慢的正弦式循环
    /// （轻微上下浮动 + 轻微缩放），让静止的立绘看起来在“活着”而不是死图。
    /// 幅度很小、节奏很慢，不会干扰玩家读牌。
    /// </summary>
    private void StartEnemyIdleAnimation(EnemyStagePresentation presentation)
    {
        presentation.IdleTween?.Kill();

        var baseScale = new Vector2(presentation.Visual.Scale, presentation.Visual.Scale);
        var bobHeight = presentation.Visual.IsFullWidthBackdrop ? 12f : 6f;
        var scalePulse = presentation.Visual.IsFullWidthBackdrop ? 0f : 0.02f;
        var halfCycleSeconds = presentation.Visual.IsFullWidthBackdrop ? 2.8f : 1.35f;

        var tween = presentation.Model.CreateTween();
        tween.SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(presentation.Model, "position:y", -bobHeight, halfCycleSeconds);
        if (scalePulse > 0f)
            tween.Parallel().TweenProperty(presentation.Model, "scale", baseScale * (1f + scalePulse), halfCycleSeconds);
        tween.Chain().TweenProperty(presentation.Model, "position:y", 0f, halfCycleSeconds);
        if (scalePulse > 0f)
            tween.Parallel().TweenProperty(presentation.Model, "scale", baseScale, halfCycleSeconds);

        presentation.IdleTween = tween;
    }

    /// <summary>
    /// 攻击动画：向玩家方向（舞台下方）猛地冲出一段距离，用 Back 缓动制造“扑击感”，
    /// 再弹回原位；播放期间暂停待机呼吸动画，避免两个 Tween 同时抢 Position/Scale，
    /// 结束后恢复待机动画。找不到该敌人的舞台表现（例如敌人已经死亡被移除）时直接跳过。
    /// </summary>
    private void PlayEnemyAttackLunge(EnemyInstance enemy)
    {
        var presentation = GetEnemyStagePresentation(enemy);
        if (presentation == null || !IsInstanceValid(presentation.Model))
        {
            return;
        }

        presentation.IdleTween?.Kill();
        presentation.AttackTween?.Kill();

        var baseScale = new Vector2(presentation.Visual.Scale, presentation.Visual.Scale);
        presentation.Model.Position = Vector2.Zero;
        presentation.Model.Scale = baseScale;

        const float lungeDistance = 70f;
        const float lungeOutSeconds = 0.14f;
        const float lungeBackSeconds = 0.22f;

        var tween = presentation.Model.CreateTween();
        tween.TweenProperty(presentation.Model, "position:y", lungeDistance, lungeOutSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (!presentation.Visual.IsFullWidthBackdrop)
        {
            tween.Parallel().TweenProperty(presentation.Model, "scale", baseScale * 1.08f, lungeOutSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        tween.Chain().TweenProperty(presentation.Model, "position:y", 0f, lungeBackSeconds)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        if (!presentation.Visual.IsFullWidthBackdrop)
        {
            tween.Parallel().TweenProperty(presentation.Model, "scale", baseScale, lungeBackSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        tween.TweenCallback(Callable.From(() =>
        {
            presentation.AttackTween = null;
            if (!presentation.Enemy.IsDead)
            {
                StartEnemyIdleAnimation(presentation);
            }
        }));

        presentation.AttackTween = tween;
    }

    private static TemporaryEnemyStageVisual GetTemporaryEnemyStageVisual(EnemyInstance enemy)
    {
        return TemporaryEnemyStageVisualsByEnemyId.TryGetValue(enemy.Definition.Id, out var visual)
            ? visual
            : NormalEnemyFallbackStageVisual;
    }

    private static Texture2D? LoadEnemyStageTexture(string texturePath)
    {
        if (ElitePortraitTextures.IsElitePortrait(texturePath))
            return ElitePortraitTextures.Load(texturePath);
        return LoadPresentationTexture(texturePath);
    }

    private static Texture2D? LoadPresentationTexture(string texturePath)
    {
        var texture = GD.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            return texture;
        }

        GD.PushWarning($"Presentation 贴图加载失败：{texturePath}");
        return null;
    }

    private void RefreshTemporaryEnemyStageVisualLayout()
    {
        if (_battleStageVisualLayer == null || _enemyStagePresentations.Count == 0)
        {
            return;
        }

        var stageSize = _battleStageVisualLayer.Size;
        if (stageSize.X <= 0 || stageSize.Y <= 0)
        {
            return;
        }

        var count = _enemyStagePresentations.Count;
        for (var i = 0; i < count; i++)
        {
            var presentation = _enemyStagePresentations[i];
            if (!IsInstanceValid(presentation.ModelAnchor) || !IsInstanceValid(presentation.Model) || !IsInstanceValid(presentation.StatusUi))
            {
                continue;
            }

            var visual = presentation.Visual;
            if (visual.IsFullWidthBackdrop)
            {
                // 月亮以“从战场边缘升起”的大背景方式出现：占据上半屏而不挤压下方卡牌区。
                var moonSize = new Vector2(stageSize.X * 0.92f, stageSize.Y * 0.58f);
                var moonPosition = new Vector2((stageSize.X - moonSize.X) * 0.5f, -stageSize.Y * 0.03f);
                presentation.Model.Size = moonSize;
                presentation.ModelAnchor.Position = moonPosition;
                presentation.Model.TextureFilter = TextureFilterEnum.Nearest;
                PositionEnemyHoverArea(presentation.HoverArea, moonPosition, moonSize);
                presentation.StatusUi.Visible = false;
                continue;
            }

            var scaledSize = presentation.Model.Size * visual.Scale;
            var xRatio = GetEnemyStageXRatio(i, count);
            var modelTop = stageSize.Y * 0.10f;
            var modelX = stageSize.X * xRatio - scaledSize.X * 0.5f;
            var statusX = stageSize.X * xRatio - presentation.StatusUi.CustomMinimumSize.X * 0.5f;
            var statusY = modelTop + scaledSize.Y + visual.StatusOffsetY;

            // 只移动锚点本身；Model 的 Position/Scale 完全交给待机/攻击动画独占（见
            // StartEnemyIdleAnimation/PlayEnemyAttackLunge），布局刷新不再触碰它们。
            presentation.ModelAnchor.Position = new Vector2(modelX, modelTop);
            presentation.Model.TextureFilter = TextureFilterEnum.Nearest;
            PositionEnemyHoverArea(presentation.HoverArea, presentation.ModelAnchor.Position, scaledSize);
            presentation.StatusUi.Size = presentation.StatusUi.CustomMinimumSize;
            presentation.StatusUi.Position = new Vector2(statusX, statusY);
        }
    }

    private static void PositionEnemyHoverArea(Control hoverArea, Vector2 modelPosition, Vector2 scaledSize)
    {
        var padding = scaledSize * 0.20f;
        hoverArea.Position = modelPosition - padding;
        hoverArea.Size = scaledSize + padding * 2f;
    }

    private static float GetEnemyStageXRatio(int index, int count)
    {
        return count switch
        {
            <= 1 => 0.58f,
            2 => index == 0 ? 0.42f : 0.66f,
            3 => index switch
            {
                0 => 0.34f,
                1 => 0.52f,
                _ => 0.70f
            },
            _ => 0.28f + index * (0.52f / System.Math.Max(1, count - 1))
        };
    }

    /// <summary>
    /// 单体 Boss 继续使用顶部的大型 Boss 信息栏；共享生命池中的多名 Boss 则必须保留
    /// 各自的舞台状态框，才能明确选择刘备、关羽或张飞其中一人。三个框的生命会同步，
    /// 但费用、技能、行动和目标身份仍属于各自的 EnemyInstance。
    /// </summary>
    private static bool ShouldShowIndividualStageStatus(EnemyInstance enemy)
    {
        return enemy.Definition.Type != EnemyType.Boss
            || (enemy.SharedPool != null && enemy.SharedPool.Members.Count > 1);
    }

    private void OnEnemyStageVisualInput(EnemyInstance enemy, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }

        if (enemy.IsDead)
        {
            return;
        }

        _selectedTarget = enemy;
        RefreshSelectedTargetHighlight();
        RefreshUi();
    }

    private void OnEnemyStageVisualMouseEntered(EnemyInstance enemy, Control model)
    {
        var presentation = GetEnemyStagePresentation(enemy);
        if (presentation == null)
        {
            return;
        }

        _selectedTarget = enemy;
        RefreshUi();
        TooltipPresenter.ShowEnemy(enemy, _context, model);
        HighlightStageEnemy(enemy, presentation);
    }

    private void OnEnemyStageVisualMouseExited(EnemyInstance enemy)
    {
        if (IsMouseInsideEnemyStagePresentation(enemy))
        {
            return;
        }

        // 鼠标离开只结束悬停说明，不清除当前目标。选中高亮由 _selectedTarget 驱动，
        // 直到玩家移入/点击另一个敌人或当前目标死亡时才由 RefreshSelectedTargetHighlight 切换。
        TooltipPresenter.Hide();
    }

    private void UpdateBattleStageGeometryHover()
    {
        var hoveredEnemy = FindGeometryHoveredStageEnemy();
        if (ReferenceEquals(_geometryHoveredStageEnemy, hoveredEnemy))
        {
            return;
        }

        if (_geometryHoveredStageEnemy != null)
        {
            var previous = _geometryHoveredStageEnemy;
            _geometryHoveredStageEnemy = null;
            OnEnemyStageVisualMouseExited(previous);
        }

        if (hoveredEnemy == null)
        {
            return;
        }

        var presentation = GetEnemyStagePresentation(hoveredEnemy);
        if (presentation == null)
        {
            return;
        }

        _geometryHoveredStageEnemy = hoveredEnemy;
        OnEnemyStageVisualMouseEntered(hoveredEnemy, presentation.HoverArea);
    }

    private EnemyInstance? FindGeometryHoveredStageEnemy()
    {
        var mouse = GetViewport().GetMousePosition();

        for (var i = _enemyStagePresentations.Count - 1; i >= 0; i--)
        {
            var presentation = _enemyStagePresentations[i];
            if (presentation.Enemy.IsDead)
            {
                continue;
            }

            if (IsControlRectHovered(presentation.HoverArea, mouse)
                || IsStatusUiHovered(presentation.StatusUi, mouse))
            {
                return presentation.Enemy;
            }
        }

        return null;
    }

    private bool IsMouseInsideEnemyStagePresentation(EnemyInstance enemy)
    {
        var presentation = GetEnemyStagePresentation(enemy);
        if (presentation == null)
        {
            return false;
        }

        var mouse = GetViewport().GetMousePosition();
        return IsControlRectHovered(presentation.HoverArea, mouse)
            || IsStatusUiHovered(presentation.StatusUi, mouse);
    }

    private static bool IsControlRectHovered(Control control, Vector2 mousePosition)
    {
        return GodotObject.IsInstanceValid(control)
            && control.Visible
            && control.GetGlobalRect().HasPoint(mousePosition);
    }

    private static bool IsStatusUiHovered(EnemyStatusUI statusUi, Vector2 mousePosition)
    {
        return GodotObject.IsInstanceValid(statusUi)
            && statusUi.Visible
            && statusUi.GlobalHoverRect.HasPoint(mousePosition);
    }

    private void RefreshSelectedTargetHighlight()
    {
        var selected = GetResolvedSelectedTarget();
        if (selected is not EnemyInstance enemy)
        {
            _selectionPresenter.ClearAll();
            return;
        }

        if (!ShouldShowIndividualStageStatus(enemy)
            && _bossStatusUi != null
            && _bossStatusUi.Visible)
        {
            _selectionPresenter.HighlightBoss(enemy, _bossStatusUi);
            return;
        }

        var presentation = GetEnemyStagePresentation(enemy);
        if (presentation != null)
        {
            _selectionPresenter.HighlightEnemy(enemy, presentation.StatusUi);
            return;
        }

        _selectionPresenter.ClearAll();
    }

    private void HighlightStageEnemy(EnemyInstance enemy, EnemyStagePresentation presentation)
    {
        if (!ShouldShowIndividualStageStatus(enemy)
            && _bossStatusUi != null
            && _bossStatusUi.Visible)
        {
            _selectionPresenter.HighlightBoss(enemy, _bossStatusUi);
            return;
        }

        _selectionPresenter.HighlightEnemy(enemy, presentation.StatusUi);
    }

    private Control? GetEnemyStageModelAnchor(EnemyInstance enemy)
    {
        foreach (var presentation in _enemyStagePresentations)
        {
            if (ReferenceEquals(presentation.Enemy, enemy))
            {
                return presentation.Model;
            }
        }

        return null;
    }

    private Control? GetEnemyStageManaAnchor(EnemyInstance enemy)
    {
        foreach (var presentation in _enemyStagePresentations)
        {
            if (ReferenceEquals(presentation.Enemy, enemy))
            {
                return presentation.StatusUi;
            }
        }

        return null;
    }

    private EnemyStagePresentation? GetEnemyStagePresentation(EnemyInstance enemy)
    {
        foreach (var presentation in _enemyStagePresentations)
        {
            if (ReferenceEquals(presentation.Enemy, enemy))
            {
                return presentation;
            }
        }

        return null;
    }
}
