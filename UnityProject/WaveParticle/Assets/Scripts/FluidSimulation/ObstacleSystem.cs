using UnityEngine;
using UnityEngine.Rendering;

public class ObstacleSystem
{
    readonly SimulationParameters  param;
    readonly WaterSimulationManager mgr;

    readonly Material matCreate;
    readonly Material matBlurH;
    readonly Material matBlurV;
    readonly Mesh     circleMesh;

    static readonly int ID_brushScale    = Shader.PropertyToID("_BrushScale");
    static readonly int ID_brushStrength = Shader.PropertyToID("_BrushStrength");
    static readonly int ID_brushOffsetU  = Shader.PropertyToID("_BrushOffsetU");
    static readonly int ID_brushOffsetV  = Shader.PropertyToID("_BrushOffsetV");
    static readonly int ID_sourceTex     = Shader.PropertyToID("_SourceTex");
    static readonly int ID_texWidth      = Shader.PropertyToID("_TextureWidth");

    public ObstacleSystem(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr   = mgr;
        matCreate = Load("Water/Obstacle_Create");
        matBlurH  = Load("Water/Obstacle_BlurH");
        matBlurV  = Load("Water/Obstacle_BlurV");
        circleMesh = BuildCircleMesh(16);
    }

    public void DrawObstacle(Vector2 brushUV)
    {
        matCreate.SetFloat(ID_brushScale,    param.brushScale);
        matCreate.SetFloat(ID_brushStrength, param.brushStrength);
        matCreate.SetFloat(ID_brushOffsetU,  brushUV.x * 2f - 1f); // UV [0,1] -> NDC [-1,1]
        matCreate.SetFloat(ID_brushOffsetV, -(brushUV.y * 2f - 1f)); // flip Y: DX RenderTexture is Y-down

        using var cb = new CommandBuffer { name = "Draw Obstacle" };
        cb.SetRenderTarget(mgr.rtObstacleCreate);
        // Additive blend: don't clear, accumulate strokes
        cb.DrawMesh(circleMesh, Matrix4x4.identity, matCreate, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    public void RunBlurPass()
    {
        matBlurH.SetTexture(ID_sourceTex, mgr.rtObstacleCreate);
        matBlurH.SetFloat(ID_texWidth, param.textureWidthFluid);
        Graphics.Blit(null, mgr.rtObstacleBlur, matBlurH);

        matBlurV.SetTexture(ID_sourceTex, mgr.rtObstacleBlur);
        matBlurV.SetFloat(ID_texWidth, param.textureHeightFluid);
        Graphics.Blit(null, mgr.rtObstacleFinal, matBlurV);
    }

    public void ClearObstacles()
    {
        using var cb = new CommandBuffer { name = "Clear Obstacles" };
        cb.SetRenderTarget(mgr.rtObstacleCreate);
        cb.ClearRenderTarget(false, true, Color.clear);
        Graphics.ExecuteCommandBuffer(cb);
    }

    static Mesh BuildCircleMesh(int segments)
    {
        var verts   = new Vector3[segments + 1];
        var tris    = new int[segments * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = 2f * Mathf.PI * i / segments;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }
        var m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        return m;
    }

    static Material Load(string name)
    {
        var s = Shader.Find(name);
        if (s == null) Debug.LogError($"Shader not found: {name}");
        return new Material(s);
    }
}
