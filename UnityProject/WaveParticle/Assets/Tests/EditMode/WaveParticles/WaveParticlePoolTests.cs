using NUnit.Framework;
using UnityEngine;

public class WaveParticlePoolTests
{
    [Test]
    public void ResetAmbient_FillsConfiguredAmbientSlots()
    {
        var pool = new WaveParticlePool(capacity: 16);
        pool.ResetAmbient(
            ambientCount: 4,
            amplitudeMin: 0.1f,
            amplitudeMax: 0.2f,
            speedMin: 0.3f,
            speedMax: 0.6f,
            random: new System.Random(0));

        Assert.AreEqual(4, pool.ActiveCount);
        Assert.AreEqual(4, pool.AmbientCount);
        Assert.AreEqual(0, pool.EventCount);
    }

    [Test]
    public void SpawnEventRing_ActivatesRequestedParticlesWithLife()
    {
        var pool = new WaveParticlePool(capacity: 32);
        pool.SpawnEventRing(
            center: new Vector2(0.25f, -0.5f),
            particleCount: 8,
            speed: 1.2f,
            amplitude: 0.4f,
            life: 1.5f);

        Assert.AreEqual(8, pool.ActiveCount);
        Assert.AreEqual(8, pool.EventCount);
    }

    [Test]
    public void Step_ExpiresEventParticlesWhenLifeRunsOut()
    {
        var pool = new WaveParticlePool(capacity: 16);
        pool.SpawnEventRing(Vector2.zero, 4, 1f, 0.3f, 0.1f);

        pool.Step(
            dt: 0.2f,
            fluidParticleStrength: 0f,
            eventAmplitudeDamping: 1f,
            sampleVelocity: _ => Vector2.zero);

        Assert.AreEqual(0, pool.EventCount);
        Assert.AreEqual(0, pool.ActiveCount);
    }

    [Test]
    public void Step_KeepsAmbientParticlesAlive()
    {
        var pool = new WaveParticlePool(capacity: 16);
        pool.ResetAmbient(4, 0.1f, 0.2f, 0.3f, 0.6f, new System.Random(0));

        pool.Step(10f, 0f, 1f, _ => Vector2.zero);

        Assert.AreEqual(4, pool.ActiveCount);
        Assert.AreEqual(4, pool.AmbientCount);
    }

    [Test]
    public void BuildRenderData_EncodesPosAmplitudeDirectionAndSpeed()
    {
        var pool = new WaveParticlePool(capacity: 4);
        pool.SpawnEventRing(Vector2.zero, 1, 2f, 0.5f, 1f);

        var vertices = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var indices = new System.Collections.Generic.List<int>();

        pool.BuildRenderData(vertices, uvs, normals, indices);

        Assert.AreEqual(1, vertices.Count);
        Assert.AreEqual(1, uvs.Count);
        Assert.AreEqual(1, normals.Count);
        Assert.AreEqual(1, indices.Count);
        Assert.That(vertices[0].z, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(normals[0].z, Is.EqualTo(2f).Within(0.001f));
    }
}
