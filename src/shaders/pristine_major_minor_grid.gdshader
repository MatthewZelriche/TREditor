// Godot port of "Pristine Major Minor Grid" by Ben Golus.
// Original: https://gist.githubusercontent.com/bgolus/3a561077c86b5bfead0d6cc521097bae/raw/2f1dcffc84bd0b9a0293f685c5136e55257f54d2/PristineMajorMinorGrid.shader
// Original License: Unlicense.

shader_type spatial;
render_mode unshaded, cull_disabled, blend_premul_alpha, depth_draw_never, fog_disabled;

uniform float plane_height = 0.0;
uniform float grid_scale : hint_range(0.001, 1024.0, 0.001) = 1.0;
uniform float major_grid_divisions : hint_range(2.0, 25.0, 1.0) = 10.0;

uniform float axis_line_width : hint_range(0.0, 1.0) = 0.04;
uniform float major_line_width : hint_range(0.0, 1.0) = 0.02;
uniform float minor_line_width : hint_range(0.0, 1.0) = 0.01;

uniform vec4 major_line_color : source_color = vec4(1.0, 1.0, 1.0, 0.5);
uniform vec4 minor_line_color : source_color = vec4(1.0, 1.0, 1.0, 0.2);
uniform vec4 base_color : source_color = vec4(0.0);

uniform vec4 x_axis_color : source_color = vec4(1.0, 0.15, 0.15, 0.9);
uniform vec4 x_axis_dash_color : source_color = vec4(0.5, 0.05, 0.05, 0.9);
uniform vec4 z_axis_color : source_color = vec4(0.2, 0.45, 1.0, 0.9);
uniform vec4 z_axis_dash_color : source_color = vec4(0.05, 0.15, 0.5, 0.9);
uniform float axis_dash_scale = 1.33;
uniform vec4 center_color : source_color = vec4(1.0);

vec2 antialiased_line(vec2 distance_to_line, vec2 target_width, vec2 uv_derivative) {
	vec2 draw_width = clamp(target_width, uv_derivative, vec2(0.5));
	vec2 line_aa = uv_derivative * 1.5;
	vec2 coverage = 1.0 - smoothstep(
		draw_width - line_aa,
		draw_width + line_aa,
		distance_to_line
	);
	return coverage * clamp(target_width / draw_width, vec2(0.0), vec2(1.0));
}

vec4 composite_line(vec4 below, vec4 line_color, float coverage) {
	float line_alpha = line_color.a * clamp(coverage, 0.0, 1.0);
	vec4 line = vec4(line_color.rgb * line_alpha, line_alpha);
	return line + below * (1.0 - line_alpha);
}

void vertex() {
	// The grid is an analytical world-space plane, so the carrier quad always covers the viewport.
	// Reversed-Z uses 1.0 for the near plane in Godot 4.3 and newer.
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 ndc_xy = SCREEN_UV * 2.0 - 1.0;
	vec4 near_view_h = INV_PROJECTION_MATRIX * vec4(ndc_xy, 1.0, 1.0);
	vec4 far_view_h = INV_PROJECTION_MATRIX * vec4(ndc_xy, 0.0, 1.0);
	vec3 near_view = near_view_h.xyz / near_view_h.w;
	vec3 far_view = far_view_h.xyz / far_view_h.w;

	// Transform view-relative positions as directions. This omits the camera translation and avoids
	// losing precision by constructing a large absolute world position only to subtract it again.
	mat3 camera_basis = mat3(INV_VIEW_MATRIX);
	vec3 near_relative = camera_basis * near_view;
	vec3 far_relative = camera_basis * far_view;
	vec3 relative_ray = far_relative - near_relative;
	if (abs(relative_ray.y) < 0.000001) {
		discard;
	}

	float intersection = (
		plane_height - CAMERA_POSITION_WORLD.y - near_relative.y
	) / relative_ray.y;
	if (intersection < 0.0 || intersection > 1.0) {
		discard;
	}

	vec3 relative_position = mix(near_relative, far_relative, intersection);
	vec3 view_position = mix(near_view, far_view, intersection);
	vec4 clip_position = PROJECTION_MATRIX * vec4(view_position, 1.0);
	DEPTH = clip_position.z / clip_position.w;

	float divisions = max(2.0, floor(major_grid_divisions + 0.5));
	float safe_grid_scale = max(grid_scale, 0.000001);
	float major_world_size = safe_grid_scale * divisions;
	vec2 camera_cell = CAMERA_POSITION_WORLD.xz
		- floor(CAMERA_POSITION_WORLD.xz / major_world_size) * major_world_size;

	// grid_uv remains close to the camera. axis_uv is absolute because only the two origin axes need
	// it, matching the camera-centering strategy in the source shader.
	vec2 grid_uv = (camera_cell + relative_position.xz) / safe_grid_scale;
	vec2 axis_uv = (CAMERA_POSITION_WORLD.xz + relative_position.xz) / safe_grid_scale;
	vec2 uv_derivative = max(
		vec2(
			length(vec2(dFdx(grid_uv.x), dFdy(grid_uv.x))),
			length(vec2(dFdx(grid_uv.y), dFdy(grid_uv.y)))
		),
		vec2(0.000001)
	);

	float effective_axis_width = max(major_line_width, axis_line_width);
	vec2 axis_coverage = antialiased_line(
		abs(axis_uv * 2.0),
		vec2(effective_axis_width),
		uv_derivative
	);

	vec2 major_derivative = uv_derivative / divisions;
	float scaled_major_width = major_line_width / divisions;
	vec2 major_distance = 1.0 - abs(fract(grid_uv / divisions) * 2.0 - 1.0);
	vec2 major_axis_offset = (
		1.0 - clamp(abs(axis_uv / divisions * 2.0), vec2(0.0), vec2(1.0))
	) * 2.0;
	major_distance += major_axis_offset;
	vec2 major_grid = antialiased_line(
		major_distance,
		vec2(scaled_major_width),
		major_derivative
	);
	major_grid = clamp(major_grid - axis_coverage, vec2(0.0), vec2(1.0));
	major_grid = mix(
		major_grid,
		vec2(scaled_major_width),
		clamp(major_derivative * 2.0 - 1.0, vec2(0.0), vec2(1.0))
	);

	float effective_minor_width = min(minor_line_width, major_line_width);
	bool invert_minor_line = effective_minor_width > 0.5;
	float minor_target_width = invert_minor_line
		? 1.0 - effective_minor_width
		: effective_minor_width;
	vec2 minor_distance = abs(fract(grid_uv) * 2.0 - 1.0);
	if (!invert_minor_line) {
		minor_distance = 1.0 - minor_distance;
	}
	vec2 minor_major_offset = (
		1.0 - clamp(
			(1.0 - abs(fract(axis_uv / divisions) * 2.0 - 1.0)) * divisions,
			vec2(0.0),
			vec2(1.0)
		)
	) * 2.0;
	minor_distance += minor_major_offset;
	vec2 minor_grid = antialiased_line(
		minor_distance,
		vec2(minor_target_width),
		uv_derivative
	);
	minor_grid = clamp(minor_grid - axis_coverage, vec2(0.0), vec2(1.0));
	minor_grid = mix(
		minor_grid,
		vec2(minor_target_width),
		clamp(uv_derivative * 2.0 - 1.0, vec2(0.0), vec2(1.0))
	);
	if (invert_minor_line) {
		minor_grid = 1.0 - minor_grid;
	}
	minor_grid *= step(vec2(0.5), abs(axis_uv));

	float minor_coverage = mix(minor_grid.x, 1.0, minor_grid.y);
	float major_coverage = mix(major_grid.x, 1.0, major_grid.y);

	vec2 axis_dash_uv = abs(
		fract((axis_uv + effective_axis_width * 0.5) * axis_dash_scale) * 2.0 - 1.0
	) - 0.5;
	vec2 axis_dash_derivative = uv_derivative * axis_dash_scale * 1.5;
	vec2 axis_dash = smoothstep(
		-axis_dash_derivative,
		axis_dash_derivative,
		axis_dash_uv
	);
	axis_dash = mix(axis_dash, vec2(1.0), step(vec2(0.0), axis_uv));

	// axis_coverage.x is the Z axis (x == 0); axis_coverage.y is the X axis (z == 0).
	vec4 x_color = mix(x_axis_dash_color, x_axis_color, axis_dash.x);
	vec4 z_color = mix(z_axis_dash_color, z_axis_color, axis_dash.y);
	float center_coverage = axis_coverage.x * axis_coverage.y;

	vec4 color = vec4(base_color.rgb * base_color.a, base_color.a);
	color = composite_line(color, minor_line_color, minor_coverage);
	color = composite_line(color, major_line_color, major_coverage);
	color = composite_line(color, z_color, axis_coverage.x);
	color = composite_line(color, x_color, axis_coverage.y);
	color = composite_line(color, center_color, center_coverage);

	ALBEDO = color.rgb;
	ALPHA = color.a;
}
