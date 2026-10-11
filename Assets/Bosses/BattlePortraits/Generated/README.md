# 第一至四章 Boss AI 立绘

本目录保存新增的原始透明背景 PNG Boss 立绘。战斗中实际加载的是同级 `../Runtime/` 下的固定画布副本；Boss 原顶部状态条保留，立绘复用原有战场模型层，因此不改变页面布局。

第一章重制例外：`boss_healer` 直接加载 `boss_healer_pixel_v2.png`；`boss_traitor` 加载用户指定的 `boss_traitor_supplied_pixel_v13.png`（吕布前列持戟、貂蝉后方数据供能的完整构图；武器已包含在主体中）。背叛者原稿会先降至 128×96 逻辑像素画布，再以 Nearest 映射到 256×192 透明战斗画布，确保呈现清晰、粗颗粒的像素感。`crazy_performer` 加载 `crazy_performer_robot_v3.png`。这样不会再出现原稿更新、战斗仍显示旧 Runtime 副本的问题。华佗不变，背叛者采用清晰的赛博像素造型，卖艺人改为全机械机器人；不改变敌人数值、AI 或技能。详见 `ARMED_BOSS_V3.md`。旧版背叛者资源已移至废纸篓。

## Boss（14）

```text
crazy_performer
boss_healer
boss_traitor
wulong_collective_intelligence
zuoci
gang_boss
huang_yi_zhi_zhu
boss_tyrant
liu_bei
guan_yu
zhang_fei
rat_king
moon_boss
huan_xiang_chu_shou
```

其中 `rat_king` 为下水道事件战斗 Boss，`moon_boss` 为第一章月相分支 Boss，
`huan_xiang_chu_shou` 为第四章深渊路线 4-8 的传奇 Boss。

第四章 Boss `huan_xiang_chu_shou` 使用 `ch4_phantom_tentacle_pixel_v1.png`，由 `ChapterFourPortraitTextures` 映射到 256×192 逻辑像素画布。设计为单眼、机械环节触手与少量幻象方块粒子的深渊实体。

月亮 Boss（`moon_boss`）现使用 `../Runtime/moon_boss_rising_true_pixel.png`，本目录旧原稿及 Runtime 下两张旧版月亮图片已移除；其余现用 Boss 原稿保留。
