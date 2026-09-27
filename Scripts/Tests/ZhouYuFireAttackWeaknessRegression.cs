//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ZhouYuFireAttackWeaknessRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证周瑜角色定义、【英姿】赋予的专属锦囊【火攻】、【业炎】被动技能。
// 2. 验证【火攻】的目标依赖型伤害公式、火属性归属、与火杀共用的克制关系、
//    仍保留锦囊牌身份（可被无懈可击反制）。
// 3. 验证【虚弱】Debuff：层数=剩余回合数、不随层数加深减伤、叠加规则、
//    回合结束衰减、伤害预估同步、不影响治疗/费用等无关数值。
//
// 不负责：
// × 验证美术/UI视觉效果。
// × 验证敌方AI是否会主动使用火攻（火攻是周瑜专属，敌人不会获得该技能）。
//
// 主要依赖：
// BattleRules / BattleTriggerEffects / DamagePreviewService / CardChoiceProvider
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;
using System.Reflection;

/// <summary>
/// 周瑜 / 火攻 / 虚弱 的 Headless 回归入口。
/// </summary>
public partial class ZhouYuFireAttackWeaknessRegression : Node
{
    private int _assertionCount;

    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();

            TestCharacterDefinition();
            TestFireAttackCardDefinition();
            TestFireAttackCanStack();
            TestFireAttackDamageFormula();
            TestFireAttackUsesMaxHealthNotTempHp();
            TestFireAttackCounterProfileMatchesFireKill();
            TestFireAttackBlockedByUnassailableUnlikeCirclePurity();
            TestFireAttackDodgeAndShieldBehaviorMatchesFireKill();
            TestFireAttackIsFireDamageType();
            TestFireAttackExcludedFromGenericCardPool();
            TestCardMatchupDataHasFireAttack();

            TestWeaknessSingleLayerReducesDamageBy25Percent();
            TestWeaknessMultipleLayersStillOnly25Percent();
            TestWeaknessDoesNotAffectHealOrOtherActor();
            TestWeaknessDurationDecrementsPerRound();
            TestWeaknessReapplyAddsDuration();
            TestWeaknessDamagePreviewMatchesRealMultiplierRegardlessOfStacks();

            TestYeYanGrantsWeaknessOnRealFireDamage();
            TestYeYanDoesNotTriggerOnZeroDamage();
            TestYeYanDoesNotTriggerOnNonFireDamage();
            TestYeYanDoesNotTriggerDuringDamagePreview();
            TestYeYanTogglePerRoundDedup();

            await TestFireAttackGrantedByYingZiInRealBattle();

            GD.Print($"ZHOUYU_FIREATTACK_WEAKNESS_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ZHOUYU_FIREATTACK_WEAKNESS_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    // ───────────────────────── 周瑜 / 火攻定义 ─────────────────────────

    private void TestCharacterDefinition()
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.ZhouYu);
        Assert(character != null, "找不到周瑜角色定义");
        Assert(character!.MaxHp == 30, $"周瑜基础生命值应为30，实际{character.MaxHp}");
        Assert(character.Faction == Faction.Wu, "周瑜阵营应为吴");
        Assert(character.SkillIds.Contains(SkillIds.YingZi), "周瑜应携带【英姿】");
        Assert(character.SkillIds.Contains(SkillIds.YeYan), "周瑜应携带【业炎】");

        var yingZi = SkillDatabase.GetSkill(SkillIds.YingZi);
        Assert(yingZi != null && yingZi.CharacterId == CharacterIds.ZhouYu, "英姿归属错误");
        Assert(yingZi!.Rarity == SkillRarity.Rare, "英姿稀有度应为稀有");

        var yeYan = SkillDatabase.GetSkill(SkillIds.YeYan);
        Assert(yeYan != null && yeYan.CharacterId == CharacterIds.ZhouYu, "业炎归属错误");
        Assert(yeYan!.Rarity == SkillRarity.Common, "业炎稀有度应为普通");
        Assert(yeYan.Kinds.Contains(SkillKind.Passive), "业炎应为被动技能");
    }

    private void TestFireAttackCardDefinition()
    {
        var card = Card.FireAttack();
        Assert(card.Cost == 1, $"火攻费用应为1，实际{card.Cost}");
        Assert(card.Categories.HasFlag(CardCategory.Attack), "火攻应属于攻击类");
        Assert(card.IsTrickCard, "火攻应属于锦囊牌");
        Assert(card.TargetType == CardTargetType.Targeted, "火攻应为指向性单体卡牌");
        Assert(!BattleRules.IsShaAttack(CardType.FireAttack), "火攻默认不应该是杀类型牌");
    }

    private void TestFireAttackCanStack()
    {
        Assert(BattleAction.CanStackType(CardType.FireAttack), "火攻应支持连续出牌");
        var stacked = BattleAction.FromCard(Card.FireAttack(), 2);
        Assert(stacked.Count == 2, "两次火攻应保留为同一个两层行动");
        Assert(stacked.DisplayName.Contains("×2"), "连续火攻的行动名称应显示次数");
    }

    private void TestFireAttackDamageFormula()
    {
        Assert(ComputeExpected(30) == 8, "MaxHp=30时火攻基础伤害应为8");
        Assert(ComputeExpected(55) == 10, "MaxHp=55时火攻基础伤害应为10");
        Assert(ComputeExpected(100) == 15, "MaxHp=100时火攻基础伤害应为15");
        Assert(ComputeExpected(200) == 25, "MaxHp=200时火攻基础伤害应为25");
        Assert(ComputeExpected(450) == 50, "MaxHp=450时火攻基础伤害应为50");

        static int ComputeExpected(int maxHp)
        {
            var target = new EnemyInstance(new EnemyDefinition
            {
                Id = $"fireattack_formula_{maxHp}",
                Name = "火攻公式测试敌人",
                MaxHP = maxHp,
                Type = EnemyType.Normal,
                StartingResource = 0,
                StartingDeck = new EnemyDeck()
            });
            return BattleRules.GetFireAttackDamage(target);
        }
    }

    private void TestFireAttackUsesMaxHealthNotTempHp()
    {
        var target = CreateEnemy("fireattack_temp_hp_target", 100);
        Assert(BattleRules.GetFireAttackDamage(target) == 15, "未强化时MaxHp=100应造成15点基础伤害");

        // 战斗内临时提升最大生命值（如Boss强化）：火攻应该跟随新的有效值重新计算。
        target.AddMaxHealth(200);
        Assert(target.MaxHealth == 300, "测试前提失败：AddMaxHealth后最大生命值应变为300");
        Assert(BattleRules.GetFireAttackDamage(target) == 35, "最大生命值被临时提升到300后，火攻基础伤害应变为35");
    }

    private void TestFireAttackCounterProfileMatchesFireKill()
    {
        foreach (var other in new[] { CardType.Kill, CardType.ThunderKill, CardType.FireThunderKill, CardType.IceKill, CardType.PoisonKill, CardType.SureKill })
        {
            Assert(
                BattleRules.BeatsAttack(CardType.FireAttack, other) == BattleRules.BeatsAttack(CardType.FireKill, other),
                $"火攻 vs {other} 的克制判定应该和火杀完全一致（attack方向）");
            Assert(
                BattleRules.BeatsAttack(other, CardType.FireAttack) == BattleRules.BeatsAttack(other, CardType.FireKill),
                $"{other} vs 火攻 的克制判定应该和 {other} vs 火杀 完全一致（defense方向）");
        }
    }

    // 无懈可击反制资格与"循环克制关系是否复用火杀"是两件独立的事：
    // 火攻虽然复用火杀的三元循环，但它仍然是锦囊牌，必须能被无懈可击反制；
    // 普通火杀本身并不在 CanUnassailableCounter 的允许列表里。这条差异必须
    // 被保留，不能因为"复用火杀判定"就把这一点也一起复用掉。
    private void TestFireAttackBlockedByUnassailableUnlikeCirclePurity()
    {
        Assert(BattleRules.CanUnassailableCounter(CardType.FireAttack), "火攻作为攻击性锦囊牌应该能被无懈可击反制");
        Assert(!BattleRules.CanUnassailableCounter(CardType.FireKill), "火杀本身不应该能被无懈可击反制（用于对照火攻不是单纯复制火杀的每一条规则）");
    }

    private void TestFireAttackDodgeAndShieldBehaviorMatchesFireKill()
    {
        var player = CreatePlayer("zhouyu_dodge_test");
        var target = CreateEnemy("fireattack_dodge_target", 100);
        var context = CreateContext(player, target);

        target.RuntimeStates["__dummy"] = true; // no-op，保持与其它测试一致的写法
        var enemyKey = BattleContext.GetUnitStateKey(target);
        context.EnemyDodgeDefenseActive[enemyKey] = true;
        context.EnemyDodgeLayers[enemyKey] = 1;

        var damage = new DamageEvent(player, target, CardType.FireAttack, BattleRules.GetFireAttackDamage(target), isDirectAttackDamage: true);
        context.DamageEvent = damage;
        new DefenseBeforeDamageEffect().Execute(context);
        damage.ResolveModifiers();

        Assert(damage.Cancelled, "火攻应该像普通火杀一样被闪格挡（不穿透）");
    }

    private void TestFireAttackIsFireDamageType()
    {
        var player = CreatePlayer("zhouyu_firetype_test");
        var target = CreateEnemy("fireattack_firetype_target", 100);
        var damage = new DamageEvent(player, target, CardType.FireAttack, BattleRules.GetFireAttackDamage(target));
        Assert((damage.DamageType & DamageType.Fire) != 0, "火攻的DamageEvent应该带有Fire属性标记");
    }

    private void TestFireAttackExcludedFromGenericCardPool()
    {
        var provider = new CardChoiceProvider { Count = 200, Randomize = false, IncludeCharacterExclusive = false };
        var choices = provider.CreateChoices();
        Assert(!choices.Any(c => c.Id == CardType.FireAttack.ToString()), "默认随机卡池不应该包含周瑜专属的火攻");
        Assert(!choices.Any(c => c.Id == CardType.Tuxi.ToString()), "测试前提核对：突袭同样应该被排除（与火攻使用同一条豁免规则）");

        var includeExclusive = new CardChoiceProvider { Count = 200, Randomize = false, IncludeCharacterExclusive = true };
        var withExclusive = includeExclusive.CreateChoices();
        Assert(withExclusive.Any(c => c.Id == CardType.FireAttack.ToString()), "显式允许角色专属牌时，火攻应该能出现在候选池里");
    }

    private void TestCardMatchupDataHasFireAttack()
    {
        var lines = CardMatchupData.GetMatchups(CardType.FireAttack);
        Assert(lines != null && lines.Length > 0, "图鉴克制关系表里缺少火攻的条目");
    }

    // ───────────────────────── 虚弱 ─────────────────────────

    private void TestWeaknessSingleLayerReducesDamageBy25Percent()
    {
        var player = CreatePlayer("weakness_single_layer");
        player.AddWeaknessLayers(1);
        var target = CreateEnemy("weakness_target_a", 100);
        var damage = ResolveOnDamage(player, target, CardType.Kill, 20);
        Assert(damage.Amount == 15, $"1层虚弱下20点基础伤害应变为15（×0.75），实际{damage.Amount}");
    }

    private void TestWeaknessMultipleLayersStillOnly25Percent()
    {
        foreach (var layers in new[] { 1, 2, 5, 99 })
        {
            var player = CreatePlayer($"weakness_multi_layer_{layers}");
            player.AddWeaknessLayers(layers);
            var target = CreateEnemy($"weakness_target_{layers}", 100);
            var damage = ResolveOnDamage(player, target, CardType.Kill, 20);
            Assert(damage.Amount == 15, $"虚弱{layers}层时伤害仍应该是20×0.75=15（不随层数继续降低），实际{damage.Amount}");
        }
    }

    private void TestWeaknessDoesNotAffectHealOrOtherActor()
    {
        var player = CreatePlayer("weakness_no_heal_effect");
        player.AddWeaknessLayers(3);
        var healthBefore = player.Health;
        player.Heal(20);
        Assert(player.Health == System.Math.Min(player.MaxHealth, healthBefore + 20), "虚弱不应该影响自身的治疗量");

        // 虚弱只影响"造成"伤害的一方；虚弱角色作为目标被攻击时不应该被削弱来源伤害。
        var attacker = CreatePlayer("weakness_unrelated_attacker");
        var damage = ResolveOnDamage(attacker, player, CardType.Kill, 20);
        Assert(damage.Amount == 20, "虚弱角色作为受害目标时，攻击者造成的伤害不应该被削弱");
    }

    private void TestWeaknessDurationDecrementsPerRound()
    {
        var player = CreatePlayer("weakness_duration_player");
        player.AddWeaknessLayers(2);
        var context = CreateContext(player, CreateEnemy("weakness_duration_enemy", 50));

        new TurnEndCleanupEffect().Execute(context);
        Assert(player.WeaknessLayers == 1, $"2层虚弱回合结束后应该变为1层，实际{player.WeaknessLayers}");
        Assert(player.HasWeakness, "剩余1层时应该仍处于虚弱状态");

        new TurnEndCleanupEffect().Execute(context);
        Assert(player.WeaknessLayers == 0, $"1层虚弱回合结束后应该变为0层，实际{player.WeaknessLayers}");
        Assert(!player.HasWeakness, "0层时不应该再处于虚弱状态");
    }

    private void TestWeaknessReapplyAddsDuration()
    {
        var player = CreatePlayer("weakness_reapply_player");
        player.AddWeaknessLayers(1);
        Assert(player.WeaknessLayers == 1, "测试前提失败：初始应为1层");

        player.AddWeaknessLayers(2);
        Assert(player.WeaknessLayers == 3, $"虚弱×1再获得虚弱×2应该叠加为3层，实际{player.WeaknessLayers}");

        var target = CreateEnemy("weakness_reapply_target", 100);
        var damage = ResolveOnDamage(player, target, CardType.Kill, 20);
        Assert(damage.Amount == 15, "叠加到3层后伤害倍率仍然只是×0.75，不应该变成0.75×0.75");
    }

    private void TestWeaknessDamagePreviewMatchesRealMultiplierRegardlessOfStacks()
    {
        foreach (var layers in new[] { 1, 5 })
        {
            var player = CreatePlayer($"weakness_preview_player_{layers}");
            player.AddWeaknessLayers(layers);
            var target = CreateEnemy($"weakness_preview_target_{layers}", 100);
            // DamagePreviewService 内部会用 realContext.TriggerManager 跑一次真实的
            // OnDamage RaiseTrigger，必须是注册了 WeaknessDamageEffect 的完整默认
            // TriggerManager，不能是空的 new TriggerManager()。
            var encounter = new BattleEncounter();
            encounter.Enemies.Add(target);
            var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager()) { Encounter = encounter };
            context.BeginRoundResult();

            var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, target);
            Assert(result.IsDamageCard, "普通杀预估应该被识别为伤害牌");
            Assert(result.PredictedFinalDamage == 7, $"虚弱{layers}层下杀的预估伤害应为10×0.75=7（向下取整），实际{result.PredictedFinalDamage}");
        }
    }

    // ───────────────────────── 业炎 ─────────────────────────

    private void TestYeYanGrantsWeaknessOnRealFireDamage()
    {
        var player = CreatePlayerWithYeYan("yeyan_real_damage_player");
        var target = CreateEnemy("yeyan_real_damage_target", 100);
        var context = CreateContext(player, target);

        var damage = new DamageEvent(player, target, CardType.FireAttack, BattleRules.GetFireAttackDamage(target), isDirectAttackDamage: true);
        context.DamageEvent = damage;
        damage.ResolveModifiers();
        damage.ActualDamageDealt = damage.Amount;
        new YeYanEffect().Execute(context);

        Assert(target.WeaknessLayers == 2, $"业炎成功触发后目标应该获得2层虚弱，实际{target.WeaknessLayers}");
    }

    private void TestYeYanDoesNotTriggerOnZeroDamage()
    {
        var player = CreatePlayerWithYeYan("yeyan_zero_damage_player");
        var target = CreateEnemy("yeyan_zero_damage_target", 100);
        var context = CreateContext(player, target);

        var damage = new DamageEvent(player, target, CardType.FireAttack, 0, isDirectAttackDamage: true);
        context.DamageEvent = damage;
        damage.ActualDamageDealt = 0;
        new YeYanEffect().Execute(context);

        Assert(target.WeaknessLayers == 0, "实际伤害为0时业炎不应该触发");
    }

    private void TestYeYanDoesNotTriggerOnNonFireDamage()
    {
        var player = CreatePlayerWithYeYan("yeyan_non_fire_player");
        var target = CreateEnemy("yeyan_non_fire_target", 100);
        var context = CreateContext(player, target);

        var damage = new DamageEvent(player, target, CardType.Kill, 10, isDirectAttackDamage: true);
        context.DamageEvent = damage;
        damage.ActualDamageDealt = 10;
        new YeYanEffect().Execute(context);

        Assert(target.WeaknessLayers == 0, "非火属性伤害不应该触发业炎");
    }

    private void TestYeYanDoesNotTriggerDuringDamagePreview()
    {
        var player = CreatePlayerWithYeYan("yeyan_preview_player");
        var target = CreateEnemy("yeyan_preview_target", 100);
        var context = CreateContext(player, target);

        // DamagePreviewService 只跑 OnDamage 阶段，从不触发 OnDamageTaken——
        // YeYanEffect 挂在 OnDamageTaken，所以预览天然不会调用到它。
        DamagePreviewService.PreviewCard(context, player, CardType.FireAttack, target);
        Assert(target.WeaknessLayers == 0, "手牌伤害预估不应该真正触发业炎并给目标施加虚弱");
    }

    private void TestYeYanTogglePerRoundDedup()
    {
        var player = CreatePlayerWithYeYan("yeyan_dedup_player");
        var targetA = CreateEnemy("yeyan_dedup_target_a", 100);
        var targetB = CreateEnemy("yeyan_dedup_target_b", 100);
        var context = CreateContext(player, targetA);
        context.Encounter!.Enemies.Add(targetB);

        var presentationCount = 0;
        context.PlayerSkillPresentationRequested = _ => presentationCount++;

        void FireHit(EnemyInstance target)
        {
            var damage = new DamageEvent(player, target, CardType.FireAttack, 10, isDirectAttackDamage: true);
            context.DamageEvent = damage;
            damage.ActualDamageDealt = 10;
            new YeYanEffect().Execute(context);
        }

        FireHit(targetA);
        FireHit(targetB);
        Assert(targetA.WeaknessLayers == 2 && targetB.WeaknessLayers == 2, "同一回合内两个目标都应该各自获得虚弱");
        Assert(presentationCount == 1, $"同一回合内业炎命中多个目标，技能大字只应该播放一次，实际{presentationCount}次");

        context.BeginRoundResult();
        FireHit(targetA);
        Assert(presentationCount == 2, "新的一回合再次触发业炎时，应该重新播放一次技能大字");
    }

    // ───────────────────────── 英姿：真实战斗场景内验证行动栏 ─────────────────────────

    private async System.Threading.Tasks.Task TestFireAttackGrantedByYingZiInRealBattle()
    {
        var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
        var battle = battleScene.Instantiate<BattleManager>();
        battle.ConfigureDebugMode(CharacterIds.ZhouYu);
        AddChild(battle);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var method = typeof(BattleManager).GetMethod("GetAvailableActionCards", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 BattleManager.GetAvailableActionCards，方法可能被重命名");
        var cards = (System.Collections.Generic.List<Card>)method.Invoke(battle, null)!;
        Assert(cards.Any(c => c.Type == CardType.FireAttack), "周瑜的可用行动栏里应该包含火攻（由英姿赋予）");
        Assert(!cards.Any(c => c.Type == CardType.FireKill), "周瑜的英姿应将火杀替换为火攻，而非两者同时存在");

        var shouldConfirmImmediately = typeof(BattleManager).GetMethod("ShouldConfirmImmediately", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 BattleManager.ShouldConfirmImmediately，方法可能被重命名");
        var playerField = typeof(BattleManager).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 BattleManager._player 字段，字段可能被重命名");
        var battlePlayer = (Player)playerField.GetValue(battle)!;

        battlePlayer.DebugSetMana(1);
        Assert((bool)shouldConfirmImmediately.Invoke(battle, new object[] { Card.FireAttack(), 1 })!,
            "费用只够一次火攻时不应显示继续叠加的读条");

        battlePlayer.DebugSetMana(2);
        Assert(!(bool)shouldConfirmImmediately.Invoke(battle, new object[] { Card.FireAttack(), 1 })!,
            "费用还能支付第二次火攻时应允许继续叠加");
        Assert((bool)shouldConfirmImmediately.Invoke(battle, new object[] { Card.FireAttack(), 2 })!,
            "第二次火攻耗尽可叠加费用后应立即结算");

        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var otherBattleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
        var otherBattle = otherBattleScene.Instantiate<BattleManager>();
        otherBattle.ConfigureDebugMode(CharacterIds.ZhaoYun);
        AddChild(otherBattle);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var otherCards = (System.Collections.Generic.List<Card>)method.Invoke(otherBattle, null)!;
        Assert(!otherCards.Any(c => c.Type == CardType.FireAttack), "没有【英姿】的角色不应该在行动栏里看到火攻");
        Assert(otherCards.Any(c => c.Type == CardType.FireKill), "没有【英姿】的角色应继续拥有火杀");

        // 初始事件㉒授予的【铁索连环】应对非庞统角色同样可用，不能只因不是角色默认技能
        // 而被行动栏筛选隐藏。
        Assert(GameManager.TryAddInitialEventAttackTrick(CardType.IronChain), "初始事件㉒未能授予铁索连环");
        var eventGrantedCards = (System.Collections.Generic.List<Card>)method.Invoke(otherBattle, null)!;
        Assert(eventGrantedCards.Any(c => c.Type == CardType.IronChain), "初始事件㉒获得铁索连环后，普通角色行动栏没有显示该牌");
        Assert(eventGrantedCards.Any(c => c.Type == CardType.FireKill), "初始事件㉒获得铁索连环不应移除普通角色原有火杀");

        otherBattle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    // ───────────────────────── 测试工具方法 ─────────────────────────

    private static DamageEvent ResolveOnDamage(Player source, Player target, CardType type, int baseDamage)
    {
        var manager = BattleTriggerEffects.CreateDefaultManager();
        var encounter = new BattleEncounter();
        if (target is EnemyInstance enemyTarget) encounter.Enemies.Add(enemyTarget);
        if (source is EnemyInstance enemySource) encounter.Enemies.Add(enemySource);

        // WeaknessDamageEffect 只读取 damage.Source.HasWeakness，不依赖 context.Player
        // 是否恰好等于 source——这里传入 source 本身即可，两个测试用例里 source 始终是
        // Player 实例（从未以 EnemyInstance 身份调用本方法）。
        var context = new BattleContext(source, manager) { Encounter = encounter };
        context.BeginRoundResult();

        var damage = new DamageEvent(source, target, type, baseDamage, isDirectAttackDamage: true);
        context.DamageEvent = damage;
        manager.RaiseTrigger(TriggerTiming.OnDamage, context);
        damage.ResolveModifiers();
        return damage;
    }

    private static Player CreatePlayer(string id)
    {
        var player = new Player("玩家", id, BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static Player CreatePlayerWithYeYan(string id)
    {
        var player = CreatePlayer(id);
        player.SetCharacter(CharacterDatabase.GetCharacter(CharacterIds.ZhouYu));
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.YeYan)!);
        return player;
    }

    private static EnemyInstance CreateEnemy(string id, int maxHp)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = "测试敌人",
            MaxHP = maxHp,
            Type = EnemyType.Normal,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        });
    }

    private static BattleContext CreateContext(Player player, EnemyInstance enemy)
    {
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter
        };
        context.BeginRoundResult();
        return context;
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
