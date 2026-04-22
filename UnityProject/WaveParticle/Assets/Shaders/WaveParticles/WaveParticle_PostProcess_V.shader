// PostProcess Vertical pass
// Matches PostProcessPS_V.hlsl logic exactly
// Outputs to MRT: col1 = deviation (xyz displacement), col2 = gradient
Shader "Water/WaveParticle_PostProcess_V"
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

            Texture2D    _HorizontalFilter1;
            Texture2D    _HorizontalFilter2;
            SamplerState sampler_linear_repeat;

            int   _TextureWidth;
            int   _TextureHeight;
            int   _BlurRadius;
            float _DxScale;
            float _DzScale;
            int   _Mode;

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

                float3 f123 = _HorizontalFilter1.SampleLevel(sampler_linear_repeat, uv, 0).xyz;
                float4 f45v = _HorizontalFilter2.SampleLevel(sampler_linear_repeat, uv, 0);

                float4 deviation = float4(f45v.x, 0, f123.x, 1);
                float4 gradient  = float4(f123.y, 0, 0, 1);
                float2 gradCorr  = float2(f123.z, f45v.y);

                if (_Mode == 0 || _Mode == 8 || _Mode == 9 || _Mode == 10 || _Mode == 11)
                {
                    for (int k = 1; k <= _BlurRadius; k++)
                    {
                        float  offset = k / float(_TextureHeight);
                        float4 f123B = _HorizontalFilter1.SampleLevel(sampler_linear_repeat, uv + float2(0,  offset), 0);
                        float4 f123T = _HorizontalFilter1.SampleLevel(sampler_linear_repeat, uv + float2(0, -offset), 0);
                        float4 f45vB = _HorizontalFilter2.SampleLevel(sampler_linear_repeat, uv + float2(0,  offset), 0);
                        float4 f45vT = _HorizontalFilter2.SampleLevel(sampler_linear_repeat, uv + float2(0, -offset), 0);
                        float3 f = GetFilter(k / float(_BlurRadius));

                        deviation.x += (f45vB.x + f45vT.x) * f.x * f.x;
                        deviation.y += (f45vB.y - f45vT.y) * 2 * f.x * f.y;
                        deviation.z += (f123B.x + f123T.x) * f.x;
                        gradient.x  += (f123B.y + f123T.y) * f.x;
                        gradient.y  += (f123B.x - f123T.x) * f.y;
                        gradCorr.x  += (f123B.z + f123T.z) * f.x * f.x;
                        gradCorr.y  += (f45vB.y + f45vT.y) * f.z;
                    }

                    gradCorr   *= PI / _BlurRadius;
                    gradient.xy *= (PI / _BlurRadius) / (1 + gradCorr);
                }

                deviation.x *= _DxScale;
                deviation.y *= _DzScale;
                o.col1 = float4(-deviation.x, deviation.z, -deviation.y, deviation.w);
                o.col2 = gradient;
                return o;
            }
            ENDHLSL
        }
    }
}
