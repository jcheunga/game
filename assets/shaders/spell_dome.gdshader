shader_type canvas_item;
render_mode blend_add;

// A sphere of force (SpellVfx.Dome): a ward around a body, a rift, a held orb. The quad is shaded as a glass ball:
// almost clear in the middle, bright at the rim where the shell is seen edge-on, a turning lattice of light over
// the near and far surfaces, a highlight top-left, and a band of light that sweeps up it as it forms.
uniform float form = 1.0;      // how much has formed, 0..1 (it closes from the ground up)
uniform float spin = 0.6;      // radians per second the lattice turns
uniform float cells = 5.0;     // lattice cells round the half you can see
uniform float fill = 0.1;      // how much light the middle holds
uniform float seed = 0.0;

float lattice(vec2 p) {
	// Distance to the nearest edge of a hexagonal grid.
	vec2 r = vec2(1.0, 1.7320508);
	vec2 a = mod(p, r) - r * 0.5;
	vec2 b = mod(p - r * 0.5, r) - r * 0.5;
	vec2 g = dot(a, a) < dot(b, b) ? a : b;
	g = abs(g);
	float d = max(g.x, g.x * 0.5 + g.y * 0.8660254);
	return smoothstep(0.42, 0.5, d);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float z = sqrt(max(0.0, 1.0 - r * r));
	float inside = 1.0 - smoothstep(0.97, 1.0, r);

	float rim = pow(1.0 - z, 2.4);
	float lon = atan(p.x, z);
	float lat = asin(clamp(p.y, -1.0, 1.0));
	float turn = TIME * spin + seed;
	float near = lattice(vec2((lon + turn) * cells / PI * 2.0, lat * cells / PI * 2.0));
	float far = lattice(vec2((PI - lon + turn) * cells / PI * 2.0, lat * cells / PI * 2.0));
	// The lattice glints as waves of light pass over the shell.
	float wave = 0.55 + 0.45 * sin(lat * 5.0 - TIME * 2.6 + seed + lon * 1.5);
	float lines = near * (0.25 + 0.6 * wave) * (0.35 + 0.65 * z) + far * 0.14 * z;

	float highlight = exp(-dot(p - vec2(-0.36, -0.46), p - vec2(-0.36, -0.46)) / 0.035) * 0.8
		+ exp(-dot(p - vec2(0.42, 0.5), p - vec2(0.42, 0.5)) / 0.06) * 0.18;

	// It closes from the ground up: a bright band leads, the shell stands below it.
	float level = 1.0 - 2.0 * form * 1.15;
	float formed = smoothstep(level - 0.02, level + 0.08, p.y);
	float band = exp(-pow((p.y - level) / 0.09, 2.0)) * (1.0 - smoothstep(0.85, 1.0, form));

	float light = (fill * (0.5 + 0.5 * z) + rim * 0.9 + lines * 0.4 + highlight) * formed + band * (0.4 + rim);
	COLOR.rgb = mix(COLOR.rgb, vec3(1.0), clamp(rim * 0.35 + highlight * 0.7 + band * 0.4, 0.0, 0.85));
	COLOR.a *= clamp(light, 0.0, 1.0) * inside;
}
