//////////////////////////////////////////////////////////
// 文件：Scripts/Map/MapNodeVisual/MapNodeVisual.cs
//
// 模块：Map System
//
// 为什么存在：
// 地图上的战斗/事件/商店/Boss/藏宝阁节点需要一个能直接点击进入的世界节点：
// 玩家点击已解锁的节点即可立即进入，不需要操作角色走到节点旁边、不需要按键。
// MapNodeVisual 就是这个统一的节点实现，外观完全由 MapNodeVisualDatabase
// 提供的 MapNodeVisualDefinition 决定，不在这里写死任何节点类型判断。
//
// 职责：
// 1. 用 Area2D 的鼠标拾取（InputPickable + CollisionShape2D）检测节点被点击。
// 2. 鼠标悬停在节点上时显示"点击进入"提示，移开时自动隐藏。
// 3. 点击（且 Interactable 为真）时发出 Interacted 信号，交给地图探索场景决定如何进入关卡。
// 4. 按 NodeType 从 MapNodeVisualDatabase 查询显示参数并绘制占位外观。
//
// 不负责：
// × 判断节点是否解锁/已通关（由调用方设置 Interactable）。
// × 决定进入哪个关卡、播放什么战斗/事件（由 MainFlow/GameManager 负责）。
// × 生成地图节点数据本身（NodeData 来自既有的 GameManager.Nodes）。
//
// 主要依赖：
// MapNodeVisualDatabase / MapNodeVisualDefinition / MapNodeType（EventSystem.cs）
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 地图上的一个可交互节点：战斗、事件、精英、商店、Boss 或藏宝阁。
///
/// 每个 MapNodeVisual 持有对既有 <see cref="MapNode"/>（Scripts/EventSystem.cs
/// 里的数据类，命名已被占用，因此这里的 Godot 节点类命名为 MapNodeVisual）
/// 数据的引用（<see cref="NodeData"/>），以及对应的类型（<see cref="NodeType"/>），
/// 具体长什么样完全交给 <see cref="MapNodeVisualDatabase"/> 决定。
/// </summary>
public partial class MapNodeVisual : Area2D
{
    [Signal]
    public delegate void InteractedEventHandler(string nodeId);

    // 地图节点的尺寸统一在这里控制。把表现层放大，而不是改 MapExplorationView
    // 的节点坐标/间距，确保路线、解锁顺序和既有地图布局都不会改变。
    private const float VisualScaleMultiplier = 1.15f;
    private const float MinimumClickRadius = 84f;
    private const float LabelWidth = 170f;
    private const float LabelGap = 8f;
    private const float PromptGap = 30f;

    /// <summary>该节点对应的既有地图数据（Scripts/EventSystem.cs 的 MapNode）。</summary>
    public MapNode? NodeData { get; private set; }

    /// <summary>该节点的类型，决定从 MapNodeVisualDatabase 查询哪一份显示参数。</summary>
    public MapNodeType NodeType { get; private set; }

    /// <summary>
    /// 是否允许交互：对应既有地图逻辑里"已解锁 且 未通关 且 本轮未通关全部关卡"的判断，
    /// 由地图探索场景在生成节点时设置，本类不重复计算这些条件。
    /// </summary>
    public bool Interactable { get; set; } = true;

    // 电量不足导致的不可交互：仅用于 Hover 提示文案区分"电量不足"和其它锁定原因
    // （未解锁/已通关），不影响 Interactable 本身的判断（由调用方传入）。
    private bool _insufficientPower;

    private MapNodeVisualDefinition? _definition;
    private Texture2D? _nodeIconTexture;
    private Label? _interactLabel;
    private Label? _nameLabel;
    private string _displayText = string.Empty;
    private bool _interactionQueued;

    /// <summary>
    /// 用既有地图节点数据初始化这个 MapNodeVisual。必须在 AddChild 之前调用，
    /// 保证 _Ready 执行时数据已经就绪。
    /// </summary>
    /// <param name="nodeData">既有的地图节点数据（Scripts/EventSystem.cs 的 MapNode）。</param>
    /// <param name="interactable">是否允许交互，由调用方根据解锁/通关状态计算。</param>
    /// <param name="displayText">常驻显示在节点下方的文本（沿用原按钮版地图的标记+类型文本）。</param>
    /// <param name="insufficientPower">节点本可进入，但电量不够——用于 Hover 提示区分文案。</param>
    public void Initialize(MapNode nodeData, bool interactable, string displayText, bool insufficientPower = false)
    {
        NodeData = nodeData;
        NodeType = nodeData.Type;
        Interactable = interactable;
        _displayText = displayText;
        _insufficientPower = insufficientPower;
    }

    public override void _Ready()
    {
        // Resolve 按“事件专属外观 → 节点专属外观 → 类型默认外观”解析，
        // 这里不对 NodeType 做任何 switch/if 判断，外观完全由数据库里的数据决定。
        _definition = NodeData != null ? MapNodeVisualDatabase.Resolve(NodeData) : new MapNodeVisualDefinition();
        _nodeIconTexture = TryLoadNodeIcon(_definition);

        // 点击选择模式：Area2D 通过鼠标拾取（InputPickable + CollisionShape2D）
        // 直接检测点击/悬停，不再监听任何物理体进入/离开。
        InputPickable = true;

        var iconBounds = GetRenderedIconBounds();
        var shape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = GetClickRadius(iconBounds) }
        };
        AddChild(shape);

        InputEvent += OnInputEvent;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        _interactLabel = new Label
        {
            Text = _insufficientPower
                ? Localization.Get("map.explore.insufficient_power")
                : Localization.Get("map.explore.interact_prompt"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-LabelWidth * 0.5f, iconBounds.Position.Y - PromptGap),
            CustomMinimumSize = new Vector2(LabelWidth, 0),
            Visible = false,
            ZIndex = 100
        };
        _interactLabel.AddThemeFontSizeOverride("font_size", 20);
        _interactLabel.AddThemeColorOverride("font_color", Colors.White);
        _interactLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _interactLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_interactLabel);

        // 常驻标签：沿用原按钮版地图的"标记+类型"文本，保证从按钮切换到 2D 探索后
        // 不丢失这部分信息（例如未解锁/已通关状态、第几关 Boss 等）。
        _nameLabel = new Label
        {
            Text = _displayText,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-LabelWidth * 0.5f, iconBounds.End.Y + LabelGap),
            CustomMinimumSize = new Vector2(LabelWidth, 0)
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 17);
        _nameLabel.AddThemeColorOverride("font_color", Interactable ? Colors.White : new Color(0.6f, 0.6f, 0.6f));
        _nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _nameLabel.AddThemeConstantOverride("outline_size", 3);
        AddChild(_nameLabel);

        // 常驻"电量不足"徽标：区别于上面 Hover 才显示的 _interactLabel，这个标签
        // 不受鼠标进出控制，只要节点因为电量不够而不可交互就一直显示，方便玩家
        // 一眼看出哪些节点是因为电量被挡住（而不是未解锁/已通关）。
        if (_insufficientPower && !Interactable)
        {
            var badge = new Label
            {
                Text = Localization.Get("map.explore.insufficient_power"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(-LabelWidth * 0.5f, iconBounds.Position.Y - PromptGap),
                CustomMinimumSize = new Vector2(LabelWidth, 0),
                ZIndex = 100
            };
            badge.AddThemeFontSizeOverride("font_size", 18);
            badge.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.35f));
            badge.AddThemeColorOverride("font_outline_color", Colors.Black);
            badge.AddThemeConstantOverride("outline_size", 4);
            AddChild(badge);
        }

        // 最终 ZIndex = 统一 RenderLayer 的数值 + 该节点自己的相对偏移，
        // 例如 Boss 雕像可以配置更高的 RenderLayer 压住 Ground 层的普通地图装饰，
        // 不需要为地图节点写死判断。
        ZIndex = RenderLayerManager.ToGodotLayer(_definition.RenderLayer) + _definition.ZIndex;
        QueueRedraw();
    }

    /// <summary>
    /// 自由探索刷新一批新节点时播放的淡入动画（只做淡入，不做旧节点淡出——
    /// 地图屏幕每次进出节点都会整体重建，旧节点在新节点出现前已经不存在了）。
    /// </summary>
    public void PlaySpawnFadeIn()
    {
        Modulate = new Color(1, 1, 1, 0);
        Scale = new Vector2(0.7f, 0.7f);
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "modulate", Colors.White, 0.25)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", Vector2.One, 0.25)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (!Interactable || NodeData == null)
        {
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && !_interactionQueued)
        {
            _interactionQueued = true;
            InputPickable = false;
            CallDeferred(nameof(EmitInteractedDeferred));
        }
    }

    private void EmitInteractedDeferred()
    {
        if (NodeData == null)
        {
            return;
        }

        EmitSignal(SignalName.Interacted, NodeData.Id);
    }

    private void OnMouseEntered()
    {
        if (_interactLabel != null)
        {
            // 可交互时显示"点击进入"；电量不足时即使不可交互也要显示"电量不足"提示。
            _interactLabel.Visible = Interactable || _insufficientPower;
        }
    }

    private void OnMouseExited()
    {
        if (_interactLabel != null)
        {
            _interactLabel.Visible = false;
        }
    }

    /// <summary>
    /// 绘制节点外观：优先使用 Definition 指定的 NodeIcon 贴图；
    /// 贴图缺失时退回占位圆形和符号，保证地图节点不会因为资源问题消失。
    /// </summary>
    public override void _Draw()
    {
        if (_definition == null)
        {
            return;
        }

        if (_nodeIconTexture != null)
        {
            DrawNodeIcon(_nodeIconTexture, _definition);
            return;
        }

        var radius = 28f * GetEffectiveScale(_definition);
        DrawCircle(_definition.Offset, radius, _definition.PlaceholderColor);

        var font = ThemeDB.FallbackFont;
        var glyphSize = 24 * GetEffectiveScale(_definition);
        var textSize = font.GetStringSize(_definition.PlaceholderGlyph, HorizontalAlignment.Center, -1, (int)glyphSize);
        DrawString(
            font,
            _definition.Offset + new Vector2(-textSize.X * 0.5f, textSize.Y * 0.35f),
            _definition.PlaceholderGlyph,
            HorizontalAlignment.Center,
            -1,
            (int)glyphSize,
            Colors.White);
    }

    private static Texture2D? TryLoadNodeIcon(MapNodeVisualDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.NodeIcon))
        {
            return null;
        }

        var path = SpriteDatabase.GetPath(definition.NodeIcon);
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        return GD.Load<Texture2D>(path);
    }

    private void DrawNodeIcon(Texture2D texture, MapNodeVisualDefinition definition)
    {
        var rect = GetRenderedIconBounds();
        DrawTextureRect(texture, rect, false);
    }

    /// <summary>
    /// 返回当前图标最终绘制的区域。标签、提示、碰撞区域和绘制都共享这一个计算，
    /// 因此放大节点时不会出现文字仍停在旧位置、或点击范围没有一起变大的问题。
    /// </summary>
    private Rect2 GetRenderedIconBounds()
    {
        if (_definition == null)
        {
            return new Rect2();
        }

        if (_nodeIconTexture == null)
        {
            var radius = 28f * GetEffectiveScale(_definition);
            return new Rect2(_definition.Offset - Vector2.One * radius, Vector2.One * radius * 2f);
        }

        const float maxWidth = 96f;
        const float maxHeight = 64f;
        var textureSize = _nodeIconTexture.GetSize();
        if (textureSize.X <= 0 || textureSize.Y <= 0)
        {
            return new Rect2();
        }

        var scale = Mathf.Min(maxWidth / textureSize.X, maxHeight / textureSize.Y) * GetEffectiveScale(_definition);
        var drawSize = textureSize * scale;
        return new Rect2(_definition.Offset - drawSize * 0.5f, drawSize);
    }

    private static float GetEffectiveScale(MapNodeVisualDefinition definition)
    {
        return definition.Scale * VisualScaleMultiplier;
    }

    private static float GetClickRadius(Rect2 iconBounds)
    {
        return Mathf.Max(MinimumClickRadius, Mathf.Max(iconBounds.Size.X, iconBounds.Size.Y) * 0.5f);
    }

}
