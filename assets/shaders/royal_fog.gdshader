shader_type canvas_item;

// The campaign map's fog of war, seen from above: a thin layer of painted cloud over every hidden tile, and a
// low ground mist over the frontier tiles beside explored land.
// TEXTURE holds blurred world-space masks of the hidden tiles (red) and the frontier tiles (green), and a
// much wider blur of the hidden tiles (blue), which tells a small pocket of hidden land from a wide bank.
// The cloud is two hand-painted layers (fog-clouds.png, art/royal/clouds.py), each white cloud on black and
// tiling seamlessly: fluffy cloudlets above, and below them a thin veil that drifts more slowly on its own
// heading, so the two never line up. Toward the edge of the hidden land the thinnest cloud goes first, so
// the layer frays into separate cloudlets, and a small pocket gets one dense little cloud. Between the clouds
// a pale haze of the same white hides a heavy blur of the land, so the whole layer is one even tone and the
// cloud shows only as soft fluffy relief, with faint shadows under each cloudlet.
// The frontier's colours are muted under drifting mist, so it reads as seen but not yet charted.
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D cloud_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
// The same screen through its blurred mipmaps, for the land seen through the haze.
uniform sampler2D screen_blur : hint_screen_texture, filter_linear_mipmap;
uniform vec2 world_origin = vec2(0.0);
uniform vec2 world_size = vec2(1024.0);
// Moves the cloud pattern for each zone, so neighbouring zones never show the same field.
uniform vec2 noise_offset = vec2(0.0);
uniform float clock = 0.0;
uniform vec3 shade : source_color = vec3(0.55, 0.59, 0.67);
uniform vec3 body : source_color = vec3(0.75, 0.78, 0.83);
uniform vec3 sunlit : source_color = vec3(0.93, 0.925, 0.9);
uniform vec3 veil : source_color = vec3(0.82, 0.86, 0.91);

// Toward the sun in world space (y points down), the world size of one tile of each painted layer, and the
// drift in world units a second.
const vec2 SUN = vec2(-0.6, -0.8);
const float PUFFS = 1650.0;
const float VEILS = 2300.0;
const vec2 WIND = vec2(1.8, 0.63);
const mat2 TURN_A = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
// How opaque the thickest cloudlet and the veil are, and how much of the land the haze hides.
const float PUFF_OPACITY = 0.82;
const float VEIL_OPACITY = 0.42;
const float HAZE = 0.9;

// A painted layer's cloud, cut back as the hidden land thins out toward its edge: the faintest cloud goes
// first, so the edge breaks up into cloudlets rather than fading as a sheet.
float thinned(float painted, float reach, float pocket) {
	float cut = 1.0 - reach * (0.88 + pocket * 0.5);
	return clamp((painted - cut) / (1.0 - cut), 0.0, 1.0);
}

void fragment() {
	vec2 texel = 1.0 / world_size;
	vec3 masks = texture(TEXTURE, UV).rgb;
	// Toward the sun: the cloud that shades this point.
	vec3 sun_masks = texture(TEXTURE, UV + SUN * 40.0 * texel).rgb;
	if (max(max(masks.r, masks.g), sun_masks.r) < 0.004) discard;

	vec2 world = world_origin + UV * world_size + noise_offset;
	vec2 bend = texture(noise_tex, world / 3200.0).rg - 0.5;
	// The cloud is a little thicker in some places than others, so the field never reads as a pattern.
	float thickness = 0.9 + (texture(noise_tex, world / 5600.0 + 0.5).g - 0.5) * 0.35;
	float reach = smoothstep(0.08, 0.75, masks.r) * thickness;
	// A small pocket of hidden land is one dense little cloud, so it never shows as a patch of bare haze.
	float pocket = 1.0 - smoothstep(0.25, 0.65, masks.b);

	vec2 p = (world + WIND * clock) / PUFFS + bend * 0.16;
	float puff = thinned(texture(cloud_tex, p).r, reach, pocket);
	float sun_reach = smoothstep(0.08, 0.75, sun_masks.r) * thickness;
	float caster = thinned(texture(cloud_tex, p + SUN * (40.0 / PUFFS)).r, sun_reach, pocket);
	vec2 q = TURN_A * (world + WIND * clock * 0.55) / VEILS + vec2(0.37, 0.61) - bend * 0.04;
	float low = thinned(texture(cloud_tex, q).g, reach, pocket);

	vec3 land = texture(screen_tex, SCREEN_UV).rgb;
	// The frontier: muted colours under a low mist that drifts in long strands.
	vec2 wind = WIND * clock / 3600.0;
	float strands = texture(noise_tex, world * vec2(1.0, 2.4) / 900.0 + wind * 2.0).a * 0.6
		+ texture(noise_tex, world / 2300.0 - wind * 0.9).g * 0.4;
	float frontier = smoothstep(0.18, 0.72, masks.g + (strands - 0.5) * 0.3);
	vec3 muted = mix(land, vec3(dot(land, vec3(0.3, 0.59, 0.11))), 0.5) * vec3(0.9, 0.92, 0.97);
	muted = mix(muted, veil, smoothstep(0.3, 0.85, strands) * 0.36 + 0.06);
	vec3 ground = mix(land, muted, frontier);
	// Under hidden land the ground is only a soft blur of its colours, about a thirtieth of the screen wide:
	// four taps around the point smooth out the blocks of the blurred mipmap.
	float lod = log2(max(1.0 / SCREEN_PIXEL_SIZE.x, 1.0 / SCREEN_PIXEL_SIZE.y) / 30.0);
	vec2 spread = SCREEN_PIXEL_SIZE * exp2(lod) * 0.6;
	vec3 below = (textureLod(screen_blur, SCREEN_UV + vec2(spread.x, spread.y * 0.3), lod).rgb
		+ textureLod(screen_blur, SCREEN_UV - vec2(spread.x, spread.y * 0.3), lod).rgb
		+ textureLod(screen_blur, SCREEN_UV + vec2(-spread.x * 0.3, spread.y), lod).rgb
		+ textureLod(screen_blur, SCREEN_UV - vec2(-spread.x * 0.3, spread.y), lod).rgb) * 0.25;
	below = mix(below, vec3(dot(below, vec3(0.3, 0.59, 0.11))), 0.55);
	float hidden = smoothstep(0.5, 0.9, masks.r);
	ground = mix(ground, below, hidden);
	ground = mix(ground, mix(veil, sunlit, 0.3), hidden * HAZE);
	// The veil: thin, faint wisps in the same white.
	vec3 veils = mix(veil, sunlit, smoothstep(0.3, 1.0, low) * 0.7);
	ground = mix(ground, veils, smoothstep(0.02, 0.85, low) * VEIL_OPACITY);
	// The cloudlets' shadows fall on the veil and the land below.
	ground *= 1.0 - smoothstep(0.15, 0.8, caster) * 0.07 * reach;
	// The cloudlets: the painting's brightness is both how thick and how sunlit each part is.
	vec3 clouds = mix(mix(body, veil, 0.5), veil, smoothstep(0.05, 0.45, puff));
	clouds = mix(clouds, sunlit, smoothstep(0.4, 0.95, puff));
	COLOR = vec4(mix(ground, clouds, smoothstep(0.02, 0.55, puff) * PUFF_OPACITY), 1.0);
}
