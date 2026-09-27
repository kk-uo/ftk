# Art Documentation 美术文档入口

## 文档目标

这里记录 Rouge3C 项目的资源导入流程、美术命名规范、快速开始、FAQ 和开发检查清单。

这些文档服务于未来开发者和美术协作者，目标是让所有图片、动画、音效和字体都能按同一套规则进入项目。

## 阅读顺序

```text
01_Asset_Import_Pipeline.md
  |
  v
02_Naming_Convention.md
  |
  v
03_Quick_Start.md
  |
  v
Developer_Checklist.md
  |
  v
FAQ.md
```

## 核心原则

- 资源必须放到正确目录。
- 文件名必须稳定、可读、可搜索。
- 玩家可见图片优先使用 PNG。
- 特效、角色、武器、图标需要透明背景。
- 背景图不需要透明背景。
- 音频按 BGM、SFX、Voice 分类。
- 未来代码引用资源必须走 Database，不直接写死路径。

## 相关系统

未来资源会被以下系统引用：

- `SpriteDatabase`
- `AnimationDatabase`
- `AudioDatabase`
- `EffectDatabase`
- `WeaponVisualDatabase`
- `CharacterVisualDatabase`
- Godot Theme
- Presentation Framework
