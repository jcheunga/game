shader_type canvas_item;

// The campaign map's fog of war: a bank of dark storm cloud over every hidden tile, as in the
// cartographer's atlas concept, and a thin pale mist over the frontier tiles beside explored land.
// TEXTURE holds blurred masks of the hidden tiles (red) and the frontier tiles (green); tileable noise rounds
// the bank's outline into billows, shades the cloud tops from the upper left and drifts slowly with the wind.
uniform sampler2D noise_tex : repeat_enable, filter_linear;
uniform vec2 world_origin = vec2(0.0);
uniform vec2 world_size = vec2(1024.0);
uniform float clock = 0.0;
uniform vec3 deep : source_color = vec3(0.07, 0.074, 0.085);
uniform vec3 body : source_color = vec3(0.215, 0.222, 0.24);
uniform vec3 lit : source_color = vec3(0.47, 0.475, 0.49);
uniform vec3 haze : source_color = vec3(0.83, 0.85, 0.87);

void fragment() {
	vec2 masks = texture(TEXTURE, UV).rg;
	float mask = masks.r;
	vec2 world = world_origin + UV * world_size;
	vec2 wind = vec2(clock, clock * 0.4) * 0.0025;
	vec2 p = world / 1900.0 + wind;
	p += (texture(noise_tex, world / 3400.0 - wind * 0.5).rg - 0.5) * 0.1;
	// Big rounded cloud masses with smaller lumps riding on them.
	float masses = texture(noise_tex, p).b;
	float lumps = texture(noise_tex, p * 1.8 + vec2(0.17, 0.41) - wind * 0.6).a;
	float n = masses * 0.76 + lumps * 0.24;
	// The bank's outline: the cloud masses push the mask's edge in and out, small puffs make it lumpy,
	// and the edge stays inside the hidden tiles so explored ground is left clear.
	float puffs = texture(noise_tex, p * 4.6 + vec2(0.53, 0.29) + wind).a;
	float density = smoothstep(0.46, 0.8, mask + (n - 0.5) * 0.62 + (puffs - 0.5) * 0.32);
	// The bank as a whole is lit from the upper left too, so even a small patch reads as a rounded
	// puff rather than a flat smudge.
	vec2 texel = 1.0 / vec2(textureSize(TEXTURE, 0));
	float toward = texture(TEXTURE, UV - vec2(4.0, 5.0) * texel).r;
	float bank = clamp((mask - toward) * 2.2, -1.0, 1.0);
	// Light from the upper left: a mass brightens where it rises toward the light. Only the large
	// shapes are lit, so the tops read as soft billows rather than grain.
	float behind = texture(noise_tex, p + vec2(-0.014, -0.019)).b;
	float relief = clamp((masses - behind) * 7.0 + 0.42, 0.0, 1.0);
	float height = smoothstep(0.2, 0.85, n);
	vec3 colour = mix(deep, body, height);
	colour = mix(colour, lit, relief * relief * height * 0.95);
	colour = bank > 0.0 ? mix(colour, lit, bank * 0.55 * (0.4 + puffs * 0.6)) : mix(colour, deep * 0.6, -bank * 0.5);
	// The thin cloud at the bank's edge catches the most light.
	colour = mix(colour, lit * 0.85, (1.0 - density) * 0.45);
	// The frontier: a thin, drifting mist that pales the land without hiding it.
	float mist = smoothstep(0.2, 0.85, masks.g + (n - 0.5) * 0.4 + (puffs - 0.5) * 0.2) * 0.58;
	vec3 veil = haze * (0.92 + n * 0.12);
	float storm = density * 0.97;
	COLOR = vec4(mix(veil, colour, storm), storm + mist * (1.0 - storm));
}
