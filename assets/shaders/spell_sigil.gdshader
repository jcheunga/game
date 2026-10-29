shader_type canvas_item;
render_mode blend_add;

// A spell's rune circle being drawn on the road (SpellVfx.Sigil). The ink runs round the circle from the top, the
// inner rings a little ahead of the outer ones, and its leading edge flares as it goes; once drawn, waves of light
// travel round the runes. The circle is painted white (assets/fx/spells/sigil_*.png) and tinted by the sprite.
uniform float draw = 1.0;   // how much of the circle is drawn, 0..1
uniform float flare = 0.0;  // a flash through every line as the spell lands
uniform float seed = 0.0;

void fragment() {
	vec2 p = UV - 0.5;
	float r = length(p) * 2.0;
	float a = fract(atan(p.x, -p.y) / TAU + 1.0);
	float d = draw * 1.08 - (a * 0.86 + r * 0.14);
	float shown = smoothstep(0.0, 0.025, d);
	float edge = exp(-pow(d / 0.04, 2.0)) * (1.0 - smoothstep(0.92, 1.0, draw));
	float shimmer = 0.78 + 0.22 * sin(a * 44.0 - TIME * 4.0 + seed) * sin(r * 9.0 + TIME * 1.7);
	COLOR.rgb *= shimmer + flare + edge * 2.6;
	COLOR.a *= max(shown, edge);
}
