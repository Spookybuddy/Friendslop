using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public ItemObject[] spawnableItems;

    private void Start()
    {
        for (int i = 0; i < spawnableItems.Length; i++) {
            Vector3 pos = Random.insideUnitCircle * 10;
            (pos.z, pos.y) = (pos.y, pos.z);
            Instantiate(spawnableItems[i].prefab, pos + Vector3.up, Quaternion.identity);
        }
    }
}