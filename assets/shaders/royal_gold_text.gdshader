shader_type canvas_item;

// Engraved gold lettering for concept titles. Glyph fills are drawn in pure white and receive the
// vertical gradient; outlines and shadows keep their own colours.
uniform vec4 top_color : source_color = vec4(1.0, 0.94, 0.74, 1.0);
uniform vec4 mid_color : source_color = vec4(0.91, 0.75, 0.45, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.64, 0.45, 0.22, 1.0);
uniform float top_y = 0.0;
uniform float bottom_y = 40.0;

varying vec2 local_position;

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	vec4 glyph = texture(TEXTURE, UV);
	vec4 ink = COLOR;
	float fill = step(0.98, min(min(ink.r, ink.g), ink.b));
	float t = clamp((local_position.y - top_y) / max(1.0, bottom_y - top_y), 0.0, 1.0);
	vec3 gradient = t < 0.55 ? mix(top_color.rgb, mid_color.rgb, t / 0.55) : mix(mid_color.rgb, bottom_color.rgb, (t - 0.55) / 0.45);
	COLOR = vec4(mix(ink.rgb * glyph.rgb, gradient, fill), ink.a * glyph.a);
}
