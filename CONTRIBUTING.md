# 开发规范

## 基本原则

- 使用 C# 与 Godot 4.6 .NET。
- 保持代码简单、明确、可维护。
- UI 逻辑和游戏逻辑分离。
- 不引入不必要的插件或外部依赖。
- 不为了单个功能绕过已有 Manager、Trigger、Reward、Localization 系统。

## 命名规范

- 类型名使用 PascalCase。
- 方法名使用 PascalCase。
- 局部变量和参数使用 camelCase。
- 常量使用 PascalCase，除非项目已有局部约定。
- 稳定 ID 使用小写蛇形或项目已有格式，必须保持向后兼容。

## 文件与目录

- 战斗生命周期放在 `Scripts/Battle/`。
- 伤害、防御、濒死、死亡放在 `Scripts/Damage/`。
- 装备战斗效果放在 `Scripts/Equipment/`。
- 实时反应选项放在 `Scripts/Reactions/`。
- 技能效果放在 `Scripts/Skills/` 或 `Scripts/SkillEffects/`。
- UI Controller 放在 `Scripts/`，必要时再拆分目录。

不要为了一个功能创建过深目录。

## Localization

所有玩家可见文本必须本地化。

使用：

- `Localization.Get(key)`
- `Localization.GetName(definition)`
- `Localization.GetDescription(definition)`

禁止：

- 在 UI 中硬编码中文或英文
- 混用 `Name`、`DisplayName`、`Description`、`DisplayDescription`
- 语言切换后要求玩家重新打开界面

新增 UI 时必须测试中文和英文切换。

## Code

- 不在 `BattleManager` 写角色、装备、技能的特殊判断。
- 不在事件代码里直接修改玩家金币、生命、装备、技能。
- 不在 UI 控制器里直接改跨系统状态。
- 复杂业务逻辑必须通过中文注释解释设计原因。
- 超过 80 行的函数应使用模块分区注释。

模块分区格式：

```csharp
// ======================================================
// 战斗结算
// ======================================================
```

## EventCode

新增事件时：

1. 事件定义放在事件数据库或事件工厂。
2. 奖励使用 `RewardSequence`。
3. 选择使用 `ChoicePanel`。
4. 条件、稀有度、章节、RunOnce 在数据层表达。
5. 文本进入 Localization。

事件不应该直接：

- 加金币
- 扣生命
- 加装备
- 加技能
- 跳过 RewardManager 修改状态

## EquipmentCode

新增装备时：

1. 增加稳定装备 ID。
2. 增加装备定义。
3. 增加本地化名称与描述。
4. 战斗效果实现为 `IBattleEffect`。
5. 随机池通过 `CanAppearInRandomPool` 控制。

不要通过硬编码 ID 排除随机装备。

## SkillCode

新增技能时：

1. 增加稳定技能 ID。
2. 增加技能定义和本地化文本。
3. 被动战斗效果实现为 `IBattleEffect`。
4. 主动或反应技能复用 Reaction Window。
5. 技能触发必须通过 `TriggerTiming`。

不要在战斗主流程直接判断某个技能 ID。

## Animation

动画属于 Presentation。

战斗、伤害、奖励系统只产出状态变化或表现请求，不直接绑定具体动画节点。

后续替换 Spine、Tween 或其它演出方案时，不应修改核心结算规则。

## Presentation

表现层负责：

- 浮动伤害数字
- 治疗数字
- 资源变化提示
- 战斗日志
- 卡牌高亮
- 事件演出

表现层不负责：

- 伤害计算
- 奖励执行
- 技能触发顺序
- Buff 生命周期

## Documentation

每个新文件必须包含统一文件头。

每个 public class、enum、interface、struct 必须有 XML Documentation。

每个 public、protected、internal 方法必须有 XML Documentation。

复杂业务逻辑必须添加中文说明，解释为什么这样设计，而不是翻译代码。

新增或调整模块职责时必须同步更新对应 README。

## TODO

TODO 必须带模块名。

允许：

```csharp
// TODO(Animation):
// 后续替换为 Spine 动画。
```

禁止：

```csharp
// TODO
```

## Build

提交前必须执行：

```bash
dotnet build rouge3c.csproj -v:minimal
```

要求：

- `0 Error`
- `0 Warning`

如果无法构建，必须在提交说明或回复中明确原因。

## 文档同步要求

以后 Claude Code 或人工开发任何新代码时，必须同步更新：

- README
- XML Documentation
- Localization
- ARCHITECTURE.md
- CONTRIBUTING.md

不要让文档描述和实际代码行为分离。
