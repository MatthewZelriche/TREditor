// Matcap material used for selected object outlines. It renders like the default
// matcap but writes stencil reference 1 for the outline compositor.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled, depth_draw_opaque;
stencil_mode write, compare_always, 1;

uniform sampler2D matcap_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 tint : source_color = vec3(1.0);
uniform float intensity : hint_range(0.0, 2.0) = 1.0;
uniform bool flip_y = true;

void fragment() {
	vec3 view_normal = normalize(NORMAL);
	vec2 matcap_uv = view_normal.xy * 0.5 + vec2(0.5);

	if (flip_y) {
		matcap_uv.y = 1.0 - matcap_uv.y;
	}

	ALBEDO = texture(matcap_texture, matcap_uv).rgb * tint * intensity;
}
