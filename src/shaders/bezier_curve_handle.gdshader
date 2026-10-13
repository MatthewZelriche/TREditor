shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled, depth_test_disabled;

uniform vec4 handle_color : source_color = vec4(1.0);
uniform float handle_size_pixels = 10.0;
uniform bool diamond = false;

void vertex() {
	vec4 center_view = MODELVIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0);
	vec4 center_clip = PROJECTION_MATRIX * center_view;
	vec2 offset_ndc = VERTEX.xy * handle_size_pixels * 2.0 / VIEWPORT_SIZE;
	POSITION = center_clip;
	POSITION.xy += offset_ndc * center_clip.w;
}

void fragment() {
	vec2 point = UV * 2.0 - 1.0;
	if (diamond) {
		if (abs(point.x) + abs(point.y) > 1.0) {
			discard;
		}
	} else if (dot(point, point) > 1.0) {
		discard;
	}

	ALBEDO = handle_color.rgb;
	ALPHA = handle_color.a;
}
