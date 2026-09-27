# Combat Visual Profile System

以后所有战斗表现（攻击、防御、挥砍、特效、音效——卡牌也算）全部数据化配置，
不写死在 `WeaponPresenter`、`Battle` 或某个装备/卡牌专属代码里。

## 和已有 Presentation Framework 的关系

这一层建立在已有的四个底层资源数据库之上，**不创建第二套资源管理**：

```
SpriteDatabase    —— 所有图片（武器/Trail/卡牌/护盾……）的唯一路径入口
AnimationDatabase —— 所有动画参数（挥砍角度/位移/时长……）的唯一入口
EffectDatabase    —— 所有特效（斩击/火焰/护盾展开……）的唯一入口
（AudioDatabase）  —— 音效相关字段目前只是预留 Id，项目里还没有真正的音频系统
```

`Combat Visual Profile System` 只负责再上面一层："一件装备/一张卡牌应该用
哪些 Id"，把这些 Id 组合成一份 `AttackVisualProfile`/`DefenseVisualProfile`/
`CardVisualProfile`，播放方（`WeaponPresenter`/`EffectPlayer`/未来的
角色动作 Presenter）拿到 Id 之后再各自去对应的底层数据库查询播放。

## 文件结构

```
Scripts/Presentation/CombatVisualProfile/
├── AttackVisualProfile.cs         // 一次攻击表现的完整参数
├── DefenseVisualProfile.cs        // 一次防御表现的完整参数
├── CombatVisualProfileDatabase.cs // 装备 Id → Attack/DefenseVisualProfile
├── CardVisualProfile.cs           // 一张卡牌的完整表现参数
├── CardVisualProfileDatabase.cs   // CardType → CardVisualProfile
└── README.md                      // 本文件
```

另外还扩展了已有的 `Scripts/Presentation/CharacterVisualDefinition.cs`/
`CharacterVisualDatabase.cs`（角色动作，见下）和新增了
`Scripts/Presentation/CharacterAnimationState.cs`（角色动作状态枚举）。

## 一、AttackVisualProfile / DefenseVisualProfile

字段见 `AttackVisualProfile.cs`/`DefenseVisualProfile.cs` 的 XML 注释，
概括如下：

- **AttackVisualProfile**：`WeaponSpriteId`/`TrailSpriteId`/`AttackAnimationId`/
  `AttackEffectId`/`AttackAudioId`/`HitEffectId`/`HitAudioId`/`CameraShakeId`（预留）/
  `ScreenFlashId`（预留）。
- **DefenseVisualProfile**：`DefenseAnimationId`/`DefenseEffectId`/`DefenseAudioId`/
  `BlockPoseId`/`ShieldSpriteId`（预留）/`CounterEffectId`（预留）。

**没有在 `EquipmentDefinition`/`EquipmentDatabase` 上新增任何字段**——
`AttackVisualProfileId`/`DefenseVisualProfileId` 这个"装备拥有一个 Profile"
的概念，是通过 `CombatVisualProfileDatabase` 按装备已有的 `Id` 作为查询 key
实现的，完全不需要触碰装备逻辑数据。

## 二、CombatVisualProfileDatabase：默认 Profile 怎么回退

```csharp
public static AttackVisualProfile GetAttackProfile(string equipmentId)
{
    return AttackProfiles.TryGetValue(equipmentId, out var profile) ? profile : DefaultAttackVisualProfile;
}
```

查询方法本身就是"没有配置就用默认"，调用方（未来真正接入战斗流程时）永远
只需要写：

```csharp
var profile = CombatVisualProfileDatabase.GetAttackProfile(equipmentId);
```

不需要写 `if (profile == null)` 之类的特殊判断——这个方法从设计上就不会
返回 null。`DefaultAttackVisualProfile`/`DefaultDefenseVisualProfile` 是
两个 `public static readonly` 字段，默认攻击 Profile 直接复用了
Weapon Presentation（Phase 2）和 Effect System 已经注册好的
`"weapon_default"`/`"weapon_default_trail"`/`"weapon_default_attack"`/
`EffectDatabase.SlashDefaultEffectId`，保证从第一天起就是一条完整可用的
链路，不是一堆空字符串。

## 三、Character Animation

角色动作没有新建数据库，而是扩展了已有的 `CharacterVisualDefinition`
（Presentation Framework 更早阶段就存在的类），补齐了
`IdleAnimationId`/`DefenseAnimationId`/`VictoryAnimationId`/`SkillAnimationId`
四个字段（原来只有 Attack/Hit/Death）。

新增 `CharacterAnimationState` 枚举（Idle/Attack/Defense/Hit/Death/Victory/Skill）
和 `CharacterVisualDatabase.ResolveAnimationId(characterId, state)`：

```csharp
var animationId = CharacterVisualDatabase.ResolveAnimationId(CharacterIds.ZhaoYun, CharacterAnimationState.Attack);
```

角色未注册，或者注册了但该状态字段留空，都会自动回退到
`DefaultAnimationIdForState(state)` 生成的统一命名约定 Id（例如
`"character_default_attack"`）——是否真的在 `AnimationDatabase` 里注册了
这个默认动画由调用方自己查询，查不到就静默跳过。

## 四、攻击流程 / 五、防御流程

这次任务的目标是把"数据"建好、验证好回退链路，**没有把 Battle 的攻击/
防御结算流程接到这些 Profile 上**（明确要求不修改 Battle/Trigger/Damage/
卡牌逻辑/装备效果/技能效果，也没有修改 `WeaponPresenter`）。以后真正接入时，
流程应该是：

```
Attack Card
  → CharacterVisualDatabase.ResolveAnimationId(角色Id, Attack)
  → CombatVisualProfileDatabase.GetAttackProfile(装备Id)
      → WeaponSpriteId / TrailSpriteId / AttackAnimationId（沿用 WeaponPresenter 现有播放逻辑）
      → AttackEffectId / HitEffectId（EffectPlayer.Play）
      → AttackAudioId / HitAudioId（预留，等音频系统接入）
  → Damage Number（既有逻辑，不受影响）
```

防御流程同理，把 `GetAttackProfile` 换成 `GetDefenseProfile`。

## 六、默认 Profile

见上面"二"——`DefaultAttackVisualProfile`/`DefaultDefenseVisualProfile`
已经建好，任何装备只要没有单独 `RegisterAttackProfile`/`RegisterDefenseProfile`，
查询时自动拿到默认值。

## 七、卡牌图片（CardSpriteId）/ 八、卡牌表现（CardVisualProfile）

`CardVisualProfileDatabase` 已经为费/杀/火杀/雷杀/闪/桃/酒/顺手牵羊/
无懈可击九种卡牌各注册了一条 `CardVisualProfile`，`CardSpriteId` 按
`"card_<类型>"` 的命名约定（例如 `"card_fire_kill"`），交给 SpriteDatabase
解析；`Description` 字段记录了每张卡期望的表现意图（费→蓝色能量流入、
桃→绿色治疗粒子、酒→橙色强化光效、顺手牵羊→金币飞向玩家、
无懈可击→护盾展开、火杀/雷杀→对应属性挥砍）。目前只有"杀"这张卡的
`AnimationId`/`EffectId` 真正指向了已存在的资源（`"weapon_default_attack"`/
`EffectDatabase.SlashDefaultEffectId`），作为验证链路；其余卡牌的动画/特效
Id 留空，等对应资源做好后按同样格式补上即可。

**没有修改 `Scripts/Card.cs` 里 `CardType` 的定义，也没有修改 `CardUI.cs`
的渲染逻辑**——这次只建立数据层，没有把 `CardVisualProfileDatabase` 接进
实际的出牌/渲染流程。

## 九、Database

新增 `CombatVisualProfileDatabase`（装备表现）和 `CardVisualProfileDatabase`
（卡牌表现）两个数据库，全部通过既有的 `SpriteDatabase`/`AnimationDatabase`/
`EffectDatabase` 解析具体资源，没有新建任何第二套资源加载/缓存机制。

## 十一、开发原则：以后怎么新增

**新增一件装备的攻击/防御表现**：

```csharp
CombatVisualProfileDatabase.RegisterAttackProfile("qinggangjian", new AttackVisualProfile(
    profileId: "attack_qinggangjian",
    weaponSpriteId: "weapon_qinggangjian",
    trailSpriteId: "weapon_qinggangjian_trail",
    attackAnimationId: "weapon_qinggangjian_attack",
    attackEffectId: "slash_qinggangjian"));
```

不需要修改 `WeaponPresenter.cs`、`Battle` 或任何战斗结算代码——只需要
注册一条 Profile（以及在 `SpriteDatabase`/`AnimationDatabase`/`EffectDatabase`
里注册对应的资源）。没有配置就自动回退到 `DefaultAttackVisualProfile`。

**新增一张卡牌**：

```csharp
CardVisualProfileDatabase.Register(new CardVisualProfile(CardType.IceKill,
    cardSpriteId: "card_ice_kill",
    description: "冰杀：冰霜挥砍"));
```

不需要修改 `Battle`——只需要配置 `CardSpriteId` 和（可选的）动画/特效/音效 Id。
