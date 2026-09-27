# Faction Fate System

## 模块职责

负责：

- 阵营命运的静态数据定义（每个阵营各自的候选池）。
- 按当前角色所属阵营随机抽取本局生效的命运（整局固定，只在角色选择时抽一次）。
- 命运带来的次数/进度类状态（改命次数、骰子历史等）。
- 部分命运的即时发放效果（如魏·黄金储备命中即发装备）。

不负责：

- 命运效果的战斗内具体实现——那些属于各自的 `IBattleEffect`/技能/装备类
  （例如 `ShuEnemyInitialManaEffect.cs`），本模块只管"抽中了哪个"，不管"抽中之后
  具体怎么生效"。
- 命运的展示动画——`FactionFateRevealOverlay`（`Scripts/Battle/`）只读取本模块
  暴露的结果来展示，本模块不知道任何 UI/动画细节。

## 核心数据流

```text
GameManager.SelectCharacter(characterId)
  |
  v
FactionFateManager.RollFateIfEligible()
  |
  +-- 按 GameManager.CurrentCharacter.Faction 选中对应候选池
  |     (FactionFateDatabase.AllQunFates / AllWeiFates / AllShuFates / AllWuFates)
  |
  +-- GameManager.EventRewardRandom.Next(candidates.Count) 抽一个
  |
  v
_state.SelectedFateId（整局固定，直到 ResetForNewRun 才会清空重抽）
  |
  v
FactionFateManager.CurrentFateId（只读，供任何模块随时查询"本局命运是哪个"）
```

## 如何新增一个阵营命运（对后续新增命运的标准接口）

新增命运只需要三步，**不需要改动 `FactionFateManager` 的抽取逻辑，也不需要改动
`FactionFateRevealOverlay`/`MainFlow` 的展示代码**——两者都只读 `FactionFateDatabase`，
新条目会自动出现在抽取池和展示滚动池里：

1. 在 `FactionFateIds.cs` 里加一个新的字符串常量 Id。
2. 在 `FactionFateDefinition.cs` 对应阵营的 `AllXxxFates` 列表里加一行
   `new(FactionFateIds.你的Id, "factionfate.xxx.name", "factionfate.xxx.desc", Faction.对应阵营)`。
3. 在 `Localization/zh_CN.json` 和 `Localization/en_US.json` 里补上这两个文案 Key。

如果命运需要"命中即刻生效"的效果（像黄金储备/备用电池那样一次性发放），在
`FactionFateManager.RollFateIfEligible()` 末尾按 `_state.SelectedFateId ==
FactionFateIds.你的Id` 加一个分支；如果命运的效果需要在战斗/商店/事件中持续判定
（像锦囊蓄势、奇策先发那样），去查 `GameManager`/`ShopManager`/`EventSystem` 里其他
命运 Id 的调用点，照着同样的判定方式加分支，不要在 `FactionFateManager` 里新建
第二套判定逻辑。

命运的阵营图标徽章颜色/简称由 `Faction` 枚举值决定（魏蓝/蜀绿/吴红/群黄，见
`MainFlow.GetFactionDisplayInfo`），不是按单个命运 Id 配置——新增命运不需要额外
配图标，归到正确的 `Faction` 即可自动获得对应徽章。

## 对外接口

主要公开入口：

- `FactionFateManager.CurrentFateId` — 查询本局生效的命运 Id（可能为 null）。
- `FactionFateDatabase.Get(id)` — 按 Id 查完整定义（Name/Description/Faction）。
- `FactionFateDatabase.AllFates` — 四个阵营候选池的并集，供展示层做"全部命运"
  这类枚举需求（例如 `FactionFateRevealOverlay` 的滚动候选池），不要在展示层
  自己再拼一份。
- `FactionFateManager.RollFateIfEligible()` — 仅供 `GameManager.SelectCharacter()`
  调用一次，其它地方不应该调用（幂等，但语义上只应该在角色选择时触发一次）。

## 注意事项

- 阵营命运是"按 Run 决定一次"，不是"按战斗/按章节决定"——任何新功能如果需要
  "阵营命运"相关的展示或判定，默认应该只在角色选择后触发一次，不要接在战斗/
  地图节点/商店等重复访问的流程里。
- 不要新建第二套命运数据结构或第二套随机池；一切扩展都通过
  `FactionFateDatabase.AllXxxFates` 增补条目完成。
