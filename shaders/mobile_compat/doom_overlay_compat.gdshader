shader_type canvas_item;

// Original: per-pixel pow + dual sin pulse. Compat: static gradient, no TIME or trig.
uniform float s: hint_range(0,5) = 1.0;
uniform float v = 1.0;
uniform vec4 gradient_color : source_color;

void fragment(){
	float grad = UV.y * UV.y * 0.5;
	vec3 base = gradient_color.rgb * mix(0.12, 0.4, clamp(v, 0.0, 1.0));
	COLOR = vec4(base + grad * gradient_color.rgb * 0.3, clamp(COLOR.a * 0.28, 0.0, 1.0));
}
