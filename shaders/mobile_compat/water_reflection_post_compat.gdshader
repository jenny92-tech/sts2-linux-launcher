shader_type canvas_item;

// Original: noise-distorted reflection, 3 samples. Compat: 2-sample pass-through.
uniform sampler2D noise : repeat_enable;
uniform sampler2D mask;
uniform float speed : hint_range(0.002,0.5) = 0.1;
uniform float strength : hint_range(0.002,0.5) = 0.1;

void fragment() {
	COLOR = texture(TEXTURE, UV);
	COLOR.a *= texture(mask, UV).a * 0.35;
}
