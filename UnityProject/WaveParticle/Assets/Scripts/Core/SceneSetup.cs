using UnityEngine;

// One-click scene setup helper:
// Creates Water GameObject + Camera when added to an empty scene.
// 使用方法: 在 Hierarchy 中创建空 GameObject，挂载此脚本，进入 Play Mode 或点击 "Setup Scene"。
[ExecuteInEditMode]
public class SceneSetup : MonoBehaviour
{
    [Header("References")]
    public SimulationParameters param;
    public Material waterMaterial;
    public Texture2D foamTexture;
    public Texture2D flowmapTexture;

    [Header("Water Mesh")]
    public int cellCount = 50;
    public float waterSize = 2f;

    void Start()
    {
        if (!Application.isPlaying) return;
        SetupScene();
        Destroy(this); // run once
    }

    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        // ---- Water GameObject ----
        var waterGO = new GameObject("Water");
        waterGO.transform.position = Vector3.zero;

        var meshFilter = waterGO.AddComponent<MeshFilter>();
        var meshRenderer = waterGO.AddComponent<MeshRenderer>();
        var meshBuilder = waterGO.AddComponent<WaterMeshBuilder>();
        meshBuilder.cellCountX = cellCount;
        meshBuilder.cellCountZ = cellCount;
        meshBuilder.sizeX = waterSize;
        meshBuilder.sizeZ = waterSize;

        if (waterMaterial != null)
            meshRenderer.sharedMaterial = waterMaterial;
        else
        {
            var mat = new Material(Shader.Find("Water/WaterSurface"));
            meshRenderer.sharedMaterial = mat;
        }

        var simMgr = waterGO.AddComponent<WaterSimulationManager>();
        simMgr.param = param;
        simMgr.foamTexture = foamTexture;
        simMgr.flowmapTexture = flowmapTexture;

        var debugUI = waterGO.AddComponent<WaterDebugUI>();
        debugUI.mgr = simMgr;
        debugUI.param = param;

        // ---- Camera ----
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        // var orbit = cam.gameObject.GetComponent<OrbitCamera>() ?? cam.gameObject.AddComponent<OrbitCamera>();
        // orbit.target   = waterGO.transform;
        // orbit.distance = 4f;

        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
        // cam.transform.position = new Vector3(0, 3, -4);
        cam.transform.position = new Vector3(0, 3, 0);
        cam.transform.LookAt(Vector3.zero);

        Debug.Log("[SceneSetup] Water simulation scene created. Press Play to run.");
    }
}
