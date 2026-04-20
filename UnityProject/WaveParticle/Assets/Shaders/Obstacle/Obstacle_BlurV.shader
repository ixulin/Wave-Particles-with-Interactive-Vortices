Shader "Water/Obstacle_BlurV"
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
            float        _TextureWidth; // reuse for height

            static const float weights[3] = { 0.01330373, 0.11098164, 0.22508352 };

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float4 col = weights[0] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0);
                for (int k = 1; k <= 2; k++)
                {
                    float offset = k / _TextureWidth;
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord + float2(0, -offset), 0);
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, i.texCoord + float2(0,  offset), 0);
                }
                return col;
            }
            ENDHLSL
        }
    }
}
