# Developer Checklist

## 新增角色图片

□ 使用 PNG。  
□ 文件名使用英文小写和下划线。  
□ 头像放入 `Assets/Characters/Portraits/`。  
□ 战斗立绘放入 `Assets/Characters/BattlePortraits/`。  
□ 背景透明。  
□ Godot 导入成功。  
□ 未来已注册到 `SpriteDatabase`。  
□ 未来已注册到 `CharacterVisualDatabase`。  
□ 游戏内角色选择或战斗界面测试通过。  

## 新增 Boss 图片

□ 使用 PNG。  
□ 文件名带 `boss_` 或 `battle_boss_` 前缀。  
□ Boss 头像放入 `Assets/Bosses/Portraits/`。  
□ Boss 战斗图放入 `Assets/Bosses/BattlePortraits/`。  
□ 多阶段资源带 `phase2`、`phase3` 等后缀。  
□ Godot 导入成功。  
□ 未来已注册到 `CharacterVisualDatabase`。  
□ Boss 战斗界面测试通过。  

## 新增武器

□ 使用 PNG。  
□ 文件名使用 `weapon_名称.png`。  
□ 武器图片放入 `Assets/Weapons/Sprites/`。  
□ 轨迹图片放入 `Assets/Weapons/Trails/`。  
□ 背景透明。  
□ 图片不包含角色手部。  
□ Godot 导入成功。  
□ 未来已注册到 `WeaponVisualDatabase`。  
□ 战斗表现测试通过。  

## 新增事件背景

□ 使用 PNG。  
□ 文件名使用 `event_bg_名称.png`。  
□ 放入 `Assets/Events/Backgrounds/`。  
□ 分辨率建议 `1920 x 1080` 或 `2560 x 1440`。  
□ 不需要透明背景。  
□ 主体不被 UI 遮挡。  
□ Godot 导入成功。  
□ 未来已注册到 `SpriteDatabase`。  
□ 事件界面测试通过。  

## 新增 UI

□ 按钮放入 `Assets/UI/Buttons/`。  
□ 面板放入 `Assets/UI/Panels/`。  
□ 边框放入 `Assets/UI/Frames/`。  
□ 图标放入 `Assets/UI/Icons/`。  
□ 鼠标指针放入 `Assets/UI/Cursor/`。  
□ 命名使用 `ui_` 前缀。  
□ 图标和边框透明背景。  
□ 小尺寸下仍然清晰。  
□ Godot 导入成功。  
□ UI 内测试显示正常。  

## 新增字体

□ 使用 OTF 或 TTF。  
□ 放入 `Assets/Fonts/`。  
□ 保留字体原始名称。  
□ 确认支持简体中文。  
□ 确认英文、数字、符号正常。  
□ Godot 导入成功。  
□ Theme 中引用正确。  
□ 游戏内中文无乱码。  

## 新增音效

□ BGM 使用 OGG。  
□ SFX 使用 WAV 或 OGG。  
□ BGM 放入 `Assets/Audio/BGM/`。  
□ SFX 放入 `Assets/Audio/SFX/`。  
□ Voice 放入 `Assets/Audio/Voice/`。  
□ 命名使用 `bgm_`、`sfx_`、`voice_` 前缀。  
□ 音量合理。  
□ 起音无明显空白。  
□ Godot 导入成功。  
□ 未来已注册到 `AudioDatabase`。  
□ 游戏内播放测试通过。  

## 新增特效

□ 使用 PNG。  
□ 放入 `Assets/Effects/` 对应分类目录。  
□ 命名使用 `effect_元素_用途.png`。  
□ 背景透明。  
□ 半透明边缘干净。  
□ Godot 导入成功。  
□ 未来已注册到 `SpriteDatabase`。  
□ 未来已注册到 `EffectDatabase`。  
□ 战斗表现测试通过。  
