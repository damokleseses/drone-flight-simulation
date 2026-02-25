using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public static class GeoUtilsCesium
{
    public static Vector3 LonLatHeightToUnity(CesiumGeoreference geo, double lonDeg, double latDeg, double h)
    {
        double3 llh = new double3(lonDeg, latDeg, h);
        double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(llh);
        double3 unity = geo.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        return new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
    }

    public static double3 UnityToLonLatHeight(CesiumGeoreference geo, Vector3 unityPos)
    {
        double3 ecef = geo.TransformUnityPositionToEarthCenteredEarthFixed(new double3(unityPos.x, unityPos.y, unityPos.z));
        return CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef); // (lon,lat,height)
    }
}