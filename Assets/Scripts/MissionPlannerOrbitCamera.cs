using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MissionPlannerOrbitCamera : MonoBehaviour
{
    [Header("Picking")]
    public LayerMask pickMask = ~0;
    public float maxPickDistance = 2_000_000f;

    [Header("Speeds (globe tuned)")]
    public float orbitDegPerPixel = 0.08f;
    public float zoomFactorPerScroll = 0.10f;
    public float panMetersPerPixelAt1000m = 1.0f;

    [Header("Limits / Safety")]
    public float minDistance = 5f;
    public float maxDistance = 2_000_000f;
    public float maxDeltaPixelsPerFrame = 80f;     // key: prevents “teleport delta”
    public float maxPanMetersPerFrame = 3000f;

    [Header("State")]
    public Vector3 target;
    public float distance = 1000f;
    public float yaw = 0f;
    public float pitch = 35f;

    Camera cam;
    Vector2 prevMouse;
    bool draggingL;
    bool draggingR;

    void Awake()
    {
        cam = GetComponent<Camera>();
        prevMouse = Input.mousePosition;

        TrySetTargetFromCenterRay();
        SyncFromCurrentTransform();
        ApplyTransform();
    }

    void Update()
    {
        Vector2 mouse = Input.mousePosition;

        bool lDown = Input.GetMouseButtonDown(0);
        bool rDown = Input.GetMouseButtonDown(1);
        bool lUp   = Input.GetMouseButtonUp(0);
        bool rUp   = Input.GetMouseButtonUp(1);

        // Start drag: reset prevMouse to avoid huge first delta
        if (lDown) { draggingL = true; prevMouse = mouse; }
        if (rDown) { draggingR = true; prevMouse = mouse; }
        if (lUp)   draggingL = false;
        if (rUp)   draggingR = false;

        // Set target on MMB down
        if (Input.GetMouseButtonDown(2))
            TrySetTargetFromMouse(mouse);

        // Only compute delta while dragging
        if (draggingL || draggingR)
        {
            Vector2 delta = mouse - prevMouse;

            // clamp delta hard to avoid “one click sends you flying”
            if (delta.magnitude > maxDeltaPixelsPerFrame)
                delta = delta.normalized * maxDeltaPixelsPerFrame;

            prevMouse = mouse;

            // Orbit (LMB)
            if (draggingL)
            {
                yaw   += delta.x * orbitDegPerPixel;
                pitch -= delta.y * orbitDegPerPixel;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
            }

            // Pan (RMB)
            if (draggingR)
            {
                float scale = (distance / 1000f) * panMetersPerPixelAt1000m;
                Vector3 panWorld = (-transform.right * delta.x - transform.up * delta.y) * scale;

                if (panWorld.magnitude > maxPanMetersPerFrame)
                    panWorld = panWorld.normalized * maxPanMetersPerFrame;

                target += panWorld;
            }
        }

        // Zoom (scroll) – stable multiplicative
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float factor = 1f - Mathf.Sign(scroll) * zoomFactorPerScroll;
            distance = Mathf.Clamp(distance * factor, minDistance, maxDistance);
        }

        ApplyTransform();
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target - rot * Vector3.forward * distance;
        transform.SetPositionAndRotation(pos, rot);
    }

    void SyncFromCurrentTransform()
    {
        Vector3 toCam = transform.position - target;
        distance = Mathf.Clamp(toCam.magnitude, minDistance, maxDistance);
        Vector3 fwd = (distance > 0.001f) ? (-toCam / distance) : transform.forward;

        var e = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles;
        yaw = e.y;
        pitch = e.x;
        if (pitch > 180f) pitch -= 360f;
    }

    bool TrySetTargetFromMouse(Vector2 mousePos)
    {
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out var hit, maxPickDistance, pickMask, QueryTriggerInteraction.Ignore))
        {
            target = hit.point;
            distance = Mathf.Clamp(Vector3.Distance(transform.position, target), minDistance, maxDistance);
            return true;
        }
        return false;
    }

    bool TrySetTargetFromCenterRay()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, maxPickDistance, pickMask, QueryTriggerInteraction.Ignore))
        {
            target = hit.point;
            distance = Mathf.Clamp(Vector3.Distance(transform.position, target), minDistance, maxDistance);
            return true;
        }
        target = transform.position + transform.forward * 1000f;
        distance = 1000f;
        return false;
    }
}