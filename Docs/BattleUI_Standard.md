# Battle UI Standard

## 核心原则

**BattleManager 禁止触碰任何布局属性。**  
BattleManager 唯一允许调用的 UI 方法是：

```csharp
card.Refresh(unit, context, team, isSelected, stateText);
```

所有 `CustomMinimumSize`、`SizeFlagsHorizontal`、`SizeFlagsVertical`、`Anchor`、`Margin`、`Padding`、`ClipContents` 的设置均在 `BattleCardLayout` 或 `CharacterStatusCard` 内部完成。

---

## 文件职责

| 文件 | 职责 |
|------|------|
| `CharacterStatusCard.cs` | 构建并维护单张卡片的全部内部布局（头像、名称、HP、费用、状态、徽章）。`Refresh()` 是唯一的外部数据入口。 |
| `BattleCardLayout.cs` | 决定每张卡的尺寸和容器间距，依据敌人数量自动调整。`Configure*Card()`、`Configure*Container()` 是唯一的布局配置入口。 |
| `BattleUIValidator.cs` | 战斗开始后校验所有卡片的尺寸是否合法，用 `GD.PrintErr` 报告异常（非致命）。 |
| `BattleUnitUiFormatter.cs` | 将 `BattleUnit` 的状态/装备/技能数据转换为 UI 字符串。`BuildStatusTooltip()`、`BuildOverflowTooltip()` 均为 public static。 |

---

## CharacterStatusCard 内部结构

```
PanelContainer (CharacterStatusCard)
└── HBoxContainer (row, sep=14)
    ├── PanelContainer (_avatarPanel, 78×78, ShrinkCenter-V)
    │   └── Label (_avatarLabel, font=22)
    └── VBoxContainer (rightColumn, ExpandFill-H, ShrinkBegin-V, sep=2)
        ├── Label (_nameLabel, font=18, ExpandFill-H)
        ├── Label (_hpLabel, font=20, ExpandFill-H)
        ├── Label (_manaLabel, font=18, ExpandFill-H)
        ├── Label (_stateLabel, font=13, ExpandFill-H)
        └── HBoxContainer (_statusArea, minH=26, sep=4)
            ├── [Label × MaxStatusBadges]  ← 状态/装备徽章
            └── [Label "…"]               ← 溢出徽章（当 total > MaxStatusBadges 时）
```

---

## BattleCardLayout 尺寸表

### 敌方卡片宽度（按敌人数量）

| 敌人数 | 卡片宽度 | 容器间距 |
|--------|----------|----------|
| 1 | 420 px | 28 px |
| 2 | 340 px | 24 px |
| 3 | 280 px | 18 px |
| 4 | 230 px | 14 px |
| N>4 | max(180, 1120/N) | 10 px |

### 玩家卡片宽度

| 玩家数 | 卡片宽度 |
|--------|----------|
| 1 | 420 px |
| N>1 | max(260, 1120/N) px |

### 最大状态徽章数

```
MaxBadges = Clamp((cardWidth - 128 - 26) / 30, 1, 6)
```

---

## 防崩溃规则

**禁止出现的组合：**  
`SizeFlagsHorizontal = ExpandFill` + `CustomMinimumSize.X = 0` + 父容器为 `ShrinkBegin`

→ 该组合会使容器最小宽度计算为 0，导致卡片渲染宽度折叠为 0，所有内容被 `ClipContents` 裁剪，界面完全空白。

**`BattleCardLayout` 的设计保证：**  
所有 `Configure*Card()` 方法强制设置 `SizeFlagsHorizontal = ShrinkCenter` 并写入正数 `CustomMinimumSize.X`（最小 180 px），永远不会触发上述崩溃组合。

---

## 回归测试检查项

每次修改 `CharacterStatusCard`、`BattleCardLayout`、`BattleManager.cs` 任意一处后，在游戏内验证：

- [ ] **1v1**：玩家卡 420 px，敌方卡 420 px，头像/名称/HP/费用/状态全部可见
- [ ] **1v2**：玩家卡 420 px，敌方卡各 340 px，两张卡均可见且不重叠
- [ ] **1v3**：玩家卡 420 px，敌方卡各 280 px，三张卡均可见，徽章正确截断
- [ ] 死亡单位：卡片显示 ☠ 图标、灰色边框、"生命：已死亡"
- [ ] 选中敌人：高亮黄色边框正确出现在被选中卡片
- [ ] 飘字：伤害/治疗数字出现在正确的头像锚点上方
- [ ] Hover 信息面板：悬停任意卡片 0.2s 后弹出详细面板，离开后关闭

---

## 修改指引

| 想改什么 | 改哪里 |
|----------|--------|
| 卡片内容区间距/字体/颜色 | `CharacterStatusCard` 顶部常量 |
| 徽章图标或徽章排列方式 | `CharacterStatusCard.MakeStatusBadge/MakeEquipmentBadge/MakeMoreBadge` |
| 卡片宽度/容器间距 | `BattleCardLayout` 常量或 `EnemyWidthForCount()` |
| 状态文字格式 | `BattleUnitUiFormatter.BuildStatusTooltip()` |
| 刷新逻辑（显示哪些字段）| `CharacterStatusCard.Refresh()` |
| 飘字/弹窗锚点 | `BattleManager.Popups.cs`（读取 `card.AvatarAnchor`/`card.ManaAnchor`）|
