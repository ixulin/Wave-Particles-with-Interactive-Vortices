Shader "Water/Fluid_SubtractGradient"
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
            Texture2D    _VelocityTex;
            SamplerState sampler_linear_repeat;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T  = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float  oC = _ObstacleTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                if (oC > _ObstacleThresholdFluid)
                    return float4(0, 0, 0, 1);

                float pC = _PressureTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                float pN = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).x;
                float pS = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).x;
                float pE = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).x;
                float pW = _PressureTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).x;

                float oN = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).x;
                float oS = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).x;
                float oE = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).x;
                float oW = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).x;

                float2 obstV = float2(0, 0);
                float2 vMask = float2(1, 1);
                float2 oldV  = _VelocityTex.SampleLevel(sampler_linear_repeat, T, 0).xy;

                if (oN > _ObstacleThresholdFluid) { pN = pC; obstV.y = 0; vMask.y = 0; }
                if (oS > _ObstacleThresholdFluid) { pS = pC; obstV.y = 0; vMask.y = 0; }
                if (oE > _ObstacleThresholdFluid) { pE = pC; obstV.x = 0; vMask.x = 0; }
                if (oW > _ObstacleThresholdFluid) { pW = pC; obstV.x = 0; vMask.x = 0; }

                float  halfInvCellSize = 0.5 / _FluidCellSize;
                float2 grad  = float2(pE - pW, pN - pS) * halfInvCellSize;
                float2 newV  = oldV - grad;
                return float4((vMask * newV) + obstV, 0, 1);
            }
            ENDHLSL
        }
    }
}
