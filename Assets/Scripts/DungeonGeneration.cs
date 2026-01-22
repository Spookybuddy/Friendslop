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
    public uint currentSize;
    private float avgDist = 10;
    private int tileID = 0;
    private byte bestTileID = 0;
    private readonly List<GameObject> destroyDoorways = new List<GameObject>();
    private readonly List<TileCheck> validDoorways = new List<TileCheck>();
    public float generationTime = 0;
    private Coroutine executing;
    public bool dungeonGenerated = false;
    public bool Debugging = false;
    public System.Random rng;
    private int quality;
    private const byte nomialSize = 6;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 5, 10, 10, 5, 1 };
    private Vector3[] pathwayCoordinates;
    private Vector3[] doorwayCoordinates = new Vector3[nomialSize];

    [Header("Map")]
    public Camera mapCam;

    public void Start()
    {
        quality = Mathf.Max(dungeon.quality, nomialSize);
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
        byte doors = 0;
        for (byte i = 0; i < dungeon.tileset.Length; i++) {
            avgDist += dungeon.tileset[i].tile.spawnSpacing;
            if (dungeon.tileset[i].tile.doorCount > doors) bestTileID = i;
        }
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

        //Entrance room spawned first
        dungeonTileParent = Instantiate(tileParent, transform).transform;
        dungeonTileParent.name = "DungeonParent";
        if (dungeon.entranceRoom != null) {
            GameObject entrance = Instantiate(dungeon.entranceRoom, dungeonTileParent);
            for (byte i = 0; i < entrance.transform.childCount; i++) {
                Transform t = entrance.transform.GetChild(i);
                if (t.CompareTag("Doorway")) validDoorways.Add(new TileCheck(entrance.transform.GetChild(i), dungeon.tileset.Length));
                if (t.CompareTag("MapIcon")) {
                    t.localPosition = new Vector3(t.localPosition.x, 0, t.localPosition.z);
                    t.position += Vector3.up * (dungeon.mapHeight - entrance.transform.position.y);
                }
            }
            if (validDoorways.Count <= 0) {
                if (Debugging) Debug.LogError($"No doorways found in Entrance {entrance.name}");
                Destroy(entrance);
                validDoorways.Add(new TileCheck(dungeonTileParent, dungeon.tileset.Length));
            } else {
                Vector3 bounds = transform.localScale;
                if (entrance.transform.TryGetComponent<BoxCollider>(out BoxCollider b)) bounds = b.size;
                entrance.transform.localPosition = Vector3.forward * bounds.z / 2;
            }
        } else {
            if (Debugging) Debug.LogWarning($"No Entrance provided, using the Dungeon Tile Parent instead.");
            validDoorways.Add(new TileCheck(dungeonTileParent, dungeon.tileset.Length));
        }

        //Tile spawn loop
        while (currentSize < dungeon.targetSurfaceArea) {
            //yield return new WaitForEndOfFrame(); //This was yielding different results everytime, whereas fixed time gives deterministic results
            yield return new WaitForFixedUpdate();
            bool skip = true;
            generationTime += Time.deltaTime;

            //No more open doorways
            if (validDoorways.Count < 1) {
                if (Debugging) Debug.LogWarning($"Ran out of doors after {currentSize}m");

                //If the current size is too small, replace the latest tile with the least doors and try again
                if (dungeon.minimumSurfaceArea > currentSize) {
                    Debug.LogWarning($"Bad seed, trying next seed");
                    seed++;
                    Routine();
                    yield break;
                } else Debug.Log($"Dungeon was large enough");
                break;
            }

            //Pick and spawn a tile from the dungeon's list using weighted spawn
            int tileIndex = 0;
            int fromDoor = rng.Next(0, validDoorways.Count);
            if (dungeon.weightSummation <= 0) dungeon.SumWeights();
            uint desiredWeight = (uint)rng.Next(0, dungeon.weightSummation);
            uint weightSum = 0;
            if (dungeon.tileset.Length > 1) {
                for (byte i = 0; i < dungeon.tileset.Length; i++) {
                    weightSum += dungeon.tileset[i].spawnWeight;
                    if (weightSum >= desiredWeight) {
                        tileIndex = i;
                        break;
                    }
                }
            }

            //All tiles checked
            for (byte i = 0; i < dungeon.tileset.Length; i++) {
                if (!validDoorways[fromDoor].tilesChecked[i]) skip = false;
            }
            if (skip) {
                if (Debugging) Debug.Log($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} cannot fit any tile. Removed from list");
                validDoorways.RemoveAt(fromDoor);
                continue;
            }

            //Tile picked has already been checked
            if (validDoorways[fromDoor].tilesChecked[tileIndex]) {
                for (byte i = 1; i < dungeon.tileset.Length; i++) {
                    if (!validDoorways[fromDoor].tilesChecked[(tileIndex + i) % dungeon.tileset.Length]) {
                        if (Debugging) Debug.Log($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} cannot fit {dungeon.tileset[tileIndex].tile.prefab.name}, changed to {dungeon.tileset[(tileIndex + i) % 5].tile.prefab.name}");
                        tileIndex = i;
                        break;
                    }
                }
            }

            GameObject newTile = Instantiate(dungeon.tileset[tileIndex].tile.prefab);

            //Find new doorway to connect
            List<TileCheck> newDoors = new List<TileCheck>();
            for (byte i = 0; i < newTile.transform.childCount; i++) {
                if (newTile.transform.GetChild(i).CompareTag("Doorway")) newDoors.Add(new TileCheck(newTile.transform.GetChild(i), dungeon.tileset.Length));
            }
            if (newDoors.Count <= 0) {
                Debug.LogError($"No doorways found in {dungeon.tileset[tileIndex].tile.prefab.name}");
                Destroy(newTile);
                continue;
            }
            int toDoor = rng.Next(0, newDoors.Count);

            //Attempt fitting the tile in multiple times before giving up
            newTile.name = $"#{tileID}";
            for (byte i = 0; i < dungeon.tilePlacementAttempts; i++) {
                skip = false;
                if (ApplyTransforms(newTile.transform, fromDoor, newDoors[toDoor].doorway, tileIndex)) break;
                else skip = true;
            }
            if (skip) {
                if (Debugging) Debug.Log($"{validDoorways[fromDoor].doorway.parent.name}'s {validDoorways[fromDoor].doorway.name} could not fit {dungeon.tileset[tileIndex].tile.prefab.name}");
                newDoors.Clear();
                Destroy(newTile);
                validDoorways[fromDoor].tilesChecked[tileIndex] = true;
                continue;
            }
            newTile.transform.SetParent(validDoorways[fromDoor].doorway.parent, true);

            //Pathways after checking overlap so it doesnt kill itself
            CreatePath(validDoorways[fromDoor].doorway, newDoors[toDoor].doorway, newTile.transform, avgDist / 3);

            //Map edits for icon heights
            for (byte i = 0; i < newTile.transform.childCount; i++) {
                Transform t = newTile.transform.GetChild(i);
                if (t.CompareTag("MapIcon")) {
                    t.localPosition = new Vector3(t.localPosition.x, 0, t.localPosition.z);
                    t.position += Vector3.up * (dungeon.mapHeight - newTile.transform.position.y);
                }
            }

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
                for (int j = i + 1; j < validDoorways.Count; j++) {
                    //Already connected check
                    if (destroyDoorways.Contains(validDoorways[j].doorway.gameObject) || destroyDoorways.Contains(validDoorways[i].doorway.gameObject)) continue;

                    //Self check
                    if (dungeon.selfConnections && validDoorways[i].doorway.parent == validDoorways[j].doorway.parent) continue;

                    //Dot product check
                    float dot = Vector3.Dot(validDoorways[i].doorway.forward, validDoorways[j].doorway.forward);
                    if (dot > dungeon.dotLimit) continue;

                    //Distance checks
                    float dist = Vector3.Distance(validDoorways[i].doorway.position, validDoorways[j].doorway.position);
                    float dist2 = Vector3.Distance(WorldForward(validDoorways[i].doorway), WorldForward(validDoorways[j].doorway));
                    if (dist > avgDist * dungeon.distanceMultiplier || dist2 > dist) continue;

                    //Angle check
                    float theta = Mathf.Abs(Vector3.SignedAngle(validDoorways[i].doorway.forward, -validDoorways[j].doorway.forward, Vector3.up));
                    if (theta <= Mathf.Abs(dungeon.maxRotationVariation) && theta >= Mathf.Abs(dungeon.minRotationVariation)) {
                        GameObject path = CreatePath(validDoorways[i].doorway, validDoorways[j].doorway, dungeonTileParent, avgDist / 2, $"Path {string.Format("{0:0.00}", dist)}m {string.Format("{0:0.00}", dot)}*");
                        
                        //Overlap check - Work on just doing math instead of waiting for physics update
                        bool exit = false;
                        for (int k = 1; k < quality; k++) {
                            //Raycast along path and delete if it overlaps
                            yield return new WaitForFixedUpdate();
                            RaycastHit[] hits = Physics.SphereCastAll(pathwayCoordinates[k] + Vector3.up, dungeon.pathWidth / 2, Vector3.down, dungeon.pathWidth, 256);
                            for (byte l = 0; l < hits.Length; l++) {
                                if (hits[l].collider.transform.parent.gameObject.Equals(path)) continue;
                                else if (hits[l].collider.transform.Equals(validDoorways[i].doorway.parent)) continue;
                                else if (hits[l].collider.transform.Equals(validDoorways[j].doorway.parent)) continue;
                                else {
                                    Debug.LogWarning($"{path.name} overlaps {hits[l].collider.transform.parent.name}'s {hits[l].collider.name}");
                                    Destroy(path);
                                    exit = true;
                                    goto SKIP;
                                }
                            }
                        }
                        SKIP:
                        if (exit) continue;
                        
                        //Mark as used
                        destroyDoorways.Add(validDoorways[i].doorway.gameObject);
                        destroyDoorways.Add(validDoorways[j].doorway.gameObject);
                    }
                }
            }
        }

        //Remove door walls
        for (int i = destroyDoorways[0].Equals(dungeonTileParent) ? 1 : 0; i < destroyDoorways.Count; i++) Destroy(destroyDoorways[i]);
        yield return new WaitForFixedUpdate();
        navMeshSurface.BuildNavMesh();
        dungeonGenerated = true;
        Debug.Log($"Generated a dungeon covering {currentSize}m");

        //Map
        yield return new WaitForFixedUpdate();
        Vector3 center = navMeshSurface.navMeshData.sourceBounds.center;
        mapCam.transform.localPosition = new Vector3(center.x, dungeon.mapHeight + 1, center.z);
        mapCam.orthographicSize = Mathf.Max(navMeshSurface.navMeshData.sourceBounds.extents.x, navMeshSurface.navMeshData.sourceBounds.extents.z);
        mapCam.enabled = true;
        yield return new WaitForEndOfFrame();
        mapCam.Render();
        yield return new WaitForEndOfFrame();
        mapCam.enabled = false;
    }

    //Checks valid spawn a few times
    private bool ApplyTransforms(Transform tile, int from, Transform to, int index)
    {
        //Setup tile
        tile.transform.position = WorldForward(validDoorways[from].doorway, dungeon.tileset[index].tile.spawnSpacing);
        tile.transform.LookAt(validDoorways[from].doorway.position);

        //Rotate tile to face new door towards from door with some random variation
        float variation = Mathf.Sign(rng.Next() % 2 - 1) * rng.Next(dungeon.minRotationVariation, dungeon.maxRotationVariation);
        tile.transform.Rotate(Vector3.down * (Vector3.SignedAngle(tile.transform.forward, to.forward, Vector3.up) + variation));

        //Move tile back a lil bit with noise
        tile.transform.position += tile.transform.position - WorldForward(to) + Random.insideUnitSphere;

        //Check if overlapping
        Vector3 bounds = tile.transform.localScale;
        if (tile.transform.TryGetComponent<BoxCollider>(out BoxCollider b)) bounds = b.size;
        Collider[] collide = Physics.OverlapBox(tile.transform.position, bounds, tile.transform.rotation, 256);
        for (int i = 0; i < collide.Length; i++) {
            if (collide[i] != null && collide[i].transform != tile.transform) return false;
        }
        return true;
    }

    //Get world pos + transform's forward in one call
    private Vector3 WorldForward(Transform trans, float scale = 1)
    {
        return trans.position + trans.forward * scale;
    }

    //Creates the paths from the given inputs
    private GameObject CreatePath(Transform from, Transform to, Transform parent, float weight, string name = default)
    {
        doorwayCoordinates = new Vector3[nomialSize] { from.position, WorldForward(from, weight), Vector3.Lerp(from.position, to.position, 0.375f) + from.forward, Vector3.Lerp(from.position, to.position, 0.625f) + to.forward, WorldForward(to, weight), to.position };
        Beizer();
        GameObject path = Instantiate(dungeon.pathPrefab);
        path.transform.SetParent(parent, true);
        if (name == default) path.name = $"#{tileID}'s Path";
        else path.name = name;
        //Mesh
        if (path.TryGetComponent<MeshFilter>(out MeshFilter filter)) {
            Mesh m = CreateMesh(from.forward, -to.forward);
            filter.sharedMesh = m;
            if (path.TryGetComponent<MeshCollider>(out MeshCollider collider)) collider.sharedMesh = m;
            if (path.transform.GetChild(0).TryGetComponent<MeshCollider>(out MeshCollider childBounds)) childBounds.sharedMesh = m;
            //Map icon
            Transform t = path.transform.GetChild(1);
            if (t.CompareTag("MapIcon")) {
                t.localPosition = new Vector3(t.localPosition.x, 0, t.localPosition.z);
                t.position += Vector3.up * dungeon.mapHeight;
                if (t.TryGetComponent<MeshFilter>(out MeshFilter mapFilter)) mapFilter.sharedMesh = m;
            }
        }
        return path;
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
            pathwayDirection[i] = Vector3.Cross(pathwayDirection[i], Vector3.up) * (dungeon.pathWidth / 2);
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