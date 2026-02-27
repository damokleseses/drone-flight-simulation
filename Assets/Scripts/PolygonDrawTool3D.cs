using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using UnityEngine.EventSystems;
using CesiumForUnity;

public class PolygonDrawTool3D : MonoBehaviour
{
    [Header("Refs")]
    public MissionStore missionStore;
    public CesiumGeoreference georeference;
    public ViewModeController viewMode;

    [Header("Picking")]
    public LayerMask pickMask = ~0;
    public float maxRayDistance = 2_000_000f;
    public bool blockWhenPointerOverUI = true;

    [Header("Click vs Drag")]
    public float maxClickMovePixels = 6f;

    private Vector2 _pressPos;
    private bool _pressed;

    void Reset()
    {
        missionStore = FindFirstObjectByType<MissionStore>();
        georeference = FindFirstObjectByType<CesiumGeoreference>();
        viewMode = FindFirstObjectByType<ViewModeController>();
    }

    void Update()
    {
        if (missionStore == null || georeference == null || viewMode == null) return;

        // Draw only in 2D map mode
        if (viewMode.CurrentMode != ViewModeController.Mode.Map2D) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _pressed = true;
            _pressPos = mouse.position.ReadValue();
        }

        if (_pressed && mouse.leftButton.wasReleasedThisFrame)
        {
            _pressed = false;

            Vector2 releasePos = mouse.position.ReadValue();
            if (Vector2.Distance(_pressPos, releasePos) > maxClickMovePixels)
                return; // drag -> don't place a point

            // Use active camera (2D)
            Camera cam = viewMode.cam2D != null ? viewMode.cam2D : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(releasePos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, pickMask, QueryTriggerInteraction.Ignore))
            {
                // Convert hit point -> lon/lat/height via Cesium
                double3 llh = GeoUtilsCesium.UnityToLonLatHeight(georeference, hit.point);
                missionStore.AddVertex(llh.x, llh.y, llh.z);

                Debug.Log($"Vertex added: lon={llh.x:F6}, lat={llh.y:F6}, h={llh.z:F1}");
            }
        }
    }
}