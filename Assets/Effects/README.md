# Effects 特效资源

## 存放内容

这里存放攻击、治疗、防护、Buff、Debuff 等表现特效。

- `Slash/`：普通斩击。
- `Fire/`：火焰特效。
- `Thunder/`：雷电特效。
- `Ice/`：冰冻特效。
- `Poison/`：毒素特效。
- `Heal/`：治疗特效。
- `Shield/`：护盾和格挡特效。
- `Buff/`：增益特效。
- `Debuff/`：负面状态特效。

## 命名规范

```text
effect_slash_hit.png
effect_fire_hit.png
effect_thunder_trail.png
effect_ice_burst.png
effect_poison_mist.png
effect_heal_glow.png
effect_shield_block.png
effect_buff_ring.png
effect_debuff_smoke.png
```

## 推荐尺寸

- 小型命中特效：`256 x 256`
- 大型爆发特效：`512 x 512`
- 轨迹类特效：`512 x 256` 或 `1024 x 256`

## 透明背景

特效必须使用透明背景 PNG。

发光、烟雾、火焰、雷电需要保留半透明边缘，不要导出成白底或黑底。

## 引用系统

未来由以下系统引用：

- `EffectDatabase`
- `EffectPresenter`
- `InteractionPresenter`
- `PresentationManager`

Damage 不应该直接加载或播放这里的资源。
