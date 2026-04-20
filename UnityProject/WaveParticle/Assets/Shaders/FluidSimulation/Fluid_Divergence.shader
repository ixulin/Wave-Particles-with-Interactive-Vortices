Shader "Water/Fluid_Divergence"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert_fullscreen
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            Texture2D    _ObstacleTex;
            Texture2D    _VelocityTex;
            SamplerState sampler_linear_repeat;

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float2 T = i.texCoord;
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
