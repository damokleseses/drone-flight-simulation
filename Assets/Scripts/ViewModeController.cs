using UnityEngine;
using CesiumForUnity;

public class ViewModeController : MonoBehaviour
{
    [Header("Cameras")]
    public Camera cam3D;
    public Camera cam2D;

    [Header("Cesium")]
    public CesiumGeoreference georeference;
    public CesiumGlobeAnchor focusAnchor;

    [Header("2D Settings")]
    public float topDownHeightMeters = 2000f;
    public float orthoSize = 2000f;

    [Header("Optional: enale drawing only in 2D")]
    public MonoBehaviour drawTool; // PolygonDrawTool3D

    private void Awake()
    {
        if (cam3D != null) cam3D.enabled = true;
        if (cam2D != null) cam2D.enabled = false;
    }

    public void Set2D()
    {
        if (cam2D == null || cam3D == null) return;

        // Pick a focus point: either focusAnchor or cam3D current position
        Vector3 focusWorld = (focusAnchor != null) ? focusAnchor.transform.position : cam3D.transform.position;

        // Place cam2D
        cam2D.transform.position = focusWorld + Vector3.up * topDownHeightMeters;
        cam2D.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Orthographic map feel
        cam2D.orthographic = true;
        cam2D.orthographicSize = orthoSize;

        // Switch rendering (robust)
        cam3D.enabled = false;
        cam2D.enabled = true;

        if (drawTool != null) drawTool.enabled = true;
    }

    public void Set3D()
    {
        if (cam2D == null || cam3D == null) return;

        cam2D.enabled = false;
        cam3D.enabled = true;

        // optional: keep drawing also in 3D
        // if (drawTool != null) drawTool.enabled = false;
    }

}
