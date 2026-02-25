using UnityEngine;
using UnityEngine.InputSystem;
using CesiumForUnity;

public class PolygonDrawTool3D : MonoBehaviour
{
    public Camera mainCam;
    public CesiumGeoreference georeference;
    public MissionStore missionStore;

    public LayerMask pickMask = ~0;
    public float maxRayDistance = 2_000_000f;

    public bool blockWhenPointerOverUI = true;

    void Reset()
    {
        mainCam = Camera.main;
        georeference = FindFirstObjectByType<CesiumGeoreference>();
        missionStore = FindFirstObjectByType<MissionStore>();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (blockWhenPointerOverUI && UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            TryAddVertex(mouse.position.ReadValue());
        }
    }

    void TryAddVertex(Vector2 screenPos)
    {
        if (mainCam == null || georeference == null || missionStore == null) return;

        Ray ray = mainCam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, pickMask, QueryTriggerInteraction.Ignore))
            return;

        var llh = GeoUtilsCesium.UnityToLonLatHeight(georeference, hit.point);
        missionStore.AddVertex(llh.x, llh.y, llh.z);

        Debug.Log($"Vertex: lon={llh.x:F6}, lat={llh.y:F6}, h={llh.z:F1}");
    }
}