using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Furniture", menuName = "ScriptableObjects/Furniture", order = 7)]
public class Decor : ScriptableObject
{
    [Tooltip("Local position when held by player")]
    public Vector3 holdOffset = Vector3.zero;
    [Tooltip("Local rotation when held by player")]
    public Vector3 holdRotation = Vector3.zero;
    [Tooltip("Local scale when held by player")]
    public Vector3 holdScale = Vector3.one;
    [Tooltip("Ground offset when placed")]
    public float offset = 0;
    [Tooltip("Normals Y limit. Cannot be placed on normals < this")]
    public float minYNormal = 1;
}