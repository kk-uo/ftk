using Godot;

/// <summary>
/// 真实 alpha 披风层：破洞与边缘直接露出补全底图，只有布料自己的网格随风摆动。
/// 以原画归一化位置放置，窗口等比覆盖、裁剪和缩放由父背景统一处理。
/// </summary>
public partial class MainMenuCapeLayer : Node2D
{
    public const string TexturePath = "res://Assets/Backgrounds/MainMenu/main_menu_cape.png";
    public Vector2 ImageSize { get; set; }
    private const int Columns = 36;
    private const int Rows = 32;
    private const double FrameInterval = 1.0 / 30.0;
    private readonly Vector2[] _rest = new Vector2[(Columns + 1) * (Rows + 1)];
    private readonly Vector2[] _vertices = new Vector2[(Columns + 1) * (Rows + 1)];
    private readonly float[] _freedom = new float[(Columns + 1) * (Rows + 1)];
    private Polygon2D? _cloth;
    private double _elapsed;
    private double _frameTime;

    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>(TexturePath);
        if (texture == null) return;
        using var image = texture.GetImage();
        var used = GetVisibleBounds(image);
        if (used.Size.X <= 0 || used.Size.Y <= 0)
        {
            GD.PushError("披风图层没有有效的不透明像素。");
            return;
        }
        // 图层素材保留了完整透明画布，读取 alpha 包围框校准到人物肩部。
        // 布料右上边缘连接 x≈850/y≈480，左侧下摆覆盖原画披风所在区域。
        var placement = new Rect2(ImageSize * new Vector2(.346f, .490f),
            ImageSize * new Vector2(.168f, .266f));
        var uvs = new Vector2[_rest.Length];
        var triangles = new Godot.Collections.Array();
        for (var y = 0; y <= Rows; y++)
        for (var x = 0; x <= Columns; x++)
        {
            var index = y * (Columns + 1) + x;
            var uv = new Vector2((float)x / Columns, (float)y / Rows);
            _rest[index] = placement.Position + uv * placement.Size;
            _vertices[index] = _rest[index];
            uvs[index] = (Vector2)used.Position + uv * (Vector2)used.Size;
            var distanceFromShoulder = (uv - new Vector2(.9f, .08f)).Length();
            _freedom[index] = Mathf.SmoothStep(.20f, .94f, distanceFromShoulder);
            // 与护甲相接的右边布料仍保持垂坠；风主要作用于左侧自由下摆。
            _freedom[index] *= 1f - .75f * Mathf.SmoothStep(.78f, 1f, uv.X);
            if (x == Columns || y == Rows) continue;
            var below = index + Columns + 1;
            triangles.Add(new int[] { index, index + 1, below });
            triangles.Add(new int[] { index + 1, below + 1, below });
        }
        _cloth = new Polygon2D
        {
            Name = "CapeMesh",
            Texture = texture,
            TextureFilter = TextureFilterEnum.Nearest,
            Polygon = _vertices,
            UV = uvs,
            Polygons = triangles,
            Antialiased = false
        };
        AddChild(_cloth);
    }

    private static Rect2I GetVisibleBounds(Image image)
    {
        // 生成素材透明区可能残留极低 alpha 噪点；不能让这些噪点改变布料的定位和缩放。
        var minimum = image.GetSize();
        var maximum = new Vector2I(-1, -1);
        for (var y = 0; y < image.GetHeight(); y++)
        for (var x = 0; x < image.GetWidth(); x++)
        {
            if (image.GetPixel(x, y).A < .25f) continue;
            minimum = new Vector2I(Mathf.Min(minimum.X, x), Mathf.Min(minimum.Y, y));
            maximum = new Vector2I(Mathf.Max(maximum.X, x), Mathf.Max(maximum.Y, y));
        }
        return maximum.X < 0 ? new Rect2I() : new Rect2I(minimum, maximum - minimum + Vector2I.One);
    }

    public override void _Process(double delta)
    {
        if (_cloth == null || !IsVisibleInTree()) return;
        _elapsed += delta;
        _frameTime += delta;
        if (_frameTime < FrameInterval) return;
        _frameTime %= FrameInterval;
        var time = (float)_elapsed;
        for (var i = 0; i < _vertices.Length; i++)
        {
            var uv = _rest[i] / ImageSize;
            var wave = Mathf.Sin(time * 2.2f + uv.X * 37f - uv.Y * 18f);
            var flutter = Mathf.Sin(time * 4.1f + uv.Y * 55f);
            _vertices[i] = _rest[i] + _freedom[i] * new Vector2(
                6.5f * wave + 1.4f * flutter,
                9f * wave + 1.8f * flutter);
        }
        _cloth.Polygon = _vertices;
    }
}
