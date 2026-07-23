shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never;

//uniform sampler2D SCREEN_TEXTURE : hint_screen_texture;
uniform vec4 outline_color : source_color;
uniform float outline_width = 0.005;
//uniform bool constant_width = true;

void vertex()
{
	vec3 world_pos = NODE_POSITION_WORLD + VERTEX;
	float len_to_camera = length(CAMERA_POSITION_WORLD - world_pos);
	
	//float dot_r = dot(NORMAL, CAMERA_DIRECTION_WORLD);
	//vec3 normal_fixed = NORMAL - CAMERA_DIRECTION_WORLD * dot_r + 0.002 * CAMERA_DIRECTION_WORLD;
	//normal_fixed = normalize(normal_fixed);
	
	VERTEX = VERTEX + outline_width * NORMAL * len_to_camera;
	
}

void fragment() {
	//vec4 pixelatedtext = texture(SCREEN_TEXTURE, SCREEN_UV);
	//vec2 pixel_size = 1.0 / VIEWPORT_SIZE;
	//ALBEDO = pixelatedtext.rgb;
	
	ALBEDO = outline_color.rgb;
	//ALPHA = 1.0 - pixelatedtext.a;
}