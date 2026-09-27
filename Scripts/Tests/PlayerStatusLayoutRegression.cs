//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/PlayerStatusLayoutRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证战斗右下角玩家状态 UI 锚点不会被刷新逻辑移动。
// 2. 验证 HP、费用、Buff 与窗口尺寸变化不会改变 PlayerStatusRoot 的 Offset。
// 3. 验证同帧连续刷新和出牌栏数量变化不会拉伸或推动玩家状态卡。
//
// 不负责：
// × 验证战斗数值结算。
// × 验证所有手动交互流程。
//
// 主要依赖：
// Battle.tscn
// BattleManager
// CharacterStatusCard
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 玩家状态 HUD 固定位置的 Headless 回归入口。
/// </summary>
public partial class PlayerStatusLayoutRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 启动测试并通过进程退出码返回结果。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print($"PLAYER_STATUS_LAYOUT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PLAYER_STATUS_LAYOUT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        Localization.Initialize();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        GameManager.SetCurrentNode("battle_1_1");

        var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
        var battle = battleScene.Instantiate<BattleManager>();
        AddChild(battle);

        await NextFrame();
        await NextFrame();

        var root = battle.GetNode<Control>("PlayerStatusLayer/PlayerStatusViewportRoot/PlayerStatusAnchor");
        var card = battle.GetNode<CharacterStatusCard>("PlayerStatusLayer/PlayerStatusViewportRoot/PlayerStatusAnchor/FriendlyPanel/FriendlyContainer/PlayerCard1");
        var hpBar = FindHpBar(card);
        var initialCardWidth = BattleCardLayout.FriendlyCardWidth;
        var initialHpBarWidth = hpBar.Size.X;
        var initialCardPosition = card.GlobalPosition;
        AssertFixedOffsets(root, "初始布局");
        AssertFixedPlayerCardLayout(card, hpBar, initialCardWidth, initialHpBarWidth, initialCardPosition, "初始布局");

        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(120, 120, 1);

        for (var i = 0; i < 100; i++)
        {
            player.DebugSetHealth(120 - i % 100);
            player.DebugSetMana(i % 11);
            card.Refresh(player, null, BattleTeam.Player, false, $"刷新{i}");
            battle._Process(0);
            AssertFixedOffsets(root, $"HP/费用刷新 {i}");
            AssertFixedPlayerCardLayout(card, hpBar, initialCardWidth, initialHpBarWidth, initialCardPosition, $"HP/费用刷新 {i}");
        }

        for (var i = 0; i < 100; i++)
        {
            player.SetFrozen(i % 4);
            player.SetStun((i + 1) % 3);
            card.Refresh(player, null, BattleTeam.Player, false, $"Buff刷新{i}");
            battle._Process(0);
            AssertFixedOffsets(root, $"Buff刷新 {i}");
            AssertFixedPlayerCardLayout(card, hpBar, initialCardWidth, initialHpBarWidth, initialCardPosition, $"Buff刷新 {i}");
        }

        var actionArea = battle.GetNode<HBoxContainer>("%ActionArea");
        var actionCards = actionArea.GetChildren();
        for (var visibleCount = 0; visibleCount <= actionCards.Count; visibleCount++)
        {
            for (var i = 0; i < actionCards.Count; i++)
            {
                if (actionCards[i] is CanvasItem item)
                {
                    item.Visible = i < visibleCount;
                }
            }

            await NextFrame();
            AssertFixedOffsets(root, $"出牌栏数量 {visibleCount}");
            AssertFixedPlayerCardLayout(card, hpBar, initialCardWidth, initialHpBarWidth, initialCardPosition, $"出牌栏数量 {visibleCount}");
        }

        foreach (var child in actionCards)
        {
            if (child is CanvasItem item)
            {
                item.Visible = true;
            }
        }

        GetWindow().Size = new Vector2I(1920, 1080);
        await NextFrame();
        battle._Process(0);
        AssertFixedOffsets(root, "窗口缩小");
        AssertFixedPlayerCardSize(card, hpBar, initialCardWidth, initialHpBarWidth, "窗口缩小");

        GetWindow().Size = new Vector2I(2560, 1440);
        await NextFrame();
        battle._Process(0);
        AssertFixedOffsets(root, "窗口恢复");
        AssertFixedPlayerCardSize(card, hpBar, initialCardWidth, initialHpBarWidth, "窗口恢复");
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void AssertFixedOffsets(Control root, string context)
    {
        Assert(NearlyEqual(root.AnchorLeft, 1.0f), $"{context}: AnchorLeft 不是右下角固定值");
        Assert(NearlyEqual(root.AnchorTop, 1.0f), $"{context}: AnchorTop 不是右下角固定值");
        Assert(NearlyEqual(root.AnchorRight, 1.0f), $"{context}: AnchorRight 不是右下角固定值");
        Assert(NearlyEqual(root.AnchorBottom, 1.0f), $"{context}: AnchorBottom 不是右下角固定值");
        Assert(NearlyEqual(root.OffsetLeft, -420.0f), $"{context}: OffsetLeft 发生移动");
        Assert(NearlyEqual(root.OffsetTop, -217.0f), $"{context}: OffsetTop 发生移动");
        Assert(NearlyEqual(root.OffsetRight, -48.0f), $"{context}: OffsetRight 发生移动");
        Assert(NearlyEqual(root.OffsetBottom, -32.0f), $"{context}: OffsetBottom 发生移动");
    }

    private void AssertFixedPlayerCardLayout(
        CharacterStatusCard card,
        ProgressBar hpBar,
        float expectedCardWidth,
        float expectedHpBarWidth,
        Vector2 expectedPosition,
        string context)
    {
        AssertFixedPlayerCardSize(card, hpBar, expectedCardWidth, expectedHpBarWidth, context);
        Assert(NearlyEqual(card.GlobalPosition.X, expectedPosition.X), $"{context}: 玩家状态卡水平位置发生变化");
        Assert(NearlyEqual(card.GlobalPosition.Y, expectedPosition.Y), $"{context}: 玩家状态卡垂直位置发生变化");
    }

    private void AssertFixedPlayerCardSize(
        CharacterStatusCard card,
        ProgressBar hpBar,
        float expectedCardWidth,
        float expectedHpBarWidth,
        string context)
    {
        Assert(NearlyEqual(card.CustomMinimumSize.X, expectedCardWidth), $"{context}: 玩家状态卡最小宽度发生变化");
        Assert(card.Size.X <= expectedCardWidth + 0.001f, $"{context}: 玩家状态卡被拉伸");
        Assert(NearlyEqual(hpBar.Size.X, expectedHpBarWidth), $"{context}: 玩家血条宽度发生变化");
    }

    private static ProgressBar FindHpBar(Node root)
    {
        return TryFindHpBar(root) ?? throw new InvalidOperationException("找不到玩家状态 UI 的 ProgressBar");
    }

    private static ProgressBar? TryFindHpBar(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is ProgressBar progressBar)
            {
                return progressBar;
            }

            var nested = TryFindHpBar(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.001f;
    }
}
