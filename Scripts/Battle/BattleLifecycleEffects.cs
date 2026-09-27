//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/BattleLifecycleEffects.cs
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

// ======================================================
// BattlePhase 生命周期
// ======================================================
// 本文件收拢“回合开始 / 出牌前 / 回合结束 / 战斗结束”这些生命周期效果。
// 它们不直接决定卡牌胜负，而是为 BattleResolver 准备状态、清理临时标记，
// 并把跨回合 Buff、装备冷却、酒状态等规则接入统一 Trigger 流程。
//
// 设计原因：
// - 生命周期效果集中注册，方便排查某个状态为什么在回合边界变化。
// - 战斗结算只关注本回合行动，跨回合状态在这里统一推进。
// - 新增装备或 Buff 时优先挂接已有 TriggerTiming，不新增旁路调用。
/// <summary>
/// 回合开始时推进酒状态的生命周期效果。
///
/// 该效果只负责把上一回合留下的酒增伤状态转入当前可结算状态，
/// 不直接参与卡牌胜负判断，避免 BattleResolver 同时承担状态推进职责。
/// </summary>
public sealed class TurnStartWineEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnTurnStart;
	public EffectPriority Priority => EffectPriority.Low;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		var enemies = context.Encounter?.Enemies;
		if (enemies != null)
		{
			foreach (var enemy in enemies)
			{
				ActivateWine(context, enemy);
			}
		}
		ActivateWine(context, context.Player);
	}

	private static void ActivateWine(BattleContext context, Player player)
	{
		var layers = player.PendingWinePower;
		player.ActivatePendingWinePower();
		if (layers > 0)
		{
			context.AddTriggerLog($"{player.DisplayName}酒状态生效：本回合杀系伤害 ×{BattleRules.GetWineDamageMultiplier(player, layers):0.##}");
		}
	}
}

/// <summary>
/// Battle System 的公开类：BattlePrePhaseEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattlePrePhaseEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
	public EffectPriority Priority => EffectPriority.Immediate;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		context.BeginRoundResult();
	}
}

/// <summary>
/// Battle System 的公开类：EquipmentBattlePrePhaseEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentBattlePrePhaseEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
	public EffectPriority Priority => EffectPriority.High;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		if (GameManager.HasEquipment(EquipmentIds.YellowTalisman))
		{
			context.AddTriggerLog("[Equipment]");
			context.AddTriggerLog("黄道符：战斗开始初始费用固定为2。");
		}

		// 冰蓝装甲（玩家）：第一个战斗回合开始时，每件装甲提供 +7 费用。
		if (context.TurnNumber == 1)
		{
			if (GameManager.CurrentChapter == 1 && GameManager.HasChapterVariant(ChapterVariant.BloodMoon))
			{
				var variantEnemies = context.Encounter?.Enemies;
				if (variantEnemies != null)
				{
					foreach (var enemy in variantEnemies)
					{
						if (enemy.IsDead)
						{
							continue;
						}

						enemy.AddFerocityLayers(1);
						enemy.RuntimeStates["blood_moon_persistent_ferocity"] = true;
						context.RoundResult.AddLine($"{enemy.DisplayName}受到【血红之月】影响：获得不会衰减的凶残。");
						context.AddTriggerLog("[血红之月]");
						context.AddTriggerLog($"{enemy.DisplayName}获得永久凶残。");
					}
				}
			}

			var iceArmorCount = GameManager.CountEquipment(EquipmentIds.IceBlueArmor);
			if (iceArmorCount > 0)
			{
				context.Player.GainMana(iceArmorCount * 7);
				context.RoundResult.AddLine($"冰蓝装甲触发：战斗开始获得{iceArmorCount * 7}费。");
				context.AddTriggerLog($"[Equipment] 冰蓝装甲×{iceArmorCount}：战斗开始 +{iceArmorCount * 7}费。");
			}

			// 蛮族的牙齿：战斗开始时获得3层凶残。
			if (GameManager.HasEquipment(EquipmentIds.BarbarianTooth))
			{
				context.Player.AddFerocityLayers(3);
				context.RoundResult.AddLine("蛮族的牙齿：战斗开始获得3层【凶残】（杀系伤害×1.5，每回合结束-1层）。");
				context.AddTriggerLog("[Equipment] 蛮族的牙齿：+3层凶残。");
			}

			// 冰蓝装甲（敌方）：每件装甲提供 +7 费用。
			var enemies = context.Encounter?.Enemies;
			if (enemies != null)
			{
				foreach (var enemy in enemies)
				{
					if (enemy.IsDead) continue;
					var count = enemy.CountEquipment(EquipmentIds.IceBlueArmor);
					if (count <= 0) continue;
					enemy.GainMana(count * 7);
					context.RoundResult.AddLine($"{enemy.DisplayName}冰蓝装甲触发：战斗开始获得{count * 7}费。");
					context.AddTriggerLog($"[Equipment] {enemy.DisplayName}冰蓝装甲×{count}：+{count * 7}费。");
				}
			}
		}
	}
}

/// <summary>
/// RunBuff 的战斗开始费用加成。
///
/// 这类效果不能直接揉进 GameManager.GetPlayerInitialMana()，否则 UI 会把
/// “Buff 触发后的费用”误显示成“基础初始费用”，排查时看不到来源。
/// 放在 BattlePrePhase 且只在第 1 回合触发，可以复用 RoundResult 和 TriggerLog。
/// </summary>
public sealed class RunBuffBattleStartManaEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
	public EffectPriority Priority => EffectPriority.Low;

	/// <summary>
	/// 读取当前 RunBuff 的战斗开始费用加成，并通过正式费用接口发放。
	/// </summary>
	public void Execute(BattleContext context)
	{
		if (context.TurnNumber != 1) return;

		var bonus = RunBuffManager.GetPlayerBattleStartManaBonus();
		if (bonus == 0) return;

		context.Player.GainMana(bonus);
		var sign = bonus > 0 ? "+" : string.Empty;
		context.RoundResult.AddLine($"RunBuff：战斗开始费用{sign}{BattleRules.FormatMana(bonus)}。");
		context.AddTriggerLog($"[RunBuff/BattleStartMana] 玩家费用{sign}{BattleRules.FormatMana(bonus)}。");
	}
}

/// <summary>
/// 蜀·先机：每场战斗开始时额外+1费用。必须在固定初始费用（黄道符，走的是费用系统另一处
/// 计算入口）/装备加成费用（冰蓝装甲，见上面 EquipmentBattlePrePhaseEffect 的 High 优先级）
/// 都结算完之后最后应用，所以用 EffectPriority.Lowest——同一个 OnBattlePrePhase timing 内，
/// 排在 High 之后执行。
/// </summary>
public sealed class ShuInitiativeManaEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
	public EffectPriority Priority => EffectPriority.Lowest;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		if (context.TurnNumber != 1) return;
		if (FactionFateManager.CurrentFateId != FactionFateIds.ShuInitiative) return;

		context.Player.GainMana(1);
		context.RoundResult.AddLine("蜀·先机：战斗开始额外获得1费。");
		context.AddTriggerLog("[FactionFate/ShuInitiative] 战斗开始 +1费。");
	}
}

/// <summary>
/// Battle System 的公开类：QingnangPeachEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class QingnangPeachEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
	public EffectPriority Priority => EffectPriority.Highest;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		// 青囊的“桃格挡后仍保留治疗”由桃盾消耗分支统一处理；此效果类只负责
		// 保持技能接入既有 Trigger 优先级，不在这里重复修改治疗或伤害数据。
	}
}

/// <summary>
/// Battle System 的公开类：BattlePostPhaseEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattlePostPhaseEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
	public EffectPriority Priority => EffectPriority.Lowest;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
	}
}

/// <summary>
/// Battle System 的公开类：EquipmentBattlePostPhaseEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentBattlePostPhaseEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
	public EffectPriority Priority => EffectPriority.Low;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		if (context.TurnCounter != 4)
		{
			return;
		}

		// 玩家虎符：GameManager 负责持有/消耗一次性使用次数。
		var triggered = 0;
		while (GameManager.TryConsumeEquipmentUse(EquipmentIds.TigerTally))
		{
			triggered += 1;
		}

		if (triggered > 0)
		{
			context.Player.GainMana(triggered);
			context.RoundResult.AddLine($"虎符触发：第四个战斗回合结束后获得{triggered}费。");
			context.AddTriggerLog("[Equipment]");
			context.AddTriggerLog($"虎符：第4回合结束，获得{triggered}费。");
		}

		// 敌方虎符：通过 RuntimeStates["tiger_tally_triggered"] 保证每场战斗仅触发一次。
		var enemies = context.Encounter?.Enemies;
		if (enemies == null)
		{
			return;
		}

		const string tigerTallyKey = "tiger_tally_triggered";
		foreach (var enemy in enemies)
		{
			if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.TigerTally))
			{
				continue;
			}

			if (enemy.RuntimeStates.ContainsKey(tigerTallyKey))
			{
				continue;
			}

			enemy.RuntimeStates[tigerTallyKey] = true;
			enemy.GainMana();
			context.RoundResult.AddLine($"虎符触发：{enemy.DisplayName}第四个战斗回合结束后获得1费。");
			context.AddTriggerLog("[Equipment]");
			context.AddTriggerLog($"虎符（敌方）：{enemy.DisplayName}第4回合结束获得1费。");
		}
	}
}

/// <summary>
/// Battle System 的公开类：TurnEndCleanupEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TurnEndCleanupEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
	public EffectPriority Priority => EffectPriority.Lowest;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
		// 蜀·奇策增幅：酒效果多存活1回合——ActivatePendingWinePower 在饮酒当回合已经打上
		// shu_amp_wine_extra_turn_pending 标记时，本次 OnTurnEnd 跳过清空酒状态（只跳过这一行，
		// 不影响下面 ClearProtectedStealMana 等其它清理逻辑），下一次
		// OnTurnEnd 才会真正清空。
		var skipWineClear = context.Player.RuntimeStates.TryGetValue("shu_amp_wine_extra_turn_pending", out var pending) && pending is true;
		if (skipWineClear)
		{
			context.Player.RuntimeStates.Remove("shu_amp_wine_extra_turn_pending");
		}
		else
		{
			context.Player.ClearWinePower();
		}
		context.Player.ClearProtectedStealMana();
		context.Player.DecrementFreezeTimer();
		context.Player.DecrementStunTimer();
		context.Player.DecrementFerocity();
		DecrementWeaknessWithLog(context, context.Player);
		context.PlayerDodgeDefenseActive = false;
		context.PlayerCounterDefenseActive = false;
		// 仁德已统一为“本回合护盾”。它不能像旧实现一样跨回合累计；未消耗的层数
		// 在回合结束时直接失效，下回合由 OnTurnStart 重新刷新。
		context.Player.RuntimeStates.Remove("rende_shield_player");
		var enemies = context.Encounter?.Enemies;
		if (enemies != null)
		{
			foreach (var enemy in enemies)
			{
				enemy.ClearWinePower();
				enemy.ClearProtectedStealMana();
				enemy.DecrementFreezeTimer();
				enemy.DecrementStunTimer();
				enemy.RuntimeStates.Remove("rende_shield");
				if (!enemy.RuntimeStates.TryGetValue("blood_moon_persistent_ferocity", out var persistent) || persistent is not true)
				{
					enemy.DecrementFerocity();
				}
				DecrementWeaknessWithLog(context, enemy);
			}
		}
		context.EnemyDodgeDefenseActive.Clear();
		context.EnemyCounterDefenseActive.Clear();
		if (context.Player.ClearExpiredLianyingFreeKill())
		{
			context.RoundResult.AddLine("连营未使用，状态已消失。");
			context.AddTriggerLog("[连营]");
			context.AddTriggerLog("Trigger: OnTurnEnd");
			context.AddTriggerLog("Priority: Lowest");
			context.AddTriggerLog("玩家未使用免费普通杀，连营状态消失。");
		}

		var lianyingEnemies = context.Encounter?.Enemies;
		if (lianyingEnemies != null)
		{
			foreach (var enemy in lianyingEnemies)
			{
				if (enemy.ClearExpiredLianyingFreeKill())
				{
					context.RoundResult.AddLine($"{enemy.DisplayName}连营未使用，状态已消失。");
					context.AddTriggerLog("[连营]");
					context.AddTriggerLog($"{enemy.DisplayName}未使用免费普通杀，连营状态消失。");
				}
			}
		}
	}

	// 虚弱：回合结束时-1层持续时间，剩余>0时补一条战报行，方便左侧战报读到
	// "剩余N回合"这类具体信息，而不是只在获得时看到层数。
	private static void DecrementWeaknessWithLog(BattleContext context, Player player)
	{
		if (player.WeaknessLayers <= 0)
		{
			return;
		}

		player.DecrementWeakness();
		if (player.WeaknessLayers > 0)
		{
			context.RoundResult.AddLine($"{player.DisplayName}【虚弱】剩余{player.WeaknessLayers}回合。");
		}
		else
		{
			context.RoundResult.AddLine($"{player.DisplayName}【虚弱】已消失。");
		}
	}
}

/// <summary>
/// Battle System 的公开类：BattleEndEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattleEndEffect : IBattleEffect
{
	public TriggerTiming Timing => TriggerTiming.OnBattleEnd;
	public EffectPriority Priority => EffectPriority.Lowest;

	/// <summary>
	/// Battle System 的公开入口：Execute。
	///
	/// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
	/// </summary>
	public void Execute(BattleContext context)
	{
	}
}
