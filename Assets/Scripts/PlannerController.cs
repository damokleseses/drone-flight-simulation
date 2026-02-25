using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public class PlannerController : MonoBehaviour
{
    [Header("Refs")]
    public MissionStore missionStore;
    public MissionSettings settings;
    public LawnMowerPlanner planner;
    public OverlayRenderer overlay;
    public CesiumGeoreference georeference;

    [Header("Planner UX")]
    public float closeSnapMeters = 10f;

    public void Clear()
    {
        if (missionStore != null) missionStore.Clear();
        if (overlay != null) overlay.DrawPath(System.Array.Empty<double3>());
    }

    public void ClosePolygon()
    {
        if (missionStore == null) return;
        missionStore.ClosePolygonWithSnap(closeSnapMeters, georeference);
    }

    public void Generate()
    {
        if (missionStore == null || planner == null || overlay == null || settings == null) return;
        if (missionStore.fieldLonLatHeight.Count < 3) return;

        if (!missionStore.IsClosed)
            missionStore.ClosePolygonWithSnap(closeSnapMeters, georeference);

        var path = planner.Generate(
            missionStore.fieldLonLatHeight,
            settings.spacingMeters,
            settings.lineAngleDeg,
            settings.altitudeAGLMeters
        );

        overlay.DrawPath(path);
        Debug.Log($"Generated path points: {path.Count}");
    }
}