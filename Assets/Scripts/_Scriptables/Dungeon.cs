using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Dungeon", menuName = "ScriptableObjects/Dungeon", order = 2)]
public class Dungeon : ScriptableObject
{
    [Tooltip("The Tiles that can spawn")]
    public Tile[] tileset;
    [Tooltip("The target total meterage of spawned Tiles")]
    public uint targetSurfaceArea;
    [Tooltip("Dot product greater than this value allows Tile to spawn")]
    [Range(0f, 1f)]
    public float dotThreshold = 0.9f;
}