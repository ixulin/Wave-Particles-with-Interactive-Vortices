using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterSimulationManager : MonoBehaviour
{
    [Header("Configuration")]
    public SimulationParameters _param;
    public Texture2D foamTexture;
    public Texture2D flowmapTexture;

    // Shared render textures
    public PingPongRT rtVelocity { get; private set; }
    public PingPongRT rtDensity { get; private set; }
    public PingPongRT rtPressure { get; private set; }
    public RenderTexture rtDivergence { get; private set; }

    public RenderTexture rtObstacleCreate { get; private set; }
    public RenderTexture rtObstacleBlur { get; private set; }
    public RenderTexture rtObstacleFinal { get; private set; }

    public RenderTexture rtWaveParticle { get; private set; }
    public RenderTexture rtPostProcessH1 { get; private set; }
    public RenderTexture rtPostProcessH2 { get; private set; }
    public RenderTexture rtPostProcessV1 { get; private set; }
    public RenderTexture rtPostProcessV2 { get; private set; }

    FluidSimulator fluidSimulator;
    ObstacleSystem obstacleSystem;
    WaveParticleSystem waveParticleSystem;
    WaveParticlePostProcess wavePostProcess;
    WaterSurfaceRenderer waterSurfaceRenderer;

    int frameCount = 0;

    // Brush interaction state
    bool pendingObstacleDraw = false;
    Vector2 brushUV = Vector2.zero;

    void Awake()
    {

    }

    void OnDestroy() => ReleaseAll();

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        if (obstacleSystem == null) return; // Awake not finished
        // 1. Obstacle pipeline
        if (pendingObstacleDraw)
        {
            obstacleSystem.DrawObstacle(brushUV);
            pendingObstacleDraw = false;
        }
        obstacleSystem.RunBlurPass();

        // 2. Fluid simulation (every N frames)
        if (frameCount % _param.fluidSimulationInterval == 0)
            fluidSimulator.RunFullPipeline();

        // 3. Wave particle rasterization
        waveParticleSystem.Rasterize(frameCount);

        // 4. Fourier post-process
        wavePostProcess.RunHorizontalPass();
        wavePostProcess.RunVerticalPass();

        // 5. Update water surface material
        waterSurfaceRenderer.UpdateMaterialProperties(frameCount);

        frameCount++;
    }

    void HandleInput()
    {
        if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl))
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane waterPlane = new Plane(Vector3.up, transform.position);
            if (waterPlane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 local = transform.InverseTransformPoint(hit);

                // Map local space to UV [0,1]. WaterMeshBuilder places vertices at
                // ((u-0.5)*sizeX, 0, (v-0.5)*sizeZ) so UV = local / size + 0.5.
                var builder = GetComponent<WaterMeshBuilder>();
                float sx = builder != null ? builder.sizeX : 2f;
                float sz = builder != null ? builder.sizeZ : 2f;

                brushUV = new Vector2(
                    local.x / sx + 0.5f,
                    local.z / sz + 0.5f
                );
                brushUV.x = Mathf.Clamp01(brushUV.x);
                brushUV.y = Mathf.Clamp01(brushUV.y);
                pendingObstacleDraw = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.C) && obstacleSystem != null)
            obstacleSystem.ClearObstacles();
    }

    void AllocateRenderTextures()
    {
        int fw = _param.textureWidthFluid, fh = _param.textureHeightFluid;
        int mw = _param.textureWidth, mh = _param.textureHeight;
        var fp16 = RenderTextureFormat.ARGBHalf;
        var r8 = RenderTextureFormat.ARGB32;

        rtVelocity = new PingPongRT(fw, fh, fp16, "FluidVelocity1", "FluidVelocity2");
        rtDensity = new PingPongRT(fw, fh, fp16, "FluidDensity1", "FluidDensity2");
        rtPressure = new PingPongRT(fw, fh, fp16, "FluidPressure1", "FluidPressure2");
        rtDivergence = MakeRT(fw, fh, fp16, "FluidDivergence", TextureWrapMode.Repeat);

        rtObstacleCreate = MakeRT(fw, fh, r8, "ObstacleCreate", TextureWrapMode.Repeat);
        rtObstacleBlur = MakeRT(fw, fh, r8, "ObstacleBlur", TextureWrapMode.Repeat);
        rtObstacleFinal = MakeRT(fw, fh, r8, "ObstacleFinal", TextureWrapMode.Repeat);

        rtWaveParticle = MakeRT(mw, mh, fp16, "WaveParticle", TextureWrapMode.Repeat);
        rtPostProcessH1 = MakeRT(mw, mh, fp16, "PostProcessH1", TextureWrapMode.Repeat);
        rtPostProcessH2 = MakeRT(mw, mh, fp16, "PostProcessH2", TextureWrapMode.Repeat);
        rtPostProcessV1 = MakeRT(mw, mh, fp16, "PostProcessV1", TextureWrapMode.Repeat);
        rtPostProcessV2 = MakeRT(mw, mh, fp16, "PostProcessV2", TextureWrapMode.Repeat);

        // Clear all to black at startup
        ClearRT(rtDivergence);
        ClearRT(rtObstacleCreate); ClearRT(rtObstacleBlur); ClearRT(rtObstacleFinal);
        ClearRT(rtWaveParticle);
        ClearRT(rtPostProcessH1); ClearRT(rtPostProcessH2);
        ClearRT(rtPostProcessV1); ClearRT(rtPostProcessV2);
        ClearRT(rtVelocity.Ping); ClearRT(rtVelocity.Pong);
        ClearRT(rtDensity.Ping); ClearRT(rtDensity.Pong);
        ClearRT(rtPressure.Ping); ClearRT(rtPressure.Pong);
    }

    static RenderTexture MakeRT(int w, int h, RenderTextureFormat fmt, string rtName, TextureWrapMode wrap)
    {
        var rt = new RenderTexture(w, h, 0, fmt)
        {
            name = rtName,
            filterMode = FilterMode.Bilinear,
            wrapMode = wrap
        };
        rt.Create();
        return rt;
    }

    static void ClearRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = prev;
    }

    void ReleaseAll()
    {
        rtVelocity?.Release();
        rtDensity?.Release();
        rtPressure?.Release();
        SafeRelease(rtDivergence);
        SafeRelease(rtObstacleCreate); SafeRelease(rtObstacleBlur); SafeRelease(rtObstacleFinal);
        SafeRelease(rtWaveParticle);
        SafeRelease(rtPostProcessH1); SafeRelease(rtPostProcessH2);
        SafeRelease(rtPostProcessV1); SafeRelease(rtPostProcessV2);
    }

    static void SafeRelease(RenderTexture rt)
    {
        if (rt != null) { rt.Release(); Object.Destroy(rt); }
    }

    internal void SetParam(SimulationParameters param)
    {
        _param = param; // Store reference for sub-systems
        if (param == null)
        {
            Debug.LogWarning("WaterSimulationManager: SimulationParameters not assigned, using defaults.");
            param = ScriptableObject.CreateInstance<SimulationParameters>();
        }
        AllocateRenderTextures();
        fluidSimulator = new FluidSimulator(param, this);
        obstacleSystem = new ObstacleSystem(param, this);
        waveParticleSystem = new WaveParticleSystem(param, this);
        wavePostProcess = new WaveParticlePostProcess(param, this);
        waterSurfaceRenderer = new WaterSurfaceRenderer(param, this, foamTexture, flowmapTexture,
                                                        GetComponent<MeshRenderer>().sharedMaterial);
    }

}
