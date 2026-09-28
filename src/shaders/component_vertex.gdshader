shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;

uniform vec4 point_color : source_color;
uniform float point_size_pixels = 8.0;
uniform float depth_bias = 0.001;

void vertex() {
	// Build the quad directly in clip space. Its size is expressed in pixels, so it remains a
	// camera-facing disc of constant apparent size at every perspective and orthographic distance.
	// Pull it slightly in front of coplanar edge geometry so its later transparent pass survives
	// the depth test and can consistently cover every edge tier.
	vec4 center_view = MODELVIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0);
	center_view.z += depth_bias;
	vec4 center_clip = PROJECTION_MATRIX * center_view;
	vec2 offset_ndc = VERTEX.xy * point_size_pixels * 2.0 / VIEWPORT_SIZE;
	POSITION = center_clip;
	POSITION.xy += offset_ndc * center_clip.w;
}

void fragment() {
	// Vertices use the transparent queue so render priority can place every vertex above edges.
	ALPHA = 1.0;
	vec2 point = UV * 2.0 - 1.0;
	float radius_squared = dot(point, point);
	if (radius_squared > 1.0) {
		discard;
	}

	// A small procedural sphere highlight makes the billboard read as a point rather than a flat quad.
	float sphere_z = sqrt(max(0.0, 1.0 - radius_squared));
	vec3 sphere_normal = normalize(vec3(point.x, -point.y, sphere_z));
	float light = 0.62 + 0.38 * max(0.0, dot(sphere_normal, normalize(vec3(-0.4, -0.5, 1.0))));
	ALBEDO = point_color.rgb * light;
}
