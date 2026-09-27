# 02 Naming Convention

## 总原则

资源命名必须：

- 使用英文小写。
- 使用下划线分隔。
- 能从文件名判断类型和用途。
- 不使用空格。
- 不使用中文文件名。
- 不使用临时名，例如 `new_image.png`、`final2.png`。

推荐格式：

```text
分类_名称_用途.png
```

## 角色

```text
portrait_zhaoyun.png
portrait_lvbu.png
battle_zhaoyun.png
battle_lvbu.png
icon_zhaoyun.png
```

角色名应使用角色数据库中的稳定英文 ID。

## Boss

```text
boss_doctor.png
boss_traitor.png
boss_tyrant.png
battle_boss_doctor.png
battle_boss_tyrant_phase2.png
```

Boss 多阶段使用：

```text
phase2
phase3
enraged
```

## Weapon

```text
weapon_qinggang.png
weapon_chitu.png
weapon_dilu.png
trail_qinggang_slash.png
trail_shadow_blade.png
```

武器图片使用 `weapon_`，轨迹使用 `trail_`。

## Card

```text
card_slay.png
card_fire_slay.png
card_thunder_slay.png
card_shadow_slay.png
icon_card_attack.png
icon_card_recovery.png
```

卡牌插画使用 `card_`，卡牌图标使用 `icon_card_`。

## Effect

```text
effect_slash_hit.png
effect_fire_hit.png
effect_fire_trail.png
effect_thunder_hit.png
effect_thunder_trail.png
effect_ice_burst.png
effect_poison_mist.png
effect_heal_glow.png
effect_shield_block.png
effect_buff_ring.png
effect_debuff_smoke.png
```

元素名放在中间，表现用途放在最后。

## UI

```text
ui_button_blue.png
ui_button_disabled.png
ui_panel_common.png
ui_panel_battle_log.png
ui_frame_rare.png
ui_icon_gold.png
ui_icon_food.png
ui_cursor_default.png
```

UI 资源必须带 `ui_` 前缀。

## Background

```text
bg_chapter1.png
bg_chibi.png
bg_battle_palace.png
bg_map_chapter3.png
event_bg_chibi_wreck.png
event_bg_imperial_temple.png
```

通用背景使用 `bg_`，事件背景使用 `event_bg_`。

## Audio

```text
sfx_attack.wav
sfx_fire_hit.wav
sfx_button_click.wav
sfx_heal.wav
bgm_chapter1.ogg
bgm_boss_tyrant.ogg
voice_zhaoyun_attack.ogg
```

音频前缀：

- `sfx_`：短音效。
- `bgm_`：背景音乐。
- `voice_`：语音。

## 禁止命名

不要使用：

```text
赵云立绘.png
fire slay.png
image.png
test.png
final.png
final_final.png
button-new.png
BG01.png
```

原因：

- 中文路径可能造成跨平台工具问题。
- 空格和大小写会增加引用错误。
- 临时名无法长期维护。
- 资源数据库需要稳定 ID。
