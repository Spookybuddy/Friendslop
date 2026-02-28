using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "OutsideObject", menuName = "ScriptableObjects/OutsideObject", order = 6)]
public class OutsideObject : ScriptableObject
{
    public GameObject prefab;
    [Tooltip("Add to raycast hit's y")]
    public float groundOffset;
    [Tooltip("Radius of the check when placed")]
    [Range(0.1f, 10)]
    public float spawnRadius = 0.1f;
}

[System.Serializable]
public struct OutsideObjectSettings
{
    public OutsideObject Object;
    [Tooltip("Random position added to the tile for variation")]
    public Vector3 randomVariation;
    [Tooltip("Weighted odds for this tile to spawn")]
    [Range(1, 255)]
    public byte spawnWeight;
    [Tooltip("Chance to spawn when chosen")]
    [Range(1, 255)]
    public byte spawnOdds;
}