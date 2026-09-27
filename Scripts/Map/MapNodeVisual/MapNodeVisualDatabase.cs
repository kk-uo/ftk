//////////////////////////////////////////////////////////
// 文件：Scripts/Map/MapNodeVisual/MapNodeVisualDatabase.cs
//
// 模块：Map System
//
// 为什么存在：
// 地图上每一个节点（战斗/事件/精英/商店/Boss/藏宝阁，以后还有赤壁残骸、
// 统治者雕像、载具店等具体事件）应该长什么样，需要一个统一的地方注册，
// 而且不能靠“如果 Type == Boss 就用这张图”这种写死判断——同样是 Event 类型，
// 藏宝阁和统治者雕像应该能有完全不同的外观。
//
// 职责：
// 1. 用一个统一的字符串 VisualId 注册 MapNodeVisualDefinition，不区分
//    VisualId 到底来自事件 ID、节点 ID 还是类型默认值。
// 2. 提供 Resolve(MapNode) 入口：按“事件专属外观 → 节点专属外观 →
//    类型默认外观”的顺序查找，调用方（MapNodeVisual）不需要知道这个查找顺序，
//    也不需要对 MapNodeType 做任何 switch/if 判断。
// 3. 让新增一个事件的专属外观变成“注册一条以事件 ID 为 VisualId 的
//    Definition”，不需要修改 MapNodeVisual 或地图探索场景的代码。
//
// 不负责：
// × 判断节点解锁/通关状态（由 GameManager 负责）。
// × 实例化 Godot 节点（由 MapNodeVisual 负责）。
// × 生成/复制地图节点数据（VisualId 的来源——MapNode.FixedEventId/MapNode.Id/
//   MapNode.Type——全部读取自既有的 GameManager.Nodes，不新增任何地图数据）。
//
// 主要依赖：
// MapNodeVisualDefinition / MapNode / MapNodeType（Scripts/EventSystem.cs）
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using Godot;

/// <summary>
/// 地图节点显示参数数据库：VisualId → MapNodeVisualDefinition。
///
/// VisualId 不是新的地图数据，而是从既有 <see cref="MapNode"/> 派生出来的一个
/// 查询键：优先用 <see cref="MapNode.FixedEventId"/>（藏宝阁、赤壁残骸、
/// 统治者雕像、载具店等具体事件都有自己的 FixedEventId，天然适合做专属外观的
/// key），其次用 <see cref="MapNode.Id"/>（单个节点实例的专属外观），
/// 最后回退到按 <see cref="MapNodeType"/> 生成的统一命名约定
/// （"type_default_battle" 等）。三层都只是字典查询，MapNodeVisual 里没有任何
/// 针对具体类型的 if/switch 分支。
/// </summary>
public static class MapNodeVisualDatabase
{
    private const string UnknownDefaultVisualId = "type_default_unknown";
    private const string InitialEventVisualId = "initial_event";
    private const string InitialEventIconSpriteId = "map_initial_event_1_0";
    private const string InitialEventIconPath = "res://Assets/UI/Icons/map_initial_event_1_0.png";
    private const string BattleNodeIconSpriteId = "map_battle_normal";
    private const string BattleNodeIconPath = "res://Assets/UI/Icons/map_battle_normal.png";
    private const string EliteNodeIconSpriteId = "map_elite";
    private const string EliteNodeIconPath = "res://Assets/UI/Icons/map_elite.png";
    private const string BossNodeIconSpriteId = "map_boss";
    private const string BossNodeIconPath = "res://Assets/UI/Icons/map_boss.png";

    private static readonly Dictionary<string, MapNodeVisualDefinition> Definitions = new();

    static MapNodeVisualDatabase()
    {
        if (SpriteDatabase.GetPath(InitialEventIconSpriteId) == null)
        {
            SpriteDatabase.Register(InitialEventIconSpriteId, InitialEventIconPath);
        }
        if (SpriteDatabase.GetPath(BattleNodeIconSpriteId) == null)
        {
            SpriteDatabase.Register(BattleNodeIconSpriteId, BattleNodeIconPath);
        }
        if (SpriteDatabase.GetPath(EliteNodeIconSpriteId) == null)
        {
            SpriteDatabase.Register(EliteNodeIconSpriteId, EliteNodeIconPath);
        }
        if (SpriteDatabase.GetPath(BossNodeIconSpriteId) == null)
        {
            SpriteDatabase.Register(BossNodeIconSpriteId, BossNodeIconPath);
        }

        // 六种既有类型的默认外观：都是普通数据行，不是特殊代码分支；
        // 以后要让某个具体事件（例如藏宝阁、赤壁残骸）使用不同外观，
        // 只需要用它的 FixedEventId 或节点 Id 注册一条新的 Definition 覆盖它，
        // 不需要改这里的默认行，也不需要改 MapNodeVisual。
        Register(DefaultVisualIdForType(MapNodeType.Battle), new MapNodeVisualDefinition(
            nodeIcon: BattleNodeIconSpriteId,
            scale: 1.35f,
            placeholderGlyph: "○",
            placeholderColor: new Color(0.55f, 0.62f, 0.70f),
            description: "普通战斗节点，类型默认外观。"));

        Register(DefaultVisualIdForType(MapNodeType.Event), new MapNodeVisualDefinition(
            scale: 1.5f,
            placeholderGlyph: "？",
            placeholderColor: new Color(0.70f, 0.62f, 0.30f),
            description: "事件节点，类型默认外观（没有专属 VisualId 时使用）。"));

        Register(DefaultVisualIdForType(MapNodeType.Elite), new MapNodeVisualDefinition(
            nodeIcon: EliteNodeIconSpriteId,
            scale: 1.35f,
            placeholderGlyph: "◆",
            placeholderColor: new Color(0.62f, 0.32f, 0.68f),
            description: "精英战斗节点，类型默认外观。"));

        Register(DefaultVisualIdForType(MapNodeType.Shop), new MapNodeVisualDefinition(
            scale: 1f,
            placeholderGlyph: "🛒",
            placeholderColor: new Color(0.30f, 0.62f, 0.42f),
            description: "商店节点，类型默认外观（没有专属 VisualId 时使用，例如以后的载具店可以单独覆盖）。"));

        Register(DefaultVisualIdForType(MapNodeType.Boss), new MapNodeVisualDefinition(
            nodeIcon: BossNodeIconSpriteId,
            scale: 1.5f,
            placeholderGlyph: "☠",
            placeholderColor: new Color(0.74f, 0.20f, 0.20f),
            description: "Boss 节点，类型默认外观，以后可配置 GlowEffect 做 Boss 光效。"));

        Register(DefaultVisualIdForType(MapNodeType.Treasure), new MapNodeVisualDefinition(
            scale: 1.1f,
            placeholderGlyph: "💰",
            placeholderColor: new Color(0.86f, 0.72f, 0.24f),
            description: "藏宝阁节点，类型默认外观（没有专属 VisualId 时使用）。"));

        Register(UnknownDefaultVisualId, new MapNodeVisualDefinition(
            scale: 1f,
            placeholderGlyph: "●",
            placeholderColor: new Color(0.5f, 0.5f, 0.5f),
            description: "兜底外观：理论上不会用到，六种既有类型都已经注册了默认值。"));

        Register(InitialEventVisualId, new MapNodeVisualDefinition(
            nodeIcon: InitialEventIconSpriteId,
            scale: 3f,
            placeholderGlyph: "？",
            placeholderColor: new Color(0.70f, 0.62f, 0.30f),
            description: "初始事件 1-0 的地图专属图标。"));
    }

    /// <summary>
    /// 注册（或覆盖）一个 VisualId 对应的显示参数定义。
    ///
    /// VisualId 可以是事件 ID（例如 "event_ruler_statue"）、节点 Id，
    /// 也可以是 <see cref="DefaultVisualIdForType"/> 生成的类型默认 ID。
    /// </summary>
    public static void Register(string visualId, MapNodeVisualDefinition definition)
    {
        Definitions[visualId] = definition;
    }

    /// <summary>
    /// 按 VisualId 直接查询；一般不需要调用方自己拼 VisualId，
    /// 应该优先使用 <see cref="Resolve"/>。
    /// </summary>
    public static MapNodeVisualDefinition? Get(string visualId)
    {
        return Definitions.TryGetValue(visualId, out var definition) ? definition : null;
    }

    /// <summary>
    /// 按“事件专属外观 → 节点专属外观 → 类型默认外观”的顺序，
    /// 从既有的 <see cref="MapNode"/> 数据解析出应该使用的显示参数。
    ///
    /// 这是 <see cref="MapNodeVisual"/> 应该调用的唯一入口；整个方法里没有任何
    /// 针对具体 MapNodeType 的特殊判断，只是按优先级尝试三个字符串 key。
    /// </summary>
    public static MapNodeVisualDefinition Resolve(MapNode node)
    {
        if (!string.IsNullOrEmpty(node.FixedEventId) && Get(node.FixedEventId) is { } byEvent)
        {
            return byEvent;
        }

        if (Get(node.Id) is { } byNodeId)
        {
            return byNodeId;
        }

        return Get(DefaultVisualIdForType(node.Type)) ?? Get(UnknownDefaultVisualId)!;
    }

    /// <summary>
    /// 按类型生成统一命名约定的默认 VisualId（例如 "type_default_boss"）。
    ///
    /// 这只是一个字符串拼接，不代表"按类型决定外观"——它注册的默认行随时可以被
    /// 更具体的 FixedEventId/节点 Id 覆盖，六种类型共享同一段拼接逻辑，
    /// 不是针对某个类型的特殊代码。
    /// </summary>
    public static string DefaultVisualIdForType(MapNodeType nodeType)
    {
        return $"type_default_{nodeType.ToString().ToLowerInvariant()}";
    }
}
