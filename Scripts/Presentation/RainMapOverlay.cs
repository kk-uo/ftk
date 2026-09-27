using Godot;

public partial class RainMapOverlay : Control
{
    private const int DropCount = 190;
    private readonly Vector2[] _drops = new Vector2[DropCount];
    private readonly float[] _speeds = new float[DropCount];
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        for (var i = 0; i < DropCount; i++) ResetDrop(i, true);
    }

    public override void _Process(double delta)
    {
        for (var i = 0; i < DropCount; i++)
        {
            _drops[i] += new Vector2(-_speeds[i] * 0.22f, _speeds[i]) * (float)delta;
            if (_drops[i].Y > Size.Y + 24 || _drops[i].X < -24) ResetDrop(i, false);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < DropCount; i++)
        {
            var cyan = i % 5 == 0;
            DrawLine(_drops[i], _drops[i] + new Vector2(-2, 10), cyan ? new Color(0.12f, 0.92f, 1f, 0.48f) : new Color(0.45f, 0.66f, 0.92f, 0.32f), 1f, false);
        }
    }

    private void ResetDrop(int index, bool initial)
    {
        _drops[index] = new Vector2(_rng.RandfRange(0, Mathf.Max(1, Size.X)), initial ? _rng.RandfRange(0, Mathf.Max(1, Size.Y)) : -16);
        _speeds[index] = _rng.RandfRange(520, 900);
    }
}
