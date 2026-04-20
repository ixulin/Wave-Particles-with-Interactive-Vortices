Shader "Water/Obstacle_Create"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One  // additive: accumulate brush strokes

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            float _BrushScale;
            float _BrushStrength;
            float _BrushOffsetU;
            float _BrushOffsetV;

            float4 vert(VS_INPUT v) : SV_Position
            {
                // Scale circle and offset to brush position in NDC
                float2 pos = v.pos.xy * _BrushScale + float2(_BrushOffsetU, _BrushOffsetV);
                return float4(pos, 0.5, 1.0);
            }

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                return float4(_BrushStrength, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
