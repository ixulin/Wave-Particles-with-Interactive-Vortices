// PostProcess Horizontal pass
// Matches PostProcessPS_H.hlsl logic exactly
// Outputs to MRT: col1 = f123 (height/gradient), col2 = f45v (deviation/velocity)
Shader "Water/WaveParticle_PostProcess_H"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            Texture2D    _WaveParticleTex;
            SamplerState sampler_linear_repeat;

            int _TextureWidth;
            int _TextureHeight;
            int _BlurRadius;
            int _Mode;

            // MRT output struct
            struct FragOut
            {
                float4 col1 : SV_Target0;
                float4 col2 : SV_Target1;
            };

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            FragOut frag(float4 pos : SV_Position)
            {
                float2 uv = pos.xy / float2(_TextureWidth, _TextureHeight);
                FragOut o;
                o.col1 = float4(0, 0, 0, 0);
                o.col2 = float4(0, 0, 0, 0);

                float3 velAmp = _WaveParticleTex.SampleLevel(sampler_linear_repeat, uv, 0).xyz;
                float4 f123 = float4(velAmp.z, 0, 0.5 * velAmp.z, 1);
                float4 f45v = float4(0, velAmp.z, sign(velAmp.z) * velAmp.xy);

                if (_Mode == 0 || _Mode == 7 || _Mode == 9 || _Mode == 10 || _Mode == 11)
                {
                    for (int k = 1; k <= _BlurRadius; k++)
                    {
                        float  offset  = k / float(_TextureWidth);
                        float4 velAmpL = _WaveParticleTex.SampleLevel(sampler_linear_repeat, uv + float2( offset, 0), 0);
                        float4 velAmpR = _WaveParticleTex.SampleLevel(sampler_linear_repeat, uv + float2(-offset, 0), 0);
                        float  ampSum  = velAmpL.z + velAmpR.z;
                        float  ampDif  = velAmpL.z - velAmpR.z;
                        float3 f       = GetFilter(k / float(_BlurRadius));

                        f123.x += ampSum * f.x;
                        f123.y += ampDif * f.y;
                        f123.z += ampSum * f.z;
                        f45v.x += ampDif * f.x * f.y * 2;
                        f45v.y += ampSum * f.x * f.x;
                        f45v.z += (sign(velAmpL.z) * velAmpL.x + sign(velAmpR.z) * velAmpR.x) * f.x;
                        f45v.w += (sign(velAmpL.z) * velAmpL.y + sign(velAmpR.z) * velAmpR.y) * f.x;
                    }
                }

                o.col1 = f123;
                o.col2 = f45v;
                return o;
            }
            ENDHLSL
        }
    }
}
