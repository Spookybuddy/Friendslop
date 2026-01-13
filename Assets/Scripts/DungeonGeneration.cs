using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

[System.Serializable]
public struct TileCheck
{
    public Transform doorway;
    public bool[] tilesChecked;
    public TileCheck(Transform t, int s)
    {
        doorway = t;
        tilesChecked = new bool[s];
    }
}

public class DungeonGeneration : MonoBehaviour
{
    public int seed = 0;
    [Tooltip("The dungeon settings to use")]
    public Dungeon dungeon;
    [Tooltip("The prefab used for the tile parent")]
    public GameObject tileParent;
    [Tooltip("The navigation surface to access and bake")]
    public NavMeshSurface navMeshSurface;
    private Transform dungeonTileParent;
    private uint currentSize;
    private float avgDist = 10;
    private int tileID = 0;
    //private List<Transform> openDoorways = new List<Transform>();
    private List<GameObject> destroyDoorways = new List<GameObject>();
    public List<TileCheck> validDoorways = new List<TileCheck>();
    public float generationTime = 0;
    private Coroutine executing;
    public bool dungeonGenerated = false;
    System.Random rng;

    [Header("Beizer Paths")]
    [Tooltip("The number of subdivisions along paths")]
    public int quality = nomialSize;
    [Tooltip("How many meters wide the paths are")]
    public float pathWidth = 2;
    [Tooltip("The path with mesh renderer & collider")]
    public GameObject pathPrefab;
    private const byte nomialSize = 4;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 3, 3, 1 };
    private Vector3[] pathwayCoordinates;
    private Vector3[] doorwayCoordinates = new Vector3[nomialSize];

    public void Start()
    {
        quality = Mathf.Max(quality, nomialSize);
        dungeonTileParent = Instantiate(tileParent, transform).transform;
        Routine();
    }

    //Reset vars and generate
    [ContextMenu("Generate")]
    public void Routine()
    {
        rng = new System.Random(seed);
        Random.InitState(seed);
        if (dungeonTileParent != null) Destroy(dungeonTileParent.gameObject);
        tileID = 0;
        currentSize = 0;
        generationTime = 0;
        for (int i = 0; i < dungeon.tileset.Length; i++) avgDist += dungeon.tileset[i].tile.spawnSpacing;
        avgDist /= dungeon.tileset.Length;
        dungeonGenerated = false;
        validDoorways.Clear();
        destroyDoorways.Clear();
        if (executing != null) StopCoroutine(executing);
        executing = StartCoroutine(Generate());
    }

    //Frame delayed generation
    public IEnumerator Generate()
    {
        if (dungeon == null) yield break;
        dungeonTileParent = Instantiate(tileParent, transform).transform;
        dungeonTileParent.name = "DungeonParent";
        validDoorways.Add(new TileCheck(dungeonTileParent, dungeon.tileset.Length));
        while (currentSize < dungeon.targetSurfaceArea) {
            //yield return new WaitForEndOfFrame(); //This was yielding different results everytime, whereas fixed time gives deterministic results
            yield return new WaitForFixedUpdate();
            bool skip = true;
            generationTime += Time.deltaTime;

            //No more open doorways
            if (validDoorways.Count < 1) {
                Debug.LogWarning($"Ran out of doors after {currentSize}m");
                break;
            }

            //Pick and spawn a tile from the dungeon's list using weighted spawn
            int tileIndex = 0;
            int fromDoor = rng.Next(0, validDoorways.Count);
            if (dungeon.weightSummation <= 0) dungeon.SumWeights();
            uint desiredWeight = (uint)rng.Next(0, dungeon.weightSummation);
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

            //All tiles checked
            for (int i = 0; i < dungeon.tileset.Length; i++) {
                if (!validDoorways[fromDoor].tilesChecked[i]) skip = false;
            }
            if (skip) {
                Debug.Log($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} cannot fit any tile. Removed from list");
                validDoorways.RemoveAt(fromDoor);
                continue;
            }

            //Tile picked has already been checked
            if (validDoorways[fromDoor].tilesChecked[tileIndex]) {
                for (int i = 1; i < dungeon.tileset.Length; i++) {
                    if (!validDoorways[fromDoor].tilesChecked[(tileIndex + i) % dungeon.tileset.Length]) {
                        Debug.Log($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} cannot fit {dungeon.tileset[tileIndex].tile.prefab.name}, changed to {dungeon.tileset[(tileIndex + i) % 5].tile.prefab.name}");
                        tileIndex = i;
                        break;
                    }
                }
            }

            GameObject newTile = Instantiate(dungeon.tileset[tileIndex].tile.prefab);

            //Find new doorway to connect
            List<TileCheck> newDoors = new List<TileCheck>();
            for (int i = 0; i < newTile.transform.childCount; i++) {
                if (newTile.transform.GetChild(i).CompareTag("Doorway")) newDoors.Add(new TileCheck(newTile.transform.GetChild(i), dungeon.tileset.Length));
            }
            if (newDoors.Count <= 0) {
                Debug.LogError($"No doorways found in {dungeon.tileset[tileIndex].tile.prefab.name}");
                Destroy(newTile);
                continue;
            }
            int toDoor = rng.Next(0, newDoors.Count);

            //Attempt fitting the tile in 5 times before giving up
            newTile.name = $"#{tileID}";
            for (int i = 0; i < 5; i++) {
                if (ApplyTransforms(newTile.transform, fromDoor, newDoors[toDoor].doorway, tileIndex)) break;
                else skip = true;
            }
            if (skip) {
                Debug.LogWarning($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} could not fit {dungeon.tileset[tileIndex].tile.prefab.name}");
                newDoors.Clear();
                Destroy(newTile);
                validDoorways[fromDoor].tilesChecked[tileIndex] = true;
                continue;
            }
            newTile.transform.SetParent(dungeonTileParent, true);

            //Pathways after checking overlap so it doesnt kill itself
            CreatePath(validDoorways[fromDoor].doorway, newDoors[toDoor].doorway, newTile.transform);

            //Remove from lists
            destroyDoorways.Add(validDoorways[fromDoor].doorway.gameObject);
            destroyDoorways.Add(newDoors[toDoor].doorway.gameObject);
            validDoorways.RemoveAt(fromDoor);
            newDoors.RemoveAt(toDoor);
            validDoorways.AddRange(newDoors);

            currentSize += dungeon.tileset[tileIndex].tile.meterage;
            tileID++;
        }
        
        //Connect random open doors to other ones nearby
        if (dungeon.moreConnections) {
            for (int i = 0; i < validDoorways.Count; i++) {
                if (destroyDoorways.Contains(validDoorways[i].doorway.gameObject)) continue;
                for (int j = i; j < validDoorways.Count; j++) {
                    float dist = Vector3.Distance(validDoorways[i].doorway.position, validDoorways[j].doorway.position);
                    float dot = Vector3.Dot(validDoorways[i].doorway.forward, validDoorways[j].doorway.forward);
                    if (dot > dungeon.dotLimit) continue;
                    if (dist > avgDist * dungeon.distanceMultiplier) continue;
                    if (validDoorways[i].doorway.parent == validDoorways[j].doorway.parent) continue;
                    if (destroyDoorways.Contains(validDoorways[j].doorway.gameObject) || destroyDoorways.Contains(validDoorways[i].doorway.gameObject)) continue;
                    float theta = Mathf.Abs(Vector3.SignedAngle(validDoorways[i].doorway.forward, -validDoorways[j].doorway.forward, Vector3.up));
                    if (theta <= Mathf.Abs(dungeon.maxRotationVariation) && theta >= Mathf.Abs(dungeon.minRotationVariation)) {
                        CreatePath(validDoorways[i].doorway, validDoorways[j].doorway, dungeonTileParent, $"Path {string.Format("{0:0.00}", dist)}m {string.Format("{0:0.00}", dot)}*");
                        destroyDoorways.Add(validDoorways[i].doorway.gameObject);
                        destroyDoorways.Add(validDoorways[j].doorway.gameObject);
                    }
                }
            }
        }

        //Remove door walls
        for (int i = 1; i < destroyDoorways.Count; i++) Destroy(destroyDoorways[i]);
        navMeshSurface.BuildNavMesh();
        dungeonGenerated = true;
        Debug.Log($"Generated a dungeon covering {currentSize}m");
    }

    //Checks valid spawn a few times
    private bool ApplyTransforms(Transform tile, int from, Transform to, int index)
    {
        //Setup tile
        tile.transform.position = validDoorways[from].doorway.position + validDoorways[from].doorway.forward * dungeon.tileset[index].tile.spawnSpacing;
        tile.transform.LookAt(validDoorways[from].doorway.position);

        //Rotate tile to face new door towards from door with some random variation
        float variation = Mathf.Sign(rng.Next() % 2 - 1) * rng.Next(dungeon.minRotationVariation, dungeon.maxRotationVariation);
        tile.transform.Rotate(Vector3.down * (Vector3.SignedAngle(tile.transform.forward, to.forward, Vector3.up) + variation));

        //Move tile back a lil bit
        Vector3 tdward = to.position + to.forward;
        tile.transform.position += tile.transform.position - tdward + Random.insideUnitSphere;

        //Check if overlapping
        Vector3 bounds = transform.localScale;
        if (tile.transform.TryGetComponent<BoxCollider>(out BoxCollider b)) bounds = b.size;
        Collider[] collide = Physics.OverlapBox(tile.transform.position, bounds, tile.transform.rotation, 256);
        for (int i = 0; i < collide.Length; i++) {
            if (collide[i] != null && collide[i].transform != tile.transform) return false;
        }
        return true;
    }

    //Creates the paths from the given inputs
    private void CreatePath(Transform from, Transform to, Transform parent, string name = default)
    {
        doorwayCoordinates = new Vector3[nomialSize] { from.position, from.position + from.forward * avgDist / 3, to.position + to.forward * avgDist / 3, to.position };
        Beizer();
        GameObject path = Instantiate(pathPrefab);
        path.transform.SetParent(parent, true);
        if (name == default) path.name = $"#{tileID}'s Path";
        else path.name = name;
        //Mesh
        if (path.TryGetComponent<MeshFilter>(out MeshFilter filter)) {
            Mesh m = CreateMesh(from.forward, -to.forward);
            filter.sharedMesh = m;
            if (path.TryGetComponent<MeshCollider>(out MeshCollider collider)) collider.sharedMesh = m;
            if (path.transform.GetChild(0).TryGetComponent<MeshCollider>(out MeshCollider childBounds)) childBounds.sharedMesh = m;
        }
    }

    //Create a curve from door to door
    private void Beizer()
    {
        pathwayCoordinates = new Vector3[quality + 1];
        for (int l = 0; l <= quality; l++) {
            float polynomialX = 0, polynomialY = 0, polynomialZ = 0;
            float t = (float)l / quality;
            for (int x = 0; x < nomialSize; x++) {
                float C = binomial[x] * Mathf.Pow(t, x) * Mathf.Pow(1 - t, nomialSize - 1 - x);
                polynomialX += C * doorwayCoordinates[x].x;
                polynomialY += C * doorwayCoordinates[x].y;
                polynomialZ += C * doorwayCoordinates[x].z;
            }
            pathwayCoordinates[l] = new Vector3(polynomialX, polynomialY, polynomialZ);
        }
    }

    //Create the mesh because I am so smart and cool and awesome :)
    private Mesh CreateMesh(Vector3 dirStart, Vector3 dirEnd)
    {
        //Get point's transform.right values
        Vector3[] pathwayDirection = new Vector3[quality + 1];
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