// Based on: https://bgolus.medium.com/the-best-darn-grid-shader-yet-727f9278b9d8
// Added Transparency
// Added Separate Axis Colors
// Added Major/Minor Lines
// Added Multiple Grid LOD levels

shader_type spatial;
// NOTE: intentionally NOT using `depth_prepass_alpha` here. If the grid
// participated in the depth prepass, the selected mesh's stencil-write material
// (matcap_stencil_write.gdshader, which depends on the depth test passing)
// would fail to write stencil wherever a grid line lies in front of the mesh.
// That would punch grid-shaped holes in the silhouette mask used by the
// outline compositor (shaders/outline/), producing an outline contour around
// every grid line crossing the mesh. Leaving depth_prepass_alpha off means the
// grid never writes depth, so the mesh's stencil mask stays intact. Opaque
// geometry in front of the grid still occludes it (its depth is written in the
// opaque pass), which is all the grid needs.
render_mode unshaded, cull_disabled, blend_mix, shadows_disabled;

// --- Line Widths (World Units) -------------------------------------------------------------------
uniform float minor_line_width : hint_range(0.0, 0.5) = 0.01;
uniform float major_line_width : hint_range(0.0, 0.5) = 0.03;

// --- Line Colors ---------------------------------------------------------------------------------
// Opacity of all grid lines
uniform float line_opacity : hint_range(0.0, 1.0) = 1.0;
// RGB Color values for the major and minor lines
uniform vec3 major_line_rgb : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 minor_line_rgb : source_color = vec3(0.5, 0.5, 0.5);
// Color that major lines fade out to towards the horizon. 
uniform vec3 major_line_fade_rgb : source_color = vec3(0.8, 0.8, 0.8);
// Color values for the X and Z origin axes
uniform vec3 axis_x_color : source_color = vec3(1.0, 0.25, 0.25);
uniform vec3 axis_z_color : source_color = vec3(0.25, 0.25, 1.0);

// --- LOD Configuration ---------------------------------------------------------------------------
// The size of a cell at LOD 0, in world units.
uniform float lod_base_cell_size : hint_range(0.001, 1000.0) = 1.0;
// LOD coarsening factor, as a multiplier of lod_base_cell_size. a value of 5 means 
// a single cell at LOD N is the size of 5x5 cells at LOD N-1.
uniform float lod_multiplier : hint_range(2.0, 100.0) = 5.0;
// The spacing between major lines starting from the origin axes
uniform int major_every_n : hint_range(1, 100) = 10;
// Strength of the LOD transition between LOD levels. Lower values = sharper transition.
uniform float lod_crossfade_width : hint_range(0.0, 1.0) = 0.35;
// The distance to the grid plane, in world units, at which LOD 0 is exactly the active level.
uniform float lod_reference_height : hint_range(0.001, 10000.0) = 8.0;
// Maximum number of LOD levels.
uniform float lod_level_max : hint_range(0.0, 32.0) = 6.0;

// Actual camera distance to the grid plane this frame, in world units.
uniform float lod_camera_distance = 0;
varying vec3 v_world_pos;

void vertex() {
	// Convert from local to world space so we can access worldspace in the fragment shader.
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

vec2 saturate2(vec2 x) {
	return clamp(x, 0.0, 1.0);
}

// Coverage / intensity for a pair of grid line directions, sampled at a given world-unit `spacing`
// between adjacent lines. `width_world` is the desired line width in world units.
// Returns vec4(coverage.xy, intensity.xy) where .x is the X-direction lines (varying world.x)
// and .y is the Z-direction lines (varying world.z).
// This remains fairly similar to the original source by bgolus
vec4 compute_line(vec2 uv, vec2 derivative, float width_world, float spacing) {
	float smoothstep_aa = 1.5;
	// Sample the grid in "cells" (one cell = `spacing` world units). All quantities below live in
	// the same scaled space so the existing fract()/derivative-clamp moire mitigation works correctly
	// per-grid, regardless of how coarse the spacing is.
	vec2 uv_s = uv / spacing;
	vec2 derivative_s = derivative / spacing;
	vec2 line_width = vec2(width_world / spacing);
	vec2 draw_width = clamp(line_width, derivative_s, vec2(0.5)); // Black magic
	vec2 line_aa = derivative_s * smoothstep_aa;
	vec2 grid_uv = 1.0 - abs(fract(uv_s) * 2.0 - 1.0);
	vec2 line_coverage = 1.0 - smoothstep(draw_width - line_aa, draw_width + line_aa, grid_uv);

	// Fade line brightness towards the horizon, and fade out as cells become < 1 pixel wide to
	// avoid moire patterns in the distance.
	vec2 line_intensity = line_coverage;
	line_intensity *= saturate2(line_width / draw_width);
	line_intensity = mix(line_intensity, line_width, saturate2(derivative_s * 2.0 - 1.0));

	return vec4(line_coverage, line_intensity);
}

// Minor grid at `cell_spacing` plus major grid at `cell_spacing * major_every_n`; returns coverage,
// intensity, and per-direction RGB tint (fade vs bright) from major-line share.
void combine_minor_major_at_scale(vec2 uv, vec2 derivative, float cell_spacing, out vec2 o_cov, out vec2 o_int, out vec3 o_rgb_x, out vec3 o_rgb_y) {
	vec4 mn = compute_line(uv, derivative, minor_line_width, cell_spacing);
	vec4 mj = compute_line(uv, derivative, major_line_width, cell_spacing * float(major_every_n));
	o_cov = max(mn.xy, mj.xy);
	o_int = max(mn.zw, mj.zw);
	vec2 maj_share = saturate2(mj.zw / max(o_int, vec2(1e-4)));
	o_rgb_x = mix(major_line_fade_rgb, major_line_rgb, maj_share.x);
	o_rgb_y = mix(major_line_fade_rgb, major_line_rgb, maj_share.y);
}

void fragment() {
	vec2 uv = v_world_pos.xz;
	vec2 derivative = fwidth(uv);

	// Compute the current LOD level based on the camera distance to the grid plane,
	// and clamp it to the maximum of LOD levels.
	float k = log(lod_camera_distance / lod_reference_height) / log(lod_multiplier);
	k = clamp(k, 0.0, lod_level_max);
	float k_int = floor(k);
	float frac_k = k - k_int;
	// Centered, configurable-width crossfade. Default width is "fairly short" so most pixels see a
	// single dominant LOD pair (current LOD's minor + major) with only a brief blend between them.
	float t = smoothstep(0.5 - lod_crossfade_width * 0.5, 0.5 + lod_crossfade_width * 0.5, frac_k);

	// Three consecutive LOD scales for blending
	float spacing_A = lod_base_cell_size * pow(lod_multiplier, k_int);
	float spacing_B = spacing_A * lod_multiplier;
	float spacing_C = spacing_B * lod_multiplier;

	// Compute the coverage, intensity, and per-direction RGB tint accounting for LOD scaling.
	vec2 covA, intA, covB, intB, covC, intC;
	vec3 tintAx, tintAy, tintBx, tintBy, tintCx, tintCy;
	combine_minor_major_at_scale(uv, derivative, spacing_A, covA, intA, tintAx, tintAy);
	combine_minor_major_at_scale(uv, derivative, spacing_B, covB, intB, tintBx, tintBy);
	combine_minor_major_at_scale(uv, derivative, spacing_C, covC, intC, tintCx, tintCy);

	// Compute crossfade and produce the final line intensity and coverage for non-axis lines.
	vec2 a_int = intA * (1.0 - t);
	vec2 b_int = intB;
	vec2 c_int = intC * t;

	vec2 a_cov = covA * (1.0 - t);
	vec2 b_cov = covB;
	vec2 c_cov = covC * t;

	vec2 line_intensity = max(max(a_int, b_int), c_int);
	vec2 line_coverage = max(max(a_cov, b_cov), c_cov);

	vec2 sum_int = a_int + b_int + c_int;
	vec3 line_rgb_x = (tintAx * a_int.x + tintBx * b_int.x + tintCx * c_int.x) / max(sum_int.x, 1e-4);
	vec3 line_rgb_y = (tintAy * a_int.y + tintBy * b_int.y + tintCy * c_int.y) / max(sum_int.y, 1e-4);

	float sum_w = line_intensity.x + line_intensity.y;
	vec3 line_tint = (line_rgb_x * line_intensity.x + line_rgb_y * line_intensity.y) / max(sum_w, 1e-4);

	float alpha_mask = max(line_coverage.x, line_coverage.y);
	float intensity_mask = max(line_intensity.x, line_intensity.y);

	// Compute axis lines with special constant width and colors.
	float smoothstep_aa = 1.5;
	vec2 axis_line_width = vec2(major_line_width);
	vec2 axis_draw_width = clamp(axis_line_width, derivative, vec2(0.5));
	vec2 axis_aa = derivative * smoothstep_aa;
	float z_axis_coverage = 1.0 - smoothstep(axis_draw_width.x - axis_aa.x, axis_draw_width.x + axis_aa.x, abs(uv.x));
	float x_axis_coverage = 1.0 - smoothstep(axis_draw_width.y - axis_aa.y, axis_draw_width.y + axis_aa.y, abs(uv.y));
	float axis_sum = x_axis_coverage + z_axis_coverage;
	vec3 axis_blend = (axis_x_color * x_axis_coverage + axis_z_color * z_axis_coverage) / max(axis_sum, 1e-4);
	float axis_w = max(x_axis_coverage, z_axis_coverage);

	line_tint = mix(line_tint, axis_blend, axis_w);

	ALBEDO = line_tint * intensity_mask;
	ALPHA = alpha_mask * line_opacity;
	if (ALPHA < 0.001) {
		discard;
	}
}
