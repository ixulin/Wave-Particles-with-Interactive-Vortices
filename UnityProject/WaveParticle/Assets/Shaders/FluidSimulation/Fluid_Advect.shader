Shader "Water/Fluid_Advect"
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
            Texture2D    _SrcTex;
            SamplerState sampler_linear_repeat;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float solid = _ObstacleTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                if (solid > _ObstacleThresholdFluid)
                    return float4(0, 0, 0, 1);

                float2 u = _VelocityTex.SampleLevel(sampler_linear_repeat, T, 0).xy;
                float2 c = T - _TimeStepFluid * u;
                return _FluidDissipation * _SrcTex.SampleLevel(sampler_linear_repeat, c, 0);
            }
            ENDHLSL
        }
    }
}
