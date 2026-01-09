using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGeneration : MonoBehaviour
{
    public Dungeon dungeon;
    public Transform dungeonTileParent;
    public uint currentSize;
    public List<Transform> openDoorways = new List<Transform>();
    public float giveUp = 0;
    public Coroutine executing;
    public int tileID = 0;

    [Header("Beizer Paths")]
    public int quality = 40;
    public GameObject line;
    private Vector3[] points;
    private const byte nomialSize = 4;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 3, 3, 1 };
    private Vector3[] coordinates = new Vector3[nomialSize];

    public void Start()
    {
        dungeonTileParent = Instantiate(new GameObject(), transform).transform;
        Routine();
    }

    [ContextMenu("Generate")]
    public void Routine()
    {
        if (executing != null) StopCoroutine(executing);
        executing = StartCoroutine(Generate());
    }

    public IEnumerator Generate()
    {
        if (dungeon == null) yield break;
        tileID = 0;
        openDoorways.Clear();
        Destroy(dungeonTileParent.gameObject);
        dungeonTileParent = Instantiate(new GameObject(), transform).transform;
        dungeonTileParent.name = "DungeonParent";
        currentSize = 0;
        openDoorways.Add(dungeonTileParent);
        while (currentSize < dungeon.targetSurfaceArea) {
            yield return new WaitForSeconds(0.5f);

            if (openDoorways.Count < 1) {
                Debug.LogWarning($"Ran out of doors after {currentSize}m");
                currentSize = dungeon.targetSurfaceArea;
                break;
            }

            //Pick and spawn a tile from the dungeon's list
            int tileIndex = Random.Range(0, dungeon.tileset.Length);
            int fromDoor = Random.Range(0, openDoorways.Count);
            GameObject newTile = Instantiate(dungeon.tileset[tileIndex].prefab);

            //Find new doorway to connect
            List<Transform> newDoors = new List<Transform>();
            for (int i = 0; i < newTile.transform.childCount; i++) {
                if (newTile.transform.GetChild(i).CompareTag("Doorway")) newDoors.Add(newTile.transform.GetChild(i));
            }
            if (newDoors.Count <= 0) {
                Debug.LogError($"No doorways found in {dungeon.tileset[tileIndex].prefab.name}");
                continue;
            }
            int toDoor = Random.Range(0, newDoors.Count);

            //Apply transforms to new tile
            Vector3 fdward = openDoorways[fromDoor].position + openDoorways[fromDoor].forward;
            newTile.transform.position = openDoorways[fromDoor].position + openDoorways[fromDoor].forward * dungeon.tileset[tileIndex].spawnSpacing;
            newTile.transform.LookAt(openDoorways[fromDoor].position);
            Vector3 dir = Vector3.up * Mathf.Sign(Random.Range(-1, 1));
            while (Vector3.Dot(openDoorways[fromDoor].forward, newDoors[toDoor].forward) >= -dungeon.dotThreshold) {
                newTile.transform.Rotate(dir);
                giveUp += Time.deltaTime;
                if (giveUp > 3) {
                    Debug.LogError($"Could not match rotation :(");
                    giveUp = 0;
                    break;
                }
            }
            giveUp = 0;
            Vector3 tdward = newDoors[toDoor].position + newDoors[toDoor].forward;
            newTile.transform.position += newTile.transform.position - tdward;
            newTile.transform.SetParent(dungeonTileParent, true);
            newTile.name = $"#{tileID}";

            //Check if overlapping
            Vector3 bounds = transform.localScale;
            if (transform.TryGetComponent<BoxCollider>(out BoxCollider b)) bounds = b.size;
            Collider[] collide = Physics.OverlapBox(newTile.transform.position, bounds, newTile.transform.rotation, 256);
            bool skip = false;
            for (int i = 0; i < collide.Length; i++) {
                if (collide[i] != null && collide[i].transform != newTile.transform) {
                    Debug.LogWarning($"#{tileID} overlaped with {collide[i].name}!");
                    newDoors.Clear();
                    Destroy(newTile);
                    skip = true;
                    break;
                }
            }
            if (skip) continue;

            //Pathways
            coordinates = new Vector3[nomialSize] { openDoorways[fromDoor].position, fdward, tdward, newDoors[toDoor].position };
            Beizer();
            GameObject path = Instantiate(line, dungeonTileParent);
            if (path.TryGetComponent<LineRenderer>(out LineRenderer render)) {
                path.transform.localEulerAngles = Vector3.right * 90;
                render.positionCount = points.Length;
                render.SetPositions(points);
            }
            /*
            GameObject path = Instantiate(line, dungeonTileParent);
            if (path.TryGetComponent<LineRenderer>(out LineRenderer render)) {
                render.positionCount = coordinates.Length;
                render.SetPositions(coordinates);
            }
            */

            //Remove from lists
            openDoorways.RemoveAt(fromDoor);
            newDoors.RemoveAt(toDoor);
            openDoorways.AddRange(newDoors);

            currentSize += dungeon.tileset[tileIndex].meterage;
            tileID++;
        }
        for (int i = 0; i < openDoorways.Count; i++) Destroy(openDoorways[i].gameObject);
    }

    private void Beizer()
    {
        points = new Vector3[quality / 2 + 1];
        for (int l = 0; l <= quality / 2; l++) {
            float polynomialX = 0, polynomialZ = 0;
            float t = 2.0f * l / quality;
            for (int x = 0; x < nomialSize; x++) {
                float C = binomial[x] * Mathf.Pow(t, x) * Mathf.Pow(1 - t, nomialSize - 1 - x);
                polynomialX += C * coordinates[x].x;
                polynomialZ += C * coordinates[x].z;
            }
            points[l] = new Vector3(polynomialX, 0, polynomialZ);
        }
    }
}