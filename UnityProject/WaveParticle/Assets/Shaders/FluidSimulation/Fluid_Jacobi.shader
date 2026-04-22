Shader "Water/Fluid_Jacobi"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            Texture2D    _ObstacleTex;
            Texture2D    _PressureTex;
            Texture2D    _DivergenceTex;
            SamplerState sampler_linear_repeat;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float4 pC = _PressureTex.SampleLevel(sampler_linear_repeat, T, 0);
                float4 pN = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0);
                float4 pS = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0);
                float4 pE = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0);
                float4 pW = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0);

                float oN = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).x;
                float oS = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).x;
                float oE = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).x;
                float oW = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).x;

                if (oN > _ObstacleThresholdFluid) pN = pC;
                if (oS > _ObstacleThresholdFluid) pS = pC;
                if (oE > _ObstacleThresholdFluid) pE = pC;
                if (oW > _ObstacleThresholdFluid) pW = pC;

                float4 bC    = _DivergenceTex.SampleLevel(sampler_linear_repeat, T, 0);
                float  alpha = -_FluidCellSize * _FluidCellSize;
                return 1.01 * (pW + pE + pS + pN + alpha * bC) * 0.25;
            }
            ENDHLSL
        }
    }
}
