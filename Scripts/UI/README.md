# UI System / UI Icon System

`Scripts/UI/` 是通用 UI 工具的落脚点，目前只有一个文件：`IconLibrary.cs`。
这里说明整个"以后所有 UI 都优先显示图片，而不是文字"升级里，图片是怎么
从数据一路查到 UI 上、查不到又该怎么办。

## 整体链路

```
业务数据（EquipmentDefinition.Id / CharacterData.Id / ...）
        ↓
XxxVisualDatabase / XxxIconDatabase（业务 Id → SpriteId）
        ↓
SpriteDatabase（SpriteId → res:// 路径）
        ↓
Texture2D
        ↓
UI（TextureRect）
```

三层各自的职责：

- **业务数据**（`EquipmentDefinition` 等）：完全不知道图片这件事，
  本次升级没有给它们新增任何图片相关字段。
- **XxxVisualDatabase / XxxIconDatabase**（例如 `EquipmentIconDatabase`、
  已有的 `CharacterVisualDatabase`）：只做"业务 Id → SpriteId"这一层映射，
  在 `Scripts/Presentation/` 下。
- **SpriteDatabase**：全项目唯一的"SpriteId → 路径"入口，见
  `Scripts/Presentation/README.md`。

`IconLibrary` 不参与前两层的映射关系，只负责第三步之后的事：**图片查不到
的时候怎么办**。

## IconLibrary

- `IconLibrary.GetDefaultIcon()`：默认占位图标。优先从 `SpriteDatabase`
  查 `"ui_icon_default"`（以后美术可以注册一张真正的默认图标贴图）；
  如果连这个也没注册，退回到程序生成的纯色占位纹理（64×64，带一圈边框），
  不依赖任何外部资源文件，保证任何情况下都不会报错、不会空白。
- `IconLibrary.ResolveEquipmentIcon(equipmentId)`：装备图标解析 + 兜底
  一步到位，`InventoryItemUI`/`CharacterSelectController` 等 UI 只需要调用
  这一个方法。

## Tooltip（统一入口，不在这个文件夹里，但属于同一套体系）

`Scripts/TooltipManager.cs` 新增了 `TooltipManager.ShowRich(TooltipContent, source)`，
和原来的 `TooltipManager.Show(text, source)` 共用同一个 `TooltipPanel`。
装备、技能、Buff、事件、角色以后都应该构造一份 `TooltipContent`
（标题/副标题/品质颜色/图片/描述/额外说明，除描述外全部可选，留空的字段
会自动隐藏对应的行）交给 `ShowRich`，不要每个系统自己再写一个 Tooltip 弹窗。

```csharp
var content = new TooltipContent(
    description: Localization.GetDescription(definition),
    title: Localization.GetName(definition),
    subtitle: $"{Localization.GetRarityName(definition.Rarity)} · {typesText}",
    titleColor: rarityColor,
    icon: IconLibrary.ResolveEquipmentIcon(definition.Id));

TooltipManager.ShowRich(content, this);
```

## 以后如何给一个新系统接入图片 + Tooltip

1. 确认这个系统有没有已有的"业务 Id"（装备用 `Id`，角色用 `Id`，事件用
   `FixedEventId`……）。
2. 如果还没有对应的 `XxxVisualDatabase`/`XxxIconDatabase`，参考
   `EquipmentIconDatabase` 新建一个（业务 Id → SpriteId 的映射，
   不要往业务数据类上加字段）。
3. UI 里用 `IconLibrary`（或该系统自己类似的 Resolve 方法）拿到 `Texture2D`
   丢给 `TextureRect`，不要在 UI 代码里直接 `GD.Load` 或写 `Assets/` 路径。
4. 悬停提示统一构造 `TooltipContent` 交给 `TooltipManager.ShowRich`。

## 当前已知的后续工作（这次没有做，留给以后）

`CharacterStatusCard.cs` 里战斗状态卡的技能/Buff/装备徽标
（`MakeStatusBadge`/`MakeEquipmentBadge`）目前仍然是文字/emoji，
因为它们读取的 `BattleUnitUiFormatter.GetEquipments`/状态格式化结果
目前只返回已本地化的展示字符串，没有携带业务 Id，还不能直接接
`EquipmentIconDatabase`。以后要把这部分也换成图标，需要先给
`BattleUnitUiFormatter` 的返回值补上对应的 Id，再复用这里的
`IconLibrary`/`TooltipContent` 体系，不需要另起一套。
