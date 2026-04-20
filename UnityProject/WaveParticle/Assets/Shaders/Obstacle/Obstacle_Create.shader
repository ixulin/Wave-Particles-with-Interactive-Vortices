// Obstacle brush: fullscreen quad + SV_Position UV + explicit viewport.
// Avoids Graphics.Blit viewport inheritance issue on DX11.
Shader "Water/Obstacle_Create"
{
    Properties
    {
        _BrushRadius   ("Brush Radius (UV)",  Float) = 0.05
        _BrushStrength ("Brush Strength",     Float) = 1.0
        _BrushCenterU  ("Brush Center U",     Float) = 0.5
        _BrushCenterV  ("Brush Center V",     Float) = 0.5
        _TexWidth      ("RT Width",           Float) = 142
        _TexHeight     ("RT Height",          Float) = 142
    }

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

            float4 vert(float4 v : POSITION) : SV_Position
            {
                return float4(v.xy, 0.5, 1.0);
            }

            float _BrushRadius;
            float _BrushStrength;
            float _BrushCenterU;
            float _BrushCenterV;
            float _TexWidth;
            float _TexHeight;

            float4 frag(float4 pos : SV_Position) : SV_Target
            {
                float2 uv   = pos.xy / float2(_TexWidth, _TexHeight);
                float  dist = length(uv - float2(_BrushCenterU, _BrushCenterV));
                return float4(_BrushStrength * step(dist, _BrushRadius), 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
