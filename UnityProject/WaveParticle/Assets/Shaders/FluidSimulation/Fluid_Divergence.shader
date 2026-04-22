Shader "Water/Fluid_Divergence"
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
            Texture2D    _VelocityTex;
            SamplerState sampler_linear_repeat;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float2 vN = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).xy;
                float2 vS = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).xy;
                float2 vE = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).xy;
                float2 vW = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).xy;

                float oN = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).x;
                float oS = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).x;
                float oE = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).x;
                float oW = _ObstacleTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).x;

                if (oN > _ObstacleThresholdFluid) vN = float2(0, 0);
                if (oS > _ObstacleThresholdFluid) vS = float2(0, 0);
                if (oE > _ObstacleThresholdFluid) vE = float2(0, 0);
                if (oW > _ObstacleThresholdFluid) vW = float2(0, 0);

                float halfInvCellSize = 0.5 / _FluidCellSize;
                return halfInvCellSize * (vE.x - vW.x + vN.y - vS.y);
            }
            ENDHLSL
        }
    }
}
