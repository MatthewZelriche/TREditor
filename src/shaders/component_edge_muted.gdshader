shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled, depth_draw_never;

uniform vec4 edge_color : source_color;

void fragment() {
	// Keep muted edges in the transparent queue so render priority orders all component tiers.
	ALPHA = 1.0;
	ALBEDO = edge_color.rgb;
}
