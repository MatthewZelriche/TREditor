shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled, depth_draw_never;

uniform vec4 overlay_color : source_color;

void vertex() {
	// Lift the overlay just above the authored surface to prevent coplanar flicker.
	VERTEX += NORMAL * 0.002;
}

void fragment() {
	ALBEDO = overlay_color.rgb;
	ALPHA = overlay_color.a;
}
