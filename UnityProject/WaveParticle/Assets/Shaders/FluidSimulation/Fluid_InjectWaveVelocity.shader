Shader "Water/Fluid_InjectWaveVelocity"
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
            Texture2D    _WaveParticleTex;
            SamplerState sampler_linear_repeat;

            float _WaveInjectStrength;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T   = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float4 col = _VelocityTex.SampleLevel(sampler_linear_repeat, T, 0);
                float  ob  = _ObstacleTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                if (ob > _ObstacleThresholdFluid)
                    return col;

                float2 waveVel = _WaveParticleTex.SampleLevel(sampler_linear_repeat, T, 0).xy;
                col.xy += waveVel * _WaveInjectStrength;
                return col;
            }
            ENDHLSL
        }
    }
}
