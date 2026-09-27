# 第一至三章敌人 AI 立绘

本目录保存新增的原始透明背景 PNG 立绘。战斗中实际加载的是同级 `../Runtime/` 下的固定画布副本，以保持原有战场锚点、缩放和页面布局稳定；旧版 `_temp.png` 资源仍完整保留。

第一章两名重制精英例外：收尸人 `corpse_collector` 使用本目录的 `corpse_collector_v2.png`；追捕者的战斗 ID 为 `hunter`，使用本目录的 `pursuer.png`。由 `ElitePortraitTextures` 在加载时以 Nearest 等比映射为 128×160 透明画布并缓存，避免新原稿与旧 Runtime 副本不同步。这两个敌人的旧原稿和旧 Runtime 图已移除。

## 普通与精英敌人（25）

```text
scavenger
gate_guard
feather_guard
abandoned_servant
corpse_collector
hunter
exile_barbarian
steel_guard
wine_drinker
modified_thug
collective_intelligence
shixin_zhe
cyclops
yellow_turban_devotee
giant_pus_sac
giant_mech_cockroach
giant_mech_rat
mihuan_xiao_shou
gunner
renwan_guard
heavy_armor_guard
ice_guard
royal_death_guard
giant_rolling_stone
giant_rolling_log
```

其中 `giant_rolling_stone` 与 `giant_rolling_log` 用于藏宝阁支线战斗；下水道路线敌人也已包含在内。
