//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.EnemyHover.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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
/// Enemy System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    private PanelContainer? _battleUnitInfoPanel;
    private Label? _battleUnitInfoHeader;
    private Label? _battleUnitInfoSummary;
    private TabContainer? _battleUnitInfoTabs;
    private VBoxContainer? _battleUnitStatusList;
    private VBoxContainer? _battleUnitEquipmentList;
    private VBoxContainer? _battleUnitSkillsList;
    private BattleUnit? _visibleHoverUnit;
    private Control? _visibleHoverAnchor;

    private void CreateEnemyHoverUi()
    {
        _battleUnitInfoPanel = new PanelContainer
        {
            Name = "BattleUnitInformationPanel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 120,
            CustomMinimumSize = new Vector2(460, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        _battleUnitInfoPanel.AddThemeStyleboxOverride("panel", BattleUiSkin.CreateTooltipStyle(0));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _battleUnitInfoPanel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        _battleUnitInfoHeader = new Label();
        _battleUnitInfoHeader.AddThemeFontSizeOverride("font_size", 26);
        root.AddChild(_battleUnitInfoHeader);

        _battleUnitInfoSummary = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _battleUnitInfoSummary.AddThemeFontSizeOverride("font_size", 18);
        _battleUnitInfoSummary.AddThemeColorOverride("font_color", new Color(0.86f, 0.90f, 0.96f));
        root.AddChild(_battleUnitInfoSummary);

        _battleUnitInfoTabs = new TabContainer
        {
            CustomMinimumSize = new Vector2(432, 360)
        };
        root.AddChild(_battleUnitInfoTabs);

        _battleUnitStatusList = CreateInfoTab(_battleUnitInfoTabs, Localization.Get("battle.hover.tab_status"));
        _battleUnitEquipmentList = CreateInfoTab(_battleUnitInfoTabs, Localization.Get("battle.hover.tab_equip"));
        _battleUnitSkillsList = CreateInfoTab(_battleUnitInfoTabs, Localization.Get("battle.hover.tab_skills"));

        _battleUnitInfoPanel.MouseExited += OnHoverPanelMouseExited;
        AddChild(_battleUnitInfoPanel);
    }

    private VBoxContainer CreateInfoTab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        tabs.AddChild(scroll);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);
        return list;
    }

    // 头像和信息面板之间隔着几像素间距（见 PositionEnemyHoverPanel 里的 12px
    // 偏移），鼠标从头像移动到面板、或在面板内部的 Tab/ScrollContainer/
    // 子控件之间移动时都会经过"当前控件不是头像也不是面板本体"的中间状态。
    // MouseEntered/MouseExited 只在鼠标真正跨越某一个 Control 自己的矩形边界
    // 时触发，子控件（Tab 内容、ScrollContainer、Label、Button 等）各自独立
    // 上报，并不会可靠地冒泡成"面板作为一个整体是否仍被悬停"。
    //
    // 因此隐藏与否不再信任任何一次 MouseEntered/MouseExited 事件本身，只用
    // 它们来"排一个延迟检查"；到期时改为直接查询
    // GetViewport().GuiGetHoveredControl() 当前真正悬停的控件，并沿 Parent
    // 链一路上溯，判断它是否落在 BattleUnitInfoPanel 这棵子树内（或落在头像
    // 锚点 _visibleHoverAnchor 上）——只要鼠标此刻处于这两棵树中的任意一棵，
    // 一律视为仍在悬停，不隐藏。这种"事到临头再查一次真实几何/树关系"的判断
    // 比逐个信任子控件转发的 Enter/Exit 信号更稳定，不会因为某个子控件没有
    // 正确转发事件而误判成"已经离开"。
    private const double HoverHideDelaySeconds = 0.15;

    private void OnHoverPanelMouseExited()
    {
        ScheduleHoverHide();
    }

    private void OnCardMouseEntered(CharacterStatusCard card)
    {
        if (card.Unit == null) return;
        ShowBattleUnitInformationPanel(card.Unit, card);
    }

    private void OnCardMouseExited(CharacterStatusCard card)
    {
        ScheduleHoverHide();
    }

    private void ScheduleHoverHide()
    {
        GetTree().CreateTimer(HoverHideDelaySeconds).Timeout += DeferredHidePanel;
    }

    private void DeferredHidePanel()
    {
        if (IsHoveringBattleUnitInfoPanel() || IsHoveringHoverAnchor())
            return;

        HideEnemyHoverPanel();
    }

    /// <summary>
    /// 判断当前真正悬停的控件（GuiGetHoveredControl）是否落在
    /// BattleUnitInfoPanel 这一整棵 UI 子树内——包括标题、Tab、
    /// ScrollContainer、VBoxContainer、状态/装备/技能卡片等任意子节点。
    /// </summary>
    private bool IsHoveringBattleUnitInfoPanel()
    {
        if (_battleUnitInfoPanel == null) return false;
        return IsControlWithinSubtree(GetViewport().GuiGetHoveredControl(), _battleUnitInfoPanel);
    }

    /// <summary>
    /// 判断当前真正悬停的控件是否落在触发悬浮面板的锚点（角色头像/状态卡）
    /// 子树内，与 IsHoveringBattleUnitInfoPanel 二选一满足即可保持面板显示。
    /// </summary>
    private bool IsHoveringHoverAnchor()
    {
        if (_visibleHoverAnchor == null) return false;
        return IsControlWithinSubtree(GetViewport().GuiGetHoveredControl(), _visibleHoverAnchor);
    }

    private static bool IsControlWithinSubtree(Control? hovered, Control root)
    {
        Node? node = hovered;
        while (node != null)
        {
            if (node == root) return true;
            node = node.GetParent();
        }
        return false;
    }

    private void ShowBattleUnitInformationPanel(BattleUnit unit, Control anchor)
    {
        if (_battleUnitInfoPanel == null
            || _battleUnitInfoHeader == null
            || _battleUnitInfoSummary == null
            || _battleUnitStatusList == null
            || _battleUnitEquipmentList == null
            || _battleUnitSkillsList == null
            || _battleUnitInfoTabs == null)
        {
            return;
        }

        _visibleHoverUnit = unit;
        _visibleHoverAnchor = anchor;
        _battleUnitInfoHeader.Text = unit.Name;
        _battleUnitInfoSummary.Text = Localization.GetFmt("battle.hover.summary_fmt",
            BattleUnitUiFormatter.GetFactionText(unit), unit.CurrentHP, unit.MaxHP, BattleRules.FormatMana(unit.Resource));
        _battleUnitInfoTabs.CurrentTab = 0;

        RebuildStatusTab(unit);
        RebuildEquipmentTab(unit);
        RebuildSkillsTab(unit);

        _battleUnitInfoPanel.Visible = true;
        CallDeferred(nameof(ResizeAndPositionEnemyHoverPanel));
    }

    private void RebuildStatusTab(BattleUnit unit)
    {
        if (_battleUnitStatusList == null)
        {
            return;
        }

        ClearChildren(_battleUnitStatusList);
        foreach (var entry in BattleUnitUiFormatter.GetStatuses(unit, _context))
        {
            _battleUnitStatusList.AddChild(CreateStatusCard(entry));
        }

        var danger = BattleUnitUiFormatter.GetDangerReason(unit);
        if (!string.IsNullOrWhiteSpace(danger))
        {
            _battleUnitStatusList.AddChild(CreateTextCard(Localization.Get("battle.hover.danger"), danger, new Color(0.92f, 0.48f, 0.34f)));
        }

        if (_battleUnitStatusList.GetChildCount() == 0)
        {
            _battleUnitStatusList.AddChild(CreateTextCard(Localization.Get("battle.hover.tab_status"), Localization.Get("battle.hover.none")));
        }
    }

    private void RebuildEquipmentTab(BattleUnit unit)
    {
        if (_battleUnitEquipmentList == null)
        {
            return;
        }

        ClearChildren(_battleUnitEquipmentList);
        foreach (var equipment in BattleUnitUiFormatter.GetEquipments(unit))
        {
            _battleUnitEquipmentList.AddChild(CreateTextCard(
                $"{equipment.Name}  [{equipment.Rarity}]",
                $"{equipment.Type}\n{equipment.Description}"));
        }

        if (_battleUnitEquipmentList.GetChildCount() == 0)
        {
            _battleUnitEquipmentList.AddChild(CreateTextCard(Localization.Get("battle.hover.tab_equip"), Localization.Get("battle.hover.none")));
        }
    }

    private void RebuildSkillsTab(BattleUnit unit)
    {
        if (_battleUnitSkillsList == null)
        {
            return;
        }

        ClearChildren(_battleUnitSkillsList);
        foreach (var skill in BattleUnitUiFormatter.GetSkills(unit))
        {
            _battleUnitSkillsList.AddChild(CreateTextCard(
                $"{skill.Name}  [{skill.Rarity}]",
                $"{skill.Type}\n{skill.Description}"));
        }

        if (_battleUnitSkillsList.GetChildCount() == 0)
        {
            _battleUnitSkillsList.AddChild(CreateTextCard(Localization.Get("battle.hover.tab_skills"), Localization.Get("battle.hover.none")));
        }
    }

    private Control CreateStatusCard(BattleUnitStatusEntry entry)
    {
        var tooltipText = BattleUnitUiFormatter.BuildStatusTooltip(entry);
        var button = new Button
        {
            Text = entry.Stackable && entry.StackCount > 1
                ? $"{entry.IconText} {entry.Name} ×{entry.StackCount}"
                : $"{entry.IconText} {entry.Name}",
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.MouseEntered += () => TooltipManager.Show(tooltipText, button);
        button.MouseExited += () => TooltipManager.Hide();
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", CreateInfoCardStyle(GetStatusColor(entry.Kind)));
        button.AddThemeStyleboxOverride("hover", CreateInfoCardStyle(GetStatusColor(entry.Kind).Lightened(0.1f)));
        return button;
    }

    private Control CreateTextCard(string title, string body, Color? border = null)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreateInfoCardStyle(border ?? new Color(0.28f, 0.32f, 0.40f)));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = $"[b]{EscapeBbCode(title)}[/b]\n{EscapeBbCode(body)}"
        };
        label.AddThemeFontSizeOverride("normal_font_size", 18);
        margin.AddChild(label);
        return panel;
    }

    private static StyleBox CreateInfoCardStyle(Color border)
    {
        return BattleUiSkin.CreatePanelStyle(8, Colors.White.Lerp(border, 0.18f));
    }

    private static Color GetStatusColor(BattleUnitStatusKind kind)
    {
        return kind switch
        {
            BattleUnitStatusKind.Buff => new Color(0.20f, 0.68f, 0.42f),
            BattleUnitStatusKind.Debuff => new Color(0.78f, 0.28f, 0.28f),
            _ => new Color(0.62f, 0.52f, 0.88f)
        };
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void ResizeAndPositionEnemyHoverPanel()
    {
        if (_battleUnitInfoPanel == null || _visibleHoverAnchor == null)
        {
            return;
        }

        _battleUnitInfoPanel.ResetSize();
        PositionEnemyHoverPanel(_visibleHoverAnchor);
    }

    private void HideEnemyHoverPanel()
    {
        _visibleHoverUnit = null;
        _visibleHoverAnchor = null;
        if (_battleUnitInfoPanel != null)
        {
            _battleUnitInfoPanel.Visible = false;
        }
    }

    private void RefreshHoverForSelectedTarget()
    {
        HideEnemyHoverPanel();
        RefreshSelectedTargetHighlight();
    }

    private CharacterStatusCard? GetEnemyCardForUnit(BattleUnit unit)
    {
        foreach (var card in _enemyCards)
        {
            if (card.Visible && card.Unit == unit)
            {
                return card;
            }
        }

        return null;
    }

    private void PositionEnemyHoverPanel(Control anchor)
    {
        if (_battleUnitInfoPanel == null)
        {
            return;
        }

        var viewportRect = GetViewportRect();
        var anchorRect = anchor.GetGlobalRect();
        var panelSize = _battleUnitInfoPanel.Size;
        if (panelSize == Vector2.Zero)
        {
            panelSize = _battleUnitInfoPanel.GetCombinedMinimumSize();
        }

        const float margin = 12.0f;
        var position = anchorRect.Position + new Vector2(anchorRect.Size.X + 12, 0);
        if (position.X + panelSize.X > viewportRect.Size.X - margin)
        {
            position.X = anchorRect.Position.X - panelSize.X - margin;
        }

        if (position.X < margin)
        {
            position.X = margin;
        }

        if (position.Y + panelSize.Y > viewportRect.Size.Y - margin)
        {
            var aboveAnchorY = anchorRect.Position.Y - panelSize.Y - margin;
            position.Y = aboveAnchorY >= margin
                ? aboveAnchorY
                : viewportRect.Size.Y - panelSize.Y - margin;
        }

        if (position.Y < margin)
        {
            position.Y = margin;
        }

        _battleUnitInfoPanel.GlobalPosition = position;
    }

    private void RefreshHoverLocalization()
    {
        if (_battleUnitStatusList?.GetParent() is Node statusParent)
            statusParent.Name = Localization.Get("battle.hover.tab_status");
        if (_battleUnitEquipmentList?.GetParent() is Node equipParent)
            equipParent.Name = Localization.Get("battle.hover.tab_equip");
        if (_battleUnitSkillsList?.GetParent() is Node skillsParent)
            skillsParent.Name = Localization.Get("battle.hover.tab_skills");
    }
}
