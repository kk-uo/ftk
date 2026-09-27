# Main / Battle 节点层级

```text
Main (Control)
└─ Battle (Battle.tscn instance)

Battle (Control, BattleManager.cs)
├─ Background (ColorRect)
└─ MainMargin (MarginContainer)
   └─ RootLayout (VBoxContainer)
      ├─ TopBar (PanelContainer)
      │  └─ TopBarRow (HBoxContainer)
      │     ├─ TurnLabel
      │     ├─ ActionLabel
      │     ├─ SkillDebugButton
      │     ├─ BattleLogButton
      │     └─ RestartButton
      ├─ EnemyPanel (PanelContainer)
      │  └─ EnemyContainer (HBoxContainer)
      │     ├─ EnemySlot1 (PanelContainer, visible)
      │     │  └─ EnemySlot1Row (HBoxContainer)
      │     │     ├─ EnemyAvatar
      │     │     └─ EnemyStats
      │     ├─ EnemySlot2 (PanelContainer, hidden)
      │     └─ EnemySlot3 (PanelContainer, hidden)
      ├─ Battlefield (PanelContainer)
      │  └─ BattlefieldRow (HBoxContainer)
      │     ├─ TurnCounterPanel
      │     │  ├─ TurnCounterLabel
      │     │  └─ RecentReportLabel
      │     └─ BattlefieldCenter (CenterContainer)
      │        └─ BattlefieldHintLabel
      ├─ PlayerPanel (PanelContainer)
      │  └─ PlayerRow (HBoxContainer)
      │     ├─ PlayerAvatar (PanelContainer)
      │     │  └─ PlayerAvatarLabel
      │     ├─ PlayerStats (VBoxContainer)
      │     │  ├─ PlayerNameLabel
      │     │  ├─ PlayerHpLabel
      │     │  ├─ PlayerManaLabel
      │     │  └─ PlayerDodgeLabel
      │     └─ PlayerSkillArea (HBoxContainer)
      └─ ActionPanel (PanelContainer)
         └─ ActionCenter (CenterContainer)
            └─ ActionArea (HBoxContainer)
               ├─ 杀 (CardUI)
               ├─ 火杀 (CardUI)
               ├─ 雷杀 (CardUI)
               ├─ 闪 (CardUI)
               ├─ 桃 (CardUI)
               ├─ 酒 (CardUI)
               ├─ 顺手牵羊 (CardUI)
               ├─ 无懈可击 (CardUI)
               └─ 费 (CardUI)
```

核心脚本分工：

- `Card.cs`：定义卡牌类型、费用和文本效果。
- `Character.cs`：定义 `CharacterData`、`CharacterInstance`、`Gender` 和 `CharacterDatabase`，统一管理角色名称、性别、最大生命和默认技能档案；当前阶段不自动绑定角色技能。
- `Player.cs`：维护角色档案引用、当前战斗生命、费用、治疗和受伤逻辑；当前白板模式双方初始生命为 40。
- `EnemyAI.cs`：封装敌方基础权重随机行动选择。
- `TriggerTiming.cs`：定义 `TriggerTiming`、五阶段 `BattlePhase` 和 `DyingState`。
- `TriggerManager.cs`：统一注册触发器效果，并通过 `EffectQueue` 按 `EffectPriority` 排序执行。
- `BattleContext.cs`：保存当前阶段、双方出牌、伤害事件、触发日志和胜负状态。
- `BattleRules.cs`：注册默认触发器，并承载当前卡牌、伤害、防御、濒死和死亡规则。
- `Skill.cs`：定义技能分类、稀有度、类型、详情文本和技能数据库。
- `Reaction.cs`：定义通用响应接口、响应选项和响应队列。
- `ReactionWindow.cs`：运行时创建通用实时响应控件，用于龙胆和未来所有响应类技能；战斗区中下部显示技能发动与 `ReactionProgressBar`，行动栏通过 ReactionMode 显示响应选项。
- `BattleManager.cs`：只负责五阶段推进、UI 刷新、玩家输入、飘字反馈、最近战报、运行时创建 `BattlePresentationArea` / `SkillDebugWindow` / `BattleLogWindow` 和触发 `TriggerManager`。
- `CardUI.cs`：显示行动牌内容，并通过点击信号通知战斗管理器；固定位置和快捷键由 11 个 `ActionSlot` 管理。

`BattleLogWindow` 在运行时创建，不写入场景文件。窗口包含搜索框、`导出日志` 按钮，以及 `战斗` / `技能` / `系统` 三个 Tab。每个回合以折叠卡片显示，`战斗` Tab 默认只显示玩家和敌方出牌与结果，卡片内可展开查看 TriggerTiming、EffectPriority、EffectQueue、技能触发和卡牌结算顺序。
