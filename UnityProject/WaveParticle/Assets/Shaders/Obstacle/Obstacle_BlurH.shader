Shader "Water/Obstacle_BlurH"
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

            float4 vert(float4 v : POSITION) : SV_Position
            {
                return float4(v.xy, 0.5, 1.0); // NDC passthrough, viewport set explicitly
            }

            Texture2D    _SourceTex;
            SamplerState sampler_linear_repeat;
            float        _TextureWidth;
            float        _TextureHeight;

            static const float weights[3] = { 0.01330373, 0.11098164, 0.22508352 };

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                // Derive UV from pixel position — no texCoord/Blit convention issues
                float2 uv = pos.xy / float2(_TextureWidth, _TextureHeight);
                float4 col = weights[0] * _SourceTex.SampleLevel(sampler_linear_repeat, uv, 0);
                for (int k = 1; k <= 2; k++)
                {
                    float offset = k / _TextureWidth;
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, uv + float2(-offset, 0), 0);
                    col += weights[k] * _SourceTex.SampleLevel(sampler_linear_repeat, uv + float2( offset, 0), 0);
                }
                return col;
            }
            ENDHLSL
        }
    }
}
