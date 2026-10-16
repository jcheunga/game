shader_type canvas_item;

// A held button over painted art (ButtonFeel / PressedArt): redraws what lies under the button pressed in, shrunk a
// few pixels toward its middle, dropped and darker. A button's rect is often larger than the art painted in it (a
// hotspot over a medallion and its label), so the press is a smooth dent rather than a cut-out: the art is pulled in
// fully a little way inside the rect and not at all at its edge, and the darkening fades the same way, so neither a
// seam, a doubled edge nor the rect itself ever shows.

uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform vec2 size = vec2(64.0);
uniform float amount = 0.0;    // 0 at rest, 1 fully held
uniform float shrink = 0.06;   // fraction the face shrinks when fully held
uniform float depth = 2.0;     // pixels the face drops when fully held
uniform float corner = 5.0;
uniform float shade = 0.2;     // how much darker the held face is at its middle

varying vec2 local;

void vertex() {
	local = VERTEX;
}

float rounded_box(vec2 point, vec2 half_size, float radius) {
	radius = min(radius, min(half_size.x, half_size.y));
	vec2 q = abs(point) - half_size + radius;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

void fragment() {
	float a = clamp(amount, 0.0, 1.0);
	float s = 1.0 - shrink * a;
	vec2 middle = size * 0.5;
	vec2 drop = vec2(0.0, depth * a);
	float outer = rounded_box(local - middle, middle, corner);
	// How much of the press reaches this pixel: all of it a band's width inside the rect, none at its edge.
	float band = clamp(min(size.x, size.y) * 0.25, 4.0, 14.0);
	float reach = smoothstep(0.0, band, -outer);
	// Where this pixel's art sits when the button is at rest.
	vec2 source = mix(local, middle + (local - middle - drop) / s, reach);
	// Local units to screen pixels from how they change across the screen, whatever the stretch, zoom or flip.
	mat2 local_per_pixel = mat2(dFdx(local), dFdy(local));
	vec2 pixels = inverse(local_per_pixel) * (source - local);
	vec3 color = texture(screen, SCREEN_UV + pixels * SCREEN_PIXEL_SIZE).rgb * (1.0 - shade * a * reach);
	COLOR = vec4(color, step(outer, 0.0) * step(0.0001, a));
}
