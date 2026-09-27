//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Rendering.cs
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
    private void RenderPlayerSkills()
    {
        var skillArea = GetPrimaryFriendlySkillArea();
        if (skillArea == null)
        {
            return;
        }

        foreach (var child in skillArea.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var skill in _player.Skills)
        {
            var button = CreateSkillButton(skill);
            skillArea.AddChild(button);
        }
    }

    private Button CreateSkillButton(Skill skill)
    {
        var button = new Button
        {
            Text = skill.IconText,
            CustomMinimumSize = new Vector2(58, 58),
            FocusMode = FocusModeEnum.None
        };
        button.MouseEntered += () => TooltipManager.Show(Localization.GetName(skill), button);
        button.MouseExited += () => TooltipManager.Hide();
        button.AddThemeFontSizeOverride("font_size", 20);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", CreateSkillButtonStyle(skill, new Color(0.18f, 0.19f, 0.22f)));
        button.AddThemeStyleboxOverride("hover", CreateSkillButtonStyle(skill, new Color(0.25f, 0.27f, 0.32f)));
        button.AddThemeStyleboxOverride("pressed", CreateSkillButtonStyle(skill, new Color(0.12f, 0.13f, 0.16f)));
        button.Pressed += () => ShowSkillDetail(skill);
        return button;
    }

    private static StyleBoxFlat CreateSkillButtonStyle(Skill skill, Color background)
    {
        var border = skill.Rarity switch
        {
            SkillRarity.Common => new Color(0.65f, 0.70f, 0.75f),
            SkillRarity.Rare => new Color(0.22f, 0.56f, 0.95f),
            SkillRarity.Legendary => new Color(0.95f, 0.68f, 0.18f),
            _ => Colors.White
        };

        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 29,
            CornerRadiusBottomRight = 29,
            CornerRadiusTopLeft = 29,
            CornerRadiusTopRight = 29
        };
    }

    private void RenderActionCards()
    {
        if (_actionArea == null)
        {
            return;
        }

        BeginRenderActionSlots();

        // 观星重复：仅显示被锁定的记录牌。
        if (_player.GuanxingPhase == GuanxingPhase.Repeating && _player.GuanxingRecordedCardType.HasValue)
        {
            var lockedCard = new Card(_player.GuanxingRecordedCardType.Value);
            AddActionCardToSlot(
                lockedCard,
                CanChooseAction(lockedCard),
                Localization.GetFmt("ui.cost_fmt", 0),
                Localization.GetFmt("battle.card.guanxing_rep_fmt", _player.GuanxingRepeatsRemaining));
            FinishRenderActionCards();
            return;
        }

        // 连弩模式：仅显示普通杀（0费，无限点击）。
        if (_zhugeMode)
        {
            var zhugekillCard = Card.Kill();
            AddActionCardToSlot(
                zhugekillCard,
                true,
                Localization.GetFmt("ui.cost_fmt", 0),
                Localization.Get("battle.card.zhugemode"));
            FinishRenderActionCards();
            return;
        }

        // 影袭状态：出牌区仅剩影袭杀（未出招时）和潜伏。
        if (_player.InShadowState)
        {
            if (!_player.ShadowSlashUsed)
            {
                var shadowKillCard = Card.ShadowKill();
                AddActionCardToSlot(
                    shadowKillCard,
                    CanChooseAction(shadowKillCard),
                    Localization.GetFmt("ui.cost_fmt", BattleRules.FormatMana(BattleRules.GetCardCost(_player, CardType.ShadowKill))),
                    Localization.Get("battle.card.shadow_kill"));
            }
            var lurkCard = Card.ShadowLurk();
            AddActionCardToSlot(
                lurkCard,
                CanChooseAction(lurkCard),
                Localization.GetFmt("ui.cost_fmt", 0),
                Localization.GetFmt("battle.card.shadow_lurk_fmt", _player.RemainingTurns));
            FinishRenderActionCards();
            return;
        }

        // 正常出牌和规则判定必须读取同一份实时列表。此前渲染层维护了另一套硬编码列表，
        // 技能或事件新增招式后容易出现“规则中存在、UI中溢出或缺失”的分叉。
        var cards = GetAvailableActionCards();
        var visibleCount = System.Math.Min(cards.Count, BattleHotkeySystem.ActionSlotCount);
        for (var i = 0; i < visibleCount; i++)
        {
            AddActionCard(cards[i]);
        }

        if (cards.Count > BattleHotkeySystem.ActionSlotCount)
        {
            ShowCardOverflowRemovalChoice(cards);
        }

        FinishRenderActionCards();
    }

    private void RenderReactionCards(IReaction reaction)
    {
        if (_actionArea == null)
        {
            return;
        }

        BeginRenderActionSlots();

        _reactionOptionsByCard.Clear();
        foreach (var option in reaction.Options)
        {
            if (!option.CardType.HasValue)
            {
                continue;
            }

            AddReactionCard(option);
        }
        RefreshBattleDebugHotkeyViewer();
    }

    private void AddReactionCard(ReactionOption option)
    {
        if (!option.CardType.HasValue)
        {
            return;
        }

        var card = new Card(option.CardType.Value);
        var costText = option.ReactionCost.HasValue ? Localization.GetFmt("ui.cost_fmt", BattleRules.FormatMana(option.ReactionCost.Value)) : null;
        var cardUi = AddActionCardToSlot(
            card,
            option.Enabled,
            costText,
            option.Enabled ? Localization.GetFmt("battle.card.reaction_fmt", option.DisplayText) : option.DisplayText);
        if (cardUi != null)
        {
            _reactionOptionsByCard[cardUi] = option;
        }
    }

    private void AddActionCard(Card card)
    {
        string? costTextOverride = null;
        string? descriptionTextOverride = null;
        if (card.Type == CardType.Peach && _player.HasSkill(SkillIds.Qingnang))
        {
            descriptionTextOverride = Localization.Get("battle.card.qingnang");
        }
        if (card.Type == CardType.Kill && GameManager.HasEquipment(EquipmentIds.ZhugeCrossbow))
        {
            descriptionTextOverride = Localization.Get("battle.card.zhugecrossbow");
        }
        if (card.Type == CardType.YingXiActivate)
        {
            descriptionTextOverride = Localization.Get("battle.card.yingxi");
        }

        // 统一按 BattleRules.GetCardCost（真正结算时使用的同一个函数）计算显示费用，
        // 而不是逐个卡牌类型手写覆盖——手写覆盖曾经漏掉火纹银枪（火杀）、雷矛（雷杀）、
        // 谦逊（酒/顺手牵羊/无懈可击）等装备/技能带来的费用变化，导致卡面显示的费用
        // 和实际结算费用不一致。只要动态费用与卡牌静态基础费用不同就覆盖显示文本，
        // 这样任何现在或未来影响费用的装备/技能都会自动正确显示，不需要再逐个补丁。
        var dynamicCost = BattleRules.GetCardCost(_player, card.Type, ResolveTargetForCard(card));
        if (dynamicCost != card.Cost)
        {
            costTextOverride = Localization.GetFmt("ui.cost_fmt", BattleRules.FormatMana(dynamicCost));
        }

        AddActionCardToSlot(card, CanChooseAction(card), costTextOverride, descriptionTextOverride);
    }

    /// <summary>
    /// 当永久行动牌超过固定槽位容量时，要求玩家移除一张牌。
    ///
    /// 使用统一 ChoicePanel 展示当前实时牌表；结果写入 Run 牌型状态后重新渲染。
    /// 若一次移除后仍超过容量，下一次渲染会继续要求选择，直到不再溢出。
    /// </summary>
    private void ShowCardOverflowRemovalChoice(IReadOnlyList<Card> cards)
    {
        if (_cardOverflowChoicePanel != null && IsInstanceValid(_cardOverflowChoicePanel))
        {
            return;
        }

        var cardTypes = new List<CardType>();
        foreach (var card in cards)
        {
            if (!cardTypes.Contains(card.Type))
            {
                cardTypes.Add(card.Type);
            }
        }

        var provider = new CardChoiceProvider
        {
            Count = cardTypes.Count,
            Randomize = false,
            IncludeCharacterExclusive = true,
            CardPool = cardTypes
        };

        var panel = new ChoicePanel
        {
            Name = "CardOverflowChoicePanel",
            ZIndex = 5000
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        _cardOverflowChoicePanel = panel;

        panel.ChoiceCompleted += result =>
        {
            if (result.Cancelled || result.Payload is not CardType removedType)
            {
                return;
            }

            GameManager.RemovePlayerCardType(removedType);
            _context?.AddTriggerLog($"[ActionBar] Remove overflow card: {removedType}");
            TooltipManager.Hide();
            panel.QueueFree();
            _cardOverflowChoicePanel = null;
            RenderActionCards();
        };

        panel.Configure(new ChoiceRequest
        {
            Title = Localization.Get("battle.card_overflow.title"),
            Description = Localization.GetFmt(
                "battle.card_overflow.description",
                cards.Count,
                BattleHotkeySystem.ActionSlotCount),
            Footer = Localization.Get("battle.card_overflow.footer"),
            AllowCancel = false,
            Options = provider.CreateChoices()
        });
    }

    private void DismissCardOverflowRemovalChoice()
    {
        if (_cardOverflowChoicePanel != null && IsInstanceValid(_cardOverflowChoicePanel))
        {
            _cardOverflowChoicePanel.QueueFree();
        }

        _cardOverflowChoicePanel = null;
    }

    // 手牌伤害预估：不可打出（费用不足等）也照算照显示，playable 是独立判断，
    // 与卡面费用显示的既有范式一致（见上面 dynamicCost）。
    private DamagePreviewResult? ComputeDamagePreview(Card card)
    {
        if (!DamagePreviewSettings.IsEnabled || _context == null)
        {
            return null;
        }

        var lockedTarget = GetResolvedSelectedTarget() as Player;
        return DamagePreviewService.PreviewCard(_context, _player, card.Type, lockedTarget);
    }

    private void BeginRenderActionSlots()
    {
        _nextActionSlotIndex = 0;
        foreach (var slot in _actionSlots)
        {
            slot?.Clear();
        }
    }

    private CardUI? AddActionCardToSlot(Card card, bool playable, string? costTextOverride = null, string? descriptionTextOverride = null, DamagePreviewResult? damagePreview = null)
    {
        if (_nextActionSlotIndex >= _actionSlots.Length)
        {
            return null;
        }

        var slot = _actionSlots[_nextActionSlotIndex++];
        slot?.SetCard(card, playable, costTextOverride, descriptionTextOverride, damagePreview ?? ComputeDamagePreview(card));
        return slot?.CardUi;
    }

    private void FinishRenderActionCards()
    {
        if (_actionArea == null)
        {
            return;
        }

        RefreshBattleDebugHotkeyViewer();
    }

    private void RefreshUi()
    {
        if (_turnLabel != null)
        {
            _turnLabel.Text = Localization.GetFmt("battle.turn_label", _turnNumber, GetPhaseName());
        }

        if (_turnCounterLabel != null)
        {
            _turnCounterLabel.Text = _turnNumber.ToString();
        }

        UpdatePlayerActionDisplay();
        RefreshFriendlySlots();
        RefreshEnemySlots();
        RefreshLowHealthScreenBorder();
    }

    private void RefreshLowHealthScreenBorder()
    {
        _lowHealthScreenBorder?.SetPlayerHealth(_player.Health);
    }

    private void RefreshFriendlySlots()
    {
        EnsureFriendlySlotsBuilt();

        for (var i = 0; i < _friendlyCards.Count; i++)
        {
            var card = _friendlyCards[i];
            BattleUnit? unit = i == 0 ? _player : null;
            card.Visible = unit != null;
            if (unit == null) continue;

            var stateText = _stackingCard == null
                ? Localization.GetFmt("battle.state.status_prefix", GetPlayerStateText(_player))
                : Localization.GetFmt("battle.state.selected_fmt", Localization.GetName(_stackingCard), _stackCount);
            card.Refresh(unit, _context, BattleTeam.Player, false, stateText);
        }
    }

    private void RefreshEnemySlots()
    {
        EnsureEnemySlotsBuilt();
        if (_enemyContainer != null)
            BattleCardLayout.ConfigureEnemyContainer(_enemyContainer, _encounter.Enemies.Count);

        var visibleCount = 0;
        EnemyInstance? bossUnit = null;
        for (var i = 0; i < _enemyCards.Count; i++)
        {
            var card = _enemyCards[i];
            BattleUnit? unit = i < _encounter.Enemies.Count ? _encounter.Enemies[i] : null;
            var shouldUseBossUi = unit is EnemyInstance enemy && enemy.Definition.Type == EnemyType.Boss;
            card.Visible = false;
            if (unit == null) continue;

            if (shouldUseBossUi)
            {
                bossUnit ??= (EnemyInstance)unit;
                visibleCount++;
            }

            var isSelected = false;
            var playerUnit = unit as Player;
            var deadText = Localization.Get("battle.state.dead_unit");
            var stateText = playerUnit != null
                ? Localization.GetFmt("battle.state.status_prefix", unit.IsDead ? deadText : GetPlayerStateText(playerUnit))
                : Localization.GetFmt("battle.state.status_prefix", unit.IsDead ? deadText : Localization.Get("battle.state.waiting_action"));
            card.Refresh(unit, _context, BattleTeam.Enemy, isSelected, stateText);
        }

        if (_enemyStatusPanel != null)
        {
            _enemyStatusPanel.Visible = false;
        }

        RefreshBossStatusUi(bossUnit);
        UpdateBattleStageEnemyVisuals();
        RefreshSelectedTargetHighlight();

        if (visibleCount != _lastLoggedVisibleEnemySlots || _encounter.Enemies.Count != _lastLoggedEncounterCount)
        {
            GD.Print($"[EncounterUI] Enemy slots visible={visibleCount}, encounterCount={_encounter.Enemies.Count}");
            _lastLoggedVisibleEnemySlots = visibleCount;
            _lastLoggedEncounterCount = _encounter.Enemies.Count;
        }
    }

    private void RefreshBossStatusUi(EnemyInstance? bossUnit)
    {
        if (_bossStatusUi == null)
        {
            return;
        }

        _bossStatusUi.Visible = bossUnit != null;
        if (bossUnit == null)
        {
            return;
        }

        _bossStatusUi.Refresh(bossUnit, _context);
    }

    private HBoxContainer? GetPrimaryFriendlySkillArea()
    {
        return null;
    }

    private Control? GetPrimaryFriendlyAvatarAnchor()
    {
        return _friendlyCards.Count > 0 ? _friendlyCards[0].AvatarAnchor : null;
    }

    private Control? GetPrimaryFriendlyManaAnchor()
    {
        return _friendlyCards.Count > 0 ? _friendlyCards[0].ManaAnchor : null;
    }

    private void UpdatePlayerActionDisplay()
    {
        if (_playerActionDisplayLabel == null)
        {
            return;
        }

        _playerActionDisplayLabel.Visible = false;
    }

    private void ShowSelectionConfirmUi()
    {
        if (_selectionConfirmPanel == null || _selectionProgressBar == null)
        {
            return;
        }

        _selectionConfirmPanel.Visible = true;
        _selectionProgressBar.Value = 100;
    }

    private void HideSelectionConfirmUi()
    {
        if (_selectionConfirmPanel != null)
        {
            _selectionConfirmPanel.Visible = false;
        }
    }

    private EnemyInstance? GetFirstAliveEnemy()
    {
        foreach (var enemy in _encounter.Enemies)
        {
            if (!enemy.IsDead)
            {
                return enemy;
            }
        }

        return null;
    }

    private static string GetActionDisplayText(BattleAction? action)
    {
        return action?.DisplayName ?? Localization.Get("battle.state.pending");
    }

    private string GetPhaseActionPrompt()
    {
        if (_phase == BattlePhase.BattlePrePhase)
        {
            if (_player.GuanxingPhase == GuanxingPhase.Recording)
                return Localization.Get("battle.action.guanxing_rec");
            if (_player.GuanxingPhase == GuanxingPhase.Repeating)
                return Localization.GetFmt("battle.action.guanxing_rep_fmt", _player.GuanxingRepeatsRemaining);
        }

        return _phase switch
        {
            BattlePhase.StartPhase => Localization.Get("battle.action.before"),
            BattlePhase.BattlePrePhase => Localization.GetFmt("battle.action.action_fmt", GetTargetDisplayName(GetResolvedSelectedTarget())),
            BattlePhase.BattlePhase => Localization.Get("battle.action.resolve"),
            BattlePhase.BattlePostPhase => Localization.Get("battle.action.post"),
            BattlePhase.EndPhase => Localization.Get("battle.action.end"),
            BattlePhase.GameOver => Localization.Get("battle.action.gameover"),
            _ => string.Empty
        };
    }

    private string GetPhaseBattlefieldPrompt()
    {
        return string.Empty;
    }

    private string GetPhaseStateText()
    {
        return _phase switch
        {
            BattlePhase.StartPhase => Localization.Get("battle.state.before"),
            BattlePhase.BattlePrePhase => Localization.Get("battle.state.waiting"),
            BattlePhase.BattlePhase => Localization.Get("battle.state.resolving"),
            BattlePhase.BattlePostPhase => Localization.Get("battle.state.post_fx"),
            BattlePhase.EndPhase => Localization.Get("battle.state.cleanup"),
            BattlePhase.GameOver => Localization.Get("battle.state.gameover"),
            _ => string.Empty
        };
    }

    private string GetPlayerStateText(Player player)
    {
        if (player.IsDead)
            return Localization.Get("battle.state.dead");

        if (player.DyingState == DyingState.Dying)
            return Localization.Get("battle.state.dying");

        var states = new List<string>();
        if (player.WinePower > 0)
            states.Add(Localization.GetFmt("state.wine_now_fmt", player.WinePower, $"{BattleRules.GetWineDamageMultiplier(player, player.WinePower):0.##}"));

        if (player.PendingWinePower > 0)
            states.Add(Localization.GetFmt("state.wine_next_fmt", player.PendingWinePower, $"{BattleRules.GetWineDamageMultiplier(player, player.PendingWinePower):0.##}"));

        if (player.LianyingFreeKillAvailable)
            states.Add(Localization.Get("state.lianying_free"));

        if (player.LianyingPrepared)
            states.Add(Localization.Get("state.lianying_ready"));

        if (player.HasUsedWineRevive)
            states.Add(Localization.Get("state.wine_revive_used"));

        if (player.GuanxingPhase == GuanxingPhase.Recording)
            states.Add(Localization.Get("state.guanxing_rec"));
        else if (player.GuanxingPhase == GuanxingPhase.Repeating)
            states.Add(Localization.GetFmt("state.guanxing_rep_fmt", player.GuanxingRepeatsRemaining));

        if (player.JiGuActive)
            states.Add(Localization.GetFmt("state.jigu_fmt", player.JiGuTurnsRemaining));

        if (player.StealthModuleActive)
            states.Add(Localization.Get("state.stealth_module"));

        if (player.HasWeakness)
            states.Add(Localization.GetFmt("state.weakness_fmt", player.WeaknessLayers));

        if (player.WarDrumActive)
            states.Add(Localization.Get("state.wardrum"));
        else if (player.WarDrumPending)
            states.Add(Localization.Get("state.wardrum_next"));

        if (states.Count > 0)
            return string.Join("；", states);

        return GetPhaseStateText();
    }

    private string GetPhaseName()
    {
        return _phase switch
        {
            BattlePhase.StartPhase => Localization.Get("battle.phase.before"),
            BattlePhase.BattlePrePhase => Localization.Get("battle.phase.action"),
            BattlePhase.BattlePhase => Localization.Get("battle.phase.resolve"),
            BattlePhase.BattlePostPhase => Localization.Get("battle.phase.post"),
            BattlePhase.EndPhase => Localization.Get("battle.phase.end"),
            BattlePhase.GameOver => Localization.Get("battle.phase.gameover"),
            _ => string.Empty
        };
    }

    private void SetActionText(string text)
    {
        if (_actionLabel != null)
        {
            _actionLabel.Text = text;
        }
    }

    private static string EscapeBbCode(string text)
    {
        return text
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }
}
