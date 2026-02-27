using UnityEngine;
using CesiumForUnity;

public class ViewModeController : MonoBehaviour
{
    public enum Mode { View3D, Map2D }
    public Mode CurrentMode { get; private set; } = Mode.View3D;

    [Header("Cameras")]
    public Camera cam3D;     // Cesium DynamicCamera
    public Camera cam2D;     // MapCamera2D

    [Header("Focus")]
    public CesiumGlobeAnchor focusAnchor; // optional: if set, 2D centers on this
    public float topDownHeightMeters = 2000f;
    public float orthoSize = 2000f;

    [Header("Enable/Disable input per mode")]
    public MonoBehaviour[] enableIn3D; // e.g. CesiumCameraController, FlyToController, etc.
    public MonoBehaviour[] enableIn2D; // e.g. MapCamera2DController

    [Header("Drawing")]
    public MonoBehaviour drawTool; // PolygonDrawTool3D

    void Awake()
    {
        // Ensure both camera GOs are active so switching works
        if (cam3D != null) cam3D.gameObject.SetActive(true);
        if (cam2D != null) cam2D.gameObject.SetActive(true);

        Set3D();
    }

    public void Set2D()
    {
        if (cam2D == null || cam3D == null) return;

        // Place map cam above focus
        Vector3 focus = (focusAnchor != null) ? focusAnchor.transform.position : cam3D.transform.position;
        cam2D.transform.position = focus + Vector3.up * topDownHeightMeters;
        cam2D.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        cam2D.orthographic = true;
        cam2D.orthographicSize = orthoSize;
        cam2D.nearClipPlane = 0.1f;
        cam2D.farClipPlane = 2_000_000f;

        cam3D.enabled = false;
        cam2D.enabled = true;

        SetEnabled(enableIn3D, false);
        SetEnabled(enableIn2D, true);

        if (drawTool != null) drawTool.enabled = true;

        CurrentMode = Mode.Map2D;
    }

    public void Set3D()
    {
        if (cam2D == null || cam3D == null) return;

        cam2D.enabled = false;
        cam3D.enabled = true;

        SetEnabled(enableIn2D, false);
        SetEnabled(enableIn3D, true);

        if (drawTool != null) drawTool.enabled = false;

        CurrentMode = Mode.View3D;
    }

    static void SetEnabled(MonoBehaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null) behaviours[i].enabled = enabled;
        }
    }
}