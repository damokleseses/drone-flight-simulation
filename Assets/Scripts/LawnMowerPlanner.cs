using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public class LawnMowerPlanner : MonoBehaviour
{
    public CesiumGeoreference georeference;

    void Reset() => georeference = FindFirstObjectByType<CesiumGeoreference>();

    // Intersection includes where on the polygon boundary it occurred (edge + t)
    struct Intersection
    {
        public float x;
        public int edgeIndex; // edge from i -> i+1
        public float t;       // 0..1 along that edge
        public Intersection(float x, int edgeIndex, float t)
        {
            this.x = x; this.edgeIndex = edgeIndex; this.t = t;
        }
    }

    public List<double3> Generate(
        IReadOnlyList<double3> polygonLonLatHeight,
        float spacingMeters,
        float angleDeg,
        float altitudeAGLMeters)
    {
        if (georeference == null) throw new InvalidOperationException("Missing CesiumGeoreference");
        if (polygonLonLatHeight == null) return new List<double3>();

        spacingMeters = Mathf.Max(0.01f, spacingMeters);

        // Copy polygon and remove duplicate closing point if present
        var polyLLH = new List<double3>(polygonLonLatHeight.Count);
        for (int i = 0; i < polygonLonLatHeight.Count; i++) polyLLH.Add(polygonLonLatHeight[i]);
        if (polyLLH.Count >= 4 && NearlySameLLH(polyLLH[0], polyLLH[^1])) polyLLH.RemoveAt(polyLLH.Count - 1);
        if (polyLLH.Count < 3) return new List<double3>();

        // Origin in LLH and ECEF
        double3 originLLH = polyLLH[0];
        double3 originECEF = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(originLLH);

        // ENU basis at origin
        BuildEnuBasis(originLLH, out double3 east, out double3 north, out double3 up);

        // Polygon -> local ENU 2D (E,N) meters
        var polyENU = new List<Vector2>(polyLLH.Count);
        for (int i = 0; i < polyLLH.Count; i++)
        {
            double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(polyLLH[i]);
            double3 d = ecef - originECEF;
            float E = (float)math.dot(d, east);
            float N = (float)math.dot(d, north);
            polyENU.Add(new Vector2(E, N));
        }

        // Degenerate polygon check
        if (Mathf.Abs(SignedArea(polyENU)) < 0.001f) return new List<double3>();

        // Rotate polygon into sweep space (so sweep lines are horizontal)
        float ang = -angleDeg * Mathf.Deg2Rad;
        var polySweep = new List<Vector2>(polyENU.Count);
        for (int i = 0; i < polyENU.Count; i++) polySweep.Add(Rotate(polyENU[i], ang));

        // Precompute perimeter distances for boundary-walking
        BuildPerimeter(polySweep, out float[] cumLen, out float totalLen);

        GetBounds(polySweep, out _, out _, out float minY, out float maxY);
        if (maxY - minY < 0.001f) return new List<double3>();

        // Build sweep segments and connect them along boundary
        var pathSweep = new List<Vector2>();

        bool reverse = false;
        bool hasPrev = false;
        float prevS = 0f;      // perimeter position of previous segment end
        Vector2 prevEnd = default;

        for (float y = minY; y <= maxY + 1e-6f; y += spacingMeters)
        {
            var inter = IntersectionsWithHorizontalLine(polySweep, y);
            if (inter.Count < 2) continue;
            inter.Sort((a, b) => a.x.CompareTo(b.x));

            // Process pairs [0,1], [2,3], ...
            for (int k = 0; k + 1 < inter.Count; k += 2)
            {
                var ia = inter[k];
                var ib = inter[k + 1];

                // Skip tiny segments
                if (Mathf.Abs(ib.x - ia.x) < 0.001f) continue;

                Vector2 A = new Vector2(ia.x, y);
                Vector2 B = new Vector2(ib.x, y);

                // Compute perimeter position (s) for both endpoints
                float sA = PerimeterS(cumLen, polySweep, ia.edgeIndex, ia.t);
                float sB = PerimeterS(cumLen, polySweep, ib.edgeIndex, ib.t);

                Vector2 start = !reverse ? A : B;
                Vector2 end   = !reverse ? B : A;
                float sStart  = !reverse ? sA : sB;
                float sEnd    = !reverse ? sB : sA;

                // If we already have a previous end, connect along boundary
                if (hasPrev)
                {
                    var boundary = BoundaryPath(polySweep, cumLen, totalLen, prevS, sStart);

                    // Append boundary points, but avoid duplicating the first point
                    for (int i = 0; i < boundary.Count; i++)
                    {
                        if (i == 0 && (boundary[i] - prevEnd).sqrMagnitude < 1e-6f) continue;
                        pathSweep.Add(boundary[i]);
                    }
                }
                else
                {
                    // First point in overall path
                    pathSweep.Add(start);
                }

                // Add the sweep segment itself (start->end)
                if ((pathSweep[pathSweep.Count - 1] - start).sqrMagnitude > 1e-6f)
                    pathSweep.Add(start);
                pathSweep.Add(end);

                // Update previous end
                hasPrev = true;
                prevEnd = end;
                prevS = sEnd;

                reverse = !reverse;
            }
        }

        if (pathSweep.Count == 0) return new List<double3>();

        // Rotate back from sweep space -> ENU
        float invAng = -ang;
        for (int i = 0; i < pathSweep.Count; i++)
            pathSweep[i] = Rotate(pathSweep[i], invAng);

        // Convert ENU -> ECEF -> LLH
        double baseHeight = originLLH.z + altitudeAGLMeters;

        var outLonLatHeight = new List<double3>(pathSweep.Count);
        for (int i = 0; i < pathSweep.Count; i++)
        {
            float E = pathSweep[i].x;
            float N = pathSweep[i].y;

            double3 ecef = originECEF + (double)E * east + (double)N * north;
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);

            outLonLatHeight.Add(new double3(llh.x, llh.y, baseHeight));
        }

        return outLonLatHeight;
    }

    // ---------------- helpers ----------------

    static bool NearlySameLLH(double3 a, double3 b)
    {
        const double epsDeg = 1e-9;
        const double epsH = 1e-3;
        return math.abs(a.x - b.x) < epsDeg &&
               math.abs(a.y - b.y) < epsDeg &&
               math.abs(a.z - b.z) < epsH;
    }

    static void BuildEnuBasis(double3 originLLH, out double3 east, out double3 north, out double3 up)
    {
        double lon = math.radians(originLLH.x);
        double lat = math.radians(originLLH.y);

        up = new double3(
            math.cos(lat) * math.cos(lon),
            math.cos(lat) * math.sin(lon),
            math.sin(lat)
        );

        east = new double3(
            -math.sin(lon),
            math.cos(lon),
            0.0
        );

        north = math.cross(up, east);

        east = math.normalize(east);
        north = math.normalize(north);
        up = math.normalize(up);
    }

    static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    static float SignedArea(List<Vector2> pts)
    {
        double sum = 0;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % n];
            sum += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)(0.5 * sum);
    }

    static void GetBounds(List<Vector2> pts, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = minY = float.PositiveInfinity;
        maxX = maxY = float.NegativeInfinity;
        foreach (var p in pts)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
    }

    static void BuildPerimeter(List<Vector2> poly, out float[] cumLen, out float totalLen)
    {
        int n = poly.Count;
        cumLen = new float[n + 1];
        cumLen[0] = 0f;

        float sum = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % n];
            sum += Vector2.Distance(a, b);
            cumLen[i + 1] = sum;
        }
        totalLen = sum;
    }

    static float PerimeterS(float[] cumLen, List<Vector2> poly, int edgeIndex, float t)
    {
        int n = poly.Count;
        int i = edgeIndex;
        int j = (i + 1) % n;

        float edgeLen = Vector2.Distance(poly[i], poly[j]);
        float baseS = cumLen[i];
        return baseS + Mathf.Clamp01(t) * edgeLen;
    }

    // Returns the shorter boundary path between sFrom and sTo
    static List<Vector2> BoundaryPath(List<Vector2> poly, float[] cumLen, float totalLen, float sFrom, float sTo)
    {
        float forwardDist = ForwardDistance(totalLen, sFrom, sTo);
        float backwardDist = totalLen - forwardDist;

        bool goForward = forwardDist <= backwardDist;
        return goForward
            ? BoundaryPathForward(poly, cumLen, totalLen, sFrom, sTo)
            : BoundaryPathForward(poly, cumLen, totalLen, sFrom, sTo + totalLen); // wrap by shifting sTo
    }

    static float ForwardDistance(float totalLen, float sFrom, float sTo)
    {
        if (sTo >= sFrom) return sTo - sFrom;
        return (totalLen - sFrom) + sTo;
    }

    // Build boundary polyline from sFrom to sTo, assuming sTo >= sFrom (can be > totalLen for wrap)
    static List<Vector2> BoundaryPathForward(List<Vector2> poly, float[] cumLen, float totalLen, float sFrom, float sTo)
    {
        int n = poly.Count;
        var pts = new List<Vector2>();

        // Normalize sFrom into [0,totalLen)
        sFrom = Mod(sFrom, totalLen);

        // Allow sTo to be up to totalLen + sFrom (wrap)
        float sToNorm = sTo;

        // Start point
        var start = PointOnPerimeter(poly, cumLen, totalLen, sFrom, out int startEdge, out float startT);
        pts.Add(start);

        float s = sFrom;

        // Walk edges forward until reaching sToNorm
        // We'll advance by visiting vertices.
        int edge = startEdge;

        while (true)
        {
            int nextV = (edge + 1) % n;
            float edgeStartS = cumLen[edge];
            float edgeEndS = edgeStartS + Vector2.Distance(poly[edge], poly[nextV]);

            // current s may be inside this edge
            float currentS = edgeStartS + startT * (edgeEndS - edgeStartS);
            if (ForwardDistance(totalLen, sFrom, Mod(sToNorm, totalLen)) < 1e-5f && Mathf.Abs(Mod(sToNorm, totalLen) - sFrom) < 1e-5f)
            {
                // same point, nothing to do
                break;
            }

            // Target could lie within current edge
            float targetS = sToNorm;
            // If wrapping, compare in "unwrapped" space:
            float currentUnwrapped = UnwrapS(cumLen, totalLen, currentS, sFrom);
            float edgeEndUnwrapped = UnwrapS(cumLen, totalLen, edgeEndS, sFrom);
            float targetUnwrapped = targetS;

            if (targetUnwrapped <= edgeEndUnwrapped + 1e-6f)
            {
                // End lies on this edge
                var end = PointOnEdge(poly[edge], poly[nextV], (targetUnwrapped - currentUnwrapped) / (edgeEndUnwrapped - currentUnwrapped));
                pts.Add(end);
                break;
            }
            else
            {
                // Add next vertex and continue
                pts.Add(poly[nextV]);
                edge = nextV;
                startT = 0f;
                // Update sFrom reference for unwrapping doesn't change; loop continues
                if (pts.Count > n + 5) break; // safety
            }
        }

        return pts;
    }

    static float UnwrapS(float[] cumLen, float totalLen, float s, float sRef)
    {
        // Return s in a space where sRef is baseline (no wrap discontinuity).
        s = Mod(s, totalLen);
        sRef = Mod(sRef, totalLen);
        if (s < sRef) s += totalLen;
        return s;
    }

    static Vector2 PointOnPerimeter(List<Vector2> poly, float[] cumLen, float totalLen, float s, out int edgeIndex, out float t)
    {
        int n = poly.Count;
        s = Mod(s, totalLen);

        // Find edge where cumLen[i] <= s < cumLen[i+1]
        edgeIndex = 0;
        for (int i = 0; i < n; i++)
        {
            float a = cumLen[i];
            float b = cumLen[i + 1];
            if (s >= a && s <= b)
            {
                edgeIndex = i;
                float len = b - a;
                t = (len <= 1e-6f) ? 0f : (s - a) / len;
                int j = (i + 1) % n;
                return Vector2.Lerp(poly[i], poly[j], t);
            }
        }

        // Fallback (shouldn't happen)
        edgeIndex = n - 1;
        t = 1f;
        return poly[0];
    }

    static Vector2 PointOnEdge(Vector2 a, Vector2 b, float t)
    {
        t = Mathf.Clamp01(t);
        return Vector2.Lerp(a, b, t);
    }

    static float Mod(float x, float m)
    {
        float r = x % m;
        return r < 0 ? r + m : r;
    }

    static List<Intersection> IntersectionsWithHorizontalLine(List<Vector2> poly, float y)
    {
        var xs = new List<Intersection>();
        int n = poly.Count;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % n];

            // Ignore horizontal edges
            if (Mathf.Abs(a.y - b.y) < 1e-6f) continue;

            // Half-open to avoid double-count at vertices
            bool cond1 = (a.y <= y && y < b.y);
            bool cond2 = (b.y <= y && y < a.y);
            if (!(cond1 || cond2)) continue;

            float t = (y - a.y) / (b.y - a.y);
            float x = Mathf.Lerp(a.x, b.x, t);

            xs.Add(new Intersection(x, i, t));
        }

        return xs;
    }
}