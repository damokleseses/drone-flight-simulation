using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public class MissionStore : MonoBehaviour
{
    public readonly List<double3> fieldLonLatHeight = new();
    public bool IsClosed { get; private set; }

    public void Clear()
    {
        fieldLonLatHeight.Clear();
        IsClosed = false;
    }

    public void AddVertex(double lonDeg, double latDeg, double heightMeters)
    {
        if (IsClosed) return;
        fieldLonLatHeight.Add(new double3(lonDeg, latDeg, heightMeters));
    }

    public void ClosePolygonWithSnap(double snapMeters, CesiumGeoreference geo)
    {
        if (IsClosed) return;
        if (fieldLonLatHeight.Count < 3) return;

        // If no georeference, just close by appending start point
        if (geo == null)
        {
            fieldLonLatHeight.Add(fieldLonLatHeight[0]);
            IsClosed = true;
            return;
        }

        // distance in Unity space (meters)
        Vector3 firstU = GeoUtilsCesium.LonLatHeightToUnity(
            geo, fieldLonLatHeight[0].x, fieldLonLatHeight[0].y, fieldLonLatHeight[0].z);

        int lastIndex = fieldLonLatHeight.Count - 1;
        var last = fieldLonLatHeight[lastIndex];

        Vector3 lastU = GeoUtilsCesium.LonLatHeightToUnity(geo, last.x, last.y, last.z);

        if (Vector3.Distance(firstU, lastU) <= snapMeters)
        {
            // Replace last point with first (clean snap closure)
            fieldLonLatHeight[lastIndex] = fieldLonLatHeight[0];
        }
        else
        {
            // Append first point to close polygon cleanly
            fieldLonLatHeight.Add(fieldLonLatHeight[0]);
        }

        IsClosed = true;
    }
}