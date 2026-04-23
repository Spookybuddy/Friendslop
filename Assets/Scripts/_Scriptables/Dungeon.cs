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
    [Tooltip("How many times to try placing the tile before giving up")]
    [Range(5, 255)]
    public byte tilePlacementAttempts = 5;
    [Tooltip("Sum of the tiles' weights. Can be calculated via the Context Menu (three dots top right)")]
    public int tileWeightSum = 0;
    [Tooltip("The target total meterage of spawned Tiles")]
    [Range(0, 10000)]
    public int targetSurfaceArea;
    [Tooltip("No dungeon meterage can be smaller than this")]
    [Range(0, 10000)]
    public int minimumSurfaceArea;
    [Tooltip("Min variation added to tile rotation")]
    [Range(-128, 127)]
    public sbyte minRotationVariation = 15;
    [Tooltip("Max variation added to tile rotation")]
    [Range(-128, 127)]
    public sbyte maxRotationVariation = 60;

    [Header("Connections")]
    [Tooltip("The path with mesh renderer & collider.")]
    public GameObject pathPrefab;
    [Tooltip("The number of subdivisions along paths.")]
    [Range(6, 24)]
    public int quality = 6;
    [Tooltip("How many meters wide the paths are.")]
    public float pathWidth = 2;
    [Tooltip("How many meters tall the path's collider box.")]
    public float pathHeight = 5;
    [Tooltip("Randomly connect remaining doors after generating.")]
    public bool moreConnections = true;
    [Tooltip("Allow paths to connect to its own tile.")]
    public bool selfConnections = false;
    [Tooltip("Connections with distance < (average spawn spacing * this) will be valid connections.")]
    [Range(1f, 10f)]
    public float distanceMultiplier = 2.2f;
    [Tooltip("Any dot product between doors < this will be valid connections.")]
    [Range(-1f, 1)]
    public float dotLimit = -0.5f;

    [Header("Map")]
    [Tooltip("The prefab for the out of bounds area.")]
    public GameObject chunkPrefab;
    [Tooltip("Additional ring of chunks to spawn, / 2.\nUse only odd numbers pls.")]
    [Range(1, 11)]
    public byte extraChunks = 3; 
    [Tooltip("Perlin noise scale applied to the out of bounds area.")]
    public Vector3 perlinScale;
    [Tooltip("Points away from dungeon bounds to slerp to max perlin noise applied.")]
    [Range(1, 10)]
    public byte slerpDistance = 3;
    [Tooltip("The first point adjacent to dungeon bounds will be 0 before slerping.")]
    public bool extra0Point = true;
    [Tooltip("Decoration spawned in the out of bounds area.")]
    public OutsideObjectSettings[] outsideObjects;
    [Tooltip("Sum of the objects' weights. Can be calculated via the Context Menu (three dots top right)")]
    public int objectWeightSum = 0;
    [Tooltip("The height for the map icons.")]
    public float mapHeight = 50;
    [Tooltip("Possible fog/weather settings to choose from in this dungeon.")]
    public WeatherSettings[] atmospheres;
    [Tooltip("Sum of the weathers' weights. Can be calculated via the Context Menu (three dots top right)")]
    public int atmosWeightSum = 0;
    [Tooltip("Variation to grass color, stored in the default generated vertex color B channel.")]
    public Channel grassColorNoise = (Channel)32;
    [Tooltip("Variation to grass density, stored in the default generated vertex color A channel.")]
    public Channel grassAlphaNoise = (Channel)1042;
    [Tooltip("Vertex channel noise scale. X & Y = perlin. Z = noise. -Z inverts the noise.")]
    public Vector3 channelNoiseScale = Vector3.one;

    [ContextMenu("Calculate Weight Sum")]
    public void SumWeights()
    {
        tileWeightSum = 0;
        atmosWeightSum = 0;
        objectWeightSum = 0;
        for (int i = 0; i < tileset.Length; i++) tileWeightSum += tileset[i].spawnWeight;
        for (int i = 0; i < atmospheres.Length; i++) atmosWeightSum += atmospheres[i].weight;
        for (int i = 0; i < outsideObjects.Length; i++) objectWeightSum += outsideObjects[i].spawnWeight;
        if (minimumSurfaceArea > targetSurfaceArea) (targetSurfaceArea, minimumSurfaceArea) = (minimumSurfaceArea, targetSurfaceArea);
    }

    [System.Flags]
    [System.Serializable]
    public enum Channel
    {
        Harsh = 1,
        Smooth = 2,
        Ceiling = 4,
        Floor = 8,
        Noise = 16,
        Perlin = 32,
        Minimum = 64,
        Maximum = 128,
        Combined = 256,
        Difference = 512,
        Multiplicitive = 1024,
        Normalized = 2048
    }
}