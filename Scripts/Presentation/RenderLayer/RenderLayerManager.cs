//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/RenderLayer/RenderLayerManager.cs
//
// 模块：Presentation System / Render Layer System
//
// 为什么存在：
// WeaponPresenter、EffectPlayer 以前各自维护一个私有的 CanvasLayer
// （"WeaponPresenterOverlay"、"EffectPlayerOverlay"），层号各写各的。
// RenderLayerManager 提供一个共享入口：同一个 RenderLayer 在同一棵场景树里
// 只会对应一个 CanvasLayer 实例，新的 Presenter 不需要再各自发明一套
// "建 CanvasLayer、缓存、判断是否还有效"的样板代码。
//
// 职责：
// 1. 把 RenderLayer 枚举值转换成 Godot 的 CanvasLayer.Layer 整数
//    （ToGodotLayer——目前就是枚举的底层数值，转换封装在这里，以后如果
//    数值方案变化，只需要改这一个方法）。
// 2. 按 RenderLayer 提供/复用共享的 CanvasLayer 节点（GetOrCreateCanvasLayer）。
//
// 不负责：
// × 决定某个具体资源用哪一层（由各自的 Definition 决定）。
// × 播放任何表现、实例化 Sprite2D 等具体节点。
//
// 主要依赖：
// RenderLayer
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using Godot;

/// <summary>
/// Render Layer 的运行时管理器：RenderLayer → 共享 CanvasLayer。
/// </summary>
public static class RenderLayerManager
{
    private static readonly Dictionary<RenderLayer, CanvasLayer> ActiveLayers = new();

    /// <summary>
    /// 把 RenderLayer 转换成 Godot CanvasLayer.Layer 数值。
    ///
    /// 目前直接使用枚举的底层数值；以后如果渲染方案变化（例如需要更大的层间距），
    /// 只需要改这一个方法，不需要改任何调用方。
    /// </summary>
    public static int ToGodotLayer(RenderLayer layer) => (int)layer;

    /// <summary>
    /// 获取（或创建）某个 RenderLayer 对应的共享 CanvasLayer。
    ///
    /// 同一个 RenderLayer 在同一次运行里只会创建一个 CanvasLayer 节点，
    /// 多个 Presenter（武器、特效、未来的 UI）挂同一个 RenderLayer 时
    /// 会共用这一个节点，不会互相冲突或产生多余的 CanvasLayer。
    /// 场景切换导致旧节点失效时会自动重新创建。
    /// </summary>
    public static CanvasLayer GetOrCreateCanvasLayer(SceneTree tree, RenderLayer layer)
    {
        if (ActiveLayers.TryGetValue(layer, out var existing)
            && GodotObject.IsInstanceValid(existing)
            && existing.IsInsideTree())
        {
            return existing;
        }

        var canvasLayer = new CanvasLayer
        {
            Layer = ToGodotLayer(layer),
            Name = $"RenderLayer_{layer}"
        };
        tree.Root.AddChild(canvasLayer);
        ActiveLayers[layer] = canvasLayer;
        return canvasLayer;
    }
}
