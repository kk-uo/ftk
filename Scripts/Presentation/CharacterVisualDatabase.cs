//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CharacterVisualDatabase.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 角色视觉资源会在角色选择、战斗、图鉴和事件中复用。
// 集中查询可以避免多个 UI 各自维护头像、立绘或动作路径。
//
// 职责：
// 1. 注册角色视觉定义。
// 2. 为角色 ID 到头像、立绘、动作的查询提供统一入口。
// 3. 避免角色逻辑数据直接引用表现资源。
//
// 不负责：
// × 角色数据库。
// × 技能加载。
// × 战斗属性初始化。
//
// 主要依赖：
// CharacterVisualDefinition
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using Godot;

/// <summary>
/// 角色视觉数据库。
///
/// 全部可选角色都在这里拥有统一尺寸的头像资源。调用方只通过角色 ID 查询，
/// 不需要关心文件命名或为单个角色写回退分支。
/// </summary>
public static class CharacterVisualDatabase
{
    /// <summary>角色头像的标准原始尺寸。</summary>
    public const int PortraitSourceSize = 1254;

    private const string ZhaoYunMapSpriteId = "character_zhaoyun_map";
    private const string ZhaoYunMapSpritePath = "res://Assets/Characters/MapSprites/zhao_yun_map.png";

    private static readonly Dictionary<string, CharacterVisualDefinition> Definitions = new();

    static CharacterVisualDatabase()
    {
        RegisterPortrait(CharacterIds.ZhouTai, "zhou_tai");
        RegisterPortrait(CharacterIds.PangTong, "pang_tong");
        RegisterPortrait(CharacterIds.ZhaoYun, "zhao_yun");
        RegisterPortrait(CharacterIds.MaChao, "ma_chao");
        RegisterPortrait(CharacterIds.LuXun, "lu_xun");
        RegisterPortrait(CharacterIds.ZhouYu, "zhou_yu");
        RegisterPortrait(CharacterIds.LuMeng, "lv_meng");
        RegisterPortrait(CharacterIds.MengHuo, "meng_huo");
        RegisterPortrait(CharacterIds.ZhenJi, "zhen_ji");
        RegisterPortrait(CharacterIds.XuSheng, "xu_sheng");
        RegisterPortrait(CharacterIds.HuangYueYing, "huang_yue_ying");
        RegisterPortrait(CharacterIds.CaoZhen, "cao_zhen");
        RegisterPortrait(CharacterIds.ZhangLiao, "zhang_liao");
        RegisterPortrait(CharacterIds.SunCe, "sun_ce");
        RegisterPortrait(CharacterIds.LuBu, "lv_bu");
        RegisterPortrait(CharacterIds.HuaTuo, "hua_tuo");
        RegisterPortrait(CharacterIds.MiHeng, "mi_heng");
        RegisterPortrait(CharacterIds.DiaoChan, "diao_chan");
        RegisterPortrait(CharacterIds.XiaHouDun, "xiahou_dun");
        RegisterPortrait(CharacterIds.ZhuGeLiang, "zhuge_liang");
        RegisterPortrait(CharacterIds.ZhangJiao, "zhang_jiao");
        RegisterPortrait(CharacterIds.SunShangXiang, "sun_shang_xiang");
        RegisterPortrait(CharacterIds.DongZhuo, "dong_zhuo");
        RegisterPortrait(CharacterIds.LiuBei, "liu_bei");
        RegisterPortrait(CharacterIds.GuanYu, "guan_yu");
        RegisterPortrait(CharacterIds.ZhangFei, "zhang_fei");

        if (SpriteDatabase.GetPath(ZhaoYunMapSpriteId) == null)
        {
            SpriteDatabase.Register(ZhaoYunMapSpriteId, ZhaoYunMapSpritePath);
        }

        // 赵云已有地图精灵，其余角色当前只接入统一规格头像；地图精灵可独立补齐。
        Register(new CharacterVisualDefinition(
            CharacterIds.ZhaoYun,
            portraitSpriteId: PortraitSpriteIdFor(CharacterIds.ZhaoYun),
            mapSpriteId: ZhaoYunMapSpriteId));
    }

    private static void RegisterPortrait(string characterId, string fileName)
    {
        var spriteId = PortraitSpriteIdFor(characterId);
        if (SpriteDatabase.GetPath(spriteId) == null)
        {
            SpriteDatabase.Register(spriteId, $"res://Assets/Characters/Portraits/{fileName}.png");
        }

        if (!Definitions.ContainsKey(characterId))
        {
            Register(new CharacterVisualDefinition(characterId, portraitSpriteId: spriteId));
        }
    }

    private static string PortraitSpriteIdFor(string characterId)
    {
        return $"character_{characterId}_portrait";
    }

    /// <summary>
    /// 注册角色视觉定义。
    /// </summary>
    public static void Register(CharacterVisualDefinition definition)
    {
        Definitions[definition.Id] = definition;
    }

    /// <summary>
    /// 按 ID 查询角色视觉定义。
    /// </summary>
    public static CharacterVisualDefinition? Get(string id)
    {
        return Definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// 返回当前已注册的全部角色视觉定义。
    /// </summary>
    public static IReadOnlyCollection<CharacterVisualDefinition> GetAll()
    {
        return Definitions.Values;
    }

    /// <summary>
    /// 按角色 ID 查询已注册且资源存在的头像贴图；查询失败时返回 null，
    /// 调用方应回退到占位显示，不需要各自重复维护 SpriteDatabase 查询逻辑。
    /// </summary>
    public static Texture2D? TryGetPortraitTexture(string characterId)
    {
        var definition = Get(characterId);
        if (definition == null || string.IsNullOrEmpty(definition.PortraitSpriteId))
        {
            return null;
        }

        var path = SpriteDatabase.GetPath(definition.PortraitSpriteId);
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        return GD.Load<Texture2D>(path);
    }

    /// <summary>
    /// 按角色 ID 查询地图探索精灵贴图；未注册或资源缺失时返回 null，
    /// 调用方应回退到默认地图玩家占位外观。
    /// </summary>
    public static Texture2D? TryGetMapSpriteTexture(string characterId)
    {
        var definition = Get(characterId);
        if (definition == null || string.IsNullOrEmpty(definition.MapSpriteId))
        {
            return null;
        }

        var path = SpriteDatabase.GetPath(definition.MapSpriteId);
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        return GD.Load<Texture2D>(path);
    }

    /// <summary>
    /// 按角色 Id + 动作状态解析应该使用的 AnimationId：优先用该角色
    /// CharacterVisualDefinition 里对应状态的字段；角色未注册，或注册了但该状态
    /// 字段留空，都会自动回退到按状态生成的默认约定 Id（"character_default_idle"
    /// 等）。调用方（未来的角色动作 Presenter）不需要写任何 if (id == null) 之类的
    /// 特殊判断——回退逻辑已经封装在这个方法里。
    /// </summary>
    public static string ResolveAnimationId(string characterId, CharacterAnimationState state)
    {
        var definition = Get(characterId);
        var specific = definition == null ? string.Empty : state switch
        {
            CharacterAnimationState.Idle => definition.IdleAnimationId,
            CharacterAnimationState.Attack => definition.AttackAnimationId,
            CharacterAnimationState.Defense => definition.DefenseAnimationId,
            CharacterAnimationState.Hit => definition.HitAnimationId,
            CharacterAnimationState.Death => definition.DeathAnimationId,
            CharacterAnimationState.Victory => definition.VictoryAnimationId,
            CharacterAnimationState.Skill => definition.SkillAnimationId,
            _ => string.Empty
        };

        return string.IsNullOrEmpty(specific) ? DefaultAnimationIdForState(state) : specific;
    }

    /// <summary>
    /// 按状态生成统一命名约定的默认 AnimationId（例如 "character_default_attack"）。
    /// 是否真的在 AnimationDatabase 里注册了这个 Id 由调用方自行查询
    /// （AnimationDatabase.GetAnimation 查不到会返回 null，静默跳过）。
    /// </summary>
    public static string DefaultAnimationIdForState(CharacterAnimationState state)
    {
        return $"character_default_{state.ToString().ToLowerInvariant()}";
    }
}
