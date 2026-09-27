# Weapons 武器资源

## 存放内容

这里存放武器表现资源。

- `RustSteelSword/`：青钢剑-锈，初始默认武器。
- `RustSword/`：锈剑。
- `RustSpear/`：锈矛。
- `RustBow/`：短弓。
- `Sprites/`：武器静态图片。
- `Trails/`：武器挥动轨迹、刀光、残影。

武器资源只负责表现，不负责装备属性和伤害规则。

## 命名规范

```text
Assets/Weapons/RustSword/weapon.png
Assets/Weapons/RustSword/slash_01.png
Assets/Weapons/RustSword/slash_02.png
Assets/Weapons/RustSword/slash_03.png
```

新武器优先使用独立目录，而不是继续把所有图片堆到 `Sprites/` 或 `Trails/`。
目录名就是表现层读取的武器 Id。

## 推荐尺寸

- 武器图片：`256 x 256` 到 `512 x 512`
- 初始武器可更小，当前目录化武器约 `160 x 170`，由动画系统负责缩放。
- 轨迹图片：按效果需要，建议控制在战斗区域宽度的 15% 以内。

## 透明背景

武器和轨迹必须使用透明背景 PNG。

武器图不应包含角色手部或背景，方便挂接到不同角色动作上。

## 引用系统

未来由以下系统引用：

- `WeaponAnimationController`
- `WeaponVisualDatabase`
- `WeaponPresenter`
- `EffectPresenter`
- 装备展示 UI

不要在装备效果代码中直接加载武器图片。
