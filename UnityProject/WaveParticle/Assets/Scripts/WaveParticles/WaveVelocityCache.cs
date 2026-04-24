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
