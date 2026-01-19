using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Dungeon", menuName = "ScriptableObjects/Dungeon", order = 2)]
public class Dungeon : ScriptableObject
{
    [Header("Tiles")]
    [Tooltip("The Tile spawned first")]
    public GameObject entranceRoom;
    [Tooltip("The Tiles that can spawn and their odds for spawning")]
    public TileWithWeight[] tileset;
    [Tooltip("Sum of the tiles' weights. Can be calculated via the Context Menu (three dots top right)")]
    public int weightSummation = 0;
    [Tooltip("The target total meterage of spawned Tiles")]
    [Range(0, 100000)]
    public int targetSurfaceArea;
    [Tooltip("Min variation added to tile rotation")]
    [Range(-128, 127)]
    public sbyte minRotationVariation = 15;
    [Tooltip("Max variation added to tile rotation")]
    [Range(-128, 127)]
    public sbyte maxRotationVariation = 60;

    [Header("Connections")]
    [Tooltip("The path with mesh renderer & collider")]
    public GameObject pathPrefab;
    [Tooltip("The number of subdivisions along paths")]
    [Range(6, 24)]
    public int quality = 6;
    [Tooltip("How many meters wide the paths are")]
    public float pathWidth = 2;
    [Tooltip("Randomly connect remaining doors after generating")]
    public bool moreConnections = true;
    [Tooltip("Allow paths to connect to its own tile")]
    public bool selfConnections = false;
    [Tooltip("Connections with distance < (average spawn spacing * this) will be valid connections")]
    [Range(1f, 10f)]
    public float distanceMultiplier = 2.2f;
    [Tooltip("Any dot product between doors < this will be valid connections")]
    [Range(-1f, 1)]
    public float dotLimit = -0.5f;

    [Header("Map")]
    [Tooltip("The height for the map icons.")]
    public float mapHeight = 50;

    [ContextMenu("Calculate Weight Sum")]
    public void SumWeights()
    {
        for (int i = 0; i < tileset.Length; i++) weightSummation += tileset[i].spawnWeight;
    }
}