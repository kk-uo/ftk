# Audio 音频资源

## 存放内容

这里存放所有音频资源。

- `BGM/`：背景音乐。
- `SFX/`：按钮、攻击、受击、治疗、Buff 等音效。
- `Voice/`：角色语音或旁白。

## 命名规范

```text
bgm_chapter1.ogg
bgm_boss_tyrant.ogg
sfx_attack.wav
sfx_fire_hit.wav
sfx_button_click.wav
voice_zhaoyun_attack.ogg
```

## 推荐格式

- BGM：`.ogg`
- SFX：`.wav` 或 `.ogg`
- Voice：`.ogg`

BGM 使用压缩格式节省体积，短音效可使用 WAV 保留清晰度。

## 推荐长度

- UI 音效：0.1 到 0.5 秒。
- 攻击/受击音效：0.2 到 1.0 秒。
- BGM：可循环，建议提前处理无缝循环点。

## 透明背景

音频没有透明背景要求。

## 引用系统

未来由以下系统引用：

- `AudioDatabase`
- `PresentationManager`
- `EffectPresenter`
- `UIPresenter`
- 设置界面音量控制

Damage、Reward、Battle 不应该直接播放音效。
