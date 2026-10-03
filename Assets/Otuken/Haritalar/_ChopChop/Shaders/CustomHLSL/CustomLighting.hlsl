// Chop Chop'un Toon gölgelendiricilerinin ışık hesabı, URP 17 (Unity 6) için yeniden yazıldı.
// Asıl dosya: UOP1_Project/Assets/Shaders/CustomHLSL/CustomLighting.hlsl (Apache 2.0).
#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    #pragma multi_compile_fragment _ _SHADOWS_SOFT
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

void MainLight_float(float3 WorldPos, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = float3(0.5, 0.5, 0);
    Color = 1;
    ShadowAtten = 1;
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color;
    ShadowAtten = mainLight.shadowAttenuation;
#endif
}

void MainLight_half(float3 WorldPos, out half3 Direction, out half3 Color, out half ShadowAtten)
{
    float3 d, c;
    float s;
    MainLight_float(WorldPos, d, c, s);
    Direction = d;
    Color = c;
    ShadowAtten = s;
}

void DirectSpecular_float(float Smoothness, float3 Direction, float3 WorldNormal, float3 WorldView, out float3 Out)
{
#ifdef SHADERGRAPH_PREVIEW
    Out = 0;
#else
    float shininess = exp2(10 * Smoothness + 1);
    float3 n = normalize(WorldNormal);
    float3 v = SafeNormalize(WorldView);
    float3 h = SafeNormalize(Direction + v);
    Out = pow(saturate(dot(n, h)), shininess);
#endif
}

void DirectSpecular_half(half Smoothness, half3 Direction, half3 WorldNormal, half3 WorldView, out half3 Out)
{
    float3 o;
    DirectSpecular_float(Smoothness, Direction, WorldNormal, WorldView, o);
    Out = o;
}

void AdditionalLights_float(float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView,
                            out float3 Diffuse, out float3 Specular)
{
    float3 diffuseColor = 0;
    float3 specularColor = 0;
#ifndef SHADERGRAPH_PREVIEW
    float shininess = exp2(10 * Smoothness + 1);
    float3 n = normalize(WorldNormal);
    float3 v = SafeNormalize(WorldView);
    uint pixelLightCount = GetAdditionalLightsCount();
    for (uint i = 0u; i < pixelLightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPosition);
        float3 c = light.color * (light.distanceAttenuation * light.shadowAttenuation);
        diffuseColor += c * saturate(dot(n, light.direction));
        float3 h = SafeNormalize(light.direction + v);
        specularColor += c * pow(saturate(dot(n, h)), shininess);
    }
#endif
    Diffuse = diffuseColor;
    Specular = specularColor;
}

void AdditionalLights_half(half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView,
                           out half3 Diffuse, out half3 Specular)
{
    float3 d, s;
    AdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, d, s);
    Diffuse = d;
    Specular = s;
}

#endif
