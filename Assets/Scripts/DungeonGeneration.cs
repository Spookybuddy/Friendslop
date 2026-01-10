using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGeneration : MonoBehaviour
{
    public Dungeon dungeon;
    private Transform dungeonTileParent;
    private uint currentSize;
    private List<Transform> openDoorways = new List<Transform>();
    private List<GameObject> destroyDoorways = new List<GameObject>();
    private float giveUp = 0;
    private Coroutine executing;
    private int tileID = 0;

    [Header("Beizer Paths")]
    public int quality = nomialSize;
    public GameObject line;
    private Vector3[] pathwayCoordinates;
    private Vector3[] pathwayDirection;
    private const byte nomialSize = 4;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 3, 3, 1 };
    private Vector3[] doorwayCoordinates = new Vector3[nomialSize];

    public void Start()
    {
        quality = Mathf.Max(quality, nomialSize);
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
            yield return new WaitForEndOfFrame();

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
            bool skip = false;
            while (Vector3.Dot(openDoorways[fromDoor].forward, newDoors[toDoor].forward) >= -dungeon.dotThreshold) {
                newTile.transform.Rotate(dir);
                giveUp += Time.deltaTime;
                if (giveUp > 5) {
                    Debug.LogError($"Could not match rotation :(");
                    newDoors.Clear();
                    Destroy(newTile);
                    skip = true;
                    break;
                }
            }
            if (skip) continue;
            giveUp = 0;
            Vector3 tdward = newDoors[toDoor].position + newDoors[toDoor].forward;
            newTile.transform.position += newTile.transform.position - tdward;
            newTile.transform.SetParent(dungeonTileParent, true);
            newTile.name = $"#{tileID}";

            //Check if overlapping
            Vector3 bounds = transform.localScale;
            if (transform.TryGetComponent<BoxCollider>(out BoxCollider b)) bounds = b.size;
            Collider[] collide = Physics.OverlapBox(newTile.transform.position, bounds, newTile.transform.rotation, 256);
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
            doorwayCoordinates = new Vector3[nomialSize] { openDoorways[fromDoor].position, fdward, tdward, newDoors[toDoor].position };
            Beizer();
            Directionalize(openDoorways[fromDoor].forward, -newDoors[toDoor].forward);
            GameObject path = Instantiate(line, dungeonTileParent);
            if (path.TryGetComponent<LineRenderer>(out LineRenderer render)) {
                path.transform.localEulerAngles = Vector3.right * 90;
                render.positionCount = pathwayCoordinates.Length;
                render.SetPositions(pathwayCoordinates);
                /*
                Mesh mesh = new Mesh();
                render.BakeMesh(mesh);
                if (path.TryGetComponent<MeshCollider>(out MeshCollider meshCollider)) meshCollider.sharedMesh = mesh;
                if (path.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)) meshFilter.sharedMesh = mesh;
                */
            }

            //Remove from lists
            destroyDoorways.Add(openDoorways[fromDoor].gameObject);
            destroyDoorways.Add(newDoors[toDoor].gameObject);
            openDoorways.RemoveAt(fromDoor);
            newDoors.RemoveAt(toDoor);
            openDoorways.AddRange(newDoors);

            currentSize += dungeon.tileset[tileIndex].meterage;
            tileID++;
        }
        for (int i = 1; i < destroyDoorways.Count; i++) Destroy(destroyDoorways[i]);
        Debug.Log($"Generated a dungeon covering {currentSize}m");
    }

    private void Beizer()
    {
        pathwayCoordinates = new Vector3[quality + 1];
        for (int l = 0; l <= quality; l++) {
            float polynomialX = 0, polynomialZ = 0;
            float t = (float)l / quality;
            for (int x = 0; x < nomialSize; x++) {
                float C = binomial[x] * Mathf.Pow(t, x) * Mathf.Pow(1 - t, nomialSize - 1 - x);
                polynomialX += C * doorwayCoordinates[x].x;
                polynomialZ += C * doorwayCoordinates[x].z;
            }
            pathwayCoordinates[l] = new Vector3(polynomialX, 0, polynomialZ);
        }
    }

    private void Directionalize(Vector3 dirStart, Vector3 dirEnd)
    {
        pathwayDirection = new Vector3[quality + 1];
        pathwayDirection[0] = dirStart;
        pathwayDirection[quality] = dirEnd;
        Debug.DrawRay(pathwayCoordinates[0], pathwayDirection[0], Color.blue, 5);
        Debug.DrawRay(pathwayCoordinates[0], Vector3.Cross(pathwayDirection[0], Vector3.up), Color.red, 5);
        Debug.DrawRay(pathwayCoordinates[quality], pathwayDirection[quality], Color.blue, 5);
        Debug.DrawRay(pathwayCoordinates[quality], Vector3.Cross(pathwayDirection[quality], Vector3.up), Color.red, 5);
        for (int i = 1; i < quality; i++) {
            pathwayDirection[i] = (pathwayCoordinates[i + 1] - pathwayCoordinates[i]).normalized;
            Debug.DrawRay(pathwayCoordinates[i], pathwayDirection[i], Color.blue, 5);
            Debug.DrawRay(pathwayCoordinates[i], Vector3.Cross(pathwayDirection[i], Vector3.up), Color.red, 5);
        }
    }
}