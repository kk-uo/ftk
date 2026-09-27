using Godot;

/// <summary>仅覆盖背景与天气的 CRT 层；菜单和弹窗在它之后绘制，保持文字清晰。</summary>
public partial class MainMenuCrtFilter : ColorRect
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"
shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
void fragment() {
    vec2 uv = SCREEN_UV;
    vec3 color = texture(screen_texture, uv).rgb;
    // 细扫描线和轻微荧光屏色偏，不做弯曲以免再次扭曲建筑或披风。
    float scan = 1.0 - 0.13 * (0.5 + 0.5 * cos(FRAGCOORD.y * 2.0943951));
    color *= scan;
    color *= vec3(1.025, 0.99, 1.015);
    vec2 p = UV * 2.0 - 1.0;
    float edge = smoothstep(0.45, 1.45, length(p));
    color *= 1.0 - 0.28 * edge;
    // 圆角管屏暗边，仅占画面最外侧。
    vec2 q = abs(p) - vec2(0.965, 0.945);
    float corner = length(max(q, vec2(0.0)));
    color *= 1.0 - smoothstep(0.025, 0.065, corner);
    COLOR = vec4(color, 1.0);
}"
            }
        };
    }
}
