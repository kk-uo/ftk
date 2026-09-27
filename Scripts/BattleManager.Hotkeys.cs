//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Hotkeys.cs
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

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    /// <summary>
    /// Core System 的公开入口：_UnhandledInput。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        // 反应窗口（ShowReactionWindow/EnterReactionMode 期间，_reactionMode == true——
        // 目前只有龙胆会触发，以后任何新反应窗口只要走同一套 _reactionMode/_actionSlots
        // 机制就会自动获得这里的快捷键支持，不需要为具体技能单独适配）：
        // 反应窗口发生在 BattlePostPhase 且 _inputLocked 已经是 true，正常回合的
        // IsBattleHotkeyEnabled() 会整体拒绝，这也是"反应窗口里临时手牌只能点击、
        // 数字键没反应"这个 Bug 的根因。反应窗口的卡牌牌面点击已经只依赖
        // ActionSlot.Execute() 内部的 IsPlayable 判断（不依赖 _phase/_inputLocked），
        // 这里让数字快捷键走同一个 Execute() 入口，保持和鼠标点击完全一致；
        // 目标切换（I/P）在反应窗口期间维持原有的"不可用"行为，不在这次修复范围内。
        if (_reactionMode)
        {
            if (IsReactionHotkeyEnabled())
            {
                var reactionActionIndex = BattleHotkeySystem.GetActionIndexForKeyEvent(keyEvent);
                if (reactionActionIndex >= 0)
                {
                    TryPlayActionByHotkey(reactionActionIndex);
                    GetViewport().SetInputAsHandled();
                }
            }

            return;
        }

        if (!IsBattleHotkeyEnabled())
        {
            return;
        }

        if (keyEvent.Keycode == Key.I)
        {
            SwitchTargetByHotkey(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.P)
        {
            SwitchTargetByHotkey(1);
            GetViewport().SetInputAsHandled();
            return;
        }

        var actionIndex = BattleHotkeySystem.GetActionIndexForKeyEvent(keyEvent);
        if (actionIndex >= 0)
        {
            TryPlayActionByHotkey(actionIndex);
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsBattleHotkeyEnabled()
    {
        if (!Visible
            || _context == null
            || _context.GameOver
            || _phase != BattlePhase.BattlePrePhase
            || _inputLocked
            || _reactionMode)
        {
            return false;
        }

        if (HasVisibleChoicePanel(GetTree().Root) || IsDeveloperUiOpen() || IsTextInputFocused())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 反应窗口期间的数字快捷键是否可用。刻意不检查 <c>_phase</c>/<c>_inputLocked</c>——
    /// 反应窗口本来就发生在 BattlePostPhase 且 <c>_inputLocked == true</c>，这两个字段
    /// 是为"正常回合出牌"设计的门槛，鼠标点击（<see cref="ActionSlot.Execute"/>）从来
    /// 不检查它们，只检查槽位自身的 <c>IsPlayable</c>；这里保持和鼠标点击同样的判断口径，
    /// 只额外排除弹窗/开发者面板/文本输入焦点这几种"确实不应该响应任何快捷键"的情况。
    /// </summary>
    private bool IsReactionHotkeyEnabled()
    {
        if (!Visible || _context == null || _context.GameOver)
        {
            return false;
        }

        return !HasVisibleChoicePanel(GetTree().Root) && !IsDeveloperUiOpen() && !IsTextInputFocused();
    }

    private void TryPlayActionByHotkey(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _actionSlots.Length)
        {
            return;
        }

        _actionSlots[slotIndex]?.Execute();
    }

    private void SwitchTargetByHotkey(int direction)
    {
        var aliveEnemies = GetAliveEnemiesInDisplayOrder();
        if (aliveEnemies.Count == 0)
        {
            return;
        }

        var currentTarget = GetResolvedSelectedTarget();
        var currentIndex = currentTarget is EnemyInstance enemy
            ? aliveEnemies.IndexOf(enemy)
            : -1;
        var nextIndex = BattleHotkeySystem.GetTargetIndexAfterSwitch(currentIndex, aliveEnemies.Count, direction);
        if (nextIndex < 0 || nextIndex >= aliveEnemies.Count)
        {
            return;
        }

        var nextTarget = aliveEnemies[nextIndex];
        if (_selectedTarget == nextTarget)
        {
            return;
        }

        _selectedTarget = nextTarget;
        RefreshUi();
        RefreshHoverForSelectedTarget();
    }

    private List<EnemyInstance> GetAliveEnemiesInDisplayOrder()
    {
        var enemies = new List<EnemyInstance>();
        foreach (var enemy in _encounter.Enemies)
        {
            if (!enemy.IsDead)
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    private bool IsDeveloperUiOpen()
    {
        return (_battleDebugWindow?.Visible == true)
            || (_skillDebugWindow?.Visible == true)
            || (_battleLogWindow?.Visible == true);
    }

    private bool IsTextInputFocused()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        if (focused == null)
        {
            return false;
        }

        return focused is LineEdit
            or TextEdit
            or SpinBox
            or OptionButton
            || HasAncestorOfType<ChoicePanel>(focused);
    }

    private static bool HasVisibleChoicePanel(Node node)
    {
        if (node is ChoicePanel panel && panel.Visible)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (HasVisibleChoicePanel(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestorOfType<T>(Node node) where T : Node
    {
        var current = node;
        while (current != null)
        {
            if (current is T)
            {
                return true;
            }

            current = current.GetParent();
        }

        return false;
    }
}
