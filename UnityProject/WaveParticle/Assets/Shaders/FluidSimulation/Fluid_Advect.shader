Shader "Water/Fluid_Advect"
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
            Texture2D    _SrcTex;
            SamplerState sampler_linear_repeat;

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float solid = _ObstacleTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0).x;
                if (solid > _ObstacleThresholdFluid)
                    return float4(0, 0, 0, 1);

                float2 u = _VelocityTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0).xy;
                float2 c = i.texCoord - _TimeStepFluid * u;
                return _FluidDissipation * _SrcTex.SampleLevel(sampler_linear_repeat, c, 0);
            }
            ENDHLSL
        }
    }
}
