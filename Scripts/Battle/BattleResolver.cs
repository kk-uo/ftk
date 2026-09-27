//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/BattleResolver.cs
//
// 模块：Battle System
//
// 职责：
// 1. 承载战斗流程、阶段推进与战斗单位协作相关代码。
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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Battle System 的公开类：BattlePhaseResolutionEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattlePhaseResolutionEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
	public EffectPriority Priority => EffectPriority.Mid;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		if (context.PlayerAction == null || context.EnemyActions.Count == 0)
		{
			return;
		}

		// 图鉴：玩家已确认提交进入正式结算的行动，就是"使用一次"的唯一判定点——
		// 早于UI点击/悬停/取消选择/预估伤害的阶段都不会走到这里。
		CodexService.RecordCardUsed(context.PlayerAction.Type, context.PlayerAction.Count);

		// ======================================================
		// 战斗结算顺序
		// ======================================================
		// 同一回合内玩家和敌人的行动需要同时揭示，但资源、护盾、攻击、
		// 治疗和偷取费用之间存在严格依赖。这里集中编排顺序，具体效果
		// 仍交给 BattleRules / Trigger / Damage 系统处理。
		//
		// 设计原因：
		// - 避免每张卡牌各自决定结算时机，造成顺手牵羊、无懈可击、桃盾等规则互相穿透。
		// - 保留统一入口，便于之后加入新的 BattlePhase 或 Reaction 机制。
		// - 任何伤害最终都走 DamageEvent，保证装备、技能、Buff 能统一监听。
		var playerManaBeforeActions = context.Player.CurrentMana;
		var enemyManaBeforeActions = new Dictionary<string, double>();
		foreach (var enemyEntry in context.EnemyActions)
		{
			enemyManaBeforeActions[BattleContext.GetUnitStateKey(enemyEntry.Enemy)] = enemyEntry.Enemy.CurrentMana;
		}

		// 洛神：只要本回合使用的是【费】牌就记录为出费；普通攻击等其它牌不会阻止
		// 下回合减半。减半数值在下回合开始时读取实际持有费用，而不是旧快照。
		if (context.Player.HasSkill(SkillIds.Luoshen))
		{
			context.Player.LuoshenOnActionStarted(playerManaBeforeActions);
			var pa = context.PlayerAction;
			if (pa.IsFee || (context.Player.HasSkill(SkillIds.Luoyi) && pa.IsAttack))
			{
				context.Player.LuoshenOnFeeUsed();
			}
		}

		PayActionCost(context, context.Player, context.PlayerAction);
		foreach (var enemyEntry in context.EnemyActions)
		{
			PayActionCost(context, enemyEntry.Enemy, enemyEntry.Action);
		}
		PrepareDefenseLayers(context);
		ResolveUnassailable(context);
		var pendingTuxiSteals = new List<PendingTuxiSteal>();
		ResolveActions(context, pendingTuxiSteals);
		ApplyResourceAndHealing(context, context.Player, context.PlayerAction);
		foreach (var enemyEntry in context.EnemyActions)
		{
			ApplyResourceAndHealing(context, enemyEntry.Enemy, enemyEntry.Action);
		}
		ResolvePendingTuxiSteals(context, pendingTuxiSteals);
		ApplyWineStatus(context, context.Player, context.PlayerAction);
		foreach (var enemyEntry in context.EnemyActions)
		{
			ApplyWineStatus(context, enemyEntry.Enemy, enemyEntry.Action);
		}
		ApplyStealActions(context, playerManaBeforeActions, enemyManaBeforeActions);
		ApplyPendingRuYingSuiXingResourceReset(context);
		AddUnusedActionNotes(context);

		if (!context.RoundResult.HasLines)
		{
			context.RoundResult.AddLine("无人受伤。");
		}
	}

	private static void ApplyPendingRuYingSuiXingResourceReset(BattleContext context)
	{
		if (!context.RuYingSuiXingResourceResetPending || !context.Player.HasSkill(SkillIds.RuYingSuiXing))
		{
			return;
		}

		context.RuYingSuiXingResourceResetPending = false;
		context.Player.SetManaToOne();

		var enemies = context.Encounter?.Enemies;
		if (enemies != null)
		{
			foreach (var enemy in enemies)
			{
				if (!enemy.IsDead)
				{
					enemy.SetManaToOne();
				}
			}
		}

		context.RoundResult.AddLine("如影随行：有人受伤，全场费用重置为1。");
		context.AddTriggerLog("[如影随行]");
		context.AddTriggerLog("战斗动作结算后：全场费用设为1。");
		context.ReportPlayerCharacterSkillTriggered(
			context.Player, SkillIds.RuYingSuiXing, TriggerTiming.OnDamageTaken, EffectPriority.Low, "mana_reset_applied");
	}

	private static void PayActionCost(BattleContext context, Player player, BattleAction action)
	{
		// 观星重复：强制重复回合中费用全免。
		if (player == context.Player
			&& player.GuanxingPhase == GuanxingPhase.Repeating
			&& player.GuanxingRecordedCardType.HasValue
			&& action.Type == player.GuanxingRecordedCardType.Value)
		{
			context.RoundResult.AddLine($"观星重复：{BattleRules.GetCardName(action.Type)}费用免除。");
			return;
		}

		var cost = BattleRules.GetActionCost(player, action);

		if (FactionFateManager.CanUseShuFirstTrickFree(player, action.Type))
		{
			FactionFateManager.ConsumeShuFirstTrickFree(player);
			context.RoundResult.AddLine($"蜀·奇策先发：{BattleRules.GetCardName(action.Type)}本场战斗首次使用免费。");
			context.AddTriggerLog("[FactionFate/ShuFirstTrickFree] 首张锦囊费用免除。");
		}

		// 酒池（董卓专属）：消耗本回合免费酒机会。
		if (action.IsWine && player.HasSkill(SkillIds.JiuChi) && player.FreeWineUsesRemaining > 0)
		{
			var freeCount = Math.Min(action.Count, player.FreeWineUsesRemaining);
			for (var i = 0; i < freeCount; i++)
				player.ConsumeFreeWineUse();
			context.RoundResult.AddLine($"【酒池】：{freeCount}张酒免费（剩余{player.FreeWineUsesRemaining}次）。");
			context.AddTriggerLog("[酒池]");
			context.AddTriggerLog($"酒池：消耗{freeCount}次，剩余{player.FreeWineUsesRemaining}次。");
			context.ReportPlayerCharacterSkillTriggered(
				player, SkillIds.JiuChi, TriggerTiming.OnBattlePhase, EffectPriority.High, "free_wine");
		}

		if (action.IsWine && player.Team == BattleTeam.Player
			&& BattleRules.GetCardCost(player, CardType.Wine) > 0)
		{
			var freeWineCount = Math.Min(
				action.Count,
				GameManager.GetRemainingEquipmentUses(EquipmentIds.WinePouch));
			for (var i = 0; i < freeWineCount; i++)
			{
				if (!GameManager.TryConsumeEquipmentUse(EquipmentIds.WinePouch))
				{
					break;
				}
			}

			if (freeWineCount > 0)
			{
				context.RoundResult.AddLine($"酒囊生效：{player.DisplayName}本局第一张酒费用变为0。");
				context.AddTriggerLog("[Equipment]");
				context.AddTriggerLog("酒囊：本局第一张酒费用变为0。");
			}
		}

		if (action.Type is CardType.Kill or CardType.PoisonKill && player.LianyingFreeKillAvailable)
		{
			cost = Math.Max(0, cost - BattleRules.GetCardCost(player, CardType.Kill));
			player.ConsumeLianyingFreeKill();
			context.RoundResult.AddLine($"{player.DisplayName}连营生效：第一张普通杀免费。");
			context.AddTriggerLog("[连营]");
			context.AddTriggerLog("Trigger: OnBattlePhase");
			context.AddTriggerLog("Priority: High");
			context.AddTriggerLog($"{player.DisplayName}免费使用一张普通杀。");
		}

		// 白马：每场战斗中第一次打出普通杀费用变为0（仅影响玩家方）。
		if (action.Type is CardType.Kill or CardType.PoisonKill
			&& player == context.Player
			&& player.WhiteHorseFreeKillAvailable
			&& GameManager.HasEquipment(EquipmentIds.WhiteHorse))
		{
			cost = Math.Max(0, cost - BattleRules.GetCardCost(player, CardType.Kill));
			player.ConsumeWhiteHorseFreeKill();
			context.RoundResult.AddLine($"白马触发：{player.DisplayName}本场战斗第一张普通杀费用变为0。");
			context.AddTriggerLog("[Equipment]");
			context.AddTriggerLog("白马：本场战斗第一张普通杀免费。");
		}

		// 影袭（黄月英专属）：每场战斗第一次激活影袭费用为0。
		if (action.Type == CardType.YingXiActivate && player.HasSkill(SkillIds.YingXi) && player.YingXiFirstUseFreeAvailable)
		{
			cost = 0;
			player.ConsumeYingXiFirstUseFree();
			context.RoundResult.AddLine($"{player.DisplayName}影袭触发：本场战斗第一次激活影袭费用为0。");
			context.AddTriggerLog("[影袭]");
			context.AddTriggerLog("影袭：本场战斗第一次激活免费。");
		}

		if (BattleRules.CanUseZhuaHuangFreeAttack(player, action.Type) && action.Count > 0)
		{
			if (player == context.Player)
			{
				player.ConsumeZhuaHuangFreeAttack();
			}
			else if (player is EnemyInstance enemy)
			{
				enemy.RuntimeStates["zhua_huang_free_attack_used"] = true;
			}

			context.RoundResult.AddLine($"爪黄飞电触发：{player.DisplayName}本场战斗第一张需要消耗费用的攻击牌不消耗费用。");
			context.AddTriggerLog("[Equipment/爪黄飞电]");
			context.AddTriggerLog($"{player.DisplayName}使用{BattleRules.GetCardName(action.Type)}，本次费用视为0。");
		}

		BattleRules.PayManaAndRaiseResourceChanged(context, player, cost, true);
	}

	private static void PrepareDefenseLayers(BattleContext context)
	{
		var playerAction = context.PlayerAction;
		if (playerAction == null) return;

		// 必中杀等价视为同时拥有闪 + 无懈可击，无需额外打出。
		// 裸衣：攻击牌视为出费，必中杀不触发任何防御。
		var luoyiPlayer = context.Player.HasSkill(SkillIds.Luoyi);
		var playerHasSureKill = !luoyiPlayer && context.PlayerAction?.Type == CardType.SureKill;
		context.PlayerDodgeLayers = context.PlayerAction?.IsDodge == true ? 1 : 0;
		context.PlayerDodgeDefenseActive = context.PlayerAction?.IsDodge == true || playerHasSureKill;
		context.PlayerCounterDefenseActive = context.PlayerAction?.IsUnassailable == true || playerHasSureKill;
		var playerPeachEffect = BattleRules.ShouldApplyPeachEffect(context.Player, playerAction.Type);
		if (playerPeachEffect)
		{
			context.PlayerPeachShieldLayers += playerAction.Count;
			context.PlayerPeachHealGranted = playerAction.Count;
			// 鼠王事件：累计3桃触发和平解决
			if (!context.GameOver && GameManager.ActiveSpecialBattleId == "rat_king_event")
			{
				GameManager.IncrementRatKingEventPeachCount();
				if (GameManager.RatKingEventPeachCount >= 3)
				{
					GameManager.SetRatKingEventPeacefulResolved();
					context.GameOver = true;
					context.Outcome = BattleOutcome.Victory;
					context.GameOverText = Localization.Get("battle.gameover.victory");
					context.AddTriggerLog("[RatKingEvent] 3桃和平解决 → 战斗结束");
				}
			}
		}
		var playerWineEffect = BattleRules.ShouldApplyWineEffect(context.Player, playerAction.Type);
		if (playerWineEffect)
		{
			var wineCount = playerAction.Count;
			context.PlayerWineShieldLayers = wineCount;
			context.Player.QueueWinePower(wineCount);
			// 司敌：酒翻倍
			if (context.Player.RuntimeStates.TryGetValue("sidi_double_wine", out var sdw) && sdw is true)
			{
				context.Player.RuntimeStates.Remove("sidi_double_wine");
				context.Player.QueueWinePower(wineCount);
				context.AddTriggerLog("[Skill/SiDi] 双倍酒生效 → 额外QueueWinePower");
			}
		}
		foreach (var enemyEntry in context.EnemyActions)
		{
			var enemyHasSureKill = enemyEntry.Action.Type == CardType.SureKill;
			var enemyKey = BattleContext.GetUnitStateKey(enemyEntry.Enemy);
			context.EnemyDodgeLayers[enemyKey] = enemyEntry.Action.IsDodge ? 1 : 0;
			context.EnemyDodgeDefenseActive[enemyKey] = enemyEntry.Action.IsDodge || enemyHasSureKill;
			context.EnemyCounterDefenseActive[enemyKey] = enemyEntry.Action.IsUnassailable || enemyHasSureKill;
			if (enemyEntry.Action.IsPeach)
			{
				var prev = context.EnemyPeachShieldLayers.TryGetValue(enemyKey, out var p) ? p : 0;
				context.EnemyPeachShieldLayers[enemyKey] = prev + enemyEntry.Action.Count;
				context.EnemyPeachHealGranted[enemyKey] = enemyEntry.Action.Count;
			}
			// 友方定向酒：护盾与增伤均给目标而非出牌者。
			if (enemyEntry.Action.IsWine)
			{
				if (enemyEntry.Action.Target is EnemyInstance wineAllyTarget && wineAllyTarget != enemyEntry.Enemy)
				{
					var wineTargetKey = BattleContext.GetUnitStateKey(wineAllyTarget);
					var existing = context.EnemyWineShieldLayers.TryGetValue(wineTargetKey, out var prev) ? prev : 0;
					context.EnemyWineShieldLayers[wineTargetKey] = existing + enemyEntry.Action.Count;
					wineAllyTarget.QueueWinePower(enemyEntry.Action.Count);
				}
				else
				{
					context.EnemyWineShieldLayers[enemyKey] = enemyEntry.Action.Count;
					enemyEntry.Enemy.QueueWinePower(enemyEntry.Action.Count);
				}
			}
		}

		// 影袭激活回合立即进入无敌状态：提前至伤害结算前，确保当回合受到的伤害也被取消。
		if (context.PlayerAction?.Type == CardType.YingXiActivate && context.Player.HasSkill(SkillIds.YingXi))
		{
			context.Player.EnterShadowState();
		}
		foreach (var enemyEntry in context.EnemyActions)
		{
			if (enemyEntry.Action.Type == CardType.YingXiActivate && enemyEntry.Enemy.HasSkill(SkillIds.YingXi))
			{
				enemyEntry.Enemy.EnterShadowState();
			}
		}
	}

	private static void ResolveActions(BattleContext context, List<PendingTuxiSteal> pendingTuxiSteals)
	{
		var playerAction = context.PlayerAction!;
		var playerCancelled = context.PlayerActionCancelled;
		// 【裸衣】把玩家的攻击行动作为“出费”处理：攻击仍会造成伤害，
		// 但不再参与与敌方攻击或特殊攻击的相互反制。
		var luoyiActive = !playerCancelled && playerAction.IsAttack && context.Player.HasSkill(SkillIds.Luoyi);
		var targetEntry = context.GetTargetEnemyActionEntry();
		var enemyResolutionOrder = GetEnemyResolutionOrder(context);
		LogActionBindings(context, playerAction, targetEntry);
		AddAttackVsStealRelation(context, playerAction, playerCancelled, targetEntry);

		if (!playerCancelled && playerAction.IsArrowBarrage)
		{
			foreach (var enemyEntry in enemyResolutionOrder)
			{
				if (enemyEntry.Enemy.IsDead || enemyEntry.ActionCancelled)
				{
					continue;
				}

				ResolveArrowBarrage(context, context.Player, playerAction, enemyEntry.Enemy,
					luoyiActive ? BattleAction.FromCard(Card.Fee()) : enemyEntry.Action);
				if (luoyiActive)
				{
					ResolveEnemyActionIndependently(context, enemyEntry.Enemy, enemyEntry.Action, pendingTuxiSteals);
				}
			}
			return;
		}

		if (!playerCancelled && playerAction.IsCelestialImpact)
		{
			foreach (var enemyEntry in enemyResolutionOrder)
			{
				if (enemyEntry.Enemy.IsDead || enemyEntry.ActionCancelled)
				{
					continue;
				}

				ResolveCelestialImpact(context, context.Player, playerAction, enemyEntry.Enemy,
					luoyiActive ? BattleAction.FromCard(Card.Fee()) : enemyEntry.Action);
				if (luoyiActive)
				{
					ResolveEnemyActionIndependently(context, enemyEntry.Enemy, enemyEntry.Action, pendingTuxiSteals);
				}
			}
			return;
		}

		if (!playerCancelled && playerAction.UsesNanmanSettlement)
		{
			if (context.Player.HasSkill(SkillIds.Manzu))
			{
				context.ReportPlayerCharacterSkillTriggered(
					context.Player, SkillIds.Manzu, TriggerTiming.OnBattlePhase, variant: "card");
			}
			var tuxiTriggered = false;
			foreach (var enemyEntry in enemyResolutionOrder)
			{
				if (enemyEntry.Enemy.IsDead || enemyEntry.ActionCancelled)
				{
					continue;
				}

				var actualDamage = ResolveNanmanInvasion(
					context,
					context.Player,
					playerAction,
					enemyEntry.Enemy,
					luoyiActive ? BattleAction.FromCard(Card.Fee()) : enemyEntry.Action);
				if (playerAction.IsTuxi && actualDamage > 0)
				{
					tuxiTriggered = true;
					QueueTuxiSteal(pendingTuxiSteals, context.Player, enemyEntry.Enemy);
				}
				if (luoyiActive)
				{
					ResolveEnemyActionIndependently(context, enemyEntry.Enemy, enemyEntry.Action, pendingTuxiSteals);
				}
			}

			if (tuxiTriggered)
			{
				context.ReportPlayerCharacterSkillTriggered(
					context.Player, SkillIds.Tuxi, TriggerTiming.OnBattlePhase, variant: "steal");
			}
			return;
		}

		var playerAreaAttackResolved = false;
		if (!playerCancelled && TryResolveRustSpearAreaKill(context, playerAction, luoyiActive, pendingTuxiSteals))
		{
			playerAreaAttackResolved = true;
		}

		if (!playerAreaAttackResolved
			&& !playerCancelled
			&& !luoyiActive
			&& playerAction.IsAttack
			&& targetEntry != null
			&& !targetEntry.ActionCancelled
			&& targetEntry.Action.IsAttack)
		{
			// 攻击碰撞统一在下面的 enemyResolutionOrder 中结算。若在这里提前处理锁定目标，
			// 该目标的低伤害会先于其它敌人的高伤害消耗护盾，破坏“最高伤害优先”规则。
		}
		else if (!playerAreaAttackResolved
			&& !playerCancelled
			&& playerAction.IsAttack
			&& playerAction.Target is EnemyInstance targetedEnemy
			&& (luoyiActive
				|| (targetEntry?.Action.UsesNanmanSettlement != true
					&& targetEntry?.Action.IsArrowBarrage != true
					&& targetEntry?.Action.IsCelestialImpact != true)))
		{
			if (playerAction.Type == CardType.SureKill && context.Player.HasSkill(SkillIds.Bizhong))
			{
				context.ReportPlayerCharacterSkillTriggered(
					context.Player, SkillIds.Bizhong, TriggerTiming.OnBattlePhase, variant: "card");
			}
			// 当目标敌人使用南蛮入侵时，杀系牌作为响应处理（双方均X），不独立发动攻击。
			// 单体攻击牌：Count次均命中选中目标，不分散到其他敌人。
			for (var i = 0; i < playerAction.Count && !context.GameOver; i++)
			{
				DealAttackDamage(context, context.Player, targetedEnemy, playerAction.Type);
			}
		}

		foreach (var enemyEntry in enemyResolutionOrder)
		{
			if (enemyEntry.Enemy.IsDead || enemyEntry.ActionCancelled)
			{
				continue;
			}

			// 范围杀已在 TryResolveRustSpearAreaKill 中逐敌完成双方行动结算。
			// 这里必须整体跳过，否则敌人的攻击会在同一回合被执行第二次。
			if (playerAreaAttackResolved)
			{
				continue;
			}

			// 单体攻击的伤害仍只落到锁定目标，但亮出的攻击牌也必须参与其它敌人的
			// 攻击碰撞。此前非目标敌人的攻击直接走独立伤害，导致“玩家杀、左敌杀”
			// 没有互消。这里仅结算防御侧碰撞，禁止把玩家伤害扩散到非锁定敌人。
			if (!luoyiActive
				&& !playerCancelled
				&& playerAction.IsAttack
				&& enemyEntry.Action.IsAttack)
			{
				ResolveAttackVsAttack(
					context,
					enemyEntry.Enemy,
					playerAction,
					enemyEntry.Action,
					playerCanDamageEnemy: enemyEntry.Enemy == targetEntry?.Enemy);
				continue;
			}

			if (enemyEntry.Action.IsArrowBarrage)
			{
				ResolveArrowBarrage(context, enemyEntry.Enemy, enemyEntry.Action, context.Player, GetEffectivePlayerResponse(context, enemyEntry.Enemy));
				continue;
			}

			if (enemyEntry.Action.IsCelestialImpact)
			{
				ResolveCelestialImpact(context, enemyEntry.Enemy, enemyEntry.Action, context.Player, GetEffectivePlayerResponse(context, enemyEntry.Enemy));
				continue;
			}

			if (enemyEntry.Action.UsesNanmanSettlement)
			{
				var actualDamage = ResolveNanmanInvasion(
					context,
					enemyEntry.Enemy,
					enemyEntry.Action,
					context.Player,
					GetEffectivePlayerResponse(context, enemyEntry.Enemy));
				if (enemyEntry.Action.IsTuxi && actualDamage > 0)
				{
					QueueTuxiSteal(pendingTuxiSteals, enemyEntry.Enemy, context.Player);
				}
				continue;
			}

			if (enemyEntry.Action.Type == CardType.ZiBaoAttack)
			{
				ResolveZiBaoExplosion(context, enemyEntry.Enemy);
				continue;
			}

			if (enemyEntry.Action.Type == CardType.XueZhaiAttack)
			{
				ResolveXueZhaiNearDeathDamage(context, enemyEntry.Enemy);
				continue;
			}

			if (enemyEntry.Action.IsAttack)
			{
				ResolveAttackAgainstTarget(context, enemyEntry.Enemy, context.Player, enemyEntry.Action);
			}
		}
	}

	private static IReadOnlyList<EnemyActionEntry> GetEnemyResolutionOrder(BattleContext context)
	{
		// 护盾必须优先承受本批次中威胁最高的单次伤害来源。敌人列表本身仍保持战场/UI顺序，
		// 这里只创建结算副本；同伤害时保留原顺序，避免改变模型、状态栏和索敌绑定。
		return context.EnemyActions
			.Select((entry, index) => new
			{
				Entry = entry,
				Index = index,
				Priority = EstimateIncomingDamage(entry)
			})
			.OrderByDescending(item => item.Priority)
			.ThenBy(item => item.Index)
			.Select(item => item.Entry)
			.ToList();
	}

	private static int EstimateIncomingDamage(EnemyActionEntry entry)
	{
		var action = entry.Action;
		if (action.Type == CardType.ZiBaoAttack || action.Type == CardType.XueZhaiAttack)
		{
			return (int)(entry.Enemy.MaxHealth * 0.4);
		}

		if (!action.IsAttack
			&& !action.IsArrowBarrage
			&& !action.IsCelestialImpact
			&& !action.UsesNanmanSettlement)
		{
			return 0;
		}

		return BattleRules.GetCardBaseDamage(action.Type);
	}

	private static void AddAttackVsStealRelation(
		BattleContext context,
		BattleAction playerAction,
		bool playerCancelled,
		EnemyActionEntry? targetEntry)
	{
		if (targetEntry == null || targetEntry.ActionCancelled || playerCancelled)
		{
			return;
		}

		if (playerAction.IsAttack && targetEntry.Action.IsSteal)
		{
			context.RoundResult.AddRelation(
				$"{BattleRules.GetCardName(playerAction.Type)}克制{BattleRules.GetCardName(CardType.Steal)}");
		}
		else if (playerAction.IsSteal && targetEntry.Action.IsAttack)
		{
			context.RoundResult.AddRelation(
				$"{BattleRules.GetCardName(targetEntry.Action.Type)}克制{BattleRules.GetCardName(CardType.Steal)}");
		}
	}

	private static void ResolveZiBaoExplosion(BattleContext context, EnemyInstance enemy)
	{
		if (context.GameOver)
		{
			return;
		}

		var damage = (int)(enemy.MaxHealth * 0.4);
		context.RoundResult.AddLine($"{enemy.DisplayName}【自爆】：对玩家造成{damage}点伤害（仅无懈、桃盾、酒盾可抵挡）。");
		context.AddTriggerLog("[自爆]");
		context.AddTriggerLog($"自爆伤害 = MaxHP({enemy.MaxHealth}) × 40% = {damage}");

		context.DamageEvent = new DamageEvent(
			enemy,
			context.Player,
			CardType.ZiBaoAttack,
			damage,
			isDirectAttackDamage: true,
			onlyAllowCounterOrCardShields: true);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
		context.DamageEvent = null;

		if (context.GameOver)
		{
			return;
		}

		// 新版自爆只在首次濒死后发动一次。释放完毕后保留1点生命并恢复普通行动，
		// 不再沿用旧版“爆炸后永久死亡”的处理。
		enemy.RuntimeStates["zibao_pending"] = false;
		enemy.RuntimeStates["zibao_triggered"] = true;
		context.RoundResult.AddLine($"{enemy.DisplayName}释放自爆后仍保留1点生命。");
		context.AddTriggerLog($"{enemy.DisplayName} 自爆已释放：解除待发状态，后续恢复普通AI行动。");
	}

	private static void ResolveXueZhaiNearDeathDamage(BattleContext context, EnemyInstance enemy)
	{
		if (context.GameOver) return;

		var damage = (int)(enemy.MaxHealth * 0.4);
		context.RoundResult.AddLine($"{enemy.DisplayName}【血债血偿】：释放濒死之力，对玩家造成{damage}点真实伤害（无视护甲、无视闪避、无视无懈）！");
		context.AddTriggerLog("[血债血偿]");
		context.AddTriggerLog($"血债血偿伤害 = MaxHP({enemy.MaxHealth}) × 40% = {damage}");

		// 真实伤害：直接TakeDamage，不经过伤害链（无视护盾/闪避/无懈可击）。
		var healthBefore = context.Player.Health;
		context.Player.TakeDamage(damage);
		context.RecordDirectDamage(context.Player, damage, System.Math.Max(0, healthBefore - context.Player.Health), healthBefore, context.Player.Health,
			new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, enemy));
		context.RoundResult.AddLine($"玩家受到{damage}点真实伤害（生命剩余{System.Math.Max(0, context.Player.Health)}/{context.Player.MaxHealth}）。");
		context.AddTriggerLog($"血债血偿：玩家 -{damage} → {context.Player.Health}/{context.Player.MaxHealth}");

		if (context.Player.Health <= 0 && !context.Player.IsDead && !context.GameOver)
		{
			var savedDamage = context.DamageEvent;
			context.DamageEvent = new DamageEvent(enemy, context.Player, CardType.XueZhaiAttack, damage,
				origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, enemy));
			context.RaiseOnDying();
			context.DamageEvent = savedDamage;
		}

		if (context.GameOver) return;

		// 血债血偿后强制死亡（绕过桃/酒复活流程，永久死亡）。
		enemy.MarkDead();
		enemy.ClearStatuses();
		context.RoundResult.AddLine($"{enemy.DisplayName}血债血偿后死亡。");

		if (context.Encounter == null) return;

		var allDead = true;
		foreach (var e in context.Encounter.Enemies)
		{
			if (!e.IsDead)
			{
				allDead = false;
				break;
			}
		}

		if (allDead)
		{
			context.GameOver = true;
			context.Outcome = BattleOutcome.Victory;
			context.GameOverText = Localization.Get("battle.gameover.victory");
		}
	}

	private static bool TryResolveRustSpearAreaKill(
		BattleContext context,
		BattleAction playerAction,
		bool resolveIndependently,
		List<PendingTuxiSteal> pendingTuxiSteals)
	{
		var hasRustSpear = GameManager.HasEquipment(EquipmentIds.RustSpear);
		var hasHejinJian = GameManager.HasEquipment(EquipmentIds.HejinJian);

		if (playerAction.Type is not (CardType.Kill or CardType.PoisonKill)
			|| context.Encounter == null
			|| (!hasRustSpear && !hasHejinJian))
		{
			return false;
		}

		var sourceName = hasHejinJian ? "合金剑" : "锈剑";
		context.RoundResult.AddLine($"{sourceName}触发：普通杀对所有敌人生效。");
		context.AddTriggerLog($"[Equipment/{sourceName}]");
		context.AddTriggerLog($"{sourceName}：本次普通杀分别与每个存活敌人的当前出牌结算。");
		foreach (var enemyEntry in GetEnemyResolutionOrder(context))
		{
			if (enemyEntry.Enemy.IsDead)
			{
				continue;
			}

			ResolveAreaKillAgainstEnemy(context, playerAction, enemyEntry, resolveIndependently, pendingTuxiSteals);
		}

		return true;
	}

	private static void ResolveAreaKillAgainstEnemy(
		BattleContext context,
		BattleAction playerAction,
		EnemyActionEntry enemyEntry,
		bool resolveIndependently,
		List<PendingTuxiSteal> pendingTuxiSteals)
	{
		var enemy = enemyEntry.Enemy;
		var enemyAction = enemyEntry.Action;

		// 全体杀仍然是“分别对每个敌人出一张杀”，不是先无条件扣除所有敌人生命，
		// 再让所有敌方攻击额外命中玩家。每个敌人的实际出牌必须只结算一次。
		if (enemyEntry.ActionCancelled)
		{
			ResolveAttackAgainstTarget(context, context.Player, enemy, playerAction);
			return;
		}

		if (resolveIndependently)
		{
			ResolveAttackAgainstTarget(context, context.Player, enemy, playerAction);
			ResolveEnemyActionIndependently(context, enemy, enemyAction, pendingTuxiSteals);
			return;
		}

		if (enemyAction.IsAttack)
		{
			ResolveAttackVsAttack(context, enemy, playerAction, enemyAction);
			return;
		}

		if (enemyAction.IsArrowBarrage)
		{
			ResolveArrowBarrage(context, enemy, enemyAction, context.Player, playerAction);
			return;
		}

		if (enemyAction.IsCelestialImpact)
		{
			ResolveCelestialImpact(context, enemy, enemyAction, context.Player, playerAction);
			return;
		}

		if (enemyAction.UsesNanmanSettlement)
		{
			var actualDamage = ResolveNanmanInvasion(
				context,
				enemy,
				enemyAction,
				context.Player,
				playerAction);
			if (enemyAction.IsTuxi && actualDamage > 0)
			{
				QueueTuxiSteal(pendingTuxiSteals, enemy, context.Player);
			}
			return;
		}

		ResolveAttackAgainstTarget(context, context.Player, enemy, playerAction);

		// 自爆和血债血偿不是普通攻击牌；玩家的范围杀与其独立生效后，仍需执行该敌人的
		// 强制行动，但不能让后续主循环再执行第二次。
		if (enemyAction.Type == CardType.ZiBaoAttack)
		{
			ResolveZiBaoExplosion(context, enemy);
		}
		else if (enemyAction.Type == CardType.XueZhaiAttack)
		{
			ResolveXueZhaiNearDeathDamage(context, enemy);
		}
	}

	private static void ResolveEnemyActionIndependently(
		BattleContext context,
		EnemyInstance enemy,
		BattleAction enemyAction,
		List<PendingTuxiSteal> pendingTuxiSteals)
	{
		var neutralResponse = BattleAction.FromCard(Card.Fee());
		if (enemyAction.IsArrowBarrage)
		{
			ResolveArrowBarrage(context, enemy, enemyAction, context.Player, neutralResponse);
		}
		else if (enemyAction.IsCelestialImpact)
		{
			ResolveCelestialImpact(context, enemy, enemyAction, context.Player, neutralResponse);
		}
		else if (enemyAction.UsesNanmanSettlement)
		{
			var actualDamage = ResolveNanmanInvasion(
				context,
				enemy,
				enemyAction,
				context.Player,
				neutralResponse);
			if (enemyAction.IsTuxi && actualDamage > 0)
			{
				QueueTuxiSteal(pendingTuxiSteals, enemy, context.Player);
			}
		}
		else if (enemyAction.Type == CardType.ZiBaoAttack)
		{
			ResolveZiBaoExplosion(context, enemy);
		}
		else if (enemyAction.Type == CardType.XueZhaiAttack)
		{
			ResolveXueZhaiNearDeathDamage(context, enemy);
		}
		else if (enemyAction.IsAttack)
		{
			ResolveAttackAgainstTarget(context, enemy, context.Player, enemyAction);
		}
	}

	private static void ResolveUnassailable(BattleContext context)
	{
		// 无懈可击改为整回合"反制防御状态"，由具体结算点统一判断，不再在这里直接取消动作。
	}

	private static void ResolveAttackVsAttack(
		BattleContext context,
		EnemyInstance enemy,
		BattleAction playerAction,
		BattleAction enemyAction,
		bool playerCanDamageEnemy = true)
	{
		if (playerAction.Type == enemyAction.Type)
		{
			context.RoundResult.AddRelation($"双方【{BattleRules.GetCardName(playerAction.Type)}】互相抵消");
			return;
		}

		// 一方克制另一方时：克制方全部攻击命中，被克制方全部攻击消耗（无效化）。
		// 例：火杀×3 VS 杀×1 → 三张火杀分别造成一次伤害，杀方攻击无效。
		// 所有克制关系统一读取 BattleAction.Count，避免某个元素杀分支把整组攻击压缩为一次。
		if (BattleRules.BeatsAttack(playerAction.Type, enemyAction.Type))
		{
			context.RoundResult.AddRelation($"{BattleRules.GetCardName(playerAction.Type)}克制{BattleRules.GetCardName(enemyAction.Type)}");
			if (playerCanDamageEnemy)
			{
				for (var i = 0; i < playerAction.Count && !context.GameOver; i++)
					DealAttackDamage(context, context.Player, enemy, playerAction.Type);
			}
			return;
		}

		if (BattleRules.BeatsAttack(enemyAction.Type, playerAction.Type))
		{
			context.RoundResult.AddRelation($"{BattleRules.GetCardName(enemyAction.Type)}克制{BattleRules.GetCardName(playerAction.Type)}");
			for (var i = 0; i < enemyAction.Count && !context.GameOver; i++)
				DealAttackDamage(context, enemy, context.Player, enemyAction.Type);
			return;
		}

		// 双方互不克制：逐张平局；多余的一方独立命中。
		var rounds = Math.Max(playerAction.Count, enemyAction.Count);
		for (var i = 0; i < rounds && !context.GameOver; i++)
		{
			var playerHasAttack = i < playerAction.Count;
			var enemyHasAttack = i < enemyAction.Count;

			if (playerHasAttack && enemyHasAttack)
			{
				ResolveAttackClash(context, enemy, playerAction.Type, enemyAction.Type);
				continue;
			}

			if (playerHasAttack)
			{
				if (playerCanDamageEnemy)
				{
					DealAttackDamage(context, context.Player, enemy, playerAction.Type);
				}
				continue;
			}

			DealAttackDamage(context, enemy, context.Player, enemyAction.Type);
		}
	}

	private static void ResolveAttackClash(BattleContext context, EnemyInstance enemy, CardType playerAttack, CardType enemyAttack)
	{
		if (playerAttack == enemyAttack)
		{
			context.RoundResult.AddRelation($"双方【{BattleRules.GetCardName(playerAttack)}】平局");
			return;
		}

		// 火雷杀与雷杀：互消（雷属性中和；两者均 X）。
		if ((playerAttack == CardType.FireThunderKill && enemyAttack == CardType.ThunderKill)
			|| (playerAttack == CardType.ThunderKill && enemyAttack == CardType.FireThunderKill))
		{
			context.RoundResult.AddRelation($"{BattleRules.GetCardName(playerAttack)}与{BattleRules.GetCardName(enemyAttack)}互相抵消");
			return;
		}

		if (BattleRules.BeatsAttack(playerAttack, enemyAttack))
		{
			context.RoundResult.AddRelation($"{BattleRules.GetCardName(playerAttack)}克制{BattleRules.GetCardName(enemyAttack)}");
			DealAttackDamage(context, context.Player, enemy, playerAttack);
			return;
		}

		if (BattleRules.BeatsAttack(enemyAttack, playerAttack))
		{
			context.RoundResult.AddRelation($"{BattleRules.GetCardName(enemyAttack)}克制{BattleRules.GetCardName(playerAttack)}");
			DealAttackDamage(context, enemy, context.Player, enemyAttack);
			return;
		}

		// 双方互不克制：平局，均不受到伤害。
		context.RoundResult.AddRelation($"双方【{BattleRules.GetCardName(playerAttack)}】与【{BattleRules.GetCardName(enemyAttack)}】互不影响");
	}

	private static void ResolveAttackAgainstTarget(BattleContext context, Player attacker, Player target, BattleAction action)
	{
		for (var i = 0; i < action.Count && !context.GameOver; i++)
		{
			DealAttackDamage(context, attacker, target, action.Type);
		}
	}

	/// <summary>
	/// Battle System 的公开入口：ResolveArrowBarrage。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void ResolveArrowBarrage(
		BattleContext context,
		Player attacker,
		BattleAction attackerAction,
		Player target,
		BattleAction responseAction)
	{
		context.RoundResult.AddLine($"{attacker.DisplayName} 使用{attackerAction.DisplayName}。");
		var luoyiPiercesTargetCardDefense = attacker == context.Player && attacker.HasSkill(SkillIds.Luoyi);

		// 必中杀与影袭杀都属于必中类反击：先命中万箭使用者，再结算箭雨。
		// 影袭杀只会在影袭状态中打出，后续箭雨伤害仍走统一伤害管线，
		// 再由影袭无敌取消；不能在这里遗漏反击或绕开相关触发器。
		// 必须先于通用防御检查结算，否则会被误判成无懈可击并提前结束箭雨。
		if (responseAction.Type is CardType.SureKill or CardType.ShadowKill)
		{
			var responseName = BattleRules.GetCardName(responseAction.Type);
			context.RoundResult.AddRelation($"{responseName}克制万箭齐发");
			context.RoundResult.AddLine($"{target.DisplayName} 使用{responseName}进行反制。");
			for (var i = 0; i < responseAction.Count && !context.GameOver; i++)
			{
				DealAttackDamage(context, target, attacker, responseAction.Type);
			}
			ApplyArrowBarrageHits(context, attacker, target, attackerAction.Count);
			return;
		}

		if (!luoyiPiercesTargetCardDefense && HasCounterDefense(context, target))
		{
			context.RoundResult.AddLine($"{target.DisplayName} 使用无懈可击。");
			context.RoundResult.AddLine($"{target.DisplayName} 成功防御。");
			context.RoundResult.AddCounterDefenseBlock(target == context.Player, CardType.ArrowBarrage);
			RaiseCancelledAttackEvent(context, attacker, target, CardType.ArrowBarrage);
			return;
		}

		if (!luoyiPiercesTargetCardDefense && HasDodgeDefense(context, target))
		{
			context.RoundResult.AddLine($"{target.DisplayName} 使用闪。");
			context.RoundResult.AddLine($"{target.DisplayName} 成功闪避。");
			context.RoundResult.AddPersistentDodgeBlock(target == context.Player, CardType.ArrowBarrage);
			// 万箭齐发使用专属对撞分支，不会创建 DamageEvent，因此不会经过
			// FullBlockRecordingEffect。这里补齐同一份结构化完全格挡结果，并生成
			// 已取消的伤害事件，让【雷击】等“攻击被抵消”监听器与普通攻击共用
			// OnDamageTaken 触发链，而不需要为万箭齐发写特例。
			context.RoundResult.AddFullBlock(target);
			RaiseCancelledAttackEvent(context, attacker, target, CardType.ArrowBarrage);
			return;
		}

		if (responseAction.Type is CardType.Kill or CardType.PoisonKill or CardType.FireKill or CardType.ThunderKill or CardType.FireThunderKill)
		{
			context.RoundResult.AddLine($"{target.DisplayName} 使用{BattleRules.GetCardName(responseAction.Type)}进行反制。");
			// 叠加牌仍代表逐张打出。不能把火杀×2压缩为一次伤害，否则万箭齐发的
			// 专属双方命中结算会与普通攻击对撞的 Count 语义不一致。
			for (var i = 0; i < responseAction.Count && !context.GameOver; i++)
			{
				DealAttackDamage(context, target, attacker, responseAction.Type);
			}
			ApplyArrowBarrageHits(context, attacker, target, attackerAction.Count);
			return;
		}

		// 冰杀响应万箭齐发：反制造成 10 点伤害，万箭使用者获得冰冻，自身承受万箭 10 点。
		if (responseAction.Type == CardType.IceKill)
		{
			context.RoundResult.AddLine($"{target.DisplayName} 使用冰杀进行反制。");
			for (var i = 0; i < responseAction.Count && !context.GameOver; i++)
			{
				DealAttackDamage(context, target, attacker, CardType.IceKill);
			}
			if (!attacker.IsDead && !context.GameOver)
				IceKillFreezeEffect.ApplyFreeze(context, attacker);
			ApplyArrowBarrageHits(context, attacker, target, attackerAction.Count);
			return;
		}

		ApplyArrowBarrageHits(context, attacker, target, attackerAction.Count);
	}

	private static void ApplyArrowBarrageHits(BattleContext context, Player attacker, Player target, int count)
	{
		for (var i = 0; i < Math.Max(1, count) && !context.GameOver && !target.IsDead; i++)
		{
			ApplyArrowBarrageHit(context, attacker, target);
		}
	}

	/// <summary>
	/// Battle System 的公开入口：ApplyArrowBarrageHit。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void ApplyArrowBarrageHit(BattleContext context, Player attacker, Player target)
	{
		// 爆炸果实②"远处射箭激发果实"：只有攻击方为玩家、且本局已永久强化时才覆盖伤害属性为火；
		// 敌方使用万箭齐发（若存在）不受影响。只改 DamageType，不改费用/目标数量/基础伤害/
		// 卡牌类型/是否属于杀——伤害计算仍然完全走既有管线。
		var overrideDamageType = attacker == context.Player && GameManager.IsArrowBarrageFireUpgraded
			? DamageType.Fire
			: (DamageType?)null;
		context.DamageEvent = new DamageEvent(attacker, target, CardType.ArrowBarrage, BattleConstants.KillDamage, isDirectAttackDamage: true, overrideDamageType: overrideDamageType);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
		context.DamageEvent = null;
	}

	/// <summary>
	/// Battle System 的公开入口：ResolveCelestialImpact。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void ResolveCelestialImpact(BattleContext context, Player attacker, BattleAction attackerAction, Player target, BattleAction responseAction)
	{
		// 天体撞击只是名称和费用不同的南蛮入侵。所有响应关系必须通过同一结算入口，
		// 避免后续修改南蛮规则时两张牌再次产生行为分叉。
		ResolveNanmanInvasion(context, attacker, attackerAction, target, responseAction);
	}

	/// <summary>
	/// Battle System 的公开入口：ApplyCelestialImpactHit。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void ApplyCelestialImpactHit(BattleContext context, Player attacker, Player target)
	{
		ApplyNanmanInvasionHit(context, attacker, target, CardType.CelestialImpact);
	}

	// 南蛮入侵：出招表中的"X"表示不受到任何伤害（非失效/取消结算）。逐种响应牌严格按规则结算，
	// 不复用万箭齐发的"闪/无懈可击直接免伤"判定（两者对响应牌的克制关系并不相同）。
	/// <summary>
	/// Battle System 的公开入口：ResolveNanmanInvasion。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static int ResolveNanmanInvasion(BattleContext context, Player attacker, BattleAction attackerAction, Player target, BattleAction responseAction)
	{
		var attackName = BattleRules.GetCardName(attackerAction.Type);
		context.RoundResult.AddLine($"{attacker.DisplayName} 使用{attackName}。");

		switch (responseAction.Type)
		{
			case CardType.Kill:
			case CardType.PoisonKill:
			case CardType.FireKill:
			case CardType.ThunderKill:
			case CardType.FireThunderKill:
			case CardType.IceKill:
				// 杀系牌 VS 南蛮入侵：两者均属锦囊/攻击混合，互不影响，双方均未受到伤害（X / X）。
				context.RoundResult.AddLine($"{target.DisplayName} 使用{BattleRules.GetCardName(responseAction.Type)}，对{attackName}无效，双方均未受到伤害。");
				return 0;

			case CardType.Unassailable:
				context.RoundResult.AddLine($"{target.DisplayName} 使用无懈可击，成功反制，双方均未受到伤害。");
				RaiseCancelledAttackEvent(context, attacker, target, attackerAction.Type);
				return 0;

			case CardType.ArrowBarrage:
				context.RoundResult.AddLine($"{target.DisplayName} 使用万箭齐发，两张锦囊牌互不影响，双方均未受到伤害。");
				return 0;

			case CardType.SureKill:
				context.RoundResult.AddRelation($"必中杀克制{attackName}");
				context.RoundResult.AddLine($"{target.DisplayName} 使用必中杀，未受到伤害。");
				DealAttackDamage(context, target, attacker, CardType.SureKill);
				return 0;

			case CardType.ShadowKill:
				// 影袭杀（黄月英·如影随行专属）和必中杀一样必定命中，同样能反击南蛮入侵，
				// 不受南蛮入侵"锦囊互不影响"规则限制。
				context.RoundResult.AddRelation($"影袭杀克制{attackName}");
				context.RoundResult.AddLine($"{target.DisplayName} 使用影袭杀，未受到伤害。");
				DealAttackDamage(context, target, attacker, CardType.ShadowKill);
				return 0;

			case CardType.Steal:
				// 顺手牵羊是锦囊牌，不造成任何伤害；无法抵挡南蛮入侵，正常受击。
				context.RoundResult.AddLine($"{target.DisplayName} 使用顺手牵羊，无法抵挡{attackName}。");
				return ApplyNanmanInvasionHits(context, attacker, attackerAction, target);

			case CardType.Dodge:
				context.RoundResult.AddLine($"{target.DisplayName} 使用闪，但闪无法抵挡{attackName}。");
				return ApplyNanmanInvasionHits(context, attacker, attackerAction, target);

			case CardType.Wine:
			case CardType.Peach:
				// 酒/桃护盾仅能抵挡一次伤害：其余叠加段仍会逐段进入伤害管线。
				return ApplyNanmanInvasionHits(context, attacker, attackerAction, target);

			default:
				return ApplyNanmanInvasionHits(context, attacker, attackerAction, target);
		}
	}

	private static int ApplyNanmanInvasionHits(
		BattleContext context,
		Player attacker,
		BattleAction attackerAction,
		Player target)
	{
		var totalDamage = 0;
		for (var i = 0; i < Math.Max(1, attackerAction.Count) && !context.GameOver && !target.IsDead; i++)
		{
			totalDamage += ApplyNanmanInvasionHit(context, attacker, target, attackerAction.Type);
		}

		return totalDamage;
	}

	/// <summary>
	/// Battle System 的公开入口：ApplyNanmanInvasionHit。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static int ApplyNanmanInvasionHit(
		BattleContext context,
		Player attacker,
		Player target,
		CardType attackType = CardType.NanmanInvasion)
	{
		var damageEvent = new DamageEvent(attacker, target, attackType, BattleConstants.KillDamage, isDirectAttackDamage: true);
		context.DamageEvent = damageEvent;
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
		context.DamageEvent = null;
		return damageEvent.ActualDamageDealt;
	}

	/// <summary>
	/// 为专属对撞结算中被防御完全抵消的攻击补发取消事件。
	///
	/// 【万箭齐发】以及南蛮系攻击会在命中前直接返回，无法像普通伤害一样进入
	/// OnBeforeDamage。因此需要在这里保留一份 <see cref="DamageEvent"/>，使
	/// 【雷击】、图鉴等以“攻击是否被抵消”为条件的监听器也能读取统一状态。
	/// 不触发 OnBeforeDamage/OnDamage，避免重复消耗已经生效的防御层或重复结算伤害。
	/// </summary>
	public static void RaiseCancelledAttackEvent(BattleContext context, Player attacker, Player target, CardType attackType)
	{
		if (!BattleRules.IsAnyAttackCard(attackType))
		{
			return;
		}

		var previousDamageEvent = context.DamageEvent;
		var cancelledDamage = new DamageEvent(
			attacker,
			target,
			attackType,
			BattleRules.GetCardBaseDamage(attackType),
			isDirectAttackDamage: true);
		cancelledDamage.CancelAsFullyBlocked();
		context.DamageEvent = cancelledDamage;

		try
		{
			context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
		}
		finally
		{
			context.DamageEvent = previousDamageEvent;
		}
	}

	/// <summary>
	/// Battle System 的公开入口：DealAttackDamage。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void DealAttackDamage(BattleContext context, Player attacker, Player target, CardType attackType)
	{
		if (context.GameOver || target.IsDead)
		{
			return;
		}

		context.RoundResult.AddLine($"{attacker.DisplayName} 对 {target.DisplayName} 使用{BattleRules.GetCardName(attackType)}。");
		if (FoldingKnifeEquipment.AppliesTo(attacker, attackType))
		{
			context.RoundResult.AddLine($"折叠刀：普通杀分为{FoldingKnifeEquipment.HitCount}段，每段基础伤害{FoldingKnifeEquipment.DamagePerHit}。");
			context.AddTriggerLog("[Equipment/FoldingKnife]");
			context.AddTriggerLog($"普通杀拆分为{FoldingKnifeEquipment.HitCount}段独立{FoldingKnifeEquipment.DamagePerHit}点伤害，逐段进入完整伤害管线。");

			for (var hitIndex = 0; hitIndex < FoldingKnifeEquipment.HitCount && !context.GameOver && !target.IsDead; hitIndex++)
			{
				context.RoundResult.AddLine($"折叠刀第{hitIndex + 1}段。");
				ResolveSingleAttackDamage(context, attacker, target, attackType, FoldingKnifeEquipment.DamagePerHit);
			}
			return;
		}

		// 火雷杀基础伤害 30；其余杀系均为 KillDamage（10）；火攻（周瑜专属）是唯一的
		// 目标依赖型基础伤害，单独走 GetFireAttackDamage。
		var baseDamage = attackType == CardType.FireAttack
			? BattleRules.GetFireAttackDamage(target)
			: BattleRules.GetCardBaseDamage(attackType);
		ResolveSingleAttackDamage(context, attacker, target, attackType, baseDamage);
	}

	/// <summary>
	/// 将单次主动攻击完整送入统一伤害管线。折叠刀等多段攻击通过重复调用本方法，
	/// 保证每段都能分别触发加伤、护盾与命中后效果。
	/// </summary>
	private static void ResolveSingleAttackDamage(BattleContext context, Player attacker, Player target, CardType attackType, int baseDamage)
	{
		context.DamageEvent = new DamageEvent(attacker, target, attackType, baseDamage, isDirectAttackDamage: true);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
		context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
		context.DamageEvent = null;
	}

	/// <summary>
	/// Battle System 的公开入口：ResolveReactionAttack。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public static void ResolveReactionAttack(BattleContext context, EnemyInstance enemy, BattleAction playerReaction, BattleAction enemyAction)
	{
		if (!playerReaction.IsAttack || !enemyAction.IsAttack || context.GameOver)
		{
			return;
		}

		var enemySingleAction = BattleAction.FromCard(new Card(enemyAction.Type));
		ResolveAttackVsAttack(context, enemy, playerReaction, enemySingleAction);
	}

	private static void ApplyResourceAndHealing(BattleContext context, Player player, BattleAction action)
	{
		if (context.GameOver)
		{
			return;
		}

		if (context.IsActionCancelled(player))
		{
			return;
		}

		// 影袭发动：状态已在 PrepareDefenseLayers 中提前激活（使激活回合伤害结算前即生效）。
		if (action.Type == CardType.YingXiActivate && player.HasSkill(SkillIds.YingXi))
		{
			context.RoundResult.AddLine($"{player.DisplayName}影袭发动：进入影袭状态（3回合），全程无敌与控制免疫。");
			context.AddTriggerLog("[影袭]");
			context.AddTriggerLog($"Trigger: ApplyResourceAndHealing");
			context.AddTriggerLog($"{player.DisplayName}影袭状态激活，RemainingTurns={player.RemainingTurns}。");
			context.ReportPlayerCharacterSkillTriggered(
				player, SkillIds.YingXi, TriggerTiming.OnBattlePhase, variant: "activated");
			return;
		}

		// 影袭杀：出牌即标记 ShadowSlashUsed=true，防止无懈/护盾抵消时 OnDamageTaken 流程漏标。
		if (action.Type == CardType.ShadowKill && player.HasSkill(SkillIds.YingXi) && player.InShadowState)
		{
			player.MarkShadowSlashUsed();
			context.AddTriggerLog("[影袭]");
			context.AddTriggerLog($"{player.DisplayName}影袭杀出牌：ShadowSlashUsed=true。");
			return;
		}

		var luoyiTreatsAttackAsFee = player == context.Player
			&& player.HasSkill(SkillIds.Luoyi)
			&& action.IsAttack;
		if (luoyiTreatsAttackAsFee)
		{
			// 裸衣的“视为出费”只改变招式关系与费类触发语义，不产生费用。
			context.RoundResult.AddLine($"{player.DisplayName}【裸衣】：{BattleRules.GetCardName(action.Type)}视为出费，但不获得费用。");
			context.AddTriggerLog("[裸衣] 攻击牌视为出费，不产生资源。");
			context.ReportPlayerCharacterSkillTriggered(
				player, SkillIds.Luoyi, TriggerTiming.OnBattlePhase, EffectPriority.Mid, "attack_as_fee_no_gain");
			return;
		}

		if (action.IsFee)
		{
			// 冰蓝装甲：禁止通过出费获得费用（效果仍然消耗行动，但无费用产出）。
			var playerHasIceArmor = player == context.Player
				? GameManager.HasEquipment(EquipmentIds.IceBlueArmor)
				: (player as EnemyInstance)?.HasEquipment(EquipmentIds.IceBlueArmor) == true;
			if (playerHasIceArmor)
			{
				context.RoundResult.AddLine($"{player.DisplayName}冰蓝装甲：出费不获得费用。");
				return;
			}

			var feeGain = player.HasSkill(SkillIds.Luoshen) ? player.LuoshenFeeStreak + 1 : 1;
			player.GainMana(feeGain);
			context.RoundResult.AddResourceGain(player, feeGain);
			if (player == context.Player)
			{
				CodexService.RecordCardValue(CardType.Fee, feeGain);
			}
			return;
		}

		var appliesPeachEffect = BattleRules.ShouldApplyPeachEffect(player, action.Type);
		if (appliesPeachEffect)
		{
			// 每层桃护盾触发时已撤销对应回血；healCount = 未被护盾消耗的桃数量。
			var healCount = player == context.Player
				? context.PlayerPeachHealGranted
				: context.EnemyPeachHealGranted.TryGetValue(BattleContext.GetUnitStateKey(player), out var eh) ? eh : 0;

			// 司敌：桃翻倍
			if (player == context.Player
				&& context.Player.RuntimeStates.TryGetValue("sidi_double_peach", out var sdp) && sdp is true)
			{
				context.Player.RuntimeStates.Remove("sidi_double_peach");
				healCount *= 2;
				context.AddTriggerLog("[Skill/SiDi] 双倍桃生效 → healCount ×2");
			}

			if (healCount <= 0) return;

			var peachHealAmount = GameManager.GetPeachHealAmountFor(player);
			// 蜀·奇策增幅：桃的回复量×2（只影响回复数值，不影响是否允许超过上限等其它桃副作用）。
			if (player == context.Player && FactionFateManager.IsShuAmplificationActive())
			{
				peachHealAmount *= 2;
			}

			// 青囊：桃的回复允许超过生命值上限结算（本场战斗内有效）。
			var peachAllowOverheal = player.HasSkill(SkillIds.Qingnang);

			var healing = BattleHealing.Apply(
				context,
				player,
				peachHealAmount * healCount,
				peachAllowOverheal,
				new HealthChangeSource(HealthChangeSourceKind.AttackAction, BattleRules.GetCardName(action.Type), action.Type.ToString(), player));
			var healed = healing.HealedAmount;
			if (player == context.Player && healed > 0)
			{
				CodexService.RecordCardValue(action.Type, healed);
			}
			if (healed > 0 && peachAllowOverheal)
			{
				context.ReportPlayerCharacterSkillTriggered(
					player, SkillIds.Qingnang, TriggerTiming.OnHeal, variant: "heal");
			}

		}
	}

	private static void ApplyWineStatus(BattleContext context, Player player, BattleAction action)
	{
		var appliesWineEffect = BattleRules.ShouldApplyWineEffect(player, action.Type);
		if (!appliesWineEffect || context.IsActionCancelled(player)) return;

		// 友方定向酒：酒效（WinePower）给目标而非出牌者。
		var recipient = action.Target is EnemyInstance allyTarget && allyTarget != player
			? (Player)allyTarget
			: player;

		// 酒增伤已在 PrepareDefenseLayers 中入队，酒护盾触发时已撤销对应层；此处仅记录日志。
		if (recipient.PendingWinePower > 0)
		{
			context.RoundResult.AddWineStatus(recipient, recipient.PendingWinePower);
		}

		if (action.IsWine
			&& BattleRules.HasEquipment(player, EquipmentIds.XianNiang)
			&& BattleRules.GetCardCost(player, CardType.Wine) <= 0)
		{
			player.RuntimeStates["xian_niang_zero_wine_used"] = true;
		}
	}

	private static void ApplyStealActions(BattleContext context, double playerManaBeforeActions, Dictionary<string, double> enemyManaBeforeActions)
	{
		var playerSteals = GetStealAmount(context, context.Player, context.PlayerAction, context.PlayerActionCancelled, playerManaBeforeActions);
		if (playerSteals > 0 && context.PlayerAction?.Target is EnemyInstance stealTarget)
		{
			var stolenAmount = ResolveStealEffect(context, context.Player, stealTarget, playerSteals);
			// 只在这次行动真的是玩家打出【顺手牵羊】本身时才计入卡牌图鉴——突袭命中后
			// 追加结算的顺手牵羊复用同一个方法，但不代表玩家这回合打出了顺手牵羊这张牌。
			if (context.PlayerAction.Type == CardType.Steal && stolenAmount > 0)
			{
				CodexService.RecordCardValue(CardType.Steal, stolenAmount);
				CodexService.RecordCardSpecial(CardType.Steal);
			}
		}

		foreach (var enemyEntry in context.EnemyActions)
		{
			// 观星 VS 顺手牵羊：双方无事发生。
			if (context.PlayerAction?.Type == CardType.Guanxing && enemyEntry.Action.IsSteal)
			{
				context.RoundResult.AddLine($"{enemyEntry.Enemy.DisplayName}使用顺手牵羊，但观星令顺手牵羊无效，双方无事发生。");
				continue;
			}

			enemyManaBeforeActions.TryGetValue(BattleContext.GetUnitStateKey(enemyEntry.Enemy), out var beforeMana);
			var enemySteals = GetStealAmount(context, enemyEntry.Enemy, enemyEntry.Action, enemyEntry.ActionCancelled, beforeMana);

			if (enemySteals <= 0)
			{
				continue;
			}

			ResolveStealEffect(context, enemyEntry.Enemy, context.Player, enemySteals);
		}
	}

	/// <summary>
	/// 使用顺手牵羊的统一结算入口转移目标当前可窃取费用。
	///
	/// 普通顺手牵羊与【突袭】追加效果都调用此方法，确保无懈、谦逊、影袭免疫、
	/// 保护费用及阵营增幅始终遵循同一套规则。
	/// </summary>
	public static double ResolveStealEffect(
		BattleContext context,
		Player actor,
		Player target,
		double? requestedAmount = null)
	{
		var amount = Math.Min(requestedAmount ?? target.GetStealableMana(), target.GetStealableMana());
		context.RoundResult.AddLine($"{actor.DisplayName} 对 {target.DisplayName} 使用顺手牵羊。");

		// 【观星】的费用保护必须位于统一偷取入口，而不是只在“玩家出观星、敌人出顺手”
		// 的同回合分支中处理。这样普通顺手牵羊、突袭附带偷取，以及敌我双方作为目标时
		// 都使用同一条规则；卧龙集智体在记录/重复观星期间不会被抽空费用。
		if (target.GuanxingPhase != GuanxingPhase.None)
		{
			context.RoundResult.AddLine($"{target.DisplayName}处于观星状态，顺手牵羊无效。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, false);
			context.AddTriggerLog("[观星]");
			context.AddTriggerLog($"{target.DisplayName}观星状态：顺手牵羊无效。");
			return 0;
		}

		if (target == context.Player && target.HasSkill(SkillIds.Qianxun))
		{
			context.RoundResult.AddLine("谦逊：免疫顺手牵羊，无效。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, false);
			context.ReportPlayerCharacterSkillTriggered(
				target, SkillIds.Qianxun, TriggerTiming.OnBattlePhase, variant: "steal_immunity");
			return 0;
		}

		if (target == context.Player && target.HasSkill(SkillIds.KejiUnlimited))
		{
			context.RoundResult.AddLine("克己·无限制协议：免疫顺手牵羊，无效。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, false);
			context.ReportPlayerCharacterSkillTriggered(
				target, SkillIds.KejiUnlimited, TriggerTiming.OnBattlePhase, variant: "steal_immunity");
			return 0;
		}

		if (IsStealImmuneByShadowState(target))
		{
			context.RoundResult.AddLine($"{target.DisplayName}处于影袭状态，无法被顺手牵羊。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, false);
			context.AddTriggerLog("[影袭]");
			context.AddTriggerLog($"{target.DisplayName}影袭状态：顺手牵羊无效。");
			return 0;
		}

		if (HasCounterDefense(context, target))
		{
			context.RoundResult.AddLine($"{target.DisplayName}无懈可击发动。");
			context.RoundResult.AddLine("顺手牵羊无效。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, false);
			context.RoundResult.AddCounterDefenseBlock(target == context.Player, CardType.Steal);
			return 0;
		}

		if (amount <= 0)
		{
			context.RoundResult.AddLine($"{target.DisplayName}没有可窃取费用。");
			context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, 0, true);
			return 0;
		}

		BattleRules.PayManaAndRaiseResourceChanged(context, target, amount, false);
		actor.GainProtectedStealMana(amount);
		context.RoundResult.AddSteal(actor, amount);
		context.RoundResult.AddStealResolution(actor.DisplayName, target.DisplayName, amount, true);
		context.RoundResult.AddLine("顺手牵羊获得的费用受到保护。");

		// 蜀·奇策增幅只强化玩家一侧的成功顺手牵羊，不改变敌方原有行为。
		if (actor == context.Player && FactionFateManager.IsShuAmplificationActive())
		{
			actor.GainMana(0.5);
			context.RoundResult.AddLine("蜀·奇策增幅：顺手牵羊额外获得0.5费。");
		}

		return amount;
	}

	private readonly record struct PendingTuxiSteal(Player Actor, Player Target);

	private static void QueueTuxiSteal(List<PendingTuxiSteal> pendingTuxiSteals, Player actor, Player target)
	{
		pendingTuxiSteals.Add(new PendingTuxiSteal(actor, target));
	}

	private static void ResolvePendingTuxiSteals(
		BattleContext context,
		IReadOnlyList<PendingTuxiSteal> pendingTuxiSteals)
	{
		foreach (var pending in pendingTuxiSteals)
		{
			ResolveTuxiStealEffect(context, pending.Actor, pending.Target);
		}
	}

	/// <summary>
	/// 在资源牌结算后处理【突袭】命中附带的顺手牵羊与空费用补偿。
	///
	/// 偷取过程仍完全委托给统一顺手牵羊入口；本层只处理突袭独有的“目标确实
	/// 没有费用时获得1费”，避免普通顺手牵羊意外继承该规则。延后到资源结算
	/// 完成后执行，确保目标本回合通过【费】获得的费用也能被正常偷取。
	/// </summary>
	private static double ResolveTuxiStealEffect(BattleContext context, Player actor, Player target)
	{
		var targetHadNoMana = target.CurrentMana <= 0;
		var stolenAmount = ResolveStealEffect(context, actor, target);
		if (!targetHadNoMana || stolenAmount > 0)
		{
			return stolenAmount;
		}

		actor.GainMana(1);
		context.RoundResult.AddResourceGain(actor, 1);
		context.RoundResult.AddLine($"【突袭】：{target.DisplayName}没有费用，{actor.DisplayName}获得1费。");
		return 0;
	}

	private static bool IsStealImmuneByShadowState(Player target)
	{
		return target.InShadowState;
	}

	private static double GetStealAmount(BattleContext context, Player actor, BattleAction? action, bool actionCancelled, double manaBeforeActions)
	{
		if (action?.IsSteal != true || actionCancelled)
		{
			return 0;
		}

		if (!CanResolveSteal(context, actor, manaBeforeActions))
		{
			context.RoundResult.AddStealFailure(actor);
			var failedTarget = actor == context.Player
				? action?.Target as Player
				: context.Player;
			context.RoundResult.AddStealResolution(
				actor.DisplayName,
				failedTarget?.DisplayName ?? string.Empty,
				0,
				false);
			return 0;
		}

		var amount = actor == context.Player
			? action?.Target is EnemyInstance targetEnemy ? targetEnemy.GetStealableMana() : 0
			: context.Player.GetStealableMana();
		if (amount <= 0)
		{
			var emptyTarget = actor == context.Player
				? action?.Target as Player
				: context.Player;
			context.RoundResult.AddStealResolution(
				actor.DisplayName,
				emptyTarget?.DisplayName ?? string.Empty,
				0,
				true);
		}

		return amount;
	}

	private static bool CanResolveSteal(BattleContext context, Player actor, double manaBeforeActions)
	{
		if (manaBeforeActions > 0)
		{
			return true;
		}

		return actor == context.Player
			? context.RoundResult.PlayerManaGain > 0
			: context.RoundResult.EnemyManaGain > 0;
	}

	// 返回玩家对指定攻击者的有效响应行动。
	// 正确规则：每张牌只与其目标之间进行结算。
	// 如果玩家出的是针对特定目标的攻击牌（如杀→医者），且当前攻击者不是该目标，
	// 则玩家对此攻击者没有可用的响应，返回中性的"费"作为无效响应占位。
	private static BattleAction GetEffectivePlayerResponse(BattleContext context, EnemyInstance attacker)
	{
		var playerAction = context.PlayerAction!;
		if (context.Player.HasSkill(SkillIds.Luoyi) && playerAction.IsAttack)
		{
			return BattleAction.FromCard(Card.Fee());
		}

		if (playerAction.TargetType == CardTargetType.Targeted
			&& playerAction.Target is EnemyInstance playerTarget
			&& playerTarget != attacker)
		{
			return BattleAction.FromCard(Card.Fee());
		}

		return playerAction;
	}

	private static void LogActionBindings(
		BattleContext context,
		BattleAction playerAction,
		EnemyActionEntry? targetEntry)
	{
		context.AddTriggerLog("[TargetBinding]");
		context.AddTriggerLog(
			$"Player={BattleContext.GetUnitStateKey(context.Player)} "
			+ $"Action={BattleRules.GetCardName(playerAction.Type)}×{playerAction.Count} "
			+ $"Target={DescribeBattleUnit(playerAction.Target)} "
			+ $"ResolvedTargetEntry={DescribeBattleUnit(targetEntry?.Enemy)}");

		foreach (var entry in context.EnemyActions)
		{
			context.AddTriggerLog(
				$"Enemy={BattleContext.GetUnitStateKey(entry.Enemy)} "
				+ $"Definition={entry.Enemy.Id} "
				+ $"Action={BattleRules.GetCardName(entry.Action.Type)}×{entry.Action.Count} "
				+ $"Target={DescribeBattleUnit(entry.Action.Target)} "
				+ $"Cancelled={entry.ActionCancelled}");
		}
	}

	private static string DescribeBattleUnit(BattleUnit? unit)
	{
		return unit is Player player
			? $"{player.DisplayName}({BattleContext.GetUnitStateKey(player)})"
			: "none";
	}

	private static void AddUnusedActionNotes(BattleContext context)
	{
		if (context.PlayerAction == null || context.EnemyActions.Count == 0)
		{
			return;
		}

		// 玩家使用闪/无懈可击时，逐一检查所有敌方行动，确认是否有有效响应目标。
		// 不再依赖 GetTargetEnemyActionEntry()，避免非目标单位被错误纳入检查。
		AddPlayerUnusedActionNote(context);

		// 各敌方单位以玩家行动为响应对象，检查其闪/无懈可击是否闲置。
		foreach (var enemyEntry in context.EnemyActions)
		{
			AddUnusedActionNote(context, enemyEntry.Enemy, enemyEntry.Action, context.PlayerAction);
		}
	}

	// 检查玩家的闪/无懈可击在当前回合是否有任意有效响应目标；无则输出"闲置"提示。
	private static void AddPlayerUnusedActionNote(BattleContext context)
	{
		var playerAction = context.PlayerAction;
		if (playerAction == null || context.IsActionCancelled(context.Player))
		{
			return;
		}

		if (!playerAction.IsDodge && !playerAction.IsUnassailable)
		{
			return;
		}

		foreach (var enemyEntry in context.EnemyActions)
		{
			if (enemyEntry.Enemy.IsDead || enemyEntry.ActionCancelled)
			{
				continue;
			}

			if (playerAction.IsDodge && CanDodgeRespondTo(enemyEntry.Action.Type))
			{
				return;
			}

			if (playerAction.IsUnassailable && BattleRules.CanUnassailableCounter(enemyEntry.Action.Type))
			{
				return;
			}
		}

		// 没有任何敌方行动可被响应，生成闲置提示。
		if (playerAction.IsDodge)
		{
			context.RoundResult.AddLine($"{context.Player.DisplayName}使用闪，但未受到可抵挡攻击。");
			return;
		}

		foreach (var enemyEntry in context.EnemyActions)
		{
			if (!enemyEntry.Enemy.IsDead)
			{
				context.RoundResult.AddLine($"{context.Player.DisplayName}使用无懈可击，但未能反制{BattleRules.GetCardName(enemyEntry.Action.Type)}。");
				return;
			}
		}
	}

	private static void AddUnusedActionNote(BattleContext context, Player actor, BattleAction action, BattleAction opponentAction)
	{
		if (context.IsActionCancelled(actor))
		{
			return;
		}

		if (action.IsDodge && !CanDodgeRespondTo(opponentAction.Type))
		{
			context.RoundResult.AddLine($"{actor.DisplayName}使用闪，但未受到可抵挡攻击。");
			return;
		}

		if (action.IsUnassailable && !BattleRules.CanUnassailableCounter(opponentAction.Type))
		{
			context.RoundResult.AddLine($"{actor.DisplayName}使用无懈可击，但未能反制{BattleRules.GetCardName(opponentAction.Type)}。");
		}
	}

	private static bool CanDodgeRespondTo(CardType type)
	{
		// 火雷杀穿透闪但仍属"可响应"攻击，避免错误显示"未受到可抵挡攻击"提示。
		return type is CardType.Kill or CardType.FireKill or CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.Tuxi
			or CardType.FireThunderKill or CardType.CelestialImpact;
	}

	private static bool HasDodgeDefense(BattleContext context, Player target)
	{
		if (target == context.Player)
		{
			return context.PlayerDodgeDefenseActive;
		}

		return target is EnemyInstance enemy
			&& context.EnemyDodgeDefenseActive.TryGetValue(BattleContext.GetUnitStateKey(enemy), out var active)
			&& active;
	}

	private static bool HasCounterDefense(BattleContext context, Player target)
	{
		if (target == context.Player)
		{
			return context.PlayerCounterDefenseActive;
		}

		return target is EnemyInstance enemy
			&& context.EnemyCounterDefenseActive.TryGetValue(BattleContext.GetUnitStateKey(enemy), out var active)
			&& active;
	}

}
