using UnityEngine;
using UnityEngine.Rendering;

public class WaveParticleSystem
{
    readonly SimulationParameters  param;
    readonly WaterSimulationManager mgr;

    readonly Mesh     particleMesh;
    readonly Material particleMaterial;

    static readonly int ID_heightScale            = Shader.PropertyToID("_HeightScale");
    static readonly int ID_waveParticleSpeedScale = Shader.PropertyToID("_WaveParticleSpeedScale");
    static readonly int ID_timeScale              = Shader.PropertyToID("_TimeScale");
    static readonly int ID_time                   = Shader.PropertyToID("_Time_Custom");
    static readonly int ID_velocityTex            = Shader.PropertyToID("_VelocityTex");
    static readonly int ID_fluidParticleStrength  = Shader.PropertyToID("_FluidParticleStrength");

    public WaveParticleSystem(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr   = mgr;
        particleMesh     = BuildParticleMesh(6000);
        particleMaterial = Load("Water/WaveParticle_Rasterize");
    }

    public void Rasterize(int frameTime)
    {
        particleMaterial.SetFloat(ID_heightScale,            param.heightScale);
        particleMaterial.SetFloat(ID_waveParticleSpeedScale, param.waveParticleSpeedScale);
        particleMaterial.SetFloat(ID_timeScale,              param.timeScale);
        particleMaterial.SetInt(ID_time,                     frameTime);
        particleMaterial.SetTexture(ID_velocityTex,          mgr.rtVelocity.Current);
        particleMaterial.SetFloat(ID_fluidParticleStrength,  param.fluidParticleStrength);

        using var cb = new CommandBuffer { name = "WaveParticle Rasterize" };
        cb.SetRenderTarget(mgr.rtWaveParticle);
        cb.ClearRenderTarget(false, true, Color.clear);
        cb.SetViewport(new Rect(0, 0, param.textureWidth, param.textureHeight));
        cb.DrawMesh(particleMesh, Matrix4x4.identity, particleMaterial, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    // Vertices encode wave particle data:
    //   pos.xy   = initial position [-1, 1]
    //   pos.z    = amplitude (height)
    //   uv       = direction (normalized)
    //   normal.z = speed
    static Mesh BuildParticleMesh(int count)
    {
        var vertices = new Vector3[count];
        var uvs      = new Vector2[count];
        var normals  = new Vector3[count];
        var indices  = new int[count];
        var rng      = new System.Random(0);

        for (int i = 0; i < count; i++)
        {
            float px  = (float)(rng.NextDouble() * 2.0 - 1.0);
            float py  = (float)(rng.NextDouble() * 2.0 - 1.0);
            float amp = (float)(rng.NextDouble() * 0.1 + 0.2);
            float dx  = (float)(rng.NextDouble() * 2.0 - 1.0);
            float dy  = (float)(rng.NextDouble() * 2.0 - 1.0);
            float spd = (float)rng.NextDouble();

            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len > 0f) { dx /= len; dy /= len; }

            vertices[i] = new Vector3(px, py, amp);
            uvs[i]      = new Vector2(dx, dy);
            normals[i]  = new Vector3(0f, 0f, spd);
            indices[i]  = i;
        }

        var mesh = new Mesh { name = "WaveParticles" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = vertices;
        mesh.uv          = uvs;
        mesh.normals     = normals;
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        return mesh;
    }

    static Material Load(string name)
    {
        var s = Shader.Find(name);
        if (s == null) Debug.LogError($"Shader not found: {name}");
        return new Material(s);
    }
}
