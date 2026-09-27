using Godot;

/// <summary>
/// 补全披风后方景物的底图，仅为触手和破旗做局部网格动画。
/// 披风使用独立透明纹理和网格；其摆动不会修改底图坐标。
/// </summary>
public partial class MainMenuLivingBackground : Control
{
    public const string TexturePath = "res://Assets/Backgrounds/MainMenu/main_menu_clean_plate.png";
    private const int Columns = 100;
    private const int Rows = 60;
    private const double FrameInterval = 1.0 / 30.0;
    private readonly Vector2[] _rest = new Vector2[(Columns + 1) * (Rows + 1)];
    private readonly Vector2[] _vertices = new Vector2[(Columns + 1) * (Rows + 1)];
    private readonly Vector3[] _weights = new Vector3[(Columns + 1) * (Rows + 1)];
    private readonly Color[] _colors = new Color[(Columns + 1) * (Rows + 1)];
    private readonly Vector3[] _lightWeights = new Vector3[(Columns + 1) * (Rows + 1)];
    private Polygon2D? _surface;
    private Vector2 _imageSize;
    private double _elapsed;
    private double _frameTime;

    public Rect2 ImageRect { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var texture = GD.Load<Texture2D>(TexturePath);
        if (texture == null)
        {
            GD.PushError($"主菜单背景加载失败：{TexturePath}");
            SetProcess(false);
            return;
        }
        _imageSize = texture.GetSize();
        var triangles = new Godot.Collections.Array();
        for (var y = 0; y <= Rows; y++)
        for (var x = 0; x <= Columns; x++)
        {
            var index = y * (Columns + 1) + x;
            var uv = new Vector2((float)x / Columns, (float)y / Rows);
            _rest[index] = uv * _imageSize;
            _vertices[index] = _rest[index];
            _colors[index] = Colors.White;
            // 权重随原图坐标定义，任意窗口比例下仍跟随原图物件。
            var left = Mathf.Max(
                Ellipse(uv, new Vector2(.509f, .237f), new Vector2(.112f, .086f)),
                Ellipse(uv, new Vector2(.560f, .322f), new Vector2(.048f, .073f)));
            var right = Mathf.Max(
                Ellipse(uv, new Vector2(.862f, .116f), new Vector2(.079f, .069f)),
                Ellipse(uv, new Vector2(.925f, .230f), new Vector2(.045f, .124f)));
            var banner = Ellipse(uv, new Vector2(.314f, .444f), new Vector2(.026f, .103f));
            _weights[index] = new Vector3(left, right, banner);
            var moon = Ellipse(uv, new Vector2(.709f, .156f), new Vector2(.142f, .235f));
            var core = Mathf.Max(
                Ellipse(uv, new Vector2(.729f, .371f), new Vector2(.020f, .100f)),
                Ellipse(uv, new Vector2(.726f, .329f), new Vector2(.033f, .043f)));
            core = Mathf.Max(core,
                Ellipse(uv, new Vector2(.726f, .177f), new Vector2(.012f, .092f)));
            var windows = Mathf.Max(
                Ellipse(uv, new Vector2(.687f, .386f), new Vector2(.013f, .080f)),
                Ellipse(uv, new Vector2(.779f, .398f), new Vector2(.012f, .068f)));
            _lightWeights[index] = new Vector3(moon, core, windows);
            if (x == Columns || y == Rows) continue;
            var below = index + Columns + 1;
            triangles.Add(new int[] { index, index + 1, below });
            triangles.Add(new int[] { index + 1, below + 1, below });
        }
        _surface = new Polygon2D
        {
            Texture = texture,
            TextureFilter = TextureFilterEnum.Nearest,
            Polygon = _vertices,
            UV = _rest,
            Polygons = triangles,
            Antialiased = false
        };
        AddChild(_surface);
        // 子节点只继承底图的缩放和位置，不继承 Polygon 顶点变形。
        _surface.AddChild(new MainMenuCapeLayer { Name = "IndependentCape", ImageSize = _imageSize });
        Resized += FitImage;
        FitImage();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _frameTime += delta;
        if (_surface == null || _frameTime < FrameInterval) return;
        _frameTime %= FrameInterval;
        var time = (float)_elapsed;
        // 连续低幅闪烁叠加缓慢明暗变化。光源不会完全熄灭，也不会产生全屏闪光。
        var coreLight = -.07f + .16f * Mathf.Sin(time * 2.1f)
            + .045f * Mathf.Sin(time * 9.7f) + .025f * Mathf.Sin(time * 17.3f);
        for (var i = 0; i < _vertices.Length; i++)
        {
            var uv = _rest[i] / _imageSize;
            var w = _weights[i];
            var dx = w.X * 3.2f * Mathf.Sin(time * .62f + uv.Y * 14f)
                + w.Y * 4f * Mathf.Sin(time * .48f + uv.Y * 12f + 1.4f)
                + w.Z * 3f * Mathf.Sin(time * 1.9f + uv.Y * 27f);
            var dy = w.X * 3f * Mathf.Sin(time * .72f + uv.X * 15f)
                + w.Y * 3.5f * Mathf.Sin(time * .56f + uv.X * 16f + 2f);
            _vertices[i] = _rest[i] + new Vector2(dx, dy);
            var lighting = _lightWeights[i];
            var light = 1f + lighting.X * .035f * Mathf.Sin(time * .8f)
                + lighting.Y * coreLight
                + lighting.Z * .13f * Mathf.Sin(time * 3.3f + uv.Y * 80f);
            _colors[i] = new Color(light, light, light, 1f);
        }
        _surface.Polygon = _vertices;
        _surface.VertexColors = _colors;
    }

    private void FitImage()
    {
        if (_surface == null || _imageSize.X <= 0f) return;
        var scale = Mathf.Max(Size.X / _imageSize.X, Size.Y / _imageSize.Y);
        ImageRect = new Rect2((Size - _imageSize * scale) * .5f, _imageSize * scale);
        _surface.Scale = Vector2.One * scale;
        _surface.Position = ImageRect.Position;
    }

    private static float Ellipse(Vector2 uv, Vector2 center, Vector2 radius)
    {
        var distance = ((uv - center) / radius).Length();
        return 1f - Mathf.SmoothStep(.35f, 1f, distance);
    }
}
