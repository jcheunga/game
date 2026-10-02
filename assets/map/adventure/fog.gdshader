shader_type canvas_item;
render_mode unshaded;
uniform sampler2D charted_mask : filter_linear, repeat_disable;
uniform vec2 canvas_size;
uniform vec2 map_offset;
uniform float map_zoom = 1.0;
uniform vec2 world_size;
uniform float ground_perspective = 0.56;
uniform vec2 hero_point;
uniform float fog_time = 0.0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float clouds(vec2 p) {
    return noise(p)*0.52 + noise(p*2.07+13.7)*0.28 + noise(p*4.13-7.1)*0.14 + noise(p*8.3+29.0)*0.06;
}
void fragment() {
    vec2 world = (UV*canvas_size-map_offset)/map_zoom;
    // Noise, silhouettes and drift occupy the same flattened ground plane.
    vec2 ground = world/vec2(1.0,ground_perspective);
    vec2 drift = vec2(fog_time*0.045,-fog_time*0.027);
    float billow = clouds(ground*0.005+drift);
    float wisps = clouds(ground*0.015-drift*0.7+vec2(billow*1.2));
    vec2 warp = (vec2(noise(ground*0.010+drift*0.3),noise(ground*0.010-drift*0.3+17.8))-0.5)*vec2(110.0,110.0*ground_perspective);
    float charted = texture(charted_mask,(world+warp)/world_size).r;
    float interior = texture(charted_mask,world/world_size).r;
    // Deform the feathered bank, protecting the readable interior as clouds drift.
    float clear_ground = max(charted,smoothstep(0.98,1.0,interior));
    float sight = 1.0-smoothstep(1.3,2.5,length((world-hero_point)/vec2(128.0,128.0*ground_perspective)));
    float haze = (1.0-sight)*(0.035+wisps*0.045);
    float smoke = 1.0;
    vec3 deep = vec3(0.48,0.60,0.61);
    vec3 rolling = vec3(0.90,0.93,0.87);
    vec3 color = mix(deep,rolling,smoothstep(0.16,0.85,billow)*0.80);
    color += vec3(0.055,0.06,0.064)*pow(wisps,3.0);
    color = mix(color,rolling,clear_ground*(1.0-clear_ground)*0.9);
    // Uncharted ground is fully concealed; knowledge reveals the painted landscape.
    COLOR = vec4(color,mix(smoke,haze,clear_ground));
}
