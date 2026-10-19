#version 120
#define FXAA_SPAN_MAX 8.0
#define FXAA_REDUCE_MUL 1.0/8.0
#define FXAA_REDUCE_MIN 1.0/128.0

uniform sampler2D texSampler;
uniform float screenSizeX;
uniform float screenSizeY;
 
void main( void ) {
    vec4 frameBufSize = vec4(screenSizeX, screenSizeY, 1, 1);
 
	vec3 rgbNW=texture2D(texSampler,gl_TexCoord[0].st+(vec4(-1.0,-1.0, 0, 0)/frameBufSize).st).xyz;
	vec3 rgbNE=texture2D(texSampler,gl_TexCoord[0].st+(vec4(1.0,-1.0, 0, 0)/frameBufSize).st).xyz;
	vec3 rgbSW=texture2D(texSampler,gl_TexCoord[0].st+(vec4(-1.0,1.0, 0, 0)/frameBufSize).st).xyz;
	vec3 rgbSE=texture2D(texSampler,gl_TexCoord[0].st+(vec4(1.0,1.0, 0, 0)/frameBufSize).st).xyz;
	vec3 rgbM=texture2D(texSampler,gl_TexCoord[0].st).xyz;
	
	vec3 luma=vec3(0.299, 0.587, 0.114);
	float lumaNW = dot(rgbNW, luma);
	float lumaNE = dot(rgbNE, luma);
	float lumaSW = dot(rgbSW, luma);
	float lumaSE = dot(rgbSE, luma);
	float lumaM  = dot(rgbM,  luma);
	
	float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
	float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
	
	vec2 dir;
	dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
	dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));
	
	float dirReduce = max(
			(lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * FXAA_REDUCE_MUL),
			FXAA_REDUCE_MIN);
	  
	float rcpDirMin = 1.0/(min(abs(dir.x), abs(dir.y)) + dirReduce);
	
	dir = min(vec2( FXAA_SPAN_MAX,  FXAA_SPAN_MAX),
			  max(vec2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
			  dir * rcpDirMin)) / frameBufSize.xy;
			
	vec3 rgbA = (0.5) * (
			texture2D(texSampler, gl_TexCoord[0].xy + dir * (0.333 - 0.5)).xyz +
			texture2D(texSampler, gl_TexCoord[0].xy + dir * (0.666 - 0.5)).xyz);
	vec3 rgbB = rgbA * (0.5) + (0.25) * (
			texture2D(texSampler, gl_TexCoord[0].xy + dir * (0 - 0.5)).xyz +
			texture2D(texSampler, gl_TexCoord[0].xy + dir * (1 - 0.5)).xyz);
	float lumaB = dot(rgbB, luma);

	if((lumaB < lumaMin) || (lumaB > lumaMax)){
			gl_FragColor.xyz=rgbA;
			gl_FragColor.a = 1;
	}else{
			gl_FragColor.xyz=rgbB;
			gl_FragColor.a = 1;
	}
}