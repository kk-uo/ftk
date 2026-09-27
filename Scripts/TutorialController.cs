//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialController.cs
//
// 模块：Tutorial System
//
// 职责：
// 1. 作为教程的"逻辑层"：监听真实 BattleManager / TutorialManager / TutorialEventBus
//    的事件，据此推进教程步骤、锁定或解锁输入、给训练傀儡逐回合武装出牌。
// 2. 战斗本身（伤害、AI、回合、牌的合法性）永远由真实 BattleManager 负责，
//    本类只调用 BattleManager.Tutorial.cs 暴露的教程专属公开接口。
//
// 不负责：
// × 维护任何血量/费用/敌人行为的独立副本。
// × 直接绘制界面（界面全部由 TutorialOverlay 负责）。
//
// 主要依赖：
// BattleManager（通过构造函数注入真实实例）、TutorialManager、TutorialEventBus、
// TutorialDatabase
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// Tutorial System 的公开类：TutorialController。
///
/// BattleManager → TutorialOverlay → TutorialController 三层架构中的逻辑层：
/// 只读取/调用真实 BattleManager 暴露的教程接口，不重新实现战斗。
/// </summary>
public sealed class TutorialController
{
    // 每个教学步骤要求训练傀儡本回合展示的卡：null 代表交还随机AI（综合练习），
    // CardType.Fee 代表"敌人按兵不动/等待"——引擎里敌人每回合必须真实出一张牌，
    // 没有"什么都不做"的选项，而【费】不参与任何伤害/偷取结算，是唯一对玩家
    // 绝对无威胁、可以安全代表"等待"的真实卡。
    //
    // 【无懈可击】教学使用【万箭齐发】作为敌方锦囊攻击样例；它会走无懈可击
    // 的真实反制结算分支。顺手牵羊已从初始牌组与教程中移除，不能再作为示例。
    private static readonly Dictionary<string, CardType?> StepEnemyAction = new()
    {
        [TutorialDatabase.StepIds.SimultaneousPlay] = CardType.Fee,
        [TutorialDatabase.StepIds.CardFee] = CardType.Fee,
        [TutorialDatabase.StepIds.CardDodge] = CardType.Kill,
        [TutorialDatabase.StepIds.CardKill] = CardType.Fee,
        [TutorialDatabase.StepIds.CardFireKill] = CardType.Kill,
        [TutorialDatabase.StepIds.CardThunderKill] = CardType.Dodge,
        // 克制关系复习三连回合：敌人每回合固定出"会被玩家这一步打出的牌克制"
        // 的那张牌，让玩家真实体验一次完整的环形克制，而不是只读文字说明。
        [TutorialDatabase.StepIds.CounterRoundA] = CardType.Kill,
        [TutorialDatabase.StepIds.CounterRoundB] = CardType.ThunderKill,
        [TutorialDatabase.StepIds.CounterRoundC] = CardType.FireKill,
        [TutorialDatabase.StepIds.CardPeach] = CardType.Fee,
        [TutorialDatabase.StepIds.CardWine] = CardType.Fee,
        [TutorialDatabase.StepIds.CardWineKill] = CardType.Fee,
        [TutorialDatabase.StepIds.CardUnassailable] = CardType.ArrowBarrage,
        [TutorialDatabase.StepIds.FinalBattle] = null
    };

    // 部分课程在正式教学前会先经过"费用不足，引导玩家自己打费"的 NeedFee
    // 插入步骤（见下方 StepRequiredMana）。这段等待期间训练傀儡该做什么因课
    // 而异（例如火杀课等待期敌人固定出闪、雷杀课等待期敌人固定出费），凡是
    // 没有在这里单独列出的目标课程，默认在等待期用【费】占位，同样代表
    // "按兵不动、不攻击玩家"。
    private static readonly Dictionary<string, CardType?> NeedFeeWaitingAction = new()
    {
        [TutorialDatabase.StepIds.CardFireKill] = CardType.Dodge
    };

    // 每个需要真实费用才能打出的教学步骤要求的费用。进入该步骤前会用玩家
    // 真实（非伪造）的当前费用做比对，不够就先跳去 NeedFee 步骤引导玩家
    // 自己打【费】，凑够了再跳回来——不再由教程直接把费用垫满。
    private static readonly Dictionary<string, double> StepRequiredMana = new()
    {
        [TutorialDatabase.StepIds.CardFireKill] = 2,
        [TutorialDatabase.StepIds.CardThunderKill] = 2,
        [TutorialDatabase.StepIds.CounterRoundA] = 2,
        [TutorialDatabase.StepIds.CounterRoundC] = 2,
        [TutorialDatabase.StepIds.CardPeach] = 2,
        [TutorialDatabase.StepIds.CardWine] = 1,
        [TutorialDatabase.StepIds.CardWineKill] = 1,
        [TutorialDatabase.StepIds.CardUnassailable] = 0.5
    };

    private const int LowHealthForPeachLesson = 20;
    private const int FinalBattleEnemyHealth = 100;
    private const double TutorialPlayerMana = 999;

    private readonly BattleManager _battleManager;
    private bool _finalBattleWinHandled;
    private string? _pendingTargetStepId;
    private bool _waitingForFeeResolution;
    private int _feeActionTurnNumber = -1;

    // 上一次成功写入训练傀儡出牌覆盖时的回合数；只有当前真实回合数严格大于
    // 这个值时才允许再次写入，见 Tick() 内的详细说明。
    private int _armedTurnNumber = -1;

    public TutorialController(BattleManager battleManager)
    {
        _battleManager = battleManager;

        TutorialManager.StepChanged += OnStepChanged;
        TutorialEventBus.CardPlayed += OnCardPlayed;

        // Steps 0/1 are narrative-only; lock input until the player reaches the
        // first taught card so no accidental real card play can consume a round
        // before the tutorial has explained anything.
        _battleManager.SetTutorialInputLocked(true);

        if (TutorialManager.IsActive && TutorialManager.CurrentStep != null)
            OnStepChanged(TutorialManager.CurrentStep);
    }

    public void Shutdown()
    {
        TutorialManager.StepChanged -= OnStepChanged;
        TutorialEventBus.CardPlayed -= OnCardPlayed;
        _battleManager.SetTutorialInputLocked(false);
    }

    /// <summary>
    /// 由 TutorialOverlay 每帧调用一次。真实 BattleManager 的回合/阶段完全在
    /// 自己的 _Process 内独立运行。
    ///
    /// 训练傀儡的出牌覆盖（SetTutorialEnemyAction）在这里、且只在这里被写入，
    /// 不再由 CardPlayed/StepChanged 事件直接触发。原因：点击【杀】等非即时
    /// 卡牌时，真实的回合结算（含读取覆盖值的 BuildEnemyActions）要等一段
    /// 出牌动画播放完之后才会异步继续；TutorialEventBus.CardPlayed 虽然只会
    /// 在行动正式进入结算时触发，但该事件到敌方行动真正读取覆盖值之间仍有
    /// 异步表现阶段。若教程收到 CardPlayed 后立刻把覆盖值改写为下一课的内容，
    /// 仍会让本课回合错误地读取下一课的脚本（例如玩家学习【杀】时敌人却出了
    /// 【杀】——那其实是【火杀】课本该出的值提前泄漏进来）。
    ///
    /// 修复方式：只信任"回合数是否已经真正推进"这一个信号。GetTutorialTurnNumber
    /// 只在 EnterEndPhase 里、一整回合（含 BuildEnemyActions）完全结算完毕后
    /// 才 +1。每帧检查一次：只要当前回合数比"上次成功写入覆盖值时的回合数"
    /// 大，就说明上一次写入的值已经被安全消费掉，这时才允许写入当前教程步骤
    /// 需要的新值。这样无论回合结算需要经过几帧的动画/异步延迟，都不会提前
    /// 覆盖还未被读取的旧值。
    /// </summary>
    public void Tick()
    {
        var currentTurn = _battleManager.GetTutorialTurnNumber();
        ResolvePendingFeeLessonAfterBattle(currentTurn);

        var step = TutorialManager.CurrentStep;
        if (step == null) return;

        _battleManager.SetTutorialInputLocked(step.AdvanceMode == TutorialAdvanceMode.Button);

        if (currentTurn > _armedTurnNumber)
        {
            _battleManager.SetTutorialEnemyAction(GetDesiredEnemyAction(step));
            _armedTurnNumber = currentTurn;
        }

        if (step.Id != TutorialDatabase.StepIds.FinalBattle || _finalBattleWinHandled)
            return;

        var enemyHp = _battleManager.GetTutorialEnemyHealth();
        if (enemyHp != int.MinValue && enemyHp <= 0)
        {
            _finalBattleWinHandled = true;
            TutorialManager.AdvanceToNext();
        }
    }

    private CardType? GetDesiredEnemyAction(TutorialStep step)
    {
        if (step.Id == TutorialDatabase.StepIds.NeedFee)
        {
            return _pendingTargetStepId != null
                ? NeedFeeWaitingAction.GetValueOrDefault(_pendingTargetStepId, CardType.Fee)
                : CardType.Fee;
        }

        return StepEnemyAction.GetValueOrDefault(step.Id);
    }

    private void OnStepChanged(TutorialStep? step)
    {
        if (step == null) return;

        _battleManager.SetTutorialInputLocked(step.AdvanceMode == TutorialAdvanceMode.Button);

        // Redirect to the shared "need fee" interstitial when the player's real
        // (not forced) mana can't afford the card this lesson is about to teach.
        // NeedFee itself is excluded so the recursive StepChanged it triggers
        // below doesn't bounce back into this branch.
        if (step.Id != TutorialDatabase.StepIds.NeedFee &&
            StepRequiredMana.TryGetValue(step.Id, out var requiredMana) &&
            _battleManager.GetTutorialPlayerMana() < requiredMana)
        {
            _pendingTargetStepId = step.Id;
            TutorialManager.PendingFeeLessonTarget = step.ActionTarget;
            TutorialManager.JumpToStep(TutorialDatabase.StepIds.NeedFee);
            return;
        }

        ApplyStepPreconditions(step);
    }

    private void OnCardPlayed(CardType type)
    {
        var step = TutorialManager.CurrentStep;
        if (step == null) return;

        if (step.Id == TutorialDatabase.StepIds.NeedFee && type == CardType.Fee)
        {
            // 【费】的实际资源增加发生在本回合战斗结算中。教程事件现在在
            // 行动确认后发出，因此必须等真实回合结束后再读取费用，否则会
            // 用结算前的旧数值错误地留在 NeedFee 步骤。
            _waitingForFeeResolution = true;
            _feeActionTurnNumber = _battleManager.GetTutorialTurnNumber();
        }
    }

    private void ResolvePendingFeeLessonAfterBattle(int currentTurn)
    {
        if (!_waitingForFeeResolution || currentTurn <= _feeActionTurnNumber)
        {
            return;
        }

        _waitingForFeeResolution = false;
        _feeActionTurnNumber = -1;
        TryResolveFeeRedirect();
    }

    private void TryResolveFeeRedirect()
    {
        var targetStepId = _pendingTargetStepId;
        if (targetStepId == null) return;

        var requiredMana = StepRequiredMana.GetValueOrDefault(targetStepId);
        if (_battleManager.GetTutorialPlayerMana() < requiredMana)
        {
            // Still short after this Fee play; stay on NeedFee and let the
            // player play Fee again as many times as it takes.
            return;
        }

        _pendingTargetStepId = null;
        TutorialManager.PendingFeeLessonTarget = null;
        TutorialManager.JumpToStep(targetStepId);
    }

    /// <summary>
    /// 只保留玩家自己无法通过出牌达成的"场景搭建"（血量偏低、敌人已攒够
    /// 费用），费用是否够用完全交给 <see cref="StepRequiredMana"/> 的重定向
    /// 逻辑处理，不再由教程直接把玩家费用垫满——费用本身就是被教学的资源。
    /// </summary>
    private void ApplyStepPreconditions(TutorialStep step)
    {
        switch (step.Id)
        {
            case TutorialDatabase.StepIds.CardPeach:
                _battleManager.SetTutorialPlayerHealth(LowHealthForPeachLesson);
                break;
            case TutorialDatabase.StepIds.FinalBattle:
                _finalBattleWinHandled = false;
                _battleManager.SetTutorialPlayerHealth(_battleManager.GetTutorialPlayerMaxHealth());
                _battleManager.SetTutorialPlayerMana(TutorialPlayerMana);
                _battleManager.SetTutorialEnemyHealth(FinalBattleEnemyHealth, FinalBattleEnemyHealth);
                break;
        }
    }
}
