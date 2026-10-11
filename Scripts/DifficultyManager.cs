//////////////////////////////////////////////////////////
// 文件：Scripts/DifficultyManager.cs
//
// 模块：Difficulty System
//
// 职责：
// 1. 保存全角色共用的永久难度解锁进度。
// 2. 持有当前 Run 选择的难度，并提供累加式规则查询。
// 3. 集中提供终章、Boss生命、商店价格与传奇概率规则。
//////////////////////////////////////////////////////////

using Godot;
using System;

public enum DifficultyLevel
{
    None = 0,
    Difficulty1 = 1,
    Difficulty2 = 2,
    Difficulty3 = 3,
    Difficulty4 = 4,
    Difficulty5 = 5,
    Difficulty6 = 6
}

public static class DifficultyManager
{
    private const string SavePath = "user://difficulty_progress.cfg";
    private const string SaveSection = "difficulty";
    private const string MaxUnlockedKey = "max_unlocked";
    public const DifficultyLevel MaximumDifficulty = DifficultyLevel.Difficulty6;

    public static DifficultyLevel CurrentDifficulty { get; private set; } = DifficultyLevel.None;
    public static DifficultyLevel MaxUnlockedDifficulty { get; private set; } = DifficultyLevel.None;

    public static bool ChapterVariantsEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty1;
    public static bool BossHealthBonusesEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty2;
    public static bool TimeCollapseEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty3;
    public static bool BlackMarketEventOptionEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty3;
    public static bool FactionFateEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty4;
    public static bool ReducedStartingPowerEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty4;
    public static bool EconomyModifiersEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty5;
    public static bool BossReinforcementsEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty6;
    public static bool ChapterThreeRareSkillChoiceEnabled => CurrentDifficulty >= DifficultyLevel.Difficulty6;
    public static int FinalChapter => CurrentDifficulty >= DifficultyLevel.Difficulty2 ? 4 : 3;
    public static int InitialPower => ReducedStartingPowerEnabled ? 80 : GameManager.InitialMaxPower;

    public static void Initialize()
    {
        var config = new ConfigFile();
        if (config.Load(SavePath) != Error.Ok)
        {
            MaxUnlockedDifficulty = DifficultyLevel.None;
            CurrentDifficulty = DifficultyLevel.None;
            return;
        }

        var savedValue = config.GetValue(SaveSection, MaxUnlockedKey, 0).AsInt32();
        MaxUnlockedDifficulty = (DifficultyLevel)Math.Clamp(savedValue, 0, (int)MaximumDifficulty);
        CurrentDifficulty = DifficultyLevel.None;
    }

    public static void ResetCurrentRun() => CurrentDifficulty = DifficultyLevel.None;

    /// <summary>
    /// 正式模式只允许选择永久解锁范围；开发者模式临时开放全部难度用于测试，
    /// 但不会提高或写入 MaxUnlockedDifficulty。
    /// </summary>
    public static bool IsUnlocked(DifficultyLevel difficulty) =>
        Enum.IsDefined(difficulty)
        && (DeveloperModeManager.IsDeveloperMode || difficulty <= MaxUnlockedDifficulty);

    public static bool TrySelect(DifficultyLevel difficulty)
    {
        if (!Enum.IsDefined(difficulty) || !IsUnlocked(difficulty))
        {
            return false;
        }

        CurrentDifficulty = difficulty;
        return true;
    }

    /// <summary>
    /// 最终章节胜利时调用。通关当前最高已开放难度后永久解锁下一档；重复通关不倒退。
    /// </summary>
    public static bool RecordCurrentDifficultyCleared()
    {
        if (CurrentDifficulty >= MaximumDifficulty)
        {
            return false;
        }

        var next = (DifficultyLevel)((int)CurrentDifficulty + 1);
        if (next <= MaxUnlockedDifficulty)
        {
            return false;
        }

        MaxUnlockedDifficulty = next;
        Save();
        return true;
    }

    public static int GetBossHealthBonus(int chapter)
    {
        if (!BossHealthBonusesEnabled)
        {
            return 0;
        }

        return chapter switch
        {
            1 => 10,
            2 => 30,
            3 => 100,
            4 => 200,
            _ => 0
        };
    }

    public static int GetBaseShopPrice(EquipmentRarity rarity)
    {
        if (!EconomyModifiersEnabled)
        {
            return rarity switch
            {
                EquipmentRarity.Common => 75,
                EquipmentRarity.Rare => 150,
                EquipmentRarity.Epic => 300,
                EquipmentRarity.Legendary => 600,
                _ => 0
            };
        }

        return rarity switch
        {
            EquipmentRarity.Common => 100,
            EquipmentRarity.Rare => 200,
            EquipmentRarity.Epic => 400,
            EquipmentRarity.Legendary => 800,
            _ => 0
        };
    }

    public static double GetDifficultyLegendaryShopChance(int chapter)
    {
        if (!EconomyModifiersEnabled)
        {
            return 0.0;
        }

        return chapter switch
        {
            2 => 0.02,
            3 => 0.04,
            >= 4 => 0.10,
            _ => 0.0
        };
    }

    /// <summary>难度6主Boss战追加的护卫；其它章节不追加单位。</summary>
    public static string? GetBossCompanionEnemyId(int chapter)
    {
        if (!BossReinforcementsEnabled)
        {
            return null;
        }

        return chapter switch
        {
            1 => "gate_guard",
            2 => "steel_guard",
            _ => null
        };
    }

    /// <summary>难度6主Boss额外携带的装备；只装备到遭遇中的第一名Boss。</summary>
    public static string? GetBossBonusEquipmentId(int chapter)
    {
        if (!BossReinforcementsEnabled)
        {
            return null;
        }

        return chapter switch
        {
            3 => EquipmentIds.ClampExoskeleton,
            4 => EquipmentIds.TimeHourglass,
            _ => null
        };
    }

    private static void Save()
    {
        var config = new ConfigFile();
        config.SetValue(SaveSection, MaxUnlockedKey, (int)MaxUnlockedDifficulty);
        var error = config.Save(SavePath);
        if (error != Error.Ok)
        {
            GD.PushError($"[DifficultyManager] 难度进度保存失败：{error}");
        }
    }

    internal static void ResetForTest(DifficultyLevel maxUnlocked = DifficultyLevel.Difficulty6)
    {
        MaxUnlockedDifficulty = maxUnlocked;
        CurrentDifficulty = DifficultyLevel.None;
    }
}
