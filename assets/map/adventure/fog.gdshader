shader_type canvas_item;
uniform vec2 canvas_size;
uniform vec2 map_offset;
uniform float map_zoom = 1.0;
uniform vec3 revealed[128];
uniform int reveal_count = 0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1,0)), f.x), mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
}
void fragment() {
    vec2 world = (UV * canvas_size - map_offset) / map_zoom;
    float clouds = noise(world * 0.011 + vec2(TIME * 0.012, 0.0)) * 0.65 + noise(world * 0.028) * 0.35;
    float fog = 1.0;
    for (int i = 0; i < reveal_count; i++) {
        float distance_to_edge = distance(world, revealed[i].xy) + (clouds - 0.5) * 42.0;
        fog = min(fog, smoothstep(revealed[i].z - 48.0, revealed[i].z + 38.0, distance_to_edge));
    }
    COLOR = vec4(mix(vec3(0.025,0.045,0.060), vec3(0.12,0.16,0.17), clouds), fog);
}
