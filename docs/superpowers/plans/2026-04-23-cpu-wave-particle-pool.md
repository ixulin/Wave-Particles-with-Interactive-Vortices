# CPU Wave Particle Pool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将当前基于初始随机顶点的伪 wave particle 实现，重构为 CPU 粒子池 + 固定容量动态 Mesh，并保留单向 `fluid -> wave particle` 耦合。

**Architecture:** 以 `WaveParticlePool` 作为纯 C# 状态核心，管理底噪层与事件层粒子的生成、推进、死亡与回收；`WaveVelocityCache` 负责异步缓存 `rtVelocity` 到 CPU 侧；`WaveParticleSystem` 只负责驱动池子更新、构建动态 Mesh，并将粒子当前状态 rasterize 到既有的 `rtWaveParticle`。保留既有 `WaveParticle_PostProcess_H/V.shader` 与 `_DeviationTex` 渲染链，关闭 `wave -> fluid` 的反向注入。

**Tech Stack:** Unity Built-in Render Pipeline、C#、CommandBuffer、MeshTopology.Points、AsyncGPUReadback、Unity EditMode Tests、ShaderLab/HLSL

---

## File Structure

### New files
- `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticle.cs` — 单个粒子的纯数据结构
- `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticlePool.cs` — 固定容量粒子池；负责初始化、事件生成、推进、衰减、回收、构建渲染数据
- `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveVelocityCache.cs` — `rtVelocity` 的异步读回与 CPU 侧双线性采样
- `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticles.EditMode.asmdef` — EditMode 测试程序集
- `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs` — 粒子池行为测试
- `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveVelocityCacheTests.cs` — 速度缓存采样测试

### Modified files
- `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticleSystem.cs` — 改为使用 `WaveParticlePool` + `WaveVelocityCache` + 动态 Mesh
- `UnityProject/WaveParticle/Assets/Scripts/Core/WaterSimulationManager.cs` — 将 Shift+LMB 拖拽同时作为事件粒子生成源，并在 wave system 上调用 spawn/step
- `UnityProject/WaveParticle/Assets/Scripts/Core/SimulationParameters.cs` — 新增 ambient/event/readback 参数；移除或废弃当前只适用于旧随机实现的字段
- `UnityProject/WaveParticle/Assets/WaterParams.asset` — 配置新参数默认值
- `UnityProject/WaveParticle/Assets/Shaders/WaveParticles/WaveParticle_Rasterize.shader` — 删除“根据初始状态推进粒子”的逻辑，退化为纯输出 shader
- `UnityProject/WaveParticle/Assets/Scripts/FluidSimulation/FluidSimulator.cs` — 保持 `RunInjectWaveVelocity()` 关闭，不恢复 `wave -> fluid` 反向注入

### Unchanged but relied upon
- `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticlePostProcess.cs`
- `UnityProject/WaveParticle/Assets/Shaders/WaveParticles/WaveParticle_PostProcess_H.shader`
- `UnityProject/WaveParticle/Assets/Shaders/WaveParticles/WaveParticle_PostProcess_V.shader`
- `UnityProject/WaveParticle/Assets/Shaders/WaterSurface/WaterSurface.shader`

---

### Task 1: 建立可测试的粒子池骨架

**Files:**
- Create: `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticle.cs`
- Create: `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticlePool.cs`
- Create: `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticles.EditMode.asmdef`
- Create: `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs`

- [ ] **Step 1: 写失败测试，约束 ambient 初始化、event 生成、死亡回收**

```csharp
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
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task1.xml" -quit
```

Expected: FAIL，提示 `WaveParticlePool` / `ResetAmbient` / `SpawnEventRing` / `Step` 未定义。

- [ ] **Step 3: 创建 EditMode asmdef**

```json
{
  "name": "WaveParticles.EditMode",
  "rootNamespace": "",
  "references": [
    "Assembly-CSharp",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 4: 写最小实现，让测试编译通过**

`WaveParticle.cs`
```csharp
using UnityEngine;

public struct WaveParticle
{
    public bool active;
    public byte layerType;
    public Vector2 pos;
    public Vector2 dir;
    public float speed;
    public float amplitude;
    public float life;
    public float maxLife;
}
```

`WaveParticlePool.cs`
```csharp
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

    public void ResetAmbient(int ambientCount, float amplitudeMin, float amplitudeMax, float speedMin, float speedMax, Random random)
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
```

- [ ] **Step 5: 运行测试，确认通过**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task1.xml" -quit
```

Expected: PASS，4 个 `WaveParticlePoolTests` 通过。

- [ ] **Step 6: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticle.cs UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticlePool.cs UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticles.EditMode.asmdef UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs
git commit -m "test: add wave particle pool core tests and skeleton"
```

### Task 2: 为速度缓存建立异步读回和 CPU 采样测试

**Files:**
- Create: `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveVelocityCache.cs`
- Create: `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveVelocityCacheTests.cs`

- [ ] **Step 1: 写失败测试，约束双线性采样和边界 clamp**

```csharp
using NUnit.Framework;
using UnityEngine;

public class WaveVelocityCacheTests
{
    [Test]
    public void SampleBilinear_ReturnsCenterAverage()
    {
        var cache = new WaveVelocityCache();
        cache.SetDebugData(
            width: 2,
            height: 2,
            data: new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 2f)
            });

        var v = cache.Sample(new Vector2(0.5f, 0.5f));
        Assert.That(v.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(v.y, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void Sample_ClampsOutsideUv()
    {
        var cache = new WaveVelocityCache();
        cache.SetDebugData(
            width: 2,
            height: 2,
            data: new[]
            {
                new Vector2(1f, 3f),
                new Vector2(2f, 4f),
                new Vector2(5f, 7f),
                new Vector2(6f, 8f)
            });

        var v = cache.Sample(new Vector2(-1f, -1f));
        Assert.AreEqual(new Vector2(1f, 3f), v);
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task2.xml" -quit
```

Expected: FAIL，提示 `WaveVelocityCache` / `SetDebugData` / `Sample` 未定义。

- [ ] **Step 3: 实现 `WaveVelocityCache` 的最小可测版本**

```csharp
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class WaveVelocityCache
{
    Vector2[] velocityData = System.Array.Empty<Vector2>();
    int width;
    int height;
    bool readbackPending;

    public bool HasData => width > 0 && height > 0 && velocityData.Length == width * height;

    public void TrySchedule(RenderTexture source)
    {
        if (source == null || readbackPending)
            return;

        readbackPending = true;
        AsyncGPUReadback.Request(source, 0, request =>
        {
            readbackPending = false;
            if (request.hasError)
                return;

            NativeArray<Color> colors = request.GetData<Color>();
            width = source.width;
            height = source.height;
            velocityData = new Vector2[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                velocityData[i] = new Vector2(colors[i].r, colors[i].g);
        });
    }

    public Vector2 Sample(Vector2 uv)
    {
        if (!HasData)
            return Vector2.zero;

        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        float x = uv.x * (width - 1);
        float y = uv.y * (height - 1);
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = x - x0;
        float ty = y - y0;

        Vector2 v00 = velocityData[y0 * width + x0];
        Vector2 v10 = velocityData[y0 * width + x1];
        Vector2 v01 = velocityData[y1 * width + x0];
        Vector2 v11 = velocityData[y1 * width + x1];

        Vector2 a = Vector2.Lerp(v00, v10, tx);
        Vector2 b = Vector2.Lerp(v01, v11, tx);
        return Vector2.Lerp(a, b, ty);
    }

    public void SetDebugData(int width, int height, Vector2[] data)
    {
        this.width = width;
        this.height = height;
        this.velocityData = data;
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task2.xml" -quit
```

Expected: PASS，`WaveVelocityCacheTests` 通过。

- [ ] **Step 5: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveVelocityCache.cs UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveVelocityCacheTests.cs
git commit -m "test: add wave velocity cache sampling tests"
```

### Task 3: 将 `WaveParticle_Rasterize.shader` 改为纯输出 shader

**Files:**
- Modify: `UnityProject/WaveParticle/Assets/Shaders/WaveParticles/WaveParticle_Rasterize.shader`
- Test: `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs`

- [ ] **Step 1: 写一个小测试，确保 mesh payload 约定未变**

在 `WaveParticlePoolTests.cs` 追加：

```csharp
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
```

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task3.xml" -quit
```

Expected: FAIL，提示 `BuildRenderData` 未定义。

- [ ] **Step 3: 在 `WaveParticlePool.cs` 增加 `BuildRenderData`**

```csharp
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
```

- [ ] **Step 4: 把 shader 改成纯输出，不再自己推进粒子**

将 `WaveParticle_Rasterize.shader` 中的 vertex shader 替换为：

```hlsl
WAVE_PARTICLE vert(VS_INPUT v)
{
    WAVE_PARTICLE o;
    float2 pos       = v.pos.xy;
    float2 direction = normalize(v.texCoord);
    float  height    = v.pos.z;
    float  speed     = v.nor.z;

    o.pos       = float4(pos, 0.5, 1.0);
    o.velocity  = direction * speed;
    o.amplitude = height * _HeightScale;
    return o;
}
```

并删除以下无用 uniform：

```hlsl
float _WaveParticleSpeedScale;
float _TimeScale;
int   _Time_Custom;
Texture2D _VelocityTex;
SamplerState sampler_linear_clamp;
float _FluidParticleStrength;
```

- [ ] **Step 5: 运行测试，确认通过**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task3.xml" -quit
```

Expected: PASS，旧测试 + `BuildRenderData_EncodesPosAmplitudeDirectionAndSpeed` 全部通过。

- [ ] **Step 6: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticlePool.cs UnityProject/WaveParticle/Assets/Shaders/WaveParticles/WaveParticle_Rasterize.shader UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs
git commit -m "refactor: make wave particle rasterizer consume live particle state"
```

### Task 4: 重写 `WaveParticleSystem`，接入 CPU 粒子池与动态 Mesh

**Files:**
- Modify: `UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticleSystem.cs`
- Modify: `UnityProject/WaveParticle/Assets/Scripts/Core/SimulationParameters.cs`
- Modify: `UnityProject/WaveParticle/Assets/WaterParams.asset`

- [ ] **Step 1: 增加参数字段**

在 `SimulationParameters.cs` 的 wave particle 区域追加：

```csharp
public int ambientParticleCount = 2048;
public float ambientParticleSpeedMin = 0.05f;
public float ambientParticleSpeedMax = 0.2f;
public float ambientParticleAmplitudeMin = 0.03f;
public float ambientParticleAmplitudeMax = 0.08f;
public int eventParticlesPerSpawn = 12;
public float eventParticleSpeed = 0.6f;
public float eventParticleAmplitude = 0.2f;
public float eventParticleLife = 1.25f;
public float eventSpawnSpacing = 0.08f;
public float eventAmplitudeDamping = 0.96f;
public int velocityReadbackInterval = 2;
```

在 `WaterParams.asset` 追加对应默认值：

```yaml
  ambientParticleCount: 2048
  ambientParticleSpeedMin: 0.05
  ambientParticleSpeedMax: 0.2
  ambientParticleAmplitudeMin: 0.03
  ambientParticleAmplitudeMax: 0.08
  eventParticlesPerSpawn: 12
  eventParticleSpeed: 0.6
  eventParticleAmplitude: 0.2
  eventParticleLife: 1.25
  eventSpawnSpacing: 0.08
  eventAmplitudeDamping: 0.96
  velocityReadbackInterval: 2
```

- [ ] **Step 2: 重写 `WaveParticleSystem.cs` 的核心字段与构造函数**

用以下结构替换旧字段：

```csharp
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
```

- [ ] **Step 3: 实现 `Step`、`SpawnEventRing`、`Rasterize`**

```csharp
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
```

- [ ] **Step 4: 删除旧随机初始化逻辑**

删除：
```csharp
particleMesh = BuildParticleMesh(6000);
```

删除整个：
```csharp
static Mesh BuildParticleMesh(int count) { ... }
```

- [ ] **Step 5: 编译并做 smoke test**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -quit -logFile "Logs/wave-particles-task4.log"
```

Expected: Editor batch compile 成功；log 中无 `CS0103` / `CS0117` / `Shader error`。

- [ ] **Step 6: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticleSystem.cs UnityProject/WaveParticle/Assets/Scripts/Core/SimulationParameters.cs UnityProject/WaveParticle/Assets/WaterParams.asset
git commit -m "feat: drive wave particles from cpu pool and dynamic mesh"
```

### Task 5: 将 Shift+LMB 拖拽接成事件波粒子生成源

**Files:**
- Modify: `UnityProject/WaveParticle/Assets/Scripts/Core/WaterSimulationManager.cs`
- Test: `UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs`

- [ ] **Step 1: 写一个纯逻辑测试，验证拖拽距离足够时才生成事件**

在 `WaveParticlePoolTests.cs` 追加一个帮助类测试（若不想在 `WaterSimulationManager` 里塞逻辑，先提取一个纯函数）：

```csharp
[Test]
public void DragSpawner_EmitsCentersAtConfiguredSpacing()
{
    var centers = WaveParticleDragUtil.BuildSpawnCenters(
        lastUv: new Vector2(0f, 0f),
        currentUv: new Vector2(0.25f, 0f),
        spacing: 0.1f);

    Assert.AreEqual(2, centers.Count);
    Assert.That(centers[0].x, Is.EqualTo(0.1f).Within(0.001f));
    Assert.That(centers[1].x, Is.EqualTo(0.2f).Within(0.001f));
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task5.xml" -quit
```

Expected: FAIL，提示 `WaveParticleDragUtil` 未定义。

- [ ] **Step 3: 新增纯工具并修改 `WaterSimulationManager.cs`**

在 `WaterSimulationManager.cs` 同目录创建或内联一个静态工具：

```csharp
using System.Collections.Generic;
using UnityEngine;

public static class WaveParticleDragUtil
{
    public static List<Vector2> BuildSpawnCenters(Vector2 lastUv, Vector2 currentUv, float spacing)
    {
        var result = new List<Vector2>();
        Vector2 delta = currentUv - lastUv;
        float distance = delta.magnitude;
        if (distance < spacing)
            return result;

        Vector2 dir = delta / distance;
        for (float t = spacing; t <= distance; t += spacing)
            result.Add(lastUv + dir * t);
        return result;
    }
}
```

然后在 `HandleVelocityDrag()` 中，在调用 `fluidSimulator.QueueVelocityImpulse(uv, velUV);` 后追加：

```csharp
if (waveParticleSystem != null)
{
    foreach (var center in WaveParticleDragUtil.BuildSpawnCenters(lastVelocityDragUV, uv, param.eventSpawnSpacing))
        waveParticleSystem.SpawnEventRing(center * 2f - Vector2.one);
}
```

如果 `WaveParticleSystem` 希望输入是 `[-1,1]` 空间，以上转换保留；如果系统统一使用 `[0,1]` UV，则去掉转换并同步所有调用处。

- [ ] **Step 4: 在 `LateUpdate` 中添加 `waveParticleSystem.Step(Time.deltaTime)`**

将原有：
```csharp
waveParticleSystem.Rasterize(frameCount);
```

替换为：
```csharp
waveParticleSystem.Step(Time.deltaTime);
waveParticleSystem.Rasterize();
```

- [ ] **Step 5: 运行 EditMode 测试与手工 smoke test**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -runTests -testPlatform EditMode -testResults "TestResults/wave-particles-task5.xml" -quit
```

Expected: PASS。

手工验证：
- 打开场景，`renderMode = 6`
- 按住 Shift+LMB 拖拽
- 观察 `_DeviationTex` 对应的水面 debug 输出在拖拽路径附近出现一串扩散波纹

- [ ] **Step 6: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/Core/WaterSimulationManager.cs UnityProject/WaveParticle/Assets/Scripts/WaveParticles/WaveParticleSystem.cs UnityProject/WaveParticle/Assets/Tests/EditMode/WaveParticles/WaveParticlePoolTests.cs
git commit -m "feat: spawn event wave particles from shift-drag input"
```

### Task 6: 收口单向耦合，明确关闭 `wave -> fluid`

**Files:**
- Modify: `UnityProject/WaveParticle/Assets/Scripts/FluidSimulation/FluidSimulator.cs`
- Modify: `UnityProject/WaveParticle/Assets/WaterParams.asset`
- Modify: `UnityProject/WaveParticle/Assets/Scripts/Core/SimulationParameters.cs`

- [ ] **Step 1: 保持 `RunInjectWaveVelocity()` 不在主流程里执行**

确认 `RunFullPipeline()` 为：

```csharp
public void RunFullPipeline()
{
    SetGlobalUniforms();

    RunAdvect(mgr.rtVelocity);
    if (pendingImpulse)
    {
        RunSplatVelocityImpulse();
        pendingImpulse = false;
    }
    RunDivergence();
    RunJacobi();
    RunSubtractGradient();
}
```

如果仍需保留 `RunInjectWaveVelocity()` 方法本体，允许保留但不调用；不要恢复到 pipeline 中。

- [ ] **Step 2: 清理未使用参数**

如果当前工程中以下字段只服务于旧实现且已无调用，删除或标记弃用：

```csharp
public float waveInjectStrength = 200f;
public float waveParticleSpeedScale = 0.00005f;
public float timeScale = 1.3f; // 若只供 wave particle 老逻辑使用则删，否则保留
```

删除前先 grep；若仍被 WaterSurface/Flow 逻辑使用，则保留。

- [ ] **Step 3: 编译并手工验收**

Run:
```bash
Unity -batchmode -projectPath "UnityProject/WaveParticle" -quit -logFile "Logs/wave-particles-task6.log"
```

Expected: 编译成功。

手工验收：
- `renderMode = 6`：静止时 `_DeviationTex` 非全黑（ambient 成功）
- Shift+LMB 拖拽：路径附近出现事件涟漪
- Shift+LMB 拖拽并持续注入流体速度后：涟漪会被流场带偏
- 关闭所有交互后：事件涟漪消失，但环境底噪仍存在
- 观察 `rtVelocity`：不再受 `rtWaveParticle` 反向注入干扰

- [ ] **Step 4: Commit**

```bash
git add UnityProject/WaveParticle/Assets/Scripts/FluidSimulation/FluidSimulator.cs UnityProject/WaveParticle/Assets/Scripts/Core/SimulationParameters.cs UnityProject/WaveParticle/Assets/WaterParams.asset
git commit -m "refactor: keep wave to fluid coupling disabled during cpu particle migration"
```

---

## Spec Coverage Check

- “CPU 粒子池 + 固定容量动态 Mesh” → Task 1、Task 4
- “底噪层 + 事件层” → Task 1、Task 4、Task 5
- “fluid velocity advect wave particles” → Task 2、Task 4
- “保留 `rtWaveParticle -> PostProcess_H/V -> WaterSurface`” → Task 3、Task 4
- “关闭 `wave -> fluid` 注入” → Task 6
- “Shift+LMB 拖拽产生事件层粒子” → Task 5

无 spec 缺口。
