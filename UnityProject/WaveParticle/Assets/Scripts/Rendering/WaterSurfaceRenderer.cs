using UnityEngine;

public class WaterSurfaceRenderer
{
    SimulationParameters param;
    readonly WaterSimulationManager mgr;
    readonly Material mat;
    readonly Texture2D foamTex;
    readonly Texture2D flowmapTex;

    Material obstacleVisualMat;

    static readonly int ID_albedo = Shader.PropertyToID("_AlbedoTex");
    static readonly int ID_flowmap = Shader.PropertyToID("_FlowmapTex");
    static readonly int ID_deviation = Shader.PropertyToID("_DeviationTex");
    static readonly int ID_gradient = Shader.PropertyToID("_GradientTex");
    static readonly int ID_density = Shader.PropertyToID("_DensityTex");
    static readonly int ID_pressure = Shader.PropertyToID("_PressureTex");
    static readonly int ID_divergence = Shader.PropertyToID("_DivergenceTex");
    static readonly int ID_obstacle = Shader.PropertyToID("_ObstacleTex");
    static readonly int ID_velocity = Shader.PropertyToID("_VelocityTex");
    static readonly int ID_time = Shader.PropertyToID("_Time_Custom");
    static readonly int ID_heightScale = Shader.PropertyToID("_HeightScale");
    static readonly int ID_flowSpeed = Shader.PropertyToID("_FlowSpeed");
    static readonly int ID_timeScale = Shader.PropertyToID("_TimeScale");
    static readonly int ID_edgeTess = Shader.PropertyToID("_EdgeTessFactor");
    static readonly int ID_insideTess = Shader.PropertyToID("_InsideTessFactor");
    static readonly int ID_mode = Shader.PropertyToID("_Mode");
    static readonly int ID_texW = Shader.PropertyToID("_TextureWidth");
    static readonly int ID_texH = Shader.PropertyToID("_TextureHeight");
    static readonly int ID_lightH = Shader.PropertyToID("_LightHeight");
    static readonly int ID_extinc = Shader.PropertyToID("_ExtinctionCoeff");
    static readonly int ID_shini = Shader.PropertyToID("_Shininess");
    static readonly int ID_fBias = Shader.PropertyToID("_FresnelBias");
    static readonly int ID_fPow = Shader.PropertyToID("_FresnelPow");
    static readonly int ID_fScale = Shader.PropertyToID("_FresnelScale");
    static readonly int ID_foamScale = Shader.PropertyToID("_FoamScale");
    static readonly int ID_foamPow = Shader.PropertyToID("_FoamPow");
    static readonly int ID_obstThreshW = Shader.PropertyToID("_ObstacleThresholdWave");
    static readonly int ID_dxScale = Shader.PropertyToID("_DxScale");
    static readonly int ID_dzScale = Shader.PropertyToID("_DzScale");
    static readonly int ID_fluidHeightScale = Shader.PropertyToID("_FluidHeightScale");

    public WaterSurfaceRenderer(SimulationParameters param, WaterSimulationManager mgr,
                                 Texture2D foamTex, Texture2D flowmapTex, Material mat)
    {
        this.param = param;
        this.mgr = mgr;
        this.mat = mat;
        this.foamTex = foamTex;
        this.flowmapTex = flowmapTex;

        if (foamTex != null) mat.SetTexture(ID_albedo, foamTex);
        if (flowmapTex != null) mat.SetTexture(ID_flowmap, flowmapTex);

        CreateObstacleVisual();
    }

    void CreateObstacleVisual()
    {
        var shader = Shader.Find("Water/Obstacle_Visual");
        if (shader == null) { Debug.LogError("Shader 'Water/Obstacle_Visual' not found"); return; }

        var obsGO = new GameObject("WaterObstacleVisual");
        obsGO.transform.SetParent(mgr.transform, false);
        // Offset below water so flat areas are occluded by water via depth test;
        // only obstacle "bumps" (raised by displacement) protrude above water.
        obsGO.transform.localPosition = new Vector3(0f, -0.1f, 0f);

        var mf = obsGO.AddComponent<MeshFilter>();
        var waterMF = mgr.GetComponent<MeshFilter>();
        if (waterMF != null) mf.sharedMesh = waterMF.sharedMesh;

        obstacleVisualMat = new Material(shader);
        obsGO.AddComponent<MeshRenderer>().sharedMaterial = obstacleVisualMat;
    }

    public void UpdateMaterialProperties(int frameTime)
    {
        if (mat == null) return;

        // Computed textures
        mat.SetTexture(ID_deviation, mgr.rtPostProcessV1);
        mat.SetTexture(ID_gradient, mgr.rtPostProcessV2);
        mat.SetTexture(ID_density, mgr.rtDensity.Current);
        mat.SetTexture(ID_pressure, mgr.rtPressure.Current);
        mat.SetTexture(ID_divergence, mgr.rtDivergence);
        mat.SetTexture(ID_obstacle, mgr.rtObstacleFinal);
        mat.SetTexture(ID_velocity, mgr.rtVelocity.Current);

        // Frame / simulation params
        mat.SetInt(ID_time, frameTime);
        mat.SetFloat(ID_heightScale, param.heightScale);
        mat.SetFloat(ID_flowSpeed, param.flowSpeed);
        mat.SetFloat(ID_timeScale, param.timeScale);
        mat.SetFloat(ID_dxScale, param.dxScale);
        mat.SetFloat(ID_dzScale, param.dzScale);
        mat.SetInt(ID_edgeTess, param.edgeTessFactor);
        mat.SetInt(ID_insideTess, param.insideTessFactor);
        mat.SetInt(ID_mode, param.renderMode);
        mat.SetFloat(ID_texW, param.textureWidth);
        mat.SetFloat(ID_texH, param.textureHeight);

        // Shading
        mat.SetFloat(ID_lightH, param.lightHeight);
        mat.SetFloat(ID_extinc, param.extinctionCoeff);
        mat.SetFloat(ID_shini, param.shininess);
        mat.SetFloat(ID_fBias, param.fresnelBias);
        mat.SetFloat(ID_fPow, param.fresnelPow);
        mat.SetFloat(ID_fScale, param.fresnelScale);
        mat.SetFloat(ID_foamScale, param.foamScale);
        mat.SetFloat(ID_foamPow, param.foamPow);
        mat.SetFloat(ID_obstThreshW, param.obstacleThresholdWave);
        mat.SetFloat(ID_fluidHeightScale, param.fluidHeightScale);

        // Obstacle visual mesh
        if (obstacleVisualMat != null)
        {
            obstacleVisualMat.SetTexture("_ObstacleFinal", mgr.rtObstacleFinal);
            obstacleVisualMat.SetFloat("_ObstacleScale", param.obstacleScale);
            obstacleVisualMat.SetFloat("_EdgeTessFactor", param.edgeTessFactor);
            obstacleVisualMat.SetFloat("_InsideTessFactor", param.insideTessFactor);
            obstacleVisualMat.SetFloat("_TextureWidth", param.textureWidthFluid);
            obstacleVisualMat.SetFloat("_TextureHeight", param.textureHeightFluid);
        }
    }
}
