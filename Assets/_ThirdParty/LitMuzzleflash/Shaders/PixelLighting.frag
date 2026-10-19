#version 120

#define MULTIPLYER 0.8
#define DIFFUSE_MULTIPLYER 2.5

uniform sampler2D Texture0;
uniform sampler2D normalMap;
uniform sampler2D emissiveMap;
uniform float enabledLights;
varying vec3 position;
varying vec3 normal;
uniform vec2 resolution;
uniform float usesNormalMap;
uniform float normalFlip;
uniform float usesEmissiveMap;
uniform float vertexLighting;

void main()
{
	float depth = gl_FragCoord.z / gl_FragCoord.w;
	float fogFactor = (gl_Fog.end - depth) / (gl_Fog.end - gl_Fog.start);
	fogFactor = clamp(fogFactor, 0.0, 1.0);

	vec4 finalColor = gl_LightSource[0].ambient / gl_LightSource[0].quadraticAttenuation * MULTIPLYER;
	vec3 lengthVector = gl_LightSource[0].position.xyz;
	vec3 normalizedLengthVector = normalize(lengthVector);
	float vectorLength = length(lengthVector);

	vec3 newNormal = normal;
//NormalMaps
	if(usesNormalMap == 1) {
		newNormal = normalize (texture2D(normalMap,gl_TexCoord[0].st).xyz*2.0 - 1.0);
		newNormal.y*=normalFlip;	//invert if directx normal
		vec3 fragTangent;
		vec3 fragBitangent;

		vec3 edge1 = dFdx(position);
		vec3 edge2 = dFdy(position);
		vec2 deltaUV1 = dFdx(gl_TexCoord[0].st);
		vec2 deltaUV2 = dFdy(gl_TexCoord[0].st);
		float f = 1.0 / (deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y);
		fragTangent = normalize(f * (deltaUV2.y * edge1 - deltaUV1.y * edge2));	
		fragBitangent = normalize(f * (-deltaUV2.x * edge1 + deltaUV1.x * edge2));

		vec3 T = normalize(fragTangent);
		vec3 B = normalize(fragBitangent);
		vec3 N = normalize(normal);
		mat3 TBN = mat3(T, B, N);
		vec3 normalWorld = normalize(TBN * newNormal);
		newNormal = normalWorld;
	}
//End_NormalMapsz
	

	//Causes walls to light up when look at from the side (causes entire scene to light, should be world-based not screen/view-based)
	float NdotL = dot(newNormal, normalizedLengthVector);
	float viewlightIntensity = max(0.0, NdotL);
	finalColor += viewlightIntensity * (gl_LightSource[0].diffuse / gl_LightSource[0].quadraticAttenuation * DIFFUSE_MULTIPLYER);

	vec4 texColor = texture2D(Texture0,gl_TexCoord[0].st);
	for (int i = 1; i < enabledLights; i++) {
		lengthVector = gl_LightSource[i].position.xyz - position;
		normalizedLengthVector = normalize(lengthVector);
		vectorLength = length(lengthVector);
		NdotL = dot(newNormal, normalizedLengthVector);
		if (NdotL > 0.0) {
			if (gl_LightSource[i].quadraticAttenuation > 0) {
				float lightAttenuation = NdotL * (1.f - vectorLength / gl_LightSource[i].quadraticAttenuation);
				if (gl_LightSource[i].spotCutoff == 180 && i > 2) {
					//point lights
					if (vectorLength < gl_LightSource[i].quadraticAttenuation) {
						finalColor += lightAttenuation * (gl_LightSource[i].diffuse / gl_LightSource[i].quadraticAttenuation * DIFFUSE_MULTIPLYER);
					};
				} else {
					//flashlight and muzzleflash
					float spotEffect = dot(normalize(gl_LightSource[i].spotDirection), -normalizedLengthVector);
					
					//Cutoff
					float outterCutoff = gl_LightSource[i].spotCosCutoff + 0.05;
					float innerCutoff = gl_LightSource[i].spotCosCutoff;
					float cutoffDifference = innerCutoff-outterCutoff;
					float cutoffEffect = 1 - clamp((spotEffect - outterCutoff) / cutoffDifference, 0.0, 1.0);
	
					if(spotEffect > gl_LightSource[i].spotCosCutoff) {
						float lightIntensity = max(0.0, lightAttenuation * cutoffEffect);
						vec4 lightStrength = gl_LightSource[i].diffuse / gl_LightSource[i].quadraticAttenuation * DIFFUSE_MULTIPLYER;	//original lighting calculation
						lightStrength *= ((0.01/gl_LightSource[i].linearAttenuation)/(vectorLength*0.002));	//multiply strength by distance and flashlight strength
						vec4 maxLight = texColor * normalize(gl_LightSource[i].diffuse) * DIFFUSE_MULTIPLYER;	//Don't let the light completely overblow everything
						lightStrength = min(maxLight,lightStrength);
						lightStrength *= lightIntensity;
						finalColor += lightStrength;
					};
				};
			};
		};
	}
	
	vec4 finalColorClamp = gl_Color * 20;
	finalColorClamp = clamp(finalColorClamp,0,DIFFUSE_MULTIPLYER);
	finalColor += finalColorClamp * vertexLighting;
	
	finalColor.a = 1;
	vec4 fogColor = gl_Fog.color;
	fogColor.a = texColor.a;
//Apply emissive map
	if(usesEmissiveMap == 1) {
		vec4 emissiveFactor = texture2D(emissiveMap,gl_TexCoord[0].st);
		vec4 emissiveOut = mix(finalColor, texColor, emissiveFactor);
		finalColor = mix(finalColor, emissiveOut, usesEmissiveMap);
	}
//End_emissive
	gl_FragColor = mix(fogColor, texColor * finalColor, fogFactor);
}