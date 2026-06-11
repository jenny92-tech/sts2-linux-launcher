shader_type canvas_item;

// Original: fullscreen SCREEN_TEXTURE heat-wave distortion. Compat: output transparent, no SCREEN_TEXTURE copy.
uniform sampler2D NOISE_TEXTURE: repeat_enable;
uniform float strength: hint_range(0.0, 5, 0.1) = 1.0;
uniform float uv_scaling: hint_range (0.0, 1.0, 0.05) = 1.0;
uniform vec2 movement_direction = vec2(1, 0);
uniform float movement_speed: hint_range (0.0, 0.5, 0.01) = 0.1;

void fragment() {
	COLOR = vec4(0.0);
}
