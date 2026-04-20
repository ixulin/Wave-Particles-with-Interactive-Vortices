using UnityEngine;

public class PingPongRT
{
    public RenderTexture Ping { get; private set; }
    public RenderTexture Pong { get; private set; }
    public RenderTexture Current => Ping;
    public RenderTexture Next    => Pong;

    public PingPongRT(int width, int height, RenderTextureFormat format, string namePing, string namePong)
    {
        Ping = Create(width, height, format, namePing);
        Pong = Create(width, height, format, namePong);
    }

    private static RenderTexture Create(int w, int h, RenderTextureFormat fmt, string rtName)
    {
        var rt = new RenderTexture(w, h, 0, fmt)
        {
            name        = rtName,
            filterMode  = FilterMode.Bilinear,
            wrapMode    = TextureWrapMode.Repeat
        };
        rt.Create();
        return rt;
    }

    public void Swap() => (Ping, Pong) = (Pong, Ping);

    public void Release()
    {
        if (Ping != null) { Ping.Release(); Object.Destroy(Ping); }
        if (Pong != null) { Pong.Release(); Object.Destroy(Pong); }
    }
}
