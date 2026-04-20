using UnityEngine;
using UnityEngine.Rendering;

public class WaveParticlePostProcess
{
    readonly SimulationParameters  param;
    readonly WaterSimulationManager mgr;

    readonly Material matH;
    readonly Material matV;
    readonly Mesh     fullscreenQuad;

    static readonly int ID_waveParticleTex = Shader.PropertyToID("_WaveParticleTex");
    static readonly int ID_hFilter1        = Shader.PropertyToID("_HorizontalFilter1");
    static readonly int ID_hFilter2        = Shader.PropertyToID("_HorizontalFilter2");
    static readonly int ID_texWidth        = Shader.PropertyToID("_TextureWidth");
    static readonly int ID_texHeight       = Shader.PropertyToID("_TextureHeight");
    static readonly int ID_blurRadius      = Shader.PropertyToID("_BlurRadius");
    static readonly int ID_dxScale         = Shader.PropertyToID("_DxScale");
    static readonly int ID_dzScale         = Shader.PropertyToID("_DzScale");
    static readonly int ID_mode            = Shader.PropertyToID("_Mode");

    public WaveParticlePostProcess(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr   = mgr;
        matH           = Load("Water/WaveParticle_PostProcess_H");
        matV           = Load("Water/WaveParticle_PostProcess_V");
        fullscreenQuad = BuildFullscreenQuad();
    }

    public void RunHorizontalPass()
    {
        matH.SetTexture(ID_waveParticleTex, mgr.rtWaveParticle);
        matH.SetInt(ID_texWidth,   param.textureWidth);
        matH.SetInt(ID_texHeight,  param.textureHeight);
        matH.SetInt(ID_blurRadius, param.blurRadius);
        matH.SetInt(ID_mode,       param.renderMode);

        // MRT: output to H1 and H2 simultaneously
        BlitMRT(new RenderTargetIdentifier[] { mgr.rtPostProcessH1.colorBuffer, mgr.rtPostProcessH2.colorBuffer },
                mgr.rtPostProcessH1.depthBuffer, matH);
    }

    public void RunVerticalPass()
    {
        matV.SetTexture(ID_hFilter1,   mgr.rtPostProcessH1);
        matV.SetTexture(ID_hFilter2,   mgr.rtPostProcessH2);
        matV.SetInt(ID_texWidth,   param.textureWidth);
        matV.SetInt(ID_texHeight,  param.textureHeight);
        matV.SetInt(ID_blurRadius, param.blurRadius);
        matV.SetFloat(ID_dxScale,  param.dxScale);
        matV.SetFloat(ID_dzScale,  param.dzScale);
        matV.SetInt(ID_mode,       param.renderMode);

        BlitMRT(new RenderTargetIdentifier[] { mgr.rtPostProcessV1.colorBuffer, mgr.rtPostProcessV2.colorBuffer },
                mgr.rtPostProcessV1.depthBuffer, matV);
    }

    void BlitMRT(RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthBuffer, Material mat)
    {
        using var cb = new CommandBuffer { name = "MRT PostProcess" };
        cb.SetRenderTarget(colorBuffers, depthBuffer);
        cb.DrawMesh(fullscreenQuad, Matrix4x4.identity, mat, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    static Mesh BuildFullscreenQuad()
    {
        var m = new Mesh { name = "FullscreenQuad" };
        m.vertices  = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0) };
        m.uv        = new[] { new Vector2(0,0),     new Vector2(1,0),    new Vector2(0,1),    new Vector2(1,1)    };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        return m;
    }

    static Material Load(string name)
    {
        var s = Shader.Find(name);
        if (s == null) Debug.LogError($"Shader not found: {name}");
        return new Material(s);
    }
}
