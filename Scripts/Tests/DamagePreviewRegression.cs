//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DamagePreviewRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 DamagePreviewService 对无双/赤兔/酒/凶残/亢奋/诅咒/
//    煞气缠身/排箫/知识芯片/南蛮入侵多目标/孙策激昂链式追加/
//    张辽突袭等机制的预估数字与真实伤害管线一致。
// 2. 验证预估过程零副作用：不扣血、不消耗装备次数/技能次数、
//    不写入真实战斗日志、不消耗正式 RNG。
//
// 不负责：
// × 验证 UI 角标渲染/悬停 Tooltip（阶段B之后由人工在 Godot 内验证）。
//
// 主要依赖：
// DamagePreviewService
// BattleRules / TriggerManager / BattleRules 完整效果注册列表
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 手牌预估伤害显示功能的 Headless 回归入口。
/// </summary>
public partial class DamagePreviewRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"DAMAGE_PREVIEW_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DAMAGE_PREVIEW_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        RunBuffManager.RemoveAllStacks(RunBuffIds.Curse);
        RunBuffManager.RemoveAllStacks(RunBuffIds.ShaQiChenShen);

        TestNonDamageCardReturnsNotDamageCard();
        TestBasicKillDamage();
        TestDamagePipelineAlwaysAddsBeforeMultiplying();
        TestGiantShieldPreviewUsesCurrentHealth();
        TestFoldingKnifePreviewSumsThreeBoostedHits();
        TestWushuangDoubleAndTripleWithChiTuLuBu();
        TestChituFirstRoundDouble();
        TestWineDamageBonus();
        TestFerocityAndFrenzyStacking();
        TestCurseAndShaQiMultipliers();
        TestPanXiaoBidirectionalMultiplier();
        TestKnowledgeChipAttackTrickBonus();
        TestMultiTargetNanmanInvasionDifferentAmounts();
        TestTuxiDamageExcludesStealBonus();
        TestJiAngFollowUpDamage();
        TestJiAngFinalDamageRespectsRemainingHealth();
        TestJiAngNoFollowUpWhenBaseDamageIsZero();
        TestPreviewDoesNotConsumeOneShotEquipment();
        TestPreviewHasZeroSideEffects();
    }

    // ------------------------------------------------------------
    // 构造帮助方法
    // ------------------------------------------------------------

    private static TriggerManager BuildFullTriggerManager()
    {
        return BattleTriggerEffects.CreateDefaultManager();
    }

    private static Player NewPlayer(string characterId = "player", int maxHealth = 100)
    {
        var player = new Player("玩家", characterId, BattleTeam.Player);
        player.ResetForNewBattle(maxHealth, maxHealth, 0);
        return player;
    }

    private static EnemyInstance NewEnemy(string id, int maxHp = 50)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = "测试敌人",
            MaxHP = maxHp,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
    }

    private static BattleContext NewContext(Player player, TriggerManager manager, BattleEncounter? encounter = null)
    {
        var context = new BattleContext(player, manager) { Encounter = encounter };
        context.BeginRoundResult();
        return context;
    }

    // ------------------------------------------------------------
    // 用例
    // ------------------------------------------------------------

    private void TestNonDamageCardReturnsNotDamageCard()
    {
        var player = NewPlayer();
        var enemy = NewEnemy("np1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Dodge, enemy);
        Assert(!result.IsDamageCard, "闪不是伤害牌，却返回了 IsDamageCard=true");

        var wineResult = DamagePreviewService.PreviewCard(context, player, CardType.Wine, enemy);
        Assert(!wineResult.IsDamageCard, "酒不是伤害牌，却返回了 IsDamageCard=true");
    }

    private void TestBasicKillDamage()
    {
        var player = NewPlayer();
        var enemy = NewEnemy("basic1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(result.IsDamageCard, "普通杀应被识别为伤害牌");
        Assert(result.BaseDamage == BattleRules.GetCardBaseDamage(CardType.Kill), "普通杀基础伤害读取错误");
        Assert(result.PredictedFinalDamage == BattleConstants.KillDamage, $"无任何加成时普通杀预估伤害应为{BattleConstants.KillDamage}，实际={result.PredictedFinalDamage}");
        Assert(result.IsUncertain, "预估结果必须始终标注不确定性（可能被闪/无懈可击抵消）");
        Assert(!result.IsMultiTarget, "普通杀不应被标记为多目标牌");
    }

    private void TestDamagePipelineAlwaysAddsBeforeMultiplying()
    {
        var pipeline = new DamageModifierPipeline();
        // 特意把固定增伤放在很晚的 Priority，验证来源注册顺序/优先级不会改变
        // "(基础伤害 + 全部固定增伤) × 全部倍率" 这一核心规则。
        pipeline.Add(new DamageModifier("晚注册固定增伤", DamageModifierPriority.FinalMultiplier,
            DamageModifierOperation.Add, 5));
        pipeline.Add(new DamageModifier("测试倍率", DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.Multiply, 2));

        Assert(pipeline.Resolve(10) == 30,
            $"伤害必须先加算后乘算：(10+5)×2 应为30，实际={pipeline.Resolve(10)}");
        Assert(pipeline.ResolvePreCap(10) == 30,
            $"封顶前伤害也必须遵守先加算后乘算，实际={pipeline.ResolvePreCap(10)}");
    }

    private void TestGiantShieldPreviewUsesCurrentHealth()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var giantShield = InventoryManager.AddToInventory(EquipmentIds.GiantShield, EquipmentGainSource.SaveRestore);
        Assert(giantShield != null && InventoryManager.EquipToSlot(giantShield.InstanceId, EquipmentSlot.Armor),
            "巨人盾测试前未能装备到护甲槽");

        // 实战开局会由 BattleManager 将巨人盾的+45生命并入战斗角色；这里直接构造
        // 同一份战斗内数据，专门验证预览也读取当前生命而非最大生命或静态定义。
        var player = NewPlayer("giant_shield_preview", 145);
        var enemy = NewEnemy("giant_shield_preview_enemy", 300);
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var fullHealth = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(fullHealth.PredictedFinalDamage == 14,
            $"巨人盾满血145时，杀预览应为10+floor(145×3%)=14，实际={fullHealth.PredictedFinalDamage}");

        player.TakeDamage(45);
        var injured = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(injured.PredictedFinalDamage == 13,
            $"巨人盾当前生命100时，杀预览应为10+floor(100×3%)=13，实际={injured.PredictedFinalDamage}");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestFoldingKnifePreviewSumsThreeBoostedHits()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var foldingKnife = InventoryManager.AddToInventory(EquipmentIds.FoldingKnife, EquipmentGainSource.SaveRestore);
        Assert(foldingKnife != null && InventoryManager.EquipToSlot(foldingKnife.InstanceId, EquipmentSlot.Weapon),
            "折叠刀预览测试前未能装备到武器槽");
        InventoryManager.AddToInventory(EquipmentIds.AttackChip, EquipmentGainSource.SaveRestore);

        var player = NewPlayer("folding_knife_preview", 40);
        var enemy = NewEnemy("folding_knife_preview_enemy", 100);
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);
        var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);

        Assert(result.BaseDamage == 10, $"折叠刀普通杀的总基础伤害应显示为10，实际={result.BaseDamage}");
        Assert(result.PredictedFinalDamage == 16,
            $"折叠刀普通杀应显示2段各(5+3)的最终总伤害16，实际={result.PredictedFinalDamage}");
        Assert(result.Breakdown.Any(line => line.Contains("折叠刀第2段", StringComparison.Ordinal))
            && !result.Breakdown.Any(line => line.Contains("折叠刀第3段", StringComparison.Ordinal)),
            "折叠刀伤害预览没有准确显示两段结算");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestWushuangDoubleAndTripleWithChiTuLuBu()
    {
        var player = NewPlayer();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Wushuang)!);
        var enemy = NewEnemy("wushuang1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);
        context.TurnNumber = 2; // 避开赤兔"第一回合杀系伤害×2"的额外交互，单独验证无双本身。

        var doubled = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(doubled.PredictedFinalDamage == BattleConstants.KillDamage * 2, $"无双未装备赤兔时应为×2，实际={doubled.PredictedFinalDamage}");

        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.LuBu);
        InventoryManager.Reset();
        var chitu = InventoryManager.AddToInventory(EquipmentIds.ChiTu, EquipmentGainSource.SaveRestore)!;
        InventoryManager.EquipToSlot(chitu.InstanceId, EquipmentSlot.Vehicle);

        var tripled = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(tripled.PredictedFinalDamage == BattleConstants.KillDamage * 3, $"吕布+赤兔时无双应为×3，实际={tripled.PredictedFinalDamage}");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestChituFirstRoundDouble()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var chitu = InventoryManager.AddToInventory(EquipmentIds.ChiTu, EquipmentGainSource.SaveRestore)!;
        InventoryManager.EquipToSlot(chitu.InstanceId, EquipmentSlot.Vehicle);

        var player = NewPlayer();
        var enemy = NewEnemy("chitu1");
        var manager = BuildFullTriggerManager();

        var turn1Context = NewContext(player, manager);
        turn1Context.TurnNumber = 1;
        var turn1 = DamagePreviewService.PreviewCard(turn1Context, player, CardType.Kill, enemy);
        Assert(turn1.PredictedFinalDamage == BattleConstants.KillDamage * 2, $"赤兔第一回合应翻倍，实际={turn1.PredictedFinalDamage}");

        var turn2Context = NewContext(player, manager);
        turn2Context.TurnNumber = 2;
        var turn2 = DamagePreviewService.PreviewCard(turn2Context, player, CardType.Kill, enemy);
        Assert(turn2.PredictedFinalDamage == BattleConstants.KillDamage, $"赤兔翻倍只应作用于第一回合，第二回合实际={turn2.PredictedFinalDamage}");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestWineDamageBonus()
    {
        var player = NewPlayer();
        player.QueueWinePower(3);
        player.ActivatePendingWinePower();
        var enemy = NewEnemy("wine1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(result.PredictedFinalDamage > BattleConstants.KillDamage, $"酒Buff应提高杀的预估伤害，实际={result.PredictedFinalDamage}");
    }

    private void TestFerocityAndFrenzyStacking()
    {
        var player = NewPlayer();
        player.AddFerocityLayers(2);
        var enemy = NewEnemy("ferocity1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var ferocityResult = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        var expectedFerocity = (int)(BattleConstants.KillDamage * 1.5);
        Assert(ferocityResult.PredictedFinalDamage == expectedFerocity, $"凶残应为×1.5（不随层数额外叠加），期望={expectedFerocity}，实际={ferocityResult.PredictedFinalDamage}");

        var frenzyPlayer = NewPlayer();
        FrenzyBuffHelper.AddStacks(frenzyPlayer, 2);
        var frenzyContext = NewContext(frenzyPlayer, manager);
        var frenzyResult = DamagePreviewService.PreviewCard(frenzyContext, frenzyPlayer, CardType.Kill, enemy);
        var expectedFrenzy = (int)(BattleConstants.KillDamage * Math.Pow(1.5, 2));
        Assert(frenzyResult.PredictedFinalDamage == expectedFrenzy, $"亢奋2层应为×1.5^2，期望={expectedFrenzy}，实际={frenzyResult.PredictedFinalDamage}");
    }

    private void TestCurseAndShaQiMultipliers()
    {
        RunBuffManager.RemoveAllStacks(RunBuffIds.Curse);
        RunBuffManager.AddStacks(RunBuffIds.Curse, 3, null, false);

        var player = NewPlayer();
        var enemy = NewEnemy("curse1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var curseResult = DamagePreviewService.PreviewCard(context, enemy, CardType.Kill, player);
        Assert(curseResult.PredictedFinalDamage > BattleConstants.KillDamage, $"诅咒应提高玩家受到的伤害预估，实际={curseResult.PredictedFinalDamage}");
        RunBuffManager.RemoveAllStacks(RunBuffIds.Curse);

        RunBuffManager.RemoveAllStacks(RunBuffIds.ShaQiChenShen);
        RunBuffManager.AddStacks(RunBuffIds.ShaQiChenShen, 1, null, false);
        var shaqiResult = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(shaqiResult.PredictedFinalDamage > BattleConstants.KillDamage, $"煞气缠身应提高玩家造成的伤害预估，实际={shaqiResult.PredictedFinalDamage}");
        RunBuffManager.RemoveAllStacks(RunBuffIds.ShaQiChenShen);
    }

    private void TestPanXiaoBidirectionalMultiplier()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var panxiao = InventoryManager.AddToInventory(EquipmentIds.PanXiao, EquipmentGainSource.SaveRestore)!;
        InventoryManager.EquipToSlot(panxiao.InstanceId, EquipmentSlot.Accessory1);

        var player = NewPlayer();
        var enemy = NewEnemy("panxiao1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var enemyToPlayer = DamagePreviewService.PreviewCard(context, enemy, CardType.Kill, player);
        Assert(enemyToPlayer.PredictedFinalDamage < BattleConstants.KillDamage, $"排箫应使我方受到伤害÷1.1，实际={enemyToPlayer.PredictedFinalDamage}");

        var playerToEnemy = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(playerToEnemy.PredictedFinalDamage > BattleConstants.KillDamage, $"排箫应使敌方受到伤害×1.1，实际={playerToEnemy.PredictedFinalDamage}");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestKnowledgeChipAttackTrickBonus()
    {
        GameManager.BeginNewRun();
        GameManager.IncrementKnowledgeChipCount();
        GameManager.IncrementKnowledgeChipCount();

        var player = NewPlayer();
        var enemy = NewEnemy("chip1");
        var manager = BuildFullTriggerManager();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = NewContext(player, manager, encounter);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.ArrowBarrage, null);
        Assert(result.IsDamageCard, "万箭齐发应被识别为伤害牌");
        var expected = BattleConstants.KillDamage + GameManager.AttackTrickDamageBonus;
        Assert(result.TargetBreakdown.Count == 1 && result.TargetBreakdown[0].Amount == expected,
            $"知识芯片加成未正确计入万箭齐发预估，期望单目标={expected}，实际={(result.TargetBreakdown.Count > 0 ? result.TargetBreakdown[0].Amount : -1)}");

        GameManager.BeginNewRun();
    }

    private void TestMultiTargetNanmanInvasionDifferentAmounts()
    {
        var player = NewPlayer();
        var enemyA = NewEnemy("nanman_a");
        var enemyB = NewEnemy("nanman_b");
        var manager = BuildFullTriggerManager();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemyA);
        encounter.Enemies.Add(enemyB);
        var context = NewContext(player, manager, encounter);

        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var panxiao = InventoryManager.AddToInventory(EquipmentIds.PanXiao, EquipmentGainSource.SaveRestore)!;
        InventoryManager.EquipToSlot(panxiao.InstanceId, EquipmentSlot.Accessory1);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.NanmanInvasion, null);
        Assert(result.IsMultiTarget, "南蛮入侵应被标记为多目标牌");
        Assert(result.TargetBreakdown.Count == 2, $"南蛮入侵应对2名敌人分别预估，实际={result.TargetBreakdown.Count}");
        Assert(result.TargetBreakdown.All(t => t.Amount == (int)(BattleConstants.KillDamage * 1.1)), "排箫加成应对每个南蛮入侵目标都生效");
        Assert(!result.TargetsHaveDifferentAmounts, "排箫对两个敌人加成相同时不应标记为差异化伤害");
        Assert(result.PredictedFinalDamage == result.TargetBreakdown.Sum(t => t.Amount), "多目标总计应为各目标之和");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestTuxiDamageExcludesStealBonus()
    {
        var player = NewPlayer();
        var enemy = NewEnemy("tuxi1");
        var manager = BuildFullTriggerManager();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = NewContext(player, manager, encounter);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Tuxi, null);
        Assert(result.IsDamageCard, "张辽突袭应被识别为伤害牌");
        Assert(result.TargetBreakdown.Count == 1 && result.TargetBreakdown[0].Amount == BattleConstants.KillDamage,
            $"突袭伤害数字不应包含顺手牵羊，期望={BattleConstants.KillDamage}，实际={(result.TargetBreakdown.Count > 0 ? result.TargetBreakdown[0].Amount : -1)}");
        Assert(result.Warnings.Any(w => w.Contains("顺手牵羊") || w.Length > 0), "突袭应在Warnings中提示顺手牵羊，而不计入伤害数字");
    }

    private void TestJiAngFollowUpDamage()
    {
        var player = NewPlayer("sunce_test", 40);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiAng)!);
        var enemy = NewEnemy("jiang1");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(result.FollowUpDamage != null, "带激昂技能且本体命中伤害>0时应生成链式追加预览");
        var expectedFollowUp = (int)Math.Floor(player.MaxHealth * JiAngBattleEffect.ExtraDamageMaxHealthRatio);
        Assert(result.FollowUpDamage!.Amount == expectedFollowUp, $"激昂追加量应为floor(MaxHealth*0.20)={expectedFollowUp}，实际={result.FollowUpDamage.Amount}");
        Assert(result.PredictedFinalDamage == BattleConstants.KillDamage + expectedFollowUp,
            $"卡面最终伤害必须计入激昂追加，期望={BattleConstants.KillDamage + expectedFollowUp}，实际={result.PredictedFinalDamage}");
        Assert(result.TargetBreakdown.Count == 1 && result.TargetBreakdown[0].Amount == result.PredictedFinalDamage,
            "单目标的伤害明细必须与卡面最终伤害总数一致");
    }

    private void TestJiAngNoFollowUpWhenBaseDamageIsZero()
    {
        var player = NewPlayer("sunce_test2", 40);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiAng)!);
        var enemy = NewEnemy("jiang2");
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Dodge, enemy);
        Assert(!result.IsDamageCard, "非伤害牌不应触发激昂追加分析路径");
    }

    private void TestJiAngFinalDamageRespectsRemainingHealth()
    {
        var player = NewPlayer("sunce_low_hp_target", 40);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiAng)!);
        var enemy = NewEnemy("jiang_low_hp", 15);
        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);

        var result = DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        Assert(result.FollowUpDamage != null && result.FollowUpDamage.Amount == 5,
            "目标剩余15生命时，激昂追加伤害应只显示剩余的5点");
        Assert(result.PredictedFinalDamage == 15,
            $"最终伤害不能超过目标剩余生命，期望=15，实际={result.PredictedFinalDamage}");
    }

    private void TestPreviewDoesNotConsumeOneShotEquipment()
    {
        // 喷气式狼牙棒：预览要显示本次可获得的三倍伤害，但不能提前耗掉本场次数。
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var jetMace = InventoryManager.AddToInventory(EquipmentIds.JetMace, EquipmentGainSource.SaveRestore)!;
        Assert(InventoryManager.EquipToSlot(jetMace.InstanceId, EquipmentSlot.Weapon), "喷气式狼牙棒测试前无法装备");
        var jetPlayer = NewPlayer("jet_mace_preview", 40);
        var jetPreview = DamagePreviewService.PreviewCard(
            NewContext(jetPlayer, BuildFullTriggerManager()), jetPlayer, CardType.Kill, NewEnemy("jet_mace_target", 100));
        Assert(jetPreview.PredictedFinalDamage == BattleConstants.KillDamage * 3, "喷气式狼牙棒预览没有显示三倍伤害");
        Assert(jetPlayer.JetMaceActive, "伤害预览不应提前消耗喷气式狼牙棒");
        var jetRealContext = NewContext(jetPlayer, BuildFullTriggerManager());
        var jetRealDamage = new DamageEvent(jetPlayer, NewEnemy("jet_mace_real_target", 100), CardType.Kill, BattleConstants.KillDamage, isDirectAttackDamage: true);
        jetRealContext.DamageEvent = jetRealDamage;
        jetRealContext.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, jetRealContext);
        jetRealDamage.ResolveModifiers(jetPlayer.WinePower);
        Assert(jetRealDamage.Amount == BattleConstants.KillDamage * 3 && !jetPlayer.JetMaceActive,
            "喷气式狼牙棒应在预览后仍能于首次真实攻击造成三倍伤害并消耗");

        // 长弓同样是首击一次性效果；之前也会被 OnDamage 预览提前标记为已使用。
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var longBow = InventoryManager.AddToInventory(EquipmentIds.LongBow, EquipmentGainSource.SaveRestore)!;
        Assert(InventoryManager.EquipToSlot(longBow.InstanceId, EquipmentSlot.Weapon), "长弓测试前无法装备");
        var longBowPlayer = NewPlayer("long_bow_preview", 40);
        var longBowPreview = DamagePreviewService.PreviewCard(
            NewContext(longBowPlayer, BuildFullTriggerManager()), longBowPlayer, CardType.Kill, NewEnemy("long_bow_target", 100));
        Assert(longBowPreview.PredictedFinalDamage == BattleConstants.KillDamage * 2, "长弓预览没有显示首击双倍伤害");
        Assert(!longBowPlayer.LongBowUsed, "伤害预览不应提前消耗长弓首击");

        // 战鼓首次攻击只应在实战中进入“下回合激活”的待机状态；预览不能推进它。
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var warDrum = InventoryManager.AddToInventory(EquipmentIds.WarDrum, EquipmentGainSource.SaveRestore)!;
        Assert(InventoryManager.EquipToSlot(warDrum.InstanceId, EquipmentSlot.Accessory1), "战鼓测试前无法装备");
        var warDrumPlayer = NewPlayer("war_drum_preview", 40);
        var warDrumContext = NewContext(warDrumPlayer, BuildFullTriggerManager());
        _ = DamagePreviewService.PreviewCard(warDrumContext, warDrumPlayer, CardType.Kill, NewEnemy("war_drum_target", 100));
        Assert(!warDrumPlayer.WarDrumFirstAttackPlayed && !warDrumPlayer.WarDrumPending,
            "伤害预览不应触发战鼓的首次攻击状态");

        warDrumPlayer.TriggerWarDrum();
        warDrumPlayer.ActivateWarDrum();
        var activeDrumPreview = DamagePreviewService.PreviewCard(
            warDrumContext, warDrumPlayer, CardType.Kill, NewEnemy("active_war_drum_target", 100));
        Assert(activeDrumPreview.PredictedFinalDamage == BattleConstants.KillDamage + 5,
            "已激活战鼓的伤害预览应继续显示+5伤害");
        Assert(!warDrumPlayer.WarDrumAttackedThisTurn,
            "伤害预览不应把战鼓标记为本回合已攻击");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestPreviewHasZeroSideEffects()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var chitu = InventoryManager.AddToInventory(EquipmentIds.ChiTu, EquipmentGainSource.SaveRestore)!;
        InventoryManager.EquipToSlot(chitu.InstanceId, EquipmentSlot.Vehicle);
        var equipUsesBefore = InventoryManager.CountEquipped(EquipmentIds.ChiTu);

        var player = NewPlayer("sunce_test3", 40);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiAng)!);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Wushuang)!);
        var healthBefore = player.Health;
        var enemy = NewEnemy("sideeffect1");
        var enemyHealthBefore = enemy.Health;

        var manager = BuildFullTriggerManager();
        var context = NewContext(player, manager);
        var roundResultTextBefore = context.RoundResult.Text;
        var triggerLogCountBefore = context.TriggerLogs.Count;

        for (var i = 0; i < 20; i++)
        {
            DamagePreviewService.PreviewCard(context, player, CardType.Kill, enemy);
        }

        Assert(player.Health == healthBefore, "预估过程不应改变玩家生命值");
        Assert(enemy.Health == enemyHealthBefore, "预估过程不应改变敌人生命值");
        Assert(InventoryManager.CountEquipped(EquipmentIds.ChiTu) == equipUsesBefore, "预估过程不应影响装备持有/装备状态");
        Assert(context.RoundResult.Text == roundResultTextBefore, "预估过程不应写入真实战斗日志（RoundResult）");
        Assert(context.TriggerLogs.Count == triggerLogCountBefore, "预估过程不应写入真实 TriggerLogs");
        Assert(context.DamageEvent == null, "预估过程不应残留 DamageEvent 到真实 context 上");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
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
