Shader "Water/WaveParticle_Rasterize"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One  // additive: accumulate all particles

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            float _HeightScale;

            WAVE_PARTICLE vert(VS_INPUT v)
            {
                WAVE_PARTICLE o;
                float2 pos       = v.pos.xy;
                float2 direction = normalize(v.texCoord);
                float  height    = v.pos.z;
                float  speed     = v.nor.z;

                o.pos       = float4(pos, 0.5, 1.0);
                o.velocity  = direction * speed;
                o.amplitude = height * _HeightScale;
                return o;
            }

            float4 frag(WAVE_PARTICLE i) : SV_Target
            {
                return float4(i.velocity, i.amplitude, 1.0);
            }
            ENDHLSL
        }
    }
}
