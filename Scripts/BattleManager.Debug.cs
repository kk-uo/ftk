//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Debug.cs
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
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    private const string ShuHanCollectiveDebugPresetId = "__shu_han_collective__";

    private readonly record struct BattleDebugEnemyOption(string Id, string Name);

    private void ShowSkillDetail(Skill skill)
    {
        var dialog = new AcceptDialog
        {
            Title = Localization.GetName(skill),
            Size = new Vector2I(520, 360),
            Exclusive = false
        };
        AddChild(dialog);

        var text = new Label
        {
            Text = BuildSkillDetailText(skill),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(460, 220)
        };
        text.AddThemeFontSizeOverride("font_size", 22);
        dialog.AddChild(text);
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }

    private static string BuildSkillDetailText(Skill skill)
    {
        var kinds = new System.Text.StringBuilder();
        for (var i = 0; i < skill.Kinds.Count; i++)
        {
            if (i > 0)
            {
                kinds.Append(" / ");
            }
            kinds.Append(SkillText.GetKindName(skill.Kinds[i]));
        }

        var owner = string.IsNullOrWhiteSpace(skill.ExclusiveCharacter)
            ? string.Empty
            : $"角色专属：{skill.ExclusiveCharacter}\n";

        var category = skill.Category == SkillCategory.CharacterExclusive && !string.IsNullOrWhiteSpace(skill.ExclusiveCharacter)
            ? string.Empty
            : $"{SkillText.GetCategoryName(skill.Category)}\n";

        var trigger = skill.Timing.HasValue
            ? $"\nTrigger:\n{skill.Timing.Value}\n"
            : string.Empty;

        var priority = skill.Priority.HasValue
            ? $"\nPriority:\n{skill.Priority.Value}\n"
            : string.Empty;

        return $"{Localization.GetName(skill)}\n\n{category}{owner}稀有度：{SkillText.GetRarityName(skill.Rarity)}\n类型：{kinds}\n{trigger}{priority}\n{Localization.GetDescription(skill)}";
    }

    private void ShowSkillDebugWindow()
    {
        if (_skillDebugWindow != null && !_skillDebugWindow.IsQueuedForDeletion())
        {
            RefreshSkillDebugOwnedList();
            _skillDebugWindow.PopupCentered();
            return;
        }

        var window = new Window
        {
            Title = "技能调试",
            Size = new Vector2I(620, 520),
            Exclusive = false
        };
        _skillDebugWindow = window;
        AddChild(window);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        window.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        root.AddChild(CreateDebugLabel("添加技能", 24));

        var addRow = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(addRow);

        var skillPool = SkillDatabase.All();
        var picker = new OptionButton
        {
            CustomMinimumSize = new Vector2(360, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        picker.AddThemeFontSizeOverride("font_size", 20);
        for (var i = 0; i < skillPool.Count; i++)
        {
            picker.AddItem(Localization.GetName(skillPool[i]), i);
        }
        addRow.AddChild(picker);

        var addButton = new Button
        {
            Text = Localization.Get("debug.button.add"),
            CustomMinimumSize = new Vector2(120, 42)
        };
        addButton.AddThemeFontSizeOverride("font_size", 20);
        addButton.Pressed += () =>
        {
            var index = picker.Selected;
            if (index < 0 || index >= skillPool.Count)
            {
                return;
            }

            _player.AddSkill(skillPool[index]);
            if (_battleDebugMode)
            {
                _debugSkillIds.Add(skillPool[index].Id);
            }
            else
            {
                GameManager.AddAcquiredSkill(skillPool[index].Id);
            }
            RenderPlayerSkills();
            RenderActionCards();
            RefreshSkillDebugOwnedList();
            RefreshBattleDebugWindow();
            RefreshUi();
        };
        addRow.AddChild(addButton);

        root.AddChild(CreateDebugLabel("当前拥有技能", 24));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 280),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _skillDebugOwnedList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _skillDebugOwnedList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_skillDebugOwnedList);

        var closeButton = new Button
        {
            Text = Localization.Get("debug.button.close"),
            CustomMinimumSize = new Vector2(120, 42),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        closeButton.AddThemeFontSizeOverride("font_size", 20);
        closeButton.Pressed += window.Hide;
        root.AddChild(closeButton);

        window.CloseRequested += window.Hide;
        RefreshSkillDebugOwnedList();
        window.PopupCentered();
    }

    private static Label CreateDebugLabel(string text, int fontSize)
    {
        var label = new Label
        {
            Text = text
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private void RefreshSkillDebugOwnedList()
    {
        if (_skillDebugOwnedList == null || _skillDebugOwnedList.IsQueuedForDeletion())
        {
            return;
        }

        foreach (var child in _skillDebugOwnedList.GetChildren())
        {
            _skillDebugOwnedList.RemoveChild(child);
            child.QueueFree();
        }

        if (_player.Skills.Count == 0)
        {
            var emptyLabel = CreateDebugLabel("当前没有技能。", 20);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.80f, 0.84f));
            _skillDebugOwnedList.AddChild(emptyLabel);
            return;
        }

        foreach (var skill in new List<Skill>(_player.Skills))
        {
            _skillDebugOwnedList.AddChild(CreateOwnedSkillRow(skill));
        }
    }

    private Control CreateOwnedSkillRow(Skill skill)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 46)
        };
        row.AddThemeConstantOverride("separation", 12);

        var nameLabel = new Label
        {
            Text = $"{Localization.GetName(skill)} · {SkillText.GetRarityName(skill.Rarity)}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(nameLabel);

        var detailButton = new Button
        {
            Text = Localization.Get("debug.button.details"),
            CustomMinimumSize = new Vector2(90, 38)
        };
        detailButton.AddThemeFontSizeOverride("font_size", 18);
        detailButton.Pressed += () => ShowSkillDetail(skill);
        row.AddChild(detailButton);

        var removeButton = new Button
        {
            Text = Localization.Get("debug.button.delete"),
            CustomMinimumSize = new Vector2(90, 38)
        };
        removeButton.AddThemeFontSizeOverride("font_size", 18);
        removeButton.Pressed += () =>
        {
            _player.RemoveSkill(skill.Id);
            if (_battleDebugMode)
            {
                _debugSkillIds.Remove(skill.Id);
            }
            else
            {
                GameManager.RemoveAcquiredSkill(skill.Id);
            }
            RenderPlayerSkills();
            RenderActionCards();
            RefreshSkillDebugOwnedList();
            RefreshBattleDebugWindow();
            RefreshUi();
        };
        row.AddChild(removeButton);

        return row;
    }

    private void ShowBattleDebugWindow()
    {
        if (_battleDebugWindow != null && !_battleDebugWindow.IsQueuedForDeletion())
        {
            RefreshBattleDebugWindow();
            _battleDebugWindow.PopupCentered();
            return;
        }

        var window = new Window
        {
            Title = "BattleDebug",
            Size = new Vector2I(1180, 980),
            Exclusive = false
        };
        _battleDebugWindow = window;
        AddChild(window);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        window.AddChild(margin);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddChild(scroll);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 0)
        };
        root.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(root);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 760)
        };
        root.AddChild(tabs);

        tabs.AddChild(CreateBattleDebugTab("角色", CreateBattleDebugCharacterTab()));
        tabs.AddChild(CreateBattleDebugTab("技能", CreateBattleDebugSkillTab()));
        tabs.AddChild(CreateBattleDebugTab("装备", CreateBattleDebugEquipmentTab()));
        tabs.AddChild(CreateBattleDebugTab("卡牌", CreateBattleDebugCardTab()));
        tabs.AddChild(CreateBattleDebugTab("战斗", CreateBattleDebugBattleTab()));
        tabs.AddChild(CreateBattleDebugTab("Run", CreateBattleDebugRunTab()));
        tabs.AddChild(CreateBattleDebugTab("Localization", CreateLocalizationTab()));

        var closeButton = new Button
        {
            Text = Localization.Get("debug.button.close"),
            CustomMinimumSize = new Vector2(180, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        closeButton.AddThemeFontSizeOverride("font_size", 24);
        closeButton.Pressed += window.Hide;
        root.AddChild(closeButton);

        window.CloseRequested += window.Hide;
        RefreshBattleDebugWindow();
        window.PopupCentered();
    }

    private Control CreateBattleDebugTab(string title, Control content)
    {
        var panel = new PanelContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        margin.AddChild(scroll);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1020, 0)
        };
        root.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(root);
        root.AddChild(content);
        return panel;
    }

    private Control CreateBattleDebugCharacterTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        root.AddChild(CreateBattleDebugSection("角色属性", CreateBattleDebugPlayerSection()));
        root.AddChild(CreateBattleDebugSection("Buff调试", CreateBattleDebugBuffSection()));
        root.AddChild(CreateBattleDebugSection("手牌伤害预估自检", CreateBattleDebugDamagePreviewSection()));
        return root;
    }

    // 手牌伤害预估自检：把当前手牌槽里每张牌的角标预估值 + 悬停明细，原样打印到输出，
    // 供人工在真实对局状态下核对角标数字与这里打印的构成明细是否一致，
    // 也可以用来快速核对16类关键场景（无双/赤兔/酒/凶残/亢奋/煞气缠身/诅咒/排箫/
    // 知识芯片/南蛮万箭多目标/张辽突袭/孙策激昂/敌方带临时生命等）在真实回合状态下
    // 的预估表现，而不需要为每一种场景单独造一个按钮。
    private Control CreateBattleDebugDamagePreviewSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Text = $"功能开关：{(DamagePreviewSettings.IsEnabled ? "已开启" : "已关闭")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(label);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 14);
        root.AddChild(buttons);

        buttons.AddChild(CreateDebugActionButton("打印当前手牌预估伤害", () =>
        {
            label.Text = $"功能开关：{(DamagePreviewSettings.IsEnabled ? "已开启" : "已关闭")}";
            DumpCurrentHandDamagePreviews();
        }));
        buttons.AddChild(CreateDebugActionButton("开关功能", () =>
        {
            DamagePreviewSettings.SetEnabled(!DamagePreviewSettings.IsEnabled);
            label.Text = $"功能开关：{(DamagePreviewSettings.IsEnabled ? "已开启" : "已关闭")}";
            RenderActionCards();
        }));

        return root;
    }

    private void DumpCurrentHandDamagePreviews()
    {
        if (_context == null)
        {
            GD.Print("手牌伤害预估自检：当前没有进行中的战斗 context。");
            return;
        }

        GD.Print("===== 手牌伤害预估自检 =====");
        foreach (var slot in _actionSlots)
        {
            var cardUi = slot?.CardUi;
            if (cardUi?.CardData == null)
            {
                continue;
            }

            var lockedTarget = GetResolvedSelectedTarget() as Player;
            var preview = DamagePreviewService.PreviewCard(_context, _player, cardUi.CardData.Type, lockedTarget);
            if (!preview.IsDamageCard)
            {
                GD.Print($"[{cardUi.CardData.Name}] 非伤害牌，跳过。");
                continue;
            }

            GD.Print($"[{cardUi.CardData.Name}] 预估={preview.PredictedFinalDamage} 基础={preview.BaseDamage} 多目标={preview.IsMultiTarget}");
            foreach (var target in preview.TargetBreakdown)
            {
                GD.Print($"    -> {target.TargetName}: {target.Amount}{(target.Note != null ? $"（{target.Note}）" : string.Empty)}");
            }
            foreach (var line in preview.Breakdown)
            {
                GD.Print($"    构成: {line}");
            }
            if (preview.FollowUpDamage != null)
            {
                GD.Print($"    链式追加: {preview.FollowUpDamage.SourceLabel} = {preview.FollowUpDamage.Amount}");
            }
            foreach (var warning in preview.Warnings)
            {
                GD.Print($"    提示: {warning}");
            }
        }
        GD.Print("===== 自检结束 =====");
    }

    private Control CreateBattleDebugSkillTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        root.AddChild(CreateBattleDebugSection("技能调试器", CreateBattleDebugSkillSection()));
        root.AddChild(CreateBattleDebugSection("当前技能", CreateBattleDebugOwnedSkillSection()));
        return root;
    }

    private Control CreateBattleDebugEquipmentTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var search = new LineEdit
        {
            PlaceholderText = Localization.Get("debug.search.equipment"),
            CustomMinimumSize = new Vector2(320, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        search.AddThemeFontSizeOverride("font_size", 22);

        var rarityPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, 52)
        };
        rarityPicker.AddThemeFontSizeOverride("font_size", 22);
        rarityPicker.AddItem(Localization.Get("debug.filter.all_rarities"), 0);
        rarityPicker.AddItem(Localization.Get("rarity.common"), 1);
        rarityPicker.AddItem(Localization.Get("rarity.rare"), 2);
        rarityPicker.AddItem(Localization.Get("rarity.epic"), 3);
        rarityPicker.AddItem(Localization.Get("rarity.legendary"), 4);

        var typePicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, 52)
        };
        typePicker.AddThemeFontSizeOverride("font_size", 22);
        typePicker.AddItem(Localization.Get("debug.filter.all_types"), 0);
        typePicker.AddItem(Localization.Get("debug.equipment_type.weapon"), 1);
        typePicker.AddItem(Localization.Get("debug.equipment_type.armor"), 2);
        typePicker.AddItem(Localization.Get("debug.equipment_type.accessory"), 3);
        typePicker.AddItem(Localization.Get("debug.equipment_type.chip"), 4);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 12);
        tools.AddChild(search);
        tools.AddChild(rarityPicker);
        tools.AddChild(typePicker);
        root.AddChild(tools);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);

        void Rebuild()
        {
            ClearDebugChildren(list);
            var keyword = search.Text?.Trim() ?? string.Empty;
            var rarityFilter = rarityPicker.Selected;
            var typeFilter = typePicker.Selected;
            foreach (var equipment in EquipmentDatabase.GetAllEquipments())
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && !Localization.GetName(equipment).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !Localization.GetDescription(equipment).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rarityFilter > 0 && (int)equipment.Rarity != rarityFilter - 1)
                {
                    continue;
                }

                if (!MatchesEquipmentDebugTypeFilter(equipment, typeFilter))
                {
                    continue;
                }

                list.AddChild(CreateBattleDebugEquipmentRow(equipment));
            }
        }

        search.TextChanged += _ => Rebuild();
        rarityPicker.ItemSelected += _ => Rebuild();
        typePicker.ItemSelected += _ => Rebuild();
        Rebuild();

        root.AddChild(list);
        return root;
    }

    private bool MatchesEquipmentDebugTypeFilter(EquipmentDefinition equipment, long typeFilter)
    {
        return typeFilter switch
        {
            1 => equipment.Types.Contains(EquipmentType.Weapon),
            2 => equipment.Types.Contains(EquipmentType.Armor) || equipment.Types.Contains(EquipmentType.Defense),
            3 => equipment.Types.Contains(EquipmentType.Accessory),
            4 => Localization.GetName(equipment).Contains("芯片", StringComparison.OrdinalIgnoreCase)
                || equipment.Id.Contains("chip", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private Control CreateBattleDebugEquipmentRow(EquipmentDefinition equipment)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Text = $"{Localization.GetName(equipment)}  [{GetEquipmentRarityText(equipment.Rarity)}]  {Localization.GetDescription(equipment)}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        var button = CreateDebugActionButton("获得", () =>
        {
            GameManager.AddEquipment(equipment.Id, EquipmentGainSource.Developer);
            RefreshUi();
            RebuildBattleDebugWindow();
        });
        row.AddChild(button);
        return row;
    }

    private static string GetEquipmentRarityText(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => Localization.Get("rarity.common"),
            EquipmentRarity.Rare => Localization.Get("rarity.rare"),
            EquipmentRarity.Epic => Localization.Get("rarity.epic"),
            EquipmentRarity.Legendary => Localization.Get("rarity.legendary"),
            _ => rarity.ToString()
        };
    }

    private Control CreateBattleDebugCardTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var search = new LineEdit
        {
            PlaceholderText = Localization.Get("debug.search.card"),
            CustomMinimumSize = new Vector2(320, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        search.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(search);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);

        void Rebuild()
        {
            ClearDebugChildren(list);
            var keyword = search.Text?.Trim() ?? string.Empty;
            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            {
                var card = new Card(type);
                if (!string.IsNullOrWhiteSpace(keyword)
                    && !Localization.GetName(card).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !type.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.AddChild(CreateBattleDebugCardRow(type, card));
            }
        }

        search.TextChanged += _ => Rebuild();
        Rebuild();
        root.AddChild(list);
        return root;
    }

    private Control CreateBattleDebugCardRow(CardType type, Card card)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Text = $"{Localization.GetName(card)}  [{type}]",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        var button = CreateDebugActionButton("加入出牌栏", () =>
        {
            GameManager.AddPlayerCardType(type);
            RefreshBattleDebugRuntimeUi();
            RebuildBattleDebugWindow();
        });
        row.AddChild(button);

        var removeButton = CreateDebugActionButton("移除", () =>
        {
            GameManager.RemovePlayerCardType(type);
            RefreshBattleDebugRuntimeUi();
            RebuildBattleDebugWindow();
        });
        row.AddChild(removeButton);
        return row;
    }

    private Control CreateBattleDebugBattleTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        root.AddChild(CreateBattleDebugSection("敌人生成器", CreateBattleDebugEnemySection()));

        var battleOps = new HFlowContainer();
        battleOps.AddThemeConstantOverride("h_separation", 12);
        battleOps.AddThemeConstantOverride("v_separation", 12);
        battleOps.AddChild(CreateDebugActionButton("结束战斗", DebugEndBattleVictory));
        battleOps.AddChild(CreateDebugActionButton("秒杀敌人", DebugKillAllEnemies));
        battleOps.AddChild(CreateDebugActionButton("秒杀自己", DebugKillPlayer));
        battleOps.AddChild(CreateDebugActionButton("刷新回合", RestartBattle));
        battleOps.AddChild(CreateDebugActionButton("跳过回合", DebugSkipRound));

        root.AddChild(CreateBattleDebugSection("战斗控制", battleOps));

        var skillPresentationOps = new HFlowContainer();
        skillPresentationOps.AddThemeConstantOverride("h_separation", 12);
        skillPresentationOps.AddThemeConstantOverride("v_separation", 12);
        skillPresentationOps.AddChild(CreateDebugActionButton("技能大字：无双", () => DebugPlaySkillTrigger(SkillIds.Wushuang)));
        skillPresentationOps.AddChild(CreateDebugActionButton("技能大字：连续3个", DebugPlayThreeSkillTriggers));
        skillPresentationOps.AddChild(CreateDebugActionButton("技能大字：重复去重", DebugPlayDuplicateSkillTrigger));
        skillPresentationOps.AddChild(CreateDebugActionButton("技能大字：长名称", DebugPlayLongSkillTrigger));
        skillPresentationOps.AddChild(CreateDebugActionButton("技能大字：完整/简化", DebugToggleSkillTriggerMode));
        root.AddChild(CreateBattleDebugSection("角色技能触发表现", skillPresentationOps));

        _battleDebugAiDecisionCheckBox = new CheckBox
        {
            Text = Localization.Get("debug.show_ai_decisions")
        };
        _battleDebugAiDecisionCheckBox.AddThemeFontSizeOverride("font_size", 24);

        _battleDebugAiViewer = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 320),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _battleDebugAiViewer.AddThemeFontSizeOverride("normal_font_size", 22);

        var aiSection = new VBoxContainer();
        aiSection.AddThemeConstantOverride("separation", 12);
        aiSection.AddChild(_battleDebugAiDecisionCheckBox);
        aiSection.AddChild(_battleDebugAiViewer);
        root.AddChild(CreateBattleDebugSection("AI权重查看器", aiSection));

        _battleDebugHotkeyViewer = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 220),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _battleDebugHotkeyViewer.AddThemeFontSizeOverride("normal_font_size", 22);
        root.AddChild(CreateBattleDebugSection("Hotkey Debug", _battleDebugHotkeyViewer));

        _battleDebugShadowViewer = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _battleDebugShadowViewer.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(CreateBattleDebugSection("影袭状态机 (YingXi)", _battleDebugShadowViewer));

        return root;
    }

    private void DebugPlaySkillTrigger(string skillId)
    {
        var skill = SkillDatabase.GetSkill(skillId);
        if (_skillTriggerToastQueue == null || skill == null)
        {
            return;
        }

        _skillTriggerToastQueue.Enqueue(new SkillTriggerPresentationRequest(
            BattleTeam.Player,
            _player.Id,
            skill.Id,
            skill.DisplayName,
            skill.Timing ?? TriggerTiming.OnBattlePostPhase,
            skill.Priority,
            System.DateTime.UtcNow.Ticks,
            "developer"));
    }

    private void DebugPlayThreeSkillTriggers()
    {
        DebugPlaySkillTrigger(SkillIds.Wushuang);
        DebugPlaySkillTrigger(SkillIds.Biyue);
        DebugPlaySkillTrigger(SkillIds.HunZi);
    }

    private void DebugPlayDuplicateSkillTrigger()
    {
        if (_skillTriggerToastQueue == null || SkillDatabase.GetSkill(SkillIds.Longdan) is not { } skill)
        {
            return;
        }

        var chainId = System.DateTime.UtcNow.Ticks;
        var request = new SkillTriggerPresentationRequest(
            BattleTeam.Player,
            _player.Id,
            skill.Id,
            skill.DisplayName,
            TriggerTiming.OnBattlePostPhase,
            EffectPriority.High,
            chainId,
            "developer_dedup");
        _skillTriggerToastQueue.Enqueue(request);
        _skillTriggerToastQueue.Enqueue(request);
    }

    private void DebugPlayLongSkillTrigger()
    {
        _skillTriggerToastQueue?.Enqueue(new SkillTriggerPresentationRequest(
            BattleTeam.Player,
            _player.Id,
            "developer_long_name",
            "超长角色技能名称表现验证",
            TriggerTiming.OnBattlePostPhase,
            EffectPriority.High,
            System.DateTime.UtcNow.Ticks,
            "developer_long"));
    }

    private void DebugToggleSkillTriggerMode()
    {
        if (_skillTriggerToastQueue == null)
        {
            return;
        }

        _skillTriggerToastQueue.Mode = _skillTriggerToastQueue.Mode == SkillTriggerPresentationMode.Full
            ? SkillTriggerPresentationMode.Simplified
            : SkillTriggerPresentationMode.Full;
    }

    private void DebugEndBattleVictory()
    {
        if (_context == null)
        {
            return;
        }

        _context.GameOver = true;
        _context.Outcome = BattleOutcome.Victory;
        _context.GameOverText = Localization.Get("battle.gameover.victory");
        CheckBattleOver();
    }

    private void DebugKillAllEnemies()
    {
        foreach (var enemy in _encounter.Enemies)
        {
            enemy.TakeDamage(999999);
            enemy.MarkDead();
        }

        if (_context != null)
        {
            _context.GameOver = true;
            _context.Outcome = BattleOutcome.Victory;
            _context.GameOverText = Localization.Get("battle.gameover.victory");
        }

        RefreshUi();
        CheckBattleOver();
    }

    private void DebugKillPlayer()
    {
        _player.TakeDamage(999999);
        _player.MarkDead();
        if (_context != null)
        {
            _context.GameOver = true;
            _context.Outcome = BattleOutcome.Defeat;
            _context.GameOverText = Localization.Get("battle.gameover.defeat");
        }

        RefreshUi();
        CheckBattleOver();
    }

    private async void DebugSkipRound()
    {
        if (_context == null || _inputLocked || _phase != BattlePhase.BattlePrePhase)
        {
            return;
        }

        await ResolvePlayerAction(BattleAction.FromCard(Card.Fee(), 1), null, _battleRunId);
    }

    /// <summary>
    /// 开发者面板专属：清空当前真实战斗中周泰【不屈】的本场状态（RuntimeStates 键），
    /// 清空本场不屈判定与首次失败保底状态，用于反复测试。
    /// </summary>
    public void DebugResetZhouTaiBuQuState()
    {
        _player.RuntimeStates.Remove("zhoutai_buqu_first_failure_rescue_used");
        _player.RuntimeStates.Remove("zhoutai_buqu_roll_count");
        _player.RuntimeStates.Remove("zhoutai_buqu_last_dying_event_id");
        _player.RuntimeStates.Remove("zhoutai_fenji_last_dying_event_id");
    }

    /// <summary>
    /// 开发者面板专属：把玩家 HP 强制设为 0 并手动重新抛出一次 TriggerTiming.OnDying，
    /// 完全复用 BattleResolver.ResolveXueZhaiNearDeathDamage 的既有做法（临时替换
    /// context.DamageEvent 后照常触发、再还原），用于在真实战斗里测试【不屈】/【奋激】。
    /// </summary>
    public void DebugForceZhouTaiDying()
    {
        if (_context == null)
        {
            return;
        }

        _player.DebugSetHealth(0);
        var savedDamage = _context.DamageEvent;
        _context.DamageEvent = new DamageEvent(_player, _player, CardType.XueZhaiAttack, 0);
        _context.RaiseOnDying();
        _context.DamageEvent = savedDamage;
        RefreshUi();
    }

    private Control CreateBattleDebugEnemySection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var toolsRow = new HBoxContainer();
        toolsRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(toolsRow);

        _battleDebugEnemySearchInput = new LineEdit
        {
            PlaceholderText = Localization.Get("debug.search.enemy"),
            CustomMinimumSize = new Vector2(300, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _battleDebugEnemySearchInput.AddThemeFontSizeOverride("font_size", 22);
        _battleDebugEnemySearchInput.TextChanged += text =>
        {
            _battleDebugEnemySearchText = text ?? string.Empty;
            RefreshBattleDebugEnemyPickers();
        };
        toolsRow.AddChild(_battleDebugEnemySearchInput);

        _battleDebugEnemyFilterPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, 52)
        };
        _battleDebugEnemyFilterPicker.AddThemeFontSizeOverride("font_size", 22);
        _battleDebugEnemyFilterPicker.AddItem(Localization.Get("debug.filter.all"), 0);
        _battleDebugEnemyFilterPicker.AddItem(Localization.Get("debug.enemy_type.normal"), 1);
        _battleDebugEnemyFilterPicker.AddItem(Localization.Get("debug.enemy_type.elite"), 2);
        _battleDebugEnemyFilterPicker.AddItem("Boss", 3);
        _battleDebugEnemyFilterPicker.ItemSelected += selected =>
        {
            _battleDebugEnemyFilterType = selected switch
            {
                1 => EnemyType.Normal,
                2 => EnemyType.Elite,
                3 => EnemyType.Boss,
                _ => null
            };
            RefreshBattleDebugEnemyPickers();
        };
        toolsRow.AddChild(_battleDebugEnemyFilterPicker);

        root.AddChild(CreateBattleDebugPresetSection());

        for (var i = 0; i < 3; i++)
        {
            root.AddChild(CreateBattleDebugEnemyRow(i));
        }

        var refreshButton = new Button
        {
            Text = Localization.Get("debug.refresh_enemies"),
            CustomMinimumSize = new Vector2(220, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        refreshButton.AddThemeFontSizeOverride("font_size", 24);
        refreshButton.Pressed += () =>
        {
            SyncBattleDebugConfigFromUi();
            GD.Print("[Debug] RefreshEnemyButton Pressed");
            StartBattle();
            RefreshBattleDebugWindow();
        };
        root.AddChild(refreshButton);
        return root;
    }

    private Control CreateBattleDebugEnemyRow(int index)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);

        var label = new Label
        {
            Text = Localization.GetFmt("debug.enemy_index_fmt", index + 1),
            CustomMinimumSize = new Vector2(100, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        row.AddChild(label);

        var enemyPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(320, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        enemyPicker.AddThemeFontSizeOverride("font_size", 22);
        enemyPicker.ItemSelected += selected =>
        {
            var enemyOptions = GetFilteredBattleDebugEnemyOptions();
            _debugEnemyIds[index] = selected == 0 ? null : enemyOptions[(int)selected].Id;
            var selectedName = selected == 0 ? "None" : enemyOptions[(int)selected].Name;
            GD.Print($"[Debug] Selected Enemy: {selectedName}");
        };
        row.AddChild(enemyPicker);

        var actionPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(280, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        actionPicker.AddThemeFontSizeOverride("font_size", 22);
        actionPicker.AddItem(Localization.Get("debug.automatic_ai"), 0);
        var overrideTypes = GetBattleDebugOverrideCardTypes();
        for (var i = 0; i < overrideTypes.Count; i++)
        {
            actionPicker.AddItem(BattleRules.GetCardName(overrideTypes[i]), i + 1);
        }
        actionPicker.ItemSelected += selected =>
        {
            _debugEnemyNextActionOverrides[index] = selected == 0 ? null : overrideTypes[(int)selected - 1];
            var actionName = selected == 0 ? Localization.Get("debug.automatic_ai") : BattleRules.GetCardName(overrideTypes[(int)selected - 1]);
            GD.Print("[Debug] Override Action");
            GD.Print($"Enemy={GetDebugEnemyDisplayName(index)}");
            GD.Print($"Action={actionName}");
            GD.Print("Source=DebugPanel");
        };
        row.AddChild(actionPicker);

        _battleDebugEnemyPickers[index] = enemyPicker;
        _battleDebugEnemyActionPickers[index] = actionPicker;
        PopulateBattleDebugEnemyPicker(enemyPicker, _debugEnemyIds[index]);
        return row;
    }

    private Control CreateBattleDebugPlayerSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 14);
        root.AddChild(statsRow);

        _battleDebugHpSpin = CreateDebugSpinBox(0, 999, _debugPlayerHp);
        _battleDebugMaxHpSpin = CreateDebugSpinBox(1, 999, _debugPlayerMaxHp);
        _battleDebugManaSpin = CreateDebugSpinBox(0, 10, _debugPlayerMana, 0.5);

        statsRow.AddChild(CreateDebugSpinColumn("玩家HP", _battleDebugHpSpin));
        statsRow.AddChild(CreateDebugSpinColumn("玩家MaxHP", _battleDebugMaxHpSpin));
        statsRow.AddChild(CreateDebugSpinColumn("玩家费用", _battleDebugManaSpin));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 14);
        buttons.Alignment = BoxContainer.AlignmentMode.Begin;
        root.AddChild(buttons);

        buttons.AddChild(CreateDebugActionButton("应用", ApplyBattleDebugPlayerValues));
        buttons.AddChild(CreateDebugActionButton("满费用", () =>
        {
            _debugPlayerMana = 10;
            if (_battleDebugManaSpin != null)
            {
                _battleDebugManaSpin.Value = _debugPlayerMana;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("清空费用", () =>
        {
            _debugPlayerMana = 0;
            if (_battleDebugManaSpin != null)
            {
                _battleDebugManaSpin.Value = _debugPlayerMana;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("满血", () =>
        {
            _debugPlayerMaxHp = (int)(_battleDebugMaxHpSpin?.Value ?? _debugPlayerMaxHp);
            _debugPlayerHp = _debugPlayerMaxHp;
            if (_battleDebugHpSpin != null)
            {
                _battleDebugHpSpin.Value = _debugPlayerHp;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("999血", () =>
        {
            _debugPlayerMaxHp = 999;
            _debugPlayerHp = 999;
            if (_battleDebugMaxHpSpin != null)
            {
                _battleDebugMaxHpSpin.Value = _debugPlayerMaxHp;
            }
            if (_battleDebugHpSpin != null)
            {
                _battleDebugHpSpin.Value = _debugPlayerHp;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("999费", () =>
        {
            _debugPlayerMana = 999;
            if (_battleDebugManaSpin != null)
            {
                _battleDebugManaSpin.Value = _debugPlayerMana;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("999/999一键应用", () =>
        {
            _debugPlayerMaxHp = 999;
            _debugPlayerHp = 999;
            _debugPlayerMana = 999;
            if (_battleDebugMaxHpSpin != null)
            {
                _battleDebugMaxHpSpin.Value = _debugPlayerMaxHp;
            }
            if (_battleDebugHpSpin != null)
            {
                _battleDebugHpSpin.Value = _debugPlayerHp;
            }
            if (_battleDebugManaSpin != null)
            {
                _battleDebugManaSpin.Value = _debugPlayerMana;
            }
            ApplyBattleDebugPlayerValues();
        }));
        buttons.AddChild(CreateDebugActionButton("清空技能", ClearBattleDebugSkills));

        _battleDebugInvincibleCheckBox = new CheckBox
        {
            Text = Localization.Get("debug.invincible")
        };
        _battleDebugInvincibleCheckBox.AddThemeFontSizeOverride("font_size", 22);
        _battleDebugInvincibleCheckBox.Toggled += toggled =>
        {
            _debugPlayerInvincible = toggled;
            _player.DebugSetInvincible(toggled);
            RefreshBattleDebugRuntimeUi();
        };
        root.AddChild(_battleDebugInvincibleCheckBox);

        return root;
    }

    private Control CreateBattleDebugBuffSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);

        var search = new LineEdit
        {
            PlaceholderText = Localization.Get("debug.search.buff"),
            CustomMinimumSize = new Vector2(320, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        search.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(search);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);

        void Rebuild()
        {
            ClearDebugChildren(list);
            var keyword = search.Text?.Trim() ?? string.Empty;
            foreach (var entry in GetBattleDebugBuffEntries())
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && !entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !entry.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.AddChild(CreateBattleDebugBuffRow(entry));
            }
        }

        search.TextChanged += _ => Rebuild();
        Rebuild();
        root.AddChild(list);
        return root;
    }

    private sealed record BattleDebugBuffEntry(string Key, string DisplayName, Action Add, Action Remove);

    private List<BattleDebugBuffEntry> GetBattleDebugBuffEntries()
    {
        var entries = new List<BattleDebugBuffEntry>();
        foreach (var buff in RunBuffDatabase.GetAll())
        {
            entries.Add(new BattleDebugBuffEntry(
                buff.BuffId,
                Localization.GetName(buff),
                () => RunBuffManager.Add(buff.BuffId),
                () => RunBuffManager.RemoveAllStacks(buff.BuffId)));
        }

        entries.Add(new BattleDebugBuffEntry("battle_frozen", "冰冻", () => _player.SetFrozen(1), () => _player.ClearFreeze()));
        entries.Add(new BattleDebugBuffEntry("battle_stun", "眩晕", () => _player.SetStun(1), () => _player.ClearStun()));
        entries.Add(new BattleDebugBuffEntry("battle_ferocity", "凶残", () => _player.AddFerocityLayers(1), () =>
        {
            while (_player.FerocityLayers > 0)
            {
                _player.DecrementFerocity();
            }
        }));
        entries.Add(new BattleDebugBuffEntry("battle_wine", "酒层", () => _player.QueueWinePower(1), () => _player.ClearWinePower()));
        return entries;
    }

    private Control CreateBattleDebugBuffRow(BattleDebugBuffEntry entry)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Text = entry.DisplayName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        row.AddChild(CreateDebugActionButton("添加", () =>
        {
            entry.Add();
            RefreshBattleDebugRuntimeUi();
            RebuildBattleDebugWindow();
        }));
        row.AddChild(CreateDebugActionButton("移除", () =>
        {
            entry.Remove();
            RefreshBattleDebugRuntimeUi();
            RebuildBattleDebugWindow();
        }));
        return row;
    }

    private Control CreateBattleDebugSkillSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);

        var search = new LineEdit
        {
            PlaceholderText = Localization.Get("debug.search.skill"),
            CustomMinimumSize = new Vector2(320, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        search.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(search);

        var flow = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        root.AddChild(flow);

        void Rebuild()
        {
            ClearDebugChildren(flow);
            var keyword = search.Text?.Trim() ?? string.Empty;
            foreach (var skill in SkillDatabase.All())
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && !Localization.GetName(skill).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !Localization.GetDescription(skill).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var button = new Button
                {
                    Text = Localization.GetName(skill),
                    CustomMinimumSize = new Vector2(180, 52)
                };
                button.AddThemeFontSizeOverride("font_size", 22);
                button.Pressed += () => AddBattleDebugSkill(skill);
                flow.AddChild(button);
            }
        }

        search.TextChanged += _ => Rebuild();
        Rebuild();
        return root;
    }

    private Control CreateBattleDebugOwnedSkillSection()
    {
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);

        foreach (var skill in new List<Skill>(_player.Skills))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            var label = new Label
            {
                Text = $"{Localization.GetName(skill)} · {SkillText.GetRarityName(skill.Rarity)}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            row.AddChild(label);

            row.AddChild(CreateDebugActionButton("详情", () => ShowSkillDetail(skill)));
            row.AddChild(CreateDebugActionButton("升级", () => UpgradeBattleDebugSkill(skill.Id)));
            row.AddChild(CreateDebugActionButton("删除", () => RemoveBattleDebugSkill(skill.Id)));
            list.AddChild(row);
        }

        if (_player.Skills.Count == 0)
        {
            list.AddChild(CreateDebugLabel("当前没有技能。", 20));
        }

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(CreateDebugActionButton("重置技能", ClearBattleDebugSkills));
        list.AddChild(footer);
        return list;
    }

    private void AddBattleDebugSkill(Skill skill)
    {
        _player.AddSkill(skill);
        if (_battleDebugMode)
        {
            _debugSkillIds.Add(skill.Id);
        }
        else
        {
            GameManager.AddAcquiredSkill(skill.Id);
        }

        RefreshBattleDebugRuntimeUi();
        RebuildBattleDebugWindow();
    }

    private void RemoveBattleDebugSkill(string skillId)
    {
        _player.RemoveSkill(skillId);
        if (_battleDebugMode)
        {
            _debugSkillIds.Remove(skillId);
        }
        else
        {
            GameManager.RemoveAcquiredSkill(skillId);
        }

        RefreshBattleDebugRuntimeUi();
        RebuildBattleDebugWindow();
    }

    private void UpgradeBattleDebugSkill(string skillId)
    {
        var upgradedSkillId = skillId switch
        {
            SkillIds.Keji => SkillIds.KejiUnlimited,
            SkillIds.Manzu => SkillIds.ManzuWang,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(upgradedSkillId))
        {
            return;
        }

        RemoveBattleDebugSkill(skillId);
        var upgraded = SkillDatabase.GetSkill(upgradedSkillId);
        if (upgraded != null)
        {
            AddBattleDebugSkill(upgraded);
        }
    }

    private void ApplyBattleDebugPlayerValues()
    {
        _debugPlayerHp = (int)(_battleDebugHpSpin?.Value ?? _debugPlayerHp);
        _debugPlayerMaxHp = (int)(_battleDebugMaxHpSpin?.Value ?? _debugPlayerMaxHp);
        _debugPlayerMana = _battleDebugManaSpin?.Value ?? _debugPlayerMana;

        if (_debugPlayerMaxHp < 1)
        {
            _debugPlayerMaxHp = 1;
        }
        _player.DebugSetMaxHealth(_debugPlayerMaxHp);
        _player.DebugSetHealth(_debugPlayerHp);
        _player.DebugSetMana(_debugPlayerMana);
        _player.DebugSetInvincible(_debugPlayerInvincible);
        RefreshBattleDebugRuntimeUi();
    }

    private void ApplyBattleDebugRunValues()
    {
        var forage = (int)(_battleDebugForageSpin?.Value ?? GameManager.Forage);
        var gold = (int)(_battleDebugGoldSpin?.Value ?? GameManager.Gold);
        GameManager.AddForage(forage - GameManager.Forage);
        GameManager.AddGold(gold - GameManager.Gold);
        RefreshBattleDebugRuntimeUi();
    }

    private void ClearBattleDebugSkills()
    {
        _debugSkillIds.Clear();
        _player.DebugClearSkillsAndTemporaryStates();
        ClearStack();
        _reactionMode = false;
        _activeReaction = null;
        _reactionSelection = null;
        _reactionOptionsByCard.Clear();
        RefreshBattleDebugRuntimeUi();
        RebuildBattleDebugWindow();
    }

    private void RefreshBattleDebugRuntimeUi()
    {
        RenderPlayerSkills();
        RenderActionCards();
        RefreshSkillDebugOwnedList();
        RefreshUi();
        RefreshBattleDebugWindow();
    }

    private static SpinBox CreateDebugSpinBox(double min, double max, double value, double step = 1.0)
    {
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            Step = step,
            CustomMinimumSize = new Vector2(180, 52)
        };
        spin.AddThemeFontSizeOverride("font_size", 22);
        return spin;
    }

    private static Control CreateDebugSpinColumn(string title, SpinBox spinBox)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        var label = new Label
        {
            Text = title
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(label);
        root.AddChild(spinBox);
        return root;
    }

    private static Button CreateDebugActionButton(string text, System.Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(160, 52)
        };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.Pressed += () => action();
        return button;
    }

    private void RefreshBattleDebugWindow()
    {
        if (_battleDebugWindow == null || _battleDebugWindow.IsQueuedForDeletion())
        {
            return;
        }

        _debugButton?.Show();
        if (_battleDebugHpSpin != null)
        {
            _battleDebugHpSpin.Value = _player.Health;
        }
        if (_battleDebugMaxHpSpin != null)
        {
            _battleDebugMaxHpSpin.Value = _player.MaxHealth;
        }
        if (_battleDebugManaSpin != null)
        {
            _battleDebugManaSpin.Value = _player.CurrentMana;
        }
        if (_battleDebugForageSpin != null)
        {
            _battleDebugForageSpin.Value = GameManager.Forage;
        }
        if (_battleDebugGoldSpin != null)
        {
            _battleDebugGoldSpin.Value = GameManager.Gold;
        }
        if (_battleDebugInvincibleCheckBox != null)
        {
            _battleDebugInvincibleCheckBox.ButtonPressed = _player.DebugInvincible;
        }
        if (_battleDebugAiViewer != null)
        {
            _battleDebugAiViewer.Text = BuildBattleDebugAiViewerText();
        }
        RefreshBattleDebugHotkeyViewer();
        if (_battleDebugShadowViewer != null)
        {
            _battleDebugShadowViewer.Text = BuildShadowStateDebugText();
        }
        if (_battleDebugEnemySearchInput != null && _battleDebugEnemySearchInput.Text != _battleDebugEnemySearchText)
        {
            _battleDebugEnemySearchInput.Text = _battleDebugEnemySearchText;
        }
        if (_battleDebugEnemyFilterPicker != null)
        {
            _battleDebugEnemyFilterPicker.Select(_battleDebugEnemyFilterType switch
            {
                EnemyType.Normal => 1,
                EnemyType.Elite => 2,
                EnemyType.Boss => 3,
                _ => 0
            });
        }

        for (var i = 0; i < _debugEnemyIds.Length; i++)
        {
            if (_battleDebugEnemyPickers[i] != null)
            {
                PopulateBattleDebugEnemyPicker(_battleDebugEnemyPickers[i]!, _debugEnemyIds[i]);
            }

            if (_battleDebugEnemyActionPickers[i] != null)
            {
                var selected = 0;
                var overrideTypes = GetBattleDebugOverrideCardTypes();
                if (_debugEnemyNextActionOverrides[i].HasValue)
                {
                    for (var actionIndex = 0; actionIndex < overrideTypes.Count; actionIndex++)
                    {
                        if (overrideTypes[actionIndex] == _debugEnemyNextActionOverrides[i]!.Value)
                        {
                            selected = actionIndex + 1;
                            break;
                        }
                    }
                }

                _battleDebugEnemyActionPickers[i]!.Select(selected);
            }
        }
    }

    private void SyncBattleDebugConfigFromUi()
    {
        var allEnemies = GetFilteredBattleDebugEnemyOptions();
        var overrideTypes = GetBattleDebugOverrideCardTypes();
        for (var i = 0; i < _debugEnemyIds.Length; i++)
        {
            var enemyPicker = _battleDebugEnemyPickers[i];
            if (enemyPicker != null)
            {
                var selectedEnemyIndex = enemyPicker.GetSelectedId();
                _debugEnemyIds[i] = selectedEnemyIndex == 0 ? null : allEnemies[selectedEnemyIndex].Id;
            }

            var actionPicker = _battleDebugEnemyActionPickers[i];
            if (actionPicker != null)
            {
                var selectedActionIndex = actionPicker.GetSelectedId();
                _debugEnemyNextActionOverrides[i] = selectedActionIndex == 0 ? null : overrideTypes[selectedActionIndex - 1];
            }
        }
    }

    private string GetDebugEnemyDisplayName(int slotIndex)
    {
        var enemyId = slotIndex >= 0 && slotIndex < _debugEnemyIds.Length ? _debugEnemyIds[slotIndex] : null;
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            return "None";
        }

        if (enemyId == ShuHanCollectiveDebugPresetId)
        {
            return "蜀汉共生体";
        }

        var def = EnemyDatabase.GetEnemy(enemyId);
        return def is null ? enemyId : Localization.GetName(def);
    }

    private static List<BattleDebugEnemyOption> GetBattleDebugEnemyOptions()
    {
        var options = new List<BattleDebugEnemyOption>
        {
            new(null!, "None"),
            new(ShuHanCollectiveDebugPresetId, "蜀汉共生体")
        };

        foreach (var enemy in EnemyDatabase.GetAllEnemies())
        {
            options.Add(new BattleDebugEnemyOption(enemy.Id, Localization.GetName(enemy)));
        }

        return options;
    }

    private List<BattleDebugEnemyOption> GetFilteredBattleDebugEnemyOptions()
    {
        var options = GetBattleDebugEnemyOptions();
        if (_battleDebugEnemyFilterType == null && string.IsNullOrWhiteSpace(_battleDebugEnemySearchText))
        {
            return options;
        }

        var filtered = new List<BattleDebugEnemyOption> { options[0] };
        for (var i = 1; i < options.Count; i++)
        {
            var option = options[i];
            if (option.Id == ShuHanCollectiveDebugPresetId)
            {
                if (MatchesBattleDebugEnemySearch(option.Name))
                {
                    filtered.Add(option);
                }
                continue;
            }

            var definition = EnemyDatabase.GetEnemy(option.Id);
            if (definition == null)
            {
                continue;
            }

            if (_battleDebugEnemyFilterType.HasValue && definition.Type != _battleDebugEnemyFilterType.Value)
            {
                continue;
            }

            if (!MatchesBattleDebugEnemySearch(option.Name))
            {
                continue;
            }

            filtered.Add(option);
        }

        return filtered;
    }

    private bool MatchesBattleDebugEnemySearch(string enemyName)
    {
        return string.IsNullOrWhiteSpace(_battleDebugEnemySearchText)
            || enemyName.Contains(_battleDebugEnemySearchText, System.StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateBattleDebugEnemyPicker(OptionButton picker, string? selectedEnemyId)
    {
        picker.Clear();
        var options = GetFilteredBattleDebugEnemyOptions();
        var selectedIndex = 0;
        for (var i = 0; i < options.Count; i++)
        {
            picker.AddItem(options[i].Name, i);
            if (!string.IsNullOrWhiteSpace(selectedEnemyId) && options[i].Id == selectedEnemyId)
            {
                selectedIndex = i;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedEnemyId) && selectedIndex == 0)
        {
            var fallbackName = GetDebugEnemyDisplayNameFromId(selectedEnemyId);
            picker.AddItem(fallbackName, options.Count);
            selectedIndex = options.Count;
        }

        picker.Select(selectedIndex);
    }

    private void RefreshBattleDebugEnemyPickers()
    {
        for (var i = 0; i < _debugEnemyIds.Length; i++)
        {
            if (_battleDebugEnemyPickers[i] != null)
            {
                PopulateBattleDebugEnemyPicker(_battleDebugEnemyPickers[i]!, _debugEnemyIds[i]);
            }
        }
    }

    private Control CreateBattleDebugPresetSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);

        var label = CreateDebugLabel("常用预设", 24);
        root.AddChild(label);

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 12);
        buttons.AddThemeConstantOverride("v_separation", 12);
        root.AddChild(buttons);

        buttons.AddChild(CreateDebugActionButton("城关守卫", () => ApplyBattleDebugEnemyPreset("gate_guard", null, null)));
        buttons.AddChild(CreateDebugActionButton("羽卫+医者+羽卫", () => ApplyBattleDebugEnemyPreset("feather_guard", "boss_healer", "feather_guard")));
        buttons.AddChild(CreateDebugActionButton("暴君+羽卫+羽卫", () => ApplyBattleDebugEnemyPreset("boss_tyrant", "feather_guard", "feather_guard")));
        buttons.AddChild(CreateDebugActionButton("蜀汉共生体", () => ApplyBattleDebugEnemyPreset(ShuHanCollectiveDebugPresetId, null, null)));

        return root;
    }

    private void RebuildBattleDebugWindow()
    {
        if (_battleDebugWindow == null || _battleDebugWindow.IsQueuedForDeletion())
        {
            return;
        }

        _battleDebugWindow.QueueFree();
        _battleDebugWindow = null;
        ShowBattleDebugWindow();
    }

    private static void ClearDebugChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void ApplyBattleDebugEnemyPreset(string? enemy1, string? enemy2, string? enemy3)
    {
        _debugEnemyIds[0] = enemy1;
        _debugEnemyIds[1] = enemy2;
        _debugEnemyIds[2] = enemy3;
        RefreshBattleDebugEnemyPickers();
    }

    private void RefreshBattleDebugHotkeyViewer()
    {
        if (_battleDebugHotkeyViewer == null || _battleDebugHotkeyViewer.IsQueuedForDeletion())
        {
            return;
        }

        var lines = BuildActionSlotDebugLines();
        if (lines.Count == 0)
        {
            _battleDebugHotkeyViewer.Text = "No action slots.";
            return;
        }

        _battleDebugHotkeyViewer.Text = string.Join("\n", lines);
    }

    private List<string> BuildActionSlotDebugLines()
    {
        var lines = new List<string>();
        for (var i = 0; i < _actionSlots.Length; i++)
        {
            var slot = _actionSlots[i];
            var card = slot?.Card;
            var cardText = card == null ? "Empty" : $"{card.Name} ({card.Type})";
            lines.Add($"Slot{i} / Hotkey {BattleHotkeySystem.GetKeyNameForActionIndex(i)} -> {cardText}");
        }

        return lines;
    }

    private string GetDebugEnemyDisplayNameFromId(string enemyId)
    {
        if (enemyId == ShuHanCollectiveDebugPresetId)
        {
            return "蜀汉共生体";
        }

        var def = EnemyDatabase.GetEnemy(enemyId);
        return def is null ? enemyId : Localization.GetName(def);
    }

    private Control CreateBattleDebugRunTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        root.AddChild(CreateBattleDebugSection("Run资源", CreateBattleDebugRunResourceSection()));
        root.AddChild(CreateBattleDebugSection("章节跳转", CreateBattleDebugChapterNavSection()));
        root.AddChild(CreateBattleDebugSection("当前RunBuff", CreateBattleDebugRunBuffOverview()));
        root.AddChild(CreateBattleDebugSection("当前装备", CreateBattleDebugEquipmentOverview()));
        root.AddChild(CreateBattleDebugSection("当前技能", CreateBattleDebugSkillOverview()));
        root.AddChild(CreateBattleDebugSection("当前芯片", CreateBattleDebugChipOverview()));
        return root;
    }

    private Control CreateBattleDebugRunResourceSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);

        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 14);
        root.AddChild(statsRow);

        _battleDebugForageSpin = CreateDebugSpinBox(0, 99, GameManager.Forage);
        _battleDebugGoldSpin = CreateDebugSpinBox(0, 9999, GameManager.Gold);
        statsRow.AddChild(CreateDebugSpinColumn("粮草", _battleDebugForageSpin));
        statsRow.AddChild(CreateDebugSpinColumn("金币", _battleDebugGoldSpin));

        var applyRow = new HBoxContainer();
        applyRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(applyRow);

        applyRow.AddChild(CreateDebugActionButton("应用", ApplyBattleDebugRunValues));
        applyRow.AddChild(CreateDebugActionButton("满粮草", () =>
        {
            if (_battleDebugForageSpin != null) _battleDebugForageSpin.Value = 99;
            ApplyBattleDebugRunValues();
        }));
        applyRow.AddChild(CreateDebugActionButton("满金币", () =>
        {
            if (_battleDebugGoldSpin != null) _battleDebugGoldSpin.Value = 9999;
            ApplyBattleDebugRunValues();
        }));

        return root;
    }

    private Control CreateBattleDebugChapterNavSection()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);

        var infoLabel = CreateDebugLabel($"当前章节：第 {GameManager.CurrentChapter} 章  |  节点：{GameManager.CurrentNodeId}", 20);
        infoLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.94f));
        root.AddChild(infoLabel);

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 16);
        buttons.AddThemeConstantOverride("v_separation", 12);
        root.AddChild(buttons);

        void GoToChapter(int chapter)
        {
            GameManager.DebugGoToChapter(chapter);
            _battleDebugWindow?.Hide();
            CallDeferred(nameof(EmitGoToMapRequestedDeferred));
        }

        var btn1 = new Button { Text = Localization.Get("debug.go_to_chapter_1"), CustomMinimumSize = new Vector2(200, 56) };
        btn1.AddThemeFontSizeOverride("font_size", 22);
        btn1.Pressed += () => GoToChapter(1);
        buttons.AddChild(btn1);

        var btn2 = new Button { Text = Localization.Get("debug.go_to_chapter_2"), CustomMinimumSize = new Vector2(200, 56) };
        btn2.AddThemeFontSizeOverride("font_size", 22);
        btn2.Pressed += () => GoToChapter(2);
        buttons.AddChild(btn2);

        var btn3 = new Button { Text = Localization.Get("debug.go_to_chapter_3"), CustomMinimumSize = new Vector2(200, 56) };
        btn3.AddThemeFontSizeOverride("font_size", 22);
        btn3.Pressed += () => GoToChapter(3);
        buttons.AddChild(btn3);

        var btn4 = new Button { Text = Localization.Get("debug.go_to_chapter_4"), CustomMinimumSize = new Vector2(200, 56) };
        btn4.AddThemeFontSizeOverride("font_size", 22);
        btn4.Pressed += () => GoToChapter(4);
        buttons.AddChild(btn4);

        return root;
    }

    private Control CreateBattleDebugRunBuffOverview()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        var grouped = new System.Collections.Generic.Dictionary<string, (string Name, int Count)>();
        foreach (var buff in RunBuffManager.ActiveBuffs)
        {
            if (grouped.TryGetValue(buff.BuffId, out var existing))
                grouped[buff.BuffId] = (existing.Name, existing.Count + 1);
            else
                grouped[buff.BuffId] = (Localization.GetName(buff.Definition), 1);
        }

        if (grouped.Count == 0)
        {
            root.AddChild(CreateDebugLabel("当前无RunBuff", 18));
            return root;
        }

        foreach (var (_, (name, count)) in grouped)
        {
            var label = new Label { Text = count > 1 ? $"{name} ×{count}" : name };
            label.AddThemeFontSizeOverride("font_size", 18);
            root.AddChild(label);
        }

        return root;
    }

    private Control CreateBattleDebugEquipmentOverview()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);

        var equipment = GameManager.Equipment;
        if (equipment.Count == 0)
        {
            root.AddChild(CreateDebugLabel("当前无装备", 18));
            return root;
        }

        foreach (var eq in equipment)
        {
            var label = new Label
            {
                Text = $"[{GetEquipmentRarityText(eq.Rarity)}] {Localization.GetName(eq)}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", 18);
            root.AddChild(label);
        }

        return root;
    }

    private Control CreateBattleDebugSkillOverview()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);

        if (_player.Skills.Count == 0)
        {
            root.AddChild(CreateDebugLabel("当前无技能", 18));
            return root;
        }

        foreach (var skill in _player.Skills)
        {
            var label = new Label
            {
                Text = $"[{SkillText.GetRarityName(skill.Rarity)}] {Localization.GetName(skill)}"
            };
            label.AddThemeFontSizeOverride("font_size", 18);
            root.AddChild(label);
        }

        return root;
    }

    private Control CreateBattleDebugChipOverview()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        var rows = new (string Label, int Count)[]
        {
            ("攻击芯片", GameManager.AttackChipCount),
            ("防御芯片", GameManager.DefenseChipCount),
            ("知识芯片", GameManager.KnowledgeChipCount),
            ("扩容芯片", GameManager.ExpansionChipCount)
        };

        foreach (var (labelText, count) in rows)
        {
            var label = new Label { Text = $"{labelText}：×{count}" };
            label.AddThemeFontSizeOverride("font_size", 18);
            root.AddChild(label);
        }

        return root;
    }

    private void EmitGoToMapRequestedDeferred()
    {
        EmitSignal(SignalName.GoToMapRequested);
    }

    private static Control CreateBattleDebugSection(string title, Control content)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var label = CreateDebugLabel(title, 28);
        root.AddChild(label);
        root.AddChild(content);
        return panel;
    }

    private void LogBattleDebugEncounterToContext()
    {
        if (_context == null)
        {
            return;
        }

        _context.AddTriggerLog("[Debug]");
        _context.AddTriggerLog("Rebuild Encounter");
        _context.AddTriggerLog($"Enemy Count={_encounter.Enemies.Count}");
        foreach (var enemy in _encounter.Enemies)
        {
            _context.AddTriggerLog($"Enemy={enemy.Name}");
        }
    }

    private string BuildShadowStateDebugText()
    {
        if (!_player.HasSkill(SkillIds.YingXi))
            return "玩家无影袭技能。";

        return $"InShadowState:       {_player.InShadowState}\n" +
               $"ShadowSlashUsed:     {_player.ShadowSlashUsed}\n" +
               $"ShadowSlashHit:      {_player.ShadowSlashHit}\n" +
               $"ShadowSlashDealDamage: {_player.ShadowSlashDealDamage}\n" +
               $"RemainingTurns:      {_player.RemainingTurns}\n" +
               $"CurrentShadowCost:   {_player.ShadowCost}";
    }

    private string BuildBattleDebugAiViewerText()
    {
        if (GetResolvedSelectedTarget() is not EnemyInstance enemy)
        {
            return "[b]查看AI[/b]\n请先点击一个敌人。";
        }

        var evaluation = _enemyAi.Evaluate(enemy, _player, enemy.Definition);
        var lines = new List<string>
        {
            $"[b]{enemy.Name}[/b]",
            string.Empty,
            "[b]基础权重[/b]"
        };

        foreach (var row in BuildWeightRows(evaluation.BaseWeights))
        {
            lines.Add($"{row.Label}：{row.Value:0.#}");
        }

        lines.Add(string.Empty);
        lines.Add("[b]AiRule修正[/b]");
        if (evaluation.AppliedRuleLines.Count == 0 && evaluation.ModifierLines.Count == 0)
        {
            lines.Add("无");
        }
        else
        {
            lines.AddRange(evaluation.AppliedRuleLines);
            lines.AddRange(evaluation.ModifierLines);
        }

        lines.Add(string.Empty);
        lines.Add("[b]最终权重[/b]");
        foreach (var row in BuildWeightRows(evaluation.FinalWeights))
        {
            lines.Add($"{row.Label}：{row.Value:0.#}");
        }

        return string.Join("\n", lines);
    }

    private static List<(string Label, float Value)> BuildWeightRows(EnemyActionWeights weights)
    {
        return new List<(string Label, float Value)>
        {
            ("杀", weights.Slash),
            ("火杀", weights.FireSlash),
            ("雷杀", weights.ThunderSlash),
            ("天体撞击", weights.CelestialImpact),
            ("闪", weights.Dodge),
            ("无懈可击", weights.Wuxie),
            ("费", weights.Resource),
            ("顺手牵羊", weights.ShunShou),
            ("桃", weights.Peach),
            ("万箭齐发", weights.ArrowBarrage)
        };
    }

    private static List<CardType> GetBattleDebugOverrideCardTypes()
    {
        return new List<CardType>
        {
            CardType.Kill,
            CardType.FireKill,
            CardType.ThunderKill,
            CardType.CelestialImpact,
            CardType.SureKill,
            CardType.Dodge,
            CardType.Unassailable,
            CardType.Steal,
            CardType.ArrowBarrage,
            CardType.Peach,
            CardType.Wine
        };
    }

    private int GetDebugSlotIndex(EnemyInstance enemy)
    {
        if (enemy.RuntimeStates.TryGetValue("debug_slot", out var slotValue) && slotValue is int slotIndex)
        {
            return slotIndex;
        }

        return -1;
    }

    private void LogEnemyAiDecision(EnemyInstance enemy, EnemyAiEvaluation evaluation, Card selectedCard)
    {
        _context?.AddTriggerLog($"[AI]{enemy.Name}");
        foreach (var line in BuildAiDecisionLines(evaluation))
        {
            _context?.AddTriggerLog(line);
        }
        _context?.AddTriggerLog($"最终选择：{selectedCard.Name}");
    }

    private static List<string> BuildAiDecisionLines(EnemyAiEvaluation evaluation)
    {
        var lines = new List<string>();
        foreach (var row in BuildWeightRows(evaluation.FinalWeights))
        {
            lines.Add($"{row.Label}：{row.Value:0.#}");
        }

        return lines;
    }

    // ── Localization Tab ───────────────────────────────────────────────────────

    private Control CreateLocalizationTab()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var runButton = new Button
        {
            Text = "Run Validator",
            CustomMinimumSize = new Vector2(200, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        runButton.AddThemeFontSizeOverride("font_size", 20);

        var outputLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        outputLabel.AddThemeFontSizeOverride("font_size", 16);
        outputLabel.Text = "Press 'Run Validator' to scan all localization keys.";

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, 560)
        };
        scroll.AddChild(outputLabel);

        runButton.Pressed += () =>
        {
            runButton.Disabled = true;
            runButton.Text = "Running...";
            var report = LocalizationValidator.Run();
            outputLabel.Text = report.ToFullText();
            runButton.Disabled = false;
            runButton.Text = "Run Validator";
        };

        root.AddChild(runButton);
        root.AddChild(scroll);
        return root;
    }

    private static void LogEnemySpawn(EnemyInstance enemy)
    {
        var poolInfo = enemy.SharedPool != null
            ? $"SharedPool(Max={enemy.SharedPool.MaxHP}/Current={enemy.SharedPool.CurrentHP})"
            : "None";
        GD.Print($"[EnemySpawn] {enemy.Name}  TemplateMaxHP={enemy.Definition.MaxHP}  InstanceMaxHP={enemy.MaxHealth}  InstanceCurrentHP={enemy.Health}  InitialMana={enemy.CurrentMana:0.#}  SharedPool={poolInfo}");
        if (enemy.Health != enemy.MaxHealth && enemy.SharedPool == null)
        {
            GD.PrintErr($"[EnemySpawn] ASSERTION FAILED: {enemy.Name} CurrentHP({enemy.Health}) != MaxHP({enemy.MaxHealth}). Check EnemyInstance constructor.");
        }
    }
}
