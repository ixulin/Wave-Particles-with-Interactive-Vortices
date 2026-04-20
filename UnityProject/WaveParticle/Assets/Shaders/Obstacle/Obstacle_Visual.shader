Shader "Water/Obstacle_Visual"
{
    Properties
    {
        _ObstacleFinal    ("Obstacle Final",  2D) = "black" {}
        _ObstacleScale    ("Obstacle Scale",  Float) = 1.8
        _EdgeTessFactor   ("Edge Tess",       Float) = 4
        _InsideTessFactor ("Inside Tess",     Float) = 4
        _TextureWidth     ("Tex Width",       Float) = 142
        _TextureHeight    ("Tex Height",      Float) = 142
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        Pass
        {
            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex   vert
            #pragma hull     hull
            #pragma domain   domain
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../Include/WaterCommon.hlsl"

            Texture2D    _ObstacleFinal;
            SamplerState sampler_ObstacleFinal;

            float _ObstacleScale;
            float _EdgeTessFactor;
            float _InsideTessFactor;
            float _TextureWidth;
            float _TextureHeight;

            // ---- Vertex ----
            VS_CONTROL_POINT_OUTPUT vert(VS_INPUT v)
            {
                VS_CONTROL_POINT_OUTPUT o;
                o.pos      = v.pos;
                o.texCoord = v.texCoord;
                return o;
            }

            // ---- Hull ----
            HS_CONSTANT_DATA_OUTPUT PatchConstant(
                InputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> ip,
                uint patchID : SV_PrimitiveID)
            {
                HS_CONSTANT_DATA_OUTPUT o;
                o.EdgeTessFactor[0] = o.EdgeTessFactor[1] =
                o.EdgeTessFactor[2] = o.EdgeTessFactor[3] = _EdgeTessFactor;
                o.InsideTessFactor[0] = o.InsideTessFactor[1] = _InsideTessFactor;
                return o;
            }

            [domain("quad")]
            [partitioning("integer")]
            [outputtopology("triangle_ccw")]
            [outputcontrolpoints(NUM_CONTROL_POINTS)]
            [patchconstantfunc("PatchConstant")]
            VS_CONTROL_POINT_OUTPUT hull(
                InputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> ip,
                uint i : SV_OutputControlPointID,
                uint patchID : SV_PrimitiveID)
            {
                return ip[i];
            }

            // ---- Domain ----
            [domain("quad")]
            DS_OUTPUT_2 domain(
                HS_CONSTANT_DATA_OUTPUT hsData,
                float2 uv : SV_DomainLocation,
                const OutputPatch<VS_CONTROL_POINT_OUTPUT, NUM_CONTROL_POINTS> patch)
            {
                DS_OUTPUT_2 o;
                float3 pos = BLERP3(patch[0].pos, patch[1].pos, patch[3].pos, patch[2].pos, uv);
                float2 tc  = BLERP2(patch[0].texCoord, patch[1].texCoord, patch[3].texCoord, patch[2].texCoord, uv);

                float ob = clamp(_ObstacleFinal.SampleLevel(sampler_ObstacleFinal, tc, 0).x, 0, 1);
                pos.y += ob * _ObstacleScale;

                o.nor      = ObstacleMapToNormal(tc, _ObstacleFinal, sampler_ObstacleFinal, _TextureWidth, _TextureHeight);
                float3 wp  = mul(unity_ObjectToWorld, float4(pos, 1)).xyz;
                o.wpos     = wp;
                o.pos      = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.texCoord = tc;
                return o;
            }

            // ---- Fragment ----
            float4 frag(DS_OUTPUT_2 i) : SV_Target
            {
                float3 lightDir    = normalize(float3(1, 1, 1));
                float3 color       = float3(0.80, 0.52, 0.28);
                float  height      = clamp(i.wpos.y, 0, 1);
                float  halfLambert = dot(lightDir, normalize(i.nor)) * 0.5 + 0.5;
                return float4(height * color * halfLambert, 1);
            }
            ENDHLSL
        }
    }
}
