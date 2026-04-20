using UnityEngine;

// Debug overlay showing all simulation render textures as a grid
public class WaterDebugUI : MonoBehaviour
{
    public WaterSimulationManager mgr;

    bool showDebugTextures = false;
    bool showParamPanel = true;

    const int THUMB = 160;
    const int PAD = 4;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) showDebugTextures = !showDebugTextures;
        if (Input.GetKeyDown(KeyCode.F2)) showParamPanel = !showParamPanel;
    }

    void OnGUI()
    {
        int y = 10;

        // Help label
        GUI.Label(new Rect(10, y, 400, 20), "RMB: Orbit  Scroll: Zoom  LMB: Draw Obstacle  C: Clear  F1: Debug Textures  F2: Params");
        y += 22;

        if (showParamPanel && mgr != null && mgr._param != null)
            y = DrawParamPanel(y, mgr._param);

        if (showDebugTextures && mgr != null)
            DrawDebugTextures();
    }

    int DrawParamPanel(int startY, SimulationParameters param)
    {
        int x = 10, y = startY, w = 280, lh = 20;
        GUI.Box(new Rect(x - 4, y - 4, w + 8, 260), "Simulation Parameters");
        y += 22;

        // Render mode selector
        GUI.Label(new Rect(x, y, 120, lh), $"Mode: {param.renderMode}");
        if (GUI.Button(new Rect(x + 130, y, 60, lh), "<")) param.renderMode = Mathf.Max(0, param.renderMode - 1);
        if (GUI.Button(new Rect(x + 195, y, 60, lh), ">")) param.renderMode = Mathf.Min(11, param.renderMode + 1);
        y += lh + 2;

        param.heightScale = FloatSlider(x, y, w, lh, "Height Scale", param.heightScale, 0f, 3f); y += lh + 2;
        param.waveParticleSpeedScale = FloatSlider(x, y, w, lh, "Wave Speed", param.waveParticleSpeedScale, 0f, 0.0005f); y += lh + 2;
        param.timeScale = FloatSlider(x, y, w, lh, "Time Scale", param.timeScale, 0f, 10f); y += lh + 2;
        param.foamScale = FloatSlider(x, y, w, lh, "Foam Scale", param.foamScale, 0f, 20f); y += lh + 2;
        param.vorticityScale = FloatSlider(x, y, w, lh, "Vorticity", param.vorticityScale, 0f, 10f); y += lh + 2;
        param.splatScale = FloatSlider(x, y, w, lh, "Splat Scale", param.splatScale, 0f, 0.05f); y += lh + 2;
        param.shininess = FloatSlider(x, y, w, lh, "Shininess", param.shininess, 10f, 600f); y += lh + 2;
        param.fresnelScale = FloatSlider(x, y, w, lh, "Fresnel Scale", param.fresnelScale, 0f, 1f); y += lh + 2;

        y += PAD;
        return y;
    }

    float FloatSlider(int x, int y, int w, int lh, string label, float value, float min, float max)
    {
        GUI.Label(new Rect(x, y, 120, lh), $"{label}: {value:F4}");
        return GUI.HorizontalSlider(new Rect(x + 130, y + 4, w - 130, lh - 4), value, min, max);
    }

    void DrawDebugTextures()
    {
        RenderTexture[] rts = {
            mgr.rtVelocity.Current,
            mgr.rtDensity.Current,
            mgr.rtPressure.Current,
            mgr.rtDivergence,
            mgr.rtObstacleCreate,
            mgr.rtObstacleBlur,
            mgr.rtObstacleFinal,
            mgr.rtWaveParticle,
            mgr.rtPostProcessV1,
            mgr.rtPostProcessV2
        };
        string[] labels = { "Velocity", "Density", "Pressure", "Divergence", "ObsCreate", "ObsBlur", "ObsFinal", "WaveParticle", "PostV1", "PostV2" };

        int cols = 4;
        int startX = Screen.width - (THUMB + PAD) * cols - PAD;
        int startY = PAD;
        for (int i = 0; i < rts.Length; i++)
        {
            if (rts[i] == null) continue;
            int cx = startX + (i % cols) * (THUMB + PAD);
            int cy = startY + (i / cols) * (THUMB + PAD + 16);
            GUI.DrawTexture(new Rect(cx, cy, THUMB, THUMB), rts[i], ScaleMode.ScaleToFit, false);
            GUI.Label(new Rect(cx, cy + THUMB, THUMB, 16), labels[i]);
        }
    }
}
