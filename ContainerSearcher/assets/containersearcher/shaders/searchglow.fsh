#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

out vec4 fragColor;
in vec4 vertexColor;

void main()
{
    fragColor = vertexColor;
}