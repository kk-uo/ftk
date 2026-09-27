using Godot;

/// <summary>实时分层雨幕、水面涟漪、漂移雾气。所有环境节点均忽略输入。</summary>
public partial class MainMenuAtmosphere : Control
{
    public MainMenuLivingBackground? Background { get; set; }
    private const int RainCount = 240;
    private const int RippleCount = 22;
    private readonly Vector2[] _drops = new Vector2[RainCount];
    private readonly float[] _speeds = new float[RainCount];
    private readonly Vector2[] _ripples = new Vector2[RippleCount];
    private readonly float[] _ages = new float[RippleCount];
    private readonly RandomNumberGenerator _rng = new();
    private readonly Vector2[] _fogPoints = new Vector2[26];
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rng.Randomize();
        for (var i = 0; i < RainCount; i++) ResetDrop(i, true);
        for (var i = 0; i < RippleCount; i++)
        {
            ResetRipple(i);
            _ages[i] = _rng.Randf();
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _elapsed += delta;
        for (var i = 0; i < RainCount; i++)
        {
            // 归一化速度保证窗口缩放不会改变雨的密度与下落节奏。
            _drops[i] += new Vector2(-.14f, 1f) * _speeds[i] * (float)delta;
            if (_drops[i].Y > 1.03f || _drops[i].X < -.03f) ResetDrop(i, false);
        }
        for (var i = 0; i < RippleCount; i++)
        {
            _ages[i] += (float)delta * 1.1f;
            if (_ages[i] >= 1f) ResetRipple(i);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f) return;
        var imageRect = Background?.ImageRect ?? new Rect2(Vector2.Zero, Size);
        DrawFog(imageRect);
        DrawRipples(imageRect);
        for (var i = 0; i < RainCount; i++)
        {
            var near = i % 4 == 0;
            var length = Size.Y * (near ? .020f : .009f);
            var start = _drops[i] * Size;
            var alpha = near ? .36f : .16f;
            // 菜单背后的雨仍会运动，但降低对比度以保留文字可读性。
            if (_drops[i].X < .34f) alpha *= .45f;
            DrawLine(start, start + new Vector2(-length * .25f, length),
                new Color(.52f, .77f, .87f, alpha), near ? 1.6f : 1f, false);
        }
    }

    private void DrawRipples(Rect2 imageRect)
    {
        for (var i = 0; i < RippleCount; i++)
        {
            var center = imageRect.Position + _ripples[i] * imageRect.Size;
            var radius = (2f + _ages[i] * 12f) * imageRect.Size.X / 1672f;
            var alpha = (1f - _ages[i]) * .22f;
            var color = i % 3 == 0 ? new Color(.95f, .24f, .35f, alpha) : new Color(.30f, .76f, .84f, alpha);
            // 扁平菱形水纹使用硬边线条，保持像素画风。
            var a = center + new Vector2(-radius, 0);
            var b = center + new Vector2(0, -radius * .24f);
            var c = center + new Vector2(radius, 0);
            var d = center + new Vector2(0, radius * .24f);
            DrawLine(a, b, color, 1f, false);
            DrawLine(b, c, color, 1f, false);
            DrawLine(c, d, color, 1f, false);
            DrawLine(d, a, color, 1f, false);
        }
    }

    private void DrawFog(Rect2 imageRect)
    {
        for (var band = 0; band < 3; band++)
        {
            var center = imageRect.Position + new Vector2(.69f, .48f + band * .12f) * imageRect.Size;
            center.X += Mathf.Sin((float)_elapsed * .13f + band * 2f) * imageRect.Size.X * .035f;
            var width = imageRect.Size.X * .19f;
            for (var p = 0; p <= 12; p++)
            {
                var progress = p / 12f;
                var taper = Mathf.Sin(progress * Mathf.Pi);
                var wave = Mathf.Sin(progress * 14f + (float)_elapsed * .25f + band);
                var x = center.X - width * .5f + width * progress;
                var y = center.Y + wave * 4f;
                _fogPoints[p] = new Vector2(x, y - taper * 13f);
                _fogPoints[25 - p] = new Vector2(x, y + taper * 13f + .1f);
            }
            DrawColoredPolygon(_fogPoints, new Color(.30f, .55f, .61f, .035f));
        }
    }

    private void ResetDrop(int index, bool initial)
    {
        _drops[index] = new Vector2(_rng.RandfRange(0f, 1.15f), initial ? _rng.Randf() : -.03f);
        _speeds[index] = index % 4 == 0 ? _rng.RandfRange(.55f, .85f) : _rng.RandfRange(.28f, .47f);
    }

    private void ResetRipple(int index)
    {
        _ages[index] = 0f;
        _ripples[index] = new Vector2(_rng.RandfRange(.615f, .855f), _rng.RandfRange(.745f, .855f));
    }
}
