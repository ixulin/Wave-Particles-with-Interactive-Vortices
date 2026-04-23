// WaterSurface.shader - Built-in Pipeline water with Hardware Tessellation
// Port of VertexShader.hlsl + HullShader.hlsl + DomainShader.hlsl + PixelShader.hlsl
Shader "Water/WaterSurface"
{
    Properties
    {
        _AlbedoTex      ("Foam Texture",      2D) = "white" {}
        _FlowmapTex     ("Flow Map",          2D) = "gray"  {}
        _DeviationTex   ("Deviation (V1)",    2D) = "black" {}
        _GradientTex    ("Gradient (V2)",     2D) = "black" {}
        _DensityTex     ("Density",           2D) = "black" {}
        _PressureTex    ("Pressure",          2D) = "black" {}
        _DivergenceTex  ("Divergence",        2D) = "black" {}
        _ObstacleTex    ("Obstacle",          2D) = "black" {}
        _VelocityTex    ("Velocity",          2D) = "black" {}

        [IntRange] _EdgeTessFactor   ("Edge Tess Factor",   Range(1,64)) = 7
        [IntRange] _InsideTessFactor ("Inside Tess Factor", Range(1,64)) = 5
        [IntRange] _Mode ("Render Mode", Range(0,11)) = 11

        _HeightScale            ("Height Scale",             Float) = 0.14
        _FlowSpeed              ("Flow Speed",               Float) = 0.000931
        _TimeScale              ("Time Scale",               Float) = 1.3
        _DxScale                ("Dx Scale",                 Float) = 0.03
        _DzScale                ("Dz Scale",                 Float) = 0.03
        _FoamScale              ("Foam Scale",               Float) = 5.0
        _FoamPow                ("Foam Pow",                 Float) = 9.6

        _LightHeight            ("Light Height",             Float) = 9.35
        _ExtinctionCoeff        ("Extinction Coeff",         Float) = -0.41
        _Shininess              ("Shininess",                Float) = 340
        _FresnelBias            ("Fresnel Bias",             Float) = 0.0
        _FresnelPow             ("Fresnel Pow",              Float) = 3.0
        _FresnelScale           ("Fresnel Scale",            Float) = 0.68

        _ObstacleThresholdWave  ("Obstacle Threshold Wave",  Float) = 0.12
        _TextureWidth           ("Texture Width",            Float) = 500
        _TextureHeight          ("Texture Height",           Float) = 500
        _FluidHeightScale       ("Fluid Pressure -> Height", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            // SM5.0 required for tessellation
            #pragma target   5.0
            #pragma vertex   vert_water
            #pragma hull     hull_main
            #pragma domain   domain_main
            #pragma fragment frag_water

            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            // ---- Textures & Samplers ----
            Texture2D    _AlbedoTex;
            Texture2D    _FlowmapTex;
            Texture2D    _DeviationTex;
            Texture2D    _GradientTex;
            Texture2D    _DensityTex;
            Texture2D    _PressureTex;
            Texture2D    _DivergenceTex;
            Texture2D    _ObstacleTex;

            SamplerState sampler_linear_repeat;
            SamplerState sampler_linear_clamp;

            // ---- Uniforms ----
            int   _EdgeTessFactor;
            int   _InsideTessFactor;
            int   _Mode;
            int   _Time_Custom;
            float _HeightScale;
            float _FlowSpeed;
            float _TimeScale;
            float _DxScale;
            float _DzScale;
            float _FoamScale;
            float _FoamPow;
            float _LightHeight;
            float _ExtinctionCoeff;
            float _Shininess;
            float _FresnelBias;
            float _FresnelPow;
            float _FresnelScale;
            float _ObstacleThresholdWave;
            float _TextureWidth;
            float _TextureHeight;
            float _FluidHeightScale;

            // ---- Vertex Shader (pass-through, feeds hull shader) ----
            VS_CONTROL_POINT_OUTPUT vert_water(VS_INPUT v)
            {
                VS_CONTROL_POINT_OUTPUT o;
                o.pos      = v.pos;
                o.texCoord = v.texCoord;
                return o;
            }

            // ---- Hull Shader ----
            HS_CONSTANT_DATA_OUTPUT PatchConstantFunc(
                InputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> ip,
                uint patchID : SV_PrimitiveID)
            {
                HS_CONSTANT_DATA_OUTPUT o;
                o.EdgeTessFactor[0] = o.EdgeTessFactor[1] =
                o.EdgeTessFactor[2] = o.EdgeTessFactor[3] = (float)_EdgeTessFactor;
                o.InsideTessFactor[0] = o.InsideTessFactor[1] = (float)_InsideTessFactor;
                return o;
            }

            [domain("quad")]
            [partitioning("integer")]
            [outputtopology("triangle_ccw")]
            [outputcontrolpoints(NUM_CONTROL_POINTS)]
            [patchconstantfunc("PatchConstantFunc")]
            VS_CONTROL_POINT_OUTPUT hull_main(
                InputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> ip,
                uint id      : SV_OutputControlPointID,
                uint patchID : SV_PrimitiveID)
            {
                return ip[id];
            }

            // ---- Domain Shader ----
            [domain("quad")]
            DS_OUTPUT domain_main(
                HS_CONSTANT_DATA_OUTPUT tessFactors,
                float2 domainLoc : SV_DomainLocation,
                const OutputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> patch)
            {
                DS_OUTPUT o;

                float3 pos      = BLERP3(patch[0].pos, patch[1].pos, patch[3].pos, patch[2].pos, domainLoc);
                float2 texCoord = BLERP2(patch[0].texCoord, patch[1].texCoord, patch[3].texCoord, patch[2].texCoord, domainLoc);

                if (_Mode == 0 || _Mode == 10 || _Mode == 11)
                {
                    float ob = _ObstacleTex.SampleLevel(sampler_linear_clamp, texCoord, 0).x;
                    if (ob <= _ObstacleThresholdWave)
                    {
                        float  flowTime = float(_Time_Custom) * _TimeScale * _FlowSpeed;
                        float4 deviation = Flow(texCoord, flowTime,
                                                _FlowmapTex, sampler_linear_repeat,
                                                _DeviationTex, sampler_linear_repeat);
                        pos.y += deviation.y;
                        pos.x += deviation.x;
                        pos.z += deviation.z;

                        float pressure = _PressureTex.SampleLevel(sampler_linear_clamp, texCoord, 0).x;
                        pos.y += pressure * _FluidHeightScale;
                    }
                }

                float4 worldPos = mul(unity_ObjectToWorld, float4(pos, 1.0));
                o.PosW     = worldPos.xyz;
                o.pos      = mul(UNITY_MATRIX_VP, worldPos);
                o.texCoord = texCoord;
                return o;
            }

            // ---- Ambient water color (from PixelShader.hlsl) ----
            float Ei(float z)
            {
                const float euler = 0.577216f;
                float z2 = z*z, z3 = z2*z, z4 = z3*z, z5 = z4*z;
                return euler + log(z) + z + z2/4.f + z3/18.f + z4/96.f + z5/600.f;
            }

            float3 ComputeAmbientColor(float3 pos, float ext,
                                        float3 lightBot, float3 lightTop,
                                        float volTop, float volBot)
            {
                float Hp = volTop - pos.y;
                float a  = -ext * Hp;
                float3 top    = lightTop * max(0.0, exp(a) - a * Ei(a));
                float  Hb = pos.y - volBot;
                a = -ext * Hb;
                float3 bot    = lightBot * max(0.0, exp(a) - a * Ei(a));
                return top + bot;
            }

            // ---- Pixel Shader ----
            float4 frag_water(DS_OUTPUT i) : SV_Target
            {
                float2 uv  = i.texCoord;
                float3 worldPos = i.PosW;

                // ---- debug modes ----
                if (_Mode == 1)
                    return float4(_FlowmapTex.Sample(sampler_linear_clamp, uv).xy, 0, 1);
                if (_Mode == 2)
                {
                    float d = abs(_DensityTex.Sample(sampler_linear_clamp, uv).x);
                    return float4(d, d, d, 1);
                }
                if (_Mode == 3)
                {
                    float d = abs(_DivergenceTex.Sample(sampler_linear_clamp, uv).x);
                    return float4(d, d, d, 1);
                }
                if (_Mode == 4)
                    return _PressureTex.Sample(sampler_linear_clamp, uv);
                if (_Mode == 5)
                    return float4(Flow(uv, float(_Time_Custom) * _TimeScale * _FlowSpeed,
                                       _FlowmapTex, sampler_linear_repeat,
                                       _AlbedoTex, sampler_linear_repeat).xyz, 1);
                if (_Mode == 6 || _Mode == 7 || _Mode == 8 || _Mode == 9)
                    return _DeviationTex.Sample(sampler_linear_clamp, uv);

                // ---- normal computation (mode 10/11) ----
                float flowTime = float(_Time_Custom) * _TimeScale * _FlowSpeed;
                float3 normal  = FlowHeightForNormal_impl(uv, flowTime,
                                    _FlowmapTex, sampler_linear_repeat,
                                    _DeviationTex, sampler_linear_repeat,
                                    _TextureWidth, _TextureHeight);
                // World-space normal: multiply by transpose of inverse = multiply by WorldToObject transposed
                normal = normalize(mul(float4(normal, 0), (float4x4)unity_WorldToObject).xyz);

                if (_Mode == 10)
                    return float4((normal + 1.0) * 0.5, 1.0);

                // ---- mode 11: full shading ----

                // Foam
                float d = abs(_DivergenceTex.Sample(sampler_linear_clamp, uv).x);
                float3 foamCol = float3(0, 0, 0);
                if (d > 0)
                    foamCol = Flow(uv, flowTime, _FlowmapTex, sampler_linear_repeat,
                                   _AlbedoTex, sampler_linear_repeat).xyz;
                float3 foamColor = d * foamCol * _FoamScale;

                // Lighting
                float3 lightPos      = float3(2, _LightHeight, 10);
                float3 viewDir       = normalize(_WorldSpaceCameraPos - worldPos);
                float3 naiveLightDir = normalize(lightPos - worldPos);
                float3 halfwayDir    = normalize(viewDir + naiveLightDir);
                float  specHL        = pow(max(dot(normal, halfwayDir), 0.0), _Shininess);

                // Sky reflection
                float3 speccol = float3(0, 0, 0);
                if (dot(viewDir, normal) != 0)
                {
                    float3 reflDir = normalize(reflect(viewDir, normal));
                    float3 sky1 = float3(0.1, 0.2, 0.3) * 2;
                    float3 sky2 = float3(0.2, 0.2, 0.2) * 7;
                    speccol = lerp(sky1, sky2, abs(reflDir.y)) * 1.5;
                    float3 sunDir = normalize(float3(-1, 2, -1));
                    float angle = acos(dot(sunDir, -reflDir));
                    if (angle < 0.1)
                        speccol = lerp(speccol, float3(1, 0.3, 0), (0.1 - angle) / 0.1);
                }

                // Fresnel (empirical)
                float R = max(0, min(1, _FresnelBias + _FresnelScale * pow(1.0 + dot(viewDir, normal), _FresnelPow)));

                // Underwater scattering
                float3 surfNor   = float3(0, 1, 0);
                float3 waterCol  = float3(0, 0.6, 1);
                float3 sunCol    = float3(1, 1, 1);
                float3 groundCol = float3(0.4, 0.3, 0.2);
                float3 topPlane  = waterCol * dot(surfNor, naiveLightDir) * sunCol;
                float3 botPlane  = groundCol * dot(surfNor, naiveLightDir) * exp(-worldPos.y * _ExtinctionCoeff);
                float  foamTurb  = 10 * foamColor.y;
                float3 botPos    = float3(worldPos.x, 0, worldPos.z);
                float3 halfPt    = lerp(botPos, worldPos, 0.5 + foamTurb);
                float3 ambient   = ComputeAmbientColor(halfPt, _ExtinctionCoeff, botPlane, topPlane, 3, 0);

                float3 final = lerp(speccol, ambient, R)
                             + specHL * float3(1, 1, 1)
                             + clamp(_FoamPow * float4(foamColor, 1), 0, 1).xyz * 0.3;

                // mode 0: simple foam+water
                if (_Mode == 0)
                {
                    float3 water = float3(0, 0, 0.5);
                    return float4(d * foamCol * _FoamScale + (1 - d) * water, 1);
                }

                return float4(final, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Diffuse"
}
