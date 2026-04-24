using System;
using UnityEngine;

public class WaveParticlePool
{
    readonly WaveParticle[] particles;

    public int ActiveCount { get; private set; }
    public int AmbientCount { get; private set; }
    public int EventCount { get; private set; }

    public WaveParticlePool(int capacity)
    {
        particles = new WaveParticle[capacity];
    }

    public void ResetAmbient(int ambientCount, float amplitudeMin, float amplitudeMax, float speedMin, float speedMax, System.Random random)
    {
        Array.Clear(particles, 0, particles.Length);
        ActiveCount = 0;
        AmbientCount = 0;
        EventCount = 0;

        for (int i = 0; i < ambientCount && i < particles.Length; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            particles[i] = new WaveParticle
            {
                active = true,
                layerType = 0,
                pos = new Vector2(
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    (float)(random.NextDouble() * 2.0 - 1.0)),
                dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                speed = Mathf.Lerp(speedMin, speedMax, (float)random.NextDouble()),
                amplitude = Mathf.Lerp(amplitudeMin, amplitudeMax, (float)random.NextDouble()),
                life = float.PositiveInfinity,
                maxLife = float.PositiveInfinity
            };
            ActiveCount++;
            AmbientCount++;
        }
    }

    public void SpawnEventRing(Vector2 center, int particleCount, float speed, float amplitude, float life)
    {
        for (int i = 0; i < particleCount; i++)
        {
            int slot = FindInactiveSlot();
            if (slot < 0)
                return;

            float angle = (Mathf.PI * 2f * i) / particleCount;
            particles[slot] = new WaveParticle
            {
                active = true,
                layerType = 1,
                pos = center,
                dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                speed = speed,
                amplitude = amplitude,
                life = life,
                maxLife = life
            };
            ActiveCount++;
            EventCount++;
        }
    }

    public void Step(float dt, float fluidParticleStrength, float eventAmplitudeDamping, Func<Vector2, Vector2> sampleVelocity)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].active)
                continue;

            var p = particles[i];
            Vector2 baseVel = p.dir * p.speed;
            Vector2 fluidVel = sampleVelocity != null ? sampleVelocity(ToUV(p.pos)) : Vector2.zero;
            p.pos += (baseVel + fluidVel * fluidParticleStrength) * dt;

            if (p.layerType == 0)
            {
                p.pos = Wrap(p.pos);
            }
            else
            {
                p.life -= dt;
                p.amplitude *= eventAmplitudeDamping;
                if (p.life <= 0f)
                {
                    p.active = false;
                    particles[i] = p;
                    ActiveCount--;
                    EventCount--;
                    continue;
                }
            }

            particles[i] = p;
        }
    }

    public void BuildRenderData(
        System.Collections.Generic.List<Vector3> vertices,
        System.Collections.Generic.List<Vector2> uvs,
        System.Collections.Generic.List<Vector3> normals,
        System.Collections.Generic.List<int> indices)
    {
        vertices.Clear();
        uvs.Clear();
        normals.Clear();
        indices.Clear();

        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].active)
                continue;

            var p = particles[i];
            vertices.Add(new Vector3(p.pos.x, p.pos.y, p.amplitude));
            uvs.Add(p.dir);
            normals.Add(new Vector3(0f, 0f, p.speed));
            indices.Add(indices.Count);
        }
    }

    int FindInactiveSlot()
    {
        for (int i = 0; i < particles.Length; i++)
            if (!particles[i].active)
                return i;
        return -1;
    }

    static Vector2 Wrap(Vector2 p)
    {
        if (p.x > 1f) p.x -= 2f;
        if (p.x < -1f) p.x += 2f;
        if (p.y > 1f) p.y -= 2f;
        if (p.y < -1f) p.y += 2f;
        return p;
    }

    static Vector2 ToUV(Vector2 p) => p * 0.5f + Vector2.one * 0.5f;
}
