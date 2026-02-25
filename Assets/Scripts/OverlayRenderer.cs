using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class OverlayRenderer : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public MissionStore missionStore;

    public LineRenderer fieldLine;
    public LineRenderer pathLine;

    public float drapeOffsetMeters = 5f;

    void Reset()
    {
        georeference = FindFirstObjectByType<CesiumGeoreference>();
        missionStore = FindFirstObjectByType<MissionStore>();
    }

    void LateUpdate()
    {
        DrawField();
    }

    void DrawField()
    {
        if (fieldLine == null || georeference == null || missionStore == null) return;

        int n = missionStore.fieldLonLatHeight.Count;
        if (n == 0)
        {
            fieldLine.positionCount = 0;
            return;
        }

        bool closed = missionStore.IsClosed && n >= 3;
        int count = closed ? n + 1 : n;

        fieldLine.positionCount = count;

        for (int i = 0; i < n; i++)
        {
            double3 llh = missionStore.fieldLonLatHeight[i];
            Vector3 p = GeoUtilsCesium.LonLatHeightToUnity(georeference, llh.x, llh.y, llh.z + drapeOffsetMeters);
            fieldLine.SetPosition(i, p);
        }

        if (closed)
        {
            double3 first = missionStore.fieldLonLatHeight[0];
            Vector3 p0 = GeoUtilsCesium.LonLatHeightToUnity(georeference, first.x, first.y, first.z + drapeOffsetMeters);
            fieldLine.SetPosition(n, p0);
        }
    }

    public void DrawPath(IReadOnlyList<double3> pathLonLatHeight)
    {
        if (pathLine == null || georeference == null) return;

        if (pathLonLatHeight == null || pathLonLatHeight.Count == 0)
        {
            pathLine.positionCount = 0;
            return;
        }

        pathLine.positionCount = pathLonLatHeight.Count;
        for (int i = 0; i < pathLonLatHeight.Count; i++)
        {
            double3 llh = pathLonLatHeight[i];
            Vector3 p = GeoUtilsCesium.LonLatHeightToUnity(georeference, llh.x, llh.y, llh.z + drapeOffsetMeters);
            pathLine.SetPosition(i, p);
        }
    }
}