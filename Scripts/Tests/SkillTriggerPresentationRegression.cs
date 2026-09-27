//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/SkillTriggerPresentationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 通过真实技能效果验证角色技能表现请求。
// 2. 验证敌方技能不会进入我方表现队列。
// 3. 验证同结算链去重和不同结算链重复播放。
//
// 不负责：
// × 判断最终美术效果是否符合视觉稿。
// × 模拟完整玩家点击流程。
//
// 主要依赖：
// BattleContext
// TriggerManager
// SkillTriggerToastQueue
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 角色技能触发大字的 Headless 回归入口。
/// </summary>
public partial class SkillTriggerPresentationRegression : Node
{
    private readonly List<SkillTriggerPresentationRequest> _requests = new();
    private int _assertionCount;

    /// <summary>
    /// 执行真实技能效果回归，并通过退出码返回结果。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestAllPlayableCharactersHaveUniformPortraits();
            TestAllPlayableCharacterSkillsAreEligible();
            TestWushuang();
            TestBiyue();
            TestLongdan();
            TestJiAngHitAndMiss();
            TestHunZi();
            TestUnmetConditionsDoNotReport();
            TestEnemyAndNonCharacterSkillsAreFiltered();
            TestResponsiveSafeArea();
            TestLocalizedSkillName();
            await TestInsideBattleScene();
            GD.Print($"SKILL_TRIGGER_PRESENTATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SKILL_TRIGGER_PRESENTATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestAllPlayableCharactersHaveUniformPortraits()
    {
        var expectedSize = CharacterVisualDatabase.PortraitSourceSize;
        foreach (var character in CharacterDatabase.GetAllCharacters())
        {
            var definition = CharacterVisualDatabase.Get(character.Id);
            Assert(definition != null, $"{character.Name} 没有注册角色视觉定义");
            Assert(!string.IsNullOrEmpty(definition!.PortraitSpriteId), $"{character.Name} 没有头像资源 ID");

            var portrait = CharacterVisualDatabase.TryGetPortraitTexture(character.Id);
            Assert(portrait != null, $"{character.Name} 的头像贴图无法加载");
            Assert(portrait!.GetWidth() == expectedSize && portrait.GetHeight() == expectedSize,
                $"{character.Name} 的头像尺寸必须为 {expectedSize}×{expectedSize}");
        }
    }

    private void TestAllPlayableCharacterSkillsAreEligible()
    {
        var checkedSkillIds = new HashSet<string>();
        foreach (var character in CharacterDatabase.GetAllCharacters())
        {
            foreach (var skillId in character.SkillIds)
            {
                if (!checkedSkillIds.Add(skillId))
                {
                    continue;
                }

                var skill = SkillDatabase.GetSkill(skillId);
                Assert(skill != null, $"角色 {character.Id} 的默认技能 {skillId} 不存在");
                Assert(skill!.Category == SkillCategory.CharacterExclusive, $"{skillId} 不是角色专属技能");
                Assert(skill.Source == SkillSource.Character, $"{skillId} 仍被错误标记为 {skill.Source}");
                Assert(skill.CharacterId == character.Id, $"{skillId} 的角色归属不是 {character.Id}");

                var player = CreatePlayer(character.Id, skillId);
                var context = CreateContext(player, CreateEnemy());
                var before = _requests.Count;
                context.ReportPlayerCharacterSkillTriggered(player, skillId, skill.Timing ?? TriggerTiming.OnBattlePhase);
                Assert(_requests.Count == before + 1, $"{skillId} 被统一角色技能表现入口拒绝");
                AssertLast(skillId, skill.Timing ?? TriggerTiming.OnBattlePhase);
            }
        }

        Assert(checkedSkillIds.Count == 39, $"可玩角色默认技能数量发生变化：当前 {checkedSkillIds.Count}，需同步接入新增技能");
    }

    private async Task TestInsideBattleScene()
    {
        var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
        var battle = battleScene.Instantiate<BattleManager>();
        battle.ConfigureDebugMode(CharacterIds.ZhaoYun);
        AddChild(battle);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var queue = battle.GetNode<SkillTriggerToastQueue>("%SkillTriggerToastQueue");
        var visualEffectProbe = new RecordingSkillTriggerVisualEffect();
        Assert(queue.VisualEffectCount == 1, "默认赛博技能特效没有注册");
        queue.RegisterVisualEffect(visualEffectProbe);
        Assert(visualEffectProbe.AttachCount == 1, "扩展技能特效没有挂载到统一 Overlay");
        Assert(queue.VisualEffectCount == 2, "扩展技能特效没有进入统一播放队列");
        var requiredSkillIds = new[]
        {
            SkillIds.Wushuang,
            SkillIds.Biyue,
            SkillIds.Longdan,
            SkillIds.JiAng,
            SkillIds.HunZi
        };

        foreach (var skillId in requiredSkillIds)
        {
            var request = _requests.Find(item => item.SkillId == skillId);
            Assert(request != null, $"实际技能效果没有生成 {skillId} 的场景播放请求");
            queue.Enqueue(request!);
        }

        Assert(queue.AcceptedRequestCount == requiredSkillIds.Length, "Battle 场景中的技能队列未接收完整五种请求");
        var hunZiRequest = _requests.Find(item => item.SkillId == SkillIds.HunZi)!;
        queue.Enqueue(hunZiRequest);
        Assert(queue.AcceptedRequestCount == requiredSkillIds.Length, "同结算链同技能没有去重");
        queue.Enqueue(hunZiRequest with { ResolutionChainId = hunZiRequest.ResolutionChainId + 1000 });
        Assert(queue.AcceptedRequestCount == requiredSkillIds.Length + 1, "不同结算链的独立触发被错误合并");

        await ToSignal(GetTree().CreateTimer(6.9), SceneTreeTimer.SignalName.Timeout);
        Assert(!queue.IsBusy, "五个技能完整动画播放后队列仍未清空");
        Assert(queue.LastPlayedSkillName == SkillDatabase.GetSkill(SkillIds.HunZi)!.DisplayName, "技能提示没有按请求顺序播放");
        Assert(visualEffectProbe.PlayCount == requiredSkillIds.Length + 1, "扩展技能特效没有与每次有效大字同步播放");
        queue.UnregisterVisualEffect(visualEffectProbe);
        Assert(visualEffectProbe.StopCount > 0, "移除扩展技能特效时没有停止动画");
        Assert(visualEffectProbe.DetachCount == 1, "移除扩展技能特效时没有脱离统一 Overlay");
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestWushuang()
    {
        var (player, enemy, context) = CreateContext(CharacterIds.LuBu, SkillIds.Wushuang);
        var skill = SkillDatabase.GetSkill(SkillIds.Wushuang)!;
        Assert(player.HasSkill(SkillIds.Wushuang), "无双测试前提失败：玩家未持有技能");
        Assert(player.Character.Data.Id == skill.CharacterId, $"无双角色归属不一致：player={player.Character.Data.Id}, skill={skill.CharacterId}");
        Assert(skill.Source == SkillSource.Character, $"无双来源不是 Character：{skill.Source}");
        context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, 10);
        new WushuangDamageEffect().Execute(context);
        AssertLast(SkillIds.Wushuang, TriggerTiming.OnDamage);
    }

    private void TestBiyue()
    {
        var (player, _, context) = CreateContext(CharacterIds.DiaoChan, SkillIds.Biyue);
        var manaBefore = player.CurrentMana;
        new BiyueSkillEffect().Execute(context);
        Assert(player.CurrentMana > manaBefore, "闭月测试前提失败：技能没有实际增加费用");
        AssertLast(SkillIds.Biyue, TriggerTiming.OnBattlePostPhase);
    }

    private void TestLongdan()
    {
        var (player, enemy, context) = CreateContext(CharacterIds.ZhaoYun, SkillIds.Longdan);
        context.PlayerAction = BattleAction.FromCard(new Card(CardType.Dodge));
        context.PlayerLockedTarget = enemy;
        context.SetActionForEnemy(enemy, BattleAction.FromCard(new Card(CardType.Kill)));
        new LongdanReactionEffect().Execute(context);
        Assert(context.Reactions.HasPending, "龙胆测试前提失败：没有创建真实反应窗口");
        AssertLast(SkillIds.Longdan, TriggerTiming.OnBattlePostPhase);
    }

    private void TestJiAngHitAndMiss()
    {
        var (player, enemy, context) = CreateContext(CharacterIds.SunCe, SkillIds.JiAng);
        player.DebugSetHealth(40);
        var manaBefore = player.CurrentMana;
        context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, 10)
        {
            ActualDamageDealt = 0
        };
        new JiAngBattleEffect().Execute(context);
        Assert(player.Health == 27, $"激昂失手应失去当前生命值33%（13点），实际生命为{player.Health}");
        Assert(player.CurrentMana > manaBefore, "激昂测试前提失败：失手分支没有实际增加费用");
        AssertLast(SkillIds.JiAng, TriggerTiming.OnDamageTaken);
        Assert(_requests[^1].Variant == "miss", "激昂未命中分支没有携带正确表现变体");

        context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, 10)
        {
            ActualDamageDealt = 10
        };
        new JiAngBattleEffect().Execute(context);
        AssertLast(SkillIds.JiAng, TriggerTiming.OnDamageTaken);
        Assert(_requests[^1].Variant == "hit", "激昂命中分支没有携带正确表现变体");
    }

    private void TestHunZi()
    {
        var (player, enemy, context) = CreateContext(CharacterIds.SunCe, SkillIds.HunZi);
        player.DebugSetHealth(19);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 21)
        {
            ActualDamageDealt = 21
        };
        new HunZiTriggerEffect().Execute(context);
        Assert(player.MaxHealth == 22 && player.Health == 22, "魂姿测试前提失败：实际效果没有成立");
        AssertLast(SkillIds.HunZi, TriggerTiming.OnDamageTaken);

        var requestCount = _requests.Count;
        player.DebugSetMaxHealth(10);
        player.DebugSetHealth(4);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 1)
        {
            ActualDamageDealt = 1
        };
        new HunZiTriggerEffect().Execute(context);
        Assert(_requests.Count == requestCount, "魂姿无法继续触发时仍错误发出表现请求");
    }

    private void TestEnemyAndNonCharacterSkillsAreFiltered()
    {
        var before = _requests.Count;
        var player = CreatePlayer(CharacterIds.LuBu, SkillIds.Wushuang);
        var enemy = CreateEnemy();
        enemy.AddSkill(SkillDatabase.GetSkill(SkillIds.Wushuang)!);
        var context = CreateContext(player, enemy);
        context.ReportPlayerCharacterSkillTriggered(enemy, SkillIds.Wushuang, TriggerTiming.OnDamage);
        Assert(_requests.Count == before, "敌方角色技能错误进入我方大字队列");

        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Plague)!);
        context.ReportPlayerCharacterSkillTriggered(player, SkillIds.Plague, TriggerTiming.OnTurnStart);
        Assert(_requests.Count == before, "通用技能错误进入角色自身技能大字队列");

        player.AddSkill(SkillDatabase.GetSkill(SkillIds.MoonGaze)!);
        context.ReportPlayerCharacterSkillTriggered(player, SkillIds.MoonGaze, TriggerTiming.OnBattlePrePhase);
        Assert(_requests.Count == before, "Boss 来源技能错误进入我方角色自身技能大字队列");
    }

    private void TestUnmetConditionsDoNotReport()
    {
        var requestCount = _requests.Count;
        var (player, enemy, context) = CreateContext(CharacterIds.LuBu, SkillIds.Wushuang);
        context.DamageEvent = new DamageEvent(player, enemy, CardType.Dodge, 0);
        new WushuangDamageEffect().Execute(context);
        Assert(_requests.Count == requestCount, "无双条件未满足时仍发出表现请求");

        var (biyuePlayer, _, biyueContext) = CreateContext(CharacterIds.DiaoChan, SkillIds.Biyue);
        new BiyueSkillEffect().Execute(biyueContext);
        var afterFirstTrigger = _requests.Count;
        new BiyueSkillEffect().Execute(biyueContext);
        Assert(_requests.Count == afterFirstTrigger, "闭月未获得费用的间隔回合仍发出表现请求");
        Assert(biyuePlayer.RuntimeStates.TryGetValue("biyue_ticks", out var ticks) && ticks is 2, "闭月间隔回合测试前提失败");
    }

    private void TestResponsiveSafeArea()
    {
        foreach (var viewport in new[] { new Vector2(1920, 1080), new Vector2(2560, 1440) })
        {
            const float width = 520;
            var position = SkillTriggerToastQueue.CalculateRestPosition(viewport, width);
            Assert(position.X >= 0 && position.X + width <= viewport.X, $"{viewport} 下技能提示超出水平安全区");
            Assert(position.Y >= 0 && position.Y + 112 <= viewport.Y - 200, $"{viewport} 下技能提示侵入底部卡牌区域");
        }
    }

    private void TestLocalizedSkillName()
    {
        var originalLanguage = Localization.CurrentLanguage;
        try
        {
            Localization.SetLanguage("en_US");
            var (player, enemy, context) = CreateContext(CharacterIds.LuBu, SkillIds.Wushuang);
            context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, 10);
            new WushuangDamageEffect().Execute(context);
            AssertLast(SkillIds.Wushuang, TriggerTiming.OnDamage);
            Assert(
                _requests[^1].LocalizedSkillName == SkillDatabase.GetSkill(SkillIds.Wushuang)!.DisplayName,
                "英文环境下表现请求没有使用当前本地化技能名");
        }
        finally
        {
            Localization.SetLanguage(originalLanguage);
        }
    }

    private (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext(string characterId, string skillId)
    {
        var player = CreatePlayer(characterId, skillId);
        var enemy = CreateEnemy();
        return (player, enemy, CreateContext(player, enemy));
    }

    private BattleContext CreateContext(Player player, EnemyInstance enemy)
    {
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter
        };
        context.BeginRoundResult();
        context.PlayerSkillPresentationRequested = request => _requests.Add(request);
        return context;
    }

    private static Player CreatePlayer(string characterId, string skillId)
    {
        var character = CharacterDatabase.GetCharacter(characterId);
        var player = new Player(character.Name, characterId, BattleTeam.Player);
        player.ResetForNewBattle(character.MaxHp, character.MaxHp, 1);
        player.SetCharacter(character);
        player.AddSkill(SkillDatabase.GetSkill(skillId)!);
        return player;
    }

    private static EnemyInstance CreateEnemy()
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = "skill_presentation_enemy",
            Name = "表现测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
    }

    private void AssertLast(string skillId, TriggerTiming timing)
    {
        Assert(_requests.Count > 0, $"{skillId} 没有发出表现请求");
        var request = _requests[^1];
        Assert(request.SkillId == skillId, $"表现请求技能错误：期望 {skillId}，实际 {request.SkillId}");
        Assert(request.Timing == timing, $"{skillId} 的触发时机字段错误");
        Assert(request.Side == BattleTeam.Player, $"{skillId} 的阵营字段错误");
        Assert(!string.IsNullOrWhiteSpace(request.LocalizedSkillName), $"{skillId} 缺少本地化名称");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingSkillTriggerVisualEffect : ISkillTriggerVisualEffect
    {
        public int AttachCount { get; private set; }
        public int PlayCount { get; private set; }
        public int StopCount { get; private set; }
        public int DetachCount { get; private set; }

        public void Attach(Control host)
        {
            AttachCount++;
        }

        public void Play(SkillTriggerVisualEffectContext context)
        {
            PlayCount++;
        }

        public void Stop()
        {
            StopCount++;
        }

        public void Detach()
        {
            DetachCount++;
            Stop();
        }
    }
}
