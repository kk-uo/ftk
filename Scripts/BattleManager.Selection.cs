//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Selection.cs
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
using System.Threading.Tasks;

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    private async void OnActionCardPressed(CardUI cardUi)
    {
        if (_reactionMode)
        {
            OnReactionCardPressed(cardUi);
            return;
        }

        if (_inputLocked || _context == null || _context.GameOver || _phase != BattlePhase.BattlePrePhase || cardUi.CardData == null)
        {
            return;
        }

        var runId = _battleRunId;
        var card = cardUi.CardData;

        // 冰蓝装甲：装备者不能再通过【费】这张牌本身恢复费用，直接判定无法使用，不消耗牌/操作/结算。
        if (card.Type == CardType.Fee && GameManager.HasEquipment(EquipmentIds.IceBlueArmor))
        {
            ShowBattleMessage(Localization.Get("battle.msg.ice_blue_no_fee"));
            RenderActionCards();
            return;
        }

        // 观星重复：直接以记录的牌型和数量（费用免除）执行，目标使用当前选中目标。
        if (_player.GuanxingPhase == GuanxingPhase.Repeating
            && _player.GuanxingRecordedCardType.HasValue
            && card.Type == _player.GuanxingRecordedCardType.Value)
        {
            var replayCard = new Card(_player.GuanxingRecordedCardType.Value);
            var replayAction = BattleAction.FromCard(
                replayCard,
                _player.GuanxingRecordedCount,
                ResolveTargetForCard(replayCard));
            ClearStack();
            _ = ResolvePlayerAction(replayAction, cardUi, runId);
            return;
        }

        if (_stackingCard != null)
        {
            await UpdatePendingActionSelection(card, cardUi, runId);
            return;
        }

        if (!CanAffordAction(card, 1))
        {
            ShowBattleMessage(Localization.GetFmt("battle.msg.no_cost_fmt", card.Name));
            RenderActionCards();
            return;
        }

        await BeginPendingActionSelection(card, cardUi, runId);
    }

    private void OnReactionCardPressed(CardUI cardUi)
    {
        if (!_reactionMode || _reactionSelection == null || _reactionSelection.Task.IsCompleted)
        {
            return;
        }

        if (!_reactionOptionsByCard.TryGetValue(cardUi, out var option) || !option.Enabled)
        {
            return;
        }

        _context?.AddTriggerLog("[Reaction]");
        _context?.AddTriggerLog("Selected:");
        _context?.AddTriggerLog(option.Text);
        _reactionSelection.SetResult(option);
    }

    private async Task BeginPendingActionSelection(Card card, CardUI sourceUi, int runId)
    {
        _stackingCard = card;
        _stackCount = 1;
        LogPlayerSelection();

        // 费和闪无决策意义：跳过动画与等待条，直接结算。
        if (IsInstantCard(card.Type))
        {
            await ResolveCurrentStack(null, runId);
            return;
        }

        // 诸葛连弩：首次普通杀在动画前立即进入连弩模式，避免 260ms 动画期间状态被污染。
        if (card.Type is CardType.Kill or CardType.PoisonKill && !_zhugeMode && GameManager.HasEquipment(EquipmentIds.ZhugeCrossbow))
        {
            _zhugeMode = true;
            StartZhugeTimer(runId);
            return;
        }

        await PlayActionFeedback(sourceUi);
        if (IsStaleRun(runId))
        {
            return;
        }

        if (ShouldConfirmImmediately(card, _stackCount))
        {
            await ResolveCurrentStack(null, runId);
            return;
        }

        StartStackTimer(runId);
    }

    private async Task UpdatePendingActionSelection(Card card, CardUI sourceUi, int runId)
    {
        if (IsStaleRun(runId) || _stackingCard == null)
        {
            return;
        }

        // 冰蓝装甲：装备者不能再通过【费】这张牌本身恢复费用，直接判定无法使用，不消耗牌/操作/结算。
        if (card.Type == CardType.Fee && GameManager.HasEquipment(EquipmentIds.IceBlueArmor))
        {
            ShowBattleMessage(Localization.Get("battle.msg.ice_blue_no_fee"));
            RenderActionCards();
            return;
        }

        // 连弩模式：持续累加杀数，不重置计时器，不切换出牌，不等待动画（避免堆叠延迟）。
        if (_zhugeMode && card.Type is CardType.Kill or CardType.PoisonKill && _stackingCard.Type is CardType.Kill or CardType.PoisonKill)
        {
            _stackCount += 1;
            LogPlayerSelection();
            return;
        }

        if (card.Type == _stackingCard.Type)
        {
            if (CanStack(card) && CanAffordStack(card, _stackCount + 1))
            {
                _stackCount += 1;
            }
            else
            {
                await ResolveCurrentStack(null, runId);
                return;
            }
        }
        else
        {
            if (!CanAffordAction(card, 1))
            {
                ShowBattleMessage(Localization.GetFmt("battle.msg.no_cost_fmt", card.Name));
                RenderActionCards();
                return;
            }

            _stackingCard = card;
            _stackCount = 1;
        }

        _stackVersion += 1;
        LogPlayerSelection();

        // 费和闪无决策意义：跳过动画与等待条，直接结算。
        if (IsInstantCard(_stackingCard.Type))
        {
            await ResolveCurrentStack(null, runId);
            return;
        }

        await PlayActionFeedback(sourceUi);
        if (IsStaleRun(runId))
        {
            return;
        }

        if (ShouldConfirmImmediately(_stackingCard, _stackCount))
        {
            await ResolveCurrentStack(null, runId);
            return;
        }

        if (CanContinueEditingSelection())
        {
            StartStackTimer(runId);
            return;
        }

        await ResolveCurrentStack(null, runId);
    }

    private void StartStackTimer(int runId)
    {
        _stackVersion += 1;
        var version = _stackVersion;
        _activeTimerWindow = StackWindow;
        _selectionRemainingSeconds = StackWindow;
        SetStackPrompt();
        ShowSelectionConfirmUi();
        RefreshUi();
        RenderActionCards();
        _ = WaitForStackTimeout(version, runId);
    }

    private async Task WaitForStackTimeout(int version, int runId)
    {
        await ToSignal(GetTree().CreateTimer(StackWindow), SceneTreeTimer.SignalName.Timeout);

        if (IsStaleRun(runId) || _stackingCard == null || version != _stackVersion || _inputLocked || _context?.GameOver == true)
        {
            return;
        }

        await ResolveCurrentStack(null, runId);
    }

    private void StartZhugeTimer(int runId)
    {
        _stackVersion += 1;
        var version = _stackVersion;
        _activeTimerWindow = ZhugeCrossbowWindow;
        _selectionRemainingSeconds = ZhugeCrossbowWindow;
        SetActionText(Localization.Get("battle.action.zhugemode"));
        if (_selectionConfirmLabel != null)
            _selectionConfirmLabel.Text = Localization.GetFmt("battle.confirm.zhugemode_fmt", _stackCount, $"{ZhugeCrossbowWindow:0.0}s");
        ShowSelectionConfirmUi();
        RefreshUi();
        RenderActionCards();
        _ = WaitForZhugeTimeout(version, runId);
    }

    private async Task WaitForZhugeTimeout(int version, int runId)
    {
        await ToSignal(GetTree().CreateTimer(ZhugeCrossbowWindow), SceneTreeTimer.SignalName.Timeout);

        if (IsStaleRun(runId) || _stackingCard == null || version != _stackVersion || _inputLocked || _context?.GameOver == true)
        {
            return;
        }

        await ResolveCurrentStack(null, runId);
    }

    private void StartGuanxingRepeatTimer(int runId)
    {
        _guanxingTimerVersion += 1;
        var version = _guanxingTimerVersion;
        _activeTimerWindow = GuanxingWindow;
        _selectionRemainingSeconds = GuanxingWindow;
        if (_selectionConfirmLabel != null)
        {
            _selectionConfirmLabel.Text = Localization.GetFmt("battle.confirm.guanxing_rep_fmt", GuanxingWindow);
        }

        ShowSelectionConfirmUi();
        RefreshUi();
        _ = WaitForGuanxingTimeout(version, runId);
    }

    private async Task WaitForGuanxingTimeout(int version, int runId)
    {
        await ToSignal(GetTree().CreateTimer(GuanxingWindow), SceneTreeTimer.SignalName.Timeout);

        if (IsStaleRun(runId) || version != _guanxingTimerVersion || _inputLocked || _context?.GameOver == true)
        {
            return;
        }

        if (_player.GuanxingPhase != GuanxingPhase.Repeating)
        {
            return;
        }

        _guanxingTimerVersion += 1;
        HideSelectionConfirmUi();
        await ResolvePlayerAction(BattleAction.FromCard(Card.Fee(), 1), null, runId);
    }

    private void SetStackPrompt()
    {
        if (_stackingCard == null)
        {
            return;
        }

        var action = BattleAction.FromCard(_stackingCard, _stackCount);
        var targetText = _stackingCard.TargetType == CardTargetType.Targeted
            ? Localization.GetFmt("battle.state.target_fmt", GetTargetDisplayName(GetResolvedSelectedTarget()))
            : string.Empty;
        var text = Localization.GetFmt("battle.msg.selected_fmt", action.DisplayName, targetText);
        SetActionText(text);
        if (_selectionConfirmLabel != null)
        {
            _selectionConfirmLabel.Text = text;
        }
    }

    private async Task ResolveCurrentStack(CardUI? sourceUi, int runId)
    {
        if (IsStaleRun(runId) || _stackingCard == null)
        {
            return;
        }

        var action = CreateActionForCard(_stackingCard, _stackCount);
        ClearStack();
        _context?.AddTriggerLog("行动已确认");
        await ResolvePlayerAction(action, sourceUi, runId);
    }

    private void ClearStack()
    {
        _stackingCard = null;
        _stackCount = 0;
        _selectionRemainingSeconds = 0;
        _activeTimerWindow = 0;
        _stackVersion += 1;
        _zhugeMode = false;
        HideSelectionConfirmUi();
    }

    private async Task ResolvePlayerAction(BattleAction playerAction, CardUI? sourceUi, int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        _inputLocked = true;
        _guanxingTimerVersion += 1;
        ClearStack();
        // 教程只能在行动已通过输入与费用校验、并真正开始结算后推进。
        // 不能在 ActionSlot 的点击瞬间推进：可堆叠牌仍会经历确认窗口，若教程
        // 这时切至说明步骤并锁定输入，确认计时器会被中断并遗留旧的选择状态。
        if (TutorialManager.IsActive)
        {
            TutorialEventBus.NotifyCardPlayed(playerAction.Type);
        }

        // 只有行动已经通过合法性检查并正式进入结算，才结束上一轮的出牌结果展示。
        // 费用不足、禁用卡牌等无效点击不会走到这里，因此不会提前清除结果。
        DismissCardRevealPopup();

        // 观星/天机：使用后进入记录状态（下一回合正常出牌将被记录）。
        if (playerAction.Type == CardType.Guanxing
            && (_player.HasSkill(SkillIds.Guanxing) || _player.HasSkill(SkillIds.TianJi)))
        {
            _player.StartGuanxingRecording();
            if (_player.HasSkill(SkillIds.Guanxing))
            {
                _context.ReportPlayerCharacterSkillTriggered(
                    _player,
                    SkillIds.Guanxing,
                    TriggerTiming.OnCardSelected,
                    variant: "recording");
            }
            _context.AddTriggerLog("[Guanxing]");
            _context.AddTriggerLog("玩家进入 Recording 状态");
            if (_player.HasSkill(SkillIds.TianJi))
            {
                _player.GainMana(1);
                _context.ReportPlayerCharacterSkillTriggered(
                    _player,
                    SkillIds.TianJi,
                    TriggerTiming.OnCardSelected,
                    variant: "guanxing_mana");
                _context.AddTriggerLog("[天机]");
                _context.AddTriggerLog("天机：观星使用，获得1费。");
            }
        }

        if (sourceUi != null)
        {
            await PlayActionFeedback(sourceUi);
            if (IsStaleRun(runId))
            {
                return;
            }
        }

        RenderActionCards();
        var enemyActions = BuildEnemyActions();
        foreach (var enemyAction in enemyActions)
        {
            _battleLogManager.Service.RecordStructured(
                BattleLogEventKind.AiDecision,
                _turnNumber,
                BattlePhase.BattlePrePhase.ToString(),
                "battlelog.debug_line",
                new[] { $"{enemyAction.Enemy.DisplayName} -> {enemyAction.Action.DisplayName}" },
                debugOnly: true,
                actor: enemyAction.Enemy.DisplayName,
                target: enemyAction.Action.Target?.Name ?? _player.DisplayName,
                source: "EnemyAI",
                card: enemyAction.Action.DisplayName,
                result: "Selected");
        }
        // 锁定目标是玩家确认行动时的选择。非指向牌不会把敌人写入 BattleAction.Target，
        // 因此单独保存在 BattleContext，供【司敌】等“观察当前锁定敌人”的规则使用。
        _context.PlayerLockedTarget = playerAction.Target as EnemyInstance
            ?? GetResolvedSelectedTarget() as EnemyInstance;
        _context.PlayerAction = playerAction;
        if (playerAction.Type == CardType.IceKill && _player.HasSkill(SkillIds.JiHan))
        {
            _context.ReportPlayerCharacterSkillTriggered(
                _player,
                SkillIds.JiHan,
                TriggerTiming.OnCardSelected,
                variant: "ice_kill");
        }
        _context.ClearEnemyActions();
        foreach (var enemyActionEntry in enemyActions)
        {
            if (enemyActionEntry.Action.Type == CardType.Guanxing && enemyActionEntry.Enemy.HasSkill(SkillIds.Guanxing))
            {
                enemyActionEntry.Enemy.StartGuanxingRecording();
                _context.AddTriggerLog("[Guanxing]");
                _context.AddTriggerLog($"{enemyActionEntry.Enemy.DisplayName}进入 Recording 状态");
            }

            _context.SetActionForEnemy(enemyActionEntry.Enemy, enemyActionEntry.Action);
        }
        _triggerManager.RaiseTrigger(TriggerTiming.OnCardSelected, _context);
        await EnterBattlePhase(playerAction, enemyActions, runId);
    }

    private List<EnemyActionEntry> BuildEnemyActions()
    {
        var actions = new List<EnemyActionEntry>();
        foreach (var enemy in _encounter.Enemies)
        {
            if (enemy.IsDead)
            {
                continue;
            }

            var allies = _encounter.Enemies;
            enemy.RuntimeStates["current_turn"] = _context?.TurnCounter ?? 1;

            // 巨型脓包的自爆属于濒死后挂起的强制行动，不允许再被普通脚本/AI覆盖成费。
            if (enemy.RuntimeStates.TryGetValue("zibao_pending", out var ziBaoPending)
                && ziBaoPending is true)
            {
                actions.Add(new EnemyActionEntry(enemy, BattleAction.FromCard(Card.ZiBaoAttack(), 1, _player)));
                continue;
            }

            // 独眼巨人血债血偿濒死待发：强制行动，不允许被普通AI覆盖。
            if (enemy.RuntimeStates.TryGetValue("xuezhaixuechou_near_death_pending", out var xzPending)
                && xzPending is true)
            {
                actions.Add(new EnemyActionEntry(enemy, BattleAction.FromCard(Card.XueZhaiAttack(), 1, _player)));
                continue;
            }

            var scriptedAction = TryCreateScriptedEnemyAction(enemy, allies);
            if (scriptedAction != null)
            {
                actions.Add(new EnemyActionEntry(enemy, scriptedAction));
                continue;
            }

            // 观星重复阶段：强制使用已记录的行动，跳过 AI 选牌。
            if (enemy.GuanxingPhase == GuanxingPhase.Repeating && enemy.GuanxingRecordedCardType.HasValue)
            {
                var repeatedCard = new Card(enemy.GuanxingRecordedCardType.Value);
                var repeatedTarget = _enemyAi.SelectTarget(enemy, repeatedCard, _player, allies);
                actions.Add(new EnemyActionEntry(enemy, BattleAction.FromCard(repeatedCard, enemy.GuanxingRecordedCount, repeatedTarget)));
                continue;
            }

            // 观星录制阶段：强制选择最优攻击牌作为录制动作，绕过普通 AI 权重评估。
            if (enemy.GuanxingPhase == GuanxingPhase.Recording)
            {
                var recordCard = _enemyAi.SelectGuanxingRecordCard(enemy, enemy.Definition);
                var recordFreeExtra = enemy.GetExtraFreeCardCount(recordCard.Type);
                var recordCount = GetEnemyActionCount(enemy, recordCard, recordFreeExtra);
                var recordTarget = _enemyAi.SelectTarget(enemy, recordCard, _player, allies);
                actions.Add(new EnemyActionEntry(enemy, BattleAction.FromCard(recordCard, recordCount, recordTarget)));
                continue;
            }

            var evaluation = _enemyAi.Evaluate(enemy, _player, enemy.Definition, allies);
            var card = TryGetDebugOverrideAction(enemy) ?? _enemyAi.SelectAction(enemy, _player, enemy.Definition, allies);
            if (_battleDebugMode && _battleDebugAiDecisionCheckBox?.ButtonPressed == true)
            {
                LogEnemyAiDecision(enemy, evaluation, card);
            }
            var freeExtra = enemy.GetExtraFreeCardCount(card.Type);
            var count = GetEnemyActionCount(enemy, card, freeExtra);
            // 诸葛连弩：AI视为2秒内打出6张普通杀。
            if (card.Type == CardType.Kill && enemy is EnemyInstance crossbowEI && crossbowEI.HasEquipment(EquipmentIds.ZhugeCrossbow))
            {
                count = 6;
            }
            var target = _enemyAi.SelectTarget(enemy, card, _player, allies);
            actions.Add(new EnemyActionEntry(enemy, BattleAction.FromCard(card, count, target)));
        }

        if (actions.Count == 0)
        {
            var fallbackEnemy = GetFirstAliveEnemy();
            if (fallbackEnemy != null)
            {
                actions.Add(new EnemyActionEntry(fallbackEnemy, BattleAction.FromCard(Card.Fee(), 1, _player)));
            }
        }

        return actions;
    }

    private BattleAction? TryCreateScriptedEnemyAction(EnemyInstance enemy, IReadOnlyList<EnemyInstance> allies)
    {
        if (_context == null || enemy.Definition.AiProfile.ScriptedActions.Count == 0)
        {
            return null;
        }

        foreach (var rule in enemy.Definition.AiProfile.ScriptedActions)
        {
            if (_context.TurnCounter < rule.MinTurn || _context.TurnCounter > rule.MaxTurn)
            {
                continue;
            }

            if (!_enemyAi.IsCardAvailableThisTurn(enemy, enemy.Definition, rule.CardType))
            {
                _context.AddTriggerLog($"[EnemyAI/Scripted] {enemy.DisplayName} 计划使用 {BattleRules.GetCardName(rule.CardType)}，但当前出招栏不存在该牌，改用当前合法出牌。");
                return null;
            }

            var card = new Card(rule.CardType);
            var target = _enemyAi.SelectTarget(enemy, card, _player, allies);
            _context.AddTriggerLog($"[EnemyAI/Scripted] {enemy.DisplayName} 第{_context.TurnCounter}回合固定使用 {BattleRules.GetCardName(rule.CardType)} ×{rule.Count}");
            return BattleAction.FromCard(card, System.Math.Max(1, rule.Count), target);
        }

        return null;
    }

    private Card? TryGetDebugOverrideAction(EnemyInstance enemy)
    {
        if (!_battleDebugMode)
        {
            return null;
        }

        var slotIndex = GetDebugSlotIndex(enemy);
        if (slotIndex < 0 || slotIndex >= _debugEnemyNextActionOverrides.Length)
        {
            return null;
        }

        var overrideType = _debugEnemyNextActionOverrides[slotIndex];
        if (!overrideType.HasValue)
        {
            return null;
        }

        GD.Print("[Debug] Override Action");
        GD.Print($"Enemy={enemy.Name}");
        GD.Print($"Action={BattleRules.GetCardName(overrideType.Value)}");
        GD.Print("Source=DebugPanel");
        _context?.AddTriggerLog("[Debug]");
        _context?.AddTriggerLog($"Override Action Enemy={enemy.Name}");
        _context?.AddTriggerLog($"Action={BattleRules.GetCardName(overrideType.Value)}");
        _context?.AddTriggerLog("Source=DebugPanel");
        _debugEnemyNextActionOverrides[slotIndex] = null;
        _battleDebugEnemyActionPickers[slotIndex]?.Select(0);
        RefreshBattleDebugWindow();
        return new Card(overrideType.Value);
    }

    private void ClearCurrentActions()
    {
        if (_context == null)
        {
            return;
        }

        _context.PlayerAction = null;
        _context.ClearEnemyActions();
    }

    private void BuildEncounterForCurrentStage()
    {
        if (GameManager.HasActiveSpecialBattle)
        {
            var specialEnemyIds = GameManager.GetActiveSpecialBattleEnemyIds();
            GD.Print($"[Encounter/Special] {GameManager.ActiveSpecialBattleId}: enemyIds={string.Join(",", specialEnemyIds)}");
            foreach (var enemyId in specialEnemyIds)
            {
                var enemy = EnemyFactory.CreateEnemy(enemyId);
                if (enemy != null)
                {
                    _encounter.Enemies.Add(enemy);
                    LogEnemySpawn(enemy);
                }
                else
                {
                    GD.PrintErr($"[Encounter/Special] Failed to create enemy: {enemyId}");
                }
            }

            GD.Print($"[Encounter/Special] BattleEncounter.Enemies.Count={_encounter.Enemies.Count}");
            InitSharedHealthPoolIfNeeded();
            return;
        }

        var currentNode = GameManager.GetNode(GameManager.CurrentNodeId);
        // 电量系统·继续探索：动态生成的节点用 StageIdOverride 指定遭遇池（原有固定节点
        // 仍然走 Id→StageId 的既有 switch，行为不变），所以这里改用节点重载而不是字符串重载。
        var stageId = currentNode != null
            ? StageDatabase.GetStageIdForNode(currentNode)
            : StageDatabase.GetStageIdForNode(GameManager.CurrentNodeId);
        // 魏·双线征伐 的额外Boss节点（Id 形如 "boss_extra_ch1"）虽然 Type 也是 Boss，
        // 但不能走"本章主Boss锁定敌人组合"这条缓存路径——GetOrCreateChapterBossEncounterEnemyIds
        // 只按章节号缓存，不区分节点id，如果额外Boss也走这条路径，会直接复用主Boss已经锁定的
        // 敌人组合，导致额外Boss和主Boss打的是同一批敌人，StageIdOverride 指向的独立遭遇池
        // 形同虚设。额外Boss落入下面的 else 分支，走正常的 StageDatabase.RollEncounterForNode。
        var isMainBossNode = currentNode?.Type == MapNodeType.Boss
            && !(currentNode?.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal) ?? false);
        EncounterEntry? encounterEntry;
        if (isMainBossNode)
        {
            var plannedBossEnemyIds = GameManager.GetOrCreateChapterBossEncounterEnemyIds(GameManager.CurrentChapter);
            encounterEntry = plannedBossEnemyIds.Count > 0
                ? new EncounterEntry
                {
                    Weight = 1,
                    EnemyIds = new List<string>(plannedBossEnemyIds)
                }
                : null;
        }
        else if (currentNode?.FixedEncounterEnemyIds is { Count: > 0 } fixedEncounterEnemyIds)
        {
            // 第四章 4-1/4-3 等"编队已在建图时锁定"的节点：直接读取缓存，不再重新随机。
            // 其它章节节点这个字段始终为空，完全不受影响，仍走下面的现场随机分支。
            encounterEntry = new EncounterEntry
            {
                Weight = 1,
                EnemyIds = new List<string>(fixedEncounterEnemyIds)
            };
        }
        else
        {
            encounterEntry = currentNode != null
                ? StageDatabase.RollEncounterForNode(currentNode)
                : StageDatabase.RollEncounterForNode(GameManager.CurrentNodeId);

            // 初始事件⑯：第一场战斗普通敌人→随机精英遭遇
            if (GameManager.InitialEventFirstBattleElite
                && GameManager.ClearedBattleCount == 0
                && currentNode?.Type == MapNodeType.Battle)
            {
                GameManager.ConsumeInitialEventFirstBattleElite();
                var eliteEntry = StageDatabase.RollEncounterForNode("battle_5");
                if (eliteEntry != null)
                    encounterEntry = eliteEntry;
            }
        }

        if (encounterEntry == null)
        {
            GD.Print($"[Encounter] No encounter. node={GameManager.CurrentNodeId}, stage={stageId}");
            return;
        }

        GD.Print($"[Encounter] Rolled. node={GameManager.CurrentNodeId}, stage={stageId}, enemyIds={string.Join(",", encounterEntry.EnemyIds)}, requested={encounterEntry.EnemyIds.Count}");
        foreach (var enemyId in encounterEntry.EnemyIds)
        {
            var enemy = EnemyFactory.CreateEnemy(enemyId);
            if (enemy != null)
            {
                _encounter.Enemies.Add(enemy);
                LogEnemySpawn(enemy);
            }
            else
            {
                GD.PrintErr($"[Encounter] Failed to create enemy: {enemyId}");
            }
        }

        GD.Print($"[Encounter] BattleEncounter.Enemies.Count={_encounter.Enemies.Count}");
        InitSharedHealthPoolIfNeeded();
    }

    private void InitSharedHealthPoolIfNeeded()
    {
        var sharedPoolMembers = new System.Collections.Generic.List<EnemyInstance>();
        foreach (var e in _encounter.Enemies)
        {
            if (e.Definition.UseSharedHealthPool)
                sharedPoolMembers.Add(e);
        }

        if (sharedPoolMembers.Count == 0) return;

        var pool = new SharedHealthPool(sharedPoolMembers[0].MaxHealth);
        foreach (var m in sharedPoolMembers)
            m.SetSharedPool(pool);

        GD.Print($"[Encounter] SharedHealthPool initialized: MaxHP={pool.MaxHP}, members={sharedPoolMembers.Count}");
    }

    private BattleAction CreateActionForCard(Card card, int count)
    {
        return BattleAction.FromCard(card, count, ResolveTargetForCard(card));
    }

    private void LogPlayerSelection()
    {
        if (_stackingCard == null)
        {
            return;
        }

        _context?.AddTriggerLog($"玩家选择：{BattleAction.FromCard(_stackingCard, _stackCount).DisplayName}");
    }

    private BattleUnit? ResolveTargetForCard(Card card)
    {
        return card.TargetType switch
        {
            CardTargetType.SelfTarget => _player,
            CardTargetType.Targeted => GetResolvedSelectedTarget(),
            _ => null
        };
    }

    private BattleUnit? GetResolvedSelectedTarget()
    {
        if (_selectedTarget == null || _selectedTarget.IsDead || _selectedTarget.Team != BattleTeam.Enemy)
        {
            _selectedTarget = GetDefaultEnemyTarget();
        }

        return _selectedTarget;
    }

    private BattleUnit? GetDefaultEnemyTarget()
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

    private static string GetTargetDisplayName(BattleUnit? unit)
    {
        return unit?.Name ?? Localization.Get("battle.state.no_target");
    }

    private static bool IsInstantCard(CardType type)
    {
        // 影袭杀（黄月英·如影随行专属）和费/闪一样没有值得等待的决策意义：
        // 0费、通常是当下唯一能打的牌，不需要展示"是否继续叠加"的确认读条，
        // 直接结算能让流程更顺畅。这里直接归入 IsInstantCard，保证无论当时还有
        // 没有其它合法动作，影袭杀都不会触发确认读条（不依赖 CountLegalActions
        // 的"只剩一个合法动作"这条间接判断，避免出现例外情况仍然弹出读条）。
        //
        // 影袭（YingXiActivate，进入影袭状态的激活卡）同理：这张牌本身不可叠加
        // （不在 BattleAction.CanStackType 里），进入影袭状态后 GetAvailableActionCards
        // 会立即把它从出牌区移除（影袭状态下只剩影袭杀/潜伏），天然不可能重复打出；
        // 唯一的决策就是"要不要现在进入影袭"，同样不需要等待条，直接归入 IsInstantCard。
        return type is CardType.Fee or CardType.Dodge or CardType.ShadowKill or CardType.YingXiActivate;
    }

    private static bool CanStack(Card card)
    {
        return BattleAction.CanStackType(card.Type);
    }

    private static int GetEnemyActionCount(Player enemy, Card card, int freeExtra)
    {
        if (!BattleAction.CanStackType(card.Type))
        {
            return 1;
        }

        if (card.Type == CardType.Wine)
        {
            var limit = BattleRules.GetWinePlayLimitPerTurn(enemy);
            return limit == int.MaxValue ? 1 : limit;
        }

        var effectiveCost = BattleRules.GetActionCost(enemy, BattleAction.FromCard(card, 1));
        if (effectiveCost <= 0)
        {
            return System.Math.Max(1, 1 + freeExtra);
        }

        return System.Math.Max(1, (int)System.Math.Floor(enemy.CurrentMana / effectiveCost) + freeExtra);
    }

    private bool CanAffordStack(Card card, int count)
    {
        return new Card(card.Type).Cost > 0 && CanAffordAction(card, count);
    }

    private bool CanAffordAction(Card card, int count)
    {
        if (_player.GuanxingPhase == GuanxingPhase.Repeating
            && _player.GuanxingRecordedCardType.HasValue
            && card.Type == _player.GuanxingRecordedCardType.Value)
        {
            return true;
        }

        return BattleRules.CanAffordActionCost(_player, GetPreviewCost(card, count));
    }

    private double GetPreviewCost(Card card, int count)
    {
        var cost = BattleRules.GetActionCost(_player, BattleAction.FromCard(card, count, ResolveTargetForCard(card)));
        if (card.Type is CardType.Kill or CardType.PoisonKill && _player.LianyingFreeKillAvailable && count > 0)
        {
            cost = System.Math.Max(0, cost - BattleRules.GetCardCost(_player, CardType.Kill));
        }

        return cost;
    }

    private bool ShouldConfirmImmediately(Card card, int count)
    {
        if (_phase != BattlePhase.BattlePrePhase)
        {
            return true;
        }

        // 可叠加牌只在还能支付下一张同类牌时才保留确认读条。这样不会出现
        // 剩余费用不足、却仍提示玩家继续叠加同一张牌的无效等待。
        if (CanStack(card) && !CanAffordStack(card, count + 1))
        {
            return true;
        }

        return CountLegalActions() <= 1;
    }

    private int CountLegalActions()
    {
        var count = 0;
        foreach (var card in GetAvailableActionCards())
        {
            if (card.TargetType == CardTargetType.Targeted && GetResolvedSelectedTarget() == null)
            {
                continue;
            }

            if (CanAffordAction(card, 1))
            {
                count += 1;
            }
        }

        return count;
    }

    private List<Card> GetAvailableActionCards()
    {
        // 连弩模式：仅限普通杀（用于 CountLegalActions 计算）。
        if (_zhugeMode)
        {
            return new List<Card> { Card.Kill() };
        }

        // 观星重复：只有锁定的记录牌可用。
        if (_player.GuanxingPhase == GuanxingPhase.Repeating && _player.GuanxingRecordedCardType.HasValue)
        {
            return new List<Card> { new Card(_player.GuanxingRecordedCardType.Value) };
        }

        // 冰冻：只能出费。
        if (_player.IsFrozen)
        {
            return new List<Card> { Card.Fee() };
        }

        // 影袭状态：出牌区仅限影袭杀（未出招时）和潜伏。
        if (_player.InShadowState)
        {
            var shadowCards = new List<Card>();
            if (!_player.ShadowSlashUsed)
                shadowCards.Add(Card.ShadowKill());
            shadowCards.Add(Card.ShadowLurk());
            return shadowCards;
        }

        var cards = new List<Card>();

        void AddIfAvailable(Card c)
        {
            if (GameManager.HasPlayerCardType(c.Type))
                cards.Add(c);
        }

        void AddGranted(Card c, bool granted)
        {
            if (granted && !GameManager.IsPlayerCardTypeRemoved(c.Type))
                cards.Add(c);
        }

        AddIfAvailable(Card.Fee());
        AddIfAvailable(Card.Dodge());
        if (BattleRules.UsesTrueGuDingDaoActionSet(_player))
        {
            AddGranted(Card.FireThunderKill(), true);
        }
        else
        {
            if (GameManager.HasEquipment(EquipmentIds.PlagueStaff)
                || GameManager.HasPlayerCardType(CardType.PoisonKill))
                AddGranted(Card.PoisonKill(), true);
            else
                AddIfAvailable(Card.Kill());
            // 周瑜【英姿】：专属锦囊【火攻】取代基础【火杀】；其它来源（例如初始事件）
            // 获得的火攻则与火杀并存，不能因没有英姿而被出牌栏漏掉。
            if (_player.HasSkill(SkillIds.YingZi))
                AddGranted(Card.FireAttack(), true);
            else
            {
                AddIfAvailable(Card.FireKill());
                AddIfAvailable(Card.FireAttack());
            }
            AddIfAvailable(Card.ThunderKill());
            AddIfAvailable(Card.FireThunderKill());
        }

        AddGranted(Card.SureKill(), _player.HasSkill(SkillIds.Bizhong));
        AddGranted(Card.IceKill(), _player.HasSkill(SkillIds.JiHan)
            || GameManager.HasPlayerCardType(CardType.IceKill));
        AddGranted(Card.CelestialImpact(), _player.HasSkill(SkillIds.CelestialImpact)
            || GameManager.HasEquipment(EquipmentIds.MoonGem));
        AddGranted(Card.NanmanInvasion(), _player.HasSkill(SkillIds.Manzu)
            || GameManager.HasPlayerCardType(CardType.NanmanInvasion));
        AddGranted(Card.Tuxi(), _player.HasSkill(SkillIds.Tuxi));
        AddGranted(Card.ArrowBarrage(), GameManager.HasPlayerCardType(CardType.ArrowBarrage));
        // 庞统默认携带【铁索连环】；初始事件㉒也可把该锦囊加入任意角色的出牌栏。
        AddGranted(Card.IronChain(), _player.HasSkill(SkillIds.PangTongIronChain)
            || GameManager.HasPlayerCardType(CardType.IronChain));

        AddIfAvailable(Card.Peach());
        AddIfAvailable(Card.Wine());
        AddIfAvailable(Card.Steal());
        AddIfAvailable(Card.Unassailable());

        AddGranted(Card.Guanxing(), _player.HasSkill(SkillIds.Guanxing)
            && _player.GuanxingPhase == GuanxingPhase.None);

        // 影袭：非影袭状态时，提供影袭激活卡。
        AddGranted(Card.YingXiActivate(), _player.HasSkill(SkillIds.YingXi));
        // 魅惑每场战斗最多使用3次，次数耗尽后从出招栏移除（不是费用不足禁用，而是直接不再提供该选项）。
        AddGranted(Card.Meihuo(), _player.HasSkill(SkillIds.Meihuo) && _player.MeihuoUsesRemaining > 0);

        return cards;
    }

    private bool CanContinueEditingSelection()
    {
        return _phase == BattlePhase.BattlePrePhase && !_inputLocked;
    }

    private async Task PlayActionFeedback(CardUI sourceUi)
    {
        var startScale = sourceUi.Scale;
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sourceUi, "scale", new Vector2(1.08f, 1.08f), 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sourceUi, "self_modulate", new Color(1.0f, 0.92f, 0.46f), 0.10);
        tween.Chain().TweenProperty(sourceUi, "scale", startScale, 0.16)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sourceUi, "self_modulate", Colors.White, 0.16);

        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private bool CheckBattleOver()
    {
        if (_context?.GameOver != true)
        {
            return false;
        }

        RestoreBattleScopedMaxHealth();
        _skillTriggerToastQueue?.Clear();

        SetPhase(BattlePhase.GameOver);
        _inputLocked = true;
        _pendingBattleResult = _context.IsPlayerVictory;
        var displayText = _pendingBattleResult == true
            ? Localization.Get("battle.gameover.victory")
            : Localization.Get("battle.gameover.defeat");
        SetActionText(displayText);
        var subtitleText = string.Empty;
        if (_pendingBattleResult == true && GameManager.CurrentStage >= 0)
        {
            subtitleText = Localization.GetFmt("battle.gameover.stage_fmt", GameManager.CurrentStage + 1);
        }

        _battleLogManager.AddSystem(_turnNumber, _pendingBattleResult == true ? "battlelog.victory" : "battlelog.defeat");
        _battleLogManager.EndBattle(_pendingBattleResult == true ? "Victory" : "Defeat");

        // 图鉴：战斗场次/胜场/Boss击败/"被谁击败"都在这里统一记录（CodexService内部已经
        // 按调试战斗/教学战斗过滤，这里不需要重复判断 _battleDebugMode）。
        var codexCharacterId = GameManager.CurrentCharacterId;
        if (!string.IsNullOrEmpty(codexCharacterId))
        {
            CodexService.RecordCharacterBattleResult(codexCharacterId, _pendingBattleResult == true);
            if (_pendingBattleResult == true)
            {
                foreach (var defeatedEnemy in _encounter.Enemies)
                {
                    if (defeatedEnemy.Definition.Type == EnemyType.Boss && defeatedEnemy.IsDead)
                    {
                        CodexService.RecordEnemyDefeatedByBoss(defeatedEnemy.Definition.Id, codexCharacterId, defeatedEnemy.Definition.Type);
                        CodexService.RecordCharacterBossDefeated(codexCharacterId);
                    }
                }
            }
            else
            {
                foreach (var survivingEnemy in _encounter.Enemies)
                {
                    if (!survivingEnemy.IsDead)
                    {
                        CodexService.RecordPlayerDefeatedBy(survivingEnemy.Definition.Id);
                    }
                }
            }
        }

        if (_pendingBattleResult == true)
        {
            // 战场崩坏胜利（含互伤胜利）：这场战斗只要真的进入过战场崩坏阶段
            // （回合数达到 BattlefieldCollapseEffect.TriggerTurn），战后血量就不再
            // 走正常的+10/满血回复，改用战斗结束瞬间玩家的真实血量决定；互伤胜利
            // 最低保留1HP，其余情况直接按当前正生命退出。必须在下面 SettleBattleVictoryGold 读取
            // _player.Health 之前调用，两处用的是同一个"战斗结束瞬间"的快照。
            if (_turnNumber >= BattlefieldCollapseEffect.TriggerTurn)
            {
                GameManager.ArmCollapseVictoryHealthOverride(_player.Health);
            }

            // 魏·战后清算读取本场最终实际对敌伤害；吴·战后分红仍读取战斗结束瞬间生命。
            FactionFateManager.SettleBattleVictoryGold(_player.Health, _context.PlayerDamageDealtThisBattle);

            // 结算本场战斗的金币和概率掉落装备；写入 GameManager，由 MainFlow.ShowReward 消费。
            // 同时把这里已经算好的金币/装备记进 Battle Log——只是把这份既有结果也写一份日志，
            // 不重新计算、不生成第二份奖励数据。
            var totalGold = 0;
            var loggedEquipmentIds = new List<string>();
            foreach (var enemy in _encounter.Enemies)
            {
                if (enemy.Definition.Reward.GrantsGold)
                {
                    totalGold += enemy.Definition.Reward.GoldOverride
                        ?? EnemyRewardConfig.RollGold(enemy.Definition.Type, _dropRandom);
                }

                foreach (var drop in enemy.Definition.Reward.EquipmentDrops)
                {
                    if ((float)_dropRandom.NextDouble() < drop.Probability)
                    {
                        GameManager.AddPendingBattleDrop(drop.EquipmentId);
                        loggedEquipmentIds.Add(drop.EquipmentId);
                        GD.Print($"[BattleManager] Drop rolled: {drop.EquipmentId} from {enemy.Name}");
                    }
                }

                var chipProb = enemy.Definition.Reward.KnowledgeChipDropProbability;
                if (chipProb > 0 && (float)_dropRandom.NextDouble() < chipProb)
                {
                    GameManager.IncrementKnowledgeChipCount();
                    GD.Print($"[BattleManager] Knowledge chip dropped from {enemy.Name}");
                }

                for (var dc = 0; dc < enemy.Definition.Reward.DefenseChipDropCount; dc++)
                {
                    GameManager.IncrementDefenseChipCount();
                    GD.Print($"[BattleManager] Defense chip dropped from {enemy.Name}");
                }

                foreach (var equipId in enemy.Definition.Reward.EquipmentRewards)
                {
                    GameManager.AddPendingBattleDrop(equipId);
                    loggedEquipmentIds.Add(equipId);
                    GD.Print($"[BattleManager] Guaranteed equipment drop: {equipId} from {enemy.Name}");
                }

                var pool = enemy.Definition.Reward.RandomEquipmentPool;
                if (pool.Count > 0)
                {
                    var picked = pool[_dropRandom.Next(pool.Count)];
                    GameManager.AddPendingBattleDrop(picked);
                    loggedEquipmentIds.Add(picked);
                    GD.Print($"[BattleManager] Random pool drop: {picked} from {enemy.Name}");
                }
            }
            if (totalGold > 0)
            {
                GameManager.SetPendingBattleGold(totalGold);
                _battleLogManager.AddSystem(_turnNumber, "battlelog.gold_reward", totalGold.ToString());
            }

            foreach (var equipmentId in loggedEquipmentIds)
            {
                var equipmentName = EquipmentDatabase.GetEquipment(equipmentId) is { } definition
                    ? Localization.GetName(definition)
                    : equipmentId;
                _battleLogManager.AddSystem(_turnNumber, "battlelog.equipment_reward", equipmentName);
            }
        }

        // 最后1点粮草仍可用于本次战败重试；只有已经为0才提示本局结束。
        if (_pendingBattleResult == false && GameManager.Forage <= 0)
        {
            displayText = "Game Over";
        }

        RebuildBattleLogView();
        UpdateRecentReport();

        ShowGameOverOverlay(displayText, subtitleText);
        RefreshUi();
        RenderActionCards();
        return true;
    }

    private void RestoreBattleScopedMaxHealth()
    {
        HunZiTriggerEffect.RestoreBattleMaxHealth(_player);
    }

    private bool CanChooseAction(Card card)
    {
        if (_context?.GameOver == true)
        {
            return false;
        }

        if (card.TargetType == CardTargetType.Targeted && GetResolvedSelectedTarget() == null)
        {
            return false;
        }

        // 连弩模式：只有普通杀可选。
        if (_zhugeMode)
        {
            return !_inputLocked && _phase == BattlePhase.BattlePrePhase && card.Type is CardType.Kill or CardType.PoisonKill;
        }

        // 观星重复：只有被记录的牌可用（费用免除，无需检查资源）。
        if (_player.GuanxingPhase == GuanxingPhase.Repeating)
        {
            if (!_player.GuanxingRecordedCardType.HasValue || card.Type != _player.GuanxingRecordedCardType.Value)
            {
                return false;
            }

            return !_inputLocked && _phase == BattlePhase.BattlePrePhase;
        }

        if (_stackingCard != null)
        {
            return !_inputLocked
                && _phase == BattlePhase.BattlePrePhase
                && ((card.Type == _stackingCard.Type && CanStack(card) && CanAffordStack(card, _stackCount + 1))
                    || CanAffordAction(card, 1));
        }

        return !_inputLocked && _phase == BattlePhase.BattlePrePhase && CanAffordAction(card, 1);
    }
}
