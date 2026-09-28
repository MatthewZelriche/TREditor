shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled, depth_draw_never;

uniform vec3 edge_start;
uniform vec3 edge_end;
uniform vec4 edge_color : source_color;
uniform float line_width_pixels = 5.0;
uniform float depth_bias = 0.001;

void vertex() {
	// Pull only the normal, depth-tested pass slightly toward the camera. This prevents the
	// coplanar muted wire or mesh surface from winning an equal-depth test as the view changes.
	// The stencil x-ray pass reuses the same position so both passes remain aligned; with depth
	// testing disabled there, the bias cannot change its occlusion decision.
	vec4 start_view = MODELVIEW_MATRIX * vec4(edge_start, 1.0);
	vec4 end_view = MODELVIEW_MATRIX * vec4(edge_end, 1.0);
	start_view.z += depth_bias;
	end_view.z += depth_bias;
	vec4 start_clip = PROJECTION_MATRIX * start_view;
	vec4 end_clip = PROJECTION_MATRIX * end_view;
	vec2 start_ndc = start_clip.xy / start_clip.w;
	vec2 end_ndc = end_clip.xy / end_clip.w;
	vec2 direction_pixels = (end_ndc - start_ndc) * VIEWPORT_SIZE;
	vec2 normal = length(direction_pixels) > 0.0001
		? normalize(vec2(-direction_pixels.y, direction_pixels.x))
		: vec2(1.0, 0.0);

	vec4 clip_position = mix(start_clip, end_clip, UV.x);
	vec2 offset_ndc = normal * UV.y * line_width_pixels / VIEWPORT_SIZE;
	POSITION = clip_position;
	POSITION.xy += offset_ndc * clip_position.w;
}

void fragment() {
	ALBEDO = edge_color.rgb;
}
