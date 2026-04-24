using UnityEngine;
using UnityEngine.Rendering;

public class WaveParticleSystem
{
    readonly SimulationParameters  param;
    readonly WaterSimulationManager mgr;

    readonly Mesh particleMesh;
    readonly Material particleMaterial;
    readonly WaveParticlePool particlePool;
    readonly WaveVelocityCache velocityCache;
    readonly System.Collections.Generic.List<Vector3> vertices = new();
    readonly System.Collections.Generic.List<Vector2> uvs = new();
    readonly System.Collections.Generic.List<Vector3> normals = new();
    readonly System.Collections.Generic.List<int> indices = new();
    readonly System.Random random = new(0);
    int readbackFrame;

    static readonly int ID_heightScale = Shader.PropertyToID("_HeightScale");

    public WaveParticleSystem(SimulationParameters param, WaterSimulationManager mgr)
    {
        this.param = param;
        this.mgr = mgr;
        particleMesh = new Mesh { name = "WaveParticlesDynamic" };
        particleMesh.indexFormat = IndexFormat.UInt32;
        particleMaterial = Load("Water/WaveParticle_Rasterize");
        particlePool = new WaveParticlePool(8192);
        velocityCache = new WaveVelocityCache();
        particlePool.ResetAmbient(
            param.ambientParticleCount,
            param.ambientParticleAmplitudeMin,
            param.ambientParticleAmplitudeMax,
            param.ambientParticleSpeedMin,
            param.ambientParticleSpeedMax,
            random);
    }

    public void Step(float dt)
    {
        readbackFrame++;
        if (readbackFrame % Mathf.Max(1, param.velocityReadbackInterval) == 0)
            velocityCache.TrySchedule(mgr.rtVelocity.Current);

        particlePool.Step(
            dt,
            param.fluidParticleStrength,
            param.eventAmplitudeDamping,
            velocityCache.Sample);
    }

    public void SpawnEventRing(Vector2 center)
    {
        particlePool.SpawnEventRing(
            center,
            param.eventParticlesPerSpawn,
            param.eventParticleSpeed,
            param.eventParticleAmplitude,
            param.eventParticleLife);
    }

    public void Rasterize()
    {
        particlePool.BuildRenderData(vertices, uvs, normals, indices);
        particleMesh.Clear();
        particleMesh.SetVertices(vertices);
        particleMesh.SetUVs(0, uvs);
        particleMesh.SetNormals(normals);
        particleMesh.SetIndices(indices, MeshTopology.Points, 0);

        particleMaterial.SetFloat(ID_heightScale, param.heightScale);

        using var cb = new CommandBuffer { name = "WaveParticle Rasterize" };
        cb.SetRenderTarget(mgr.rtWaveParticle);
        cb.ClearRenderTarget(false, true, Color.clear);
        cb.SetViewport(new Rect(0, 0, param.textureWidth, param.textureHeight));
        cb.DrawMesh(particleMesh, Matrix4x4.identity, particleMaterial, 0, 0);
        Graphics.ExecuteCommandBuffer(cb);
    }

    static Material Load(string name)
    {
        var s = Shader.Find(name);
        if (s == null) Debug.LogError($"Shader not found: {name}");
        return new Material(s);
    }
}
