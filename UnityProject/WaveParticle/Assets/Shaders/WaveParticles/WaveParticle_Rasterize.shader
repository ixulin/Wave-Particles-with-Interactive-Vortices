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
            float _WaveParticleSpeedScale;
            float _TimeScale;
            int   _Time_Custom;

            WAVE_PARTICLE vert(VS_INPUT v)
            {
                WAVE_PARTICLE o;

                float2 pos       = v.pos.xy;
                float2 direction = normalize(v.texCoord);
                float  height    = v.pos.z;
                float  speed     = _WaveParticleSpeedScale * v.nor.z;

                pos = pos + speed * _TimeScale * float(_Time_Custom) * direction;

                float2 posAbs = abs(pos);
                if (posAbs.x > 1.0 || posAbs.y > 1.0)
                {
                    float2 offset = float2(0, 0);
                    int2   posI   = (int2)posAbs;
                    float2 posF   = posAbs - (float2)posI;

                    if (posAbs.x > 1.0)
                    {
                        offset.x = (posI.x - 1) % 2 + posF.x;
                        pos.x    = sign(pos.x) * offset.x + sign(pos.x) * -1;
                    }
                    if (posAbs.y > 1.0)
                    {
                        offset.y = (posI.y - 1) % 2 + posF.y;
                        pos.y    = sign(pos.y) * offset.y + sign(pos.y) * -1;
                    }
                }

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
