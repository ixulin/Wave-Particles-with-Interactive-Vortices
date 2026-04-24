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
