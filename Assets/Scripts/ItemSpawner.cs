using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public ItemSpawnSettings[] spawnableItems;
    public int spawnWeightTotal = 0;
    public byte baseItemSpawnCount = 5;
    public sbyte amountVariation = 1;

    private void Start()
    {
        for (int i = 0; i < spawnableItems.Length; i++) spawnWeightTotal += spawnableItems[i].spawnWeight;
        Spawn();
    }

    private void Spawn()
    {
        int spawn = baseItemSpawnCount + Random.Range(-amountVariation, amountVariation + 1);
        for (int a = 0; a < spawn; a++) {
            int desiredWeight = Random.Range(0, spawnWeightTotal);
            float weightSum = 0;
            if (spawnableItems.Length > 1) {
                for (int i = 0; i < spawnableItems.Length; i++) {
                    weightSum += spawnableItems[i].spawnWeight;
                    if (weightSum >= desiredWeight) {
                        Vector3 pos = Random.insideUnitCircle * 10;
                        (pos.z, pos.y) = (pos.y, pos.z);
                        Instantiate(spawnableItems[i].item.prefab, pos + Vector3.up, Quaternion.identity);
                        break;
                    }
                }
            } else {
                Vector3 pos = Random.insideUnitCircle * 10;
                (pos.z, pos.y) = (pos.y, pos.z);
                Instantiate(spawnableItems[0].item.prefab, pos + Vector3.up, Quaternion.identity);
            }
        }
    }
}