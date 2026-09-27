# Hero Unlock System（英雄解锁系统）

以后所有英雄只通过三种正式方式解锁：击败指定敌人、完成指定事件、达成指定成就，
不再使用"通关第几章"这种模糊的解锁方式。整套系统由 `UnlockCondition` +
`Progress` 两层数据驱动，新增一个英雄的解锁条件只需要注册数据，不需要改代码；
`CharacterSelectController`、`GameManager`、`BattleRules` 里涉及的都是"注册一次
观察者/一行调用"，没有任何角色专属的 if/switch。

## 文件结构

```
Scripts/HeroUnlock/
├── HeroUnlockConditionType.cs      // 解锁方式枚举：KillEnemy/CompleteEvent/Achievement
├── AchievementType.cs              // 成就类型枚举（目前落地 4 个示例成就）
├── HeroUnlockCondition.cs          // 一条解锁条件：类型 + 目标 Id + 数量 + 展示文案
├── HeroUnlockDatabase.cs           // 角色 Id → HeroUnlockCondition 注册表
├── HeroUnlockProgress.cs           // 全局 Progress：记录 + 查询 + 解锁判定 + 开发者模式入口
├── HeroUnlockTrackingEffects.cs    // 战斗内的记录型 IBattleEffect（击败敌人/杀系使用次数/回复量/费连击）
└── README.md                       // 本文件
```

## 一、三种解锁方式怎么统一实现

`HeroUnlockCondition` 只有四个字段：`ConditionType`（枚举）、`TargetId`（字符串）、
`RequiredAmount`（数量）、`Description`（展示文案）。`HeroUnlockProgress.EvaluateCondition`
是唯一的判定入口，内部按 `ConditionType` 分三支查询：

```csharp
KillEnemy      → EnemyKillCounts[TargetId]（击败次数，默认 0）
CompleteEvent  → EventsCompleted.Contains(TargetId)        ? 1 : 0
Achievement    → AchievementCounters[Enum.Parse(TargetId)] 的当前值
```

这个方法同时被"是否已解锁"的判定和"角色选择界面显示的进度"复用，保证两处
数字不会对不上。`TargetId` 统一是字符串——KillEnemy/CompleteEvent 用既有的
`EnemyDefinition.Id`/`EventData.Id`，Achievement 用 `AchievementType` 枚举值的
字符串名（例如 `"EquipmentAcquired"`），不需要为每种类型单独设计字段。

`KillEnemy` 的 `TargetId` 支持用 `|` 分隔多个敌人 Id——这是为了支持"共享血量池的
多体 Boss"（例如刘备/关羽/张飞共用的【蜀汉共生体】，本质是三个
`UseSharedHealthPool = true` 的独立 `EnemyInstance`：`"liu_bei"`/`"guan_yu"`/
`"zhang_fei"`，血量池归零时通常会各自触发一次死亡记录）。`EvaluateCondition`
在这种情况下取分隔出的多个敌人 Id 里"击败次数"的最大值，任意一个被记录到
击败即视为整个 Boss 被击败，不需要为"多体共享血量 Boss"单独设计一种新的
`HeroUnlockConditionType`。

**v1 只登记了 KillEnemy 和 Achievement 两类真实条件，CompleteEvent 只保留类型
定义，本版没有注册任何事件类解锁条件**（以后要用时直接照抄 `HeroUnlockDatabase`
里 KillEnemy/Achievement 的注册写法即可，`EvaluateCondition`/`RecordEventCompleted`
等 CompleteEvent 相关代码路径已经存在，随时可以启用）。

## 二、Progress 保存在哪里、什么时候写入

`HeroUnlockProgress` 是完全独立于 `GameManager` 的静态类：

- `EnemyKillCounts`（Dictionary&lt;string, int&gt;）——敌人 Id → 击败次数，来自
  `HeroUnlockTrackingEffects.cs` 里的 `HeroUnlockEnemyKilledTrackingEffect`
  （挂在 `TriggerTiming.OnDeath`，敌人死亡时 +1）。KillEnemy 类型的解锁条件只关心
  "次数 ≥ RequiredAmount"，多数条件把 RequiredAmount 设成 1（等价于原来的
  HashSet 语义），但 Developer Debug Panel 的 Enemy 页需要展示/调整具体次数，
  所以这里存的是计数而不是布尔值。
- `EventsCompleted`（HashSet&lt;string&gt;）——事件 Id，来自
  `GameManager.MarkEventSeen` 里新增的一行调用（事件系统本来就会在事件结束时
  调用这个方法，这里只是多做一次记录，不影响它原有的"本局事件去重"逻辑）。
- `AchievementCounters`（Dictionary&lt;AchievementType, int&gt;）——四个示例成就：
  - `EquipmentAcquired`：`GameManager.AddEquipment` 的两个重载各加一行。
  - `GoldEarned`：`GameManager.AddGold` 加一行（只统计净增加）。
  - `KillCardsPlayed`：`HeroUnlockKillCardTrackingEffect`（`OnBattlePhase`，
    检查 `context.PlayerAction` 是否是杀系攻击，复用既有的
    `BattleRules.IsShaAttack`）。
  - `HealingDone`：`HeroUnlockHealingTrackingEffect`（`OnHeal`）。
  - `ConsecutiveFeeTurns`：`HeroUnlockFeeStreakTrackingEffect`（`OnBattlePhase`，
    维护一个"当前连续使用费的回合数"和"历史最高值"，历史最高值才是成就进度）。
- `HeroesUnlocked`（HashSet&lt;string&gt;）——已解锁角色 Id，一旦加入就不会再被
  拿掉（除非调用 `ResetAll`）。
- `PendingUnlockAnnouncements`（Queue&lt;IReadOnlyList&lt;string&gt;&gt;）——待展示的解锁
  公告。每一项是"应该合并展示的一批角色 Id"：单人解锁时是长度为 1 的列表；
  多个角色共用同一个 `HeroUnlockCondition.GroupId` 且在同一次 `CheckForNewUnlocks`
  判定里一起达成条件时（例如蜀汉三兄弟），会被合并成一个列表，只入队一条公告，
  UI 只弹一次合并窗口，不逐个弹三次。

**不随 `GameManager.ResetRunData()`（开始新的一局）清空**——解锁进度是跨越多局的
长期进度，只有开发者模式的【重置英雄解锁】会清空它。项目目前还没有落地存档到
磁盘的机制，`HeroUnlockProgress` 和 `GameManager` 的其它运行时状态一样，只在本次
运行的进程内存里维护；以后接入存档系统时，这个类是应该被序列化的对象之一。

角色本身完全不知道"我有没有解锁"——`CharacterData`/`CharacterDatabase` 没有任何
改动，`HeroUnlockProgress.IsHeroUnlocked(characterId)` 才是唯一的真相来源，
**没有在 `HeroUnlockDatabase` 里注册条件的角色永远返回 true（一直可选）**，
保证这次改动不影响任何没有主动配置解锁条件的角色。

## 三、UI 怎么显示进度

`CharacterSelectController.cs`：

- 按钮列表：未解锁角色显示"？？？"、按钮变灰、点击不会触发 `CharacterChosen`
  （悬停/获得焦点仍然会更新右侧信息面板）。
- 右侧头像：始终显示同一张头像贴图（`CharacterVisualDatabase` 查到的真实头像，
  查不到用 `IconLibrary` 的默认图标兜底），未解锁时把 `Modulate` 调到接近黑色，
  形成"剪影"效果——玩家能看出这是个人形，但认不出具体是谁。
- 右侧信息面板：未解锁时姓名/性别/阵营/生命值全部替换成空白或"？？？"，技能区域
  替换成 `BuildUnlockConditionText`——展示 `HeroUnlockCondition.Description`
  （例如"击败【医者】"）和 `HeroUnlockProgress.EvaluateCondition` 算出的
  "当前进度 / 需要多少"（例如"8 / 15"）。

## 四、解锁公告怎么弹出

`HeroUnlockProgress` 内部的 `CheckForNewUnlocks`（每次记录任何原始数据后都会调用
一次）负责检测"有没有角色第一次满足条件"，命中后立即加入 `HeroesUnlocked`。
在入队公告之前，会先按 `HeroUnlockCondition.GroupId` 把这一次新达成条件的角色
分组：没有 `GroupId` 的角色各自单独入队一条公告；有 `GroupId` 的角色（例如刘备/
关羽/张飞，`GroupId = "shu_brothers"`）如果在同一次判定里一起达成，会合并成一条
公告——这正是"打死【蜀汉共生体】后统一弹出【蜀汉三兄弟已解锁】，而不是连续弹
三个独立窗口"的实现方式。这一步可能发生在战斗、事件、地图等任何界面（这些界面
本身完全没有改动）。`CharacterSelectController._Ready()` 会在每次打开角色选择
界面时调用 `ShowNextPendingUnlockAnnouncement`，把队列里积压的公告依次弹出
（`AcceptDialog`，单人时展示头像/名称/阵营/技能名称；多人合并公告时标题变成
`HeroUnlockCondition.GroupAnnouncementTitle`，正文依次列出每个人的信息，头像
并排展示），点击确认后刷新一次角色列表并继续检查下一条。

这意味着解锁公告不是"解锁那一刻立刻在当前界面弹出"，而是"下一次回到角色选择
界面时弹出"——因为真正解锁的时刻可能在战斗/事件里，而这次任务明确要求不修改
Battle/Trigger/Reward/地图逻辑，没有办法在那些界面里插入一个弹窗。

## 五、开发者模式

- 【全部解锁英雄】：`HeroUnlockProgress.UnlockAllForDebug()`——把
  `HeroUnlockDatabase` 里注册过条件的角色全部加入 `HeroesUnlocked`，不修改任何
  Progress 计数器，也不触发解锁公告弹窗（纯调试快捷方式）。
- 【重置英雄解锁】：`HeroUnlockProgress.ResetAll()`——清空全部记录/计数/已解锁/
  待展示公告，方便重新测试解锁流程。

两个按钮只在 `DeveloperModeManager.IsDeveloperMode == true` 时出现在角色选择
界面里，正式游戏（非开发者模式）完全不可见；点击后只调用
`HeroUnlockProgress` 的方法再重建一次界面（`RefreshLayout`），不触碰
`GameManager` 的任何存档相关字段，不影响正常游戏进度。

## 六、v1 英雄名单（`HeroUnlockDatabase.cs` 静态构造函数）

| 角色 | 解锁方式 | 目标 | 数量 | 难度分层 |
|---|---|---|---|---|
| 吕布 / 华佗 / 祢衡 | KillEnemy | `boss_traitor`/`boss_healer`/`crazy_performer`（第一章 Boss，各1个） | 1 | ② |
| 貂蝉 | KillEnemy | `boss_traitor`（背叛者，和吕布共用同一个 Boss，次数不同） | 3 | ③ |
| 夏侯惇 | KillEnemy | `cyclops`（独眼巨人，第二章精英） | 1 | ④ |
| 诸葛亮 / 张角 | KillEnemy | `wulong_collective_intelligence`（观星集智体）/`huang_yi_zhi_zhu`（黄衣之主）（第二章 Boss） | 1 | ⑤ |
| 孙尚香 | Achievement | `EquipmentAcquired`（累计获得装备，账号历史累计，只增不减） | 100 | ⑥ |
| 董卓 | KillEnemy | `boss_tyrant`（暴虐昏君，第三章 3-8 关两条随机路线之一） | 1 | ⑦ |
| 刘备/关羽/张飞 | KillEnemy（共用同一条件对象，`GroupId = "shu_brothers"`） | `liu_bei|guan_yu|zhang_fei`（蜀汉共生体，3-8 关另一条路线，共享血量池的三个 EnemyInstance） | 1 | ⑦ |

刘备、关羽这两个角色是 Hero Unlock System v1 新增的 `CharacterData`
（`Scripts/Character.cs`，`CharacterIds.LiuBei`/`CharacterIds.GuanYu` 两个常量
此前就存在，只是没有对应的角色条目）——之前项目里只有张飞是可玩角色，为了让
"蜀汉三兄弟"解锁语义完整，经用户确认后补上另外两人。两人暂时没有分配技能
（`SkillIds` 为空列表），因为这次任务的范围明确不包含设计/实现新的角色技能。

**董卓解锁条件用的 `boss_tyrant`（暴虐昏君）不是一个新敌人**——项目里原本没有
单独的"董卓"Boss 敌人条目，`boss_tyrant` 是 3-8 关（第三章终局）两条随机路线里
的其中一条，技能组成（酒池/肉林/崩坏）和董卓角色本身的技能同源，经用户确认后
直接复用这个 Boss 作为"击败董卓"的判定目标，没有新增任何敌人数据。

**贾诩（JiaXu）、左慈（ZuoCi）已从可玩角色列表移除**——`Scripts/Character.cs`
里对应的 `CharacterData` 条目已删除，两人不会再出现在角色选择界面或
Developer Debug Panel 的 Hero 页。`CharacterIds.JiaXu`/`CharacterIds.ZuoCi`
常量本身以及他们的专属技能定义（`Skill.cs` 里的"完杀"/`Wansha` 和
"化形"/`HuaXing`）**没有删除**——这次任务的范围是移除角色，不是重构技能系统，
删掉这两个技能定义、清理它们在 `Skill.cs` 里的注册需要动到技能系统本身，
超出这次改动范围；这两个技能目前变成"没有任何角色持有"的孤立数据，不影响
编译也不会在游戏里出现（没有角色的 `SkillIds` 引用它们）。

### 图鉴顺序（`CharacterDatabase.All()` 的排列顺序）

项目里目前没有单独的"英雄图鉴"页面（`Scripts/CodexController.cs` 的图鉴只覆盖
卡牌/装备/敌人/事件/增益等类别，不包含角色列表），角色选择界面
（`CharacterSelectController`）按钮顺序直接来自 `CharacterDatabase.GetAllCharacters()`
的列表顺序，因此把这份列表按解锁难度从易到难重新排列，等价于把"图鉴"（角色
选择界面）里的显示顺序按难度重排：

1. 没有注册解锁条件、一直可选的角色（赵云/马超/陆逊/吕蒙/孟获/甄姬/徐盛/黄月英/
   曹真/孙策）。
2. 第一章 Boss，击败1次解锁（吕布/华佗/祢衡）。
3. 第一章同一个 Boss（背叛者），但要求累计击败3次，比"只需1次"更难（貂蝉）。
4. 第二章精英解锁（夏侯惇：独眼巨人）。
5. 第二章 Boss，击败1次解锁（诸葛亮/张角）。
6. 需要账号历史累计的成就类条件，跨局长期养成（孙尚香：累计100件装备）。
7. 第三章终局 Boss，3-8 关两条随机路线，难度最高（董卓/刘备/关羽/张飞）。

这个顺序只影响列表展示，不影响任何解锁判定逻辑——判定始终只看
`HeroUnlockProgress`，和角色在列表里的位置无关。

## 七、以后怎么扩展

### 新增一个英雄的解锁条件

不需要改这个目录下任何一个 `.cs` 文件，只需要在 `HeroUnlockDatabase` 的静态
构造函数（或任何在游戏启动时会执行到的地方）里调用：

```csharp
HeroUnlockDatabase.Register(CharacterIds.SomeHero, new HeroUnlockCondition(
    HeroUnlockConditionType.KillEnemy,
    targetId: "boss_xxx",
    requiredAmount: 1,
    description: "击败【XXX】"));
```

上面"v1 英雄名单"表格里的 7 条注册（含刘备/关羽/张飞共用的一条）覆盖了
KillEnemy（含单目标和 `|` 分隔的多目标）、Achievement 两种写法，直接照抄格式
即可；多个角色共用同一条件、需要合并解锁公告时，构造一次
`HeroUnlockCondition`（带上 `groupId`/`groupAnnouncementTitle`）后对每个角色都
`Register` 同一个实例，参考蜀汉三兄弟的写法。

### 新增一种解锁条件类型（第四种大类）

目前刻意只支持 KillEnemy/CompleteEvent/Achievement 三种（这是任务明确要求的
"仅支持三种正式解锁方式"）。如果以后确实需要第四种大类，需要：
1. 在 `HeroUnlockConditionType.cs` 新增一个枚举值。
2. 在 `HeroUnlockProgress.EvaluateCondition` 的 switch 里新增一个分支。
3. 在合适的地方记录原始数据（参考 `RecordEnemyKilled`/`RecordEventCompleted`
   的写法）。

### 新增一个成就

1. 在 `AchievementType.cs` 新增一个枚举值。
2. 在触发这个成就的地方调用 `HeroUnlockProgress.AddAchievementProgress(type, amount)`——
   如果时机在战斗内，参考 `HeroUnlockTrackingEffects.cs` 的写法新增一个
   `IBattleEffect`，通过 `BattleRules.cs` 注册；如果时机在战斗外（例如
   `GameManager` 已有的资源变化入口），直接在那个方法里追加一行调用即可，
   和这次给 `AddEquipment`/`AddGold`/`MarkEventSeen` 追加调用的做法完全一样。
3. 在 `HeroUnlockDatabase` 里给某个英雄注册一条 `HeroUnlockConditionType.Achievement`
   条件，`TargetId` 填新枚举值的字符串名（`nameof(AchievementType.X)`）。

### 新增一个 Boss 解锁

Boss 本质上就是"击败指定敌人"，直接用 `HeroUnlockConditionType.KillEnemy`，
`TargetId` 填 `EnemyDatabase` 里那个 Boss 的 `Id`，不需要任何额外代码——
`HeroUnlockEnemyKilledTrackingEffect` 已经会在任意敌人死亡时记录，不区分
"普通敌人"还是"Boss"。

## 八、Developer Progress Debug Panel

`Scripts/DeveloperDebugPanel.cs`（按 F8 开关，由 `MainFlow.cs` 统一处理按键和
挂载，只在 `DeveloperModeManager.IsDeveloperMode` 为真时可以打开）是这个项目
统一的开发者调试入口——Hero/Enemy/Event/Achievement/Inventory/Battle/Save
七个页面全部通过按钮直接调用 `HeroUnlockProgress` 上一批 `XxxForDebug`
方法（`SetHeroUnlockedForDebug`/`AddEnemyKillCountForDebug`/
`SetEventCompletedForDebug`/`AddAchievementProgressForDebug` 等），只修改
Progress 本身，不直接改 `HeroUnlockDatabase`/`EnemyDatabase`/`EventDatabase`
这些只读定义——正式游戏读到的还是同一份 Progress，调试和正式路径永远一致。

以后任何新系统需要调试入口，都应该加到这个面板的某一页里（或新增一页），
不要再为单个系统写一个独立的调试窗口。

Hero 页现在会给每个注册了解锁条件的角色额外显示"当前进度 / 需要多少"，并提供
【条件+1】/【条件达成】两个按钮——它们通过 `AdvanceConditionForDebug` 直接推进
这条条件底层依赖的计数器（KillEnemy 按 Boss 击败次数、Achievement 按成就进度；
KillEnemy 的多目标 Boss 会把 `|` 分隔出的每个敌人 Id 都设置一遍），不直接把英雄
标记为已解锁，交给正式的 `HeroUnlockProgress.CheckForNewUnlocks` 自己判断是否
达成解锁，和真实游戏流程完全一致，这是测试"Boss 解锁"/"累计装备解锁"最快的入口。
