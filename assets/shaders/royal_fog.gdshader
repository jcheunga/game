shader_type canvas_item;

// The campaign map's fog of war, seen from above: a thin, broken layer of pale cloud over every hidden tile,
// and a low ground mist over the frontier tiles beside explored land.
// TEXTURE holds blurred world-space masks of the hidden tiles (red) and the frontier tiles (green), and a
// much wider blur of the hidden tiles (blue) that lights a whole bank as one gently rounded mass.
// The layer's outline is the hidden mask pushed in and out by smooth billows (fog-noise.png blue,
// art/royal/fognoise.py). Inside it the same billows gather into soft puffs: each is nearly opaque and lit
// from the upper left, and between them the layer thins to a veil. Through the veil the land shows only as
// a heavy blur of its colours, so nothing on hidden ground can be made out, and each puff casts a faint
// shadow on it. The frontier's colours are muted under drifting mist, so it reads as seen but not yet charted.
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
// The same screen through its blurred mipmaps, for the land seen through the thin cloud.
uniform sampler2D screen_blur : hint_screen_texture, filter_linear_mipmap;
uniform vec2 world_origin = vec2(0.0);
uniform vec2 world_size = vec2(1024.0);
// Moves the cloud pattern for each zone, so neighbouring zones never show the same layer.
uniform vec2 noise_offset = vec2(0.0);
uniform float clock = 0.0;
uniform vec3 shade : source_color = vec3(0.55, 0.59, 0.67);
uniform vec3 body : source_color = vec3(0.75, 0.78, 0.83);
uniform vec3 sunlit : source_color = vec3(0.93, 0.925, 0.9);
uniform vec3 ground_shadow : source_color = vec3(0.1, 0.13, 0.2);
uniform vec3 veil : source_color = vec3(0.82, 0.86, 0.91);

// Toward the sun in world space (y points down), and the world size of one tile of the billows.
const vec2 SUN = vec2(-0.6, -0.8);
const float BILLOWS = 3600.0;
const mat2 TURN_A = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
const mat2 TURN_B = mat2(vec2(0.28, -0.96), vec2(0.96, 0.28));
// How much of the land the cloud hides between the puffs and inside them.
const float THIN = 0.58, THICK = 0.95;

// Two sizes of billows at unrelated scales and angles, so the tile's repeat never lines up into a pattern.
float billows_at(vec2 p) {
	return texture(noise_tex, p).b * 0.62 + texture(noise_tex, TURN_A * p * 2.3 + vec2(0.31, 0.17)).b * 0.38 - 0.55;
}
float wisps_at(vec2 p, vec2 wind) { return texture(noise_tex, TURN_B * p * 5.3 + vec2(0.73, 0.41) - wind).a - 0.52; }

// The layer's density: the hidden mask pushed in and out by the billows. The deep interior stays well
// above the outline, so the layer never opens a hole over hidden land; toward the edge the billows count
// for more, so the outline breaks up along the puffs.
float density(float mask, float billows) {
	return mask * 1.25 - 0.1 + billows * (0.35 + (1.0 - mask) * 0.65);
}

// How much of a puff stands at a point: the higher billows, their edges frayed by the wisps. It rises
// steeply at the rim and levels off inside, so each puff is lit as a rounded mound.
float puff_at(float billows, float wisps) { return sqrt(smoothstep(-0.18, 0.14, billows + wisps * 0.35)); }

void fragment() {
	vec2 texel = 1.0 / world_size;
	vec3 masks = texture(TEXTURE, UV).rgb;
	// The point whose cloud would shade this one lies toward the sun.
	float shade_mask = texture(TEXTURE, UV + SUN * 130.0 * texel).r;
	if (max(max(masks.r, masks.g), shade_mask) < 0.004) discard;

	vec2 world = world_origin + UV * world_size + noise_offset;
	vec2 wind = vec2(clock, clock * 0.35) * 0.0005;
	vec2 p = world / BILLOWS + wind;
	p += (texture(noise_tex, world / 6400.0 + wind * 0.3).rg - 0.5) * 0.16;
	// Wisps smaller than a few pixels fade out as the view zooms out, so the layer never turns to grain.
	float fine = 1.0 - smoothstep(1.5, 4.5, length(fwidth(world)));
	float billows = billows_at(p), wisps = wisps_at(p, wind) * (0.4 + 0.6 * fine);
	float d = density(masks.r, billows);
	// The outline breaks into tufts: a mid-sized fray that still shows zoomed out, and the fine wisps close up.
	float tufts = texture(noise_tex, TURN_B * p * 2.1 + vec2(0.13, 0.67)).a - 0.52;
	float layer = smoothstep(0.3, 0.85, d + (wisps * 0.45 + tufts * 0.5) * (1.0 - masks.r) + wisps * 0.08);
	float puff = puff_at(billows, wisps);
	// Toward the sun: the cloud that lights this point and the puff whose shadow falls on it.
	vec2 sun_p = p + SUN * (70.0 / BILLOWS);
	float sun_billows = billows_at(sun_p);
	float sun_puff = puff_at(sun_billows, wisps_at(sun_p, wind) * (0.4 + 0.6 * fine));
	vec3 toward = texture(TEXTURE, UV + SUN * 70.0 * texel).rgb;

	// Light: each puff brightens where it rises toward the sun and shades where it falls away, and the
	// whole bank rounds off gently toward its lower-right edge.
	float slope = (puff - sun_puff) * 0.9 + (masks.b - toward.b) * 1.4 + (billows - sun_billows) * 2.0
		+ (smoothstep(0.36, 0.9, d) - smoothstep(0.36, 0.9, density(toward.r, sun_billows))) * 0.25;
	float lit = clamp(0.5 + slope, 0.0, 1.0);
	vec3 colour = mix(shade, body, smoothstep(0.0, 0.6, lit));
	colour = mix(colour, sunlit, smoothstep(0.55, 1.0, lit) * 0.8 * puff);
	colour *= 1.0 + wisps * 0.06 * fine;

	vec3 land = texture(screen_tex, SCREEN_UV).rgb;
	// The frontier: muted colours under a low mist that drifts in long strands.
	float strands = texture(noise_tex, world * vec2(1.0, 2.4) / 900.0 + wind * 2.0).a * 0.6
		+ texture(noise_tex, world / 2300.0 - wind * 0.9).b * 0.4;
	float frontier = smoothstep(0.18, 0.72, masks.g + (strands - 0.5) * 0.3);
	vec3 muted = mix(land, vec3(dot(land, vec3(0.3, 0.59, 0.11))), 0.5) * vec3(0.9, 0.92, 0.97);
	muted = mix(muted, veil, smoothstep(0.3, 0.85, strands) * 0.36 + 0.06);
	vec3 ground = mix(land, muted, frontier);
	// The layer's shadow falls faintly on the land beyond its lower-right edge.
	float caster = density(shade_mask, billows_at(p + SUN * (130.0 / BILLOWS)));
	ground = mix(ground, ground_shadow, smoothstep(0.3, 0.8, caster) * 0.3 * (1.0 - layer));

	// Under the cloud the land is only a blur of its colours, about a fortieth of the screen wide, dimmed
	// where a puff shades it.
	float lod = log2(max(1.0 / SCREEN_PIXEL_SIZE.x, 1.0 / SCREEN_PIXEL_SIZE.y) / 40.0);
	vec3 below = textureLod(screen_blur, SCREEN_UV, lod).rgb;
	below = mix(mix(below, vec3(dot(below, vec3(0.3, 0.59, 0.11))), 0.45), shade, 0.2) * (1.0 - sun_puff * 0.15);
	vec3 seen = mix(ground, below, smoothstep(0.5, 0.85, d));
	// Close up, the wisps comb the layer into thicker and thinner strands.
	float cover = clamp(mix(THIN, THICK, puff) * (1.0 + wisps * 0.45 * fine), 0.0, 1.0);
	COLOR = vec4(mix(seen, colour, layer * cover), 1.0);
}
