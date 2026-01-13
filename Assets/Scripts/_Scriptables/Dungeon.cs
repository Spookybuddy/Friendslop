using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Dungeon", menuName = "ScriptableObjects/Dungeon", order = 2)]
public class Dungeon : ScriptableObject
{
    [Tooltip("The Tiles that can spawn")]
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
    [Tooltip("Randomly connect remaining doors after generating")]
    public bool moreConnections = true;
    [Tooltip("Connections with distance < (average spawn spacing * this) will be valid connections")]
    [Range(1f, 10f)]
    public float distanceMultiplier = 2.2f;
    [Tooltip("Any dot product between doors < this will be valid connections")]
    [Range(-1f, 1)]
    public float dotLimit = -0.5f;

    [ContextMenu("Calculate Weight Sum")]
    public void SumWeights()
    {
        for (int i = 0; i < tileset.Length; i++) weightSummation += tileset[i].spawnWeight;
    }
}