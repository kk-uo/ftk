# Fonts 字体资源

## 存放内容

这里存放项目字体。

当前默认字体：

```text
NotoSerifCJKsc-Black.otf
```

字体资源影响所有中文显示，不能随意替换。

## 命名规范

保留字体原始名称，避免许可证、版本和字体来源不清晰。

示例：

```text
NotoSerifCJKsc-Black.otf
```

## 推荐格式

- `.otf`
- `.ttf`

优先使用支持简体中文、英文、数字和常用符号的字体。

## 透明背景

字体文件没有透明背景要求。

## 引用系统

未来由以下系统引用：

- Godot Theme
- Label
- Button
- Card UI
- Tooltip
- Battle Log

不要在局部 UI 中随意替换字体，除非设计规范明确要求。
