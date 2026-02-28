using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class SnowySurface : MonoBehaviour
{
    [Tooltip("Use the snow shader")]
    public Material snowMat;
    [Tooltip("If true the mesh data will be cloned before modifying")]
    public bool duplicateMesh;
    [Tooltip("How much snow is removed when something moves through")]
    public float carveRate = 2.3f;
    [Tooltip("Distance above to check for colliders")]
    public byte ceilingHeight = 20;
    [Tooltip("When > 0 uses spherecast to get wider area around vertex")]
    public float sphereRadius = 0.125f;
    [Tooltip("When > 0 raycast distance will be used to make a gradient")]
    public byte distanceBlend = 0;
    [Tooltip("What layers to check for ceilings in")]
    public LayerMask layerMask;

    [Header("Manager provided")]
    public float snowfallRate = .2f;
    [Range(0f, 1f)]
    public float snowAlreadyFallen = 0;

    [HideInInspector]
    public float snowMaxDepth;
    private float difference;
    private Mesh mesh = null;
    private Color[] vertexColors;
    private RaycastHit ceil;
    private int A;
    private int B;

    //Start: Gather covering data into R
    //Buildup: Current value is G & B

    void Start()
    {
        //Check if theres a mesh
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshCollider mc = GetComponent<MeshCollider>();
        if (mf.sharedMesh == null || mc.sharedMesh == null || mf.sharedMesh != mc.sharedMesh) {
            Debug.LogError($"{gameObject.name} is missing or has a mismatched mesh!");
            return;
        }
        if (duplicateMesh) {
            mesh = Instantiate(mf.sharedMesh);
            mf.sharedMesh = mesh;
        } else mesh = mf.sharedMesh;
        if (mesh == null) return;

        vertexColors = new Color[mesh.vertices.Length];
        difference = ceilingHeight - distanceBlend;
        snowMaxDepth = MaxDepth();
        carveRate /= snowMaxDepth;

        //Get the raycast results
        for (uint i = 0; i < mesh.vertexCount; i++) {
            Vector3 world = transform.TransformPoint(mesh.vertices[i]);
            bool hit = false;
            float gray = 1;
            if (sphereRadius > 0) {
                if (Physics.SphereCast(world, sphereRadius, Vector3.up, out ceil, ceilingHeight, layerMask)) {
                    gray = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(ceil.point.x, ceil.point.z)) / sphereRadius;
                    hit = true;
                }
            } else if (Physics.Raycast(world, Vector3.up, out ceil, ceilingHeight, layerMask)) hit = true;
            if (hit) Hit(i, ceil.distance, gray);
            else vertexColors[i] = new Color(1, 1, snowAlreadyFallen);
        }

        mesh.SetColors(vertexColors);
    }

    private void Update()
    {
        if (mesh == null) return;
        if (snowfallRate <= 0) return;
        for (int i = 0; i < vertexColors.Length; i++) {
            if (vertexColors[i].r <= 0) continue;
            Color rgb = vertexColors[i];
            vertexColors[i].g = Mathf.Clamp01(rgb.g + rgb.r * snowfallRate * Time.deltaTime);
            vertexColors[i].b = Mathf.Clamp01(rgb.b + rgb.r * snowfallRate * Time.deltaTime);
        }
        mesh.SetColors(vertexColors);
    }

    //When raycast hits
    private void Hit(uint i, float dist, float gray = 1)
    {
        if (distanceBlend > 0) {
            dist = (dist - distanceBlend) / difference;
            dist = Mathf.Max(Mathf.Sign(dist), 0) * ((Mathf.Cos(Mathf.PI * dist) - 1) / -2);
        } else dist = 0;
        dist *= gray;
        vertexColors[i] = new Color(dist, dist * snowAlreadyFallen, dist * snowAlreadyFallen);
    }

    //Update vertices with given indices
    public float Carve(int index, Vector3 bary)
    {
        float ret = 0;
        for (int i = 0; i < 3; i++) {
            int x = mesh.triangles[index + i];
            float snow = carveRate * Time.deltaTime * bary[i];
            Color rgb = vertexColors[x];
            rgb.g = Mathf.Clamp01(vertexColors[x].g - snow);
            rgb.b = Mathf.Clamp01(vertexColors[x].b - snow);
            vertexColors[x] = rgb;
            ret += rgb.g;

            //Check for edges to update adjecent chunks
            Vector3 world = transform.TransformPoint(mesh.vertices[x]);
            Vector3[] rays = new Vector3[4] {world + new Vector3(0.05f, 0.5f, 0.05f), world + new Vector3(-0.05f, 0.5f, 0.05f), world + new Vector3(-0.05f, 0.5f, -0.05f), world + new Vector3(0.05f, 0.5f, -0.05f) };
            if ((int)(mesh.vertices[x].x + A) / 2 % A == 0) EdgeRayCheck(rays, rgb);
            if ((int)(mesh.vertices[x].x + A + 2) / 2 % A == 0) EdgeRayCheck(rays, rgb);
            if ((int)(mesh.vertices[x].z + B) / 2 % B == 0) EdgeRayCheck(rays, rgb);
            if ((int)(mesh.vertices[x].z + B + 2) / 2 % B == 0) EdgeRayCheck(rays, rgb);
        }
        mesh.SetColors(vertexColors);
        return ret / 3;
    }

    //Rays around the edge vertices to check for adjacent chunks
    private void EdgeRayCheck(Vector3[] rays, Color rgb)
    {
        for (byte b = 0; b < 4; b++) {
            if (Physics.Raycast(rays[b], Vector3.down, out RaycastHit hit, 1, 512)) {
                if (hit.collider.gameObject.Equals(gameObject) || hit.collider.transform.Equals(transform)) continue;
                if (hit.collider.TryGetComponent<SnowySurface>(out SnowySurface ss)) ss.UpdateFromAnother(hit.triangleIndex * 3, hit.barycentricCoordinate, rgb);
            }
        }
    }

    //Find matching vertex and update it's colors
    public void UpdateFromAnother(int index, Vector3 bary, Color rgb)
    {
        if (mesh == null) return;
        byte b = 0;
        float max = 0;
        for (byte a = 0; a < 3; a++) {
            if (bary[a] > max) {
                max = bary[a];
                b = a;
            }
        }
        vertexColors[mesh.triangles[index + b]] = rgb;
        mesh.SetColors(vertexColors);
    }

    //Pass tile dimensions to check if edges
    public void TileDimensions(int a, int b)
    {
        A = a;
        B = b;
    }

    //Get max depth
    public float MaxDepth()
    {
        try {
            return snowMat.GetFloat("_Snow_Max_Depth");
        } catch {
            Debug.LogError($"Snow Material {snowMat.name} on {name} is not the correct shader!");
            return -1;
        }
    }
}