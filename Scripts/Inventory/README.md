# Inventory System — 图标与 Tooltip 接入说明

注意：Inventory 相关的 `.cs` 文件目前并没有实际放在 `Scripts/Inventory/`
这个文件夹里，而是和其它系统一样平铺在 `Scripts/` 根目录下
（`InventoryController.cs`、`InventoryManager.cs`、`InventoryItemUI.cs`、
`EquipmentSlotUI.cs`、`DebugInventoryPanel.cs`、`EquipmentDatabase.cs`、
`EquipmentDefinition.cs`、`EquipmentIds.cs`）。这个文件夹只放文档，
本次升级没有移动任何代码文件，避免不必要的大范围改动。

## 这次升级了什么

背包/装备槽默认只显示图片，不再默认显示装备名字；鼠标悬停显示统一
Tooltip（图片 + 名称 + 品质 + 类型 + 描述）。**没有修改任何装备逻辑、
背包数据结构、`EquipmentDatabase`、`EquipmentDefinition` 或 Localization
文件**——图标是通过外挂的一层 `EquipmentIconDatabase`
（`Scripts/Presentation/EquipmentIconDatabase.cs`）接入的，纯读取既有数据。

## InventoryItemUI（背包格子 / 已装备格子共用）

`Scripts/InventoryItemUI.cs`：

- `BuildLayout()` 新增一个 `TextureRect` 作为格子主体，`_nameLabel` 改为
  `Visible = false`（不再默认显示装备名字，字段还留着但只是内部占位）。
- `Refresh()` 里通过 `IconLibrary.ResolveEquipmentIcon(Item.Definition.Id)`
  拿到图标贴图（真实图标 or DefaultIcon 兜底），赋给 `TextureRect.Texture`。
- 悬停（`MouseEntered`）调用 `ShowTooltip()`：构造一份
  `TooltipContent`（标题=装备名称、副标题=品质+类型、标题颜色=品质色、
  图片=图标、描述=装备描述+效果文本，全部来自 `Localization`），
  交给 `TooltipManager.ShowRich(...)`；离开（`MouseExited`）调用
  `TooltipManager.Hide()`。
- 品质边框颜色和 Tooltip 标题颜色现在共用同一个 `GetRarityColor(rarity)`
  静态方法，避免两处颜色不一致。

`EquipmentSlotUI.cs`（角色装备槽）内部本来就是复用 `InventoryItemUI` 显示
已装备物品的，所以这次升级自动覆盖到装备槽，没有单独修改
`EquipmentSlotUI.cs`。

## 装备图标怎么绑定

见 `Scripts/Presentation/README.md` 的 `EquipmentIconDatabase` 小节和
`Scripts/UI/README.md`：装备 Id 按 `"equipment_icon_<Id>"` 的约定自动生成
IconSpriteId，美术资源就位后只需要
`SpriteDatabase.Register("equipment_icon_<Id>", "res://Assets/UI/Icons/xxx.png")`，
不需要改任何 Inventory 代码。

## 图片缺失时

`IconLibrary.ResolveEquipmentIcon` 保证任何装备、任何时候都会返回一个可用的
`Texture2D`——查不到真实图标就用程序生成的占位纹理，不会报错、不会空白。

## 直接拖拽交换

背包物品格和已装备物品格现在使用同一份位置交换协议：

```text
InventoryItemUI
        ↓ 仅提交两个 InstanceId
InventoryController
        ↓
InventoryManager.CanSwapEquipment / SwapEquipment
        ↓
一次性交换两个 OwnedEquipment.EquippedSlot
```

- `EquippedSlot = null` 表示位于背包，因此无需删除或重新创建装备实例。
- 类型、万能槽和普通饰品槽限制统一复用 `CanEquipToSlot`。
- 所有合法性检查都在写入之前完成；失败时两个实例均保持原位置。
- 成功后界面只刷新一次，并播放一次 0.18 秒交换反馈和一次交换音效。
- 背包容量不会影响交换，因为交换不改变拥有装备总数。
