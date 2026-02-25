using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public class SegmentPathRenderer : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public Material lineMaterial;
    public float width = 5f;
    public float drapeOffsetMeters = 5f;

    readonly List<LineRenderer> _pool = new();
    int _used;

    void Reset()
    {
        georeference = FindFirstObjectByType<CesiumGeoreference>();
    }

    public void Clear()
    {
        for (int i = 0; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        _used = 0;
    }

    public void DrawSegments(List<(double3 a, double3 b)> segments)
    {
        if (georeference == null) return;
        Clear();

        foreach (var seg in segments)
        {
            var lr = GetLine();
            lr.positionCount = 2;

            Vector3 p0 = GeoUtilsCesium.LonLatHeightToUnity(georeference, seg.a.x, seg.a.y, seg.a.z + drapeOffsetMeters);
            Vector3 p1 = GeoUtilsCesium.LonLatHeightToUnity(georeference, seg.b.x, seg.b.y, seg.b.z + drapeOffsetMeters);

            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
        }
    }

    LineRenderer GetLine()
    {
        if (_used < _pool.Count)
        {
            var lr = _pool[_used++];
            lr.gameObject.SetActive(true);
            return lr;
        }

        var go = new GameObject($"SegLine_{_pool.Count}");
        go.transform.SetParent(transform, false);

        var newLr = go.AddComponent<LineRenderer>();
        newLr.useWorldSpace = true;
        newLr.alignment = LineAlignment.View;
        newLr.startWidth = width;
        newLr.endWidth = width;
        newLr.material = lineMaterial;

        _pool.Add(newLr);
        _used++;
        return newLr;
    }
}