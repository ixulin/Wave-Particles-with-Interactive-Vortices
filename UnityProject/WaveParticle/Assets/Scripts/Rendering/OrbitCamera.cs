using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float distance = 5f;

    [Header("Orbit")]
    public float horizontalAngle = 0f;
    public float verticalAngle   = 30f;
    public float orbitSpeed      = 120f;

    [Header("Zoom")]
    public float zoomSpeed  = 2f;
    public float minDist    = 1f;
    public float maxDist    = 20f;

    void Update()
    {
        // Hold right mouse button to orbit
        if (Input.GetMouseButton(1))
        {
            horizontalAngle += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
            verticalAngle   -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
            verticalAngle    = Mathf.Clamp(verticalAngle, 5f, 85f);
        }

        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance  = Mathf.Clamp(distance, minDist, maxDist);

        Vector3 center = target != null ? target.position : Vector3.zero;
        Quaternion rot = Quaternion.Euler(verticalAngle, horizontalAngle, 0);
        transform.position = center + rot * new Vector3(0, 0, -distance);
        transform.LookAt(center);
    }
}
