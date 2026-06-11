shader_type canvas_item;
render_mode blend_mix;

// Original: 12-screen-sample radial blur. Compat: static soft glow, single distance, no sampling.
uniform vec2 blur_center = vec2(0.25, 0.5);
uniform float blur_power = 0.005;
uniform int sampling_count = 12;

void fragment(){
	float d = distance(UV, blur_center);
	COLOR = vec4(vec3(1.0), max(0.0, 0.12 - d * 0.15) * clamp(blur_power * 120.0, 0.0, 1.0));
}
