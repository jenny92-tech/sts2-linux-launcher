shader_type canvas_item;
render_mode blend_mix;

// Original: per-pixel atan/length polar + dual sample. Compat: single sample, no polar math.
uniform float distortion_base_intensity = 0.05;
uniform vec2 tiling = vec2(1.0, 1.0);
uniform vec2 offset_speed;

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	float m = texture(TEXTURE, UV).a;
	COLOR = vec4(vec3(1.0), clamp(m * vertex_color.a * distortion_base_intensity * 4.0, 0.0, 0.2));
}
