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
