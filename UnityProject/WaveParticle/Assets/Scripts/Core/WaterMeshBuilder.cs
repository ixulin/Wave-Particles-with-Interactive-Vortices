using UnityEngine;

// Creates the tessellated water surface mesh (grid of quad patches)
// Matches DX12 Mesh::InitWaterSurface()
[RequireComponent(typeof(MeshFilter))]
public class WaterMeshBuilder : MonoBehaviour
{
    [Range(1, 100)] public int cellCountX = 50;
    [Range(1, 100)] public int cellCountZ = 50;
    public float sizeX = 2f;
    public float sizeZ = 2f;

    void Awake() => Rebuild();

#if UNITY_EDITOR
    void OnValidate() { if (Application.isPlaying) Rebuild(); }
#endif

    public void Rebuild()
    {
        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = BuildMesh(cellCountX, cellCountZ, sizeX, sizeZ);
    }

    // Produces a grid of quads laid out as 4-control-point patches (MeshTopology.Quads)
    // Each cell maps to UV [0,1] and world space [-sizeX/2, sizeX/2] x [0] x [-sizeZ/2, sizeZ/2]
    public static Mesh BuildMesh(int nx, int nz, float sx, float sz)
    {
        int vertCount = (nx + 1) * (nz + 1);
        var verts  = new Vector3[vertCount];
        var uvs    = new Vector2[vertCount];

        for (int i = 0; i <= nx; i++)
        {
            for (int j = 0; j <= nz; j++)
            {
                int idx = i * (nz + 1) + j;
                float u = i / (float)nx;
                float v = j / (float)nz;
                verts[idx] = new Vector3((u - 0.5f) * sx, 0f, (v - 0.5f) * sz);
                uvs[idx]   = new Vector2(u, v);
            }
        }

        // Each cell = 4 indices forming a quad patch (order matches DX12: 0,1,2,3 = TL,BL,BR,TR)
        int cellCount = nx * nz;
        var quads = new int[cellCount * 4];
        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < nz; j++)
            {
                int c  = i * nz + j;
                int v0 = i       * (nz + 1) + j;
                int v1 = i       * (nz + 1) + j + 1;
                int v2 = (i + 1) * (nz + 1) + j + 1;
                int v3 = (i + 1) * (nz + 1) + j;
                quads[c * 4 + 0] = v0;
                quads[c * 4 + 1] = v1;
                quads[c * 4 + 2] = v2;
                quads[c * 4 + 3] = v3;
            }
        }

        var mesh = new Mesh { name = "WaterSurface" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = verts;
        mesh.uv          = uvs;
        mesh.SetIndices(quads, MeshTopology.Quads, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
