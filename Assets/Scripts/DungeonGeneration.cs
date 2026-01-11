using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGeneration : MonoBehaviour
{
    public Dungeon dungeon;
    public GameObject tileParent;
    private Transform dungeonTileParent;
    private uint currentSize;
    private List<Transform> openDoorways = new List<Transform>();
    private List<GameObject> destroyDoorways = new List<GameObject>();
    private float giveUp = 0;
    private Coroutine executing;
    private int tileID = 0;

    [Header("Beizer Paths")]
    public int quality = nomialSize;
    public float pathWidth = 2;
    public GameObject line;
    private Vector3[] pathwayCoordinates;
    private Vector3[] pathwayDirection;
    private const byte nomialSize = 4;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 3, 3, 1 };
    private Vector3[] doorwayCoordinates = new Vector3[nomialSize];

    [ContextMenu("Generate")]
    public void Start()
    {
        quality = Mathf.Max(quality, nomialSize);
        dungeonTileParent = Instantiate(tileParent, transform).transform;
        Routine();
    }

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
        dungeonTileParent = Instantiate(tileParent, transform).transform;
        dungeonTileParent.name = "DungeonParent";
        currentSize = 0;
        openDoorways.Add(dungeonTileParent);
        while (currentSize < dungeon.targetSurfaceArea) {
            yield return new WaitForEndOfFrame();

            //No more open doorways
            if (openDoorways.Count < 1) {
                Debug.LogWarning($"Ran out of doors after {currentSize}m");
                currentSize = dungeon.targetSurfaceArea;
                break;
            }

            //Pick and spawn a tile from the dungeon's list using weighted spawn
            int tileIndex = 0;
            int fromDoor = Random.Range(0, openDoorways.Count);
            if (dungeon.weightSummation <= 0) dungeon.SumWeights();
            uint desiredWeight = (uint)Random.Range(0, dungeon.weightSummation);
            uint weightSum = 0;
            if (dungeon.tileset.Length > 1) {
                for (int i = 0; i < dungeon.tileset.Length; i++) {
                    weightSum += dungeon.tileset[i].spawnWeight;
                    if (weightSum >= desiredWeight) {
                        tileIndex = i;
                        break;
                    }
                }
            }
            GameObject newTile = Instantiate(dungeon.tileset[tileIndex].tile.prefab);

            //Find new doorway to connect
            List<Transform> newDoors = new List<Transform>();
            for (int i = 0; i < newTile.transform.childCount; i++) {
                if (newTile.transform.GetChild(i).CompareTag("Doorway")) newDoors.Add(newTile.transform.GetChild(i));
            }
            if (newDoors.Count <= 0) {
                Debug.LogError($"No doorways found in {dungeon.tileset[tileIndex].tile.prefab.name}");
                continue;
            }
            int toDoor = Random.Range(0, newDoors.Count);

            //Apply transforms to new tile
            Vector3 fdward = openDoorways[fromDoor].position + openDoorways[fromDoor].forward;
            newTile.transform.position = openDoorways[fromDoor].position + openDoorways[fromDoor].forward * dungeon.tileset[tileIndex].tile.spawnSpacing;
            newTile.transform.LookAt(openDoorways[fromDoor].position);
            Vector3 dir = Vector3.up * Mathf.Sign(Random.Range(-1, 1));
            bool skip = false;

            //Rotate tile to have the new door mostly face the from door
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

            //Setup tile
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

            //Pathways after checking overlap so it doesnt kill itself
            doorwayCoordinates = new Vector3[nomialSize] { openDoorways[fromDoor].position, fdward, tdward, newDoors[toDoor].position };
            Beizer();
            GameObject path = Instantiate(line);
            path.transform.SetParent(newTile.transform, true);
            path.name = $"#{tileID}'s Path";
            //Mesh
            if (path.TryGetComponent<MeshFilter>(out MeshFilter filter)) {
                Mesh m = CreateMesh(openDoorways[fromDoor].forward, -newDoors[toDoor].forward);
                filter.sharedMesh = m;
                if (path.TryGetComponent<MeshCollider>(out MeshCollider collider)) collider.sharedMesh = m;
                if (path.transform.GetChild(0).TryGetComponent<MeshCollider>(out MeshCollider childBounds)) childBounds.sharedMesh = m;
            }

            //Remove from lists
            destroyDoorways.Add(openDoorways[fromDoor].gameObject);
            destroyDoorways.Add(newDoors[toDoor].gameObject);
            openDoorways.RemoveAt(fromDoor);
            newDoors.RemoveAt(toDoor);
            openDoorways.AddRange(newDoors);

            currentSize += dungeon.tileset[tileIndex].tile.meterage;
            tileID++;
        }
        for (int i = 1; i < destroyDoorways.Count; i++) Destroy(destroyDoorways[i]);
        Debug.Log($"Generated a dungeon covering {currentSize}m");
    }

    //Create a curve from door to door
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

    //Create the mesh because I am so smart and cool and awesome :)
    private Mesh CreateMesh(Vector3 dirStart, Vector3 dirEnd)
    {
        //Get point's transform.right values
        pathwayDirection = new Vector3[quality + 1];
        pathwayDirection[0] = dirStart;
        pathwayDirection[quality] = dirEnd;
        for (int i = 1; i < quality; i++) pathwayDirection[i] = (pathwayCoordinates[i + 1] - pathwayCoordinates[i]).normalized;

        //Mesh data
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[quality * 2 + 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] tris = new int[quality * 6];
        float leftSideSum = 0, rightSideSum = 0, left = 0, right = 0;

        //Record points
        for (int i = 0; i <= quality; i++) {
            Debug.DrawRay(pathwayCoordinates[i], pathwayDirection[i], Color.blue, 2);
            pathwayDirection[i] = Vector3.Cross(pathwayDirection[i], Vector3.up) * (pathWidth / 2);
            vertices[i * 2] = pathwayCoordinates[i] + pathwayDirection[i];
            vertices[i * 2 + 1] = pathwayCoordinates[i] - pathwayDirection[i];
            Debug.DrawRay(vertices[i * 2], -pathwayDirection[i], Color.red, 2);
            Debug.DrawRay(vertices[i * 2 + 1], pathwayDirection[i], Color.magenta, 2);
            if (i > 0) {
                leftSideSum += Vector3.Distance(vertices[i * 2], vertices[i * 2 - 2]);
                rightSideSum += Vector3.Distance(vertices[i * 2 + 1], vertices[i * 2 - 1]);
            }
        }

        //Uvs
        for (int i = 0; i <= quality; i++) {
            uvs[i * 2] = new Vector2(0, left / leftSideSum);
            uvs[i * 2 + 1] = new Vector2(1, right / rightSideSum);
            if (i < quality) {
                left += Vector3.Distance(vertices[i * 2], vertices[i * 2 + 2]);
                right += Vector3.Distance(vertices[i * 2 + 1], vertices[i * 2 + 3]);
            }
        }

        //Triangles
        for (int i = 0; i < quality; i++) {
            tris[6 * i] = 2 * i + 2;
            tris[6 * i + 1] = 2 * i + 1;
            tris[6 * i + 2] = 2 * i;
            tris[6 * i + 3] = 2 * i + 2;
            tris[6 * i + 4] = 2 * i + 3;
            tris[6 * i + 5] = 2 * i + 1;
        }

        //Set mesh data
        mesh.vertices = vertices;
        mesh.SetUVs(0, uvs);
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.Optimize();
        return mesh;
    }
}