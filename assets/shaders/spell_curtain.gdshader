shader_type canvas_item;
render_mode blend_add;

// A wall of light standing on a circle on the road (SpellVfx.Curtain): half of a cylinder seen from the battle's
// shallow camera. The quad spans the circle's width; its bottom strip holds the flattened circle and the wall
// rises from that edge. Two sprites make a whole ring: the far half behind the troops, the near half over them.
// Threads of light run up the wall and its top edge is ragged, so the circle reads as a volume, not a flat disc.
uniform float side = 1.0;      // 1 = the near half (bows down the screen), -1 = the far half
uniform float ellipse = 0.1;   // the circle's half depth, as a fraction of the quad's height
uniform float rise = 1.0;      // how far the wall has grown, 0..1
uniform float twist = 0.0;     // radians per second the wall turns (a whirlwind)
uniform float speed = 1.0;     // how fast the threads climb
uniform float seed = 0.0;

void fragment() {
	float u = UV.x * 2.0 - 1.0;
	float across = sqrt(max(0.0, 1.0 - u * u));
	float centre = 1.0 - ellipse;
	float base = centre + side * ellipse * across;
	float h = (base - UV.y) / max(0.001, 1.0 - 2.0 * ellipse);
	// The angle round the circle, continuous from the near half into the far half.
	float angle = side > 0.0 ? asin(clamp(u, -1.0, 1.0)) : PI - asin(clamp(u, -1.0, 1.0));
	angle += TIME * twist + seed;

	// A ragged top: each part of the wall reaches its own height, slowly changing.
	float top = 0.62 + 0.2 * sin(angle * 3.0 + TIME * 0.9 + seed) + 0.18 * sin(angle * 7.0 - TIME * 1.4 + seed * 2.1);
	top *= rise;
	float k = h / max(0.001, top);
	float body = smoothstep(0.0, 0.03, h) * pow(max(0.0, 1.0 - k), 1.6) * step(h, top);

	// Threads of brighter light climbing the wall.
	float threads = 0.0;
	for (int i = 0; i < 3; i++) {
		float n = float(i);
		float column = sin(angle * (9.0 + n * 8.0) + seed * (n + 1.7) + sin(h * 4.0 + TIME * (0.5 + n * 0.3)) * 0.8);
		float band = fract(h * (1.4 + n * 0.7) - TIME * speed * (0.5 + n * 0.3) + seed * 0.31 * (n + 1.0) + angle * 0.3);
		threads += smoothstep(0.55, 1.0, column) * smoothstep(0.0, 0.25, band) * smoothstep(1.0, 0.5, band);
	}
	// Seen edge-on at the sides, the wall is thicker there.
	float thickness = 0.55 + 0.75 * (1.0 - across);
	// The bright line where it meets the road.
	float foot = exp(-pow(h / 0.045, 2.0)) * step(-0.02, h) * rise;

	// Mostly threads: an even sheet would fill the circle in and flatten it again.
	float light = body * (0.13 + 0.95 * threads) * thickness + foot * 0.6 * thickness;
	light *= side > 0.0 ? 0.62 : 1.0;
	COLOR.rgb *= 0.8 + 0.5 * threads + 0.6 * foot;
	COLOR.a *= clamp(light, 0.0, 1.0) * (1.0 - smoothstep(0.985, 1.0, abs(u)));
}
