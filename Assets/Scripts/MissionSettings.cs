using UnityEngine;

[CreateAssetMenu(menuName = "Drone/Mission Settings", fileName = "MissionSettings")]
public class MissionSettings : ScriptableObject
{
    [Min(0.01f)] public float spacingMeters = 2.0f;     // erstmal 2m zum testen
    [Range(0f, 180f)] public float lineAngleDeg = 0f;
    [Min(0.1f)] public float altitudeAGLMeters = 10f;
}