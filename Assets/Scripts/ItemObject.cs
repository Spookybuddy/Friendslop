using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Item", order = 4)]
public class ItemObject : ScriptableObject
{
    public GameObject prefab;
    public float weight;
    public AudioClip grabSFX;
    public AudioClip dropSFX;
    public AudioClip useSFX;
    [Tooltip("How object falls when it does not have a rigidbody")]
    public AnimationCurve gravityCurve;
}