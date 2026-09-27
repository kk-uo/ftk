# Bosses Boss 资源

## 存放内容

这里存放 Boss 专属头像和战斗立绘。

- `Portraits/`：Boss 头像。
- `BattlePortraits/`：Boss 战斗立绘或阶段形态图。

Boss 资源独立于 `Characters/`，因为 Boss 可能有多阶段、巨大体型、特殊演出和章节主题背景。

## 命名规范

```text
boss_doctor.png
boss_traitor.png
boss_tyrant_phase2.png
battle_boss_doctor.png
```

多阶段 Boss 使用：

```text
boss_名称_phase2.png
```

## 推荐尺寸

- Boss 头像：`512 x 512`
- Boss 战斗立绘：`1536 x 1536` 或根据 Boss 体型调整

## 透明背景

Boss 头像和战斗立绘建议使用透明背景 PNG。

如果 Boss 图本身包含场景背景，应放入事件背景或战斗背景目录，不要混入 Boss 立绘。

## 引用系统

未来由以下系统引用：

- `CharacterVisualDatabase`
- Boss 战斗 UI
- Boss 图鉴
- Boss 入场演出
- 章节事件
