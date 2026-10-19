uniform sampler2D texSampler;
uniform float intensity;

void main() 
{		
	vec4 finalVal = texture2D(texSampler, gl_TexCoord[0].xy);
	
	/*float lum = dot(finalVal.rgb, vec3(0.299, 0.587, 0.114)); // ITU-R BT.601 luma coefficients
	float lum2 = max(lum - 0.5, 0.0f);
	finalVal.rgb *= lum2 / lum;*/
	
	//finalVal *= 2.0;
	finalVal = finalVal * finalVal * finalVal * finalVal;
	finalVal *= intensity;
	gl_FragColor = finalVal;
}