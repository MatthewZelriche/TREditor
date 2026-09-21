shader_type spatial;
render_mode unshaded, cull_back, fog_disabled;

uniform sampler2D matcap : source_color, filter_linear, repeat_disable;

void fragment() {
	// View-space normals mapped to the matcap sphere (diameter matches texture edges).
	vec2 uv = normalize(NORMAL).xy * 0.5 + vec2(0.5);
	ALBEDO = texture(matcap, uv).rgb;
}
