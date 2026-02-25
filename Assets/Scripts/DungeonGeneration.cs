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
    public Color[] colors;
    public Mesh chunk;
    public MeshChunk(int scale, int tris)
    {
        vertices = new Vector3[scale];
        uvs = new Vector2[scale];
        colors = new Color[scale];
        triangles = new int[tris];
        chunk = new Mesh();
    }
    public void Verts(int i, int x, int z, Vector3 pos)
    {
        vertices[i] = pos;
        uvs[i] = new Vector2(x, z);
        colors[i] = Color.white;
    }
    public void Create() {
        chunk.vertices = vertices;
        chunk.SetUVs(0, uvs);
        chunk.SetColors(colors);
        chunk.triangles = triangles;
        chunk.RecalculateNormals();
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
    [HideInInspector]
    public GameObject[] chunks;
    private MeshChunk[] chunkData;
    private int[] setChunkTris;
    private readonly List<Point> points = new List<Point>();
    private Vector3[] yValues;
    private readonly short[] Primes = new short[] {
          2,   3,   5,   7,  11,  13,  17,  19,  23,  29,  31,  37,  41,  43,  47,  53,  59,  61,  67,  71,  73,  79,  83,  89,  97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149,
        151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317, 331, 337, 347, 349,
        353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503, 509, 521, 523, 541, 547, 557, 563, 569, 571,
        577, 587, 593, 599, 601, 607, 613, 617, 619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709, 719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787, 797, 809,
        811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991, 997
    };
    //1009, 1013, 1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069, 1087, 1091, 1093, 1097, 1103, 1109, 1117, 1123, 1129, 1151, 1153, 1163, 1171, 1181, 1187, 1193, 1201, 1213,
    //1217, 1223, 1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289, 1291, 1297, 1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373, 1381, 1399, 1409, 1423, 1427, 1429, 1433, 1439,
    //1447, 1451, 1453, 1459, 1471, 1481, 1483, 1487, 1489, 1493, 1499, 1511, 1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571, 1579, 1583, 1597, 1601, 1607, 1609, 1613, 1619, 1621, 1627,
    //1637, 1657, 1663, 1667, 1669, 1693, 1697, 1699, 1709, 1721, 1723, 1733, 1741, 1747, 1753, 1759, 1777, 1783, 1787, 1789, 1801, 1811, 1823, 1831, 1847, 1861, 1867, 1871, 1873, 1877,
    //1879, 1889, 1901, 1907, 1913, 1931, 1933, 1949, 1951, 1973, 1979, 1987, 1993, 1997, 1999
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

    public void Start()
    {
        quality = Mathf.Max(dungeon.quality, nomialSize);
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
        connectionTime = 0;
        totalTime = 0;
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
        if (terrain != null) {
            //Reset rng to ensure its the same just in case
            rng = new System.Random(seed);
            Random.InitState(seed);
            //Might want to scrap this in favor of preset chunk sizes simply filling in the area (~20 verts, doubling the verts to 1/m instead of 0.5/m)
            //Take the arbitrary dimensions of the navmesh and create chunked terrain to fill it in
            extents = new Vector3(Mathf.Ceil(extents.x), Mathf.Ceil(extents.y), Mathf.Ceil(extents.z));
            int X = (int)extents.x, Z = (int)extents.z;
            int X_ = X + 1, Z_ = Z + 1;
            int A = 1, B = 1;
            bool setX = false, setZ = false;
            byte px = 0, pz = 0;
            //Exceeds the listed primes, returns set value with precalculated values of reasonable size
            if (X_ > 997) {
                X = 1009;
                X_ = 1010;
                A = 101;
                setX = true;
            }
            if (Z_ > 997) {
                Z = 1009;
                Z_ = 1010;
                B = 101;
                setZ = true;
            }
            //Get common divisor, adjusting if the value is a prime
            while (!(setX && setZ)) {
                if (!setX) {
                    if (Primes[px] > X_) setX = true;
                    if (Primes[px] == X_) {
                        X_++;
                        X++;
                        px = 0;
                    }
                    if (X_ % Primes[px] == 0) A = Primes[px];
                    px++;
                }
                if (!setZ) {
                    if (Primes[pz] > Z_) setZ = true;
                    if (Primes[pz] == Z_) {
                        Z_++;
                        Z++;
                        pz = 0;
                    }
                    if (Z_ % Primes[pz] == 0) B = Primes[pz];
                    pz++;
                }
            }
            A = Mathf.Max(A, X_ / A);
            B = Mathf.Max(B, Z_ / B);
            int A_ = A + 1, B_ = B + 1, X__ = X_ + 1, Z__ = Z_ + 1;
            int D = Z_ / B;
            setChunkTris = new int[6 * A * B];

            //All chunks share tri patterns, so only one array is needed
            for (int a = 0; a < A; a++) {
                for (int b = 0; b < B; b++) {
                    int index = a * B + b;
                    setChunkTris[6 * index] = index + a;
                    setChunkTris[6 * index + 1] = index + a + 1;
                    setChunkTris[6 * index + 2] = index + a + B + 1;
                    setChunkTris[6 * index + 3] = index + a + 1;
                    setChunkTris[6 * index + 4] = index + a + B + 2;
                    setChunkTris[6 * index + 5] = index + a + B + 1;
                }
            }

            //Chunks
            chunkData = new MeshChunk[X_ / A * Z_ / B];
            chunks = new GameObject[chunkData.Length];
            for (int i = 0; i < chunkData.Length; i++) {
                chunkData[i] = new MeshChunk(A_ * B_, A * B * 6);
                chunkData[i].triangles = setChunkTris;
            }

            //Set heights with an extra row for the edges
            yValues = new Vector3[X__ * Z__];
            for (int x = 0; x < X__; x++) {
                for (int z = 0; z < Z__; z++) {
                    int index = x * Z__ + z;
                    Vector3 pos = new Vector3(2 * x - X, -extents.y, 2 * z - Z) + center;
                    pos = terrain.InverseTransformPoint(HitCheck(terrain.TransformPoint(pos), extents.y * 2));
                    if (pos.y > center.y - extents.y) {
                        points.Add(new Point(x, z));
                        yValues[index] = new Vector3(-1, pos.y, 0);
                    } else yValues[index] = new Vector3(1, pos.y, 1);
                    //X = has been set, Y = value, Z = perlin multiplier
                }
            }

            //Update the rest of the heights not set from raycast
            Point checkpoint = points[^1];
            sbyte ring = (sbyte)(dungeon.extra0Point ? -1 : 0);
            float value = 0;
            while (points.Count > 0) {
                //Add adjacent points to list
                int index = points[0].x * Z__ + points[0].z;
                if (index + Z__ < yValues.Length) CheckPoint(Z__, points[0].x + 1, points[0].z, yValues[index].y, value);
                if (index - Z__ > -1) CheckPoint(Z__, points[0].x - 1, points[0].z, yValues[index].y, value);
                if ((index + 1) % Z__ != 0) CheckPoint(Z__, points[0].x, points[0].z + 1, yValues[index].y, value);
                if (index % Z__ != 0) CheckPoint(Z__, points[0].x, points[0].z - 1, yValues[index].y, value);

                //When marked point is matched, get next last point & update value. This gives a ring effect
                if (points[0].Equals(checkpoint) && value < 1) {
                    checkpoint = points[^1];
                    ring++;
                    value = Mathf.Clamp01((1 - Mathf.Cos(Mathf.PI * ring / dungeon.slerpDistance)) / 2f);
                }
                points.RemoveAt(0);
            }

            //Add perlin to yvalues
            for (int x = 0; x < X__; x++) {
                for (int z = 0; z < Z__; z++) {
                    int index = x * Z__ + z;
                    yValues[index].y = yValues[index].y + Mathf.PerlinNoise(dungeon.perlinScale.x * x / X__, dungeon.perlinScale.z * z / Z__) * yValues[index].z * dungeon.perlinScale.y;
                }
            }

            //Set chunk Ys
            for (int x = 0; x < X_; x++) {
                for (int z = 0; z < Z_; z++) {
                    int index = (x / A) * D + (z / B);
                    int id = x * Z__ + z;
                    float ax = 2 * (x % A) - A_;
                    float bz = 2 * (z % B) - B_;
                    if ((x + 1) % A == 0 && x > 0) {
                        chunkData[index].Verts(A * B_ + z % B, x + 1, z, new Vector3(ax + 2, yValues[id + Z__].y, bz));
                        if ((z + 1) % B == 0 && z > 0) chunkData[index].Verts(A * B_ + B, x + 1, z + 1, new Vector3(ax + 2, yValues[id + Z__ + 1].y, bz + 2));
                    }
                    if ((z + 1) % B == 0 && z > 0) chunkData[index].Verts(x % A * B_ + B, x, z + 1, new Vector3(ax, yValues[id + 1].y, bz + 2));
                    chunkData[index].Verts(x % A * B_ + z % B, x, z, new Vector3(ax, yValues[id].y, bz));
                }
            }
            /*
            //This works better using the Z, but still yields poor results. Will need to plan out something both decent looking and optimized. Maybe also reduce the number of loops here
            //Randomly spawn decor
            if (dungeon.outsideObjects.Length > 0) {
                for (int x = 0; x < X__; x++) {
                    for (int z = 0; z < Z__; z++) {
                        int index = x * Z__ + z;
                        if (yValues[index].z < 0 || yValues[index].z == 1) continue;
                        //pick random decor
                        int decor = 0;
                        uint weight = (uint)rng.Next(0, dungeon.objectWeightSum);
                        uint sum = 0;
                        if (dungeon.outsideObjects.Length > 1) {
                            for (byte i = 0; i < dungeon.outsideObjects.Length; i++) {
                                sum += dungeon.tileset[i].spawnWeight;
                                if (sum >= weight) {
                                    decor = i;
                                    break;
                                }
                            }
                        }
                        //Randomly spawn object
                        weight = (uint)rng.Next(0, 255);
                        if (weight > dungeon.outsideObjects[decor].spawnOdds) continue;
                        Vector3 pos = new Vector3(2 * x - X, yValues[index].y + dungeon.outsideObjects[decor].Object.groundOffset - center.y, 2 * z - Z) + center;
                        pos += Vector3.Scale(Random.insideUnitSphere, dungeon.outsideObjects[decor].randomVariation);
                        //Collision check for dungeon bounds
                        if (Physics.SphereCast(new Ray(terrain.TransformPoint(pos), Vector3.down * (dungeon.outsideObjects[decor].Object.groundOffset + 1)), dungeon.outsideObjects[decor].Object.spawnRadius, 256)) continue;
                        GameObject pp = Instantiate(dungeon.outsideObjects[decor].Object.prefab, terrain);
                        pp.transform.localPosition = pos;
                        pp.name = $"{pp.name[..^7]}#{index}";
                    }
                }
            }
            */
            yield return new WaitForFixedUpdate();

            //Create chunks local
            snowRayLength = 0;
            for (int i = 0; i < chunkData.Length; i++) {
                GameObject g = Instantiate(dungeon.chunkPrefab, terrain);
                if (g.TryGetComponent<MeshFilter>(out MeshFilter mf)){
                    chunkData[i].Create();
                    mf.sharedMesh = chunkData[i].chunk;
                    g.name = $"Chunk #{i + 1}";
                }
                if (g.TryGetComponent<MeshCollider>(out MeshCollider mc)) mc.sharedMesh = chunkData[i].chunk;
                if (g.TryGetComponent<SnowySurface>(out SnowySurface ss)) {
                    snowRayLength = Mathf.Max(snowRayLength, ss.MaxDepth());
                    ss.TileDimensions(A_, B_);
                }
                g.transform.localPosition = new Vector3(2 * A * (i / D) - X + A_ + center.x, 0, 2 * B * (i % D) - Z + B_ + center.z);
                chunks[i] = g;
            }

            yield return new WaitForFixedUpdate();
        }

        //Finished
        dungeonGenerated = true;
        watch.Stop();
        totalTime = watch.ElapsedMilliseconds / 1000f;
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
    private GameObject CreatePath(Transform from, Transform to, Transform parent, float weight, string name = default)
    {
        doorwayCoordinates = new Vector3[nomialSize] { from.position, WorldForward(from, weight), Vector3.Lerp(from.position, to.position, 0.375f) + from.forward, Vector3.Lerp(from.position, to.position, 0.625f) + to.forward, WorldForward(to, weight), to.position };
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

    //Checks if point has already been updated, adding to list if not
    private void CheckPoint(int Z_, int x, int z, float Y, float perlin)
    {
        int id = x * Z_ + z;
        if (yValues[id].x < 0) return;
        yValues[id] = new Vector3(-1, Mathf.Max(yValues[id].y, Y), perlin);
        points.Add(new Point(x, z));
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
            uvs[i * 5 + 1] = new Vector2(0.05f, distance / distanceSum);
            uvs[i * 5 + 2] = new Vector2(0.95f, distance / distanceSum);
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
}