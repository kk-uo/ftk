//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Tutorial.cs
//
// 模块：Core System / Tutorial System 集成层
//
// 职责：
// 1. 仅为新手教程提供一组纯新增的公开接口，用于在真实战斗上做确定性的
//    教学配置（固定敌人、强制敌方本回合出牌、设定血量/费用）。
// 2. 完全复用既有的 debug 字段与既有的敌人出牌覆盖机制
//    （_debugEnemyIds / _debugEnemyNextActionOverrides / Player.DebugSetXxx），
//    不引入任何独立的战斗模拟状态或数值计算。
// 3. 除 BattleManager.cs 中 ConfigureDebugMode 内一处教程钩子调用外，
//    不改动任何既有 BattleManager*.cs 文件的既有逻辑。
//
// 不负责：
// × 计算伤害、判定卡牌合法性、驱动回合/阶段流转（全部仍由既有 BattleManager 完成）。
// × 维护任何教程专属的战斗数值副本。
//
// 主要依赖：
// 项目内既有 BattleManager 私有字段与 Player/EnemyInstance 的 DebugSetXxx 方法
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// Tutorial System 与 Core System 的集成入口：BattleManager 的教程专属公开接口。
///
/// 这些方法只是既有 debug 机制的薄包装，真实的战斗结算、AI 决策、伤害计算
/// 全部仍然经由未被改动的 BattleManager 主流程完成。
/// </summary>
public partial class BattleManager
{
    /// <summary>
    /// 教程战斗的敌人 Id，训练傀儡为纯新增的 EnemyDatabase 条目，
    /// 不影响任何已有敌人数据。
    /// </summary>
    public const string TutorialEnemyId = "training_dummy";

    private const int TutorialPlayerMaxHealth = 40;
    private const double TutorialPlayerInitialMana = 999;

    /// <summary>
    /// ConfigureDebugMode(characterId) 会先按传入的真实角色（目前是
    /// CharacterIds.ZhaoYun）灌入该角色的真实技能列表（_debugSkillIds）。
    /// 教程要求"白板角色"——不触发任何角色技能（例如赵云的龙胆：一旦学会
    /// 该技能，玩家打出【闪】防御后，龙胆会在 EnterBattlePostPhase 把一个
    /// 反应塞进 context.Reactions，弹出一个教程完全不知情的独立反应选择
    /// 窗口，把战斗阶段推进卡住，教程后续步骤永远等不到信号）。
    ///
    /// 这里在 StartBattle() 真正读取 _debugSkillIds 组装 Player.Skills
    /// 之前（ConfigureDebugMode 早于 _Ready()/StartBattle() 执行）把它清空，
    /// 使任何角色技能（龙胆/无双/闭月/连营/洛神/影袭等）在教程战斗里都不会
    /// 被加入 Skills 列表，判定天然为 false，不改动技能/反应系统本体。
    /// </summary>
    private void ApplyTutorialConfiguration()
    {
        _debugEnemyIds[0] = TutorialEnemyId;
        for (var i = 1; i < _debugEnemyIds.Length; i++)
        {
            _debugEnemyIds[i] = null;
        }

        // 白板角色：技能为空、临时战斗状态清零（复用已有的
        // Player.DebugClearSkillsAndTemporaryStates，不改动 Player.cs 本体）。
        _debugSkillIds.Clear();
        _player.DebugClearSkillsAndTemporaryStates();

        // 固定生命值与初始费用，与已装备的真实角色数值无关。
        _debugPlayerMaxHp = TutorialPlayerMaxHealth;
        _debugPlayerHp = TutorialPlayerMaxHealth;
        _debugPlayerMana = TutorialPlayerInitialMana;
        _debugPlayerInvincible = false;
    }

    /// <summary>
    /// 强制 0 号敌人槽位下一回合的出牌。复用既有的
    /// <c>_debugEnemyNextActionOverrides</c> 机制（原本用于调试面板），
    /// 该覆盖在被 <c>TryGetDebugOverrideAction</c> 消费一次后会自动清空，
    /// 因此教程需要在每次玩家出牌后按需重新设置。传入 null 代表交还给
    /// 训练傀儡自身的 ActionWeights 随机决策（用于综合练习环节）。
    /// </summary>
    public void SetTutorialEnemyAction(CardType? cardType, int slotIndex = 0)
    {
        if (slotIndex < 0 || slotIndex >= _debugEnemyNextActionOverrides.Length) return;
        _debugEnemyNextActionOverrides[slotIndex] = cardType;
    }

    /// <summary>
    /// 教程专属：直接设定玩家当前生命值（复用 Player.DebugSetHealth）。
    /// </summary>
    public void SetTutorialPlayerHealth(int hp)
    {
        _player.DebugSetHealth(hp);
        RefreshUi();
    }

    /// <summary>
    /// 教程专属：直接设定玩家当前费用（复用 Player.DebugSetMana）。
    /// </summary>
    public void SetTutorialPlayerMana(double mana)
    {
        _player.DebugSetMana(mana);
        RefreshUi();
    }

    public int GetTutorialPlayerMaxHealth() => _player.MaxHealth;

    /// <summary>
    /// 教程专属：查询当前回合数（_turnNumber 只在 EnterEndPhase 里、一整个
    /// 回合——含 BuildEnemyActions 的敌方出牌决策——完全结算完毕之后才 +1）。
    /// 教程用它判断"某一回合是否已经真正跑完"，从而只在安全的时间点重新
    /// 设置 SetTutorialEnemyAction 的覆盖值，避免在上一回合的结算还没跑到
    /// BuildEnemyActions 之前就提前把覆盖值改写成下一课的内容。
    /// </summary>
    public int GetTutorialTurnNumber() => _turnNumber;

    /// <summary>
    /// 教程专属：查询玩家当前真实费用，供教程判断"费用是否足够打出当前教学卡牌"，
    /// 不足时引导玩家先打【费】，而不是替玩家把费用垫满。
    /// </summary>
    public double GetTutorialPlayerMana() => _player.CurrentMana;

    /// <summary>
    /// 教程专属：重开这场教程战斗（复用既有的 RestartBattle，即"刷新回合"调试按钮
    /// 背后调用的同一个方法——它只是重新执行一次 StartBattle()，会重新读取
    /// ApplyTutorialConfiguration() 已经写好的训练傀儡与白板角色配置，
    /// 不需要再次调用 ConfigureDebugMode）。用于"重新体验教程"。
    /// </summary>
    public void RestartTutorialBattle() => RestartBattle();

    /// <summary>
    /// 教程专属：直接设定 0 号敌人槽位的当前费用
    /// （EnemyInstance 继承自 Player，同样拥有 DebugSetMana）。
    /// </summary>
    public void SetTutorialEnemyMana(double mana, int slotIndex = 0)
    {
        if (slotIndex < 0 || slotIndex >= _encounter.Enemies.Count) return;
        _encounter.Enemies[slotIndex].DebugSetMana(mana);
        RefreshUi();
    }

    /// <summary>
    /// 教程专属：直接设定/重置 0 号敌人槽位的生命值与生命上限，
    /// 用于综合练习环节给训练傀儡一个全新的、与教学环节无关的血量池。
    /// </summary>
    public void SetTutorialEnemyHealth(int hp, int maxHp, int slotIndex = 0)
    {
        if (slotIndex < 0 || slotIndex >= _encounter.Enemies.Count) return;
        var enemy = _encounter.Enemies[slotIndex];
        enemy.DebugSetMaxHealth(maxHp);
        enemy.DebugSetHealth(hp);
        RefreshUi();
    }

    /// <summary>
    /// 教程专属：查询 0 号敌人槽位当前生命值，教程用它判定综合练习是否胜利。
    /// 敌人槽位尚未生成时返回 int.MinValue（教程调用方应据此忽略本次查询，
    /// 与"生命值已耗尽（&lt;=0）"区分开）。
    /// </summary>
    public int GetTutorialEnemyHealth(int slotIndex = 0)
    {
        if (slotIndex < 0 || slotIndex >= _encounter.Enemies.Count) return int.MinValue;
        return _encounter.Enemies[slotIndex].CurrentHP;
    }

    /// <summary>
    /// 教程专属：锁定/解锁玩家的出牌输入（复用既有 <c>_inputLocked</c> 字段，
    /// 该字段本就是既有战斗流程用来在弹窗/结算期间临时禁用输入的开关）。
    /// </summary>
    public void SetTutorialInputLocked(bool locked)
    {
        _inputLocked = locked;
    }

    /// <summary>
    /// 教程专属：返回出牌区当前展示的、类型匹配的真实 ActionSlot 屏幕矩形，
    /// 供 TutorialOverlay 的高亮箭头定位。直接复用既有的私有 <c>_actionSlots</c>
    /// 数组，不新增任何节点分组或场景结构。
    /// </summary>
    public Rect2 GetTutorialActionCardRect(CardType type)
    {
        foreach (var slot in _actionSlots)
        {
            if (slot != null && IsInstanceValid(slot) && slot.Card?.Type == type)
                return slot.GetGlobalRect();
        }
        return default;
    }
}
