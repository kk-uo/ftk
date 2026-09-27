//////////////////////////////////////////////////////////
// 文件：Scripts/StageDatabase.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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

using System;
using System.Collections.Generic;

/// <summary>
/// Core System 的公开类：StageDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class StageDatabase
{
    private static readonly IReadOnlyList<StageDefinition> Stages = new[]
    {
        CreateStageOneOne(),
        CreateStageOneTwo(),
        CreateStageOneThree(),
        CreateStageOneFour(),
        CreateStageOneFive(),       // 第一章精英关：仅从第一章精英池抽取
        CreateStageTwoFive(),       // 第二章精英关：预留接口，池为空直到第二章精英实装
        CreateStageThreeFive(),     // 第三章精英关：预留接口，池为空直到第三章精英实装
        CreateStageThreeOne(),      // 第三章皇宫路线第一关
        CreateStageThreeThree(),    // 第三章皇宫路线第三关
        CreateStageOneSevenBoss(),
        CreateStageOneSevenBossExtra(), // 魏·双线征伐 专用：第一章额外Boss
        CreateStageTwoOne(),        // 第二章第一关（钢铁卫士 / 改造打手）
        CreateStageTwoThree(),      // 第二章第三关（钢铁卫士 / 改造打手，混合组合）
        CreateStageTwoEightBoss(),   // 第二章 Boss 关（卧龙集智体）
        CreateStageTwoEightBossExtra(), // 魏·双线征伐 专用：第二章额外Boss
        CreateStageThreeEightBoss(), // 第三章 Boss 关（皇宫路线）
        CreateStageFourEightBoss(),  // 第四章 Boss 关（幻象触手）
        CreateStageFourOne(),        // 第四章·深渊第一关（三选一，占位编队）
        CreateStageFourThree(),      // 第四章·深渊第三关（四选一，占位编队）
        CreateStageFourFive()        // 第四章精英关（占位编队）
    };

    /// <summary>
    /// Core System 的公开入口：GetStage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static StageDefinition? GetStage(string stageId)
    {
        foreach (var stage in Stages)
        {
            if (stage.StageId == stageId)
            {
                return stage;
            }
        }

        return null;
    }

    /// <summary>
    /// Core System 的公开入口：GetAllStages。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<StageDefinition> GetAllStages()
    {
        return Stages;
    }

    /// <summary>
    /// Core System 的公开入口：RollEncounter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EncounterEntry? RollEncounter(string stageId)
    {
        var stage = GetStage(stageId);
        if (stage == null || stage.EncounterPool.Count == 0)
        {
            return null;
        }

        var totalWeight = 0.0;
        foreach (var entry in stage.EncounterPool)
        {
            if (entry.Weight > 0 && IsEntryAllowedForRoute(entry))
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        var roll = Random.Shared.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var entry in stage.EncounterPool)
        {
            if (entry.Weight <= 0 || !IsEntryAllowedForRoute(entry))
            {
                continue;
            }

            cumulative += entry.Weight;
            if (roll <= cumulative)
            {
                return entry;
            }
        }

        // 回退到最后一个通过路线筛选的条目。
        for (var i = stage.EncounterPool.Count - 1; i >= 0; i--)
        {
            if (stage.EncounterPool[i].Weight > 0 && IsEntryAllowedForRoute(stage.EncounterPool[i]))
                return stage.EncounterPool[i];
        }

        return null;
    }

    /// <summary>
    /// Core System 的公开入口：GetAllowedEncounters。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EncounterEntry> GetAllowedEncounters(string stageId)
    {
        var stage = GetStage(stageId);
        if (stage == null || stage.EncounterPool.Count == 0)
        {
            return Array.Empty<EncounterEntry>();
        }

        var allowed = new List<EncounterEntry>();
        foreach (var entry in stage.EncounterPool)
        {
            if (entry.Weight > 0 && IsEntryAllowedForRoute(entry))
            {
                allowed.Add(entry);
            }
        }

        return allowed;
    }

    // 返回 false 表示当前路线禁止该遭遇组合（含有不属于当前路线的敌人）。
    private static bool IsEntryAllowedForRoute(EncounterEntry entry)
    {
        var route = GameManager.CurrentChapterRoute;

        foreach (var enemyId in entry.EnemyIds)
        {
            var def = EnemyDatabase.GetEnemy(enemyId);
            if (def == null)
            {
                continue;
            }

            // 下水道路线：禁止生成城市路线专属敌人。
            if (route == ChapterRoute.Sewer && def.Tags.Contains(EnemyTag.RouteCity))
            {
                return false;
            }

            // 下水道专属敌人不出现在非下水道路线。
            if (route != ChapterRoute.Sewer && def.Tags.Contains(EnemyTag.RouteSewer))
            {
                return false;
            }

            // 皇宫路线专属敌人不出现在其他路线。
            if (route != ChapterRoute.Imperial && def.Tags.Contains(EnemyTag.RouteImperial))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Core System 的公开入口：RollEncounterForNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EncounterEntry? RollEncounterForNode(string nodeId)
    {
        var stageId = GetStageIdForNode(nodeId);
        return string.IsNullOrWhiteSpace(stageId) ? null : RollEncounter(stageId);
    }

    /// <summary>
    /// Core System 的公开入口：RollEncounterForNode（节点重载）。
    ///
    /// 电量系统·继续探索节点专用：优先用 <see cref="MapNode.StageIdOverride"/>，
    /// 其余节点行为与字符串重载完全一致。
    /// </summary>
    public static EncounterEntry? RollEncounterForNode(MapNode node)
    {
        var stageId = GetStageIdForNode(node);
        return string.IsNullOrWhiteSpace(stageId) ? null : RollEncounter(stageId);
    }

    /// <summary>
    /// Core System 的公开入口：GetBossStageIdForChapter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetBossStageIdForChapter(int chapter)
    {
        return chapter switch
        {
            1 => "1-7",
            2 => "2-8",
            3 => "3-8",
            4 => "4-8",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetStageIdForNode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetStageIdForNode(string nodeId)
    {
        return nodeId switch
        {
            "battle_1" => "1-1",
            "battle_3" => "1-3",
            "battle_5" => "1-5",    // 第一章精英节点 → 第一章精英池
            "boss_1"   => "1-7",
            // ── 第二章默认路线节点 ────────────────────────────────────────────────
            "battle_2_1" => "2-1",
            "battle_2_3" => "2-3",
            // ── 第二章下水道路线节点（当前内容与默认路线相同，预留独立扩展空间）────
            "sewer_battle_2_1" => "2-1",
            "sewer_battle_2_3" => "2-3",
            // ── 第二章精英节点
            "battle_2_5"       => "2-5",
            "sewer_battle_2_5" => "2-5",
            // ── 第二章 Boss 节点
            "boss_2" => "2-8",
            // ── 第三章皇宫路线节点
            "battle_3_1" => "3-1",
            "battle_3_3" => "3-3",
            "battle_3_5" => "3-5",
            "imperial_battle_3_1" => "3-1",
            "imperial_battle_3_3" => "3-3",
            "imperial_battle_3_5" => "3-5",
            "imperial_boss_3" => "3-8",
            "boss_3" => "3-8",
            // ── 第四章·深渊节点
            "battle_4_1" => "4-1",
            "battle_4_3" => "4-3",
            "battle_4_5" => "4-5",
            "boss_4" => "4-8",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetStageIdForNode（节点重载）。
    ///
    /// 电量系统·继续探索节点专用：动态生成的节点 Id 各不相同，无法进原有的
    /// Id→StageId 静态表；优先读取 <see cref="MapNode.StageIdOverride"/>，
    /// 未设置时完全回退到既有的字符串重载，不影响任何既有节点的行为。
    /// </summary>
    public static string GetStageIdForNode(MapNode node)
    {
        return !string.IsNullOrWhiteSpace(node.StageIdOverride)
            ? node.StageIdOverride
            : GetStageIdForNode(node.Id);
    }

    private static StageDefinition CreateStageOneOne()
    {
        return new StageDefinition
        {
            StageId = "1-1",
            DisplayName = "第一关",
            EncounterPool = new List<EncounterEntry>
            {
                // 组合1：拾荒者
                new() { Weight = 25, EnemyIds = new List<string> { "scavenger" } },
                // 组合2：城关守卫
                new() { Weight = 25, EnemyIds = new List<string> { "gate_guard" } },
                // 组合3：废弃机仆
                new() { Weight = 25, EnemyIds = new List<string> { "abandoned_servant" } },
                // 组合4：羽卫×2（两个独立羽卫）
                new() { Weight = 25, EnemyIds = new List<string> { "feather_guard", "feather_guard" } }
            }
        };
    }

    private static StageDefinition CreateStageOneTwo()
    {
        return new StageDefinition
        {
            StageId = "1-2",
            DisplayName = "第二关",
            EncounterPool = new List<EncounterEntry>
            {
                new()
                {
                    Weight = 45,
                    EnemyIds = new List<string> { "gate_guard", "feather_guard" }
                },
                new()
                {
                    Weight = 35,
                    EnemyIds = new List<string> { "scavenger", "gate_guard" }
                },
                new()
                {
                    Weight = 20,
                    EnemyIds = new List<string> { "feather_guard", "feather_guard" }
                }
            }
        };
    }

    private static StageDefinition CreateStageOneThree()
    {
        return new StageDefinition
        {
            StageId = "1-3",
            DisplayName = "第三关",
            EncounterPool = CreateChapterOneMidEncounterPool()
        };
    }

    private static StageDefinition CreateStageOneFour()
    {
        return new StageDefinition
        {
            StageId = "1-4",
            DisplayName = "第四关",
            EncounterPool = CreateChapterOneMidEncounterPool()
        };
    }

    // 精英关 1-5：仅从第一章精英池抽取，禁止混入普通怪或 Boss。
    private static StageDefinition CreateStageOneFive()
    {
        return new StageDefinition
        {
            StageId = "1-5",
            DisplayName = "精英关（第一章）",
            EncounterPool = CreateChapterOneElitePool()
        };
    }

    // 精英关 2-5：预留接口，精英怪实装前池为空（RollEncounter 返回 null，战斗不生成遭遇）。
    // 禁止回退到普通怪池；实装时向 CreateChapterTwoElitePool() 添加 EncounterEntry 即可。
    private static StageDefinition CreateStageTwoFive()
    {
        return new StageDefinition
        {
            StageId = "2-5",
            DisplayName = "精英关（第二章）",
            EncounterPool = CreateChapterTwoElitePool()
        };
    }

    // 精英关 3-5：预留接口，精英怪实装前池为空。
    private static StageDefinition CreateStageThreeFive()
    {
        return new StageDefinition
        {
            StageId = "3-5",
            DisplayName = "精英关（第三章）",
            EncounterPool = CreateChapterThreeElitePool()
        };
    }

    private static StageDefinition CreateStageOneSevenBoss()
    {
        return new StageDefinition
        {
            StageId = "1-7",
            DisplayName = "Boss关",
            EncounterPool = new List<EncounterEntry>
            {
                new()
                {
                    Weight = 100,
                    EnemyIds = new List<string> { "feather_guard", "boss_healer", "feather_guard" }
                },
                new()
                {
                    Weight = 100,
                    // 背叛者改为单独出场；城关守卫仍保留在第一章普通遭遇池中。
                    EnemyIds = new List<string> { "boss_traitor" }
                },
                new()
                {
                    Weight = 100,
                    EnemyIds = new List<string> { "crazy_performer" }
                }
            }
        };
    }

    // 魏·双线征伐 专用：第一章额外Boss，单敌人候选来自 1-7 主Boss池里唯一的单敌人条目
    // （crazy_performer）。数据出处见 Scripts/FactionFate/WeiExtraBossData.cs 注释。
    private static StageDefinition CreateStageOneSevenBossExtra()
    {
        return new StageDefinition
        {
            StageId = "1-7-extra",
            DisplayName = "额外Boss关（第一章）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 100, EnemyIds = new List<string> { "crazy_performer" } }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 普通战斗遭遇池（第一章中期：1-3 / 1-4）
    // 注意：此池不包含精英怪——精英怪已独立至 CreateChapterOneElitePool()。
    // ──────────────────────────────────────────────────────────────────────────
    private static List<EncounterEntry> CreateChapterOneMidEncounterPool()
    {
        return new List<EncounterEntry>
        {
            // 组合1：拾荒者 + 城关守卫
            new() { Weight = 1, EnemyIds = new List<string> { "scavenger", "gate_guard" } },
            // 组合2：城关守卫 + 羽卫
            new() { Weight = 1, EnemyIds = new List<string> { "gate_guard", "feather_guard" } },
            // 组合3：废弃机仆 + 废弃机仆
            new() { Weight = 1, EnemyIds = new List<string> { "abandoned_servant", "abandoned_servant" } },
            // 组合4：城关守卫 + 城关守卫
            new() { Weight = 1, EnemyIds = new List<string> { "gate_guard", "gate_guard" } },
            // 组合5：废弃机仆 + 拾荒者
            new() { Weight = 1, EnemyIds = new List<string> { "abandoned_servant", "scavenger" } },
            // 组合6：羽卫×3（三个独立羽卫）
            new() { Weight = 1, EnemyIds = new List<string> { "feather_guard", "feather_guard", "feather_guard" } }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 精英遭遇池 — 每个精英关只抽本章精英，严禁混入普通怪或 Boss。
    // ──────────────────────────────────────────────────────────────────────────

    // 第一章精英池：收尸人（青钢剑-锈，SureKill）/ 追捕者（钩索）。
    private static List<EncounterEntry> CreateChapterOneElitePool()
    {
        return new List<EncounterEntry>
        {
            new()
            {
                Weight = 33,
                EnemyIds = new List<string> { "corpse_collector" }
            },
            new()
            {
                Weight = 33,
                EnemyIds = new List<string> { "hunter" }
            },
            new()
            {
                Weight = 34,
                EnemyIds = new List<string> { "exile_barbarian" }
            }
        };
    }

    private static List<EncounterEntry> CreateChapterTwoElitePool()
    {
        return new List<EncounterEntry>
        {
            new() { Weight = 30, EnemyIds = new List<string> { "collective_intelligence" } },
            new() { Weight = 25, EnemyIds = new List<string> { "shixin_zhe" } },
            new() { Weight = 25, EnemyIds = new List<string> { "cyclops" } },
            new() { Weight = 30, EnemyIds = new List<string> { "yellow_turban_devotee" } }
            // 鼠王仅通过【鼠王】事件进入战斗，不出现在精英池。
        };
    }

    // 第三章精英池（皇宫路线）：寒冰护卫组 / 皇家死侍组。
    private static List<EncounterEntry> CreateChapterThreeElitePool()
    {
        return new List<EncounterEntry>
        {
            new() { Weight = 50, EnemyIds = new List<string> { "ice_guard", "gunner", "gunner" } },
            new() { Weight = 50, EnemyIds = new List<string> { "royal_death_guard", "mihuan_xiao_shou", "mihuan_xiao_shou" } }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 第二章普通战斗关（2-1 / 2-3）：钢铁卫士 + 改造打手
    // StageRange 整数：2-1 = 8，2-3 = 10
    // ──────────────────────────────────────────────────────────────────────────

    private static StageDefinition CreateStageTwoOne()
    {
        return new StageDefinition
        {
            StageId = "2-1",
            DisplayName = "第二章第一关",
            EncounterPool = new List<EncounterEntry>
            {
                // 组合A：重装前排——钢铁卫士 + 拾荒者
                new() { Weight = 40, EnemyIds = new List<string> { "steel_guard", "scavenger" } },
                // 组合B：爆发+AOE——改造打手 + 羽卫 ×2
                new() { Weight = 40, EnemyIds = new List<string> { "modified_thug", "feather_guard", "feather_guard" } },
                // 组合E（城市专属）：酒徒 + 改造打手
                new() { Weight = 20, EnemyIds = new List<string> { "wine_drinker", "modified_thug" } },
                // 组合C（下水道专属）：巨型脓包 ×2
                new() { Weight = 50, EnemyIds = new List<string> { "giant_pus_sac", "giant_pus_sac" } },
                // 组合D（下水道专属）：巨型机械蟑螂
                new() { Weight = 50, EnemyIds = new List<string> { "giant_mech_cockroach" } },
                // 组合F（下水道专属）：机械巨鼠
                new() { Weight = 30, EnemyIds = new List<string> { "giant_mech_rat" } }
            }
        };
    }

    private static StageDefinition CreateStageTwoThree()
    {
        return new StageDefinition
        {
            StageId = "2-3",
            DisplayName = "第二章第三关",
            EncounterPool = new List<EncounterEntry>
            {
                // 组合A：高生存压力——钢铁卫士 ×2 + 城关守卫
                new() { Weight = 34, EnemyIds = new List<string> { "steel_guard", "steel_guard", "gate_guard" } },
                // 组合B：辅助强化爆发——改造打手 + 酒徒 ×2
                new() { Weight = 33, EnemyIds = new List<string> { "modified_thug", "wine_drinker", "wine_drinker" } },
                // 组合C：纯爆发速攻——改造打手 ×3
                new() { Weight = 33, EnemyIds = new List<string> { "modified_thug", "modified_thug", "modified_thug" } },
                // 组合D（下水道专属）：巨型脓包 ×3
                new() { Weight = 34, EnemyIds = new List<string> { "giant_pus_sac", "giant_pus_sac", "giant_pus_sac" } },
                // 组合E（下水道专属）：巨型机械蟑螂 ×2
                new() { Weight = 34, EnemyIds = new List<string> { "giant_mech_cockroach", "giant_mech_cockroach" } },
                // 组合F（下水道专属）：机械巨鼠 ×2
                new() { Weight = 25, EnemyIds = new List<string> { "giant_mech_rat", "giant_mech_rat" } }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 第三章皇宫路线普通战斗关（3-1 / 3-3）：StageRange 整数 3-1=16，3-3=18
    // ──────────────────────────────────────────────────────────────────────────

    private static StageDefinition CreateStageThreeOne()
    {
        return new StageDefinition
        {
            StageId = "3-1",
            DisplayName = "第三章第一关（皇宫）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 34, EnemyIds = new List<string> { "mihuan_xiao_shou", "mihuan_xiao_shou", "mihuan_xiao_shou" } },
                new() { Weight = 33, EnemyIds = new List<string> { "gunner", "gunner", "renwan_guard" } },
                new() { Weight = 33, EnemyIds = new List<string> { "renwan_guard", "renwan_guard", "renwan_guard" } }
            }
        };
    }

    private static StageDefinition CreateStageThreeThree()
    {
        return new StageDefinition
        {
            StageId = "3-3",
            DisplayName = "第三章第三关（皇宫）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 34, EnemyIds = new List<string> { "heavy_armor_guard", "gunner", "gunner" } },
                new() { Weight = 33, EnemyIds = new List<string> { "renwan_guard", "heavy_armor_guard", "heavy_armor_guard" } },
                new() { Weight = 33, EnemyIds = new List<string> { "heavy_armor_guard", "mihuan_xiao_shou", "mihuan_xiao_shou" } }
            }
        };
    }

    // 第二章 Boss 关（2-8）：城市路线→卧龙集智体/左·慈/帮派头目；下水道路线→黄衣之主。
    private static StageDefinition CreateStageTwoEightBoss()
    {
        return new StageDefinition
        {
            StageId = "2-8",
            DisplayName = "Boss关（第二章）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 100, EnemyIds = new List<string> { "wulong_collective_intelligence" } },
                new() { Weight = 100, EnemyIds = new List<string> { "zuoci" } },
                new() { Weight = 100, EnemyIds = new List<string> { "gang_boss" } },
                // 下水道路线专属 Boss
                new() { Weight = 100, EnemyIds = new List<string> { "huang_yi_zhi_zhu" } }
            }
        };
    }

    // 魏·双线征伐 专用：第二章额外Boss，单敌人候选来自 2-8 主Boss池里的四个单敌人条目
    // （2-8 全部条目都是单敌人，直接复用同一份数据）。数据出处见 WeiExtraBossData.cs 注释。
    private static StageDefinition CreateStageTwoEightBossExtra()
    {
        return new StageDefinition
        {
            StageId = "2-8-extra",
            DisplayName = "额外Boss关（第二章）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 100, EnemyIds = new List<string> { "wulong_collective_intelligence" } },
                new() { Weight = 100, EnemyIds = new List<string> { "zuoci" } },
                new() { Weight = 100, EnemyIds = new List<string> { "gang_boss" } },
                new() { Weight = 100, EnemyIds = new List<string> { "huang_yi_zhi_zhu" } }
            }
        };
    }

    private static StageDefinition CreateStageThreeEightBoss()
    {
        return new StageDefinition
        {
            StageId = "3-8",
            DisplayName = "Boss关（第三章）",
            EncounterPool = new List<EncounterEntry>
            {
                // 蜀汉共生体：刘备 + 关羽 + 张飞，共享血池
                new() { Weight = 50, EnemyIds = new List<string> { "liu_bei", "guan_yu", "zhang_fei" } },
                // 暴虐昏君 + 羽卫护卫
                new() { Weight = 50, EnemyIds = new List<string> { "boss_tyrant", "feather_guard", "feather_guard" } }
            }
        };
    }

    private static StageDefinition CreateStageFourEightBoss()
    {
        return new StageDefinition
        {
            StageId = "4-8",
            DisplayName = "Boss关（第四章）",
            EncounterPool = new List<EncounterEntry>
            {
                new() { Weight = 100, EnemyIds = new List<string> { "huan_xiang_chu_shou" } }
            }
        };
    }

    // 第四章·深渊：普通敌人遭遇池——4-1 三选一、4-3 四选一，两关配方不同（占位编队，
    // 待正式内容替换）。深渊共生体固定3只同场出现，配合 UseSharedHealthPool=true
    // 自动共享同一条血条。4-1/4-3 这两个节点在建图时（GameManager.BuildChapterFourMap）
    // 会立刻调用一次 RollEncounter 把结果缓存到 MapNode.FixedEncounterEnemyIds 上，
    // 保证同一个节点进入后编队固定、不会每次重新随机——这里的 EncounterPool 只在
    // "建图那一次"被抽取，之后就不会再被读取第二次。
    private static StageDefinition CreateStageFourOne()
    {
        return new StageDefinition
        {
            StageId = "4-1",
            DisplayName = "第四章第一关（深渊，占位编队）",
            EncounterPool = BuildAbyssStageOneEncounterPool()
        };
    }

    private static StageDefinition CreateStageFourThree()
    {
        return new StageDefinition
        {
            StageId = "4-3",
            DisplayName = "第四章第三关（深渊，占位编队）",
            EncounterPool = BuildAbyssStageThreeEncounterPool()
        };
    }

    // 4-1 三选一：巨口×2 / 深渊共生体×3 / 侦测者×2，等权重。
    private static List<EncounterEntry> BuildAbyssStageOneEncounterPool()
    {
        return new List<EncounterEntry>
        {
            new() { Weight = 1, EnemyIds = new List<string> { "ju_kou", "ju_kou" } },
            new() { Weight = 1, EnemyIds = new List<string> { "abyss_symbiote", "abyss_symbiote", "abyss_symbiote" } },
            new() { Weight = 1, EnemyIds = new List<string> { "scout", "scout" } }
        };
    }

    // 4-3 四选一：巨口×3 / 深渊共生体×2+侦测者×1 / 侦测者×3 / 侦测者×2+巨口×1，等权重。
    private static List<EncounterEntry> BuildAbyssStageThreeEncounterPool()
    {
        return new List<EncounterEntry>
        {
            new() { Weight = 1, EnemyIds = new List<string> { "ju_kou", "ju_kou", "ju_kou" } },
            new() { Weight = 1, EnemyIds = new List<string> { "abyss_symbiote", "abyss_symbiote", "scout" } },
            new() { Weight = 1, EnemyIds = new List<string> { "scout", "scout", "scout" } },
            new() { Weight = 1, EnemyIds = new List<string> { "scout", "scout", "ju_kou" } }
        };
    }

    // 第四章精英关（4-5）：占位组合，待正式精英内容替换。复用现有三个占位敌人，
    // 用比 4-1/4-3 更强的搭配体现"精英"强度差异（巨口+深渊共生体三只同场）。
    private static StageDefinition CreateStageFourFive()
    {
        return new StageDefinition
        {
            StageId = "4-5",
            DisplayName = "精英关（第四章，占位编队）",
            EncounterPool = CreateChapterFourElitePool()
        };
    }

    private static List<EncounterEntry> CreateChapterFourElitePool()
    {
        return new List<EncounterEntry>
        {
            new() { Weight = 1, EnemyIds = new List<string> { "ju_kou", "abyss_symbiote", "abyss_symbiote", "abyss_symbiote" } },
            new() { Weight = 1, EnemyIds = new List<string> { "scout", "scout", "ju_kou", "ju_kou" } }
        };
    }
}
