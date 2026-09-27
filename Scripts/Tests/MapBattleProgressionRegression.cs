//////////////////////////////////////////////////////////
// Regression Tests: first battle map progression
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// Verifies that completing Chapter 1's first battle advances the map to its
/// event node, and that a duplicate completion cannot alter run progress.
/// </summary>
public partial class MapBattleProgressionRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            GameManager.SelectCharacter(CharacterIds.PangTong);

            GameManager.SetCurrentNode("initial_event");
            GameManager.MarkCurrentNodeCleared();
            Assert(GameManager.IsNodeUnlocked("battle_1"), "initial event did not unlock battle_1");

            GameManager.SetCurrentNode("battle_1");
            GameManager.MarkCurrentStageCleared();
            Assert(GameManager.IsNodeCleared("battle_1"), "battle_1 was not marked cleared");
            Assert(GameManager.IsNodeUnlocked("event_2"), "battle_1 did not unlock event_2");
            Assert(GameManager.CurrentStage == 1, "map progress did not advance to the next event stage");

            var defeatedBeforeDuplicate = GameManager.DefeatedEnemyCount;
            GameManager.MarkCurrentStageCleared();
            Assert(GameManager.DefeatedEnemyCount == defeatedBeforeDuplicate, "a cleared battle could be completed twice");
            Assert(GameManager.IsNodeUnlocked("event_2"), "duplicate completion changed the next-node unlock");

            GD.Print($"MAP_BATTLE_PROGRESSION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MAP_BATTLE_PROGRESSION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
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
