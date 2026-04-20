Shader "Water/Obstacle_BlurH"
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

            Texture2D    _SourceTex;
            SamplerState sampler_linear_repeat;
            float        _TextureWidth;

            // 3-tap Gaussian weights matching PostProcessObstaclePS_H.hlsl
            static const float weights[3] = { 0.01330373, 0.11098164, 0.22508352 };

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float4 col = weights[0] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0);
                for (int k = 1; k <= 2; k++)
                {
                    float offset = k / _TextureWidth;
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord + float2(-offset, 0), 0);
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord + float2( offset, 0), 0);
                }
                return col;
            }
            ENDHLSL
        }
    }
}
