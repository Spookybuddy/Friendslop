using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Tile", menuName = "ScriptableObjects/Tile", order = 3)]
public class Tile : ScriptableObject
{
    [Tooltip("The Tile prefab")]
    public GameObject prefab;
    [Tooltip("The area of the Tile")]
    public byte meterage;
    [Tooltip("The distance of center from previous tile")]
    public float spawnSpacing = 5;
    [Tooltip("Number of doors in Tile")]
    public byte doorCount;
}

[System.Serializable]
public struct TileWithWeight
{
    [Tooltip("Tile scriptable object")]
    public Tile tile;
    [Tooltip("Weighted odds for this tile to spawn")]
    public byte spawnWeight;
}