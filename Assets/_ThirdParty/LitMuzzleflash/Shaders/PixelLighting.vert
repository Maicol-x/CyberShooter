#version 120

varying vec3 position;
varying vec3 normal;

void main()
{  
    position = vec3(gl_ModelViewMatrix * gl_Vertex);  
	normal = normalize(gl_NormalMatrix * gl_Normal);
    gl_TexCoord[0] = gl_MultiTexCoord0;
	gl_Position = ftransform();
	gl_FrontColor = gl_Color;
} 