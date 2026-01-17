using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Item", order = 4)]
public class ItemObject : ScriptableObject
{
    [Tooltip("Item model")]
    public GameObject prefab;
    [Tooltip("Local position when held by player")]
    public Vector3 holdOffset = Vector3.zero;
    [Tooltip("Local rotation when held by player")]
    public Vector3 holdRotation = Vector3.zero;
    [Tooltip("Local scale when held by player")]
    public Vector3 holdScale = Vector3.one;
    [Tooltip("Carry weight stat")]
    public float weight;
    [Tooltip("Clip played when grabbed. A random one will be selected when multiple clips are provided")]
    public AudioClip[] grabSFX;
    [Tooltip("Clip played when hitting the ground. A random one will be selected when multiple clips are provided")]
    public AudioClip[] dropSFX;
    [Tooltip("Clip played when used. A random one will be selected when multiple clips are provided")]
    public AudioClip[] useSFX;
    [Tooltip("How object falls when it does not have a rigidbody")]
    public AnimationCurve gravityCurve;
}

[System.Serializable]
public struct ItemSpawnSettings
{
    [Tooltip("Item scriptable object")]
    public ItemObject item;
    [Tooltip("Weighted odds for this item to spawn")]
    [Range(0, 255)]
    public byte spawnWeight;
}