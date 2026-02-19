using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Fog", menuName = "ScriptableObjects/Fog", order = 5)]
public class FogSettings : ScriptableObject
{
    [Header("Fog Settings")]
    public Color Color;
    public float Density = 1;
    public float Distance = 40;
    public float Blend = 0.5f;
}

[System.Serializable]
public struct WeatherSettings
{
    [Header("Spawn Settings")]
    [Tooltip("The particles to enable with this weather")]
    public Weathers weather;
    [Tooltip("Fog settings that override the default when this weather occurs")]
    public FogSettings fog;
    [Tooltip("The weighted odds of this weather occuring")]
    [Range(0, 255)]
    public byte weight;
}

[System.Serializable]
public enum Weathers
{
    None,
    Rainy,
    Stormy,
    Snowy,
    Blizzard
}