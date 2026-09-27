//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/EnemyPresenter.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 为敌人模型注册 Hover 与点击输入。
// 2. 将输入事件转发给 TooltipPresenter / SelectionPresenter 的调用方。
// 3. 保持敌人模型输入与具体 Tooltip/高亮实现解耦。
//
// 不负责：
// × 构造 Tooltip 内容。
// × 决定高亮颜色。
// × 修改 Selected Target 或任何战斗规则。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 敌人模型输入 Presenter。
///
/// 该类只负责把模型上的鼠标事件转发出去，具体 Hover Tooltip 与 Selection
/// 高亮由更上层统一 Presenter 决定。
/// </summary>
public sealed class EnemyPresenter
{
    private readonly Control _model;
    private readonly EnemyInstance _enemy;
    private readonly Action<EnemyInstance, Control> _hoverEntered;
    private readonly Action<EnemyInstance> _hoverExited;
    private readonly Action<EnemyInstance, InputEvent> _inputReceived;

    /// <summary>
    /// 创建敌人模型输入 Presenter，并立即注册模型事件。
    /// </summary>
    public EnemyPresenter(
        Control model,
        EnemyInstance enemy,
        Action<EnemyInstance, Control> hoverEntered,
        Action<EnemyInstance> hoverExited,
        Action<EnemyInstance, InputEvent> inputReceived)
    {
        _model = model;
        _enemy = enemy;
        _hoverEntered = hoverEntered;
        _hoverExited = hoverExited;
        _inputReceived = inputReceived;

        _model.MouseEntered += OnMouseEntered;
        _model.MouseExited += OnMouseExited;
        _model.GuiInput += OnGuiInput;
    }

    private void OnMouseEntered()
    {
        _hoverEntered(_enemy, _model);
    }

    private void OnMouseExited()
    {
        _hoverExited(_enemy);
    }

    private void OnGuiInput(InputEvent inputEvent)
    {
        _inputReceived(_enemy, inputEvent);
    }
}
