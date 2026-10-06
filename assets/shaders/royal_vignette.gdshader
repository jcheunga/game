shader_type canvas_item;

// Fades a painted picture into the parchment around it with a soft, slightly ragged edge,
// like a vignette painted into a codex page.
uniform float feather = 0.16;
uniform float ragged = 0.035;
uniform vec2 size = vec2(256.0, 256.0);
varying vec2 local_uv;

void vertex() {
	local_uv = VERTEX / size;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(41.3, 289.1))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec4 colour = texture(TEXTURE, UV);
	vec2 d = min(local_uv, 1.0 - local_uv);
	float edge = min(d.x, d.y);
	float n = noise(local_uv * 18.0) * ragged + noise(local_uv * 47.0) * ragged * 0.5;
	float alpha = smoothstep(0.0, feather, edge - n);
	COLOR = vec4(colour.rgb, colour.a * alpha) * COLOR;
}
