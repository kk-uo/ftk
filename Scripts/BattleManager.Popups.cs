//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Popups.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    // 揭示结果属于“上一轮结算信息”，生命周期由下一次正式行动确认驱动，
    // 不能使用固定秒数销毁，否则玩家尚未读完或尚未出下一招时信息就会消失。
    private readonly List<Control> _activeCardRevealNodes = new();

    private void ShowBattlePhasePopups(BattleSnapshot before)
    {
        if (_context == null)
        {
            return;
        }

        foreach (var damage in _context.RoundResult.DamageByTarget)
        {
            ShowFloatingText(
                GetAvatarAnchor(damage.Key),
                $"-{damage.Value}",
                new Color(1.0f, 0.22f, 0.18f));
        }
        foreach (var target in _context.RoundResult.FullBlockTargets)
        {
            var anchor = GetAvatarAnchor(target);
            var isPlayerDodgeParry = ReferenceEquals(target, _player)
                && _context.PlayerAction?.IsDodge == true
                && _context.RoundResult.PlayerCardDodgeBlocks > 0;

            if (isPlayerDodgeParry)
            {
                ShowPlayerDodgeShieldBorder();
            }
            else
            {
                PresentationManager.PlayBlock(new PresentationEvent(
                    PresentationEventType.BlockResolved,
                    payload: anchor));
                ShowFloatingText(
                    anchor,
                    Localization.Get("battle.popup.blocked"),
                    new Color(0.48f, 0.88f, 1.0f));
            }
        }
        if (_context.RoundResult.PlayerHeal > 0)
        {
            ShowFloatingText(GetPrimaryFriendlyAvatarAnchor(), $"+{_context.RoundResult.PlayerHeal}", new Color(0.32f, 1.0f, 0.52f));
        }
        if (_context.RoundResult.EnemyHeal > 0)
        {
            ShowFloatingText(GetPrimaryEnemyAvatarAnchor(), $"+{_context.RoundResult.EnemyHeal}", new Color(0.32f, 1.0f, 0.52f));
        }

        ShowManaDeltaPopup(GetPrimaryFriendlyManaAnchor(), before.PlayerMana, _player.CurrentMana);
        ShowManaDeltaPopup(GetPrimaryEnemyManaAnchor(), before.EnemyMana, GetFirstAliveEnemy()?.CurrentMana ?? 0);
    }

    private void ShowDeltaPopups(BattleSnapshot before)
    {
        var enemy = GetFirstAliveEnemy();
        ShowHealthDeltaPopup(GetPrimaryFriendlyAvatarAnchor(), before.PlayerHealth, _player.Health);
        ShowHealthDeltaPopup(GetPrimaryEnemyAvatarAnchor(), before.EnemyHealth, enemy?.Health ?? before.EnemyHealth);
        ShowManaDeltaPopup(GetPrimaryFriendlyManaAnchor(), before.PlayerMana, _player.CurrentMana);
        ShowManaDeltaPopup(GetPrimaryEnemyManaAnchor(), before.EnemyMana, enemy?.CurrentMana ?? before.EnemyMana);
    }

    /// <summary>
    /// 【战场崩坏】表现层：第55回合起，每次 OnTurnEnd 环境伤害结算后，
    /// 对玩家和每一个存活敌人分别显示一次伤害飘字 + 统一环境伤害特效。
    ///
    /// 不依赖 ShowDeltaPopups 的单一 BattleSnapshot 差值（那套机制天生只追踪
    /// "第一个存活敌人"一份 EnemyHealth，多敌人战斗时后面的敌人不会有飘字），
    /// 这里直接用 BattlefieldCollapseEffect.CalculateDamage 算出的确定值，
    /// 对 _enemyCards 里每一个存活敌人各自弹一次，规则和表现读的是同一个数字。
    /// </summary>
    private void ShowBattlefieldCollapseDamagePopups()
    {
        var damage = BattlefieldCollapseEffect.CalculateDamage(_turnNumber);
        if (damage <= 0)
        {
            return;
        }

        var damageText = $"-{damage}";
        var damageColor = new Color(1.0f, 0.22f, 0.18f);

        if (!_player.IsDead)
        {
            var playerAnchor = GetPrimaryFriendlyAvatarAnchor();
            ShowFloatingText(playerAnchor, damageText, damageColor);
            EffectPlayer.Play(EffectDatabase.BattlefieldCollapseDamageEffectId, playerAnchor);
        }

        foreach (var card in _enemyCards)
        {
            if (card.Unit is not EnemyInstance enemy || enemy.IsDead)
            {
                continue;
            }

            ShowFloatingText(card.AvatarAnchor, damageText, damageColor);
            EffectPlayer.Play(EffectDatabase.BattlefieldCollapseDamageEffectId, card.AvatarAnchor);
        }
    }

    private Control? GetPrimaryEnemyAvatarAnchor()
    {
        var enemy = GetFirstAliveEnemy();
        if (enemy == null)
            return _enemyCards.Count > 0 ? _enemyCards[0].AvatarAnchor : null;

        if (enemy is EnemyInstance stageEnemy)
        {
            var stageAnchor = GetEnemyStageModelAnchor(stageEnemy);
            if (stageAnchor != null)
            {
                return stageAnchor;
            }
        }

        foreach (var card in _enemyCards)
        {
            if (card.Unit == enemy)
                return card.AvatarAnchor;
        }

        return _enemyCards.Count > 0 ? _enemyCards[0].AvatarAnchor : null;
    }

    private Control? GetAvatarAnchor(Player unit)
    {
        if (ReferenceEquals(unit, _player))
        {
            return GetPrimaryFriendlyAvatarAnchor();
        }

        if (unit is EnemyInstance enemy)
        {
            var stageAnchor = GetEnemyStageModelAnchor(enemy);
            if (stageAnchor != null)
            {
                return stageAnchor;
            }
        }

        foreach (var card in _enemyCards)
        {
            if (ReferenceEquals(card.Unit, unit))
            {
                return card.AvatarAnchor;
            }
        }

        return null;
    }

    private Control? GetPrimaryEnemyManaAnchor()
    {
        var enemy = GetFirstAliveEnemy();
        if (enemy == null)
            return _enemyCards.Count > 0 ? _enemyCards[0].ManaAnchor : null;

        if (enemy is EnemyInstance stageEnemy)
        {
            var stageAnchor = GetEnemyStageManaAnchor(stageEnemy);
            if (stageAnchor != null)
            {
                return stageAnchor;
            }
        }

        foreach (var card in _enemyCards)
        {
            if (card.Unit == enemy)
                return card.ManaAnchor;
        }

        return _enemyCards.Count > 0 ? _enemyCards[0].ManaAnchor : null;
    }

    private void ShowHealthDeltaPopup(Control? anchor, int before, int after)
    {
        var delta = after - before;
        if (delta == 0)
        {
            return;
        }

        var color = delta > 0 ? new Color(0.32f, 1.0f, 0.52f) : new Color(1.0f, 0.22f, 0.18f);
        var prefix = delta > 0 ? "+" : string.Empty;
        ShowFloatingText(anchor, $"{prefix}{delta}", color);
    }

    private void ShowManaDeltaPopup(Control? anchor, double before, double after)
    {
        var delta = after - before;
        if (System.Math.Abs(delta) < 0.001)
        {
            return;
        }

        var color = delta > 0 ? new Color(0.42f, 0.82f, 1.0f) : new Color(1.0f, 0.74f, 0.24f);
        var amount = BattleRules.FormatMana(System.Math.Abs(delta));
        var textKey = delta > 0 ? "battle.popup.mana_gain_fmt" : "battle.popup.mana_loss_fmt";
        ShowFloatingText(anchor, Localization.GetFmt(textKey, amount), color);
    }

    private void UpdateBattlePresentationArea()
    {
        if (_battlePresentationSource == null || _battlePresentationArea == null)
        {
            return;
        }

        var sourceRect = _battlePresentationSource.GetGlobalRect();
        var rootRect = GetGlobalRect();
        _battlePresentationArea.Position = sourceRect.Position - rootRect.Position;
        _battlePresentationArea.Size = sourceRect.Size;
    }

    private void ShowCardRevealPopup(BattleAction playerAction, IReadOnlyList<EnemyActionEntry> enemyActions)
    {
        var parent = _battlePresentationArea ?? this;
        UpdateBattlePresentationArea();
        if (parent == null)
        {
            return;
        }

        var enemyLabels = ShouldUseCenteredBossReveal(enemyActions)
            ? CreateCenteredBossRevealLabel(enemyActions, parent)
            : CreateEnemyRevealLabels(enemyActions, parent);

        var playerParent = (Control)this;

        // 玩家牌名贴在出牌栏上方，避免占用 Battle Stage 的攻击动画区域。
        var playerLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(620, 64),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 140,
            Text = $"[center][font_size=46][b][color=#35e0b5]{EscapeBbCode(playerAction.DisplayName)}[/color][/b][/font_size][/center]"
        };

        playerParent.AddChild(playerLabel);
        playerLabel.Position = CalculatePlayerRevealLabelPosition(playerParent, playerLabel.CustomMinimumSize);
        playerLabel.Modulate = Colors.White;
        playerLabel.Scale = new Vector2(1.06f, 1.06f);

        _activeCardRevealNodes.Clear();
        _activeCardRevealNodes.AddRange(enemyLabels);
        _activeCardRevealNodes.Add(playerLabel);

        var tween = CreateTween();
        tween.SetParallel(true);
        foreach (var enemyLabel in enemyLabels)
        {
            tween.TweenProperty(enemyLabel, "scale", Vector2.One, 0.16)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        tween.TweenProperty(playerLabel, "scale", Vector2.One, 0.16)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// 在下一轮行动正式确认后移除上一轮揭示结果。
    ///
    /// 不能在卡牌刚被悬停、费用不足或其它无效点击时调用；这些操作并不代表玩家已经出招。
    /// </summary>
    private void DismissCardRevealPopup()
    {
        foreach (var node in _activeCardRevealNodes)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                // QueueFree 要到帧末才真正释放；先隐藏可避免新一轮结果在同一帧生成时短暂重叠。
                node.Visible = false;
                node.QueueFree();
            }
        }

        _activeCardRevealNodes.Clear();
    }

    private List<RichTextLabel> CreateEnemyRevealLabels(IReadOnlyList<EnemyActionEntry> enemyActions, Control parent)
    {
        var labels = new List<RichTextLabel>();
        foreach (var entry in enemyActions)
        {
            if (entry.Enemy.IsDead)
            {
                continue;
            }

            var anchor = GetEnemyActionRevealAnchor(entry.Enemy);
            if (anchor == null)
            {
                continue;
            }

            var label = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                CustomMinimumSize = new Vector2(260, 82),
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 90,
                Text = $"[center][font_size=42][b][color=#ffe3b4]{EscapeBbCode(entry.Action.DisplayName)}[/color][/b][/font_size][/center]"
            };
            parent.AddChild(label);
            label.Modulate = Colors.White;
            label.Scale = new Vector2(1.06f, 1.06f);
            label.Position = CalculateEnemyRevealLabelPosition(anchor, parent, label.CustomMinimumSize);
            labels.Add(label);
        }

        return labels;
    }

    private static bool ShouldUseCenteredBossReveal(IReadOnlyList<EnemyActionEntry> enemyActions)
    {
        foreach (var entry in enemyActions)
        {
            if (!entry.Enemy.IsDead && entry.Enemy.Definition.Type == EnemyType.Boss)
            {
                return true;
            }
        }

        return false;
    }

    private List<RichTextLabel> CreateCenteredBossRevealLabel(IReadOnlyList<EnemyActionEntry> enemyActions, Control parent)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(1120, 96),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 90,
            Text = BuildEnemyRevealText(enemyActions, includeEnemyNames: true)
        };

        parent.AddChild(label);
        label.Position = CalculateBossRevealLabelPosition(parent, label.CustomMinimumSize);
        label.Modulate = Colors.White;
        label.Scale = new Vector2(1.06f, 1.06f);
        return new List<RichTextLabel> { label };
    }

    private Control? GetEnemyActionRevealAnchor(EnemyInstance enemy)
    {
        var stageAnchor = GetEnemyStageManaAnchor(enemy);
        if (stageAnchor != null)
        {
            return stageAnchor;
        }

        if (_bossStatusUi != null && _bossStatusUi.Visible && ReferenceEquals(_bossStatusUi.Unit, enemy))
        {
            return _bossStatusUi;
        }

        var statusCard = GetEnemyCardForUnit(enemy);
        return statusCard?.AvatarAnchor;
    }

    private static Vector2 CalculateEnemyRevealLabelPosition(Control anchor, Control parent, Vector2 labelSize)
    {
        var anchorRect = anchor.GetGlobalRect();
        var parentRect = parent.GetGlobalRect();
        var x = anchorRect.Position.X - parentRect.Position.X + (anchorRect.Size.X - labelSize.X) * 0.5f;
        var y = anchorRect.Position.Y - parentRect.Position.Y + anchorRect.Size.Y + 8.0f;
        return new Vector2(x, y);
    }

    private Vector2 CalculatePlayerRevealLabelPosition(Control parent, Vector2 labelSize)
    {
        var parentRect = parent.GetGlobalRect();
        var actionRect = _actionArea?.GetGlobalRect() ?? parentRect;
        var x = actionRect.Position.X - parentRect.Position.X + (actionRect.Size.X - labelSize.X) * 0.5f;
        var y = actionRect.Position.Y - parentRect.Position.Y - labelSize.Y - 10.0f;
        return new Vector2(x, y);
    }

    private Vector2 CalculateBossRevealLabelPosition(Control parent, Vector2 labelSize)
    {
        var parentRect = parent.GetGlobalRect();
        var anchorRect = _bossStatusUi?.GetGlobalRect() ?? parentRect;
        var x = anchorRect.Position.X - parentRect.Position.X + (anchorRect.Size.X - labelSize.X) * 0.5f;
        var y = anchorRect.Position.Y - parentRect.Position.Y + anchorRect.Size.Y + 8.0f;
        return new Vector2(x, y);
    }

    private static string BuildEnemyRevealText(IReadOnlyList<EnemyActionEntry> enemyActions, bool includeEnemyNames = false)
    {
        // Boss 战统一居中显示，蜀汉共生体这类多 Boss 单位需要同时展示三人的出招。
        var names = new List<string>();
        foreach (var entry in enemyActions)
        {
            if (entry.Enemy.IsDead)
            {
                continue;
            }

            var actionName = EscapeBbCode(entry.Action.DisplayName);
            names.Add(includeEnemyNames
                ? $"{EscapeBbCode(entry.Enemy.DisplayName)}：{actionName}"
                : actionName);
        }

        var content = names.Count > 0
            ? string.Join("     ", names)
            : Localization.Get("battle.hover.none");

        return $"[center][font_size=46][b][color=#ffe3b4]{content}[/color][/b][/font_size][/center]";
    }

    private async void ShowFloatingText(Control? anchor, string text, Color color)
    {
        if (anchor == null)
        {
            return;
        }

        // 统一挂载到 BattleEffectLayer，避免飘字直接叠加在敌人头像/面板上。
        var layer = _battlePresentationArea ?? (Control)this;
        if (_battlePresentationArea != null)
        {
            UpdateBattlePresentationArea();
        }

        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(240, 96),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 100
        };
        label.AddThemeFontSizeOverride("font_size", 72);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.72f));
        label.AddThemeConstantOverride("outline_size", 10);
        label.AddThemeConstantOverride("shadow_offset_x", 5);
        label.AddThemeConstantOverride("shadow_offset_y", 6);
        layer.AddChild(label);

        var anchorRect = anchor.GetGlobalRect();
        // 起始位置整体上移，确保飘字出现在头像/面板上方而不是直接覆盖在其上。
        var start = anchorRect.Position - layer.GetGlobalRect().Position + new Vector2(anchorRect.Size.X / 2.0f - 120.0f, -110.0f);
        label.Position = start;
        label.Modulate = Colors.White;
        label.Scale = new Vector2(1.32f, 1.32f);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", start + new Vector2(0, -96), 1.05)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "scale", Vector2.One, 0.18)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 0), 1.05)
            .SetDelay(0.12);

        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }

    private async void ShowBattleMessage(string message)
    {
        _battleMessageVersion += 1;
        var version = _battleMessageVersion;

        SetActionText(message);
        _isShowingBattleMessage = true;

        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);

        if (version != _battleMessageVersion)
        {
            return;
        }

        _isShowingBattleMessage = false;
        SetActionText(GetPhaseActionPrompt());
        RefreshUi();
    }
}
