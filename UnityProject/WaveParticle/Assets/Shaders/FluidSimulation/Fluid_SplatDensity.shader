Shader "Water/Fluid_SplatDensity"
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
            Texture2D    _DensityTex;
            SamplerState sampler_linear_repeat;

            float _SplatDensityU;
            float _SplatDensityV;
            float _SplatDensityRadius;
            float _SplatDensityScale;

            float4 vert(float4 v : POSITION) : SV_Position { return float4(v.xy, 0.5, 1.0); }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 T   = pos.xy / float2(_TextureWidthFluid, _TextureHeightFluid);
                float ob = _ObstacleTex.SampleLevel(sampler_linear_repeat, T, 0).x;
                if (ob > _ObstacleThresholdFluid)
                    return float4(0, 0, 0, 1);

                float4 col = _DensityTex.SampleLevel(sampler_linear_repeat, T, 0);
                if (length(T - float2(_SplatDensityU, _SplatDensityV)) < _SplatDensityRadius)
                    col += float4(_SplatDensityScale, 0, 0, 0);
                return col;
            }
            ENDHLSL
        }
    }
}
