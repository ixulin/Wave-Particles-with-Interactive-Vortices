using UnityEngine;
using UnityEngine.Rendering;

public class FluidSimulator
{
    readonly SimulationParameters param;
    readonly WaterSimulationManager mgr;

    readonly Mesh fullscreenQuad;
    readonly Material matAdvect;
    readonly Material matSplatVorticity;
    readonly Material matSplatDensity;
    readonly Material matSplatImpulse;
    readonly Material matInjectWave;
    readonly Material matDivergence;
    readonly Material matJacobi;
    readonly Material matSubtractGradient;

    static readonly int ID_obstacleTex = Shader.PropertyToID("_ObstacleTex");
    static readonly int ID_velocityTex = Shader.PropertyToID("_VelocityTex");
    static readonly int ID_srcTex = Shader.PropertyToID("_SrcTex");
    static readonly int ID_pressureTex = Shader.PropertyToID("_PressureTex");
    static readonly int ID_divergenceTex = Shader.PropertyToID("_DivergenceTex");
    static readonly int ID_densityTex = Shader.PropertyToID("_DensityTex");
    static readonly int ID_timeStepFluid = Shader.PropertyToID("_TimeStepFluid");
    static readonly int ID_fluidCellSize = Shader.PropertyToID("_FluidCellSize");
    static readonly int ID_fluidDissipation = Shader.PropertyToID("_FluidDissipation");
    static readonly int ID_vorticityScale = Shader.PropertyToID("_VorticityScale");
    static readonly int ID_texWidthFluid = Shader.PropertyToID("_TextureWidthFluid");
    static readonly int ID_texHeightFluid = Shader.PropertyToID("_TextureHeightFluid");
    static readonly int ID_obstThreshFluid = Shader.PropertyToID("_ObstacleThresholdFluid");
    static readonly int ID_splatDirU = Shader.PropertyToID("_SplatDirU");
    static readonly int ID_splatDirV = Shader.PropertyToID("_SplatDirV");
    static readonly int ID_splatScale = Shader.PropertyToID("_SplatScale");
    static readonly int ID_splatDensityU = Shader.PropertyToID("_SplatDensityU");
    static readonly int ID_splatDensityV = Shader.PropertyToID("_SplatDensityV");
    static readonly int ID_splatDensityRadius = Shader.PropertyToID("_SplatDensityRadius");
    static readonly int ID_splatDensityScale = Shader.PropertyToID("_SplatDensityScale");
    static readonly int ID_impulseCenterU = Shader.PropertyToID("_ImpulseCenterU");
    static readonly int ID_impulseCenterV = Shader.PropertyToID("_ImpulseCenterV");
    static readonly int ID_impulseRadius = Shader.PropertyToID("_ImpulseRadius");
    static readonly int ID_impulseU = Shader.PropertyToID("_ImpulseU");
    static readonly int ID_impulseV = Shader.PropertyToID("_ImpulseV");
    static readonly int ID_impulseStrength = Shader.PropertyToID("_ImpulseStrength");
    static readonly int ID_waveParticleTex = Shader.PropertyToID("_WaveParticleTex");
    static readonly int ID_waveInjectStrength = Shader.PropertyToID("_WaveInjectStrength");

    bool pendingImpulse;
    Vector2 pendingImpulseCenter;
    Vector2 pendingImpulseVelUV;

    public FluidSimulator(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr = mgr;
        fullscreenQuad = BuildFullscreenQuad();
        matAdvect = Load("Water/Fluid_Advect");
        matSplatVorticity = Load("Water/Fluid_SplatVorticity");
        matSplatDensity = Load("Water/Fluid_SplatDensity");
        matSplatImpulse = Load("Water/Fluid_SplatVelocityImpulse");
        matInjectWave = Load("Water/Fluid_InjectWaveVelocity");
        matDivergence = Load("Water/Fluid_Divergence");
        matJacobi = Load("Water/Fluid_Jacobi");
        matSubtractGradient = Load("Water/Fluid_SubtractGradient");
    }

    public void RunFullPipeline()
    {
        SetGlobalUniforms();

        RunAdvect(mgr.rtVelocity);
        RunAdvect(mgr.rtDensity);
        RunSplatVorticity();
        RunInjectWaveVelocity();
        if (pendingImpulse)
        {
            RunSplatVelocityImpulse();
            pendingImpulse = false;
        }
        RunSplatDensity();
        RunDivergence();
        RunJacobi();
        RunSubtractGradient();
    }

    void SetGlobalUniforms()
    {
        Shader.SetGlobalFloat(ID_timeStepFluid, param.timeStepFluid);
        Shader.SetGlobalFloat(ID_fluidCellSize, param.fluidCellSize);
        Shader.SetGlobalFloat(ID_fluidDissipation, param.fluidDissipation);
        Shader.SetGlobalFloat(ID_vorticityScale, param.vorticityScale);
        Shader.SetGlobalInt(ID_texWidthFluid, param.textureWidthFluid);
        Shader.SetGlobalInt(ID_texHeightFluid, param.textureHeightFluid);
        Shader.SetGlobalFloat(ID_obstThreshFluid, param.obstacleThresholdFluid);
    }

    void RunAdvect(PingPongRT target)
    {
        matAdvect.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matAdvect.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        matAdvect.SetTexture(ID_srcTex, target.Current);
        Blit(target.Next, matAdvect);
        target.Swap();
    }

    void RunSplatVorticity()
    {
        matSplatVorticity.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matSplatVorticity.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        matSplatVorticity.SetFloat(ID_splatDirU, param.splatDirU);
        matSplatVorticity.SetFloat(ID_splatDirV, param.splatDirV);
        matSplatVorticity.SetFloat(ID_splatScale, param.splatScale);
        Blit(mgr.rtVelocity.Next, matSplatVorticity);
        mgr.rtVelocity.Swap();
    }

    void RunInjectWaveVelocity()
    {
        if (mgr.rtWaveParticle == null) return;
        matInjectWave.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matInjectWave.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        matInjectWave.SetTexture(ID_waveParticleTex, mgr.rtWaveParticle);
        matInjectWave.SetFloat(ID_waveInjectStrength, param.waveInjectStrength);
        Blit(mgr.rtVelocity.Next, matInjectWave);
        mgr.rtVelocity.Swap();
    }

    void RunSplatDensity()
    {
        matSplatDensity.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matSplatDensity.SetTexture(ID_densityTex, mgr.rtDensity.Current);
        matSplatDensity.SetFloat(ID_splatDensityU, param.splatDensityU);
        matSplatDensity.SetFloat(ID_splatDensityV, param.splatDensityV);
        matSplatDensity.SetFloat(ID_splatDensityRadius, param.splatDensityRadius);
        matSplatDensity.SetFloat(ID_splatDensityScale, param.splatDensityScale);
        Blit(mgr.rtDensity.Next, matSplatDensity);
        mgr.rtDensity.Swap();
    }

    public void QueueVelocityImpulse(Vector2 centerUV, Vector2 velUV)
    {
        pendingImpulse = true;
        pendingImpulseCenter = centerUV;
        pendingImpulseVelUV = velUV;
    }

    void RunSplatVelocityImpulse()
    {
        matSplatImpulse.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matSplatImpulse.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        matSplatImpulse.SetFloat(ID_impulseCenterU, pendingImpulseCenter.x);
        matSplatImpulse.SetFloat(ID_impulseCenterV, pendingImpulseCenter.y);
        matSplatImpulse.SetFloat(ID_impulseRadius, param.velocityImpulseRadius);
        matSplatImpulse.SetFloat(ID_impulseU, pendingImpulseVelUV.x);
        matSplatImpulse.SetFloat(ID_impulseV, pendingImpulseVelUV.y);
        matSplatImpulse.SetFloat(ID_impulseStrength, param.velocityImpulseStrength);
        Blit(mgr.rtVelocity.Next, matSplatImpulse);
        mgr.rtVelocity.Swap();
    }

    void RunDivergence()
    {
        matDivergence.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matDivergence.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        Blit(mgr.rtDivergence, matDivergence);
    }

    void RunJacobi()
    {
        matJacobi.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matJacobi.SetTexture(ID_divergenceTex, mgr.rtDivergence);
        for (int i = 0; i < param.jacobiIterations; i++)
        {
            matJacobi.SetTexture(ID_pressureTex, mgr.rtPressure.Current);
            Blit(mgr.rtPressure.Next, matJacobi);
            mgr.rtPressure.Swap();
        }
    }

    void RunSubtractGradient()
    {
        matSubtractGradient.SetTexture(ID_obstacleTex, mgr.rtObstacleFinal);
        matSubtractGradient.SetTexture(ID_pressureTex, mgr.rtPressure.Current);
        matSubtractGradient.SetTexture(ID_velocityTex, mgr.rtVelocity.Current);
        Blit(mgr.rtVelocity.Next, matSubtractGradient);
        mgr.rtVelocity.Swap();
    }

    void Blit(RenderTexture dst, Material mat)
    {
        using var cb = new CommandBuffer { name = "FluidBlit" };
        cb.SetRenderTarget(dst);
        cb.SetViewport(new Rect(0, 0, dst.width, dst.height));
        cb.DrawMesh(fullscreenQuad, Matrix4x4.identity, mat, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    static Mesh BuildFullscreenQuad()
    {
        var m = new Mesh { name = "FullscreenQuad" };
        m.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    static Material Load(string shaderName)
    {
        var s = Shader.Find(shaderName);
        if (s == null) Debug.LogError($"Shader not found: {shaderName}");
        return new Material(s);
    }
}
