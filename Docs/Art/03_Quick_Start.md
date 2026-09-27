# 03 Quick Start

## 示例一：导入赵云立绘

假设 AI 给了一张：

```text
赵云立绘.png
```

### 1. 重命名

改为：

```text
battle_zhaoyun.png
```

### 2. 放入目录

```text
Assets/Characters/BattlePortraits/battle_zhaoyun.png
```

### 3. Godot Import

打开 Godot 后，资源会自动导入。

检查：

- 图片是否显示透明背景。
- 角色边缘是否干净。
- 立绘是否过小或过大。

### 4. 未来接入

未来通过：

```text
SpriteDatabase
  |
  v
CharacterVisualDatabase
```

注册并引用。

### 5. 测试

打开角色选择或战斗界面，确认图片尺寸、清晰度和位置正常。

## 示例二：导入武器图片

### 1. 命名

```text
weapon_qinggang.png
```

### 2. 放入目录

```text
Assets/Weapons/Sprites/weapon_qinggang.png
```

### 3. 检查

- 必须透明背景。
- 不要带角色手部。
- 武器方向应符合未来动画挂点。

### 4. 未来接入

```text
SpriteDatabase
  |
  v
WeaponVisualDatabase
  |
  v
WeaponPresenter
```

## 示例三：导入事件背景

### 1. 命名

```text
event_bg_chibi_wreck.png
```

### 2. 放入目录

```text
Assets/Events/Backgrounds/event_bg_chibi_wreck.png
```

### 3. 检查

- 不需要透明背景。
- 建议 `1920 x 1080` 或 `2560 x 1440`。
- 主体不要被 UI 面板遮挡。

### 4. 未来接入

```text
SpriteDatabase
  |
  v
Event UI / Presentation
```

## 示例四：导入 UI 图标

### 1. 命名

```text
ui_icon_gold.png
```

### 2. 放入目录

```text
Assets/UI/Icons/ui_icon_gold.png
```

### 3. 检查

- 必须透明背景。
- 小尺寸下仍然可读。
- 颜色不要和深色背景混在一起。

### 4. 未来接入

```text
SpriteDatabase
  |
  v
UIPresenter / UI Controller
```

## 示例五：导入按钮

### 1. 命名

```text
ui_button_blue.png
```

### 2. 放入目录

```text
Assets/UI/Buttons/ui_button_blue.png
```

### 3. 检查

- 是否适合九宫格拉伸。
- 是否支持 hover、pressed、disabled 状态。
- 文字放上去后是否清晰。

### 4. 未来接入

```text
Godot Theme
  |
  v
Button / Panel UI
```

## 示例六：导入音效

### 1. 命名

```text
sfx_attack.wav
```

### 2. 放入目录

```text
Assets/Audio/SFX/sfx_attack.wav
```

### 3. 检查

- 音量不要过大。
- 起音不要有明显空白。
- 尾音不要过长。

### 4. 未来接入

```text
AudioDatabase
  |
  v
PresentationManager
```

## 示例七：导入字体

### 1. 命名

保留字体原名。

```text
NotoSerifCJKsc-Black.otf
```

### 2. 放入目录

```text
Assets/Fonts/
```

### 3. 检查

- 中文是否完整。
- 英文和数字是否正常。
- 是否和项目 UI 风格一致。

### 4. 接入

通过 Godot Theme 统一引用，不要在单个 Label 上随意替换字体。
