using UnityEngine;
using UnityEngine.Rendering;

public class ObstacleSystem
{
    readonly SimulationParameters   param;
    readonly WaterSimulationManager mgr;

    readonly Material matCreate;
    readonly Material matBlurH;
    readonly Material matBlurV;
    readonly Mesh     fullscreenQuad;

    static readonly int ID_brushRadius   = Shader.PropertyToID("_BrushRadius");
    static readonly int ID_brushStrength = Shader.PropertyToID("_BrushStrength");
    static readonly int ID_brushCenterU  = Shader.PropertyToID("_BrushCenterU");
    static readonly int ID_brushCenterV  = Shader.PropertyToID("_BrushCenterV");
    static readonly int ID_texWidth      = Shader.PropertyToID("_TexWidth");
    static readonly int ID_texHeight     = Shader.PropertyToID("_TexHeight");
    static readonly int ID_sourceTex     = Shader.PropertyToID("_SourceTex");
    static readonly int ID_blurWidth     = Shader.PropertyToID("_TextureWidth");
    static readonly int ID_blurHeight    = Shader.PropertyToID("_TextureHeight");

    public ObstacleSystem(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr   = mgr;
        matCreate      = Load("Water/Obstacle_Create");
        matBlurH       = Load("Water/Obstacle_BlurH");
        matBlurV       = Load("Water/Obstacle_BlurV");
        fullscreenQuad = BuildFullscreenQuad();
    }

    // Draws a circular brush stroke into rtObstacleCreate (additive accumulation).
    // Uses CommandBuffer + explicit viewport so the full RT is covered.
    public void DrawObstacle(Vector2 brushUV)
    {
        int w = param.textureWidthFluid;
        int h = param.textureHeightFluid;

        matCreate.SetFloat(ID_brushRadius,   param.brushScale * 0.5f);
        matCreate.SetFloat(ID_brushStrength, param.brushStrength);
        matCreate.SetFloat(ID_brushCenterU,  brushUV.x);
        matCreate.SetFloat(ID_brushCenterV,  brushUV.y);
        matCreate.SetFloat(ID_texWidth,      w);
        matCreate.SetFloat(ID_texHeight,     h);

        using var cb = new CommandBuffer { name = "Draw Obstacle" };
        cb.SetRenderTarget(mgr.rtObstacleCreate);
        cb.SetViewport(new Rect(0, 0, w, h));
        cb.DrawMesh(fullscreenQuad, Matrix4x4.identity, matCreate, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    // Gaussian blur: Create -> Blur -> Final (two-pass separable).
    // Uses CommandBuffer + explicit viewport to avoid the Graphics.Blit viewport issue.
    public void RunBlurPass()
    {
        int w = param.textureWidthFluid;
        int h = param.textureHeightFluid;

        using var cb = new CommandBuffer { name = "Blur Obstacle" };

        // Horizontal pass: ObstacleCreate -> ObstacleBlur
        matBlurH.SetTexture(ID_sourceTex, mgr.rtObstacleCreate);
        matBlurH.SetFloat(ID_blurWidth,  w);
        matBlurH.SetFloat(ID_blurHeight, h);
        cb.SetRenderTarget(mgr.rtObstacleBlur);
        cb.SetViewport(new Rect(0, 0, w, h));
        cb.DrawMesh(fullscreenQuad, Matrix4x4.identity, matBlurH, 0, 0);

        // Vertical pass: ObstacleBlur -> ObstacleFinal
        matBlurV.SetTexture(ID_sourceTex, mgr.rtObstacleBlur);
        matBlurV.SetFloat(ID_blurWidth,  w);
        matBlurV.SetFloat(ID_blurHeight, h);
        cb.SetRenderTarget(mgr.rtObstacleFinal);
        cb.SetViewport(new Rect(0, 0, w, h));
        cb.DrawMesh(fullscreenQuad, Matrix4x4.identity, matBlurV, 0, 0);

        Graphics.ExecuteCommandBuffer(cb);
    }

    public void ClearObstacles()
    {
        using var cb = new CommandBuffer { name = "Clear Obstacles" };
        cb.SetRenderTarget(mgr.rtObstacleCreate);
        cb.ClearRenderTarget(false, true, Color.clear);
        Graphics.ExecuteCommandBuffer(cb);
    }

    static Mesh BuildFullscreenQuad()
    {
        var m = new Mesh { name = "FullscreenQuad" };
        m.vertices  = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    static Material Load(string name)
    {
        var s = Shader.Find(name);
        if (s == null) Debug.LogError($"Shader not found: {name}");
        return new Material(s);
    }
}
