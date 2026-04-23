using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Noise : MonoBehaviour
{
    public MeshChunk chunk;
    [Range(1, 255)]
    public int size;
    public byte power = 1;

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Noise")]
    public void Generate()
    {
        int size_ = size + 1;
        chunk = new MeshChunk(size_ * size_, size * size * 6);

        int[] setChunkTris = new int[6 * size * size];
        for (int a = 0; a < size; a++) {
            for (int b = 0; b < size; b++) {
                int index = a * size + b;
                setChunkTris[6 * index] = index + a;
                setChunkTris[6 * index + 1] = index + a + 1;
                setChunkTris[6 * index + 2] = index + a + size + 1;
                setChunkTris[6 * index + 3] = index + a + 1;
                setChunkTris[6 * index + 4] = index + a + size + 2;
                setChunkTris[6 * index + 5] = index + a + size + 1;
            }
        }
        chunk.triangles = setChunkTris;

        for (int a = 0; a < size_; a++) {
            for (int b = 0; b < size_; b++) {
                int index = a * size_ + b;
                int x = a - size / 2;
                int z = b - size / 2;
                int w = (index + a) ^ (index - b);
                chunk.Verts(index, a, b, new Vector3(x, 0, z), Vector3.up, new Color(Mathf.Pow(Mathf.Cos(w), power), 0, 0));
            }
        }

        chunk.Create();
        chunk.chunk.RecalculateNormals();
        chunk.chunk.Optimize();
        GetComponent<MeshFilter>().sharedMesh = chunk.chunk;
        GetComponent<MeshCollider>().sharedMesh = chunk.chunk;
    }
}