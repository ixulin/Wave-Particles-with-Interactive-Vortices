Shader "Water/Fluid_SplatVelocityImpulse"
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

            float _ImpulseCenterU;
            float _ImpulseCenterV;
            float _ImpulseRadius;
            float _ImpulseU;
            float _ImpulseV;
            float _ImpulseStrength;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T   = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float4 col = _VelocityTex.SampleLevel(sampler_linear_repeat, T, 0);
                float  ob  = _ObstacleTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                if (ob > _ObstacleThresholdFluid)
                    return col;

                float2 d = T - float2(_ImpulseCenterU, _ImpulseCenterV);
                float  r = length(d);
                if (r < _ImpulseRadius)
                {
                    float falloff = 1.0 - r / _ImpulseRadius;
                    col.xy += float2(_ImpulseU, _ImpulseV) * _ImpulseStrength * falloff;
                }
                return col;
            }
            ENDHLSL
        }
    }
}
