// WaterCommon.hlsl - Unity port of GlobalInclude.hlsli
// Shared structs, constants and helper functions used by all water shaders

#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

#define NUM_CONTROL_POINTS 4
#define PI     3.14159265359
#define HALF_PI 1.57079632679
#define EPSILON 0.00024414

// ---- Global uniforms (set via Shader.SetGlobal* or material properties) ----

// Fluid params (set globally by FluidSimulator.cs)
float _TimeStepFluid;
float _FluidCellSize;
float _FluidDissipation;
float _VorticityScale;
int   _TextureWidthFluid;
int   _TextureHeightFluid;
float _ObstacleThresholdFluid;

// ---- Struct definitions ----

struct VS_INPUT
{
    float3 pos      : POSITION;
    float2 texCoord : TEXCOORD0;
    float3 nor      : NORMAL;
};

struct VS_OUTPUT
{
    float4 pos      : SV_POSITION;
    float2 texCoord : TEXCOORD0;
};

struct WAVE_PARTICLE
{
    float4 pos       : SV_POSITION;
    float2 velocity  : TEXCOORD0;
    float  amplitude : TEXCOORD1;
};

struct VS_CONTROL_POINT_OUTPUT
{
    float3 pos      : TEXCOORD0;
    float2 texCoord : TEXCOORD1;
};

struct HS_CONSTANT_DATA_OUTPUT
{
    float EdgeTessFactor[4]   : SV_TessFactor;
    float InsideTessFactor[2] : SV_InsideTessFactor;
};

struct DS_OUTPUT
{
    float4 pos      : SV_POSITION;
    float2 texCoord : TEXCOORD0;
    float3 PosW     : TEXCOORD1;
};

struct DS_OUTPUT_2
{
    float4 pos      : SV_POSITION;
    float2 texCoord : TEXCOORD0;
    float3 wpos     : TEXCOORD1;
    float3 nor      : TEXCOORD2;
};

struct PS_MRT_OUTPUT
{
    float4 col1 : SV_TARGET0;
    float4 col2 : SV_TARGET1;
};

// ---- Helper functions ----

float2 BLERP2(float2 v00, float2 v01, float2 v10, float2 v11, float2 uv)
{
    return lerp(lerp(v00, v01, uv.yy), lerp(v10, v11, uv.yy), uv.xx);
}

float3 BLERP3(float3 v00, float3 v01, float3 v10, float3 v11, float2 uv)
{
    return lerp(lerp(v00, v01, uv.yyy), lerp(v10, v11, uv.yyy), uv.xxx);
}

float3 GetFilter(float v)
{
    float s, c;
    sincos(PI * v, s, c);
    return float3(
        0.5f * (c + 1.0f),
        -0.5f * s,
        -0.25f * (c * c - s * s + c)
    );
}

float4 Flow(float2 uv, float time,
            Texture2D flowT, SamplerState flowS,
            Texture2D flowedT, SamplerState flowedS)
{
    float timeInt = time / (1.0 * 2.0);
    float2 fTime  = frac(float2(timeInt, timeInt + 0.5));
    float2 flowDir = -flowT.SampleLevel(flowS, uv, 0).xy;
    float2 flowUV1 = uv - (flowDir * 0.5) + fTime.x * flowDir;
    float2 flowUV2 = uv - (flowDir * 0.5) + fTime.y * flowDir;
    float4 tx1 = flowedT.SampleLevel(flowedS, flowUV1, 0);
    float4 tx2 = flowedT.SampleLevel(flowedS, flowUV2, 0);
    return lerp(tx1, tx2, abs(2.0 * frac(timeInt) - 1.0));
}

// For use in water shaders where textureWidth/Height are material properties
float4 FlowHeightWithNormal_impl(float2 uv, float time,
                                  Texture2D flowT, SamplerState flowS,
                                  Texture2D flowedT, SamplerState flowedS,
                                  float texW, float texH,
                                  out float3 normal)
{
    float timeInt = time / 2.0;
    float2 fTime  = frac(float2(timeInt, timeInt + 0.5));
    float2 flowDir = -flowT.SampleLevel(flowS, uv, 0).xy;
    float2 flowUV1 = uv - (flowDir * 0.5) + fTime.x * flowDir;
    float2 flowUV2 = uv - (flowDir * 0.5) + fTime.y * flowDir;

    float2 dUP = float2(1.0 / texW, 0);
    float2 dVP = float2(0, 1.0 / texH);

    float4 tx1 = flowedT.SampleLevel(flowedS, flowUV1, 0);
    float4 tx2 = flowedT.SampleLevel(flowedS, flowUV2, 0);

    // Normal from UV1
    float3 d1ddV = flowedT.SampleLevel(flowedS, flowUV1 + dVP, 0).xyz
                 - flowedT.SampleLevel(flowedS, flowUV1 - dVP, 0).xyz + float3(0, 0, 2.0 / texH);
    float3 d1ddU = flowedT.SampleLevel(flowedS, flowUV1 + dUP, 0).xyz
                 - flowedT.SampleLevel(flowedS, flowUV1 - dUP, 0).xyz + float3(2.0 / texW, 0, 0);
    float3 nor1  = cross(normalize(d1ddV), normalize(d1ddU));

    // Normal from UV2
    float3 d2ddV = flowedT.SampleLevel(flowedS, flowUV2 + dVP, 0).xyz
                 - flowedT.SampleLevel(flowedS, flowUV2 - dVP, 0).xyz + float3(0, 0, 2.0 / texH);
    float3 d2ddU = flowedT.SampleLevel(flowedS, flowUV2 + dUP, 0).xyz
                 - flowedT.SampleLevel(flowedS, flowUV2 - dUP, 0).xyz + float3(2.0 / texW, 0, 0);
    float3 nor2  = cross(normalize(d2ddV), normalize(d2ddU));

    float blend = abs(2.0 * frac(timeInt) - 1.0);
    normal = lerp(nor1, nor2, blend);
    return lerp(tx1, tx2, blend);
}

float3 FlowHeightForNormal_impl(float2 uv, float time,
                                 Texture2D flowT, SamplerState flowS,
                                 Texture2D flowedT, SamplerState flowedS,
                                 float texW, float texH)
{
    float3 nor;
    FlowHeightWithNormal_impl(uv, time, flowT, flowS, flowedT, flowedS, texW, texH, nor);
    return normalize(nor);
}

float3 ObstacleMapToNormal(float2 uv, Texture2D obstacleT, SamplerState obstacleS, float texW, float texH)
{
    float dU = clamp(obstacleT.SampleLevel(obstacleS, uv + float2(1.0/texW, 0), 0).x, 0, 1)
             - clamp(obstacleT.SampleLevel(obstacleS, uv - float2(1.0/texW, 0), 0).x, 0, 1);
    float dV = clamp(obstacleT.SampleLevel(obstacleS, uv + float2(0, 1.0/texH), 0).x, 0, 1)
             - clamp(obstacleT.SampleLevel(obstacleS, uv - float2(0, 1.0/texH), 0).x, 0, 1);
    float3 ddV = float3(0, dV, 0) + float3(0, 0, 2.0/texH);
    float3 ddU = float3(0, dU, 0) + float3(2.0/texW, 0, 0);
    return cross(normalize(ddV), normalize(ddU));
}

// Fullscreen pass vertex shader (used by all Blit-style shaders)
VS_OUTPUT vert_fullscreen(VS_INPUT v)
{
    VS_OUTPUT o;
    o.pos      = float4(v.pos.xy, 0.5, 1.0);
    o.texCoord = v.texCoord;
    // Flip V for Unity's UV convention (OpenGL-style, Y up)
#if UNITY_UV_STARTS_AT_TOP
    o.texCoord.y = 1.0 - o.texCoord.y;
#endif
    return o;
}

#endif // WATER_COMMON_INCLUDED
