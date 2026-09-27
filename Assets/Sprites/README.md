# Sprites 历史资源目录

## 存放内容

这是历史保留目录。

如果已有资源依赖该目录，不要随意移动，避免 Godot 导入路径失效。

## 新增资源规则

不建议继续向这里新增资源。

新增图片应放入新的分类目录：

- 角色：`Assets/Characters/`
- Boss：`Assets/Bosses/`
- 武器：`Assets/Weapons/`
- 卡牌：`Assets/Cards/`
- 特效：`Assets/Effects/`
- UI：`Assets/UI/`
- 背景：`Assets/Backgrounds/`

## 命名规范

如果必须临时放入这里，仍然遵守项目统一命名规范：

```text
分类_名称_用途.png
```

## 透明背景

按具体资源类型决定。图标、角色、武器、特效需要透明背景；背景图不需要。

## 引用系统

未来资源引用应逐步迁移到：

- `SpriteDatabase`
- `CharacterVisualDatabase`
- `WeaponVisualDatabase`
- `EffectDatabase`

不要让新代码继续扩大这个历史目录的使用范围。
