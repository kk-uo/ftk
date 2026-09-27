//////////////////////////////////////////////////////////
// 文件：Scripts/IntegratedTutorialFlow.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// "整合式首次引导"教程的宏观顺序编排——角色选择→教学地图→教学战斗→奖励→
// 教学商店→背包装备→教学地图(回)→教学事件→结束→正式进入第一章。这一层只
// 决定"下一个该显示哪个真实屏幕"，不重新实现任何屏幕本身：角色选择/地图/
// 战斗/商店/背包/事件全部是 MainFlow 已有的真实 Controller，只是在教学模式
// 下叠加一层 TutorialHighlightLayer 高亮 + 操作范围限制。
//
// 与 TutorialManager 的关系：
// TutorialManager 管的是"战斗内单步教学"这一层（战斗内到底该显示哪张牌的
// 说明）。本类管的是宏观屏幕顺序，两者不是替代关系——教学战斗阶段内部仍然
// 完全交给 TutorialManager/TutorialDatabase/TutorialOverlay 驱动。
//
// 不负责：
// × 任何UI渲染（由 MainFlow 各 Show* 方法 + TutorialHighlightLayer 负责）。
// × 教程完成/跳过状态的磁盘持久化（由 TutorialIntegratedProgress 负责，
//   本类只在合适的时机调用它）。
//////////////////////////////////////////////////////////

using Godot;

public enum IntegratedTutorialStage
{
    None,
    CharacterSelect,
    Map,
    Battle,
    Reward,
    Shop,
    Inventory,
    Event,
    Complete
}

public static class IntegratedTutorialFlow
{
    // 教学地图固定用到的两个节点id——MainFlow.OnNodeSelected 需要用它们判断
    // "当前点的是不是教学节点"，GameManager.BuildTutorialMap 用它们构造节点。
    public const string TutorialBattleNodeId = "tutorial_battle_node";
    public const string TutorialEventNodeId = "tutorial_event_node";
    public const string TutorialEventId = "event_tutorial_roadside_supply";

    // 教学奖励固定数值：金币足够买下下面的教学商店商品（教学商店价格是独立于
    // 真实稀有度经济的固定教学价，不走 ShopManager.GetBuyPrice）。战斗奖励和
    // 商店购买用两件不同的简单装备：合金盾(GangDun)直接随奖励发放，锈矛
    // (RustSword) 需要玩家在商店里花钱买——避免两步教学发的是同一件装备造成
    // "背包里为什么有两个一样的东西"的困惑，也让"购买"这个动作有实际意义。
    // 背包/装备拖拽教学环节用的正是这件刚买到的锈矛。
    public const int RewardGold = 100;
    public const string RewardEquipmentId = EquipmentIds.GangDun;
    public const string ShopTutorialItemId = EquipmentIds.RustSword;
    public const int ShopTutorialItemPrice = 50;

    public static bool IsActive { get; private set; }
    public static IntegratedTutorialStage CurrentStage { get; private set; } = IntegratedTutorialStage.None;

    /// <summary>
    /// 玩家在角色选择教学步骤里点开过详情的角色数量——要求"至少点开一个"
    /// 才允许继续（对应用户§4"点击至少一个角色查看详情"）。
    /// </summary>
    public static bool HasViewedAnyCharacterDetail { get; set; }

    public static void Start()
    {
        IsActive = true;
        CurrentStage = IntegratedTutorialStage.CharacterSelect;
        HasViewedAnyCharacterDetail = false;
        TutorialIntegratedProgress.RewardGranted = false;
        TutorialIntegratedProgress.ShopPurchaseDone = false;
        TutorialIntegratedProgress.EquipDone = false;
        TutorialIntegratedProgress.EventDone = false;
        GD.Print("[IntegratedTutorialFlow] Started.");
    }

    public static void AdvanceTo(IntegratedTutorialStage stage)
    {
        CurrentStage = stage;
        GD.Print($"[IntegratedTutorialFlow] Advance to stage={stage}");
    }

    /// <summary>
    /// 教程完成（走完教学事件）：标记持久化完成状态，重置编排器为非激活。
    /// </summary>
    public static void Complete()
    {
        CurrentStage = IntegratedTutorialStage.Complete;
        IsActive = false;
        TutorialIntegratedProgress.MarkCompleted();
        GD.Print("[IntegratedTutorialFlow] Completed.");
    }

    /// <summary>
    /// 玩家主动跳过：不视为完成，单独标记 skipped（对应用户§14"分别记录
    /// completed / skipped，跳过不等同完成"）。
    /// </summary>
    public static void Skip()
    {
        CurrentStage = IntegratedTutorialStage.None;
        IsActive = false;
        TutorialIntegratedProgress.MarkSkipped();
        GD.Print("[IntegratedTutorialFlow] Skipped.");
    }

    /// <summary>
    /// 供"主菜单重新体验基础教程"入口调用：不依赖已完成/已跳过状态，强制
    /// 重新开始一轮（不影响持久化的 completed/skipped 标记，只在真正走完
    /// 或再次跳过时才会覆盖）。
    /// </summary>
    public static void Reset()
    {
        IsActive = false;
        CurrentStage = IntegratedTutorialStage.None;
        HasViewedAnyCharacterDetail = false;
    }
}
