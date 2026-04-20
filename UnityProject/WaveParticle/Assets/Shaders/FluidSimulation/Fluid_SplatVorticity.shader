Shader "Water/Fluid_SplatVorticity"
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
            Texture2D    _VelocityTex;
            SamplerState sampler_linear_repeat;

            float _SplatDirU;
            float _SplatDirV;
            float _SplatScale;

            float vorticity(float2 T)
            {
                float2 vN = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(0,  1.0/_TextureHeightFluid), 0).xy;
                float2 vS = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(0, -1.0/_TextureHeightFluid), 0).xy;
                float2 vE = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2( 1.0/_TextureWidthFluid, 0),  0).xy;
                float2 vW = _VelocityTex.SampleLevel(sampler_linear_repeat, T + float2(-1.0/_TextureWidthFluid, 0),  0).xy;
                return 0.5 / _FluidCellSize * ((vE.y - vW.y) - (vN.x - vS.x));
            }

            float4 frag(VS_OUTPUT i) : SV_Target
            {
                float ob = _ObstacleTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0).x;
                if (ob > _ObstacleThresholdFluid)
                    return _VelocityTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0);

                float4 col = _VelocityTex.SampleLevel(sampler_linear_repeat, i.texCoord, 0);
                float2 T   = i.texCoord;

                float vorC = vorticity(T);
                float vorN = vorticity(T + float2(0,  1.0/_TextureHeightFluid));
                float vorS = vorticity(T + float2(0, -1.0/_TextureHeightFluid));
                float vorE = vorticity(T + float2( 1.0/_TextureHeightFluid, 0));
                float vorW = vorticity(T + float2(-1.0/_TextureHeightFluid, 0));

                float2 force = 0.5 / _FluidCellSize * float2(abs(vorN) - abs(vorS), abs(vorE) - abs(vorW));
                float  eps   = EPSILON;
                force = force * rsqrt(max(dot(force, force), eps));
                force *= _FluidCellSize * vorC * float2(1, -1);

                float2 splatDir = float2(_SplatDirU, _SplatDirV);
                col += float4(splatDir * _SplatScale + force * _TimeStepFluid * _VorticityScale, 0, 0);
                return col;
            }
            ENDHLSL
        }
    }
}
