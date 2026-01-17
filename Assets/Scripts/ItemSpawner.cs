using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    //This script will be reworked once we figure out how we want items to be spawned/distributed
    //Until then this simply serves as the testing for spawning items
    public ItemSpawnSettings[] spawnableItems;
    public int spawnWeightTotal = 0;
    public byte baseItemSpawnCount = 5;
    public sbyte amountVariation = 1;
    private Transform itemStorageParent;

    private void Start()
    {
        itemStorageParent = GameObject.FindGameObjectWithTag("Finish").transform;
        for (int i = 0; i < spawnableItems.Length; i++) spawnWeightTotal += spawnableItems[i].spawnWeight;
        Distribute();
    }

    public void Distribute(int seed = 0)
    {
        Random.InitState(seed);
        int spawn = baseItemSpawnCount + Random.Range(-amountVariation, amountVariation + 1);
        for (int a = 0; a < spawn; a++) {
            int desiredWeight = Random.Range(0, spawnWeightTotal);
            float weightSum = 0;
            if (spawnableItems.Length > 1) {
                for (int i = 0; i < spawnableItems.Length; i++) {
                    weightSum += spawnableItems[i].spawnWeight;
                    if (weightSum >= desiredWeight) {
                        Spawn(i);
                        break;
                    }
                }
            } else Spawn(0);
        }
    }

    private void Spawn(int id)
    {
        Vector3 pos = Random.insideUnitCircle * 10;
        (pos.z, pos.y) = (pos.y, pos.z);
        GameObject item = Instantiate(spawnableItems[id].item.prefab, pos + Vector3.up, Quaternion.identity, itemStorageParent);
        item.name = spawnableItems[id].item.name;
    }
}