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

[System.Serializable]
public struct MeshChunk
{
    public Vector3[] vertices;
    public Vector2[] uvs;
    public int[] triangles;
    public Vector3[] normals;
    public Color[] colors;
    public Mesh chunk;
    public MeshChunk(int scale, int tris)
    {
        vertices = new Vector3[scale];
        normals = new Vector3[scale];
        colors = new Color[scale];
        uvs = new Vector2[scale];
        triangles = new int[tris];
        chunk = new Mesh();
    }
    public readonly void Verts(int i, int x, int z, Vector3 pos, Vector3 norm, Color col)
    {
        vertices[i] = pos;
        uvs[i] = new Vector2(x, z);
        normals[i] = norm;
        colors[i] = col;
    }
    public readonly void Create() {
        chunk.vertices = vertices;
        chunk.SetUVs(0, uvs);
        chunk.triangles = triangles;
        chunk.normals = normals;
        chunk.colors = colors;
        //chunk.RecalculateNormals();
        chunk.Optimize();
    }
}

public struct Point
{
    public int x;
    public int z;
    public Point(int x, int z)
    {
        this.x = x;
        this.z = z;
    }
}

public class DungeonGeneration : MonoBehaviour
{
    [Header("Settings")]
    public int seed = 0;
    [Tooltip("The dungeon settings to use")]
    public Dungeon dungeon;
    [Tooltip("The prefab used for the tile parent")]
    public GameObject tileParent;
    [Tooltip("The navigation surface to access and bake")]
    public NavMeshSurface navMeshSurface;
    [Tooltip("Parent object for out of bounds chunks")]
    public Transform terrain;
    [Tooltip("Doorway for the yard")]
    public Transform yardEntrance;
    [HideInInspector]
    public GameObject[] chunks;
    private MeshChunk[] chunkData;
    private int[] setChunkTris;
    private readonly List<Point> points = new List<Point>();
    private Vector3[] yValues;
    private Vector3[] normals;
    private const byte SIZE = 20;
    private const byte SIZE_ = 21;
    private const byte HALF = 10;
    [Header("Stats")]
    private Transform dungeonTileParent;
    public uint currentSize;
    private float avgDist = 10;
    private int tileID = 0;
    [HideInInspector]
    public int atmoID = 0;
    [HideInInspector]
    public float snowRayLength = 0;
    private byte bestTileID = 0;
    private readonly List<GameObject> destroyDoorways = new List<GameObject>();
    private readonly List<TileCheck> validDoorways = new List<TileCheck>();
    public float generationTime = 0;
    public float connectionTime = 0;
    public float totalTime = 0;
    private Coroutine executing;
    public bool dungeonGenerated = false;
    public bool Debugging = false;
    public System.Random rng;
    private int quality;
    private const byte nomialSize = 6;
    private readonly byte[] binomial = new byte[nomialSize] { 1, 5, 10, 10, 5, 1 };
    private Vector3[] pathwayCoordinates;
    private Vector3[] doorwayCoordinates = new Vector3[nomialSize];
    private readonly RaycastHit[] castResults = new RaycastHit[nomialSize];
    [Header("Map")]
    public Camera mapCam;

    //Reset vars and generate
    [ContextMenu("Generate")]
    public void Routine(float recycledTime = 0)
    {
        rng = new System.Random(seed);
        Random.InitState(seed);
        if (dungeonTileParent != null) Destroy(dungeonTileParent.gameObject);
        quality = Mathf.Max(dungeon.quality, nomialSize);
        tileID = 0;
        currentSize = 0;
        generationTime = 0;
        connectionTime = 0;
        totalTime = recycledTime;
        byte doors = 0;
        for (byte i = 0; i < dungeon.tileset.Length; i++) {
            avgDist += dungeon.tileset[i].tile.spawnSpacing;
            if (dungeon.tileset[i].tile.doorCount > doors) bestTileID = i;
        }

        for (byte i = 0; i < chunks.Length; i++) Destroy(chunks[i]);

        //I should expect that the weights are already summed, but people are dumb so I have to cater to the lowest denominator
        dungeon.SumWeights();

        uint desiredWeight = (uint)rng.Next(0, dungeon.atmosWeightSum);
        uint weightSum = 0;
        if (dungeon.atmospheres.Length > 1) {
            for (byte i = 0; i < dungeon.atmospheres.Length; i++) {
                weightSum += dungeon.atmospheres[i].weight;
                if (weightSum >= desiredWeight) {
                    atmoID = i;
                    break;
                }
            }
        }

        avgDist /= dungeon.tileset.Length;
        dungeonGenerated = false;
        validDoorways.Clear();
        destroyDoorways.Clear();
        if (executing != null) StopCoroutine(executing);
        executing = StartCoroutine(Generate());
    }

    //Clear dungeon
    public void ClearDungeon()
    {
        dungeonGenerated = false;
        if (dungeonTileParent != null) Destroy(dungeonTileParent.gameObject);
        for (byte i = 0; i < chunks.Length; i++) Destroy(chunks[i]);
    }

    //Frame delayed generation
    public IEnumerator Generate()
    {
        if (dungeon == null) yield break;
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();

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

        #region Dungeon Generation
        //Tile spawn loop
        while (currentSize < dungeon.targetSurfaceArea) {
            //yield return new WaitForEndOfFrame(); //This was yielding different results everytime, whereas fixed time gives deterministic results
            yield return new WaitForFixedUpdate();
            bool skip = true;

            //No more open doorways
            if (validDoorways.Count < 1) {
                if (Debugging) Debug.LogWarning($"Ran out of doors after {currentSize}m");

                //If the current size is too small, replace the latest tile with the least doors and try again
                if (dungeon.minimumSurfaceArea > currentSize) {
                    Debug.LogWarning($"Bad seed, trying next seed");
                    seed++;
                    watch.Stop();
                    Routine();
                    yield break;
                } else Debug.Log($"Dungeon was large enough");
                break;
            }

            //Pick and spawn a tile from the dungeon's list using weighted spawn
            int tileIndex = 0;
            int fromDoor = rng.Next(0, validDoorways.Count);
            uint desiredWeight = (uint)rng.Next(0, dungeon.tileWeightSum);
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
        generationTime = watch.ElapsedMilliseconds / 1000f;

        //Connect to the yard
        Transform reversed = Instantiate(tileParent, dungeonTileParent).transform;
        reversed.localEulerAngles = new Vector3(0, 180, 0);
        CreatePath(reversed, yardEntrance, dungeonTileParent, 3, "Enter", Vector3.Scale(new Vector3(2.5f, 0, 2.5f), Random.insideUnitSphere));
        
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
                            float f = Mathf.Max(dungeon.pathHeight - dungeon.pathWidth, 1);

                            int hit = Physics.SphereCastNonAlloc(pathwayCoordinates[k] + Vector3.up * f, dungeon.pathWidth / 2, Vector3.down, castResults, f + 0.01f, 256);
                            for (byte s = 0; s < hit; s++) {
                                if (castResults[s].collider.transform.parent.gameObject.Equals(path)) continue;
                                else if (castResults[s].collider.transform.Equals(validDoorways[i].doorway.parent)) continue;
                                else if (castResults[s].collider.transform.Equals(validDoorways[j].doorway.parent)) continue;
                                else {
                                    Debug.LogWarning($"{path.name} overlaps {castResults[s].collider.transform.parent.name}'s {castResults[s].collider.name}");
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
        connectionTime = watch.ElapsedMilliseconds / 1000f - generationTime;

        //Remove door walls
        for (int i = destroyDoorways[0].Equals(dungeonTileParent) ? 1 : 0; i < destroyDoorways.Count; i++) Destroy(destroyDoorways[i]);
        yield return new WaitForFixedUpdate();
        navMeshSurface.BuildNavMesh();
        Debug.Log($"Generated a dungeon covering {currentSize}m");
        #endregion

        //Map
        yield return new WaitForFixedUpdate();
        Vector3 center = navMeshSurface.navMeshData.sourceBounds.center;
        Vector3 extents = navMeshSurface.navMeshData.sourceBounds.extents;
        mapCam.transform.localPosition = new Vector3(center.x, dungeon.mapHeight + 1, center.z);
        mapCam.orthographicSize = Mathf.Max(extents.x, extents.z);
        mapCam.enabled = true;
        yield return new WaitForEndOfFrame();
        mapCam.Render();
        yield return new WaitForEndOfFrame();
        mapCam.enabled = false;

        //Create the forest
        #region Forest
        if (terrain != null) {
            //Reset rng to ensure its the same just in case
            rng = new System.Random(seed);
            Random.InitState(seed);

            //Take the arbitrary dimensions of the navmesh and create chunked terrain to fill it in
            extents = new Vector3(Mathf.Ceil(extents.x) * 2, Mathf.Ceil(extents.y), Mathf.Ceil(extents.z) * 2);
            extents = new Vector3(extents.x + (dungeon.extraChunks * SIZE - 1) - extents.x % SIZE, extents.y, extents.z + (dungeon.extraChunks * SIZE - 1) - extents.z % SIZE);
            center += new Vector3(extents.x / 2, 0, extents.z / 2);
            int X = (int)extents.x, Z = (int)extents.z;
            int X_ = X + 1, X__ = X + 2, Z_ = Z + 1, Z__ = Z + 2;
            int W = Z_ / SIZE;
            setChunkTris = new int[6 * SIZE * SIZE];

            //All chunks share tri patterns, so only one array is needed
            for (int a = 0; a < SIZE; a++) {
                for (int b = 0; b < SIZE; b++) {
                    int index = a * SIZE + b;
                    setChunkTris[6 * index] = index + a;
                    setChunkTris[6 * index + 1] = index + a + 1;
                    setChunkTris[6 * index + 2] = index + a + SIZE + 1;
                    setChunkTris[6 * index + 3] = index + a + 1;
                    setChunkTris[6 * index + 4] = index + a + SIZE + 2;
                    setChunkTris[6 * index + 5] = index + a + SIZE + 1;
                }
            }

            //Chunks
            chunkData = new MeshChunk[X_ / SIZE * Z_ / SIZE];
            chunks = new GameObject[chunkData.Length];
            for (int i = 0; i < chunkData.Length; i++) {
                chunkData[i] = new MeshChunk(SIZE_ * SIZE_, SIZE * SIZE * 6);
                chunkData[i].triangles = setChunkTris;
            }

            //Set heights with an extra row for the edges
            yValues = new Vector3[X__ * Z__];
            normals = new Vector3[yValues.Length];
            for (int x = 0; x < X__; x++) {
                for (int z = 0; z < Z__; z++) {
                    int index = x * Z__ + z;
                    Vector3 pos = new Vector3(x - X, -extents.y, z - Z) + center;
                    pos = terrain.InverseTransformPoint(HitCheck(terrain.TransformPoint(pos), extents.y * 2));
                    if (pos.y > center.y - extents.y) {
                        points.Add(new Point(x, z));
                        yValues[index] = Vector3.up * pos.y;
                    } else yValues[index] = new Vector3(1, pos.y, 1);
                    //X = Ring (negative when set), Y = value, Z = perlin multiplier
                }
            }

            //Update the rest of the heights not set from raycast
            Point checkpoint = points[^1];
            int ring = (dungeon.extra0Point ? -1 : 0);
            float value = 0;
            while (points.Count > 0) {
                //Add adjacent points to list
                int index = points[0].x * Z__ + points[0].z;
                if (index + Z__ < yValues.Length) CheckPoint(Z__, points[0].x + 1, points[0].z, yValues[index].y, value, ring);
                if (index - Z__ > -1) CheckPoint(Z__, points[0].x - 1, points[0].z, yValues[index].y, value, ring);
                if ((index + 1) % Z__ != 0) CheckPoint(Z__, points[0].x, points[0].z + 1, yValues[index].y, value, ring);
                if (index % Z__ != 0) CheckPoint(Z__, points[0].x, points[0].z - 1, yValues[index].y, value, ring);

                //When marked point is matched, get next last point & update value. This gives a ring effect
                if (points[0].Equals(checkpoint)) {
                    checkpoint = points[^1];
                    ring++;
                    if (value < 1) value = Mathf.Clamp01((1 - Mathf.Cos(Mathf.PI * ring / dungeon.slerpDistance)) / 2f);
                }
                points.RemoveAt(0);
            }

            //Add perlin to yvalues
            Vector3 normalyzed = Vector3.forward;
            Vector2 perlinOffset = new Vector2(rng.Next(), rng.Next());
            for (int x = 0; x < X__; x++) {
                for (int z = 0; z < Z__; z++) {
                    int index = x * Z__ + z;
                    yValues[index].y += Mathf.PerlinNoise(dungeon.perlinScale.x * x / X__ + perlinOffset.x, dungeon.perlinScale.z * z / Z__ + perlinOffset.y) * yValues[index].z * dungeon.perlinScale.y;
                    if (yValues[index].y < normalyzed.x) normalyzed.x = yValues[index].y;
                    if (yValues[index].y > normalyzed.y) normalyzed.y = yValues[index].y;
                }
            }
            normalyzed.z = normalyzed.y - normalyzed.x;

            //Calculate normals for whole area
            CalculateNormals(X__, Z__);

            //Set chunk Ys
            for (int x = 0; x < X_; x++) {
                for (int z = 0; z < Z_; z++) {
                    int index = (x / SIZE) * W + (z / SIZE);
                    int id = x * Z__ + z;
                    float ax = (x % SIZE) - HALF;
                    float bz = (z % SIZE) - HALF;
                    if ((x + 1) % SIZE == 0 && x > 0) {
                        chunkData[index].Verts(SIZE * SIZE_ + z % SIZE, x + 1, z, new Vector3(ax + 1, yValues[id + Z__].y, bz), normals[id + Z__], VertexColorMask(id + Z__, x + 1, z, normalyzed));
                        if ((z + 1) % SIZE == 0 && z > 0) chunkData[index].Verts(SIZE * SIZE_ + SIZE, x + 1, z + 1, new Vector3(ax + 1, yValues[id + Z__ + 1].y, bz + 1), normals[id + Z__ + 1], VertexColorMask(id + Z__ + 1, x + 1, z + 1, normalyzed));
                    }
                    if ((z + 1) % SIZE == 0 && z > 0) chunkData[index].Verts(x % SIZE * SIZE_ + SIZE, x, z + 1, new Vector3(ax, yValues[id + 1].y, bz + 1), normals[id + 1], VertexColorMask(id + 1, x, z + 1, normalyzed));
                    chunkData[index].Verts(x % SIZE * SIZE_ + z % SIZE, x, z, new Vector3(ax, yValues[id].y, bz), normals[id], VertexColorMask(id, x, z, normalyzed));
                }
            }
            yield return new WaitForFixedUpdate();

            //Create chunks local
            snowRayLength = 0;
            for (int i = 0; i < chunkData.Length; i++) {
                GameObject g = Instantiate(dungeon.chunkPrefab, terrain);
                if (g.TryGetComponent<MeshFilter>(out MeshFilter mf)){
                    chunkData[i].Create();
                    mf.sharedMesh = chunkData[i].chunk;
                    g.name = $"Chunk #{i + 1}";
                    g.transform.localPosition = new Vector3(SIZE * (i / W) - X + HALF + center.x, 0, SIZE * (i % W) - Z + HALF + center.z);
                    chunks[i] = g;
                    //Collider check
                    if (g.TryGetComponent<MeshCollider>(out MeshCollider mc)) mc.sharedMesh = chunkData[i].chunk;
                    //Sub mesh check
                    if (g.transform.childCount > 0 && g.transform.GetChild(0).TryGetComponent<MeshFilter>(out MeshFilter submesh)) submesh.sharedMesh = chunkData[i].chunk;
                    //Snow check
                    if (g.TryGetComponent<SnowySurface>(out SnowySurface ss)) {
                        snowRayLength = Mathf.Max(snowRayLength, ss.MaxDepth());
                        ss.TileDimensions(SIZE_);
                    }
                    yield return new WaitForEndOfFrame();
                }
            }

            //Populate area with decor using 'voronoi' noise (XOR noise)
            //1 - Mathf.Clamp01(Mathf.Pow(Mathf.Cos((index + x) ^ (index - z) - b), 9))

            yield return new WaitForFixedUpdate();
        }
        #endregion

        //Finished
        dungeonGenerated = true;
        watch.Stop();
        totalTime += watch.ElapsedMilliseconds / 1000f;
        Debug.Log($"{watch.ElapsedMilliseconds}ms");
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
        tile.transform.position += tile.transform.position - WorldForward(to) + Vector3.Scale(Random.insideUnitSphere, dungeon.tileset[index].tile.randomVariation);

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
    private GameObject CreatePath(Transform from, Transform to, Transform parent, float weight, string name = default, Vector3 noise = default)
    {
        doorwayCoordinates = new Vector3[nomialSize] {
            from.position,
            WorldForward(from, weight) + noise,
            Vector3.Lerp(from.position, to.position, 0.375f) + from.forward,
            Vector3.Lerp(from.position, to.position, 0.625f) + to.forward,
            WorldForward(to, weight) - noise,
            to.position
        };
        Bezier();
        GameObject path = Instantiate(dungeon.pathPrefab);
        path.transform.SetParent(parent, true);
        if (name == default) path.name = $"#{tileID}'s Path";
        else path.name = name;
        //Mesh
        if (path.TryGetComponent<MeshFilter>(out MeshFilter filter)) {
            Mesh m = CreatePathMesh(from.forward, -to.forward);
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

    //Moves point up to a hit bounding box
    private Vector3 HitCheck(Vector3 pos, float length)
    {
        if (Physics.Raycast(pos, Vector3.up, out RaycastHit hit, length, 256)) return hit.point - Vector3.up * 0.02f;
        else return pos;
    }

    //Calculate & store normals
    private void CalculateNormals(int X, int Z)
    {
        for (int x = 0; x < X; x++) {
            for (int z = 0; z < Z; z++) {
                int index = x * Z + z;
                bool posZ = (z + 1) % Z != 0;
                bool negZ = z > 0;
                float Y = yValues[index].y;
                //Get normals from the 6 triangles that share this vertex
                if (x > 0) {
                    Vector3 nx = new Vector3(-1, yValues[index - Z].y, 0);
                    if (negZ) normals[index] -= GatherNormals(Y, nx, new Vector3(0, yValues[index - 1].y, -1));
                    if (posZ) {
                        Vector3 shared = new Vector3(-1, yValues[index - Z + 1].y, 1);
                        normals[index] -= GatherNormals(Y, nx, shared);
                        normals[index] -= GatherNormals(Y, new Vector3(0, yValues[index + 1].y, 1), shared);
                    }
                }
                if (x + 1 < X - 1) {
                    Vector3 px = new Vector3(1, yValues[index + Z].y, 0);
                    if (negZ) {
                        Vector3 shared = new Vector3(1, yValues[index + Z - 1].y, -1);
                        normals[index] -= GatherNormals(Y, new Vector3(0, yValues[index - 1].y, -1), shared);
                        normals[index] -= GatherNormals(Y, px, shared);
                    }
                    if (posZ) normals[index] -= GatherNormals(Y, px, new Vector3(0, yValues[index + 1].y, 1));
                }
                normals[index].Normalize();
            }
        }
    }

    //Seamless normals
    private Vector3 GatherNormals(float Y, Vector3 A, Vector3 B)
    {
        A -= Vector3.up * Y;
        B -= Vector3.up * Y;
        return Vector3.Cross(A, B).normalized;
    }

    //Checks if point has already been updated, adding to list if not
    private void CheckPoint(int Z_, int x, int z, float Y, float value, int ring)
    {
        int id = x * Z_ + z;
        if (yValues[id].x < 1) return;
        yValues[id] = new Vector3(-Mathf.Max(ring, 0), Mathf.Max(yValues[id].y, Y), value);
        points.Add(new Point(x, z));
    }

    //Vertex colors assigned using dungeon selected channels
    private Color VertexColorMask(int index, int x, int z, Vector3 normalize)
    {
        Color c = new Color(-1, -1, -1, -1);
        float y = (yValues[index].y - normalize.x) / normalize.z;
        
        //Deconstruct the alpha enum
        float alpha = (float)dungeon.grassAlphaNoise;
        byte a = 0;
        for (int i = 2048; i > 1; i /= 2) {
            if (alpha / i >= 1) {
                alpha %= i;
                c[a] = i;
                a++;
            }
            if (a > 3) break;
        }
        alpha = ColorModifiers(c, index, x, z, y);
        c = new Color(-1, -1, -1, -1);
        
        //Deconstruct the color enum
        float vert = (float)dungeon.grassColorNoise;
        a = 0;
        for (int i = 2048; i > 1; i /= 2) {
            if (vert / i >= 1) {
                vert %= i;
                c[a] = i;
                a++;
            }
            if (a > 3) break;
        }
        vert = ColorModifiers(c, index, x, z, y);

        //RG ignored, B = color noise, A = density noise
        return new Color(0, 0, Mathf.Clamp01(vert), Mathf.Clamp01(alpha));
    }

    //Convert the enums into values using modifiers
    private float ColorModifiers(Color c, int index, int x, int z, float y)
    {
        //Set values
        byte n = (byte)Mathf.Max(-Mathf.Sign(dungeon.channelNoiseScale.z), 0);
        for (byte b = 0; b < 4; b++) {
            c[b] = c[b] switch {
                1 => Mathf.Clamp01(-yValues[index].x),
                2 => yValues[index].z,
                4 => y,
                8 => 1 - y,
                16 => n + Mathf.Sign(dungeon.channelNoiseScale.z) * Mathf.Clamp01(Mathf.Pow(Mathf.Cos((index + x) ^ (index - z) - b), Mathf.Abs(dungeon.channelNoiseScale.z))),
                32 => Mathf.PerlinNoise((float)x * dungeon.channelNoiseScale.x, (float)z * dungeon.channelNoiseScale.y),
                _ => c[b],
            };
        }

        //Apply modifiers
        for (byte b = 0; b < 4; b++) {
            switch (c[b]) {
                case 2048:
                    //normalized
                    break;
                case 1024:
                    //multiply
                    float mult = CCheck(c[0], 1) * CCheck(c[1], 1) * CCheck(c[2], 1) * CCheck(c[3], 1);
                    for (byte t = b; t < 4; t++) {
                        if (c[t] > 32) continue;
                        c[t] = mult;
                    }
                    break;
                case 512:
                    //difference
                    float dif = Mathf.Abs(Mathf.Abs(Mathf.Abs(CCheck(c[0], 0) - CCheck(c[1], 0)) - CCheck(c[2], 0)) - CCheck(c[3], 0));
                    for (byte t = b; t < 4; t++) {
                        if (c[t] > 32) continue;
                        c[t] = dif;
                    }
                    break;
                case 256:
                    //combine
                    float sum = CCheck(c[0], 0) + CCheck(c[1], 0) + CCheck(c[2], 0) + CCheck(c[3], 0);
                    for (byte t = b; t < 4; t++) {
                        if (c[t] > 32) continue;
                        c[t] = sum;
                    }
                    break;
                case 128:
                    //max
                    float max = Mathf.Max(CCheck(c[0], Mathf.NegativeInfinity), CCheck(c[1], Mathf.NegativeInfinity), CCheck(c[2], Mathf.NegativeInfinity), CCheck(c[3], Mathf.NegativeInfinity));
                    for (byte t = b; t < 4; t++) {
                        if (c[t] > 32) continue;
                        c[t] = max;
                    }
                    break;
                case 64:
                    //min
                    float min = Mathf.Min(CCheck(c[0], Mathf.Infinity), CCheck(c[1], Mathf.Infinity), CCheck(c[2], Mathf.Infinity), CCheck(c[3], Mathf.Infinity));
                    for (byte t = b; t < 4; t++) {
                        if (c[t] > 32) continue;
                        c[t] = min;
                    }
                    break;
                default:
                    //non modifier
                    break;
            }
        }
        if (c.a == -1) c.a = c.r;
        return c.a;
    }

    private float CCheck(float input, float returnValue)
    {
        return (input > 32 || input == -1) ? returnValue : input;
    }

    //Create a curve from door to door
    private void Bezier()
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
    private Mesh CreatePathMesh(Vector3 dirStart, Vector3 dirEnd)
    {
        //Get point's transform.right values
        Vector3[] pathwayDirection = new Vector3[quality + 1];
        pathwayDirection[0] = dirStart;
        pathwayDirection[quality] = dirEnd;
        for (int i = 1; i < quality; i++) pathwayDirection[i] = (pathwayCoordinates[i + 1] - pathwayCoordinates[i]).normalized;

        //Seams version
        //Mesh data
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[quality * 5 + 5];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] tris = new int[quality * 24];
        float distanceSum = 0, distance = 0;

        //Record points
        for (int i = 0; i <= quality; i++) {
            Debug.DrawRay(pathwayCoordinates[i], pathwayDirection[i], Color.blue, 2);
            pathwayDirection[i] = Vector3.Cross(pathwayDirection[i], Vector3.up) * (dungeon.pathWidth / 2);
            vertices[i * 5] = pathwayCoordinates[i] + pathwayDirection[i] + Vector3.up * dungeon.pathHeight;
            vertices[i * 5 + 1] = pathwayCoordinates[i] + pathwayDirection[i];
            vertices[i * 5 + 2] = pathwayCoordinates[i] - pathwayDirection[i];
            vertices[i * 5 + 3] = pathwayCoordinates[i] - pathwayDirection[i] + Vector3.up * dungeon.pathHeight;
            vertices[i * 5 + 4] = vertices[i * 5];
            if (i < quality) distanceSum += Vector3.Distance(pathwayCoordinates[i], pathwayCoordinates[i + 1]);
        }

        //Uvs
        for (int i = 0; i <= quality; i++) {
            uvs[i * 5] = new Vector2(-dungeon.pathHeight, distance / distanceSum);
            uvs[i * 5 + 1] = new Vector2(0.001f, distance / distanceSum);
            uvs[i * 5 + 2] = new Vector2(0.999f, distance / distanceSum);
            uvs[i * 5 + 3] = new Vector2(dungeon.pathHeight, distance / distanceSum);
            uvs[i * 5 + 4] = new Vector2(dungeon.pathHeight, distance / distanceSum);
            if (i < quality) distance += Vector3.Distance(pathwayCoordinates[i], pathwayCoordinates[i + 1]);
        }

        //Triangles
        for (int i = 0; i < quality; i++) {
            for (byte j = 0; j < 4; j++) {
                int index = i * 5 + j;
                tris[24 * i + j * 6] = index + 5;
                tris[24 * i + j * 6 + 1] = index + 1;
                tris[24 * i + j * 6 + 2] = index;
                tris[24 * i + j * 6 + 3] = index + 5;
                tris[24 * i + j * 6 + 4] = index + 6;
                tris[24 * i + j * 6 + 5] = index + 1;
            }
        }

        //Set mesh data
        mesh.vertices = vertices;
        mesh.SetUVs(0, uvs);
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.Optimize();
        return mesh;
    }

    //Check for grass mat to change to player settings
    public Material GetGrassMat()
    {
        if (dungeon.chunkPrefab.TryGetComponent<MeshRenderer>(out MeshRenderer mr)) {
            if (mr.sharedMaterial.HasProperty("_GrassLODFade")) {
                Debug.Log($"Grass material");
                return mr.sharedMaterial;
            }
        }
        if (dungeon.chunkPrefab.transform.childCount > 0 && dungeon.chunkPrefab.transform.GetChild(0).TryGetComponent<MeshRenderer>(out MeshRenderer cr)) {
            if (cr.sharedMaterial.HasProperty("_GrassLODFade")) {
                Debug.Log($"Grass material child");
                return cr.sharedMaterial;
            }
        }
        return null;
    }
}