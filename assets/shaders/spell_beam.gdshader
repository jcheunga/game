shader_type canvas_item;
render_mode blend_add;

// A column of light standing on the road (SpellVfx.Pillar): the soft beam (TEXTURE, brightest low down) carried
// by threads of brighter light that wander as they run up it, so the light visibly rises.
uniform float speed = 1.3;
uniform float seed = 0.0;

void fragment() {
	float up = 1.0 - UV.y;
	float x = UV.x - 0.5;
	float threads = 0.0;
	for (int i = 0; i < 3; i++) {
		float k = float(i);
		float column = sin(x * (20.0 + k * 11.0) + seed * (k + 1.3) + sin(up * 3.0 + TIME * (0.6 + k * 0.3) + k) * 0.9);
		float band = fract(up * (1.1 + k * 0.6) - TIME * speed * (0.7 + k * 0.4) + seed * 0.37 * (k + 1.0));
		threads += smoothstep(0.7, 1.0, column) * smoothstep(0.0, 0.2, band) * smoothstep(1.0, 0.4, band);
	}
	float core = exp(-pow(x / 0.1, 2.0));
	COLOR.rgb *= 0.65 + 1.1 * threads + 0.5 * core;
	COLOR.a *= 0.7 + 0.5 * threads;
}
