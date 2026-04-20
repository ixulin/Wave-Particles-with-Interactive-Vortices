Shader "Water/Fluid_SplatDensity"
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

            Texture2D    _ObstacleTex;
            Texture2D    _DensityTex;
            SamplerState sampler_linear_repeat;

            float _SplatDensityU;
            float _SplatDensityV;
            float _SplatDensityRadius;
            float _SplatDensityScale;

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float ob = _ObstacleTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0).x;
                if (ob > _ObstacleThresholdFluid)
                    return float4(0, 0, 0, 1);

                float4 col = _DensityTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0);
                float2 T   = i.texCoord;
                if (length(T - float2(_SplatDensityU, _SplatDensityV)) < _SplatDensityRadius)
                    col += float4(_SplatDensityScale, 0, 0, 0);
                return col;
            }
            ENDHLSL
        }
    }
}
