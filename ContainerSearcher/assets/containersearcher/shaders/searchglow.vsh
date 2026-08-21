#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPositionIn;

out vec4 vertexColor;

uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;

void main(void)
{
	vec4 cameraPos = modelViewMatrix * vec4(vertexPositionIn, 1.0);
	gl_Position = projectionMatrix * cameraPos;
	vertexColor = vec4(1.0, 1.0, 1.0, 1.0);
}