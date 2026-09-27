//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.WulongVisual.cs
//
// 模块：Battle UI
//
// 职责：
// 1. 临时验证【观星集智体】Boss 战的大幅立绘摆放效果。
// 2. 只影响 wulong_collective_intelligence 的战斗表现。
//
// 不负责：
// × Boss 立绘框架。
// × 资源数据库。
// × 战斗规则、AI、Trigger 或伤害结算。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// BattleManager 的观星集智体临时视觉扩展。
///
/// 该文件只用于验证单个 Boss 的战斗画面效果，后续如果要正式化，应迁移到表现层框架。
/// </summary>
public partial class BattleManager
{
    private const string WulongVisualEnemyId = "wulong_collective_intelligence";
    private const string WulongVisualTexturePath = "res://Assets/Bosses/BattlePortraits/wulong_collective_intelligence_temp.png";
    private const float WulongVisualScale = 0.78f;
    private const float WulongVisualWindowTopY = 0f;
    private const float WulongVisualSlideOffsetY = -150f;
    private const double WulongVisualSlideDuration = 0.4;

    private TextureRect? _wulongVisualPortrait;
    private Control? _wulongVisualHoverArea;
    private Tween? _wulongVisualTween;
    private bool _wulongVisualSlidePlayed;

    private void UpdateWulongBossVisual()
    {
        var active = IsWulongVisualEncounter();
        UpdateWulongEnemyUiLayout(active);

        if (!active)
        {
            HideWulongBossVisual();
            return;
        }

        EnsureWulongBossVisualCreated();
        if (_wulongVisualPortrait == null || _battlePresentationSource == null)
        {
            return;
        }

        var finalPosition = CalculateWulongVisualPosition(_wulongVisualPortrait);
        _wulongVisualPortrait.Visible = true;
        UpdateWulongVisualHoverArea(finalPosition);

        if (_wulongVisualSlidePlayed)
        {
            _wulongVisualPortrait.Position = finalPosition;
            return;
        }

        _wulongVisualSlidePlayed = true;
        _wulongVisualPortrait.Position = finalPosition + new Vector2(0, WulongVisualSlideOffsetY);
        _wulongVisualTween?.Kill();
        _wulongVisualTween = CreateTween();
        _wulongVisualTween
            .TweenProperty(_wulongVisualPortrait, "position", finalPosition, WulongVisualSlideDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private bool IsWulongVisualEncounter()
    {
        foreach (var enemy in _encounter.Enemies)
        {
            if (enemy.Definition.Id == WulongVisualEnemyId)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureWulongBossVisualCreated()
    {
        if (_wulongVisualPortrait != null && IsInstanceValid(_wulongVisualPortrait))
        {
            return;
        }

        var texture = LoadPresentationTexture(WulongVisualTexturePath);
        if (texture == null)
        {
            GD.PushWarning($"观星集智体临时立绘加载失败：{WulongVisualTexturePath}");
            return;
        }

        _wulongVisualPortrait = new TextureRect
        {
            Name = "WulongCollectiveIntelligenceTempPortrait",
            Texture = texture,
            MouseFilter = MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            ZIndex = 12,
            Visible = false
        };
        _wulongVisualPortrait.Size = new Vector2(texture.GetWidth(), texture.GetHeight());
        _wulongVisualPortrait.Scale = new Vector2(WulongVisualScale, WulongVisualScale);
        AddChild(_wulongVisualPortrait);

        _wulongVisualHoverArea = new Control
        {
            Name = "WulongCollectiveIntelligenceHoverArea",
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 13,
            Visible = false
        };
        _wulongVisualHoverArea.MouseEntered += OnWulongVisualMouseEntered;
        _wulongVisualHoverArea.MouseExited += OnWulongVisualMouseExited;
        AddChild(_wulongVisualHoverArea);
    }

    private Vector2 CalculateWulongVisualPosition(TextureRect portrait)
    {
        var battleRect = _battlePresentationSource?.GetGlobalRect() ?? GetGlobalRect();
        var rootOffset = GetGlobalRect().Position;
        var scaledSize = portrait.Size * WulongVisualScale;
        var x = battleRect.Position.X - rootOffset.X + (battleRect.Size.X - scaledSize.X) * 0.5f;
        var y = WulongVisualWindowTopY;
        return new Vector2(x, y);
    }

    private void UpdateWulongEnemyUiLayout(bool active)
    {
        if (_enemyContainer == null)
        {
            return;
        }

        _enemyContainer.Alignment = BoxContainer.AlignmentMode.Center;
        _enemyContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _enemyContainer.ZIndex = active ? 20 : 0;
    }

    private void UpdateWulongVisualHoverArea(Vector2 finalPosition)
    {
        if (_wulongVisualPortrait == null || _wulongVisualHoverArea == null)
        {
            return;
        }

        var scaledSize = _wulongVisualPortrait.Size * WulongVisualScale;
        var padding = scaledSize * 0.20f;
        _wulongVisualHoverArea.Visible = true;
        _wulongVisualHoverArea.Position = finalPosition - padding;
        _wulongVisualHoverArea.Size = scaledSize + padding * 2f;
    }

    private void HideWulongBossVisual()
    {
        if (_wulongVisualPortrait != null)
        {
            _wulongVisualPortrait.Visible = false;
        }
        if (_wulongVisualHoverArea != null)
        {
            _wulongVisualHoverArea.Visible = false;
        }

        ClearWulongVisualHover();
        _wulongVisualSlidePlayed = false;
        _wulongVisualTween?.Kill();
        _wulongVisualTween = null;
    }

    private void OnWulongVisualMouseEntered()
    {
        var enemy = GetWulongVisualEnemy();
        if (enemy == null || _wulongVisualHoverArea == null)
        {
            return;
        }

        _selectedTarget = enemy;
        RefreshUi();
        TooltipPresenter.ShowEnemy(enemy, _context, _wulongVisualHoverArea);
        if (_bossStatusUi != null)
        {
            _selectionPresenter.HighlightBoss(enemy, _bossStatusUi);
        }
    }

    private void OnWulongVisualMouseExited()
    {
        ClearWulongVisualHover();
    }

    private void ClearWulongVisualHover()
    {
        // 观星集智体离开模型时仅关闭Tooltip。Boss选中高亮与普通敌人一致，
        // 必须持续到玩家切换目标，而不是跟随MouseExited瞬间消失。
        TooltipPresenter.Hide();
    }

    private EnemyInstance? GetWulongVisualEnemy()
    {
        foreach (var enemy in _encounter.Enemies)
        {
            if (enemy.Definition.Id == WulongVisualEnemyId)
            {
                return enemy;
            }
        }

        return null;
    }
}
