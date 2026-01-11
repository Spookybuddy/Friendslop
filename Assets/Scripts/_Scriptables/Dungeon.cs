using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Dungeon", menuName = "ScriptableObjects/Dungeon", order = 2)]
public class Dungeon : ScriptableObject
{
    [Tooltip("The Tiles that can spawn")]
    public TileWithWeight[] tileset;
    [Tooltip("Sum of the tiles' weights. Can be calculated via the Context Menu (three dots top right)")]
    public uint weightSummation = 0;
    [Tooltip("The target total meterage of spawned Tiles")]
    public uint targetSurfaceArea;
    [Tooltip("Dot product greater than this value allows Tile to spawn")]
    [Range(0f, 1f)]
    public float dotThreshold = 0.9f;

    [ContextMenu("Calculate Weight Sum")]
    public void SumWeights()
    {
        for (int i = 0; i < tileset.Length; i++) weightSummation += tileset[i].spawnWeight;
    }
}