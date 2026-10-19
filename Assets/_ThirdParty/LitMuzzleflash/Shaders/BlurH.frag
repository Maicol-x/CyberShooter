uniform sampler2D texSampler;

uniform float screenSizeX;

void main() 
{
	float blurSize = 1.0/screenSizeX;
	vec4 BlurCol = vec4(0.0, 0.0, 0.0, 0.0);
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(-4.0 * blurSize, 0.0)) * 0.06;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(-3.0 * blurSize, 0.0)) * 0.10;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(-2.0 * blurSize, 0.0)) * 0.13;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(-1.0 * blurSize, 0.0)) * 0.15;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(0.0, 0.0)) * 0.16;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(1.0 * blurSize, 0.0)) * 0.15;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(2.0 * blurSize, 0.0)) * 0.13;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(3.0 * blurSize, 0.0)) * 0.10;
	BlurCol += texture2D(texSampler, gl_TexCoord[0].xy + vec2(4.0 * blurSize, 0.0)) * 0.06;
	gl_FragColor = BlurCol;
}